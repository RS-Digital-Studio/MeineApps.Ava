#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.World;
using ArcaneKingdom.Game.Catalog;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.World
{
    /// <summary>
    /// Application-Layer-Wrapper fuer den Domain-PrestigeService (Designplan v4 Oeko Kap. 6).
    /// Aufgaben:
    ///   - Welt-Upgrade-Flow (Gold-Abzug + Sterne-Reset + Prestige-IV-Karten-Drop)
    ///   - Daily-Income-Tick (passive Gold-Belohnung pro Welt × Prestige-Multiplikator)
    ///   - Analytics-Tracking
    /// </summary>
    public sealed class PrestigeAppService
    {
        private readonly PrestigeService _domain;
        private readonly CardCatalogService _catalog;
        private readonly WorldCatalogService _worldCatalog;
        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;

        public PrestigeAppService(
            PrestigeService domain,
            CardCatalogService catalog,
            WorldCatalogService worldCatalog,
            ISaveService<PlayerSave> save,
            IAnalyticsService analytics)
        {
            _domain = domain;
            _catalog = catalog;
            _worldCatalog = worldCatalog;
            _save = save;
            _analytics = analytics;
        }

        // ============================================================================
        // Welt-Upgrade
        // ============================================================================

        /// <summary>
        /// Prueft ob ein Prestige-Upgrade fuer die Welt moeglich ist.
        /// </summary>
        public Result CanUpgrade(string worldId, PlayerSave save)
        {
            var currentStufe = save.Prestige.Get(worldId);
            if (!save.WorldProgress.TryGetValue(worldId, out var progress))
                return Result.Failure($"Welt '{worldId}' nicht freigeschaltet.");

            var nodeStars = new Dictionary<string, int>(progress.StarsByNodeId);
            return _domain.CanUpgradePrestige(currentStufe, nodeStars, save.Currencies.Gold);
        }

        /// <summary>
        /// Fuehrt das Prestige-Upgrade durch: Gold-Abzug, Sterne-Reset, Karten-Drop bei IV.
        /// </summary>
        public async UniTask<Result<PrestigeUpgradeOutcome>> ApplyUpgradeAsync(string worldId, CancellationToken ct = default)
        {
            var saveR = await _save.LoadAsync(ct);
            if (!saveR.IsSuccess || saveR.Value == null)
                return Result<PrestigeUpgradeOutcome>.Failure(saveR.ErrorMessage ?? "Save nicht geladen");
            var save = saveR.Value;

            var can = CanUpgrade(worldId, save);
            if (!can.IsSuccess)
                return Result<PrestigeUpgradeOutcome>.Failure(can.ErrorMessage ?? "Upgrade nicht moeglich");

            var oldStufe = save.Prestige.Get(worldId);
            var newStufe = _domain.NextStufe(oldStufe);
            var cost = PrestigeStufeBalancing.GetUpgradeGoldCost(oldStufe);

            // Karte freischalten bei Prestige IV?
            string? unlockedCardId = null;
            var world = _worldCatalog.Find(worldId);
            if (_domain.UnlocksExclusiveCard(newStufe) && world != null && !string.IsNullOrEmpty(world.Prestige4CardId))
            {
                unlockedCardId = world.Prestige4CardId;
            }

            var mutation = await _save.MutateAsync(state =>
            {
                state.Currencies.SpendGold(cost);
                state.Prestige.Set(worldId, newStufe);
                // Sterne-Reset nach Designplan v4 Oeko Kap. 6.2
                if (state.WorldProgress.TryGetValue(worldId, out var p))
                {
                    var keys = new List<string>(p.StarsByNodeId.Keys);
                    foreach (var k in keys) p.StarsByNodeId[k] = 0;
                }
                if (unlockedCardId != null && !state.Prestige.Prestige4CardsUnlocked.Contains(unlockedCardId))
                {
                    state.Prestige.Prestige4CardsUnlocked.Add(unlockedCardId);
                    // Karte ins Inventar legen
                    var instId = Guid.NewGuid().ToString("N");
                    state.CardInventory[instId] = new CardInstance(
                        instanceId: instId,
                        cardDefinitionId: unlockedCardId,
                        level: 0, expWithinLevel: 0,
                        obtainedAtUtc: DateTime.UtcNow);
                }
                return state;
            }, ct);

            if (!mutation.IsSuccess)
                return Result<PrestigeUpgradeOutcome>.Failure(mutation.ErrorMessage ?? "Save-Mutation fehlgeschlagen");

            _analytics.Track("prestige_upgrade", new Dictionary<string, object>
            {
                ["world_id"] = worldId,
                ["old_stufe"] = oldStufe.ToString(),
                ["new_stufe"] = newStufe.ToString(),
                ["gold_spent"] = cost,
                ["unlocked_card"] = unlockedCardId ?? string.Empty
            });

            return Result<PrestigeUpgradeOutcome>.Success(new PrestigeUpgradeOutcome(
                worldId, oldStufe, newStufe, cost, unlockedCardId));
        }

        // ============================================================================
        // Daily-Income-Tick
        // ============================================================================

        /// <summary>
        /// Berechnet das passive Gold-Income aller Welten seit dem letzten Tick (UTC, mind. 24h).
        /// Bucht das Gold in die Spieler-Currencies ein und persistiert den Save.
        /// </summary>
        public async UniTask<Result<int>> TickDailyIncomeAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var saveR = await _save.LoadAsync(ct);
            if (!saveR.IsSuccess || saveR.Value == null)
                return Result<int>.Failure(saveR.ErrorMessage ?? "Save nicht geladen");
            var save = saveR.Value;

            var elapsed = nowUtc - save.Prestige.LastDailyIncomeAtUtc;
            var days = (int)Math.Floor(elapsed.TotalDays);
            if (days <= 0) return Result<int>.Success(0);

            // Pro Welt: BaseGoldPerDay * Multiplier * days
            long totalGold = 0;
            foreach (var kv in save.Prestige.StufenByWorldId)
            {
                var world = _worldCatalog.Find(kv.Key);
                if (world == null) continue;
                var daily = _domain.CalculateDailyRevenue(world.BaseGoldPerDay, kv.Value);
                totalGold += (long)daily * days;
            }
            if (totalGold <= 0) return Result<int>.Success(0);

            await _save.MutateAsync(state =>
            {
                state.Currencies.AddGold(totalGold);
                state.Prestige.LastDailyIncomeAtUtc = nowUtc;
                return state;
            }, ct);

            _analytics.Track("prestige_daily_income", new Dictionary<string, object>
            {
                ["days"] = days,
                ["gold"] = totalGold
            });
            return Result<int>.Success((int)Math.Min(totalGold, int.MaxValue));
        }
    }

    /// <summary>
    /// Ergebnis eines Prestige-Upgrades fuer UI-Feedback.
    /// </summary>
    public sealed class PrestigeUpgradeOutcome
    {
        public string WorldId { get; }
        public PrestigeStufe OldStufe { get; }
        public PrestigeStufe NewStufe { get; }
        public long GoldSpent { get; }
        public string? UnlockedCardId { get; }

        public PrestigeUpgradeOutcome(string worldId, PrestigeStufe oldStufe, PrestigeStufe newStufe,
                                       long goldSpent, string? unlockedCardId)
        {
            WorldId = worldId;
            OldStufe = oldStufe;
            NewStufe = newStufe;
            GoldSpent = goldSpent;
            UnlockedCardId = unlockedCardId;
        }
    }
}
