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
}
