namespace BomberBlast.Models.Entities;

/// <summary>
/// Types of power-ups (klassisches Bomberman + Erweiterungen)
/// </summary>
public enum PowerUpType
{
    /// <summary>+1 simultaneous bomb (max 10) - PERMANENT</summary>
    BombUp,

    /// <summary>+1 explosion range (max 10) - PERMANENT</summary>
    Fire,

    /// <summary>Increased movement speed - LOST ON DEATH</summary>
    Speed,

    /// <summary>Walk through destructible blocks - LOST ON DEATH</summary>
    Wallpass,

    /// <summary>Manual bomb detonation - LOST ON DEATH</summary>
    Detonator,

    /// <summary>Walk through own bombs - LOST ON DEATH</summary>
    Bombpass,

    /// <summary>Immune to explosions - LOST ON DEATH</summary>
    Flamepass,

    /// <summary>35 seconds invincibility - TEMPORARY</summary>
    Mystery,

    /// <summary>Kann Bomben in Bewegungsrichtung kicken - LOST ON DEATH</summary>
    Kick,

    /// <summary>Platziert alle Bomben in einer Linie - LOST ON DEATH</summary>
    LineBomb,

    /// <summary>Einzelne Mega-Bombe mit maximaler Reichweite - LOST ON DEATH</summary>
    PowerBomb,

    /// <summary>Zufälliger Debuff (Diarrhea/Slow/Constipation) - 6s TEMPORARY (v2.0.37: 10s→6s)</summary>
    Skull,

    /// <summary>
    /// Heilt aktive Curses sofort (entfernt Diarrhea/Slow/Constipation/ReverseControls).
    /// Spawn-Chance 5% in Welten mit Skull-Risiko (v2.0.37, Plan Task 2.5).
    /// WICHTIG: Cure als LETZTER Wert anhaengen — Skull-Persistenz darf nicht durch Enum-Verschiebung kaputt gehen.
    /// </summary>
    Cure
}

/// <summary>
/// Fluch-Typen für Skull-PowerUp
/// </summary>
public enum CurseType
{
    None,
    /// <summary>Legt automatisch Bomben ab</summary>
    Diarrhea,
    /// <summary>Geschwindigkeit halbiert</summary>
    Slow,
    /// <summary>Kann keine Bomben legen</summary>
    Constipation,
    /// <summary>Steuerung invertiert</summary>
    ReverseControls
}

public static class PowerUpExtensions
{
    // String-LUT fuer Hot-Path (PowerUp-Pickup-Tracking).
    // Vermeidet Enum.ToString()-Reflection-Allokation bei jedem Pickup.
    // Index-Reihenfolge IDENTISCH zur Enum-Reihenfolge (BombUp=0, Fire=1, ...).
    private static readonly string[] _typeNames =
    {
        "BombUp", "Fire", "Speed", "Wallpass", "Detonator",
        "Bombpass", "Flamepass", "Mystery", "Kick", "LineBomb",
        "PowerBomb", "Skull", "Cure"
    };

    /// <summary>
    /// Allocation-free Konvertierung PowerUpType → string fuer Tracking/Telemetrie.
    /// Falls Enum-Reihenfolge geaendert wird, ist die LUT manuell anzupassen.
    /// </summary>
    public static string ToFastString(this PowerUpType type)
    {
        int idx = (int)type;
        return idx >= 0 && idx < _typeNames.Length ? _typeNames[idx] : type.ToString();
    }

    /// <summary>
    /// Check if power-up is permanent (survives death)
    /// </summary>
    public static bool IsPermanent(this PowerUpType type)
    {
        return type switch
        {
            PowerUpType.BombUp => true,
            PowerUpType.Fire => true,
            _ => false
        };
    }

    /// <summary>
    /// Check if power-up is temporary (has duration)
    /// </summary>
    public static bool IsTemporary(this PowerUpType type)
    {
        return type is PowerUpType.Mystery or PowerUpType.Skull;
    }

    /// <summary>
    /// Ob das PowerUp ein negativer Effekt ist
    /// </summary>
    public static bool IsNegative(this PowerUpType type)
    {
        return type == PowerUpType.Skull;
    }

    /// <summary>
    /// Get duration in seconds for temporary power-ups
    /// </summary>
    public static float GetDuration(this PowerUpType type)
    {
        return type switch
        {
            PowerUpType.Mystery => 35f,
            PowerUpType.Skull => 10f,
            _ => 0f
        };
    }

    /// <summary>
    /// Get point value when collected
    /// </summary>
    public static int GetPoints(this PowerUpType type)
    {
        return type switch
        {
            PowerUpType.BombUp => 100,
            PowerUpType.Fire => 100,
            PowerUpType.Speed => 200,
            PowerUpType.Wallpass => 500,
            PowerUpType.Detonator => 500,
            PowerUpType.Bombpass => 500,
            PowerUpType.Flamepass => 500,
            PowerUpType.Mystery => 1000,
            PowerUpType.Kick => 300,
            PowerUpType.LineBomb => 400,
            PowerUpType.PowerBomb => 400,
            PowerUpType.Skull => 0, // Kein Score für negative Effekte
            PowerUpType.Cure => 250, // v2.0.37: Heilung als Belohnung wert
            _ => 0
        };
    }

    /// <summary>
    /// Get sprite frame index for this power-up
    /// </summary>
    public static int GetSpriteIndex(this PowerUpType type)
    {
        return (int)type;
    }

    /// <summary>
    /// Get display name key for localization
    /// </summary>
    public static string GetNameKey(this PowerUpType type)
    {
        return $"PowerUp_{type}";
    }

    /// <summary>
    /// Ab welchem Level dieses PowerUp freigeschaltet wird (Story-Modus).
    /// </summary>
    public static int GetUnlockLevel(this PowerUpType type) => type switch
    {
        PowerUpType.BombUp => 1,
        PowerUpType.Fire => 1,
        PowerUpType.Speed => 1,
        PowerUpType.Kick => 10,
        // v2.0.60 (B-A15): Wallpass von L20→L15. L20-24 sind Maze-dominiert —
        // ohne Wallpass extrem frustrierend. Spieler bekommt 5 Level Vorlauf.
        PowerUpType.Wallpass => 15,
        PowerUpType.Mystery => 15,
        // v2.0.60 (B-A12): Cure von L20→L15. Spieler hat damit eine etablierte
        // Cure-Strategie bevor der erste Skull (L20) trifft. Skull bleibt L20.
        PowerUpType.Cure => 15,
        PowerUpType.Skull => 20,
        PowerUpType.Detonator => 25,
        PowerUpType.Bombpass => 25,
        PowerUpType.LineBomb => 30,
        PowerUpType.Flamepass => 35,
        PowerUpType.PowerBomb => 40,
        _ => 1
    };
}
