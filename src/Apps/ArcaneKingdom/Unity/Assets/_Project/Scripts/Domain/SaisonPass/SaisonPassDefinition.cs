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
        public int XpPerTier { get; init; } = 1167;          // Legacy/Fallback (linear), nur falls XpThresholds leer
        public int HardCapTier { get; init; } = 30;

        /// <summary>
        /// Kumulative EXP-Schwellen je Stufe (Index = Stufe, [0] = 0). Length = TotalTiers + 1.
        /// Non-lineare Kurve (Oekosystem v4 Kap. 4.1, stueckweise-linear interpoliert zwischen den
        /// 6 Meilensteinen 5/10/15/20/25/30 = 2500/7000/12000/18000/25000/35000).
        /// Leer => Fallback auf lineare XpPerTier-Berechnung (Abwaerts-Kompatibilitaet, Bestands-Tests).
        /// </summary>
        public List<int> XpThresholds { get; init; } = new();

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
            var th = def.XpThresholds;
            if (th == null || th.Count < 2)
            {
                // Legacy-Fallback: lineare Kurve (haelt Bestands-Tests gruen)
                var lin = xp / Math.Max(1, def.XpPerTier);
                return Math.Min(lin, def.HardCapTier);
            }
            var maxTier = Math.Min(def.HardCapTier, th.Count - 1);
            var tier = 0;
            for (var t = 1; t <= maxTier; t++)
            {
                if (xp >= th[t]) tier = t; else break;   // th ist monoton steigend
            }
            return tier;
        }

        public static int XpRemainingToNextTier(int xp, SaisonPassDefinition def)
        {
            var th = def.XpThresholds;
            if (th == null || th.Count < 2)
            {
                var nextThreshold = (TierForXp(xp, def) + 1) * def.XpPerTier;
                return Math.Max(0, nextThreshold - xp);
            }
            var cur = TierForXp(xp, def);
            var maxTier = Math.Min(def.HardCapTier, th.Count - 1);
            if (cur >= maxTier) return 0;                // MAX erreicht
            return Math.Max(0, th[cur + 1] - Math.Max(0, xp));
        }

        /// <summary>Kumulierte EXP, um die gegebene Stufe zu erreichen (0..HardCapTier).</summary>
        public static int XpForTier(int tier, SaisonPassDefinition def)
        {
            var th = def.XpThresholds;
            var clamped = Math.Clamp(tier, 0, def.HardCapTier);
            if (th != null && th.Count >= 2 && clamped < th.Count) return th[clamped];
            return clamped * def.XpPerTier;             // Fallback linear
        }

        /// <summary>Gesamt-EXP fuer die Max-Stufe (HardCap) — fuer Cap-Clamp + Progress-Bar.</summary>
        public static int MaxXp(SaisonPassDefinition def) => XpForTier(def.HardCapTier, def);

        /// <summary>EXP-Spanne der aktuellen Stufe (fuer Progress-Bar): (InTier, Span).</summary>
        public static (int InTier, int Span) ProgressInTier(int xp, SaisonPassDefinition def)
        {
            var cur = TierForXp(xp, def);
            var maxTier = Math.Min(def.HardCapTier, (def.XpThresholds?.Count ?? 0) >= 2 ? def.XpThresholds!.Count - 1 : def.HardCapTier);
            if (cur >= maxTier) return (0, 0);          // MAX: kein Fortschritt mehr
            var lo = XpForTier(cur, def);
            var hi = XpForTier(cur + 1, def);
            var span = Math.Max(1, hi - lo);
            return (Math.Max(0, Math.Max(0, xp) - lo), span);
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
