namespace BomberBlast.Models;

/// <summary>
/// Typen von Shop-Upgrades
/// </summary>
public enum UpgradeType
{
    /// <summary>Start-Bomben +1 (Max 3 Stufen)</summary>
    StartBombs,

    /// <summary>Start-Feuer +1 (Max 3 Stufen)</summary>
    StartFire,

    /// <summary>Start-Speed aktiv (Max 1 Stufe)</summary>
    StartSpeed,

    /// <summary>Extra Leben +1 (Max 2 Stufen)</summary>
    ExtraLives,

    /// <summary>Score-Multiplikator erhoehen (Max 3 Stufen)</summary>
    ScoreMultiplier,

    /// <summary>Zeitbonus verdoppeln (Max 1 Stufe)</summary>
    TimeBonus,

    /// <summary>Schutzschild beim Level-Start (Max 1 Stufe)</summary>
    ShieldStart,

    /// <summary>Münzbonus +25%/+50% (Max 2 Stufen)</summary>
    CoinBonus,

    /// <summary>Zusätzliche PowerUps pro Level (Max 2 Stufen)</summary>
    PowerUpLuck,

    /// <summary>Eis-Bombe freischalten (friert Gegner ein, Max 1 Stufe)</summary>
    IceBomb,

    /// <summary>Feuer-Bombe freischalten (+2 Reichweite, Lava-Nachwirkung, Max 1 Stufe)</summary>
    FireBomb,

    /// <summary>Klebe-Bombe freischalten (klebt an Wänden, Max 1 Stufe)</summary>
    StickyBomb
}
