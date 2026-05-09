namespace BomberBlast.Services;

/// <summary>
/// Firebase Analytics — Funnel + Cohort-Tracking. 30+ Events vordefiniert.
/// Code-Hooks vorbereitet (v2.0.44 — AAA-Audit). Console-Setup macht Robert.
/// </summary>
public interface IAnalyticsService
{
    void Initialize();

    /// <summary>
    /// Sendet ein Event mit optionalen Parametern. Parameter-Werte werden auf 100 Zeichen geclamped.
    /// </summary>
    void LogEvent(string eventName, IReadOnlyDictionary<string, object>? parameters = null);

    /// <summary>User-Property setzen (für Cohorts: Premium/Free, Welt-erreicht, Master-Mode-Active).</summary>
    void SetUserProperty(string name, string? value);

    /// <summary>Erlaubt dem User Tracking auszuschalten (DSGVO).</summary>
    void SetAnalyticsCollectionEnabled(bool enabled);
}

/// <summary>
/// Standard-Event-Namen (v2.0.44 — 30+ Funnel-Events).
/// Werden in GameEngine, ShopViewModel, etc. aufgerufen.
/// </summary>
public static class AnalyticsEvents
{
    // Lifecycle
    public const string AppOpen = "app_open";
    public const string AppClose = "app_close";
    public const string FirstLaunch = "first_launch";
    public const string TutorialStart = "tutorial_start";
    public const string TutorialComplete = "tutorial_complete";
    public const string TutorialSkip = "tutorial_skip";

    // Story-Mode
    public const string LevelStart = "level_start";
    public const string LevelComplete = "level_complete";
    public const string LevelFailed = "level_failed";
    public const string LevelSkip = "level_skip"; // via Rewarded Ad
    public const string WorldUnlock = "world_unlock";
    public const string BossKilled = "boss_killed";
    public const string ThreeStarsAchieved = "three_stars";

    // Modes
    public const string DungeonRunStart = "dungeon_run_start";
    public const string DungeonRunComplete = "dungeon_run_complete";
    public const string SurvivalStart = "survival_start";
    public const string DailyChallengeStart = "daily_challenge_start";
    public const string DailyRaceStart = "daily_race_start";
    public const string BossRushStart = "boss_rush_start";
    public const string MasterModeToggle = "master_mode_toggle";

    // Live-Ops
    public const string DailyRewardClaimed = "daily_reward_claimed";
    public const string DailyMissionComplete = "daily_mission_complete";
    public const string WeeklyMissionComplete = "weekly_mission_complete";
    public const string LuckySpinUsed = "lucky_spin";
    public const string BattlePassTierUp = "battle_pass_tier_up";
    public const string LeaguePromotion = "league_promotion";
    public const string LeagueDemotion = "league_demotion";

    // Monetization
    public const string ShopPurchase = "shop_purchase";
    public const string GemPurchaseStart = "gem_purchase_start";
    public const string GemPurchaseComplete = "gem_purchase_complete";
    public const string PremiumPurchase = "premium_purchase";
    public const string AdWatched = "ad_watched";
    public const string CardCrafted = "card_crafted";
    public const string DealClaimed = "rotating_deal_claimed";
    public const string StarterPackClaimed = "starter_pack_claimed";

    // Settings/Privacy
    public const string SettingsOpen = "settings_open";
    public const string AccessibilityToggle = "accessibility_toggle";
    public const string AccountDeletion = "account_deletion";
    public const string FrameRateChange = "frame_rate_change";
}

/// <summary>No-Op für Desktop / nicht konfigurierte Firebase-Setup.</summary>
public sealed class NullAnalyticsService : IAnalyticsService
{
    public void Initialize() { }
    public void LogEvent(string eventName, IReadOnlyDictionary<string, object>? parameters = null) { }
    public void SetUserProperty(string name, string? value) { }
    public void SetAnalyticsCollectionEnabled(bool enabled) { }
}
