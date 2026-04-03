namespace HandwerkerImperium.Models;

/// <summary>
/// Zentrale Balancing-Konstanten für HandwerkerImperium.
/// Alle spielrelevanten Werte an einem Ort, statt über 10+ Dateien verstreut.
/// Änderungen am Balancing können hier vorgenommen werden.
/// </summary>
public static class GameBalanceConstants
{
    // ═══════════════════════════════════════════════════════════════════════
    // WORKSHOP - EINKOMMEN & KOSTEN
    // ═══════════════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════════════
    // WORKSHOP - MEILENSTEIN-MULTIPLIKATOREN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Level-Meilenstein-Multiplikatoren (Level, Multiplikator).
    /// Lv400/600/750/900 eingefügt um die Lv350-500 und Lv500-1000 Durststrecken zu brechen.
    /// Spieler bekommt alle ~100-150 Level einen dopaminergen Kick.
    /// </summary>
    public static readonly (int Level, decimal Multiplier)[] MilestoneMultipliers =
    [
        (25, 1.15m),
        (50, 1.30m),
        (75, 1.30m),
        (100, 1.45m),
        (150, 1.60m),
        (200, 1.45m),
        (225, 1.30m),
        (250, 1.60m),
        (350, 1.60m),
        (400, 1.40m),   // NEU: Bricht die 150-Level-Lücke Lv350→Lv500
        (500, 2.00m),
        (600, 1.50m),   // NEU: Bricht die 500-Level-Lücke Lv500→Lv1000
        (650, 1.50m),   // NEU: Entschärft tote Zone Lv600-750
        (750, 1.60m),   // NEU: Mid-Late-Game Kick
        (900, 1.40m),   // NEU: Letzter Boost vor Lv1000
        (1000, 3.00m)
    ];

    // ═══════════════════════════════════════════════════════════════════════
    // WORKSHOP - LEVEL & SLOTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Maximales Workshop-Level.</summary>
    public const int WorkshopMaxLevel = 1000;

    /// <summary>Alle X Level ein zusätzlicher Worker-Slot.</summary>
    public const int WorkerSlotInterval = 50;

    /// <summary>Maximum Worker pro Workshop.</summary>
    public const int WorkerSlotMax = 20;

    /// <summary>Maximum Ad-Bonus Worker-Slots pro Workshop.</summary>
    public const int MaxAdBonusWorkerSlots = 3;

    /// <summary>Workshop-Level ab dem die Spezialisierung verfügbar ist.</summary>
    public const int SpecializationUnlockLevel = 100;

    // ═══════════════════════════════════════════════════════════════════════
    // WORKSHOP - REBIRTH
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Rebirth-Einkommensbonus pro Stern (Index = Sterne - 1).</summary>
    public static readonly decimal[] RebirthIncomeBonuses = [0.15m, 0.35m, 0.60m, 1.00m, 1.50m];

    /// <summary>Rebirth-Upgrade-Rabatt pro Stern (Index = Sterne - 1).</summary>
    public static readonly decimal[] RebirthUpgradeDiscounts = [0.05m, 0.10m, 0.15m, 0.20m, 0.25m];

    /// <summary>
    /// Rebirth-Extra-Worker pro Stern (Index = Sterne).
    /// Stern 1 gibt sofort +1 Worker — sichtbarste Belohnung nach dem Erreichen von Lv1000.
    /// </summary>
    public static readonly int[] RebirthExtraWorkers = [0, 1, 1, 2, 2, 3];

    /// <summary>
    /// Maximaler Aura-Bonus durch S-Tier+ Worker pro Workshop (50%).
    /// Verhindert Exploit bei vielen Legendary-Workern.
    /// </summary>
    public const decimal MaxAuraBonus = 0.50m;

    // ═══════════════════════════════════════════════════════════════════════
    // WORKER - STIMMUNG & MÜDIGKEIT
    // ═══════════════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════════════
    // WORKER - LEVEL-FIT (Höhere Tiers besser für hohe Level)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Alle X Level gibt es -2% Effizienz für niedrige Tiers.</summary>
    public const int LevelPenaltyStep = 30;

    /// <summary>Penalty pro Level-Schritt.</summary>
    public const decimal LevelPenaltyPerStep = 0.02m;

    /// <summary>Minimum Level-Fit-Faktor (nie unter diesem Wert).</summary>
    public const decimal MinLevelFitFactor = 0.20m;

    // ═══════════════════════════════════════════════════════════════════════
    // BUILDING - KOSTEN & BONI
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Building-Kosten-Exponent (BaseCost * X^Level).</summary>
    public const int BuildingCostExponent = 2;

    /// <summary>Kantine: Stimmungs-Wiederherstellung pro Stunde pro Level.</summary>
    public const decimal CanteenMoodRecoveryPerLevel = 1.0m;

