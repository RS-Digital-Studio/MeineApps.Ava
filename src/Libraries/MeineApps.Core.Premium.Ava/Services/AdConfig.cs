namespace MeineApps.Core.Premium.Ava.Services;

/// <summary>
/// AdMob Konfiguration fuer alle Apps.
/// Alle Apps nutzen Publisher-Account ca-app-pub-2588160251469436.
/// Jede App hat mehrere Rewarded Ad-Unit-IDs fuer verschiedene Placements.
/// </summary>
public static class AdConfig
{
    /// <summary>
    /// Test-Device-ID fuer Roberts Testgeraet (Sony Xperia).
    /// Muss registriert werden damit AdMob Test-Ads ausliefert.
    /// Neue Geraete: Logcat nach "setTestDeviceIds" suchen und ID hier eintragen.
    /// </summary>
    public const string TestDeviceId = "E2D5405C98B542FED211EF6D24EC35DE";

    // Test Ad Unit IDs (fuer Entwicklung)
    public static class Test
    {
        public const string BannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";
        public const string InterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";
        public const string RewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
    }

    // Production Ad Unit IDs

    // RechnerPlus + ZeitManager: Werbefrei, keine AdMob-Integration

    public static class HandwerkerRechner
    {
        public const string AppId = "ca-app-pub-2588160251469436~1938872706";
        public const string BannerAdUnitId = "ca-app-pub-2588160251469436/2922272572";
        // Rewarded Ad Placements
        public const string RewardedExtendedHistory = "ca-app-pub-2588160251469436/6069708884";
        public const string RewardedPremiumAccess = "ca-app-pub-2588160251469436/2014913801";
        public const string RewardedProjectExport = "ca-app-pub-2588160251469436/6341171628";
        public const string RewardedMaterialPdf = "ca-app-pub-2588160251469436/5100242147";
    }

    public static class FinanzRechner
    {
        public const string AppId = "ca-app-pub-2588160251469436~8528331789";
        public const string BannerAdUnitId = "ca-app-pub-2588160251469436/5301023904";
        // Rewarded Ad Placements
        public const string RewardedBudgetAnalysis = "ca-app-pub-2588160251469436/5671823872";
        public const string RewardedExtendedStats = "ca-app-pub-2588160251469436/6984905545";
        public const string RewardedExportPdf = "ca-app-pub-2588160251469436/3424934485";
        public const string RewardedExportCsv = "ca-app-pub-2588160251469436/1356438259";
    }

    public static class FitnessRechner
    {
        public const string AppId = "ca-app-pub-2588160251469436~1827192061";
        public const string BannerAdUnitId = "ca-app-pub-2588160251469436/3276005103";
        // Rewarded Ad Placements
        public const string RewardedBarcodeScan = "ca-app-pub-2588160251469436/7079538226";
        public const string RewardedDetailAnalysis = "ca-app-pub-2588160251469436/6445957290";
        public const string RewardedExtendedFoodDb = "ca-app-pub-2588160251469436/9527308404";
        public const string RewardedTrackingExport = "ca-app-pub-2588160251469436/8688133362";
    }

    public static class WorkTimePro
    {
        public const string AppId = "ca-app-pub-2588160251469436~9866108383";
        public const string BannerAdUnitId = "ca-app-pub-2588160251469436/8991611030";
        // Rewarded Ad Placements
        public const string RewardedExport = "ca-app-pub-2588160251469436/4893202474";
        public const string RewardedMonthlyStats = "ca-app-pub-2588160251469436/6901145068";
        public const string RewardedVacationEntry = "ca-app-pub-2588160251469436/1747320492";
    }

