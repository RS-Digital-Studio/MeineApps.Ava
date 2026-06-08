#nullable enable
using System;

namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>Permanente Perk-Achsen des Imperium-Marken-Perkboards (PROGRESSION §6).</summary>
    public enum PerkKind
    {
        StartMoney = 0,
        OfflineCapHours = 1,
        GlobalTempo = 2,
        AutoCollectRadius = 3,
        WorkerBaseTempo = 4,
        GemFind = 5
    }

    /// <summary>
    /// Imperium-Marken-Perkboard (PROGRESSION §3/§6): permanente Meta-Boni, gespeist aus Marken der 3 Prestiges
    /// + Meilensteinen, füllt sich über das ganze Spiel (nie reset). Reine, Unity-freie Mathematik: Marken-Quelle,
    /// geometrische Stufenkosten, linearer Bonus je Stufe. Perk-Katalog (MaxLevel/Bonuswerte) liegt im BalancingConfig.
    /// </summary>
    public static class PerkboardFormulas
    {
        /// <summary>Marken, die ein abgeschlossenes Prestige einbringt (fester Schub je Prestige).</summary>
        public static int MarksFromPrestige(int prestigeCount, int marksPerPrestige)
        {
            if (prestigeCount <= 0 || marksPerPrestige <= 0) return 0;
            return prestigeCount * marksPerPrestige;
        }

        /// <summary>Marken-Kosten der nächsten Stufe eines Perks: <c>base × growth^currentLevel</c> (aufgerundet, min 1).</summary>
        public static int MarkCost(int currentLevel, int baseCost, double growth)
        {
            if (currentLevel < 0) currentLevel = 0;
            double raw = baseCost * Math.Pow(growth, currentLevel);
            if (double.IsNaN(raw) || double.IsInfinity(raw) || raw > int.MaxValue) return int.MaxValue;
            int cost = (int)Math.Ceiling(raw);
            return cost < 1 ? 1 : cost;
        }

        /// <summary>Linearer Bonus eines Perks auf einer Stufe (<c>level × bonusPerLevel</c>), auf MaxLevel geklemmt.</summary>
        public static decimal BonusAtLevel(int level, int maxLevel, decimal bonusPerLevel)
        {
            if (level < 0) level = 0;
            if (maxLevel >= 0 && level > maxLevel) level = maxLevel;
            return level * bonusPerLevel;
        }

        /// <summary>True, wenn ein Kauf der nächsten Stufe möglich ist (genug Marken + MaxLevel nicht erreicht).</summary>
        public static bool CanBuy(int availableMarks, int currentLevel, int maxLevel, int baseCost, double growth)
        {
            if (currentLevel >= maxLevel) return false;
            return availableMarks >= MarkCost(currentLevel, baseCost, growth);
        }
    }
}
