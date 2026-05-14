namespace BomberBlast.Services;

/// <summary>
/// Firebase Analytics — Funnel + Cohort-Tracking. 30+ Events vordefiniert.
/// Code-Hooks vorbereitet (v2.0.44 — ). Console-Setup macht Robert.
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
/// Standard-Event-Namen (v2.0.44 +.2 — 40+ Funnel-Events).
/// Werden in GameEngine, ShopViewModel, etc. aufgerufen.
/// </summary>
public static class AnalyticsEvents
{
    // Lifecycle
    public const string AppOpen = "app_open";
    public const string AppClose = "app_close";
    public const string FirstLaunch = "first_launch";
    public const string TutorialStart = "tutorial_start";
    public const string TutorialStepComplete = "tutorial_step_complete";  // granularer Schritt
    public const string TutorialComplete = "tutorial_complete";
    public const string TutorialSkip = "tutorial_skip";

    // Story-Mode
    public const string LevelStart = "level_start";          // Params: level_id, world_id, lives, deck_id
    public const string LevelComplete = "level_complete";    // Params: level_id, time_ms, stars, deaths
    public const string LevelFailed = "level_failed";        // Params: level_id, cause, attempt_count
    public const string LevelSkip = "level_skip";            // via Rewarded Ad
    public const string WorldUnlock = "world_unlock";
    public const string BossEncounter = "boss_encounter";    // Params: boss_type, phase
    public const string BossDefeated = "boss_defeated";      // Params: boss_type, time_ms, damage_taken
    public const string BossKilled = "boss_killed";          // Legacy — bleibt fuer Rueckwaertskompatibilitaet
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
    public const string DailyLogin = "daily_login";          // Params: consecutive_days
    public const string DailyRewardClaimed = "daily_reward_claimed";
    public const string DailyMissionComplete = "daily_mission_complete";
    public const string WeeklyMissionComplete = "weekly_mission_complete";
    public const string LuckySpinUsed = "lucky_spin";
    public const string BattlePassTierUp = "battle_pass_tier_up";
    public const string LeaguePromotion = "league_promotion";
    public const string LeagueDemotion = "league_demotion";
    public const string FeatureUnlocked = "feature_unlocked";  // Params: feature_id, session_count

    // Monetization
    public const string ShopOpened = "shop_opened";          // Params: entry_point
    public const string ShopPurchase = "shop_purchase";
    public const string PurchaseFlowStart = "purchase_flow_start";  // Params: sku, price_cents, currency
    public const string PurchaseSuccess = "purchase_success";       // Params: sku, price_cents, currency
    public const string PurchaseCancel = "purchase_cancel";         // Params: sku
    public const string PurchaseFail = "purchase_fail";             // Params: sku, error_code
    public const string GemPurchaseStart = "gem_purchase_start";    // Legacy — siehe PurchaseFlowStart
    public const string GemPurchaseComplete = "gem_purchase_complete";  // Legacy — siehe PurchaseSuccess
    public const string PremiumPurchase = "premium_purchase";
    public const string RewardedAdRequest = "rewarded_ad_request";  // Params: placement
    public const string RewardedAdCompleted = "rewarded_ad_completed";  // Params: placement
    public const string AdWatched = "ad_watched";                   // Legacy
    public const string CardCrafted = "card_crafted";
    public const string DealClaimed = "rotating_deal_claimed";
    public const string StarterPackClaimed = "starter_pack_claimed";

    // Combat / Game-Feel
    public const string ComboTierReached = "combo_tier_reached";    // Params: tier (5/10), level_id

    // Settings/Privacy
    public const string SettingsOpen = "settings_open";
    public const string AccessibilityToggle = "accessibility_toggle";
    public const string AccountDeletion = "account_deletion";
    public const string FrameRateChange = "frame_rate_change";
}

/// <summary>
/// Standard-Parameter-Namen (.2 . Vermeidet Typos beim Ueberfliegen
/// von Firebase-Dashboards (Analytics-Schluessel sind case-sensitive).
/// </summary>
public static class AnalyticsParams
{
    public const string LevelId = "level_id";
    public const string WorldId = "world_id";
    public const string Lives = "lives";
    public const string DeckId = "deck_id";
    public const string TimeMs = "time_ms";
    public const string Stars = "stars";
    public const string Deaths = "deaths";
    public const string Cause = "cause";              // enemy/bomb/time/curse/...
    public const string AttemptCount = "attempt_count";
    public const string BossType = "boss_type";
    public const string Phase = "phase";
    public const string DamageTaken = "damage_taken";
    public const string EntryPoint = "entry_point";   // shop/level_complete/main_menu/...
    public const string Sku = "sku";
    public const string PriceCents = "price_cents";
    public const string Currency = "currency";
    public const string ErrorCode = "error_code";
    public const string Placement = "placement";       // rewarded ad placement
    public const string StepId = "step_id";
    public const string FeatureId = "feature_id";
    public const string SessionCount = "session_count";
    public const string ConsecutiveDays = "consecutive_days";
    public const string Tier = "tier";                 // combo tier
    public const string Mode = "mode";                 // Story/Survival/Dungeon/BossRush/QuickPlay/Daily/Master
    public const string TotalComboCount = "total_combo_count"; // MEGA/ULTRA-Combos in einem Level
}

/// <summary>No-Op für Desktop / nicht konfigurierte Firebase-Setup.</summary>
public sealed class NullAnalyticsService : IAnalyticsService
{
    public void Initialize() { }
    public void LogEvent(string eventName, IReadOnlyDictionary<string, object>? parameters = null) { }
    public void SetUserProperty(string name, string? value) { }
    public void SetAnalyticsCollectionEnabled(bool enabled) { }
}
