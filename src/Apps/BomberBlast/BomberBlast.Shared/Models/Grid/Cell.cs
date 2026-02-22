using BomberBlast.Models.Entities;

namespace BomberBlast.Models.Grid;

/// <summary>
/// Represents a single cell in the game grid
/// </summary>
public class Cell
{
    /// <summary>X position in grid (column)</summary>
    public int X { get; }

    /// <summary>Y position in grid (row)</summary>
    public int Y { get; }

    /// <summary>Type of this cell</summary>
    public CellType Type { get; set; }

    /// <summary>Power-up hidden in this block (null if none)</summary>
    public PowerUpType? HiddenPowerUp { get; set; }

    /// <summary>Exit-Tür unter diesem Block versteckt (klassisches Bomberman)</summary>
    public bool HasHiddenExit { get; set; }

    /// <summary>Whether this cell is currently being destroyed (animation)</summary>
    public bool IsDestroying { get; set; }

    /// <summary>Destruction animation progress (0.0 to 1.0)</summary>
    public float DestructionProgress { get; set; }

    /// <summary>Bomb placed on this cell (null if none)</summary>
    public Bomb? Bomb { get; set; }

    /// <summary>Power-up on this cell (revealed after block destruction)</summary>
    public PowerUp? PowerUp { get; set; }

    /// <summary>Whether this cell is part of an active explosion</summary>
    public bool IsExploding { get; set; }

    /// <summary>Explosion animation progress (0.0 to 1.0)</summary>
    public float ExplosionProgress { get; set; }

    /// <summary>Nachglüh-Timer nach Explosion (0 = kein Glow)</summary>
    public float AfterglowTimer { get; set; }

    /// <summary>Direction of explosion passing through (for sprite selection)</summary>
    public ExplosionDirection ExplosionDirection { get; set; }

    // === Welt-Mechaniken ===

    /// <summary>Förderband-Richtung (nur für CellType.Conveyor)</summary>
    public Direction ConveyorDirection { get; set; } = Direction.None;

    /// <summary>Teleporter-Partner-Position (nur für CellType.Teleporter)</summary>
    public (int x, int y)? TeleporterTarget { get; set; }

    /// <summary>Teleporter-Farbe/ID für visuelles Pairing (0 = blau, 1 = grün, 2 = orange)</summary>
    public int TeleporterColorId { get; set; }

    /// <summary>Lava-Riss Timer: Zählt hoch, aktiv wenn Timer mod Periode > Schwellwert</summary>
    public float LavaCrackTimer { get; set; }

    /// <summary>Ob Lava-Riss gerade aktiv/gefährlich ist</summary>
    public bool IsLavaCrackActive => Type == CellType.LavaCrack && (LavaCrackTimer % 4f) > 2.5f;

    /// <summary>Teleporter-Cooldown (verhindert Ping-Pong)</summary>
    public float TeleporterCooldown { get; set; }

    // === Spezial-Bomben-Effekte ===

    /// <summary>Ob die Zelle eingefroren ist (Eis-Bombe: verlangsamt Gegner/Spieler)</summary>
    public bool IsFrozen { get; set; }

    /// <summary>Verbleibende Frost-Dauer in Sekunden</summary>
    public float FreezeTimer { get; set; }

    /// <summary>Ob auf der Zelle Lava liegt (Feuer-Bombe: Schaden bei Betreten)</summary>
    public bool IsLavaActive { get; set; }

    /// <summary>Verbleibende Lava-Dauer in Sekunden</summary>
    public float LavaTimer { get; set; }

    // === Neue Bomben-Effekte (Phase 1) ===

    /// <summary>Ob die Zelle von einer Rauchwolke bedeckt ist (Smoke-Bombe: verwirrt Gegner-AI)</summary>
    public bool IsSmokeCloud { get; set; }

    /// <summary>Verbleibende Rauch-Dauer in Sekunden</summary>
    public float SmokeTimer { get; set; }

