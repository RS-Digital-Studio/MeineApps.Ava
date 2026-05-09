namespace HandwerkerImperium.Models;

/// <summary>
/// AAA-Audit P1: Cross-Promotion zwischen den 11 eigenen Apps. Daten-Modell fuer
/// eine kontextuelle Promo-Karte (Icon, Name, Beschreibung, Play-Store-Link).
///
/// Wirtschaftliche Begruendung: Voodoo / Lion Studios machen 30% ihrer Installs
/// ueber Cross-Promo. Bei 11 Apps in einem Portfolio mit insgesamt ~1000 DAU ist
/// das Pflicht — kostet ~0 EUR und liefert Free-Installs.
/// </summary>
public sealed class CrossPromoApp
{
    /// <summary>Stabile App-ID (z.B. „bomberblast", „worktimepro") — fuer Tracking-Events.</summary>
    public string Id { get; init; } = "";

    /// <summary>Lokalisierter Name (RESX-Key z.B. „CrossPromo_BomberBlast_Name").</summary>
    public string NameKey { get; init; } = "";

    /// <summary>Lokalisierte Hook-Zeile („Bombenrausch — Action pur fuer dich!").</summary>
    public string HookKey { get; init; } = "";

    /// <summary>GameIcon-Kind-Name (Material-Icon-Style).</summary>
    public string IconKind { get; init; } = "Apps";

    /// <summary>Akzent-Farbe (Hex). Sollte zur Ziel-App passen.</summary>
    public string AccentColor { get; init; } = "#7C7FF7";

    /// <summary>Play-Store-Package-ID (z.B. „com.meineapps.bomberblast").</summary>
    public string PackageId { get; init; } = "";

    /// <summary>Liefert die Play-Store-Deep-Link-URL.</summary>
    public string GetPlayStoreUrl() =>
        $"https://play.google.com/store/apps/details?id={PackageId}";
}
