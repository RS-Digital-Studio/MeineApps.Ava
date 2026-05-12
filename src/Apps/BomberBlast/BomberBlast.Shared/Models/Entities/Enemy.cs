using BomberBlast.Models.Grid;

namespace BomberBlast.Models.Entities;

/// <summary>
/// Base enemy class with type-specific behavior
/// </summary>
public class Enemy : Entity
{
    /// <summary>Type of this enemy</summary>
    public EnemyType Type { get; }

    /// <summary>
    /// Sprint 6.1 AAA-Audit #15: Elite-Variante (1.2x Speed, 2x HitPoints, 3x Points,
    /// lila Outline beim Rendern). Wird beim Spawn random mit ~10% Chance gesetzt
    /// (skaliert mit Welt fuer Difficulty-Curve). Boss-Enemies bleiben non-Elite.
    /// </summary>
    public bool IsElite { get; }

    /// <summary>Current facing/movement direction</summary>
    public Direction FacingDirection { get; set; } = Direction.Down;

    /// <summary>Current movement direction</summary>
    public Direction MovementDirection { get; set; } = Direction.None;

    /// <summary>Whether enemy is moving</summary>
    public bool IsMoving => MovementDirection != Direction.None;

    /// <summary>Movement speed in pixels per second</summary>
    public float Speed { get; }

    /// <summary>Intelligence level for AI</summary>
    public EnemyIntelligence Intelligence { get; }

    /// <summary>Whether enemy can pass through blocks</summary>
    public bool CanPassWalls { get; }

    /// <summary>Point value when killed</summary>
    public int Points { get; }

    /// <summary>Target grid position for AI pathfinding</summary>
    public (int x, int y)? TargetPosition { get; set; }

    /// <summary>Aktueller Pfad für AI-Navigation (wiederverwendete Queue, keine Heap-Allokation)</summary>
    public Queue<(int x, int y)> Path { get; } = new();

    /// <summary>
    /// Kopiert Pfaddaten aus einer Quell-Liste in die existierende Queue (keine Allokation).
    /// </summary>
    public void CopyPathFrom(IReadOnlyList<(int x, int y)> source)
    {
        Path.Clear();
        for (int i = 0; i < source.Count; i++)
            Path.Enqueue(source[i]);
    }

    /// <summary>Current AI behavior state (for hysteresis)</summary>
    public EnemyAIState AIState { get; set; } = EnemyAIState.Wandering;

    /// <summary>Time until next AI decision</summary>
    public float AIDecisionTimer { get; set; }

    /// <summary>AI decision interval (varies by intelligence)</summary>
    public float AIDecisionInterval => Intelligence switch
    {
        EnemyIntelligence.Low => 1.5f,
        EnemyIntelligence.Normal => 1.0f,
        EnemyIntelligence.High => 0.5f,
        _ => 1.0f
    };

    /// <summary>Time stuck on current position</summary>
    public float StuckTimer { get; set; }

    /// <summary>Last recorded grid position</summary>
    public (int x, int y) LastGridPosition { get; set; }

    /// <summary>Whether enemy is dying</summary>
    public bool IsDying { get; private set; }

    /// <summary>Death animation timer</summary>
    public float DeathTimer { get; private set; }

    private const float DEATH_ANIMATION_DURATION = 0.8f;

    // Tanker: Mehrfach-Treffer
    /// <summary>Verbleibende Trefferpunkte (Tanker braucht 2 Hits)</summary>
    public int HitPoints { get; set; }

    // Ghost: Unsichtbarkeit
    /// <summary>Ob der Ghost gerade unsichtbar ist</summary>
    public bool IsInvisible { get; private set; }
    private float _visibilityTimer;
    private const float GHOST_VISIBLE_TIME = 3f;
    private const float GHOST_INVISIBLE_TIME = 2f;

    // Mimic: Block-Tarnung
    /// <summary>Ob der Mimic sich als Block tarnt (inaktiv)</summary>
    public bool IsDisguised { get; set; }
    /// <summary>Entfernung ab der Mimic aktiviert wird (in Zellen)</summary>
    private const int MIMIC_ACTIVATION_DISTANCE = 3;

