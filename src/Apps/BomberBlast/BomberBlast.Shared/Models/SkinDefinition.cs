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

    /// <summary>Coin-Preis (0 = kostenlos/Standard oder Premium-Only)</summary>
    public int CoinPrice { get; init; }

    /// <summary>Raritätsstufe</summary>
    public Rarity Rarity { get; init; }

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
        Rarity = Rarity.Common,
        PrimaryColor = new SKColor(255, 255, 255),
        SecondaryColor = new SKColor(50, 100, 255)
    };

    public static readonly SkinDefinition Crimson = new()
    {
        Id = "crimson",
        NameKey = "SkinCrimson",
        Rarity = Rarity.Common,
        CoinPrice = 1500,
        PrimaryColor = new SKColor(220, 30, 30),
        SecondaryColor = new SKColor(140, 10, 10)
    };

    public static readonly SkinDefinition Arctic = new()
    {
        Id = "arctic",
        NameKey = "SkinArctic",
        Rarity = Rarity.Common,
        CoinPrice = 1500,
        PrimaryColor = new SKColor(230, 245, 255),
        SecondaryColor = new SKColor(140, 210, 255)
    };

    public static readonly SkinDefinition Stealth = new()
    {
        Id = "stealth",
        NameKey = "SkinStealth",
        Rarity = Rarity.Common,
        CoinPrice = 2000,
        PrimaryColor = new SKColor(60, 60, 70),
        SecondaryColor = new SKColor(20, 20, 25)
    };

    public static readonly SkinDefinition Toxic = new()
    {
        Id = "toxic",
        NameKey = "SkinToxic",
        Rarity = Rarity.Rare,
        CoinPrice = 2000,
        PrimaryColor = new SKColor(80, 255, 50),
        SecondaryColor = new SKColor(20, 120, 10),
        GlowColor = new SKColor(80, 255, 50, 60)
    };

    public static readonly SkinDefinition Ocean = new()
    {
        Id = "ocean",
        NameKey = "SkinOcean",
        Rarity = Rarity.Rare,
        CoinPrice = 3000,
        PrimaryColor = new SKColor(20, 80, 180),
        SecondaryColor = new SKColor(0, 200, 180)
    };

    public static readonly SkinDefinition Sunset = new()
    {
        Id = "sunset",
        NameKey = "SkinSunset",
        Rarity = Rarity.Rare,
        CoinPrice = 3000,
        PrimaryColor = new SKColor(255, 140, 30),
        SecondaryColor = new SKColor(180, 50, 200),
        GlowColor = new SKColor(255, 140, 30, 50)
    };

    public static readonly SkinDefinition Cherry = new()
    {
        Id = "cherry",
        NameKey = "SkinCherry",
        Rarity = Rarity.Rare,
        CoinPrice = 4000,
        PrimaryColor = new SKColor(255, 80, 150),
        SecondaryColor = new SKColor(200, 20, 100),
        GlowColor = new SKColor(255, 80, 150, 50)
    };

    public static readonly SkinDefinition Emerald = new()
    {
        Id = "emerald",
        NameKey = "SkinEmerald",
        Rarity = Rarity.Rare,
        CoinPrice = 5000,
        PrimaryColor = new SKColor(0, 200, 100),
        SecondaryColor = new SKColor(0, 120, 60),
        GlowColor = new SKColor(0, 255, 120, 60)
    };

    // Neue Rare Skins
    public static readonly SkinDefinition Ninja = new()
    {
        Id = "ninja",
        NameKey = "SkinNinja",
        Rarity = Rarity.Rare,
        CoinPrice = 4000,
        PrimaryColor = new SKColor(30, 30, 40),
        SecondaryColor = new SKColor(180, 20, 20)
    };

    public static readonly SkinDefinition Pirate = new()
    {
        Id = "pirate",
        NameKey = "SkinPirate",
        Rarity = Rarity.Rare,
        CoinPrice = 4500,
        PrimaryColor = new SKColor(100, 60, 20),
        SecondaryColor = new SKColor(200, 180, 50)
    };

    // Epic Skins
    public static readonly SkinDefinition Galaxy = new()
    {
        Id = "galaxy",
        NameKey = "SkinGalaxy",
        Rarity = Rarity.Epic,
        CoinPrice = 8000,
        PrimaryColor = new SKColor(100, 40, 200),
        SecondaryColor = new SKColor(230, 230, 255),
        GlowColor = new SKColor(150, 80, 255, 70)
    };

    public static readonly SkinDefinition Neon = new()
    {
        Id = "neon",
        NameKey = "SkinNeon",
        Rarity = Rarity.Epic,
        IsPremiumOnly = true,
        PrimaryColor = new SKColor(0, 255, 200),
        SecondaryColor = new SKColor(255, 0, 200),
        GlowColor = new SKColor(0, 255, 200, 60)
    };

    public static readonly SkinDefinition Cyber = new()
    {
        Id = "cyber",
        NameKey = "SkinCyber",
        Rarity = Rarity.Epic,
        IsPremiumOnly = true,
        PrimaryColor = new SKColor(100, 200, 255),
        SecondaryColor = new SKColor(0, 80, 180),
        GlowColor = new SKColor(100, 200, 255, 60)
    };

    public static readonly SkinDefinition Retro = new()
    {
        Id = "retro",
        NameKey = "SkinRetro",
        Rarity = Rarity.Epic,
        IsPremiumOnly = true,
        PrimaryColor = new SKColor(255, 100, 50),
        SecondaryColor = new SKColor(200, 50, 0),
        GlowColor = new SKColor(255, 100, 50, 60)
    };

    public static readonly SkinDefinition Robot = new()
    {
        Id = "robot",
        NameKey = "SkinRobot",
        Rarity = Rarity.Epic,
        CoinPrice = 10000,
        PrimaryColor = new SKColor(180, 190, 200),
        SecondaryColor = new SKColor(60, 140, 255),
        GlowColor = new SKColor(60, 140, 255, 60)
    };

    public static readonly SkinDefinition PixelRetro = new()
    {
        Id = "pixel_retro",
        NameKey = "SkinPixelRetro",
        Rarity = Rarity.Epic,
        CoinPrice = 12000,
        PrimaryColor = new SKColor(200, 150, 80),
        SecondaryColor = new SKColor(140, 100, 50),
        GlowColor = new SKColor(200, 150, 80, 50)
    };

    // Legendary Skins
    public static readonly SkinDefinition Gold = new()
    {
        Id = "gold",
        NameKey = "SkinGold",
        Rarity = Rarity.Legendary,
        IsPremiumOnly = true,
        PrimaryColor = new SKColor(255, 215, 0),
        SecondaryColor = new SKColor(218, 165, 32),
        GlowColor = new SKColor(255, 215, 0, 80)
    };

    public static readonly SkinDefinition Inferno = new()
    {
        Id = "inferno",
        NameKey = "SkinInferno",
        Rarity = Rarity.Legendary,
        IsPremiumOnly = true,
        PrimaryColor = new SKColor(255, 80, 0),
        SecondaryColor = new SKColor(200, 30, 0),
        GlowColor = new SKColor(255, 120, 0, 80)
    };

    public static readonly SkinDefinition Dragon = new()
    {
        Id = "dragon",
        NameKey = "SkinDragon",
        Rarity = Rarity.Legendary,
        CoinPrice = 25000,
        PrimaryColor = new SKColor(160, 30, 50),
        SecondaryColor = new SKColor(255, 180, 30),
        GlowColor = new SKColor(255, 100, 20, 80)
    };

    public static readonly SkinDefinition[] All =
        [Default, Crimson, Arctic, Stealth,
         Toxic, Ocean, Sunset, Cherry, Emerald, Ninja, Pirate,
         Galaxy, Neon, Cyber, Retro, Robot, PixelRetro,
         Gold, Inferno, Dragon];
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

