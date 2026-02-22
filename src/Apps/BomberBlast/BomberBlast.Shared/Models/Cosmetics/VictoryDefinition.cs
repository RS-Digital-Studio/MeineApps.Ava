namespace BomberBlast.Models.Cosmetics;

/// <summary>
/// Animations-Typ für die Sieges-Animation bei Level-Complete
/// </summary>
public enum VictoryAnimationType : byte
{
    Wave,         // Winken
    Jump,         // Freudensprung
    Clap,         // Klatschen
    Nod,          // Zustimmendes Nicken
    Spin,         // Drehung
    Dance,        // Tanzen (Seitwärts-Schritte)
    Flex,         // Muskeln zeigen
    Backflip,     // Rückwärts-Salto
    Headbang,     // Headbanging
    Moonwalk,     // Moonwalk
    Dab,          // Dabbing
    Breakdance,   // Breakdance-Move
    Tornado,      // Wirbel-Drehung mit Partikel
    FireDance,    // Feuertanz mit Flammen
    FrostAura,    // Frost-Aura Pulsation
    DragonRoar,   // Drachen-Brüllen mit Schockwelle
    SuperNova,    // Supernova-Explosion
    GoldExplosion // Goldene Münz-Explosion
}

/// <summary>
/// Definiert eine Sieges-Animation (bei Level-Complete)
/// </summary>
public class VictoryDefinition
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

    /// <summary>Animations-Typ</summary>
    public VictoryAnimationType AnimationType { get; init; }

    /// <summary>Animationsdauer in Sekunden</summary>
    public float Duration { get; init; } = 2.0f;
}

/// <summary>
/// Alle verfügbaren Sieges-Animationen (18 Animationen)
/// </summary>
public static class VictoryDefinitions
{
    // === Common (5) - 1.000-2.000 Coins ===

    public static readonly VictoryDefinition Wave = new()
    {
        Id = "victory_wave", NameKey = "VictoryWave", DescKey = "VictoryWaveDesc",
        Rarity = Rarity.Common, CoinPrice = 1000,
        AnimationType = VictoryAnimationType.Wave, Duration = 1.5f
    };

    public static readonly VictoryDefinition Jump = new()
    {
        Id = "victory_jump", NameKey = "VictoryJump", DescKey = "VictoryJumpDesc",
        Rarity = Rarity.Common, CoinPrice = 1000,
        AnimationType = VictoryAnimationType.Jump, Duration = 1.5f
    };

    public static readonly VictoryDefinition Clap = new()
    {
        Id = "victory_clap", NameKey = "VictoryClap", DescKey = "VictoryClapDesc",
        Rarity = Rarity.Common, CoinPrice = 1500,
        AnimationType = VictoryAnimationType.Clap, Duration = 1.5f
    };

    public static readonly VictoryDefinition Nod = new()
    {
        Id = "victory_nod", NameKey = "VictoryNod", DescKey = "VictoryNodDesc",
        Rarity = Rarity.Common, CoinPrice = 1500,
        AnimationType = VictoryAnimationType.Nod, Duration = 1.2f
    };

    public static readonly VictoryDefinition Spin = new()
    {
        Id = "victory_spin", NameKey = "VictorySpin", DescKey = "VictorySpinDesc",
        Rarity = Rarity.Common, CoinPrice = 2000,
        AnimationType = VictoryAnimationType.Spin, Duration = 1.8f
    };

    // === Rare (5) - 3.000-5.000 Coins ===

    public static readonly VictoryDefinition Dance = new()
    {
        Id = "victory_dance", NameKey = "VictoryDance", DescKey = "VictoryDanceDesc",
        Rarity = Rarity.Rare, CoinPrice = 3000,
        AnimationType = VictoryAnimationType.Dance, Duration = 2.0f
    };

    public static readonly VictoryDefinition Flex = new()
    {
        Id = "victory_flex", NameKey = "VictoryFlex", DescKey = "VictoryFlexDesc",
        Rarity = Rarity.Rare, CoinPrice = 3500,
        AnimationType = VictoryAnimationType.Flex, Duration = 1.8f
    };

