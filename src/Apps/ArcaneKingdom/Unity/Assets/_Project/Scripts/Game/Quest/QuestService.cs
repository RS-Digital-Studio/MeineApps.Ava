#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Economy;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Quest;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcaneKingdom.Game.Quest
{
    /// <summary>
    /// Quest-Tracking: nimmt Events vom Game-Layer entgegen und matcht sie gegen alle
    /// aktiven Quest-Definitionen. Belohnungs-Auszahlung erfolgt bei Claim.
    /// </summary>
    public sealed class QuestService
    {
        private const string QuestsResourcePath = "Data/quests";

        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;
        private readonly List<QuestDefinition> _allDefinitions = new();
        private readonly Dictionary<string, QuestProgress> _progress = new();
        private bool _restored;
        private bool _dirty;

        public QuestService(ISaveService<PlayerSave> save, IAnalyticsService analytics)
        {
            _save = save;
            _analytics = analytics;
            LoadDefinitionsFromResources();
        }

        /// <summary>Test-Konstruktor mit explizit übergebenen Definitions (umgeht Resource-Load).</summary>
        public QuestService(ISaveService<PlayerSave> save, IAnalyticsService analytics,
                            IReadOnlyList<QuestDefinition> allDefinitions)
        {
            _save = save;
            _analytics = analytics;
            _allDefinitions.AddRange(allDefinitions);
        }

        public IReadOnlyDictionary<string, QuestProgress> Progress => _progress;

        /// <summary>Alle bekannten Quest-Definitionen (für UI-Listing).</summary>
        public IReadOnlyList<QuestDefinition> AllDefinitions => _allDefinitions;

        /// <summary>
        /// Stellt den Quest-Fortschritt aus dem geladenen Save wieder her. MUSS beim App-Start
        /// (nach Save-Load, vor erstem Quest-UI) genau einmal aufgerufen werden, sonst startet jeder
        /// Lauf bei 0. Idempotent (laeuft nur beim ersten Aufruf).
        /// </summary>
        public void RestoreFromSave(PlayerSave save)
        {
            if (_restored) return;
            _restored = true;
            var slice = save.Quests;
            if (slice == null) return;
            foreach (var def in _allDefinitions)
            {
                var count = slice.CountByQuestId.TryGetValue(def.Id, out var c) ? c : 0;
                var claimed = slice.ClaimedQuestIds.Contains(def.Id);
                if (count == 0 && !claimed) continue;
                _progress[def.Id] = new QuestProgress(def.Id, count, claimed, def.TargetCount);
            }
        }

        /// <summary>Schreibt den aktuellen In-Memory-Quest-Zustand in die Save-Slice (ohne zu persistieren).</summary>
        public void PersistTo(PlayerSave save)
        {
            save.Quests ??= new ArcaneKingdom.Domain.Save.QuestSaveSlice();
            var slice = save.Quests;
            foreach (var kv in _progress)
            {
                slice.CountByQuestId[kv.Key] = kv.Value.CurrentCount;
                // Claimed-Status synchronisieren: ein Reset (neuer QuestProgress) hebt ihn wieder auf,
                // damit Daily/Weekly-Quests nach dem Reset erneut eingeloest werden koennen.
                if (kv.Value.RewardClaimed) slice.ClaimedQuestIds.Add(kv.Key);
                else slice.ClaimedQuestIds.Remove(kv.Key);
            }
        }

        /// <summary>
        /// Persistiert den Quest-Zustand, wenn seit dem letzten Flush etwas geaendert wurde.
        /// Beim Hub-Eintritt / Battle-Ende / App-Pause aufrufen, um Advance-Fortschritt zu sichern.
        /// </summary>
        public async UniTask FlushAsync(CancellationToken ct = default)
        {
            if (!_dirty) return;
            _dirty = false;
            await _save.MutateAsync(save => { PersistTo(save); return save; }, ct);
        }

        /// <summary>
        /// Liefert den aktuellen Progress für eine Quest. Wenn noch nie advanced wurde,
        /// gibt es einen leeren QuestProgress zurück (count = 0).
        /// </summary>
        public QuestProgress GetProgress(string questId)
        {
            return _progress.TryGetValue(questId, out var p) ? p : new QuestProgress(questId);
        }

        private void LoadDefinitionsFromResources()
        {
            var asset = Resources.Load<TextAsset>(QuestsResourcePath);
            if (asset == null)
            {
                GameLogger.Warning("Quest", $"Resources/{QuestsResourcePath}.json nicht gefunden.");
                return;
            }
            try
            {
                var loaded = JsonConvert.DeserializeObject<List<QuestDefinition>>(asset.text);
                if (loaded != null) _allDefinitions.AddRange(loaded);
                GameLogger.Info("Quest", $"{_allDefinitions.Count} Quest-Definitionen geladen.");
            }
            catch (Exception ex)
            {
                GameLogger.Error("Quest", "Quest-Deserialisierung fehlgeschlagen", ex);
            }
        }

        public void OnBattleWon() => Advance(QuestObjectiveType.WinBattles, 1);
        public void OnArenaMatchWon() => Advance(QuestObjectiveType.WinArenaMatches, 1);
        public void OnThiefAttacked() => Advance(QuestObjectiveType.AttackThieves, 1);
        public void OnBossDefeated() => Advance(QuestObjectiveType.BeatBosses, 1);
        public void OnLoginDay() => Advance(QuestObjectiveType.LoginDays, 1);

        public void OnCardPlayed(Element element)
        {
            foreach (var def in _allDefinitions)
            {
                if (def.Objective != QuestObjectiveType.PlayCardsOfElement) continue;
                if (def.FilterElement != null && def.FilterElement != element.ToString()) continue;
                AdvanceOne(def, 1);
            }
        }

        public void OnDamageDealt(long damage) => Advance(QuestObjectiveType.DealDamage, (int)Math.Min(damage, int.MaxValue));
        public void OnWorldStarsEarned(int newStars) => Advance(QuestObjectiveType.ReachWorldStars, newStars);
        public void OnGuildPointsDonated(int amount) => Advance(QuestObjectiveType.DonateGuildPoints, amount);
        public void OnDiamondsSpent(int amount) => Advance(QuestObjectiveType.SpendDiamonds, amount);

        public async UniTask<Result> ClaimAsync(string questId, CancellationToken ct = default)
        {
            if (!_progress.TryGetValue(questId, out var progress) || !progress.Completed)
                return Result.Failure("Quest nicht abgeschlossen.");
            QuestDefinition? def = null;
            foreach (var d in _allDefinitions) if (d.Id == questId) { def = d; break; }
            if (def == null) return Result.Failure("Quest unbekannt.");

            if (!progress.TryClaim()) return Result.Failure("Bereits eingeloest.");
            var claimed = false;
            await _save.MutateAsync(save =>
            {
                // Re-Claim-Schutz ueber Neustart/Reset hinweg: persistierten Claimed-Status pruefen.
                save.Quests ??= new ArcaneKingdom.Domain.Save.QuestSaveSlice();
                if (save.Quests.ClaimedQuestIds.Contains(questId)) return save;   // schon ausgezahlt
                foreach (var r in def.Rewards) ApplyReward(save, r);
                PersistTo(save);                                  // Counts + Claimed-Flag mitschreiben
                save.Quests.ClaimedQuestIds.Add(questId);
                claimed = true;
                return save;
            }, ct);
            if (!claimed) return Result.Failure("Bereits eingeloest.");
            _dirty = false;
            _analytics.Track("quest_claimed", new Dictionary<string, object> { ["quest_id"] = questId });
            return Result.Success();
        }

        public void ResetDaily()
        {
            foreach (var def in _allDefinitions)
                if (def.Period == QuestPeriod.Daily)
                    _progress[def.Id] = new QuestProgress(def.Id);
            _dirty = true;
            GameLogger.Info("Quest", "Daily-Quests zurueckgesetzt.");
        }

        public void ResetWeekly()
        {
            foreach (var def in _allDefinitions)
                if (def.Period == QuestPeriod.Weekly)
                    _progress[def.Id] = new QuestProgress(def.Id);
            _dirty = true;
            GameLogger.Info("Quest", "Weekly-Quests zurueckgesetzt.");
        }

        private void Advance(QuestObjectiveType objective, int delta)
        {
            foreach (var def in _allDefinitions)
                if (def.Objective == objective)
                    AdvanceOne(def, delta);
        }

        private void AdvanceOne(QuestDefinition def, int delta)
        {
            if (!_progress.TryGetValue(def.Id, out var p))
            {
                p = new QuestProgress(def.Id);
                _progress[def.Id] = p;
            }
            var before = p.CurrentCount;
            p.Advance(delta, def.TargetCount);
            if (p.CurrentCount != before) _dirty = true;   // beim naechsten FlushAsync sichern
        }

        private static void ApplyReward(PlayerSave save, QuestReward reward)
        {
            switch (reward.Type)
            {
                case "Currency":
                    switch (reward.SubType)
                    {
                        case nameof(Currency.Gold): save.Currencies.AddGold(reward.Amount); break;
                        case nameof(Currency.Diamond): save.Currencies.AddDiamond(reward.Amount); break;
                        case nameof(Currency.UniversalScraps): save.Currencies.AddUniversalScraps(reward.Amount); break;
                        case nameof(Currency.MeritPoints): save.Currencies.AddMeritPoints(reward.Amount); break;
                    }
                    break;
                case "Scrap":
                    if (Enum.TryParse<ScrapType>(reward.SubType, out var scrap))
                        save.Currencies.AddScraps(scrap, reward.Amount);
                    break;
                // TODO MVP: "Card" / "Rune" via Konkrete Item-Award-Methode
            }
        }
    }
}
