namespace HandwerkerImperium.Models;

/// <summary>
/// Zentraler Katalog aller Remote-Config-Keys.
/// Eine Aenderung dieser Strings veraendert den Lookup-Pfad — immer beide Seiten (Firebase-DB + Default) aktualisieren.
/// </summary>
public static class RemoteConfigKeys
{
    // --- Balancing-Overrides ---------------------------------------------------------------

    /// <summary>Level ab dem das Starter-Angebot erscheint. Default: 10.</summary>
    public const string StarterOfferMinLevel = "balancing.starter_offer_min_level";

    /// <summary>Offline-Maximum in Stunden. Default: 8.</summary>
    public const string OfflineEarningsMaxHours = "balancing.offline_earnings_max_hours";

    /// <summary>Intervall (Sekunden) zwischen moeglichen Lieferant-Belohnungen. Default: 120.</summary>
    public const string DeliveryIntervalMinSec = "balancing.delivery_min_sec";

    /// <summary>Intervall (Sekunden) max. Default: 300.</summary>
    public const string DeliveryIntervalMaxSec = "balancing.delivery_max_sec";

    /// <summary>Anzahl MiniGame-Perfect-Ratings bis Auto-Complete freigeschaltet ist. Default: 30.</summary>
    public const string AutoCompletePerfectThreshold = "balancing.auto_complete_threshold";

    /// <summary>Anzahl MiniGame-Perfect-Ratings bei Premium. Default: 15.</summary>
    public const string AutoCompletePerfectThresholdPremium = "balancing.auto_complete_threshold_premium";

    // --- Feature-Flags ---------------------------------------------------------------------

    /// <summary>Ob saisonale Partikel-Effekte aktiv sind. Default: true.</summary>
    public const string SeasonalEffectsEnabled = "features.seasonal_effects_enabled";

    /// <summary>Ob Cloud-Save fuer neue Nutzer default-aktiviert ist. Default: true.</summary>
    public const string CloudSaveDefaultEnabled = "features.cloud_save_default";

    /// <summary>Ob das Starter-Angebot global eingeblendet wird. Default: true.</summary>
    public const string StarterOfferEnabled = "features.starter_offer_enabled";

    // --- Store-Preise (nur Anzeige, echte Preise kommen aus Google Play) -------------------

    /// <summary>Gibt an ob ein spezieller Promo-Banner aktiv ist. Default: false.</summary>
    public const string PromoBannerActive = "promo.banner_active";

    /// <summary>Textkey fuer den Promo-Banner (RESX-Key). Default: leer.</summary>
    public const string PromoBannerTextKey = "promo.banner_text_key";

    // --- Order-Generator-Tuning (P1.1) -----------------------------------------------------

    /// <summary>Globaler Difficulty-Multiplier auf Order-Werte. Default: 1.0.</summary>
    public const string OrderDifficultyMultiplier = "balancing.order_difficulty_multiplier";

    /// <summary>Wahrscheinlichkeit pro Spawn-Slot fuer Live-Order. Default: 0.5.</summary>
    public const string LiveOrderSpawnChance = "balancing.live_order_spawn_chance";

    /// <summary>Wahrscheinlichkeit dass eine Live-Order Premium/VIP ist. Default: 0.05.</summary>
    public const string LiveOrderPremiumChance = "balancing.live_order_premium_chance";

    /// <summary>Worker-Markt-Gewichtung als CSV "F=20,E=22,D=22,C=14,B=10,A=6,S=3,SS=1.5,SSS=0.5,Legendary=0.1".</summary>
    public const string WorkerMarketWeights = "balancing.worker_market_weights";

    // --- Monetization-Hooks (P1.1) ---------------------------------------------------------

    /// <summary>Fallback-Preis fuer Premium IAP wenn Store-Fetch fehlschlaegt. Default: "4.99 EUR".</summary>
    public const string PremiumPriceFallback = "monetization.premium_price_fallback";

    /// <summary>Anzahl GS pro Rewarded-Ad im Goldschrauben-Placement. Default: 8.</summary>
    public const string GoldenScrewAdReward = "monetization.golden_screw_ad_reward";

    /// <summary>Cooldown (Stunden) fuer GS-Rewarded-Ad. Default: 4.</summary>
    public const string GoldenScrewAdCooldownHours = "monetization.golden_screw_cooldown_hours";

    /// <summary>Cooldown (Stunden) fuer Shop-Rewarded-Ad. Default: 3.</summary>
    public const string ShopAdCooldownHours = "monetization.shop_reward_cooldown_hours";

    /// <summary>Ob das Daily-Bundle-System aktiv ist (P1.3 Foundation). Default: false.</summary>
    public const string DailyBundleEnabled = "monetization.daily_bundle_enabled";

    /// <summary>JSON-Array mit 7 Bundle-SKUs (Mo-So). Default: leer.</summary>
    public const string DailyBundleSkus = "monetization.daily_bundle_skus";

    // --- Events (P1.1) ---------------------------------------------------------------------

    /// <summary>Override fuer SeasonalEvent (Spring/Summer/Autumn/Winter). Bug-Out-Switch. Default: leer.</summary>
    public const string SeasonalThemeOverride = "events.seasonal_theme_override";

    /// <summary>JSON-Gewichte fuer 8 LuckySpin-Segmente. Default: leer (Code-Default).</summary>
    public const string LuckySpinSegmentWeights = "events.lucky_spin_segment_weights";

    // --- Feature-Kill-Switches (P1.1) ------------------------------------------------------

    /// <summary>Kill-Switch fuer Co-op-Auftraege bei Bug. Default: true.</summary>
    public const string CoopOrdersEnabled = "features.coop_orders_enabled";

    /// <summary>Kill-Switch fuer Worker-Auktionen bei Bug. Default: true.</summary>
    public const string AuctionsEnabled = "features.auctions_enabled";

    // --- Marketing (P1.1) ------------------------------------------------------------------

    /// <summary>Cross-Promo-Banner aktiv? Default: false.</summary>
    public const string CrossPromoBannerActive = "marketing.cross_promo_active";

    /// <summary>Cross-Promo Ziel-SKU oder Deeplink. Default: leer.</summary>
    public const string CrossPromoTarget = "marketing.cross_promo_target";

    // --- UX (P2.2 + P1.1) ------------------------------------------------------------------

    /// <summary>Anzahl Dialoge im Onboarding-Flow. 0 = aggressives Skip-Profil. Default: 1.</summary>
    public const string OnboardingDialogCount = "ux.onboarding_dialog_count";

    /// <summary>Skip-Button in Story Ch.1 sichtbar? Default: true.</summary>
    public const string OnboardingStorySkipEnabled = "ux.onboarding_story_skip";
}