    public static readonly VictoryDefinition Backflip = new()
    {
        Id = "victory_backflip", NameKey = "VictoryBackflip", DescKey = "VictoryBackflipDesc",
        Rarity = Rarity.Rare, CoinPrice = 4000,
        AnimationType = VictoryAnimationType.Backflip, Duration = 1.5f
    };

    public static readonly VictoryDefinition Headbang = new()
    {
        Id = "victory_headbang", NameKey = "VictoryHeadbang", DescKey = "VictoryHeadbangDesc",
        Rarity = Rarity.Rare, CoinPrice = 4000,
        AnimationType = VictoryAnimationType.Headbang, Duration = 2.0f
    };

    public static readonly VictoryDefinition Moonwalk = new()
    {
        Id = "victory_moonwalk", NameKey = "VictoryMoonwalk", DescKey = "VictoryMoonwalkDesc",
        Rarity = Rarity.Rare, CoinPrice = 5000,
        AnimationType = VictoryAnimationType.Moonwalk, Duration = 2.5f
    };

    // === Epic (5) - 8.000-15.000 Coins ===

    public static readonly VictoryDefinition Dab = new()
    {
        Id = "victory_dab", NameKey = "VictoryDab", DescKey = "VictoryDabDesc",
        Rarity = Rarity.Epic, CoinPrice = 8000,
        AnimationType = VictoryAnimationType.Dab, Duration = 1.5f
    };

    public static readonly VictoryDefinition Breakdance = new()
    {
        Id = "victory_breakdance", NameKey = "VictoryBreakdance", DescKey = "VictoryBreakdanceDesc",
        Rarity = Rarity.Epic, CoinPrice = 10000,
        AnimationType = VictoryAnimationType.Breakdance, Duration = 2.5f
    };

    public static readonly VictoryDefinition Tornado = new()
    {
        Id = "victory_tornado", NameKey = "VictoryTornado", DescKey = "VictoryTornadoDesc",
        Rarity = Rarity.Epic, CoinPrice = 10000,
        AnimationType = VictoryAnimationType.Tornado, Duration = 2.0f
    };

    public static readonly VictoryDefinition FireDance = new()
    {
        Id = "victory_firedance", NameKey = "VictoryFireDance", DescKey = "VictoryFireDanceDesc",
        Rarity = Rarity.Epic, CoinPrice = 12000,
        AnimationType = VictoryAnimationType.FireDance, Duration = 2.5f
    };

    public static readonly VictoryDefinition FrostAura = new()
    {
        Id = "victory_frostaura", NameKey = "VictoryFrostAura", DescKey = "VictoryFrostAuraDesc",
        Rarity = Rarity.Epic, CoinPrice = 15000,
        AnimationType = VictoryAnimationType.FrostAura, Duration = 2.0f
    };

    // === Legendary (3) - 25.000 Coins oder 200 Gems ===

    public static readonly VictoryDefinition DragonRoar = new()
    {
        Id = "victory_dragonroar", NameKey = "VictoryDragonRoar", DescKey = "VictoryDragonRoarDesc",
        Rarity = Rarity.Legendary, CoinPrice = 25000, GemPrice = 200,
        AnimationType = VictoryAnimationType.DragonRoar, Duration = 2.5f
    };

    public static readonly VictoryDefinition SuperNova = new()
    {
        Id = "victory_supernova", NameKey = "VictorySuperNova", DescKey = "VictorySuperNovaDesc",
        Rarity = Rarity.Legendary, GemPrice = 200,
        AnimationType = VictoryAnimationType.SuperNova, Duration = 3.0f
    };

    public static readonly VictoryDefinition GoldExplosion = new()
    {
        Id = "victory_goldexplosion", NameKey = "VictoryGoldExplosion", DescKey = "VictoryGoldExplosionDesc",
        Rarity = Rarity.Legendary, GemPrice = 200,
        AnimationType = VictoryAnimationType.GoldExplosion, Duration = 2.5f
    };

    public static readonly VictoryDefinition[] All =
    [
        Wave, Jump, Clap, Nod, Spin,
        Dance, Flex, Backflip, Headbang, Moonwalk,
        Dab, Breakdance, Tornado, FireDance, FrostAura,
        DragonRoar, SuperNova, GoldExplosion
    ];
}
