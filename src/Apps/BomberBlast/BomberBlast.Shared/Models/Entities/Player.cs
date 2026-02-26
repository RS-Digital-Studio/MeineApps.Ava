using BomberBlast.Models.Cards;
using BomberBlast.Models.Grid;

namespace BomberBlast.Models.Entities;

/// <summary>
/// The player character (Bomberman)
/// </summary>
public class Player : Entity
{
    // Base stats
    private const float BASE_SPEED = 80f; // Pixel pro Sekunde
    private const float SPEED_BOOST = 20f; // Pro Speed-Level
    private const int MAX_BOMB_COUNT = 10;
    private const int MAX_FIRE_RANGE = 10;
    private const int MAX_SPEED_LEVEL = 3;

    // Movement
    public Direction FacingDirection { get; set; } = Direction.Down;
    public Direction MovementDirection { get; set; } = Direction.None;
    public bool IsMoving => MovementDirection != Direction.None;

    // Power-up stats (permanent, per Shop-Upgrades auch von aussen setzbar)
    public int MaxBombs { get; set; } = 1;
    public int FireRange { get; set; } = 1;

    // Power-up abilities (lost on death)
    public int SpeedLevel { get; set; }
    /// <summary>Kompatibilitäts-Property: true wenn SpeedLevel > 0</summary>
    public bool HasSpeed
    {
        get => SpeedLevel > 0;
        set => SpeedLevel = value ? Math.Max(SpeedLevel, 1) : 0;
    }
    public bool HasWallpass { get; set; }
    public bool HasDetonator { get; set; }
    public bool HasBombpass { get; set; }
    public bool HasFlamepass { get; set; }
    public bool HasKick { get; set; }
    public bool HasLineBomb { get; set; }
    public bool HasPowerBomb { get; set; }

    // Spezial-Bomben (Shop-Upgrade, begrenzt pro Level)
    /// <summary>Ob Eis-Bombe freigeschaltet ist (Shop-Upgrade)</summary>
    public bool HasIceBomb { get; set; }
    /// <summary>Ob Feuer-Bombe freigeschaltet ist (Shop-Upgrade)</summary>
    public bool HasFireBomb { get; set; }
    /// <summary>Ob Klebe-Bombe freigeschaltet ist (Shop-Upgrade)</summary>
    public bool HasStickyBomb { get; set; }
    /// <summary>Aktiver Spezial-Bomben-Typ (Normal = deaktiviert)</summary>
    public BombType ActiveSpecialBombType { get; set; } = BombType.Normal;
    /// <summary>Verbleibende Spezial-Bomben in diesem Level</summary>
    public int SpecialBombCount { get; set; }

    // Karten-Deck (Phase 2: Karten-System)
    /// <summary>Ausgerüstete Karten für dieses Level (mit verbleibenden Uses)</summary>
    public List<EquippedCard> EquippedCards { get; set; } = new();
    /// <summary>Aktiver Karten-Slot (-1 = Normalbombe, 0-3 = Deck-Slot)</summary>
    public int ActiveCardSlot { get; set; } = -1;
    /// <summary>Aktive Karte im aktuellen Slot (null wenn Normal oder leer)</summary>
    public EquippedCard? ActiveCard => ActiveCardSlot >= 0 && ActiveCardSlot < EquippedCards.Count
        ? EquippedCards[ActiveCardSlot]
        : null;

    // Skull/Curse-System
    public CurseType ActiveCurse { get; set; } = CurseType.None;
    public float CurseTimer { get; set; }
    public bool IsCursed => ActiveCurse != CurseType.None;
    private const float CURSE_DURATION = 10f;
    // Diarrhea: Auto-Bomben-Timer
    public float DiarrheaTimer { get; set; }

    // Schutzschild (absorbiert 1 Gegnerkontakt)
    public bool HasShield { get; set; }

    // Temporary invincibility (Mystery power-up)
    public bool IsInvincible { get; private set; }
    public float InvincibilityTimer { get; private set; }

    // Spawn invincibility (brief immunity after respawn)
    public bool HasSpawnProtection { get; private set; }
    public float SpawnProtectionTimer { get; private set; }
    private const float SPAWN_PROTECTION_DURATION = 2f;

    // Bomb tracking
    public int ActiveBombs { get; set; }