/// <summary>
/// Definiert einen Bomben-Skin mit Farben fuer Body, Glow, Zuendschnur und Funken
/// </summary>
public class BombSkinDefinition
{
    public string Id { get; init; } = "";
    public string NameKey { get; init; } = "";
    public bool IsPremiumOnly { get; init; }
    public int CoinPrice { get; init; }
    public Rarity Rarity { get; init; }
    public SKColor BodyColor { get; init; }
    public SKColor GlowColor { get; init; }
    public SKColor FuseColor { get; init; }
    public SKColor SparkColor { get; init; }
    public SKColor HighlightColor { get; init; }
}

/// <summary>
/// Vordefinierte Bomben-Skins
/// </summary>
public static class BombSkins
{
    public static readonly BombSkinDefinition Default = new()
    {
        Id = "bomb_default", NameKey = "BombSkinDefault",
        Rarity = Rarity.Common,
        BodyColor = SKColor.Empty,
        GlowColor = SKColor.Empty,
        FuseColor = SKColor.Empty,
        SparkColor = SKColor.Empty,
        HighlightColor = SKColor.Empty
    };

    public static readonly BombSkinDefinition Fire = new()
    {
        Id = "bomb_fire", NameKey = "BombSkinFire",
        Rarity = Rarity.Common, CoinPrice = 2000,
        BodyColor = new SKColor(180, 40, 10),
        GlowColor = new SKColor(255, 120, 0),
        FuseColor = new SKColor(255, 200, 50),
        SparkColor = new SKColor(255, 255, 100),
        HighlightColor = new SKColor(255, 180, 80, 120)
    };

