using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Zentrale Farbkonstanten für das VitalOS Design-System.
/// Alle Renderer referenzieren diese Klasse statt eigene Farben zu definieren.
/// </summary>
public static class MedicalColors
{
    // =====================================================================
    // Primär-Palette
    // =====================================================================

    /// <summary>Haupt-Akzentfarbe (Cyan) - EKG-Linien, aktive Elemente, Highlights</summary>
    public static readonly SKColor Cyan = SKColor.Parse("#06B6D4");

    /// <summary>Helles Cyan - Glow-Effekte, Hover-Zustände</summary>
    public static readonly SKColor CyanBright = SKColor.Parse("#22D3EE");

    /// <summary>Teal - Sekundäre Akzente, Wasser-Elemente</summary>
    public static readonly SKColor Teal = SKColor.Parse("#14B8A6");

    /// <summary>Elektrisches Blau - Links, interaktive Elemente</summary>
    public static readonly SKColor ElectricBlue = SKColor.Parse("#3B82F6");

    // =====================================================================
    // Hintergrund-Farben
    // =====================================================================

    /// <summary>Tiefer Teal-Hintergrund - Hauptflächen</summary>
    public static readonly SKColor BgDeep = SKColor.Parse("#142832");

    /// <summary>Dunkler Teal - Unterer Gradient</summary>
    public static readonly SKColor BgDark = SKColor.Parse("#0A1824");

    /// <summary>Dunkelster Teal - Äußerste Hintergrundebene</summary>
    public static readonly SKColor BgDarkest = SKColor.Parse("#061018");

    /// <summary>Oberflächen-Farbe - Karten, Panels</summary>
    public static readonly SKColor Surface = SKColor.Parse("#1E3844");

    /// <summary>Helle Oberfläche - Hover, erhöhte Elemente</summary>
    public static readonly SKColor SurfaceLight = SKColor.Parse("#2A4A58");

    /// <summary>Tab-Leiste Hintergrund</summary>
    public static readonly SKColor TabBarBg = SKColor.Parse("#142832");

    // =====================================================================
    // Grid-Farbe
    // =====================================================================

    /// <summary>Raster-Linien - EKG-Grid, Chart-Hintergrund</summary>
    public static readonly SKColor Grid = SKColor.Parse("#0E7490");

    // =====================================================================
    // Feature-Farben (kategoriespezifisch)
    // =====================================================================

    /// <summary>Gewicht/Idealgewicht - Primär (Lila)</summary>
    public static readonly SKColor WeightPurple = SKColor.Parse("#8B5CF6");

    /// <summary>Gewicht/Idealgewicht - Dunklere Variante</summary>
    public static readonly SKColor WeightPurpleDark = SKColor.Parse("#7C3AED");

    /// <summary>BMI - Primär (Blau)</summary>
    public static readonly SKColor BmiBlue = SKColor.Parse("#3B82F6");

    /// <summary>BMI - Helle Variante</summary>
    public static readonly SKColor BmiBlueLight = SKColor.Parse("#60A5FA");

    /// <summary>Wasser - Primär (Grün)</summary>
    public static readonly SKColor WaterGreen = SKColor.Parse("#22C55E");

    /// <summary>Wasser - Dunklere Variante</summary>
    public static readonly SKColor WaterGreenDark = SKColor.Parse("#16A34A");

    /// <summary>Kalorien - Primär (Bernstein/Orange)</summary>
    public static readonly SKColor CalorieAmber = SKColor.Parse("#F59E0B");

    /// <summary>Kalorien - Dunklere Variante</summary>
    public static readonly SKColor CalorieAmberDark = SKColor.Parse("#D97706");

    /// <summary>Kritischer Zustand / Warnung (Rot)</summary>
    public static readonly SKColor CriticalRed = SKColor.Parse("#EF4444");

    // =====================================================================
    // Text-Farben
    // =====================================================================

    /// <summary>Primärer Text - Überschriften, wichtige Werte</summary>
    public static readonly SKColor TextPrimary = SKColor.Parse("#E2E8F0");

    /// <summary>Gedämpfter Text - Labels, Beschreibungen</summary>
    public static readonly SKColor TextMuted = SKColor.Parse("#64748B");

    /// <summary>Abgedunkelter Text - Platzhalter, deaktivierte Elemente</summary>
    public static readonly SKColor TextDimmed = SKColor.Parse("#475569");

    // =====================================================================
    // EKG-Wellenform (24 normalisierte Y-Offsets von der Baseline)
    // Identisch zum FitnessRechnerSplashRenderer
    // =====================================================================

    /// <summary>
    /// EKG-Wellenform mit 24 Datenpunkten.
    /// Aufbau: P-Welle (6) + QRS-Komplex (6) + T-Welle (8) + Baseline (4)
    /// </summary>
    public static readonly float[] EkgWave =
    {
        0f, 0f, 0.05f, 0.08f, 0.05f, 0f,              // P-Welle
        0f, -0.08f, 0.45f, -0.15f, 0f, 0f,             // QRS-Komplex
        0f, 0f, 0.03f, 0.06f, 0.08f, 0.06f, 0.03f, 0f, // T-Welle
        0f, 0f, 0f, 0f                                   // Baseline
    };

    // =====================================================================
    // Herzschlag-Timing (72 BPM)
    // =====================================================================

    /// <summary>Herzfrequenz: 1.2 Schläge pro Sekunde (= 72 BPM)</summary>
    public const float BeatsPerSecond = 1.2f;

    /// <summary>Dauer eines Herzschlags in Sekunden (~0.833s)</summary>
    public const float BeatPeriod = 1f / BeatsPerSecond;
}