    // Death state
    public bool IsDying { get; private set; }
    public float DeathTimer { get; private set; }
    private const float DEATH_ANIMATION_DURATION = 1.5f;

    // Lives
    public int Lives { get; set; } = 3;

    // Score
    public int Score { get; set; }

    // Collision box is slightly smaller than sprite for forgiving gameplay
    public override (float left, float top, float right, float bottom) BoundingBox
    {
        get
        {
            // Use smaller hitbox (80% of cell size) centered on sprite
            float size = GameGrid.CELL_SIZE * 0.4f;
            return (X - size, Y - size, X + size, Y + size);
        }
    }

    /// <summary>
    /// Aktuelle Bewegungsgeschwindigkeit (staffelbar mit SpeedLevel 0-3, halbiert bei Slow-Curse)
    /// </summary>
    public float Speed
    {
        get
        {
            float speed = BASE_SPEED + SpeedLevel * SPEED_BOOST;
            if (ActiveCurse == CurseType.Slow) speed *= 0.5f;
            return speed;
        }
    }

    public Player(float x, float y) : base(x, y)
    {
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        // Update invincibility timer
        if (IsInvincible)
        {
            InvincibilityTimer -= deltaTime;
            if (InvincibilityTimer <= 0)
            {
                IsInvincible = false;
                InvincibilityTimer = 0;
            }
        }

        // Update spawn protection timer
        if (HasSpawnProtection)
        {
            SpawnProtectionTimer -= deltaTime;
            if (SpawnProtectionTimer <= 0)
            {
                HasSpawnProtection = false;
                SpawnProtectionTimer = 0;
            }
        }

        // Curse-Timer aktualisieren
        if (IsCursed)
        {
            CurseTimer -= deltaTime;
            if (CurseTimer <= 0)
            {
                ActiveCurse = CurseType.None;
                CurseTimer = 0;
            }

            // Diarrhea: Auto-Bomben alle 0.5s
            if (ActiveCurse == CurseType.Diarrhea)
            {
                DiarrheaTimer -= deltaTime;
            }
        }

        // Update death animation
        if (IsDying)
        {
            DeathTimer += deltaTime;
            // Spieler wird nie aus der Engine entfernt (Respawn oder GameOver)
            // → kein IsMarkedForRemoval setzen
        }
    }

    protected override int GetAnimationFrameCount()
    {
        if (IsDying) return 4;
        return IsMoving ? 4 : 1;
    }

    // Toleranz fuer Grid-Alignment (wie weit vom Zentrum entfernt noch auto-korrigiert wird)
    private const float GRID_ALIGN_THRESHOLD = 0.45f; // 45% der Zellgroesse
    private const float GRID_ALIGN_SPEED = 6f; // Multiplikator fuer Align-Geschwindigkeit

    // Stuck-Detection: Zählt Frames in denen sich der Spieler trotz Input nicht bewegt
    private int _stuckFrames;
    private const int STUCK_THRESHOLD = 15; // Nach 15 Frames (~250ms) ohne Bewegung → Recovery

    /// <summary>
    /// Bewege Spieler in aktuelle Richtung mit automatischem Grid-Alignment.
    /// Klassisches Bomberman-Gefühl: Querachse wird sanft zum Grid-Zentrum gezogen,
    /// sodass der Spieler nicht an Ecken hängen bleibt.
    /// </summary>
    public void Move(float deltaTime, GameGrid grid)
    {
        if (IsDying || MovementDirection == Direction.None)
        {
            _stuckFrames = 0;
            return;
        }

        FacingDirection = MovementDirection;

        float prevX = X;
        float prevY = Y;

        float speed = Speed * deltaTime;
        float dx = MovementDirection.GetDeltaX() * speed;
        float dy = MovementDirection.GetDeltaY() * speed;

        // Grid-Alignment: Querachse automatisch zum Zellzentrum ziehen
        // Wenn horizontal → Y alignen, wenn vertikal → X alignen
        if (dx != 0)
            AlignToGridAxis(ref dy, Y, speed, deltaTime);
        else if (dy != 0)
            AlignToGridAxis(ref dx, X, speed, deltaTime);

        TryMove(dx, dy, grid);

        // Stuck-Detection: Wenn Spieler aktiv steuert aber sich nicht bewegt
        if (MathF.Abs(X - prevX) < 0.01f && MathF.Abs(Y - prevY) < 0.01f)
        {
            _stuckFrames++;
            if (_stuckFrames >= STUCK_THRESHOLD)
            {
                RecoverToNearestCell(grid);
                _stuckFrames = 0;
            }
        }
        else
        {
            _stuckFrames = 0;
        }
    }

