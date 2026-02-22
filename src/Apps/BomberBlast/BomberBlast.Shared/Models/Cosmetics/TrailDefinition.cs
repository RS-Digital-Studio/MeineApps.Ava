using SkiaSharp;

namespace BomberBlast.Models.Cosmetics;

/// <summary>
/// Trail-Stil bestimmt die Partikel-Form hinter dem Spieler
/// </summary>
public enum TrailStyle : byte
{
    Dust,         // Staubwolken
    Smoke,        // Rauchschwaden
    Footsteps,    // Fußabdrücke
    Sparkle,      // Kleine Funken
    Flame,        // Flammen-Spur
    Frost,        // Eis-Kristalle
    Electric,     // Blitz-Funken
    Leaves,       // Fallende Blätter
    Bubbles,      // Aufsteigende Blasen
    Plasma,       // Plasma-Kugeln
    Rainbow,      // Regenbogen-Streifen
    Shadow,       // Schatten-Wisps
    Crystal,      // Kristall-Splitter
    Stardust,     // Sternenstaub
    Phoenix,      // Phönix-Federn + Feuer
    Void,         // Schwarze Partikel mit violettem Glow
    GoldenPath    // Goldener Pfad mit Shimmer
}

/// <summary>
/// Definiert einen Lauf-Trail (Partikel-Spur hinter dem Spieler)
/// </summary>
public class TrailDefinition
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

    /// <summary>Trail-Partikel-Stil</summary>
    public TrailStyle Style { get; init; }

    /// <summary>Primärfarbe der Trail-Partikel</summary>
    public SKColor PrimaryColor { get; init; }

    /// <summary>Sekundärfarbe (Glow/Akzent)</summary>
    public SKColor SecondaryColor { get; init; }
}

/// <summary>
/// Alle verfügbaren Trail-Definitionen (17 Trails)
/// </summary>
public static class TrailDefinitions
{
    // === Common (4) - 1.000-2.000 Coins ===

    public static readonly TrailDefinition Dust = new()
    {
        Id = "trail_dust", NameKey = "TrailDust", DescKey = "TrailDustDesc",
        Rarity = Rarity.Common, CoinPrice = 1000,
        Style = TrailStyle.Dust,
        PrimaryColor = new SKColor(160, 140, 120),
        SecondaryColor = new SKColor(120, 100, 80)
    };

    public static readonly TrailDefinition Smoke = new()
    {
        Id = "trail_smoke", NameKey = "TrailSmoke", DescKey = "TrailSmokeDesc",
        Rarity = Rarity.Common, CoinPrice = 1000,
        Style = TrailStyle.Smoke,
        PrimaryColor = new SKColor(140, 140, 150),
        SecondaryColor = new SKColor(100, 100, 110)
    };

    public static readonly TrailDefinition Footsteps = new()
    {
        Id = "trail_footsteps", NameKey = "TrailFootsteps", DescKey = "TrailFootstepsDesc",
        Rarity = Rarity.Common, CoinPrice = 1500,
        Style = TrailStyle.Footsteps,
        PrimaryColor = new SKColor(180, 160, 130),
        SecondaryColor = new SKColor(140, 120, 90)
    };

    public static readonly TrailDefinition Sparkle = new()
    {
        Id = "trail_sparkle", NameKey = "TrailSparkle", DescKey = "TrailSparkleDesc",
        Rarity = Rarity.Common, CoinPrice = 2000,
        Style = TrailStyle.Sparkle,
        PrimaryColor = new SKColor(255, 255, 200),
        SecondaryColor = new SKColor(255, 220, 100)
    };

    // === Rare (5) - 3.000-5.000 Coins ===

    public static readonly TrailDefinition Flame = new()
    {
        Id = "trail_flame", NameKey = "TrailFlame", DescKey = "TrailFlameDesc",
        Rarity = Rarity.Rare, CoinPrice = 3000,
        Style = TrailStyle.Flame,
        PrimaryColor = new SKColor(255, 120, 20),
        SecondaryColor = new SKColor(255, 60, 10)
    };

    public static readonly TrailDefinition Frost = new()
    {
        Id = "trail_frost", NameKey = "TrailFrost", DescKey = "TrailFrostDesc",
        Rarity = Rarity.Rare, CoinPrice = 3000,
        Style = TrailStyle.Frost,
        PrimaryColor = new SKColor(160, 220, 255),
        SecondaryColor = new SKColor(100, 180, 255)
    };

    public static readonly TrailDefinition Electric = new()
    {
        Id = "trail_electric", NameKey = "TrailElectric", DescKey = "TrailElectricDesc",
        Rarity = Rarity.Rare, CoinPrice = 4000,
        Style = TrailStyle.Electric,
        PrimaryColor = new SKColor(100, 200, 255),
        SecondaryColor = new SKColor(200, 240, 255)
    };

