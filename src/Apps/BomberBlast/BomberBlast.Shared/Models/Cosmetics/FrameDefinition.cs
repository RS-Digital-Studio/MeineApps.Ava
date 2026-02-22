using SkiaSharp;

namespace BomberBlast.Models.Cosmetics;

/// <summary>
/// Profilrahmen-Stil für die Anzeige im Liga-Leaderboard, GameOver-Screen und MainMenu
/// </summary>
public enum FrameStyle : byte
{
    Simple,       // Einfacher dünner Rahmen
    Rounded,      // Abgerundeter Rahmen
    Square,       // Eckiger Rahmen mit Kanten
    Dotted,       // Gepunkteter Rahmen
    Thin,         // Sehr dünner eleganter Rahmen
    FireFrame,    // Flammen-Rahmen
    IceFrame,     // Eis-Kristall-Rahmen
    ElectricFrame,// Blitz-Rahmen
    NatureFrame,  // Natur-Rahmen (Ranken)
    WaterFrame,   // Wasser-Rahmen (Wellen)
    ShadowFrame,  // Schatten-Rahmen (Wisps)
    CrystalFrame, // Kristall-Rahmen (Facetten)
    PlasmaFrame,  // Plasma-Rahmen (Energie)
    StellarFrame, // Stern-Rahmen (Sternenmuster)
    ArcaneFrame,  // Arkaner Rahmen (Runen)
    DragonFrame,  // Drachen-Rahmen (Schuppen + Hörner)
    PhoenixFrame, // Phönix-Rahmen (Flammen-Federn)
    CrownFrame    // Kronen-Rahmen (Gold + Edelsteine)
}

/// <summary>
/// Definiert einen Profilrahmen (Anzeige im Liga-Leaderboard, GameOver, MainMenu)
/// </summary>
public class FrameDefinition
{
    /// <summary>Eindeutige ID</summary>
    public string Id { get; init; } = "";

    /// <summary>RESX-Key für den Anzeigenamen</summary>
    public string NameKey { get; init; } = "";

    /// <summary>RESX-Key für die Beschreibung</summary>
    public string DescKey { get; init; } = "";

    /// <summary>Raritätsstufe</summary>
    public Rarity Rarity { get; init; }

    /// <summary>Coin-Preis (0 = nur über Gems/BP/Liga)</summary>
    public int CoinPrice { get; init; }

    /// <summary>Gem-Preis (0 = nur über Coins/BP/Liga)</summary>
    public int GemPrice { get; init; }

    /// <summary>Rahmen-Stil</summary>
    public FrameStyle Style { get; init; }

    /// <summary>Primärfarbe des Rahmens</summary>
    public SKColor PrimaryColor { get; init; }

    /// <summary>Sekundärfarbe (Akzent/Glow)</summary>
    public SKColor SecondaryColor { get; init; }
}

/// <summary>
/// Alle verfügbaren Profilrahmen (18 Rahmen)
/// </summary>
public static class FrameDefinitions
{
    // === Common (5) - 1.000-2.000 Coins ===

    public static readonly FrameDefinition Simple = new()
    {
        Id = "frame_simple", NameKey = "FrameSimple", DescKey = "FrameSimpleDesc",
        Rarity = Rarity.Common, CoinPrice = 1000,
        Style = FrameStyle.Simple,
        PrimaryColor = new SKColor(200, 200, 200),
        SecondaryColor = new SKColor(150, 150, 150)
    };

    public static readonly FrameDefinition Rounded = new()
    {
        Id = "frame_rounded", NameKey = "FrameRounded", DescKey = "FrameRoundedDesc",
        Rarity = Rarity.Common, CoinPrice = 1000,
        Style = FrameStyle.Rounded,
        PrimaryColor = new SKColor(180, 200, 220),
        SecondaryColor = new SKColor(140, 160, 180)
    };

    public static readonly FrameDefinition SquareFrame = new()
    {
        Id = "frame_square", NameKey = "FrameSquare", DescKey = "FrameSquareDesc",
        Rarity = Rarity.Common, CoinPrice = 1500,
        Style = FrameStyle.Square,
        PrimaryColor = new SKColor(180, 180, 190),
        SecondaryColor = new SKColor(130, 130, 140)
    };

    public static readonly FrameDefinition Dotted = new()
    {
        Id = "frame_dotted", NameKey = "FrameDotted", DescKey = "FrameDottedDesc",
        Rarity = Rarity.Common, CoinPrice = 1500,
        Style = FrameStyle.Dotted,
        PrimaryColor = new SKColor(200, 200, 210),
        SecondaryColor = new SKColor(160, 160, 170)
    };

    public static readonly FrameDefinition Thin = new()
    {
        Id = "frame_thin", NameKey = "FrameThin", DescKey = "FrameThinDesc",
        Rarity = Rarity.Common, CoinPrice = 2000,
        Style = FrameStyle.Thin,
        PrimaryColor = new SKColor(220, 220, 230),
        SecondaryColor = new SKColor(180, 180, 190)
    };

    // === Rare (5) - 3.000-5.000 Coins ===

    public static readonly FrameDefinition Fire = new()
    {
        Id = "frame_fire", NameKey = "FrameFire", DescKey = "FrameFireDesc",
        Rarity = Rarity.Rare, CoinPrice = 3000,
        Style = FrameStyle.FireFrame,
        PrimaryColor = new SKColor(255, 100, 20),
        SecondaryColor = new SKColor(255, 200, 50)
    };

