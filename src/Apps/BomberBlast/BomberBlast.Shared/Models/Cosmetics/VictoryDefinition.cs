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
    GoldExplosion,// Goldene Münz-Explosion

    // Phase 29 — Welt-/Saison-thematische Victory-Animationen (prozedural via Particle-System)
    PumpkinBurst,    // Halloween: Kürbis-Explosion mit violetter Aura
    SnowflakeSpin,   // Winter: Spin mit Schneeflocken-Wirbel
    CherryBloom,     // Sengoku: Kirschblüten-Wirbel
    NeonGlitch,      // Cyberpunk: RGB-Glitch + Hologramm-Pose
    BoneRattle,      // Dia de los Muertos: Knochen-Tanz
    OceanSplash,     // Underwater: Wasser-Explosion
    MechSalute,      // Mech: Stahl-Salut mit Funken
    SunBurst,        // Inferno: Sonnen-Strahlen-Explosion
    SamuraiBow,      // Sengoku: Samurai-Verbeugung
    SteamWhistle,    // Steampunk: Dampf-Pfeife + Zahnrad-Funken

    // Phase 29 — Karriere-Status-Animationen
    PrestigeFlare,   // Master-Mode: Iridescente Aura + Krone
    DiamondCascade,  // Liga-Diamond: Diamant-Kaskade
    AscensionRise,   // Dungeon-Ascension: Aufsteigend mit Lila-Aura
    ChampionPose,    // Liga-Saison-Top-3: Pose mit Goldener Trophäe
    SeasonFinale     // BP T30: Konfetti-Storm
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

    // === Phase 29 — Welt-thematische Victories ===

    public static readonly VictoryDefinition PumpkinBurst = new()
    {
        Id = "victory_pumpkinburst", NameKey = "VictoryPumpkinBurst", DescKey = "VictoryPumpkinBurstDesc",
        Rarity = Rarity.Rare, CoinPrice = 4500,
        AnimationType = VictoryAnimationType.PumpkinBurst, Duration = 2.0f
    };

    public static readonly VictoryDefinition SnowflakeSpin = new()
    {
        Id = "victory_snowflakespin", NameKey = "VictorySnowflakeSpin", DescKey = "VictorySnowflakeSpinDesc",
        Rarity = Rarity.Rare, CoinPrice = 4000,
        AnimationType = VictoryAnimationType.SnowflakeSpin, Duration = 2.0f
    };

    public static readonly VictoryDefinition CherryBloom = new()
    {
        Id = "victory_cherrybloom", NameKey = "VictoryCherryBloom", DescKey = "VictoryCherryBloomDesc",
        Rarity = Rarity.Rare, CoinPrice = 5000,
        AnimationType = VictoryAnimationType.CherryBloom, Duration = 2.5f
    };

    public static readonly VictoryDefinition NeonGlitch = new()
    {
        Id = "victory_neonglitch", NameKey = "VictoryNeonGlitch", DescKey = "VictoryNeonGlitchDesc",
        Rarity = Rarity.Epic, CoinPrice = 11000,
        AnimationType = VictoryAnimationType.NeonGlitch, Duration = 1.8f
    };

    public static readonly VictoryDefinition BoneRattle = new()
    {
        Id = "victory_bonerattle", NameKey = "VictoryBoneRattle", DescKey = "VictoryBoneRattleDesc",
        Rarity = Rarity.Epic, CoinPrice = 10000,
        AnimationType = VictoryAnimationType.BoneRattle, Duration = 2.2f
    };

    public static readonly VictoryDefinition OceanSplash = new()
    {
        Id = "victory_oceansplash", NameKey = "VictoryOceanSplash", DescKey = "VictoryOceanSplashDesc",
        Rarity = Rarity.Epic, CoinPrice = 9500,
        AnimationType = VictoryAnimationType.OceanSplash, Duration = 2.0f
    };

    public static readonly VictoryDefinition MechSalute = new()
    {
        Id = "victory_mechsalute", NameKey = "VictoryMechSalute", DescKey = "VictoryMechSaluteDesc",
        Rarity = Rarity.Epic, CoinPrice = 13000,
        AnimationType = VictoryAnimationType.MechSalute, Duration = 1.8f
    };

    public static readonly VictoryDefinition SunBurst = new()
    {
        Id = "victory_sunburst", NameKey = "VictorySunBurst", DescKey = "VictorySunBurstDesc",
        Rarity = Rarity.Epic, CoinPrice = 11500,
        AnimationType = VictoryAnimationType.SunBurst, Duration = 2.5f
    };

    public static readonly VictoryDefinition SamuraiBow = new()
    {
        Id = "victory_samuraibow", NameKey = "VictorySamuraiBow", DescKey = "VictorySamuraiBowDesc",
        Rarity = Rarity.Epic, CoinPrice = 14000,
        AnimationType = VictoryAnimationType.SamuraiBow, Duration = 2.0f
    };

    public static readonly VictoryDefinition SteamWhistle = new()
    {
        Id = "victory_steamwhistle", NameKey = "VictorySteamWhistle", DescKey = "VictorySteamWhistleDesc",
        Rarity = Rarity.Rare, CoinPrice = 5000,
        AnimationType = VictoryAnimationType.SteamWhistle, Duration = 1.8f
    };

    // === Phase 29 — Karriere-Status-Animationen (Reward-only) ===

    public static readonly VictoryDefinition PrestigeFlare = new()
    {
        Id = "victory_prestige", NameKey = "VictoryPrestige", DescKey = "VictoryPrestigeDesc",
        Rarity = Rarity.Legendary, // Master-Mode 100x
        AnimationType = VictoryAnimationType.PrestigeFlare, Duration = 3.0f
    };

    public static readonly VictoryDefinition DiamondCascade = new()
    {
        Id = "victory_diamond", NameKey = "VictoryDiamond", DescKey = "VictoryDiamondDesc",
        Rarity = Rarity.Legendary, // Liga-Diamond
        AnimationType = VictoryAnimationType.DiamondCascade, Duration = 2.8f
    };

    public static readonly VictoryDefinition AscensionRise = new()
    {
        Id = "victory_ascension", NameKey = "VictoryAscension", DescKey = "VictoryAscensionDesc",
        Rarity = Rarity.Legendary, // Dungeon-Ascension-5
        AnimationType = VictoryAnimationType.AscensionRise, Duration = 3.0f
    };

    public static readonly VictoryDefinition ChampionPose = new()
    {
        Id = "victory_champion", NameKey = "VictoryChampion", DescKey = "VictoryChampionDesc",
        Rarity = Rarity.Legendary, // Liga-Saison-Top-3
        AnimationType = VictoryAnimationType.ChampionPose, Duration = 2.5f
    };

    public static readonly VictoryDefinition SeasonFinale = new()
    {
        Id = "victory_seasonfinale", NameKey = "VictorySeasonFinale", DescKey = "VictorySeasonFinaleDesc",
        Rarity = Rarity.Legendary, // BP T30
        AnimationType = VictoryAnimationType.SeasonFinale, Duration = 3.0f
    };

    public static readonly VictoryDefinition[] All =
    [
        Wave, Jump, Clap, Nod, Spin,
        Dance, Flex, Backflip, Headbang, Moonwalk,
        Dab, Breakdance, Tornado, FireDance, FrostAura,
        DragonRoar, SuperNova, GoldExplosion,
        // Phase 29 (15 neue) — gesamt 33 Victories
        PumpkinBurst, SnowflakeSpin, CherryBloom, NeonGlitch, BoneRattle,
        OceanSplash, MechSalute, SunBurst, SamuraiBow, SteamWhistle,
        PrestigeFlare, DiamondCascade, AscensionRise, ChampionPose, SeasonFinale
    ];
}