    public static class BomberBlast
    {
        public const string AppId = "ca-app-pub-2588160251469436~8809763733";
        public const string BannerAdUnitId = "ca-app-pub-2588160251469436/5589430378";
        // Rewarded Ad Placements
        public const string RewardedContinue = "ca-app-pub-2588160251469436/1959917378";
        public const string RewardedLevelSkip = "ca-app-pub-2588160251469436/6793333855";
        public const string RewardedPowerUp = "ca-app-pub-2588160251469436/8246016321";
        public const string RewardedScoreDouble = "ca-app-pub-2588160251469436/6242669514";
        public const string RewardedRevival = "ca-app-pub-2588160251469436/2795298966";
        public const string RewardedExtraDailySpin = "ca-app-pub-2588160251469436/4271299647";
        public const string RewardedCoinMultiplier = "ca-app-pub-2588160251469436/2746464989";
        public const string RewardedFreeShopUpgrade = "ca-app-pub-2588160251469436/2958217975";
        public const string RewardedDungeonExtraBuff = "ca-app-pub-2588160251469436/3717454234";
        public const string RewardedGemBonus = "ca-app-pub-2588160251469436/1171181949";
        public const string RewardedLuckySpin = "ca-app-pub-2588160251469436/8778881674";
        public const string RewardedDungeonRun = "ca-app-pub-2588160251469436/1574490715";
    }

    public static class HandwerkerImperium
    {
        public const string AppId = "ca-app-pub-2588160251469436~3907946957";
        public const string BannerAdUnitId = "ca-app-pub-2588160251469436/5062090417";
        // Rewarded Ad Placements
        public const string RewardedGoldenScrews = "ca-app-pub-2588160251469436/6824989999";
        public const string RewardedScoreDouble = "ca-app-pub-2588160251469436/5743852354";
        public const string RewardedMarketRefresh = "ca-app-pub-2588160251469436/4873623815";
        public const string RewardedWorkshopSpeedup = "ca-app-pub-2588160251469436/4199864062";
        public const string RewardedWorkshopUnlock = "ca-app-pub-2588160251469436/5009646761";
        public const string RewardedWorkerHireBonus = "ca-app-pub-2588160251469436/8641779750";
        public const string RewardedResearchSpeedup = "ca-app-pub-2588160251469436/3237400093";
        public const string RewardedDailyChallengeRetry = "ca-app-pub-2588160251469436/7787863083";
        public const string RewardedAchievementBoost = "ca-app-pub-2588160251469436/9763289738";
        public const string RewardedOfflineDouble = "ca-app-pub-2588160251469436/5018911702";
    }

    /// <summary>Banner Ad-Unit-ID je nach Build-Konfiguration</summary>
    public static string GetBannerAdUnitId(string appName)
    {
#if DEBUG
        return Test.BannerAdUnitId;
#else
        return appName switch
        {
            "HandwerkerRechner" => HandwerkerRechner.BannerAdUnitId,
            "FinanzRechner" => FinanzRechner.BannerAdUnitId,
            "FitnessRechner" => FitnessRechner.BannerAdUnitId,
            "WorkTimePro" => WorkTimePro.BannerAdUnitId,
            "BomberBlast" => BomberBlast.BannerAdUnitId,
            "HandwerkerImperium" => HandwerkerImperium.BannerAdUnitId,
            _ => Test.BannerAdUnitId
        };
#endif
    }

