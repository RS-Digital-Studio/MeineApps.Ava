#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Economy;
using ArcaneKingdom.Domain.Player;

namespace ArcaneKingdom.Domain.Cards
{
    /// <summary>
    /// Anforderungen fuer das Aufleveln einer Karten-Instanz von LV n -> n+1
    /// (Arcane_Legends_Designplan Kap. 3.1).
    /// </summary>
    public sealed class CardUpgradeRequirement
    {
        public int FromLevel { get; init; }
        public int ToLevel { get; init; }
        public int ExpRequired { get; init; }
        public long GoldCost { get; init; }
        public ScrapType ScrapType { get; init; }
        public int ScrapCount { get; init; }
        /// <summary>Bei LV 5/10/15 — Kopien-Anforderung (1x bei LV5, 2x bei LV10, 3x bei LV15).</summary>
        public int CopiesRequired { get; init; }

        /// <summary>True wenn der Spieler genug Material hat um zu leveln.</summary>
        public bool CanAfford(PlayerCurrencies currencies, int availableCopies, int currentExp) =>
            currentExp >= ExpRequired
            && currencies.Gold >= GoldCost
            && GetScrapBalance(currencies, ScrapType) >= ScrapCount
            && availableCopies >= CopiesRequired;

        private static long GetScrapBalance(PlayerCurrencies c, ScrapType t) => t switch
        {
            ScrapType.Common    => c.CommonScraps,
            ScrapType.Rare      => c.RareScraps,
            ScrapType.Epic      => c.EpicScraps,
            ScrapType.Legendary => c.LegendaryScraps,
            _ => 0
        };
    }

    /// <summary>
    /// Pure Logik fuer das Karten-Leveln (Arcane_Legends_Designplan Kap. 3).
    /// LV 0->1: 100 EXP, 500 Gold. LV 5: SKILL 2 freischalten. LV 10: SKILL 3.
    /// LV 15 MAX: +88% ATK/HP + Goldener Rahmen.
    /// </summary>
    public sealed class CardUpgradeService
    {
        public CardUpgradeRequirement GetRequirement(int currentLevel)
        {
            if (currentLevel < 0 || currentLevel >= CardLevelTable.MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(currentLevel));

            var nextLevel = currentLevel + 1;
            return new CardUpgradeRequirement
            {
                FromLevel = currentLevel,
                ToLevel = nextLevel,
                ExpRequired = CardLevelTable.ExpForLevel(nextLevel),
                GoldCost = CardLevelTable.GoldForLevel(nextLevel),
                ScrapType = CardLevelTable.ScrapTypeForLevel(nextLevel),
                ScrapCount = CardLevelTable.ScrapCountForLevel(nextLevel),
                CopiesRequired = CardLevelTable.CopiesRequiredForLevel(nextLevel)
            };
        }

        /// <summary>
        /// Wendet das Upgrade auf eine Karten-Instanz an. Verbraucht Materialien aus dem Save.
        /// </summary>
        public Result<int> ApplyUpgrade(CardInstance instance, PlayerSave save)
        {
            if (instance is null) return Result<int>.Failure("Instance ist null");
            if (save is null) return Result<int>.Failure("Save ist null");
            if (instance.Level >= CardLevelTable.MaxLevel)
                return Result<int>.Failure("Karte ist bereits Max-Level (LV 15).");

            var req = GetRequirement(instance.Level);

            // Kopien zaehlen: Anzahl Instanzen mit derselben Definition (ohne diese eine)
            var copies = save.CardInventory.Values
                .Count(i => i.CardDefinitionId == instance.CardDefinitionId && i.InstanceId != instance.InstanceId);

            if (!req.CanAfford(save.Currencies, copies, instance.ExpWithinLevel))
                return Result<int>.Failure("Nicht genug Material/EXP/Gold/Kopien.");

            // Materialien verbrauchen
            save.Currencies.SpendGold(req.GoldCost);
            save.Currencies.SpendScraps(req.ScrapType, req.ScrapCount);

            // Kopien-Karten konsumieren (entfernen aus Inventar)
            if (req.CopiesRequired > 0)
            {
                var toRemove = save.CardInventory.Values
                    .Where(i => i.CardDefinitionId == instance.CardDefinitionId && i.InstanceId != instance.InstanceId)
                    .Take(req.CopiesRequired)
                    .Select(i => i.InstanceId)
                    .ToList();
                foreach (var id in toRemove) save.CardInventory.Remove(id);
            }

            // Karte aufleveln: Level erhoehen, EXP zuruecksetzen
            instance.SetLevel(req.ToLevel);
            instance.SetExpWithinLevel(0);

            return Result<int>.Success(req.ToLevel);
        }
    }

    /// <summary>
    /// Level-Tabellen aus Arcane_Legends_Designplan Kap. 3.1.
    /// </summary>
    public static class CardLevelTable
    {
        public const int MaxLevel = 15;

        public static int ExpForLevel(int level) => level switch
        {
            1 => 100, 2 => 200, 3 => 400, 4 => 700,
            5 => 1_200, 6 => 2_000, 7 => 3_000, 8 => 4_500, 9 => 6_000,
            10 => 9_000, 11 => 13_000, 12 => 18_000, 13 => 25_000, 14 => 35_000,
            15 => 50_000,
            _ => 0
        };

        public static long GoldForLevel(int level) => level switch
        {
            1 => 500, 2 => 1_000, 3 => 2_000, 4 => 4_000,
            5 => 8_000, 6 => 15_000, 7 => 25_000, 8 => 40_000, 9 => 60_000,
            10 => 100_000, 11 => 150_000, 12 => 220_000, 13 => 320_000, 14 => 470_000,
            15 => 700_000,
            _ => 0
        };

        /// <summary>LV 1-4 = Common, LV 5-9 = Rare, LV 10-14 = Epic, LV 15 = Legendary.</summary>
        public static ScrapType ScrapTypeForLevel(int level)
        {
            if (level <= 4) return ScrapType.Common;
            if (level <= 9) return ScrapType.Rare;
            if (level <= 14) return ScrapType.Epic;
            return ScrapType.Legendary;
        }

        public static int ScrapCountForLevel(int level) => level switch
        {
            <= 4 => 1,
            <= 9 => 2,
            <= 14 => 3,
            15 => 1,  // Legendary-Scrap (selten, 1 reicht fuer LV15)
            _ => 0
        };

        /// <summary>LV 5: 1 Kopie. LV 10: 2 Kopien. LV 15: 3 Kopien.</summary>
        public static int CopiesRequiredForLevel(int level) => level switch
        {
            5 => 1,
            10 => 2,
            15 => 3,
            _ => 0
        };

        /// <summary>Skill-Freischaltung: LV 5 = Skill 2, LV 10 = Skill 3, LV 15 = Letzter Wille.</summary>
        public static bool UnlocksSkillAt(int level) => level == 5 || level == 10 || level == 15;
    }
}