    /// <summary>Ob die Zelle vergiftet ist (Poison-Bombe: Schaden bei Betreten)</summary>
    public bool IsPoisoned { get; set; }

    /// <summary>Verbleibende Gift-Dauer in Sekunden</summary>
    public float PoisonTimer { get; set; }

    /// <summary>Ob die Zelle ein Gravitationsfeld hat (Gravity-Bombe: zieht Gegner an)</summary>
    public bool IsGravityWell { get; set; }

    /// <summary>Verbleibende Gravitations-Dauer in Sekunden</summary>
    public float GravityTimer { get; set; }

    /// <summary>Ob die Zelle zeitverlangsamt ist (TimeWarp-Bombe: 50% Speed)</summary>
    public bool IsTimeWarped { get; set; }

    /// <summary>Verbleibende TimeWarp-Dauer in Sekunden</summary>
    public float TimeWarpTimer { get; set; }

    /// <summary>Ob die Zelle ein Schwarzes-Loch-Feld hat (BlackHole-Bombe: saugt Gegner an)</summary>
    public bool IsBlackHole { get; set; }

    /// <summary>Verbleibende Schwarzes-Loch-Dauer in Sekunden</summary>
    public float BlackHoleTimer { get; set; }

    public Cell(int x, int y, CellType type = CellType.Empty)
    {
        X = x;
        Y = y;
        Type = type;
    }

    /// <summary>
    /// Check if player/enemy can walk through this cell
    /// </summary>
    public bool IsWalkable(bool canPassWalls = false, bool canPassBombs = false)
    {
        if (Type == CellType.Wall)
            return false;

        if (Type == CellType.Block && !canPassWalls)
            return false;

        // Neue Welt-Typen sind begehbar (Ice, Conveyor, Teleporter, LavaCrack)
        // LavaCrack tötet bei aktivem Zustand, blockiert aber nicht

        // Bomb blocks movement unless:
        // - canPassBombs is true (Bombpass power-up), OR
        // - PlayerOnTop is true (player just placed this bomb and is still on it)
        if (Bomb != null && !canPassBombs && !Bomb.PlayerOnTop)
            return false;

        return true;
    }

    /// <summary>
    /// Check if explosion can pass through this cell
    /// </summary>
    public bool CanExplosionPass(bool hasFlamePass = false)
    {
        // Walls always block explosions
        if (Type == CellType.Wall)
            return false;

        // Blocks stop explosions (but get destroyed)
        if (Type == CellType.Block)
            return false;

        return true;
    }

    /// <summary>
    /// Reset cell to empty state
    /// </summary>
    public void Clear()
    {
        Type = CellType.Empty;
        HiddenPowerUp = null;
        HasHiddenExit = false;
        IsDestroying = false;
        DestructionProgress = 0;
        Bomb = null;
        PowerUp = null;
        IsExploding = false;
        ExplosionProgress = 0;
        AfterglowTimer = 0;
        ConveyorDirection = Direction.None;
        TeleporterTarget = null;
        TeleporterColorId = 0;
        LavaCrackTimer = 0;
        TeleporterCooldown = 0;
        IsFrozen = false;
        FreezeTimer = 0;
        IsLavaActive = false;
        LavaTimer = 0;
        IsSmokeCloud = false;
        SmokeTimer = 0;
        IsPoisoned = false;
        PoisonTimer = 0;
        IsGravityWell = false;
        GravityTimer = 0;
        IsTimeWarped = false;
        TimeWarpTimer = 0;
        IsBlackHole = false;
        BlackHoleTimer = 0;
    }
}

/// <summary>
/// Direction of explosion sprite
/// </summary>
public enum ExplosionDirection
{
    Center,
    HorizontalMiddle,
    VerticalMiddle,
    LeftEnd,
    RightEnd,
    TopEnd,
    BottomEnd
}
