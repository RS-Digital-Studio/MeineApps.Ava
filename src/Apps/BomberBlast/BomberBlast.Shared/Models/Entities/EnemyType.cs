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
    Pontan
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
            _ => (180, 180, 180)
        };
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