    /// <summary>
    /// Rewarded Ad-Unit-ID fuer ein bestimmtes Placement.
    /// Im DEBUG-Modus wird immer die Test-ID verwendet.
    /// </summary>
    /// <param name="appName">App-Name (z.B. "BomberBlast")</param>
    /// <param name="placement">Placement-Name (z.B. "continue", "score_double"). Null = Default.</param>
    public static string GetRewardedAdUnitId(string appName, string? placement = null)
    {
#if DEBUG
        return Test.RewardedAdUnitId;
#else
        var id = (appName, placement) switch
        {
            // BomberBlast
            ("BomberBlast", "continue") => BomberBlast.RewardedContinue,
            ("BomberBlast", "level_skip") => BomberBlast.RewardedLevelSkip,
            ("BomberBlast", "power_up") => BomberBlast.RewardedPowerUp,
            ("BomberBlast", "score_double") => BomberBlast.RewardedScoreDouble,
            ("BomberBlast", "revival") => BomberBlast.RewardedRevival,
            ("BomberBlast", "extra_daily_spin") => BomberBlast.RewardedExtraDailySpin,
            ("BomberBlast", "coin_multiplier") => BomberBlast.RewardedCoinMultiplier,
            ("BomberBlast", "free_shop_upgrade") => BomberBlast.RewardedFreeShopUpgrade,
            ("BomberBlast", "dungeon_extra_buff") => BomberBlast.RewardedDungeonExtraBuff,
            ("BomberBlast", "gem_bonus") => BomberBlast.RewardedGemBonus,
            ("BomberBlast", "lucky_spin") => BomberBlast.RewardedLuckySpin,
            ("BomberBlast", "dungeon_run") => BomberBlast.RewardedDungeonRun,
            ("BomberBlast", _) => BomberBlast.RewardedContinue,

            // FinanzRechner
            ("FinanzRechner", "export_pdf") => FinanzRechner.RewardedExportPdf,
            ("FinanzRechner", "export_csv") => FinanzRechner.RewardedExportCsv,
            ("FinanzRechner", "budget_analysis") => FinanzRechner.RewardedBudgetAnalysis,
            ("FinanzRechner", "extended_stats") => FinanzRechner.RewardedExtendedStats,
            ("FinanzRechner", _) => FinanzRechner.RewardedExportPdf,

            // HandwerkerRechner
            ("HandwerkerRechner", "premium_access") => HandwerkerRechner.RewardedPremiumAccess,
            ("HandwerkerRechner", "material_pdf") => HandwerkerRechner.RewardedMaterialPdf,
            ("HandwerkerRechner", "project_export") => HandwerkerRechner.RewardedProjectExport,
            ("HandwerkerRechner", "extended_history") => HandwerkerRechner.RewardedExtendedHistory,
            ("HandwerkerRechner", _) => HandwerkerRechner.RewardedPremiumAccess,

            // FitnessRechner
            ("FitnessRechner", "barcode_scan") => FitnessRechner.RewardedBarcodeScan,
            ("FitnessRechner", "detail_analysis") => FitnessRechner.RewardedDetailAnalysis,
            ("FitnessRechner", "extended_food_db") => FitnessRechner.RewardedExtendedFoodDb,
            ("FitnessRechner", "tracking_export") => FitnessRechner.RewardedTrackingExport,
            ("FitnessRechner", _) => FitnessRechner.RewardedBarcodeScan,

            // WorkTimePro
            ("WorkTimePro", "export") => WorkTimePro.RewardedExport,
            ("WorkTimePro", "monthly_stats") => WorkTimePro.RewardedMonthlyStats,
            ("WorkTimePro", "vacation_entry") => WorkTimePro.RewardedVacationEntry,
            ("WorkTimePro", _) => WorkTimePro.RewardedExport,

            // HandwerkerImperium
            ("HandwerkerImperium", "golden_screws") => HandwerkerImperium.RewardedGoldenScrews,
            ("HandwerkerImperium", "score_double") => HandwerkerImperium.RewardedScoreDouble,
            ("HandwerkerImperium", "market_refresh") => HandwerkerImperium.RewardedMarketRefresh,
            ("HandwerkerImperium", "workshop_speedup") => HandwerkerImperium.RewardedWorkshopSpeedup,
            ("HandwerkerImperium", "workshop_unlock") => HandwerkerImperium.RewardedWorkshopUnlock,
            ("HandwerkerImperium", "worker_hire_bonus") => HandwerkerImperium.RewardedWorkerHireBonus,
            ("HandwerkerImperium", "research_speedup") => HandwerkerImperium.RewardedResearchSpeedup,
            ("HandwerkerImperium", "daily_challenge_retry") => HandwerkerImperium.RewardedDailyChallengeRetry,
            ("HandwerkerImperium", "achievement_boost") => HandwerkerImperium.RewardedAchievementBoost,
            ("HandwerkerImperium", "offline_double") => HandwerkerImperium.RewardedOfflineDouble,
            ("HandwerkerImperium", _) => HandwerkerImperium.RewardedGoldenScrews,

            _ => ""
        };
        // Fallback auf Test-ID wenn noch keine echte ID konfiguriert
        return string.IsNullOrEmpty(id) ? Test.RewardedAdUnitId : id;
#endif
    }
}
