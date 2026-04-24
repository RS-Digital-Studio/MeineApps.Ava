namespace HandwerkerImperium.Models;

/// <summary>
/// Zentraler Katalog aller Analytics-Event-Namen.
/// Ein Place-to-Change verhindert Inkonsistenzen zwischen Instrumentierung und Auswertung.
/// Naming: snake_case, semantisch vor Struktur (z.B. <c>workshop_unlocked</c> statt <c>unlock_workshop</c>).
/// </summary>
public static class AnalyticsEvents
{
    // Session & Retention
    public const string SessionStart = "session_start";
    public const string SessionEnd = "session_end";
    public const string RetentionDay = "retention_day";
    public const string AppOpen = "app_open";
    public const string AppPause = "app_pause";
    public const string AppResume = "app_resume";

    // Tutorial / Onboarding
    public const string TutorialStep = "tutorial_step";
    public const string TutorialComplete = "tutorial_complete";
    public const string WelcomeSeen = "welcome_seen";

    // Core Gameplay Unlocks / Meilensteine
    public const string WorkshopUnlocked = "workshop_unlocked";
    public const string FirstOrderAccepted = "first_order_accepted";
    public const string FirstMiniGamePlayed = "first_minigame_played";
    public const string FeatureUnlocked = "feature_unlocked"; // guild, crafting, tournament, battle_pass
    public const string LevelUp = "level_up";

    // Progression
    public const string PrestigeDone = "prestige_done";
    public const string AscensionDone = "ascension_done";
    public const string RebirthDone = "rebirth_done";
    public const string ResearchCompleted = "research_completed";
    public const string BuildingUpgraded = "building_upgraded";
    public const string AchievementUnlocked = "achievement_unlocked";

    // Mini-Games
    public const string MiniGamePlayed = "minigame_played";
    public const string MiniGamePerfect = "minigame_perfect";
    public const string AutoCompleteUsed = "auto_complete_used";

    // Monetisierung (IAP)
    public const string IapShopViewed = "iap_shop_viewed";
    public const string IapItemViewed = "iap_item_viewed";
    public const string IapPurchaseStarted = "iap_purchase_started";
    public const string IapPurchaseSuccess = "iap_purchase_success";
    public const string IapPurchaseFailed = "iap_purchase_failed";
    public const string IapPurchaseCancelled = "iap_purchase_cancelled";

    // Monetisierung (Rewarded Ads)
    public const string AdRequested = "ad_requested";
    public const string AdShown = "ad_shown";
    public const string AdRewarded = "ad_rewarded";
    public const string AdFailed = "ad_failed";
    public const string AdDismissed = "ad_dismissed";

    // Gilden / Social
    public const string GuildJoined = "guild_joined";
    public const string GuildCreated = "guild_created";
    public const string GuildLeft = "guild_left";
    public const string GuildBossHit = "guild_boss_hit";
    public const string GuildWarJoined = "guild_war_joined";

    // Economy
    public const string OrderCompleted = "order_completed";
    public const string WorkerHired = "worker_hired";
    public const string OfflineEarningsClaimed = "offline_earnings_claimed";
    public const string DailyRewardClaimed = "daily_reward_claimed";
    public const string LuckySpinPlayed = "lucky_spin_played";

    // Cloud-Save
    public const string CloudSaveUploaded = "cloud_save_uploaded";
    public const string CloudSaveDownloaded = "cloud_save_downloaded";
    public const string CloudSaveConflict = "cloud_save_conflict";

    // Fehler (wichtig fuer QA)
    public const string ErrorOccurred = "error_occurred";
}

/// <summary>
/// Zentraler Katalog der User-Properties, die an jedem Event haengen.
/// </summary>
public static class AnalyticsUserProperties
{
    public const string Language = "language";
    public const string Premium = "premium";
    public const string PrestigeTier = "prestige_tier";
    public const string AscensionLevel = "ascension_level";
    public const string PlayerLevel = "player_level";
    public const string GraphicsQuality = "graphics_quality";
    public const string DaysSinceInstall = "days_since_install";
    public const string AppVersion = "app_version";
}
