namespace BomberBlast.Models.Entities;

/// <summary>
/// Types of enemies (original NES Bomberman)
/// </summary>
public enum EnemyType
{
    /// <summary>Slowest, dumbest enemy - basic tutorial fodder</summary>
    Ballom,

    /// <summary>Normal speed, somewhat random movement</summary>
    Onil,

    /// <summary>Normal speed, low intelligence - predictable</summary>
    Doll,

    /// <summary>Fast, normal intelligence - dangerous</summary>
    Minvo,

    /// <summary>Very slow but can walk through walls - sneaky</summary>
    Kondoria,

    /// <summary>Slow, can walk through walls - ghost-like</summary>
    Ovapi,

    /// <summary>Fast, high intelligence - actively chases</summary>
    Pass,

    /// <summary>Very fast, high intelligence, walks through walls - most dangerous</summary>
    Pontan,

    /// <summary>Langsam aber überlebt eine Explosion (2 Hits nötig)</summary>
    Tanker,

    /// <summary>Schnell, wird periodisch unsichtbar (3s sichtbar / 2s unsichtbar)</summary>
    Ghost,

    /// <summary>Normal, teilt sich bei Tod in 2 Mini-Splitter</summary>
    Splitter,

    /// <summary>Tarnt sich als Block, greift an wenn Spieler nahe</summary>
    Mimic
}

public static class EnemyTypeExtensions
{
    /// <summary>
    /// Get base movement speed in pixels per second
    /// </summary>
    public static float GetSpeed(this EnemyType type)
    {
        return type switch
        {
            EnemyType.Ballom => 30f,    // Slow
            EnemyType.Onil => 45f,      // Normal
            EnemyType.Doll => 45f,      // Normal
            EnemyType.Minvo => 65f,     // Fast
            EnemyType.Kondoria => 20f,  // Very slow
            EnemyType.Ovapi => 35f,     // Slow
            EnemyType.Pass => 70f,      // Fast
            EnemyType.Pontan => 60f,    // Fast (Strafe, aber nicht unfair)
            EnemyType.Tanker => 25f,    // Sehr langsam
            EnemyType.Ghost => 40f,     // Mittel
            EnemyType.Splitter => 50f,  // Schnell
            EnemyType.Mimic => 55f,     // Schnell (nach Aktivierung)
            _ => 45f
        };
    }

    /// <summary>
    /// Get intelligence level (affects AI behavior)
    /// </summary>
    public static EnemyIntelligence GetIntelligence(this EnemyType type)
    {
        return type switch
        {
            EnemyType.Ballom => EnemyIntelligence.Low,
            EnemyType.Onil => EnemyIntelligence.Normal,
            EnemyType.Doll => EnemyIntelligence.Low,
            EnemyType.Minvo => EnemyIntelligence.Normal,
            EnemyType.Kondoria => EnemyIntelligence.High,
            EnemyType.Ovapi => EnemyIntelligence.Normal,
            EnemyType.Pass => EnemyIntelligence.High,
            EnemyType.Pontan => EnemyIntelligence.High,
            EnemyType.Tanker => EnemyIntelligence.Normal,
            EnemyType.Ghost => EnemyIntelligence.High,
            EnemyType.Splitter => EnemyIntelligence.Normal,
            EnemyType.Mimic => EnemyIntelligence.High,
            _ => EnemyIntelligence.Normal
        };
    }

    /// <summary>
    /// Check if enemy can walk through destructible blocks
    /// </summary>
    public static bool CanPassWalls(this EnemyType type)
    {
        return type switch
        {
            EnemyType.Kondoria => true,
            EnemyType.Ovapi => true,
            EnemyType.Pontan => true,
            EnemyType.Ghost => true, // Geht durch Wände
            _ => false
        };
    }

    /// <summary>
    /// Get point value when defeated
    /// </summary>
    public static int GetPoints(this EnemyType type)
    {
        return type switch
        {
            EnemyType.Ballom => 100,
            EnemyType.Onil => 200,
            EnemyType.Doll => 400,
            EnemyType.Minvo => 800,
            EnemyType.Kondoria => 1000,
            EnemyType.Ovapi => 2000,
            EnemyType.Pass => 4000,
            EnemyType.Pontan => 8000,
            EnemyType.Tanker => 3000,
            EnemyType.Ghost => 5000,
            EnemyType.Splitter => 1500,
            EnemyType.Mimic => 6000,
            _ => 100
        };
    }

    /// <summary>
    /// Get sprite row index for this enemy type
    /// </summary>
    public static int GetSpriteRow(this EnemyType type)
    {
        return (int)type;
    }

    /// <summary>
    /// Get color for fallback rendering (when sprites not loaded)
    /// </summary>
    public static (byte r, byte g, byte b) GetColor(this EnemyType type)
    {
        return type switch
        {
            EnemyType.Ballom => (255, 180, 50),   // Bright orange
            EnemyType.Onil => (80, 120, 255),     // Bright blue
            EnemyType.Doll => (255, 150, 200),    // Bright pink
            EnemyType.Minvo => (255, 60, 60),     // Bright red
            EnemyType.Kondoria => (180, 80, 220), // Bright purple
            EnemyType.Ovapi => (80, 255, 255),    // Bright cyan
            EnemyType.Pass => (255, 255, 80),     // Bright yellow
            EnemyType.Pontan => (255, 255, 255),  // White
            EnemyType.Tanker => (100, 100, 120),   // Dunkelgrau/Stahl
            EnemyType.Ghost => (180, 200, 255),     // Geisterhaft blau-weiss
            EnemyType.Splitter => (255, 200, 0),    // Gelb-Orange
            EnemyType.Mimic => (180, 120, 60),      // Braun (Block-ähnlich)
            _ => (180, 180, 180)
        };
    }

    /// <summary>
    /// Anzahl der Treffer die nötig sind um diesen Gegner zu töten
    /// </summary>
    public static int GetHitPoints(this EnemyType type)
    {
        return type switch
        {
            EnemyType.Tanker => 2, // Überlebt 1 Explosion
            _ => 1
        };
    }

    /// <summary>
    /// Ob dieser Gegner-Typ Mini-Gegner beim Tod spawnt
    /// </summary>
    public static bool SplitsOnDeath(this EnemyType type)
    {
        return type == EnemyType.Splitter;
    }

    /// <summary>
    /// Ob dieser Gegner-Typ periodisch unsichtbar wird
    /// </summary>
    public static bool HasInvisibility(this EnemyType type)
    {
        return type == EnemyType.Ghost;
    }

    /// <summary>
    /// Ob dieser Gegner-Typ sich als Block tarnen kann
    /// </summary>
    public static bool CanDisguise(this EnemyType type)
    {
        return type == EnemyType.Mimic;
    }
}

/// <summary>
/// Intelligence level for enemy AI
/// </summary>
public enum EnemyIntelligence
{
    /// <summary>Predictable back-and-forth movement</summary>
    Low,

    /// <summary>Erratic movement, sometimes chases player</summary>
    Normal,

    /// <summary>Actively chases player, avoids bombs</summary>
    High
}

/// <summary>
/// AI behavior state for hysteresis (prevents rapid state switching)
/// </summary>
public enum EnemyAIState
{
    /// <summary>Random wandering movement</summary>
    Wandering,

    /// <summary>Actively chasing the player</summary>
    Chasing
}
