#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Guild;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Treasury
{
    /// <summary>
    /// Client-Sicht auf die Gilden-Kasse. Real wird die Treasury durch
    /// <c>dailyTerritoryTick</c> Cloud Function gefüllt; dieser Service stellt
    /// nur die Read-API + Auto-Split-Berechnung bereit.
    /// </summary>
    public sealed class GuildTreasuryService
    {
        public sealed class AutoSplitResult
        {
            public long GoldPerMember { get; init; }
            public long DiamondsPerMember { get; init; }
            public IReadOnlyDictionary<string, long> ScrapsPerMember { get; init; } = new Dictionary<string, long>();
        }

        private readonly IAnalyticsService _analytics;

        public GuildTreasuryService(IAnalyticsService analytics)
        {
            _analytics = analytics;
        }

        /// <summary>
        /// Berechnet die Auto-Split-Auszahlung pro Mitglied. Reste werden in der Treasury belassen.
        /// </summary>
        public AutoSplitResult ComputeAutoSplit(GuildTreasury treasury, int activeMemberCount)
        {
            if (activeMemberCount < 1) return new AutoSplitResult();
            var gold = treasury.Gold / activeMemberCount;
            var diamonds = treasury.Diamonds / activeMemberCount;
            var scraps = new Dictionary<string, long>();
            foreach (var kv in treasury.ScrapsByType) scraps[kv.Key] = kv.Value / activeMemberCount;
            return new AutoSplitResult { GoldPerMember = gold, DiamondsPerMember = diamonds, ScrapsPerMember = scraps };
        }

        public UniTask LogTickAsync(GuildTreasury before, TerritoryBonusEngine.DailyTickResult tick, CancellationToken ct = default)
        {
            _analytics.Track("guild_treasury_tick", new Dictionary<string, object>
            {
                ["added_gold"] = tick.AddedGold,
                ["added_diamonds"] = tick.AddedDiamonds,
                ["scraps_kinds"] = tick.AddedScraps.Count
            });
            GameLogger.Info("Treasury", $"Tick: +{tick.AddedGold} Gold, +{tick.AddedDiamonds} Diamanten, {tick.AddedScraps.Count} Scrap-Kinds.");
            return UniTask.CompletedTask;
        }
    }
}
