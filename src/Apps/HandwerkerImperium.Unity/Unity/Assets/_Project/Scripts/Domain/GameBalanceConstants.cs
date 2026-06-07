namespace HandwerkerImperium.Domain
{
    /// <summary>
    /// Zentrale Balancing-Konstanten für HandwerkerImperium.
    /// Alle spielrelevanten Werte an einem Ort, statt über 10+ Dateien verstreut.
    ///
    /// 1:1-Port aus dem produktiven Avalonia-HandwerkerImperium (Models/GameBalanceConstants.cs).
    /// Werte sind die verbindliche Wahrheit (ORIGINAL_WERTE.md). Unity-sicher (C# 9, netstandard2.1).
    /// </summary>
    public static class GameBalanceConstants
    {
        // ═══════════════════════════════════════════════════════════════════
        // WORKSHOP - EINKOMMEN & KOSTEN
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Exponentieller Einkommens-Multiplikator pro Level (1.02^Level).</summary>
        public const double IncomeBaseMultiplier = 1.02;

        /// <summary>Exponentieller Upgrade-Kosten-Multiplikator pro Level (bis Lv500).</summary>
        public const double UpgradeCostExponent = 1.07;

        /// <summary>Reduzierter Upgrade-Kosten-Exponent ab Level 500 (entschärft tote Zone Lv600-750).</summary>
        public const double UpgradeCostReducedExponent = 1.06;

        /// <summary>Basis-Upgrade-Kosten ab Level 2.</summary>
        public const decimal UpgradeCostBase = 200m;

        /// <summary>Upgrade-Kosten für Level 1 (Spezialfall).</summary>
        public const decimal UpgradeCostLevel1 = 100m;

        /// <summary>Maximaler Prestige-Rabatt auf Upgrade-Kosten.</summary>
        public const decimal PrestigeDiscountCap = 0.50m;

        /// <summary>Miete: Linearer Faktor (Level * Wert) für Level 1-100.</summary>
        public const decimal RentBaseLinear = 10m;

        /// <summary>Miete: Exponential-Basis für Level > 100.</summary>
        public const decimal RentBaseExponential = 1000m;

        /// <summary>Miete: Exponent für Level > 100.</summary>
        public const double RentExponent = 1.005;

        /// <summary>Materialkosten: Linearer Faktor für Level 1-100.</summary>
        public const decimal MaterialCostBaseLinear = 5m;

        /// <summary>Materialkosten: Exponential-Basis für Level > 100.</summary>
        public const decimal MaterialCostBaseExponential = 500m;

        /// <summary>Materialkosten: Exponent für Level > 100.</summary>
        public const double MaterialCostExponent = 1.005;

        /// <summary>Worker-Anstellungs-Kosten: Basis-Betrag.</summary>
        public const decimal HireWorkerCostBase = 50m;

        /// <summary>Worker-Anstellungs-Kosten: Exponent.</summary>
        public const double HireWorkerCostExponent = 1.5;

        // ═══════════════════════════════════════════════════════════════════
        // WORKSHOP - MEILENSTEIN-MULTIPLIKATOREN
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Level-Meilenstein-Multiplikatoren (Level, Multiplikator).
        /// Spieler bekommt alle ~100-150 Level einen dopaminergen Kick.
        /// </summary>
        public static readonly (int Level, decimal Multiplier)[] MilestoneMultipliers = new (int Level, decimal Multiplier)[]
        {
            (25, 1.15m),
            (50, 1.30m),
            (75, 1.30m),
            (100, 1.45m),
            (150, 1.60m),
            (200, 1.45m),
            (225, 1.30m),
            (250, 1.60m),
            (350, 1.60m),
            (400, 1.60m),
            (500, 2.00m),
            (600, 1.70m),
            (650, 1.65m),
            (750, 1.60m),
            (900, 1.60m),
            (1000, 3.00m)
        };

        // ═══════════════════════════════════════════════════════════════════
        // WORKSHOP - LEVEL & SLOTS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Maximales Workshop-Level. 1000 ist der Hard-Cap und das Rebirth-Trigger-Niveau.</summary>
        public const int WorkshopMaxLevel = 1000;

        /// <summary>Bonus-PP pro Ascension-Level (linear).</summary>
        public const decimal BonusPpPerAscensionLevel = 0.5m;

        // ═══════════════════════════════════════════════════════════════════
        // ETERNAL MASTERY — Long-Term-Engagement post-Lv1000
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Permanenter Einkommens-Bonus pro abgeschlossenem Prestige (jeder Tier).</summary>
        public const decimal EternalMasteryBonusPerPrestige = 0.005m; // +0.5%

        /// <summary>Ab so vielen abgeschlossenen Prestiges greift der logarithmische Soft-Cap.</summary>
        public const int EternalMasterySoftCapThreshold = 50;

        /// <summary>Zusätzlicher Stufen-Bonus alle 5 abgeschlossenen Prestiges.</summary>
        public const decimal EternalMasteryBonusPer5Prestiges = 0.025m; // +2.5%

        /// <summary>Zusätzlicher Mega-Stufen-Bonus alle 10 Prestiges.</summary>
        public const decimal EternalMasteryBonusPer10Prestiges = 0.05m; // +5%

        /// <summary>Alle X Level ein zusätzlicher Worker-Slot.</summary>
        public const int WorkerSlotInterval = 50;

        /// <summary>Maximum Worker pro Workshop.</summary>
        public const int WorkerSlotMax = 20;

        /// <summary>Maximum Ad-Bonus Worker-Slots pro Workshop.</summary>
        public const int MaxAdBonusWorkerSlots = 3;

        /// <summary>Workshop-Level ab dem die Spezialisierung verfügbar ist.</summary>
        public const int SpecializationUnlockLevel = 50;

        /// <summary>Kosten (Goldschrauben) für das Wechseln einer bestehenden Spezialisierung.</summary>
        public const int SpecializationRespecCostGoldenScrews = 20;

        /// <summary>Lernkurve-Rabatt — bis zu diesem Workshop-Level ist Re-Spec gratis.</summary>
        public const int SpecializationFreeRespecBelowLevel = 75;

        /// <summary>Maximale Anzahl parallel laufender Aufträge.</summary>
        public const int MaxParallelOrders = 3;

        // ═══════════════════════════════════════════════════════════════════
        // PRESTIGE - DIMINISHING RETURNS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Faktor für Diminishing Returns auf Tier-Multiplikator-Bonus bei wiederholten
        /// Prestiges desselben Tiers. Formel: bonus / (1 + DiminishingReturnsPerTierPrestige * tierCount).
        /// </summary>
        public const decimal DiminishingReturnsPerTierPrestige = 0.2m;

        // ═══════════════════════════════════════════════════════════════════
        // WORKSHOP - REBIRTH
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Rebirth-Einkommensbonus pro Stern (Index = Sterne - 1).</summary>
        public static readonly decimal[] RebirthIncomeBonuses = new decimal[] { 0.15m, 0.35m, 0.60m, 1.00m, 1.50m };

        /// <summary>Rebirth-Upgrade-Rabatt pro Stern (Index = Sterne - 1).</summary>
        public static readonly decimal[] RebirthUpgradeDiscounts = new decimal[] { 0.05m, 0.10m, 0.15m, 0.20m, 0.25m };

        /// <summary>Rebirth-Extra-Worker pro Stern (Index = Sterne).</summary>
        public static readonly int[] RebirthExtraWorkers = new int[] { 0, 1, 1, 2, 2, 3 };

        /// <summary>Maximaler Aura-Bonus durch S-Tier+ Worker pro Workshop (50%).</summary>
        public const decimal MaxAuraBonus = 0.50m;

        // ═══════════════════════════════════════════════════════════════════
        // WORKER - STIMMUNG & MÜDIGKEIT
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Start-Stimmung eines neuen Workers.</summary>
        public const decimal WorkerInitialMood = 80m;

        /// <summary>Stimmungs-Schwelle für "Gut" (ab hier: Bonus).</summary>
        public const decimal MoodHappyThreshold = 80m;

        /// <summary>Stimmungs-Schwelle für "Neutral" (drunter: Abzug).</summary>
        public const decimal MoodNeutralThreshold = 50m;

        /// <summary>Stimmungs-Schwelle für "Kritisch" (drunter: Kündigung droht).</summary>
        public const decimal MoodCriticalThreshold = 20m;

        /// <summary>Stimmungs-Verlust pro Stunde Arbeit.</summary>
        public const decimal MoodDecayPerHour = 3.0m;

        /// <summary>Müdigkeits-Zunahme pro Stunde Arbeit.</summary>
        public const decimal FatigueIncreasePerHour = 12.5m;

        /// <summary>Müdigkeits-Schwelle für "Erschöpft".</summary>
        public const decimal FatigueExhaustedThreshold = 100m;

        /// <summary>Benötigte Stunden zum Ausruhen.</summary>
        public const decimal RestHoursNeeded = 4m;

        /// <summary>Trainings-Kosten-Multiplikator (X * Stundenlohn).</summary>
        public const int TrainingCostMultiplier = 2;

        /// <summary>Trainings-XP pro Stunde.</summary>
        public const int TrainingXpPerHour = 50;

        /// <summary>XP-Bedarf pro Level (Level * Wert).</summary>
        public const int XpPerLevelMultiplier = 200;

        /// <summary>Effizienz-Bonus pro Experience-Level.</summary>
        public const decimal EfficiencyBonusPerLevel = 0.03m;

        /// <summary>Effizienz-Bonus pro Talent-Stern.</summary>
        public const decimal EfficiencyBonusPerTalent = 0.05m;

        // ═══════════════════════════════════════════════════════════════════
        // WORKER - LEVEL-FIT (Höhere Tiers besser für hohe Level)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Alle X Level gibt es -2% Effizienz für niedrige Tiers.</summary>
        public const int LevelPenaltyStep = 30;

        /// <summary>Penalty pro Level-Schritt.</summary>
        public const decimal LevelPenaltyPerStep = 0.02m;

        /// <summary>Minimum Level-Fit-Faktor (nie unter diesem Wert).</summary>
        public const decimal MinLevelFitFactor = 0.20m;

        // ═══════════════════════════════════════════════════════════════════
        // BUILDING - KOSTEN & BONI
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Building-Kosten-Exponent (BaseCost * X^Level).</summary>
        public const int BuildingCostExponent = 2;

        /// <summary>Kantine: Stimmungs-Wiederherstellung pro Stunde pro Level.</summary>
        public const decimal CanteenMoodRecoveryPerLevel = 1.0m;

        /// <summary>Kantine: Ruhezeit-Reduktion pro Level [Level 1-5].</summary>
        public static readonly decimal[] CanteenRestReduction = new decimal[] { 0m, 0.50m, 0.55m, 0.60m, 0.70m, 0.80m };

        /// <summary>Lager: Materialkosten-Reduktion pro Level [Level 1-5].</summary>
        public static readonly decimal[] StorageMaterialReduction = new decimal[] { 0m, 0.15m, 0.25m, 0.35m, 0.45m, 0.50m };

        /// <summary>Fuhrpark: Auftrags-Belohnungsbonus pro Level [Level 1-5].</summary>
        public static readonly decimal[] VehicleFleetRewardBonus = new decimal[] { 0m, 0.20m, 0.30m, 0.40m, 0.50m, 0.60m };

        /// <summary>Ausstellungsraum: Täglicher Ruf-Gewinn pro Level.</summary>
        public const decimal ShowroomReputationPerLevel = 0.5m;

        /// <summary>Ausbildungszentrum: Speed-Multiplikator pro Level.</summary>
        public const decimal TrainingCenterSpeedPerLevel = 1.0m;

        // ═══════════════════════════════════════════════════════════════════
        // PRESTIGE - BONUS-PP (flat, NACH Tier-Multiplikator addiert)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Bonus-PP pro 10 Perfect Ratings im Run (max BonusPpPerfectRatingsCap).</summary>
        public const int BonusPpPerPerfectBlock = 1;

        /// <summary>Maximale Bonus-PP aus Perfect Ratings.</summary>
        public const int BonusPpPerfectRatingsCap = 5;

        /// <summary>Bonus-PP für eine komplett erforschte Research-Branch (15/15).</summary>
        public const int BonusPpFullBranch = 2;

        /// <summary>Bonus-PP wenn alle 7 Gebäude auf Level 5 sind.</summary>
        public const int BonusPpAllBuildingsMax = 1;

        /// <summary>Bonus-PP pro Level über dem Tier-Minimum.</summary>
        public const decimal BonusPpPerExtraLevel = 0.05m;

        /// <summary>Maximale Bonus-PP aus Level-Überschuss.</summary>
        public const int BonusPpExtraLevelCap = 5;

        // ═══════════════════════════════════════════════════════════════════
        // PRESTIGE - MEILENSTEINE (GS-Belohnungen, permanent)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Prestige-Meilensteine: (benötigte Prestiges, ID, GS-Belohnung).
        /// Permanent — werden NICHT bei Ascension zurückgesetzt.
        /// </summary>
        public static readonly (int RequiredCount, string Id, int GoldenScrewReward)[] PrestigeMilestones = new (int RequiredCount, string Id, int GoldenScrewReward)[]
        {
            (1,   "pm_first",  10),
            (5,   "pm_5",      20),
            (10,  "pm_10",     35),
            (25,  "pm_25",     50),
            (50,  "pm_50",     75),
            (100, "pm_100",    100),
        };

        /// <summary>Maximale Kaufanzahl pro wiederholbarem Prestige-Shop-Item.</summary>
        public const int MaxRepeatableShopPurchases = 8;

        // ═══════════════════════════════════════════════════════════════════
        // AUTO-PRODUKTION
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Standard-Produktionsintervall in Sekunden (1 Item pro Worker).</summary>
        public const int AutoProductionIntervalSeconds = 180;

        /// <summary>InnovationLab: Schnelleres Produktionsintervall (Prestige-5-Bonus).</summary>
        public const int AutoProductionInnovationLabInterval = 120;

        /// <summary>MasterSmith behält sein bestehendes 60s-Intervall als Spezialeffekt.</summary>
        public const int AutoProductionMasterSmithInterval = 60;

        /// <summary>Workshop-Level ab dem Auto-Produktion freigeschaltet wird.</summary>
        public const int AutoProductionUnlockLevel = 50;

        /// <summary>Workshop-Level ab dem Auto-Craft Tier-2 freigeschaltet wird.</summary>
        public const int AutoCraftTier2UnlockLevel = 150;

        /// <summary>Workshop-Level ab dem Auto-Craft Tier-3 freigeschaltet wird.</summary>
        public const int AutoCraftTier3UnlockLevel = 320;

        /// <summary>Logarithmischer Skalierungsfaktor für Crafting-Verkaufspreise: log₂(1 + Level/Wert).</summary>
        public const double CraftingSellPriceLogDivisor = 15.0;

        // ═══════════════════════════════════════════════════════════════════
        // LIEFERAUFTRÄGE (MaterialOrder)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Belohnungs-Multiplikator für Lieferaufträge.</summary>
        public const decimal MaterialOrderRewardMultiplier = 1.8m;

        /// <summary>XP-Multiplikator für Lieferaufträge.</summary>
        public const decimal MaterialOrderXpMultiplier = 1.5m;

        /// <summary>Maximale Lieferaufträge pro Tag.</summary>
        public const int MaterialOrdersPerDay = 5;

        /// <summary>Deadline für Lieferaufträge in Stunden.</summary>
        public const int MaterialOrderDeadlineHours = 4;

        /// <summary>Spieler-Level ab dem Cross-Workshop-Materialien gefordert werden.</summary>
        public const int MaterialOrderCrossWorkshopLevel = 100;

        // ═══════════════════════════════════════════════════════════════════
        // MATERIAL-OFFER
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Spielerlevel ab dem Material-Angebote in Aufträgen erscheinen (Onboarding-Schutz).</summary>
        public const int MaterialOfferUnlockLevel = 30;

        /// <summary>Wahrscheinlichkeit eines Material-Angebots beim Auftrags-Spawn (35%).</summary>
        public const double MaterialOfferChance = 0.35;

        /// <summary>Bonus-Reward bei Quick-Auftrag mit Material (+25%).</summary>
        public const double MaterialOfferBonusQuick = 0.25;

        /// <summary>Bonus-Reward bei Standard-Auftrag mit Material (+30%).</summary>
        public const double MaterialOfferBonusStandard = 0.30;

        /// <summary>Bonus-Reward bei Large-Auftrag mit Material (+40%).</summary>
        public const double MaterialOfferBonusLarge = 0.40;

        /// <summary>Bonus-Reward bei Cooperation-Auftrag mit Material (+50%).</summary>
        public const double MaterialOfferBonusCooperation = 0.50;

        /// <summary>Bonus-Reward bei Weekly-Auftrag mit Material (+60%).</summary>
        public const double MaterialOfferBonusWeekly = 0.60;

        // ═══════════════════════════════════════════════════════════════════
        // HEIRLOOM
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Maximale Anzahl Erbstücke die beim Prestige mitgenommen werden können (Free-Tier).</summary>
        public const int MaxHeirloomsPerRun = 3;

        /// <summary>Maximale Anzahl Erbstücke für Imperium-Pass-Spieler.</summary>
        public const int MaxHeirloomsPerRunPremium = 4;

        /// <summary>Hilfsmethode: liefert den effektiven Erbstück-Slot-Cap je nach Premium-Status.</summary>
        public static int GetEffectiveHeirloomSlots(bool isPremium)
            => isPremium ? MaxHeirloomsPerRunPremium : MaxHeirloomsPerRun;

        /// <summary>Globaler Einkommens-Bonus pro Run-Erbstück (+2%).</summary>
        public const decimal HeirloomBonusPerItem = 0.02m;

        /// <summary>Globaler Einkommens-Bonus pro permanentem Erbstück (+0.5% forever).</summary>
        public const decimal PermanentHeirloomBonusPerItem = 0.005m;

        /// <summary>Hard-Cap für permanente Ascension-Erbstücke.</summary>
        public const int MaxPermanentHeirlooms = 50;

        // ═══════════════════════════════════════════════════════════════════
        // AUFTRAGS-BELOHNUNGEN - SOFT-CAP
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Soft-Cap für den externen Order-Reward-Multiplikator.</summary>
        public const decimal OrderRewardMultiplierSoftCap = 10.0m;

        /// <summary>Soft-Cap-Schwelle für den Crafting-Sell-Multiplikator.</summary>
        public const decimal CraftingSellMultiplierSoftCap = 8.0m;

        /// <summary>Harte Obergrenze für den Crafting-Sell-Multiplikator (auch nach Soft-Cap).</summary>
        public const decimal CraftingSellMultiplierHardCap = 12.0m;

        // ═══════════════════════════════════════════════════════════════════
        // UI-ANIMATION & TIMING
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Mindestdauer der Splash-Anzeige in Millisekunden.</summary>
        public const int SplashMinimumDisplayMs = 800;

        /// <summary>Interpolations-Faktor für den animierten Geld-Counter pro Frame (0.0-1.0).</summary>
        public const decimal MoneyAnimationInterpolationFactor = 0.15m;
    }
}
