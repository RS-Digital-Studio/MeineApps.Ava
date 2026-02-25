namespace BomberBlast.Models.Dungeon;

/// <summary>
/// Floor-Modifikatoren die ab Floor 3 zufällig auf einen Floor angewendet werden können (30% Chance).
/// Ändern Gameplay-Regeln für einen einzelnen Floor.
/// </summary>
public enum DungeonFloorModifier
{
    /// <summary>Kein Modifikator</summary>
    None,
    /// <summary>Äußere Reihe = Lava (sofortiger Tod)</summary>
    LavaBorders,
    /// <summary>Sichtradius 4 Zellen (wie Fog-Mechanik)</summary>
    Darkness,
    /// <summary>Doppelte Gegner-Anzahl</summary>
    DoubleSpawns,
    /// <summary>Zündschnur 50% kürzer</summary>
    FastBombs,
    /// <summary>Alle Explosionen +2 Range</summary>
    BigExplosions,
    /// <summary>Spieler heilt Shield nach 15s ohne Schaden</summary>
    Regeneration,
    /// <summary>3x Coins auf diesem Floor</summary>
    Wealthy
}