    /// <summary>
    /// Notfall-Recovery: Setzt den Spieler zum nächsten begehbaren Zellzentrum zurück.
    /// Wird nur ausgelöst wenn der Spieler mehrere Frames trotz Input feststeckt.
    /// </summary>
    private void RecoverToNearestCell(GameGrid grid)
    {
        float halfSize = GameGrid.CELL_SIZE * 0.35f;
        int gx = GridX;
        int gy = GridY;

        // Aktuelle Zelle prüfen
        float centerX = gx * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        float centerY = gy * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        if (CollisionHelper.CanMoveToPlayer(centerX, centerY, halfSize, grid, HasWallpass, HasBombpass))
        {
            X = centerX;
            Y = centerY;
            return;
        }

        // Nachbar-Zellen prüfen (4 Richtungen)
        int[] offsets = { 0, -1, 0, 1, -1, 0, 1, 0 };
        for (int i = 0; i < offsets.Length; i += 2)
        {
            int nx = gx + offsets[i];
            int ny = gy + offsets[i + 1];
            float ncx = nx * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            float ncy = ny * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            if (CollisionHelper.CanMoveToPlayer(ncx, ncy, halfSize, grid, HasWallpass, HasBombpass))
            {
                X = ncx;
                Y = ncy;
                return;
            }
        }
    }

    /// <summary>
    /// Querachse sanft zum nächsten Grid-Zentrum ziehen.
    /// Das verhindert das Hängenbleiben an Ecken komplett.
    /// </summary>
    private void AlignToGridAxis(ref float crossDelta, float crossPos, float speed, float deltaTime)
    {
        float cellCenter = MathF.Floor(crossPos / GameGrid.CELL_SIZE) * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        float offset = crossPos - cellCenter;
        float absOffset = MathF.Abs(offset);

        // Nur alignen wenn nah genug am Zentrum (sonst wäre es ein Zell-Wechsel)
        if (absOffset > 0.5f && absOffset < GameGrid.CELL_SIZE * GRID_ALIGN_THRESHOLD)
        {
            // Sanft zum Zentrum ziehen, Geschwindigkeit proportional zum Offset
            float alignAmount = MathF.Min(absOffset, speed * GRID_ALIGN_SPEED * deltaTime);
            crossDelta = -MathF.Sign(offset) * alignAmount;
        }
    }

    private void TryMove(float dx, float dy, GameGrid grid)
    {
        float newX = X + dx;
        float newY = Y + dy;

        // Direkte Bewegung möglich?
        if (CanMoveTo(newX, newY, grid))
        {
            X = newX;
            Y = newY;
        }
        else
        {
            // Achsen-Sliding: Versuche Bewegung auf einzelner Achse
            bool movedX = false, movedY = false;

            if (dx != 0 && CanMoveTo(newX, Y, grid))
            {
                X = newX;
                movedX = true;
            }
            if (dy != 0 && CanMoveTo(movedX ? X : X, newY, grid))
            {
                Y = newY;
                movedY = true;
            }

            // Corner-Assist: Wenn blockiert, prüfe ob wir fast an einer Ecke vorbei sind
            if (!movedX && !movedY)
            {
                TryCornerAssist(dx, dy, grid);
            }
        }

        // Grid-Bounds einhalten: Spieler-Hitbox darf nie in Außenwand-Zellen ragen
        // Außenwände sind Row/Col 0 und Row HEIGHT-1/Col WIDTH-1,
        // daher muss Minimum = CELL_SIZE + halfSize sein (erste begehbare Zelle = Index 1)
        float halfSize = GameGrid.CELL_SIZE * 0.35f;
        float minBound = GameGrid.CELL_SIZE + halfSize;
        float maxBoundX = grid.PixelWidth - GameGrid.CELL_SIZE - halfSize;
        float maxBoundY = grid.PixelHeight - GameGrid.CELL_SIZE - halfSize;
        X = Math.Clamp(X, minBound, maxBoundX);
        Y = Math.Clamp(Y, minBound, maxBoundY);
    }