    /// <summary>Kantine: Ruhezeit-Reduktion pro Level [Level 1-5].</summary>
    public static readonly decimal[] CanteenRestReduction = [0m, 0.50m, 0.55m, 0.60m, 0.70m, 0.80m];

    /// <summary>Lager: Materialkosten-Reduktion pro Level [Level 1-5].</summary>
    public static readonly decimal[] StorageMaterialReduction = [0m, 0.15m, 0.25m, 0.35m, 0.45m, 0.50m];

    /// <summary>Fuhrpark: Auftrags-Belohnungsbonus pro Level [Level 1-5].</summary>
    public static readonly decimal[] VehicleFleetRewardBonus = [0m, 0.20m, 0.30m, 0.40m, 0.50m, 0.60m];

    /// <summary>Ausstellungsraum: Täglicher Ruf-Gewinn pro Level.</summary>
    public const decimal ShowroomReputationPerLevel = 0.5m;

    /// <summary>Ausbildungszentrum: Speed-Multiplikator pro Level.</summary>
    public const decimal TrainingCenterSpeedPerLevel = 0.5m;

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE - BONUS-PP (flat, NACH Tier-Multiplikator addiert)
    // ═══════════════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE - MEILENSTEINE (GS-Belohnungen, permanent)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prestige-Meilensteine: (benötigte Prestiges, ID, GS-Belohnung).
    /// Permanent — werden NICHT bei Ascension zurückgesetzt.
    /// </summary>
    public static readonly (int RequiredCount, string Id, int GoldenScrewReward)[] PrestigeMilestones =
    [
        (1,   "pm_first",  10),
        (5,   "pm_5",      20),
        (10,  "pm_10",     35),
        (25,  "pm_25",     50),
        (50,  "pm_50",     75),
        (100, "pm_100",    100),
    ];

    /// <summary>
    /// GAME-10: Maximale Kaufanzahl pro wiederholbarem Prestige-Shop-Item.
    /// Verhindert unendliche Skalierung der wiederholbaren Boni.
    /// </summary>
    public const int MaxRepeatableShopPurchases = 10;

    // ═══════════════════════════════════════════════════════════════════════
    // AUTO-PRODUKTION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Standard-Produktionsintervall in Sekunden (1 Item pro Worker).</summary>
    public const int AutoProductionIntervalSeconds = 180;

    /// <summary>InnovationLab: Schnelleres Produktionsintervall (Prestige-5-Bonus).</summary>
    public const int AutoProductionInnovationLabInterval = 120;

    /// <summary>MasterSmith behält sein bestehendes 60s-Intervall als Spezialeffekt.</summary>
    public const int AutoProductionMasterSmithInterval = 60;

    /// <summary>Workshop-Level ab dem Auto-Produktion freigeschaltet wird.</summary>
    public const int AutoProductionUnlockLevel = 50;

    /// <summary>Logarithmischer Skalierungsfaktor für Crafting-Verkaufspreise: log₂(1 + Level/Wert). Von 25 auf 15 gesenkt für stärkere Skalierung.</summary>
    public const double CraftingSellPriceLogDivisor = 15.0;

    // ═══════════════════════════════════════════════════════════════════════
    // LIEFERAUFTRÄGE (MaterialOrder)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Belohnungs-Multiplikator für Lieferaufträge.</summary>
    public const decimal MaterialOrderRewardMultiplier = 1.8m;

    /// <summary>XP-Multiplikator für Lieferaufträge.</summary>
    public const decimal MaterialOrderXpMultiplier = 1.5m;

    /// <summary>Maximale Lieferaufträge pro Tag.</summary>
    public const int MaterialOrdersPerDay = 3;

    /// <summary>Deadline für Lieferaufträge in Stunden.</summary>
    public const int MaterialOrderDeadlineHours = 4;

    /// <summary>Spieler-Level ab dem Cross-Workshop-Materialien gefordert werden.</summary>
    public const int MaterialOrderCrossWorkshopLevel = 100;

    // ═══════════════════════════════════════════════════════════════════════
    // AUFTRAGS-BELOHNUNGEN - SOFT-CAP
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Soft-Cap für den externen Order-Reward-Multiplikator (Research + Gebäude + Reputation + Events + Stammkunden + PrestigeShop).
    /// Ab diesem Wert greifen Diminishing Returns (Sqrt auf den Überschuss).
    /// Verhindert Multiplikator-Explosion bei voll ausgebauten Late-Game-Spielern.
    /// Beispiel: Raw 15x → 10 + sqrt(5) ≈ 12.24x
    /// </summary>
    public const decimal OrderRewardMultiplierSoftCap = 10.0m;
}
