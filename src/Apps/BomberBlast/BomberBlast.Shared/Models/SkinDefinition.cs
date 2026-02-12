using SkiaSharp;

namespace BomberBlast.Models;

/// <summary>
/// Definiert einen Spieler- oder Gegner-Skin
/// </summary>
public class SkinDefinition
{
    /// <summary>Eindeutige ID</summary>
    public string Id { get; init; } = "";

    /// <summary>RESX-Key für den Anzeigenamen</summary>
    public string NameKey { get; init; } = "";

    /// <summary>Ob nur für Premium-Nutzer</summary>
    public bool IsPremiumOnly { get; init; }

    /// <summary>Primäre Farbe des Skins</summary>
    public SKColor PrimaryColor { get; init; }

    /// <summary>Sekundäre Farbe (Akzent/Detail)</summary>
    public SKColor SecondaryColor { get; init; }

    /// <summary>Glow/Effekt-Farbe (null = kein Glow)</summary>
    public SKColor? GlowColor { get; init; }
}

/// <summary>
/// Vordefinierte Spieler-Skins
/// </summary>
public static class PlayerSkins
{
    public static readonly SkinDefinition Default = new()
    {
        Id = "default",
        NameKey = "SkinDefault",
        IsPremiumOnly = false,
        PrimaryColor = new SKColor(255, 255, 255), // Weiß
        SecondaryColor = new SKColor(50, 100, 255)  // Blau
    };

    public static readonly SkinDefinition Gold = new()
    {
        Id = "gold",
        NameKey = "SkinGold",
        IsPremiumOnly = true,
        PrimaryColor = new SKColor(255, 215, 0),    // Gold
        SecondaryColor = new SKColor(218, 165, 32),  // Dunkelgold
        GlowColor = new SKColor(255, 215, 0, 80)
    };

    public static readonly SkinDefinition Neon = new()
    {
        Id = "neon",
        NameKey = "SkinNeon",
        IsPremiumOnly = true,
        PrimaryColor = new SKColor(0, 255, 200),     // Neon-Cyan
        SecondaryColor = new SKColor(255, 0, 200),    // Neon-Pink
        GlowColor = new SKColor(0, 255, 200, 60)
    };

    public static readonly SkinDefinition Cyber = new()
    {
        Id = "cyber",
        NameKey = "SkinCyber",
        IsPremiumOnly = true,
        PrimaryColor = new SKColor(100, 200, 255),    // Hellblau
        SecondaryColor = new SKColor(0, 80, 180),     // Dunkelblau
        GlowColor = new SKColor(100, 200, 255, 60)
    };

    public static readonly SkinDefinition Retro = new()
    {
        Id = "retro",
        NameKey = "SkinRetro",
        IsPremiumOnly = true,
        PrimaryColor = new SKColor(255, 100, 50),     // Orange-Rot
        SecondaryColor = new SKColor(200, 50, 0),      // Dunkelrot
        GlowColor = new SKColor(255, 100, 50, 60)
    };

    public static readonly SkinDefinition[] All = [Default, Gold, Neon, Cyber, Retro];
}

/// <summary>
/// Vordefinierte Gegner-Skin-Sets
/// </summary>
public static class EnemySkinSets
{
    public static readonly string Default = "default";
    public static readonly string Frost = "frost";
    public static readonly string Shadow = "shadow";
    public static readonly string[] All = [Default, Frost, Shadow];
}
