namespace BomberBlast.Services;

/// <summary>
/// Remote-Config-Service (.4c Stub +.1 .
///
/// Erlaubt Live-Tuning von Werten ohne App-Update:
/// - Event-Aktivierung (event_active_halloween, event_active_christmas, ...)
/// - Drop-Raten (drop_rate_legendary_card, drop_rate_epic_card, ...)
/// - Preise (gem_pack_small_price, starter_pack_discount_pct, ...)
/// - Difficulty-Werte (combo_slowmo_threshold, boss_telegraph_duration_ms, ...)
///
/// Default-Werte werden lokal als Fallback gehalten. Remote ueberschreibt nach
/// FetchAndActivateAsync. Cache 1h Production, 5 Min Debug.
///
/// IMPLEMENTIERUNGEN:
/// - <see cref="NullRemoteConfigService"/> — Desktop/Test-Default, liefert nur lokale Werte
/// - FirebaseRemoteConfigService — Android, sucht google-services.json + Firebase Remote Config 
/// </summary>
public interface IRemoteConfigService
{
    /// <summary>Initialisiert den Service (laedt Default-Werte, registriert Firebase wenn verfuegbar).</summary>
    Task InitializeAsync();

    /// <summary>
    /// Holt die neuesten Remote-Werte vom Server und aktiviert sie sofort.
    /// Gibt true zurueck wenn ein Update angekommen ist (Werte koennen geaendert sein).
    /// Bei Netzwerk-Fehler: silent fallback auf gecachte/Default-Werte, return false.
    /// </summary>
    Task<bool> FetchAndActivateAsync();

    /// <summary>Liefert einen Boolean-Wert (typisch Feature-Flags / Event-Toggles).</summary>
    bool GetBool(string key, bool defaultValue);

    /// <summary>Liefert einen Long-Wert (typisch Counter, Schwellenwerte).</summary>
    long GetLong(string key, long defaultValue);

    /// <summary>Liefert einen Double-Wert (typisch Drop-Raten, Multiplikatoren).</summary>
    double GetDouble(string key, double defaultValue);

    /// <summary>Liefert einen String-Wert (typisch URLs, Konfigurations-IDs).</summary>
    string GetString(string key, string defaultValue);

    /// <summary>
    /// Wird gefeuert nachdem ein erfolgreicher FetchAndActivate neue Werte bringt.
    /// Subscriber sollten ggf. ihre internen Caches invalidieren.
    /// </summary>
    event Action? ConfigChanged;
}

/// <summary>
/// Standard-Schluessel fuer Remote-Config (Single-Source-of-Truth fuer Tipp-Sicherheit).
/// Verhindert Typos und macht refactor-sicher.
/// </summary>
public static class RemoteConfigKeys
{
    // ─── Event-Toggles ────────────────────────────────────────────────────
    public const string EventActiveHalloween = "event_active_halloween";
    public const string EventActiveChristmas = "event_active_christmas";
    public const string EventActiveNewYear   = "event_active_newyear";
    public const string EventActiveSummer    = "event_active_summer";

    // ─── Drop-Raten (0.0 - 1.0) ───────────────────────────────────────────
    public const string DropRateLegendaryCard = "drop_rate_legendary_card";  // Default 0.03
    public const string DropRateEpicCard      = "drop_rate_epic_card";       // Default 0.12
    public const string DropRateRareCard      = "drop_rate_rare_card";       // Default 0.25
    public const string DropRateGemFromBlock  = "drop_rate_gem_from_block";  // Default 0.005

    // ─── Preise / Discounts ───────────────────────────────────────────────
    public const string GemPackSmallPriceCents     = "gem_pack_small_price_cents";   // Default 199
    public const string GemPackMediumPriceCents    = "gem_pack_medium_price_cents";  // Default 499
    public const string GemPackLargePriceCents     = "gem_pack_large_price_cents";   // Default 999
    public const string StarterPackDiscountPct     = "starter_pack_discount_pct";    // Default 50
    public const string FirstPurchaseMultiplier    = "first_purchase_multiplier";    // Default 2.0

    // ─── Combat-Tuning ────────────────────────────────────────────────────
    public const string ComboSlowMotionThreshold     = "combo_slowmo_threshold";       // Default 4
    public const string ComboUltraThreshold          = "combo_ultra_threshold";        // Default 10
    public const string BossTelegraphDurationMs      = "boss_telegraph_duration_ms";   // Default 2000
    public const string BossEnrageHpPercent          = "boss_enrage_hp_percent";       // Default 50

    // ─── Live-Ops ─────────────────────────────────────────────────────────
    public const string MaintenanceMode              = "maintenance_mode";             // Default false
    public const string MinSupportedVersionCode      = "min_supported_version_code";   // Default 1
    public const string ForceUpdateUrl               = "force_update_url";             // Default ""
    public const string WeeklyContentDropEnabled     = "weekly_content_drop_enabled";  // Default false

    /// <summary>
    /// Alle definierten Keys — der Android-Override (FirebaseRemoteConfigService) iteriert
    /// darueber, um nach einem erfolgreichen Fetch nur die bekannten Keys zu uebernehmen.
    /// Bei neuen Keys hier ergaenzen.
    /// </summary>
    public static readonly string[] All =
    {
        EventActiveHalloween, EventActiveChristmas, EventActiveNewYear, EventActiveSummer,
        DropRateLegendaryCard, DropRateEpicCard, DropRateRareCard, DropRateGemFromBlock,
        GemPackSmallPriceCents, GemPackMediumPriceCents, GemPackLargePriceCents,
        StarterPackDiscountPct, FirstPurchaseMultiplier,
        ComboSlowMotionThreshold, ComboUltraThreshold, BossTelegraphDurationMs, BossEnrageHpPercent,
        MaintenanceMode, MinSupportedVersionCode, ForceUpdateUrl, WeeklyContentDropEnabled,
    };
}

/// <summary>
/// No-Op-Implementierung (Desktop + Test).
/// Liefert immer Default-Werte. <see cref="FetchAndActivateAsync"/> ist No-Op.
/// FirebaseRemoteConfigService kommt im Android-Override .
/// </summary>
public sealed class NullRemoteConfigService : IRemoteConfigService
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task<bool> FetchAndActivateAsync() => Task.FromResult(false);
    public bool GetBool(string key, bool defaultValue) => defaultValue;
    public long GetLong(string key, long defaultValue) => defaultValue;
    public double GetDouble(string key, double defaultValue) => defaultValue;
    public string GetString(string key, string defaultValue) => defaultValue;

    /// <summary>NullImpl feuert nie ConfigChanged — Stub bleibt unbenutzt.</summary>
#pragma warning disable CS0067  // Event nie gefeuert in NullImpl — by design
    public event Action? ConfigChanged;
#pragma warning restore CS0067
}
