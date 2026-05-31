#nullable enable
using System;
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.SaisonPass
{
    /// <summary>
    /// Konfiguration eines Saison-Passes (Oekosystem v4 Kap. 4).
    /// 30 Stufen über 30 Tage, ~35.000 EXP gesamt (Belohnungen an Meilensteinen 5/10/15/20/25/30).
    /// </summary>
    [Serializable]
    public sealed class SaisonPassDefinition
    {
        public string Id { get; init; } = string.Empty;
        public int Number { get; init; }
        public DateTime StartedAtUtc { get; init; }
        public DateTime EndsAtUtc { get; init; }
        public int TotalTiers { get; init; } = 30;
        public int XpPerTier { get; init; } = 1167;
        public int HardCapTier { get; init; } = 30;
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
