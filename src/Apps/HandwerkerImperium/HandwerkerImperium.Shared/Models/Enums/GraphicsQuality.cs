namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Grafik-Qualitätsstufen für die Spiel-Renderer.
/// Low: Keine Partikel, kein Wetter, keine Schatten → minimaler RAM-Verbrauch.
/// Medium: Reduzierte Partikel, einfaches Wetter → ausgewogene Performance.
/// High: Alle Effekte aktiv → volle visuelle Qualität.
/// </summary>
public enum GraphicsQuality
{
    /// <summary>Niedrig: Keine Partikel, kein Wetter, keine Schatten.</summary>
    Low = 0,

    /// <summary>Mittel: Reduzierte Partikel, einfaches Wetter.</summary>
    Medium = 1,

    /// <summary>Hoch: Alle Effekte aktiv (Standard).</summary>
    High = 2
}