    public static readonly TrailDefinition Leaves = new()
    {
        Id = "trail_leaves", NameKey = "TrailLeaves", DescKey = "TrailLeavesDesc",
        Rarity = Rarity.Rare, CoinPrice = 3500,
        Style = TrailStyle.Leaves,
        PrimaryColor = new SKColor(80, 180, 50),
        SecondaryColor = new SKColor(180, 140, 40)
    };

    public static readonly TrailDefinition Bubbles = new()
    {
        Id = "trail_bubbles", NameKey = "TrailBubbles", DescKey = "TrailBubblesDesc",
        Rarity = Rarity.Rare, CoinPrice = 5000,
        Style = TrailStyle.Bubbles,
        PrimaryColor = new SKColor(80, 180, 255),
        SecondaryColor = new SKColor(200, 240, 255)
    };

    // === Epic (5) - 8.000-15.000 Coins ===

    public static readonly TrailDefinition Plasma = new()
    {
        Id = "trail_plasma", NameKey = "TrailPlasma", DescKey = "TrailPlasmaDesc",
        Rarity = Rarity.Epic, CoinPrice = 8000,
        Style = TrailStyle.Plasma,
        PrimaryColor = new SKColor(200, 50, 255),
        SecondaryColor = new SKColor(100, 200, 255)
    };

    public static readonly TrailDefinition Rainbow = new()
    {
        Id = "trail_rainbow", NameKey = "TrailRainbow", DescKey = "TrailRainbowDesc",
        Rarity = Rarity.Epic, CoinPrice = 10000,
        Style = TrailStyle.Rainbow,
        PrimaryColor = new SKColor(255, 100, 50),
        SecondaryColor = new SKColor(50, 100, 255)
    };

    public static readonly TrailDefinition ShadowTrail = new()
    {
        Id = "trail_shadow", NameKey = "TrailShadow", DescKey = "TrailShadowDesc",
        Rarity = Rarity.Epic, CoinPrice = 10000,
        Style = TrailStyle.Shadow,
        PrimaryColor = new SKColor(40, 20, 60),
        SecondaryColor = new SKColor(120, 60, 200)
    };

    public static readonly TrailDefinition Crystal = new()
    {
        Id = "trail_crystal", NameKey = "TrailCrystal", DescKey = "TrailCrystalDesc",
        Rarity = Rarity.Epic, CoinPrice = 12000,
        Style = TrailStyle.Crystal,
        PrimaryColor = new SKColor(180, 220, 255),
        SecondaryColor = new SKColor(255, 255, 255)
    };

    public static readonly TrailDefinition Stardust = new()
    {
        Id = "trail_stardust", NameKey = "TrailStardust", DescKey = "TrailStardustDesc",
        Rarity = Rarity.Epic, CoinPrice = 15000,
        Style = TrailStyle.Stardust,
        PrimaryColor = new SKColor(255, 220, 100),
        SecondaryColor = new SKColor(255, 255, 200)
    };

    // === Legendary (3) - 25.000 Coins oder 200 Gems ===

    public static readonly TrailDefinition Phoenix = new()
    {
        Id = "trail_phoenix", NameKey = "TrailPhoenix", DescKey = "TrailPhoenixDesc",
        Rarity = Rarity.Legendary, CoinPrice = 25000, GemPrice = 200,
        Style = TrailStyle.Phoenix,
        PrimaryColor = new SKColor(255, 140, 0),
        SecondaryColor = new SKColor(255, 60, 20)
    };

    public static readonly TrailDefinition VoidTrail = new()
    {
        Id = "trail_void", NameKey = "TrailVoid", DescKey = "TrailVoidDesc",
        Rarity = Rarity.Legendary, GemPrice = 200,
        Style = TrailStyle.Void,
        PrimaryColor = new SKColor(20, 10, 30),
        SecondaryColor = new SKColor(150, 50, 255)
    };

    public static readonly TrailDefinition GoldenPath = new()
    {
        Id = "trail_golden", NameKey = "TrailGoldenPath", DescKey = "TrailGoldenPathDesc",
        Rarity = Rarity.Legendary, GemPrice = 200,
        Style = TrailStyle.GoldenPath,
        PrimaryColor = new SKColor(255, 215, 0),
        SecondaryColor = new SKColor(255, 180, 50)
    };

    public static readonly TrailDefinition[] All =
    [
        Dust, Smoke, Footsteps, Sparkle,
        Flame, Frost, Electric, Leaves, Bubbles,
        Plasma, Rainbow, ShadowTrail, Crystal, Stardust,
        Phoenix, VoidTrail, GoldenPath
    ];
}
