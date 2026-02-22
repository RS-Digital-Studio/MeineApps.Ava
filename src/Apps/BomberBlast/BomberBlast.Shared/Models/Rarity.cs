using SkiaSharp;

namespace BomberBlast.Models;

/// <summary>
/// Raritätsstufen für Bomben-Karten, Kosmetik-Items und Shop-Items
/// </summary>
public enum Rarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// Extension-Methoden für Raritäts-Rendering und Darstellung
/// </summary>
public static class RarityExtensions
{
    // Farb-Definitionen pro Raritätsstufe
    private static readonly SKColor CommonColor = new(0xFF, 0xFF, 0xFF);       // Weiß
    private static readonly SKColor RareColor = new(0x21, 0x96, 0xF3);         // Blau
    private static readonly SKColor EpicColor = new(0x9C, 0x27, 0xB0);         // Violett
    private static readonly SKColor LegendaryColor = new(0xFF, 0xD7, 0x00);    // Gold

    // Sekundärfarben für Glow-Effekte
    private static readonly SKColor CommonGlow = new(0xB0, 0xB0, 0xB0);       // Hellgrau
    private static readonly SKColor RareGlow = new(0x64, 0xB5, 0xF6);         // Hellblau
    private static readonly SKColor EpicGlow = new(0xCE, 0x93, 0xD8);         // Hellviolett
    private static readonly SKColor LegendaryGlow = new(0xFF, 0xE0, 0x82);    // Hellgold

    /// <summary>Hauptfarbe der Rarität</summary>
    public static SKColor GetColor(this Rarity rarity) => rarity switch
    {
        Rarity.Common => CommonColor,
        Rarity.Rare => RareColor,
        Rarity.Epic => EpicColor,
        Rarity.Legendary => LegendaryColor,
        _ => CommonColor
    };

    /// <summary>Glow-Farbe (heller, für Leuchteffekte)</summary>
    public static SKColor GetGlowColor(this Rarity rarity) => rarity switch
    {
        Rarity.Common => CommonGlow,
        Rarity.Rare => RareGlow,
        Rarity.Epic => EpicGlow,
        Rarity.Legendary => LegendaryGlow,
        _ => CommonGlow
    };

    /// <summary>Glow-Radius für Rahmen-Effekte (in Pixel)</summary>
    public static float GetGlowRadius(this Rarity rarity) => rarity switch
    {
        Rarity.Common => 0f,
        Rarity.Rare => 3f,
        Rarity.Epic => 5f,
        Rarity.Legendary => 8f,
        _ => 0f
    };

    /// <summary>Rahmen-Breite (in Pixel)</summary>
    public static float GetBorderWidth(this Rarity rarity) => rarity switch
    {
        Rarity.Common => 1.5f,
        Rarity.Rare => 2f,
        Rarity.Epic => 2.5f,
        Rarity.Legendary => 3f,
        _ => 1.5f
    };

    /// <summary>Lokalisierter RESX-Key für den Raritätsnamen</summary>
    public static string GetNameKey(this Rarity rarity) => rarity switch
    {
        Rarity.Common => "RarityCommon",
        Rarity.Rare => "RarityRare",
        Rarity.Epic => "RarityEpic",
        Rarity.Legendary => "RarityLegendary",
        _ => "RarityCommon"
    };
}
