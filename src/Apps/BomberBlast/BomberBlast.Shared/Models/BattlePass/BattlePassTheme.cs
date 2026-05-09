namespace BomberBlast.Models.BattlePass;

/// <summary>
/// Saison-Theme fuer einen Battle-Pass (Phase 19 — AAA-Audit L1).
/// Vorbild: Brawl Stars Brawl-Pass-Themen (Halloween, Robotik, Pirates, Mech etc.).
/// Jede Saison erzaehlt eine eigene Lore + bringt thematische Cosmetics.
/// </summary>
public enum BattlePassTheme
{
    /// <summary>Klassisch (Default-Theme, wenn keines explizit gesetzt).</summary>
    Classic = 0,

    /// <summary>Cyberpunk / Neon — Welt 8/9 angelehnt.</summary>
    Cyberpunk = 1,

    /// <summary>Halloween-Saison (Oktober).</summary>
    Halloween = 2,

    /// <summary>Winter / Christmas (Dezember).</summary>
    Winter = 3,

    /// <summary>Sommer-Beach-Theme (Juli/August).</summary>
    Summer = 4,

    /// <summary>Mech-Robotik-Theme.</summary>
    Mech = 5,

    /// <summary>Underwater / Atlantis-Theme.</summary>
    Underwater = 6,

    /// <summary>Sengoku / Japan-Samurai.</summary>
    Sengoku = 7,

    /// <summary>Dia de los Muertos.</summary>
    DiaDeLosMuertos = 8,

    /// <summary>Steampunk / Viktorianisch.</summary>
    Steampunk = 9,
}

/// <summary>
/// Saison-Theme-Metadaten (Phase 19).
/// Liefert Akzent-Farbe, Lore-Hook und Skin-Drop-Hint pro Saison-Theme.
/// </summary>
public static class BattlePassThemeExtensions
{
    /// <summary>RESX-Key fuer den lokalisierten Saison-Namen (z.B. "BattlePassTheme_Cyberpunk").</summary>
    public static string GetNameKey(this BattlePassTheme theme) => $"BattlePassTheme_{theme}";

    /// <summary>RESX-Key fuer die Lore-Beschreibung der Saison.</summary>
    public static string GetLoreKey(this BattlePassTheme theme) => $"BattlePassTheme_{theme}_Lore";

    /// <summary>Akzent-Farbe (Hex) fuer UI (Header-Border, Card-Glow, Saison-Banner).</summary>
    public static string GetAccentColorHex(this BattlePassTheme theme) => theme switch
    {
        BattlePassTheme.Classic => "#D97706",         // Amber (Bestand)
        BattlePassTheme.Cyberpunk => "#FF00FF",       // Magenta-Pink Neon
        BattlePassTheme.Halloween => "#FF6B00",       // Halloween-Orange
        BattlePassTheme.Winter => "#3B82F6",          // Eis-Blau
        BattlePassTheme.Summer => "#06B6D4",          // Türkis
        BattlePassTheme.Mech => "#71717A",            // Stahl-Grau
        BattlePassTheme.Underwater => "#0EA5E9",      // Tiefsee-Blau
        BattlePassTheme.Sengoku => "#DC2626",         // Samurai-Rot
        BattlePassTheme.DiaDeLosMuertos => "#A855F7", // Marigold-Lila
        BattlePassTheme.Steampunk => "#92400E",       // Bronze
        _ => "#D97706",
    };

    /// <summary>
    /// Sekundär-Akzent (für Gradient + Glow-Layer). Komplementär zur Haupt-Akzent-Farbe.
    /// </summary>
    public static string GetSecondaryColorHex(this BattlePassTheme theme) => theme switch
    {
        BattlePassTheme.Classic => "#FBBF24",
        BattlePassTheme.Cyberpunk => "#22D3EE",
        BattlePassTheme.Halloween => "#7C3AED",
        BattlePassTheme.Winter => "#FFFFFF",
        BattlePassTheme.Summer => "#FBBF24",
        BattlePassTheme.Mech => "#06B6D4",
        BattlePassTheme.Underwater => "#10B981",
        BattlePassTheme.Sengoku => "#FCD34D",
        BattlePassTheme.DiaDeLosMuertos => "#F97316",
        BattlePassTheme.Steampunk => "#FCD34D",
        _ => "#FBBF24",
    };

    /// <summary>
    /// Material-Icon-Hint für Saison-Banner (das BomberBlast Icon-System ist eigenes,
    /// dieses Hint kann auf passende GameIconKind gemapped werden).
    /// </summary>
    public static string GetIconHint(this BattlePassTheme theme) => theme switch
    {
        BattlePassTheme.Cyberpunk => "Lightning",
        BattlePassTheme.Halloween => "Skull",
        BattlePassTheme.Winter => "Snowflake",
        BattlePassTheme.Summer => "Sun",
        BattlePassTheme.Mech => "Robot",
        BattlePassTheme.Underwater => "Water",
        BattlePassTheme.Sengoku => "Sword",
        BattlePassTheme.DiaDeLosMuertos => "SkullOutline",
        BattlePassTheme.Steampunk => "Cog",
        _ => "Star",
    };

    /// <summary>
    /// Berechnet das Saison-Theme deterministisch aus der Saison-Nummer.
    /// Saison 1=Classic, dann Rotation durch alle Themes ausser Classic.
    /// </summary>
    public static BattlePassTheme GetThemeForSeason(int seasonNumber)
    {
        if (seasonNumber <= 1) return BattlePassTheme.Classic;
        // Saison 2..10 rotiert durch die thematischen Saisons
        var themes = new[]
        {
            BattlePassTheme.Cyberpunk,
            BattlePassTheme.Halloween,
            BattlePassTheme.Winter,
            BattlePassTheme.Summer,
            BattlePassTheme.Mech,
            BattlePassTheme.Underwater,
            BattlePassTheme.Sengoku,
            BattlePassTheme.DiaDeLosMuertos,
            BattlePassTheme.Steampunk,
        };
        var idx = (seasonNumber - 2) % themes.Length;
        return themes[idx];
    }
}