    public static readonly BombSkinDefinition Ice = new()
    {
        Id = "bomb_ice", NameKey = "BombSkinIce",
        Rarity = Rarity.Common, CoinPrice = 2000,
        BodyColor = new SKColor(120, 200, 255),
        GlowColor = new SKColor(80, 180, 255),
        FuseColor = new SKColor(180, 230, 255),
        SparkColor = new SKColor(220, 245, 255),
        HighlightColor = new SKColor(255, 255, 255, 140)
    };

    public static readonly BombSkinDefinition Neon = new()
    {
        Id = "bomb_neon", NameKey = "BombSkinNeon",
        Rarity = Rarity.Rare, CoinPrice = 3000,
        BodyColor = new SKColor(20, 20, 40),
        GlowColor = new SKColor(0, 255, 200),
        FuseColor = new SKColor(255, 0, 200),
        SparkColor = new SKColor(200, 100, 255),
        HighlightColor = new SKColor(0, 255, 200, 80)
    };

    public static readonly BombSkinDefinition Pixel = new()
    {
        Id = "bomb_pixel", NameKey = "BombSkinPixel",
        Rarity = Rarity.Rare, CoinPrice = 3000,
        BodyColor = new SKColor(40, 40, 40),
        GlowColor = new SKColor(200, 80, 0),
        FuseColor = new SKColor(180, 140, 40),
        SparkColor = new SKColor(255, 200, 50),
        HighlightColor = new SKColor(120, 120, 120, 100)
    };

    public static readonly BombSkinDefinition Watermelon = new()
    {
        Id = "bomb_watermelon", NameKey = "BombSkinWatermelon",
        Rarity = Rarity.Rare, CoinPrice = 3500,
        BodyColor = new SKColor(40, 140, 40),
        GlowColor = new SKColor(80, 200, 60),
        FuseColor = new SKColor(200, 80, 80),
        SparkColor = new SKColor(255, 100, 100),
        HighlightColor = new SKColor(100, 255, 100, 80)
    };

    public static readonly BombSkinDefinition Skull = new()
    {
        Id = "bomb_skull", NameKey = "BombSkinSkull",
        Rarity = Rarity.Rare, CoinPrice = 4000,
        BodyColor = new SKColor(40, 40, 40),
        GlowColor = new SKColor(200, 200, 200),
        FuseColor = new SKColor(150, 150, 150),
        SparkColor = new SKColor(255, 255, 255),
        HighlightColor = new SKColor(200, 200, 200, 80)
    };

    public static readonly BombSkinDefinition Toxic = new()
    {
        Id = "bomb_toxic", NameKey = "BombSkinToxic",
        Rarity = Rarity.Rare, CoinPrice = 4000,
        BodyColor = new SKColor(30, 80, 20),
        GlowColor = new SKColor(80, 255, 50),
        FuseColor = new SKColor(100, 200, 50),
        SparkColor = new SKColor(150, 255, 100),
        HighlightColor = new SKColor(80, 255, 50, 80)
    };

    public static readonly BombSkinDefinition GoldBomb = new()
    {
        Id = "bomb_gold", NameKey = "BombSkinGold",
        Rarity = Rarity.Epic, CoinPrice = 5000,
        BodyColor = new SKColor(200, 170, 30),
        GlowColor = new SKColor(255, 215, 0),
        FuseColor = new SKColor(218, 185, 50),
        SparkColor = new SKColor(255, 240, 120),
        HighlightColor = new SKColor(255, 255, 200, 150)
    };

    public static readonly BombSkinDefinition Plasma = new()
    {
        Id = "bomb_plasma", NameKey = "BombSkinPlasma",
        Rarity = Rarity.Epic, CoinPrice = 8000,
        BodyColor = new SKColor(60, 20, 100),
        GlowColor = new SKColor(200, 50, 255),
        FuseColor = new SKColor(180, 100, 255),
        SparkColor = new SKColor(220, 180, 255),
        HighlightColor = new SKColor(200, 100, 255, 100)
    };

