using BomberBlast.Models.Entities;

namespace BomberBlast.Models.Levels;

/// <summary>
/// Represents a game level configuration
/// </summary>
public class Level
{
    /// <summary>Level number (1-50)</summary>
    public int Number { get; set; }

    /// <summary>Level name/title</summary>
    public string Name { get; set; } = "";

    /// <summary>Time limit in seconds (default 200)</summary>
    public int TimeLimit { get; set; } = 200;

    /// <summary>Block density (0.0-1.0)</summary>
    public float BlockDensity { get; set; } = 0.5f;

    /// <summary>Enemy spawns</summary>
    public List<EnemySpawn> Enemies { get; set; } = new();

    /// <summary>Power-ups hidden in blocks</summary>
    public List<PowerUpPlacement> PowerUps { get; set; } = new();

    /// <summary>Fixed block positions (null = random)</summary>
    public List<(int x, int y)>? FixedBlocks { get; set; }

    /// <summary>Exit position (null = random under block)</summary>
    public (int x, int y)? ExitPosition { get; set; }

    /// <summary>Random seed for reproducible generation</summary>
    public int? Seed { get; set; }

    /// <summary>Whether this is a bonus level</summary>
    public bool IsBonusLevel { get; set; }

    /// <summary>Ob dieses Level ein Boss-Level ist</summary>
    public bool IsBossLevel { get; set; }

    /// <summary>Background music track</summary>
    public string MusicTrack { get; set; } = "gameplay";

    /// <summary>Welt-spezifische Mechanik (welche Spezial-Zellen platziert werden)</summary>
    public WorldMechanic Mechanic { get; set; } = WorldMechanic.None;

    /// <summary>Vorgefertigtes Layout-Pattern (null = klassisches Random)</summary>
    public LevelLayout? Layout { get; set; }
}

/// <summary>
/// Welt-spezifische Gameplay-Mechaniken
/// </summary>
public enum WorldMechanic
{
    /// <summary>Keine Spezial-Mechanik (Welt 1: Forest)</summary>
    None,
    /// <summary>Eis-Boden: Spieler/Gegner rutschen (Welt 2: Industrial)</summary>
    Ice,
    /// <summary>Förderbänder: Schieben Entities in eine Richtung (Welt 3: Cavern)</summary>
    Conveyor,
    /// <summary>Teleporter: Paare von Portalen (Welt 4: Sky)</summary>
    Teleporter,
    /// <summary>Lava-Risse: Periodisch tödliche Bodenplatten (Welt 5: Inferno)</summary>
    LavaCrack
}

/// <summary>
/// Vorgefertigtes Level-Layout mit festen Wand-Positionen
/// </summary>
public enum LevelLayout
{
    /// <summary>Klassisches Bomberman Schachbrett-Muster</summary>
    Classic,
    /// <summary>Kreuzförmiger Korridor (offenes Zentrum)</summary>
    Cross,
    /// <summary>Arena mit Säulen (großer offener Bereich)</summary>
    Arena,
    /// <summary>Labyrinth mit engen Gängen</summary>
    Maze,
    /// <summary>Zwei Räume verbunden durch Engstelle</summary>
    TwoRooms,
    /// <summary>Spiralförmig von außen nach innen</summary>
    Spiral,
    /// <summary>Diagonale Korridore</summary>
    Diagonal,
    /// <summary>Boss-Arena: Großer offener Raum mit wenigen Säulen</summary>
    BossArena
}

/// <summary>
/// Enemy spawn configuration
/// </summary>
public class EnemySpawn
{
    public EnemyType Type { get; set; }
    public int Count { get; set; } = 1;
    public int? X { get; set; }
    public int? Y { get; set; }
}

/// <summary>
/// Power-up placement configuration
/// </summary>
public class PowerUpPlacement
{
    public PowerUpType Type { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
}
