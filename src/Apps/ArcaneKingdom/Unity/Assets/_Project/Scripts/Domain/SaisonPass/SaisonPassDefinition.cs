#nullable enable
using System;
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.SaisonPass
{
    /// <summary>
    /// Konfiguration eines Saison-Passes (DESIGN.md Kap. 16.3).
    /// 50 Stufen, parallel zur Arena-Saison (30 Tage).
    /// </summary>
    [Serializable]
    public sealed class SaisonPassDefinition
    {
        public string Id { get; init; } = string.Empty;
        public int Number { get; init; }
        public DateTime StartedAtUtc { get; init; }
        public DateTime EndsAtUtc { get; init; }
        public int TotalTiers { get; init; } = 50;
        public int XpPerTier { get; init; } = 1000;
        public int HardCapTier { get; init; } = 100;
        public List<SaisonPassTierReward> FreeTrack { get; init; } = new();
        public List<SaisonPassTierReward> PremiumTrack { get; init; } = new();
    }

    [Serializable]
    public sealed class SaisonPassTierReward
    {
        public int Tier { get; init; }
        public string RewardKind { get; init; } = "Currency";    // "Currency"|"Pack"|"Card"|"Scrap"
        public string SubType { get; init; } = string.Empty;
        public long Amount { get; init; }
        public string? CosmeticKey { get; init; }
    }

    /// <summary>
    /// Pure-C# Logik: Tier-Berechnung aus XP, naechste Stufe, Free/Premium-Claims.
    /// </summary>
    public static class SaisonPassEngine
    {
        public static int TierForXp(int xp, SaisonPassDefinition def)
        {
            if (xp < 0) return 0;
            var tier = xp / Math.Max(1, def.XpPerTier);
            return Math.Min(tier, def.HardCapTier);
        }

        public static int XpRemainingToNextTier(int xp, SaisonPassDefinition def)
        {
            var nextThreshold = (TierForXp(xp, def) + 1) * def.XpPerTier;
            return Math.Max(0, nextThreshold - xp);
        }

        public static IReadOnlyList<SaisonPassTierReward> RewardsForTierRange(SaisonPassDefinition def, int fromTier, int toTier, bool premiumActive)
        {
            var result = new List<SaisonPassTierReward>();
            for (var t = fromTier + 1; t <= toTier && t <= def.TotalTiers; t++)
            {
                foreach (var r in def.FreeTrack) if (r.Tier == t) result.Add(r);
                if (premiumActive)
                    foreach (var r in def.PremiumTrack) if (r.Tier == t) result.Add(r);
            }
            return result;
        }
    }
}
