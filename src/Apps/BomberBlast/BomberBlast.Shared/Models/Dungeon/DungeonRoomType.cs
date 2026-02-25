namespace BomberBlast.Models.Dungeon;

/// <summary>
/// Raum-Typen für Dungeon-Floors. Jeder Floor hat einen Typ der Gegner/Layout/Belohnungen beeinflusst.
/// </summary>
public enum DungeonRoomType
{
    /// <summary>Standard-Floor (Weight 40)</summary>
    Normal,
    /// <summary>Stärkere Gegner, +50% Belohnungen (Weight 20)</summary>
    Elite,
    /// <summary>Wenig Gegner, viele PowerUps + garantierter Karten-Drop (Weight 15)</summary>
    Treasure,
    /// <summary>Spezial-Bedingung: nur 60s, oder keine PowerUps, oder doppelte Gegner (Weight 15)</summary>
    Challenge,
    /// <summary>Kein Kampf, automatisch Buff-Auswahl + Heilung (Weight 10, max 1 pro 5 Floors)</summary>
    Rest
}

/// <summary>
/// Challenge-Modus für Challenge-Räume
/// </summary>
public enum DungeonChallengeMode
{
    /// <summary>Nur 60 Sekunden Zeit</summary>
    SpeedRun,
    /// <summary>Keine PowerUps auf dem Floor</summary>
    NoPowerUps,
    /// <summary>Doppelte Gegner-Anzahl</summary>
    DoubleEnemies
}