    public static readonly BombSkinDefinition Lava = new()
    {
        Id = "bomb_lava", NameKey = "BombSkinLava",
        Rarity = Rarity.Epic, CoinPrice = 10000,
        BodyColor = new SKColor(80, 20, 10),
        GlowColor = new SKColor(255, 80, 20),
        FuseColor = new SKColor(255, 160, 40),
        SparkColor = new SKColor(255, 200, 80),
        HighlightColor = new SKColor(255, 120, 40, 120)
    };

    public static readonly BombSkinDefinition Diamond = new()
    {
        Id = "bomb_diamond", NameKey = "BombSkinDiamond",
        Rarity = Rarity.Epic, CoinPrice = 8000,
        BodyColor = new SKColor(180, 220, 255),
        GlowColor = new SKColor(150, 200, 255),
        FuseColor = new SKColor(200, 230, 255),
        SparkColor = new SKColor(255, 255, 255),
        HighlightColor = new SKColor(255, 255, 255, 180)
    };

    public static readonly BombSkinDefinition Phantom = new()
    {
        Id = "bomb_phantom", NameKey = "BombSkinPhantom",
        Rarity = Rarity.Legendary, IsPremiumOnly = true,
        BodyColor = new SKColor(100, 50, 160),
        GlowColor = new SKColor(160, 80, 255),
        FuseColor = new SKColor(180, 120, 255),
        SparkColor = new SKColor(220, 180, 255),
        HighlightColor = new SKColor(200, 160, 255, 80)
    };

    public static readonly BombSkinDefinition Celestial = new()
    {
        Id = "bomb_celestial", NameKey = "BombSkinCelestial",
        Rarity = Rarity.Legendary, CoinPrice = 25000,
        BodyColor = new SKColor(20, 20, 60),
        GlowColor = new SKColor(255, 220, 100),
        FuseColor = new SKColor(255, 255, 200),
        SparkColor = new SKColor(255, 255, 255),
        HighlightColor = new SKColor(255, 240, 150, 120)
    };

    public static readonly BombSkinDefinition[] All =
        [Default, Fire, Ice,
         Neon, Pixel, Watermelon, Skull, Toxic,
         GoldBomb, Plasma, Lava, Diamond,
         Phantom, Celestial];
}

/// <summary>
/// Definiert einen Explosions-Skin mit Flammenfarben
/// </summary>
public class ExplosionSkinDefinition
{
    public string Id { get; init; } = "";
    public string NameKey { get; init; } = "";
    public bool IsPremiumOnly { get; init; }
    public int CoinPrice { get; init; }
    public Rarity Rarity { get; init; }
    public SKColor OuterColor { get; init; }
    public SKColor InnerColor { get; init; }
    public SKColor CoreColor { get; init; }
}

/// <summary>
/// Vordefinierte Explosions-Skins
/// </summary>
public static class ExplosionSkins
{
    public static readonly ExplosionSkinDefinition Default = new()
    {
        Id = "expl_default", NameKey = "ExplosionSkinDefault",
        Rarity = Rarity.Common,
        OuterColor = SKColor.Empty,
        InnerColor = SKColor.Empty,
        CoreColor = SKColor.Empty
    };

    public static readonly ExplosionSkinDefinition Blue = new()
    {
        Id = "expl_blue", NameKey = "ExplosionSkinBlue",
        Rarity = Rarity.Common, CoinPrice = 3000,
        OuterColor = new SKColor(30, 100, 255),
        InnerColor = new SKColor(100, 180, 255),
        CoreColor = new SKColor(200, 230, 255)
    };

    public static readonly ExplosionSkinDefinition Green = new()
    {
        Id = "expl_green", NameKey = "ExplosionSkinGreen",
        Rarity = Rarity.Common, CoinPrice = 3000,
        OuterColor = new SKColor(30, 200, 50),
        InnerColor = new SKColor(100, 255, 100),
        CoreColor = new SKColor(200, 255, 150)
    };

    public static readonly ExplosionSkinDefinition Pink = new()
    {
        Id = "expl_pink", NameKey = "ExplosionSkinPink",
        Rarity = Rarity.Rare, CoinPrice = 4000,
        OuterColor = new SKColor(255, 50, 150),
        InnerColor = new SKColor(255, 150, 200),
        CoreColor = new SKColor(255, 220, 240)
    };

