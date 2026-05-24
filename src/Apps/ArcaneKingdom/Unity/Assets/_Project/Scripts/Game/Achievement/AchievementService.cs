#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Achievement;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Save;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcaneKingdom.Game.Achievement
{
    /// <summary>
    /// Verfolgt alle Achievement-Werte und vergibt Tier-Belohnungen (Trophy-Points + Titel).
    /// Liest <c>achievements.json</c> aus Resources, speichert Progress in PlayerSave-Schema v2.
    /// </summary>
    public sealed class AchievementService
    {
        public sealed class TierUnlockEvent
        {
            public AchievementDefinition Definition { get; init; } = default!;
            public AchievementTier Tier { get; init; } = default!;
        }

        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;
        private readonly List<AchievementDefinition> _definitions = new();

        public event Action<TierUnlockEvent>? TierUnlocked;

        public AchievementService(ISaveService<PlayerSave> save, IAnalyticsService analytics)
        {
            _save = save;
            _analytics = analytics;
            LoadDefinitionsFromResources();
        }

        public IReadOnlyList<AchievementDefinition> AllDefinitions => _definitions;

        // --- Trigger-Hooks (Event-getrieben aus Game-Layer) -------------------

        public UniTask OnBossDefeatedAsync(CancellationToken ct = default) => AdvanceAsync("boss_slayer", 1, ct);
        public UniTask OnArenaWonAsync(CancellationToken ct = default) => AdvanceAsync("arena_champion", 1, ct);
        public UniTask OnThiefDefeatedAsync(CancellationToken ct = default) => AdvanceAsync("thief_hunter", 1, ct);
        public UniTask OnGuildContributionAsync(long delta, CancellationToken ct = default) => AdvanceAsync("guild_pillar", (int)Math.Min(delta, int.MaxValue), ct);
        public UniTask OnWorldCompletedAsync(CancellationToken ct = default) => AdvanceAsync("world_conqueror", 1, ct);
        public UniTask OnCardReachedMaxLevelAsync(CancellationToken ct = default) => AdvanceAsync("max_level_card", 1, ct);
        public UniTask OnDiamondsSpentAsync(int amount, CancellationToken ct = default) => AdvanceAsync("spender", amount, ct);
        public UniTask OnLoginDayAsync(CancellationToken ct = default) => AdvanceAsync("veteran", 1, ct);

        public async UniTask OnUniqueCardObtainedAsync(string definitionId, CancellationToken ct = default)
        {
            await _save.MutateAsync(save =>
            {
                // Sammler: Anzahl unique CardDefinitions im Inventar
                var unique = new HashSet<string>(save.CardInventory.Values.Select(c => c.CardDefinitionId));
                AdvanceLocal(save, "card_collector", unique.Count, absolute: true);
                return save;
            }, ct);
        }

        public async UniTask OnLegendaryCardObtainedAsync(CancellationToken ct = default)
        {
            await AdvanceAsync("legendary_owner", 1, ct);
        }

        // --- Generischer Advance + Tier-Auswertung ----------------------------

        public async UniTask AdvanceAsync(string achievementId, int delta, CancellationToken ct = default)
        {
            if (delta <= 0) return;
            var def = _definitions.FirstOrDefault(d => d.Id == achievementId);
            if (def == null) return;

            var newlyUnlocked = new List<AchievementTier>();
            await _save.MutateAsync(save =>
            {
                newlyUnlocked = AdvanceLocal(save, achievementId, delta, absolute: false);
                return save;
            }, ct);

            foreach (var tier in newlyUnlocked)
            {
                _analytics.Track("achievement_tier_unlocked", new Dictionary<string, object>
                {
                    ["achievement_id"] = achievementId,
                    ["tier"] = tier.Tier,
                    ["trophy_points"] = tier.TrophyPoints
                });
                TierUnlocked?.Invoke(new TierUnlockEvent { Definition = def, Tier = tier });
            }
        }

        private List<AchievementTier> AdvanceLocal(PlayerSave save, string achievementId, int valueOrDelta, bool absolute)
        {
            var def = _definitions.FirstOrDefault(d => d.Id == achievementId);
            if (def == null) return new List<AchievementTier>();

            if (!save.Achievements.Progress.TryGetValue(achievementId, out var dto))
            {
                dto = new AchievementProgressDto();
                save.Achievements.Progress[achievementId] = dto;
            }

            var progress = new AchievementProgress(achievementId, dto.CurrentValue, dto.HighestTierUnlocked);
            var unlocked = absolute
                ? AdvanceAbsolute(progress, valueOrDelta, def.Tiers)
                : progress.Advance(valueOrDelta, def.Tiers);

            dto.CurrentValue = progress.CurrentValue;
            dto.HighestTierUnlocked = progress.HighestTierUnlocked;

            foreach (var tier in unlocked)
            {
                save.Achievements.TotalTrophyPoints += tier.TrophyPoints;
                if (!string.IsNullOrEmpty(tier.TitleKey)) save.Achievements.UnlockedTitleKeys.Add(tier.TitleKey!);
                save.PendingClaims.Add(new PendingClaim
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Kind = PendingClaimKind.Currency,
                    SubType = nameof(Domain.Economy.Currency.MeritPoints),
                    Amount = tier.TrophyPoints,
                    SourceKey = $"achievement.{achievementId}.tier{tier.Tier}",
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            return unlocked.ToList();
        }

        private static IReadOnlyList<AchievementTier> AdvanceAbsolute(AchievementProgress progress, int absoluteValue, IReadOnlyList<AchievementTier> tiers)
        {
            var diff = absoluteValue - progress.CurrentValue;
            return diff > 0 ? progress.Advance(diff, tiers) : Array.Empty<AchievementTier>();
        }

        private void LoadDefinitionsFromResources()
        {
            var asset = Resources.Load<TextAsset>("Data/achievements");
            if (asset == null)
            {
                GameLogger.Warning("Achievement", "Resources/Data/achievements.json fehlt.");
                return;
            }
            try
            {
                var loaded = JsonConvert.DeserializeObject<List<AchievementDefinition>>(asset.text);
                if (loaded != null) _definitions.AddRange(loaded);
                GameLogger.Info("Achievement", $"{_definitions.Count} Achievements geladen.");
            }
            catch (Exception ex)
            {
                GameLogger.Error("Achievement", "Definitions-Load fehlgeschlagen", ex);
            }
        }
    }
}