    /// <summary>
    /// Starker Corner-Assist: Wenn der Spieler knapp an einer Ecke hängt,
    /// wird er automatisch um die Ecke geschoben. Prüft ob eine Nachbarzelle
    /// frei ist und nudgt den Spieler dorthin.
    /// </summary>
    private void TryCornerAssist(float dx, float dy, GameGrid grid)
    {
        float cellCenter;
        float offset;

        if (dx != 0)
        {
            // Horizontal blockiert → prüfe ob Y-Offset zum nächsten freien Gang reicht
            cellCenter = MathF.Floor(Y / GameGrid.CELL_SIZE) * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            offset = Y - cellCenter;

            // Prüfe ob Ausrichten nach oben oder unten die Blockade löst
            float targetX = X + dx;
            float nudgeSpeed = 4.0f; // Konstanter Nudge (unabhängig von Speed-Level)

            if (MathF.Abs(offset) < GameGrid.CELL_SIZE * 0.5f)
            {
                // Versuche zum Zellzentrum zu schieben
                float nudge = MathF.Min(MathF.Abs(offset), nudgeSpeed);
                float nudgedY = Y - MathF.Sign(offset) * nudge;
                if (CanMoveTo(targetX, nudgedY, grid))
                {
                    Y = nudgedY;
                    X = targetX;
                    return;
                }
            }

            // Alternativ: In beide Richtungen probieren
            if (CanMoveTo(targetX, Y - nudgeSpeed, grid))
                Y -= nudgeSpeed;
            else if (CanMoveTo(targetX, Y + nudgeSpeed, grid))
                Y += nudgeSpeed;
        }
        else if (dy != 0)
        {
            // Vertikal blockiert → prüfe ob X-Offset zum nächsten freien Gang reicht
            cellCenter = MathF.Floor(X / GameGrid.CELL_SIZE) * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            offset = X - cellCenter;

            float targetY = Y + dy;
            float nudgeSpeed = 4.0f; // Konstanter Nudge (unabhängig von Speed-Level)

            if (MathF.Abs(offset) < GameGrid.CELL_SIZE * 0.5f)
            {
                float nudge = MathF.Min(MathF.Abs(offset), nudgeSpeed);
                float nudgedX = X - MathF.Sign(offset) * nudge;
                if (CanMoveTo(nudgedX, targetY, grid))
                {
                    X = nudgedX;
                    Y = targetY;
                    return;
                }
            }

            if (CanMoveTo(X - nudgeSpeed, targetY, grid))
                X -= nudgeSpeed;
            else if (CanMoveTo(X + nudgeSpeed, targetY, grid))
                X += nudgeSpeed;
        }
    }

    private bool CanMoveTo(float newX, float newY, GameGrid grid)
    {
        float halfSize = GameGrid.CELL_SIZE * 0.35f;
        return CollisionHelper.CanMoveToPlayer(newX, newY, halfSize, grid, HasWallpass, HasBombpass);
    }

    /// <summary>
    /// Collect a power-up
    /// </summary>
    public void CollectPowerUp(PowerUp powerUp)
    {
        Score += powerUp.Type.GetPoints();

        switch (powerUp.Type)
        {
            case PowerUpType.BombUp:
                MaxBombs = Math.Min(MaxBombs + 1, MAX_BOMB_COUNT);
                break;

            case PowerUpType.Fire:
                FireRange = Math.Min(FireRange + 1, MAX_FIRE_RANGE);
                break;

            case PowerUpType.Speed:
                SpeedLevel = Math.Min(SpeedLevel + 1, MAX_SPEED_LEVEL);
                break;

            case PowerUpType.Wallpass:
                HasWallpass = true;
                break;

            case PowerUpType.Detonator:
                HasDetonator = true;
                break;

            case PowerUpType.Bombpass:
                HasBombpass = true;
                break;

            case PowerUpType.Flamepass:
                HasFlamepass = true;
                break;

            case PowerUpType.Mystery:
                ActivateInvincibility(PowerUpType.Mystery.GetDuration());
                break;

            case PowerUpType.Kick:
                HasKick = true;
                break;

            case PowerUpType.LineBomb:
                HasLineBomb = true;
                break;

            case PowerUpType.PowerBomb:
                HasPowerBomb = true;
                break;

            case PowerUpType.Skull:
                ActivateCurse();
                break;
        }
    }

