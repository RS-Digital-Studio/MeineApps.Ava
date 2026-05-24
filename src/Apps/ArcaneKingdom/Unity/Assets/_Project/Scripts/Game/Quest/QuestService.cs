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

namespace ArcaneKingdom.Game.Quest
{
    /// <summary>
    /// Quest-Tracking: nimmt Events vom Game-Layer entgegen und matcht sie gegen alle
    /// aktiven Quest-Definitionen. Belohnungs-Auszahlung erfolgt bei Claim.
    /// </summary>
    public sealed class QuestService
    {
        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;
        private readonly IReadOnlyList<QuestDefinition> _allDefinitions;
        private readonly Dictionary<string, QuestProgress> _progress = new();

        public QuestService(ISaveService<PlayerSave> save, IAnalyticsService analytics,
                            IReadOnlyList<QuestDefinition> allDefinitions)
        {
            _save = save;
            _analytics = analytics;
            _allDefinitions = allDefinitions;
        }

        public IReadOnlyDictionary<string, QuestProgress> Progress => _progress;

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
            await _save.MutateAsync(save =>
            {
                foreach (var r in def.Rewards) ApplyReward(save, r);
                return save;
            }, ct);
            _analytics.Track("quest_claimed", new Dictionary<string, object> { ["quest_id"] = questId });
            return Result.Success();
        }

        public void ResetDaily()
        {
            foreach (var def in _allDefinitions)
                if (def.Period == QuestPeriod.Daily)
                    _progress[def.Id] = new QuestProgress(def.Id);
            GameLogger.Info("Quest", "Daily-Quests zurueckgesetzt.");
        }

        public void ResetWeekly()
        {
            foreach (var def in _allDefinitions)
                if (def.Period == QuestPeriod.Weekly)
                    _progress[def.Id] = new QuestProgress(def.Id);
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
            p.Advance(delta, def.TargetCount);
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