    public static readonly FrameDefinition Ice = new()
    {
        Id = "frame_ice", NameKey = "FrameIce", DescKey = "FrameIceDesc",
        Rarity = Rarity.Rare, CoinPrice = 3000,
        Style = FrameStyle.IceFrame,
        PrimaryColor = new SKColor(120, 200, 255),
        SecondaryColor = new SKColor(200, 240, 255)
    };

    public static readonly FrameDefinition ElectricFrame = new()
    {
        Id = "frame_electric", NameKey = "FrameElectric", DescKey = "FrameElectricDesc",
        Rarity = Rarity.Rare, CoinPrice = 4000,
        Style = FrameStyle.ElectricFrame,
        PrimaryColor = new SKColor(100, 180, 255),
        SecondaryColor = new SKColor(200, 230, 255)
    };

    public static readonly FrameDefinition Nature = new()
    {
        Id = "frame_nature", NameKey = "FrameNature", DescKey = "FrameNatureDesc",
        Rarity = Rarity.Rare, CoinPrice = 3500,
        Style = FrameStyle.NatureFrame,
        PrimaryColor = new SKColor(60, 160, 40),
        SecondaryColor = new SKColor(120, 200, 80)
    };

    public static readonly FrameDefinition Water = new()
    {
        Id = "frame_water", NameKey = "FrameWater", DescKey = "FrameWaterDesc",
        Rarity = Rarity.Rare, CoinPrice = 5000,
        Style = FrameStyle.WaterFrame,
        PrimaryColor = new SKColor(30, 100, 200),
        SecondaryColor = new SKColor(80, 180, 255)
    };

    // === Epic (5) - 8.000-15.000 Coins ===

    public static readonly FrameDefinition ShadowFrame = new()
    {
        Id = "frame_shadow", NameKey = "FrameShadow", DescKey = "FrameShadowDesc",
        Rarity = Rarity.Epic, CoinPrice = 8000,
        Style = FrameStyle.ShadowFrame,
        PrimaryColor = new SKColor(60, 30, 80),
        SecondaryColor = new SKColor(140, 80, 220)
    };

    public static readonly FrameDefinition CrystalFrame = new()
    {
        Id = "frame_crystal", NameKey = "FrameCrystal", DescKey = "FrameCrystalDesc",
        Rarity = Rarity.Epic, CoinPrice = 10000,
        Style = FrameStyle.CrystalFrame,
        PrimaryColor = new SKColor(180, 220, 255),
        SecondaryColor = new SKColor(240, 250, 255)
    };

    public static readonly FrameDefinition PlasmaFrame = new()
    {
        Id = "frame_plasma", NameKey = "FramePlasma", DescKey = "FramePlasmaDesc",
        Rarity = Rarity.Epic, CoinPrice = 10000,
        Style = FrameStyle.PlasmaFrame,
        PrimaryColor = new SKColor(200, 50, 255),
        SecondaryColor = new SKColor(100, 200, 255)
    };

    public static readonly FrameDefinition Stellar = new()
    {
        Id = "frame_stellar", NameKey = "FrameStellar", DescKey = "FrameStellarDesc",
        Rarity = Rarity.Epic, CoinPrice = 12000,
        Style = FrameStyle.StellarFrame,
        PrimaryColor = new SKColor(255, 220, 100),
        SecondaryColor = new SKColor(255, 255, 200)
    };

    public static readonly FrameDefinition Arcane = new()
    {
        Id = "frame_arcane", NameKey = "FrameArcane", DescKey = "FrameArcaneDesc",
        Rarity = Rarity.Epic, CoinPrice = 15000,
        Style = FrameStyle.ArcaneFrame,
        PrimaryColor = new SKColor(100, 60, 200),
        SecondaryColor = new SKColor(200, 160, 255)
    };

    // === Legendary (3) - 25.000 Coins oder 200 Gems ===

    public static readonly FrameDefinition Dragon = new()
    {
        Id = "frame_dragon", NameKey = "FrameDragon", DescKey = "FrameDragonDesc",
        Rarity = Rarity.Legendary, CoinPrice = 25000, GemPrice = 200,
        Style = FrameStyle.DragonFrame,
        PrimaryColor = new SKColor(180, 40, 20),
        SecondaryColor = new SKColor(255, 120, 40)
    };

    public static readonly FrameDefinition PhoenixFrame = new()
    {
        Id = "frame_phoenix", NameKey = "FramePhoenix", DescKey = "FramePhoenixDesc",
        Rarity = Rarity.Legendary, GemPrice = 200,
        Style = FrameStyle.PhoenixFrame,
        PrimaryColor = new SKColor(255, 140, 0),
        SecondaryColor = new SKColor(255, 80, 20)
    };

    public static readonly FrameDefinition Crown = new()
    {
        Id = "frame_crown", NameKey = "FrameCrown", DescKey = "FrameCrownDesc",
        Rarity = Rarity.Legendary, GemPrice = 200,
        Style = FrameStyle.CrownFrame,
        PrimaryColor = new SKColor(255, 215, 0),
        SecondaryColor = new SKColor(255, 180, 50)
    };

    public static readonly FrameDefinition[] All =
    [
        Simple, Rounded, SquareFrame, Dotted, Thin,
        Fire, Ice, ElectricFrame, Nature, Water,
        ShadowFrame, CrystalFrame, PlasmaFrame, Stellar, Arcane,
        Dragon, PhoenixFrame, Crown
    ];
}