    // Splitter: Mini-Variante
    /// <summary>Ob dies ein Mini-Splitter ist (aus einem getöteten Splitter entstanden)</summary>
    public bool IsMiniSplitter { get; init; }

    // Spawn-Animation: 0.5s Portal-Effekt beim Erscheinen
    /// <summary>Timer fuer Spawn-Animation (0.5→0, danach normal)</summary>
    public float SpawnTimer { get; set; } = SPAWN_ANIMATION_DURATION;
    /// <summary>Ob der Gegner noch in der Spawn-Animation ist</summary>
    public bool IsSpawning => SpawnTimer > 0;
    private const float SPAWN_ANIMATION_DURATION = 0.5f;

    // HINWEIS (v2.0.35): Seit der Einführung von Entity.HitboxScale wird CollidesWith
    // nicht mehr gegen diese BoundingBox-Property ausgewertet. Der Override bleibt
    // als Sprite-Referenz erhalten (Debug-Overlay, Boss-Boss-Kollision in
    // GameEngine.Level nutzt direkt BoundingBox). Spieler-Enemy-Kollision nutzt
    // Entity.GetHitbox() mit HitboxScale=0.6 auf Width/Height.
    public override (float left, float top, float right, float bottom) BoundingBox
    {
        get
        {
            float size = GameGrid.CELL_SIZE * 0.35f;
            return (X - size, Y - size, X + size, Y + size);
        }
    }

    public Enemy(float x, float y, EnemyType type, bool isElite = false) : base(x, y)
    {
        Type = type;
        IsElite = isElite;
        // Sprint 6.1 AAA-Audit #15: Elite-Modifier multiplikativ.
        Speed = type.GetSpeed() * (isElite ? 1.2f : 1f);
        Intelligence = type.GetIntelligence();
        CanPassWalls = type.CanPassWalls();
        Points = type.GetPoints() * (isElite ? 3 : 1);
        HitPoints = type.GetHitPoints() * (isElite ? 2 : 1);
        LastGridPosition = (GridX, GridY);

        // Decision-Timer-Jitter: Wenn mehrere Gegner gleichzeitig spawnen, würde ein
        // initialer Timer von 0 alle Gegner im selben Frame A* rechnen lassen → Stutter.
        // Initialer zufälliger Offset im Intervall [0, AIDecisionInterval] verteilt die
        // ersten Decisions über die ersten 0.5-1.5s (je nach Intelligence) gleichmässig.
        // Random.Shared ist .NET 6+ thread-safe (intern ThreadStatic).
        AIDecisionTimer = Random.Shared.NextSingle() * AIDecisionInterval;

        // Mimic startet getarnt
        if (type.CanDisguise())
        {
            IsDisguised = true;
            IsActive = false; // Wird erst bei Spieler-Nähe aktiv
        }

        // Ghost startet sichtbar
        if (type.HasInvisibility())
            _visibilityTimer = GHOST_VISIBLE_TIME;
    }

    public override void Update(float deltaTime)
    {
        if (IsDying)
        {
            DeathTimer += deltaTime;
            if (DeathTimer >= DEATH_ANIMATION_DURATION)
            {
                IsMarkedForRemoval = true;
            }
            return;
        }

        // Spawn-Animation abbauen
        if (SpawnTimer > 0)
        {
            SpawnTimer -= deltaTime;
            if (SpawnTimer < 0) SpawnTimer = 0;
            return; // Während Spawn keine Bewegung
        }

        // Ghost: Unsichtbarkeits-Zyklus
        if (Type.HasInvisibility())
        {
            _visibilityTimer -= deltaTime;
            if (_visibilityTimer <= 0)
            {
                IsInvisible = !IsInvisible;
                _visibilityTimer = IsInvisible ? GHOST_INVISIBLE_TIME : GHOST_VISIBLE_TIME;
            }
        }

        // Getarnte Mimics bewegen sich nicht
        if (IsDisguised)
            return;

        base.Update(deltaTime);

        // Track stuck state
        var currentGridPos = (GridX, GridY);
        if (currentGridPos == LastGridPosition)
        {
            StuckTimer += deltaTime;
        }
        else
        {
            StuckTimer = 0;
            LastGridPosition = currentGridPos;
        }

        // Update AI decision timer
        AIDecisionTimer -= deltaTime;
    }