    public static readonly ExplosionSkinDefinition Shadow = new()
    {
        Id = "expl_shadow", NameKey = "ExplosionSkinShadow",
        Rarity = Rarity.Rare, CoinPrice = 5000,
        OuterColor = new SKColor(60, 20, 80),
        InnerColor = new SKColor(120, 60, 180),
        CoreColor = new SKColor(200, 150, 255)
    };

    public static readonly ExplosionSkinDefinition CherryBlossom = new()
    {
        Id = "expl_cherry", NameKey = "ExplosionSkinCherry",
        Rarity = Rarity.Rare, CoinPrice = 4000,
        OuterColor = new SKColor(255, 150, 180),
        InnerColor = new SKColor(255, 200, 220),
        CoreColor = new SKColor(255, 240, 245)
    };

    public static readonly ExplosionSkinDefinition Electric = new()
    {
        Id = "expl_electric", NameKey = "ExplosionSkinElectric",
        Rarity = Rarity.Rare, CoinPrice = 5000,
        OuterColor = new SKColor(50, 150, 255),
        InnerColor = new SKColor(150, 220, 255),
        CoreColor = new SKColor(220, 240, 255)
    };

    public static readonly ExplosionSkinDefinition Toxic = new()
    {
        Id = "expl_toxic", NameKey = "ExplosionSkinToxic",
        Rarity = Rarity.Rare, CoinPrice = 4500,
        OuterColor = new SKColor(40, 150, 20),
        InnerColor = new SKColor(80, 255, 50),
        CoreColor = new SKColor(180, 255, 150)
    };

    public static readonly ExplosionSkinDefinition Plasma = new()
    {
        Id = "expl_plasma", NameKey = "ExplosionSkinPlasma",
        Rarity = Rarity.Epic, CoinPrice = 8000,
        OuterColor = new SKColor(150, 30, 200),
        InnerColor = new SKColor(200, 100, 255),
        CoreColor = new SKColor(230, 200, 255)
    };

    public static readonly ExplosionSkinDefinition Frost = new()
    {
        Id = "expl_frost", NameKey = "ExplosionSkinFrost",
        Rarity = Rarity.Epic, CoinPrice = 8000,
        OuterColor = new SKColor(80, 160, 255),
        InnerColor = new SKColor(160, 220, 255),
        CoreColor = new SKColor(240, 250, 255)
    };

    public static readonly ExplosionSkinDefinition Lava = new()
    {
        Id = "expl_lava", NameKey = "ExplosionSkinLava",
        Rarity = Rarity.Epic, CoinPrice = 10000,
        OuterColor = new SKColor(180, 30, 0),
        InnerColor = new SKColor(255, 120, 20),
        CoreColor = new SKColor(255, 220, 100)
    };

    public static readonly ExplosionSkinDefinition Galaxy = new()
    {
        Id = "expl_galaxy", NameKey = "ExplosionSkinGalaxy",
        Rarity = Rarity.Epic, CoinPrice = 12000,
        OuterColor = new SKColor(60, 20, 120),
        InnerColor = new SKColor(100, 50, 200),
        CoreColor = new SKColor(200, 180, 255)
    };

    public static readonly ExplosionSkinDefinition Void = new()
    {
        Id = "expl_void", NameKey = "ExplosionSkinVoid",
        Rarity = Rarity.Epic, CoinPrice = 15000,
        OuterColor = new SKColor(20, 10, 30),
        InnerColor = new SKColor(80, 40, 140),
        CoreColor = new SKColor(160, 100, 255)
    };

    public static readonly ExplosionSkinDefinition Rainbow = new()
    {
        Id = "expl_rainbow", NameKey = "ExplosionSkinRainbow",
        Rarity = Rarity.Legendary, CoinPrice = 25000,
        OuterColor = new SKColor(255, 100, 50),
        InnerColor = new SKColor(100, 255, 100),
        CoreColor = new SKColor(100, 150, 255)
    };

    public static readonly ExplosionSkinDefinition Nova = new()
    {
        Id = "expl_nova", NameKey = "ExplosionSkinNova",
        Rarity = Rarity.Legendary, IsPremiumOnly = true,
        OuterColor = new SKColor(255, 200, 50),
        InnerColor = new SKColor(255, 255, 150),
        CoreColor = new SKColor(255, 255, 255)
    };

    public static readonly ExplosionSkinDefinition[] All =
        [Default, Blue, Green,
         Pink, Shadow, CherryBlossom, Electric, Toxic,
         Plasma, Frost, Lava, Galaxy, Void,
         Rainbow, Nova];
}