    /// <summary>
    /// Zufälligen Fluch aktivieren (Skull-PowerUp)
    /// </summary>
    public void ActivateCurse()
    {
        var curses = new[] { CurseType.Diarrhea, CurseType.Slow, CurseType.Constipation, CurseType.ReverseControls };
        ActiveCurse = curses[Random.Shared.Next(curses.Length)];
        CurseTimer = CURSE_DURATION;

        if (ActiveCurse == CurseType.Diarrhea)
            DiarrheaTimer = 0.5f;
    }

    /// <summary>
    /// Activate invincibility for specified duration
    /// </summary>
    public void ActivateInvincibility(float duration)
    {
        IsInvincible = true;
        InvincibilityTimer = duration;
    }

    /// <summary>
    /// Kill the player (starts death animation)
    /// </summary>
    public void Kill()
    {
        // Flamepass wird NICHT hier geprüft - nur in der Explosions-Kollision (GameEngine.Collision.cs)
        // Kill() wird auch bei Gegner-Kontakt aufgerufen, wo Flamepass nicht schützen soll
        if (IsDying || IsInvincible || HasSpawnProtection)
            return;

        IsDying = true;
        DeathTimer = 0;
        AnimationFrame = 0;
        AnimationTimer = 0;
        IsActive = false;
    }

    /// <summary>
    /// Reset player state for respawn (loses non-permanent power-ups)
    /// </summary>
    public void Respawn(float x, float y)
    {
        X = x;
        Y = y;
        IsDying = false;
        DeathTimer = 0;
        IsActive = true;
        IsMarkedForRemoval = false;
        FacingDirection = Direction.Down;
        MovementDirection = Direction.None;
        AnimationFrame = 0;
        AnimationTimer = 0;
        ActiveBombs = 0;

        // Lose non-permanent power-ups
        SpeedLevel = 0;
        HasWallpass = false;
        HasDetonator = false;
        HasBombpass = false;
        HasFlamepass = false;
        HasKick = false;
        HasLineBomb = false;
        HasPowerBomb = false;
        HasShield = false;
        IsInvincible = false;
        InvincibilityTimer = 0;
        ActiveCurse = CurseType.None;
        CurseTimer = 0;
        ActiveSpecialBombType = BombType.Normal;
        SpecialBombCount = 0;
        ActiveCardSlot = -1;
        // HasIceBomb/HasFireBomb/HasStickyBomb bleiben (permanent via Shop)
        // EquippedCards bleiben (werden pro Level via ApplyUpgrades geladen)

        // Grant spawn protection
        HasSpawnProtection = true;
        SpawnProtectionTimer = SPAWN_PROTECTION_DURATION;

        // Keep permanent power-ups (MaxBombs, FireRange)
    }

    /// <summary>
    /// Reset all stats for new game
    /// </summary>
    public void ResetForNewGame()
    {
        MaxBombs = 1;
        FireRange = 1;
        SpeedLevel = 0;
        HasWallpass = false;
        HasDetonator = false;
        HasBombpass = false;
        HasFlamepass = false;
        HasKick = false;
        HasLineBomb = false;
        HasPowerBomb = false;
        HasShield = false;
        HasIceBomb = false;
        HasFireBomb = false;
        HasStickyBomb = false;
        ActiveSpecialBombType = BombType.Normal;
        SpecialBombCount = 0;
        EquippedCards.Clear();
        ActiveCardSlot = -1;
        IsInvincible = false;
        InvincibilityTimer = 0;
        HasSpawnProtection = false;
        SpawnProtectionTimer = 0;
        ActiveCurse = CurseType.None;
        CurseTimer = 0;
        DiarrheaTimer = 0;
        Lives = 3;
        Score = 0;
        ActiveBombs = 0;
        IsDying = false;
        DeathTimer = 0;
        IsActive = true;
        IsMarkedForRemoval = false;
        FacingDirection = Direction.Down;
        MovementDirection = Direction.None;
        AnimationFrame = 0;
        AnimationTimer = 0;
    }

    /// <summary>
    /// Check if player can place a bomb
    /// </summary>
    public bool CanPlaceBomb()
    {
        if (ActiveCurse == CurseType.Constipation) return false;
        return !IsDying && ActiveBombs < MaxBombs;
    }
}
