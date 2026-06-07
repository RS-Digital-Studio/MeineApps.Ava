namespace HandwerkerImperium.Domain.LiveOps
{
    // Wert-Enums der Live-Ops-/Retention-Systeme. 1:1-Port aus dem Avalonia-Original
    // (Models/DailyChallenge.cs, WeeklyMission.cs, DailyReward.cs, LuckySpin.cs, Tournament.cs,
    // SeasonalEvent.cs, WelcomeBackOffer.cs, LiveEvent.cs, Enums/BattlePassRewardType.cs).
    // Enum-Reihenfolge/-Werte = Persistenz-Integer — exakt erhalten.

    /// <summary>Typ einer täglichen Herausforderung.</summary>
    public enum DailyChallengeType
    {
        CompleteOrders,
        EarnMoney,
        UpgradeWorkshop,
        HireWorker,
        CompleteQuickJob,
        PlayMiniGames,
        AchieveMinigameScore,
        TrainWorker = 7,
        CompleteCrafting = 8,
        AchievePerfectStreak = 9,
        ReachWorkshopLevel = 10,
        ProduceItems = 11,
        SellItems = 12,
        CompleteMaterialOrder = 13,
        CollectEquipment = 14
    }

    /// <summary>Typ einer wöchentlichen Mission.</summary>
    public enum WeeklyMissionType
    {
        CompleteOrders,
        EarnMoney,
        UpgradeWorkshops,
        HireWorkers,
        PlayMiniGames,
        CompleteDailyChallenges,
        AchievePerfectRatings,
        TrainWorkers = 7,
        CompleteCraftings = 8,
        AchievePerfectStreak = 9,
        ReachWorkshopLevels = 10,
        ProduceItems = 11,
        SellItems = 12,
        CompleteMaterialOrders = 13,
        CollectEquipment = 14
    }

    /// <summary>Art der Battle-Pass-Belohnung.</summary>
    public enum BattlePassRewardType
    {
        /// <summary>Standard: Geld + XP + GS.</summary>
        Standard = 0,
        /// <summary>Temporärer Speed-Boost.</summary>
        SpeedBoost = 1
    }

    /// <summary>Typ eines Glücksrad-Gewinns.</summary>
    public enum LuckySpinPrizeType
    {
        MoneySmall,
        MoneyMedium,
        MoneyLarge,
        XpBoost,
        GoldenScrews5,
        SpeedBoost,
        ToolUpgrade,
        Jackpot50
    }

    /// <summary>Belohnungsstufe im Turnier.</summary>
    public enum TournamentRewardTier
    {
        None,
        Bronze,
        Silver,
        Gold
    }

    /// <summary>Saison-Typ (4 pro Jahr).</summary>
    public enum Season
    {
        Spring,  // 1.-14. März
        Summer,  // 1.-14. Juni
        Autumn,  // 1.-14. September
        Winter   // 1.-14. Dezember
    }

    /// <summary>Typ des Welcome-Back-Angebots.</summary>
    public enum WelcomeBackOfferType
    {
        /// <summary>Standard-Angebot bei 24h+ Abwesenheit.</summary>
        Standard,
        /// <summary>Premium-Angebot bei 72h+ Abwesenheit (50% mehr).</summary>
        Premium,
        /// <summary>Einmaliges Starter-Paket für neue Spieler (Level 5-15).</summary>
        StarterPack
    }

    /// <summary>Spezial-Bonus-Typen für tägliche Belohnungen.</summary>
    public enum DailyBonusType
    {
        /// <summary>Kein Spezial-Bonus.</summary>
        None,
        /// <summary>2x Einkommens-Speed-Boost für 1 Stunde.</summary>
        SpeedBoost,
        /// <summary>50% mehr XP für 1 Stunde.</summary>
        XpBoost,
        /// <summary>Sofortiger Gratis-Worker.</summary>
        FreeWorker
    }

    /// <summary>4 Live-Event-Templates.</summary>
    public enum LiveEventTemplate
    {
        /// <summary>Doppelte Auftrags-Belohnungen.</summary>
        DoubleReward,
        /// <summary>Boss-Rush mit besonderem Spawn-Boost.</summary>
        BossRush,
        /// <summary>Co-op-Marathon: doppelte Co-op-Auftrags-Belohnungen.</summary>
        CoopMarathon,
        /// <summary>Mini-Game-Mastery: Perfekte Ratings geben Bonus-GS.</summary>
        MiniGameMastery
    }
}
