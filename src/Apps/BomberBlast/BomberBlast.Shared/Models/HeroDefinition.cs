using SkiaSharp;

namespace BomberBlast.Models;

/// <summary>
/// Hero/Character-Definition (Sprint 7.1 AAA-Audit #21).
///
/// <para>
/// 5 spielbare Charaktere mit unterschiedlichen Start-Stats. Mit existierender
/// Player-Engine moeglich — nur Sprite-Variation + Stat-Sheets.
/// </para>
///
/// <para>
/// Stats werden beim Spawn auf den Player angewendet (Player.MaxBombs, FireRange,
/// SpeedLevel etc.). Special-Trait wird in spezifischen Engine-Hooks ausgewertet
/// (z.B. CoinPickup-Multiplier in ICoinService).
/// </para>
///
/// <para>
/// Charaktere werden via IHeroService freigeschaltet — entweder via Achievement
/// oder Gem-Kauf. Aktiver Hero wird in Preferences persistiert.
/// </para>
/// </summary>
public sealed class HeroDefinition
{
    public required string Id { get; init; }
    public required string NameKey { get; init; }
    public required string DescriptionKey { get; init; }

    /// <summary>Start-MaxBombs (Default: 1).</summary>
    public int StartMaxBombs { get; init; } = 1;
    /// <summary>Start-FireRange (Default: 2).</summary>
    public int StartFireRange { get; init; } = 2;
    /// <summary>Start-SpeedLevel 0-3 (Default: 0 = BASE_SPEED 80px/s).</summary>
    public int StartSpeedLevel { get; init; } = 0;
    /// <summary>Start-Lives (Default: 3).</summary>
    public int StartLives { get; init; } = 3;

    /// <summary>Coin-Pickup-Multiplier (z.B. 1.05f = +5% extra Coins beim Pickup).</summary>
    public float CoinPickupMultiplier { get; init; } = 1.0f;
    /// <summary>PowerUp-Drop-Chance-Multiplier (z.B. 1.20f = +20% Drop-Chance).</summary>
    public float PowerUpDropMultiplier { get; init; } = 1.0f;
    /// <summary>Block-Drop-Chance-Bonus (additiv, z.B. 0.10f = +10% absolut).</summary>
    public float BlockDropChanceBonus { get; init; } = 0.0f;

    /// <summary>Spezial-Trait (Bombs-Mehrfachzuendung, Phantom-Frame etc.). None bei Default.</summary>
    public HeroTrait Trait { get; init; } = HeroTrait.None;

    /// <summary>Unlock-Bedingung (Achievement-ID oder "gems_500" fuer Direct-Buy).</summary>
    public string UnlockCondition { get; init; } = "default";  // "default" = von Anfang an unlocked

    /// <summary>Sprite-Hauptfarbe (Body) — Player-Renderer ueberschreibt damit Skin.</summary>
    public SKColor BodyColor { get; init; } = new(245, 245, 250);
    /// <summary>Sprite-Akzent-Farbe (Helm/Cape) — Player-Renderer.</summary>
    public SKColor AccentColor { get; init; } = new(60, 100, 200);
}

/// <summary>Spezial-Trait der Heroes (gameplay-relevant, nicht nur kosmetisch).</summary>
public enum HeroTrait
{
    None = 0,
    /// <summary>Twin-Tina: Bomben zuenden ein-zweimal nacheinander (Sekunde-Explosion-Wave).</summary>
    DoubleDetonation,
    /// <summary>Lucky-Lola: +20% PowerUp-Drop-Chance (additiv zu Block-Drop).</summary>
    LuckyDrops,
    /// <summary>Brick-Boris: -1 Start-Heart, +10% Block-Drop-Chance, Bomb-Power +1.</summary>
    DemolitionExpert,
    /// <summary>Speedy Sam: +5% Coin-Pickup-Multiplier, kein Speed-Penalty bei Curse.</summary>
    QuickPocket,
}

/// <summary>
/// Hardcoded Hero-Roster (5 Heroes pro Audit-Spec).
/// Wird vom IHeroService geladen.
/// </summary>
public static class HeroDefinitions
{
    public static readonly HeroDefinition Default = new()
    {
        Id = "hero_default",
        NameKey = "HeroDefaultName",
        DescriptionKey = "HeroDefaultDesc",
        StartMaxBombs = 1,
        StartFireRange = 2,
        StartSpeedLevel = 0,
        StartLives = 3,
        UnlockCondition = "default",
        BodyColor = new(245, 245, 250),
        AccentColor = new(60, 100, 200),
    };

    public static readonly HeroDefinition SpeedySam = new()
    {
        Id = "hero_speedy_sam",
        NameKey = "HeroSpeedySamName",
        DescriptionKey = "HeroSpeedySamDesc",
        StartMaxBombs = 1,
        StartFireRange = 1,
        StartSpeedLevel = 1,        // Speed-Boost from start
        StartLives = 3,
        CoinPickupMultiplier = 1.05f,
        Trait = HeroTrait.QuickPocket,
        UnlockCondition = "ach_speed_demon",
        BodyColor = new(255, 220, 100),  // Gelb
        AccentColor = new(220, 140, 0),
    };

    public static readonly HeroDefinition BrickBoris = new()
    {
        Id = "hero_brick_boris",
        NameKey = "HeroBrickBorisName",
        DescriptionKey = "HeroBrickBorisDesc",
        StartMaxBombs = 2,
        StartFireRange = 3,
        StartSpeedLevel = 0,        // Slow but powerful
        StartLives = 2,             // -1 Heart trade-off
        BlockDropChanceBonus = 0.10f,
        Trait = HeroTrait.DemolitionExpert,
        UnlockCondition = "ach_block_destroyer",
        BodyColor = new(180, 100, 60),  // Braun
        AccentColor = new(120, 60, 30),
    };

    public static readonly HeroDefinition TwinTina = new()
    {
        Id = "hero_twin_tina",
        NameKey = "HeroTwinTinaName",
        DescriptionKey = "HeroTwinTinaDesc",
        StartMaxBombs = 2,
        StartFireRange = 1,
        StartSpeedLevel = 0,
        StartLives = 3,
        Trait = HeroTrait.DoubleDetonation,
        UnlockCondition = "gems_500",   // Direct-Buy
        BodyColor = new(255, 100, 200),  // Pink
        AccentColor = new(180, 40, 140),
    };

    public static readonly HeroDefinition LuckyLola = new()
    {
        Id = "hero_lucky_lola",
        NameKey = "HeroLuckyLolaName",
        DescriptionKey = "HeroLuckyLolaDesc",
        StartMaxBombs = 1,
        StartFireRange = 2,
        StartSpeedLevel = 0,
        StartLives = 3,
        PowerUpDropMultiplier = 1.20f,
        Trait = HeroTrait.LuckyDrops,
        UnlockCondition = "ach_jackpot",
        BodyColor = new(120, 220, 120),  // Gruen
        AccentColor = new(60, 160, 60),
    };

    public static readonly HeroDefinition[] All =
    {
        Default, SpeedySam, BrickBoris, TwinTina, LuckyLola,
    };
}