    /// <summary>
    /// Move enemy in current direction
    /// </summary>
    public void Move(float deltaTime, GameGrid grid)
    {
        if (IsDying || MovementDirection == Direction.None)
            return;

        FacingDirection = MovementDirection;

        float dx = MovementDirection.GetDeltaX() * Speed * deltaTime;
        float dy = MovementDirection.GetDeltaY() * Speed * deltaTime;

        float newX = X + dx;
        float newY = Y + dy;

        if (CanMoveTo(newX, newY, grid))
        {
            X = newX;
            Y = newY;
        }
        else
        {
            // Try single axis movement
            if (dx != 0 && CanMoveTo(newX, Y, grid))
            {
                X = newX;
            }
            else if (dy != 0 && CanMoveTo(X, newY, grid))
            {
                Y = newY;
            }
            else
            {
                // Blocked - AI should pick new direction
                MovementDirection = Direction.None;
            }
        }

        // Keep within grid bounds
        float halfSize = GameGrid.CELL_SIZE * 0.35f;
        X = Math.Clamp(X, halfSize, grid.PixelWidth - halfSize);
        Y = Math.Clamp(Y, halfSize, grid.PixelHeight - halfSize);
    }

    private bool CanMoveTo(float newX, float newY, GameGrid grid)
    {
        float halfSize = GameGrid.CELL_SIZE * 0.35f;
        return CollisionHelper.CanMoveToEnemy(newX, newY, halfSize, grid, CanPassWalls);
    }

    /// <summary>
    /// Schadenstreffer. Gibt true zurück wenn der Gegner stirbt.
    /// Tanker brauchen 2 Hits.
    /// </summary>
    public bool TakeDamage()
    {
        if (IsDying) return false;

        HitPoints--;
        if (HitPoints <= 0)
        {
            Kill();
            return true;
        }

        // Tanker überlebt - visuelles Feedback (kurzes Blinken)
        return false;
    }

    /// <summary>
    /// Kill the enemy (starts death animation)
    /// </summary>
    public void Kill()
    {
        if (IsDying)
            return;

        IsDying = true;
        DeathTimer = 0;
        AnimationFrame = 0;
        AnimationTimer = 0;
        IsActive = false;
        MovementDirection = Direction.None;
    }

    /// <summary>
    /// Mimic aktivieren (Tarnung aufheben), wenn Spieler nahe
    /// </summary>
    public bool TryActivateMimic(int playerGridX, int playerGridY)
    {
        if (!IsDisguised || Type != EnemyType.Mimic)
            return false;

        int distance = Math.Abs(GridX - playerGridX) + Math.Abs(GridY - playerGridY);
        if (distance <= MIMIC_ACTIVATION_DISTANCE)
        {
            IsDisguised = false;
            IsActive = true;
            return true;
        }
        return false;
    }

    protected override int GetAnimationFrameCount()
    {
        if (IsDying) return 4;
        return IsMoving ? 4 : 2;
    }

    /// <summary>
    /// Create enemy at grid position
    /// </summary>
    public static Enemy CreateAtGrid(int gridX, int gridY, EnemyType type)
    {
        float x = gridX * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        float y = gridY * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        return new Enemy(x, y, type);
    }

    /// <summary>
    /// Mini-Splitter erzeugen (aus einem getöteten Splitter, halbe Punkte, IsMiniSplitter=true)
    /// </summary>
    public static Enemy CreateMiniSplitterAtGrid(int gridX, int gridY)
    {
        float x = gridX * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        float y = gridY * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        return new Enemy(x, y, EnemyType.Splitter) { IsMiniSplitter = true };
    }
}
