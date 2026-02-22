using BomberBlast.Models.Grid;

namespace BomberBlast.Models.Entities;

/// <summary>
/// Boss-Gegner Typ (1 pro Welt-Paar)
/// </summary>
public enum BossType
{
    /// <summary>Welt 1-2 (L10/L20): Wirft Blöcke auf den Spieler</summary>
    StoneGolem,
    /// <summary>Welt 3-4 (L30/L40): Friert eine Reihe ein</summary>
    IceDragon,
    /// <summary>Welt 5-6 (L50/L60): Halber Boden wird Lava</summary>
    FireDemon,
    /// <summary>Welt 7-8 (L70/L80): Teleportiert + Schattenklon</summary>
    ShadowMaster,
    /// <summary>Welt 9-10 (L90/L100): Alle Angriffe abwechselnd</summary>
    FinalBoss
}

/// <summary>
/// Boss-Gegner: Größer, mehr HP, Spezial-Angriffe, Enrage-Phase.
/// Erbt von Enemy für Kompatibilität mit der _enemies Liste.
/// </summary>
public class BossEnemy : Enemy
{
    // ═══════════════════════════════════════════════════════════════════════
    // BOSS-EIGENSCHAFTEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Welcher Boss-Typ</summary>
    public BossType BossKind { get; }

    /// <summary>Boss-Größe in Zellen (2 oder 3)</summary>
    public int BossSize { get; }

    /// <summary>Maximale Trefferpunkte (für HP-Balken Berechnung)</summary>
    public int MaxHitPoints { get; }

    /// <summary>Ob Boss in Enrage-Phase ist (unter 50% HP → schneller, aggressiver)</summary>
    public bool IsEnraged { get; private set; }

    // ═══════════════════════════════════════════════════════════════════════
    // SPEZIAL-ANGRIFF
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Timer bis zum nächsten Spezial-Angriff (Countdown)</summary>
    public float SpecialAttackTimer { get; set; }

    /// <summary>Cooldown zwischen Angriffen (15-20s, kürzer bei Enrage)</summary>
    public float SpecialAttackCooldown { get; }

    /// <summary>Telegraph-Timer: 2s Warnung bevor der Angriff trifft</summary>
    public float TelegraphTimer { get; set; }

    /// <summary>Ob gerade ein Angriff telegraphiert wird</summary>
    public bool IsTelegraphing => TelegraphTimer > 0;

    /// <summary>Ob der Spezial-Angriff gerade ausgeführt wird</summary>
    public bool IsAttacking { get; set; }

    /// <summary>Dauer des aktiven Angriffs-Effekts</summary>
    public float AttackDuration { get; set; }

    /// <summary>Betroffene Zellen des Spezial-Angriffs (für Warnung + Schaden)</summary>
    public List<(int x, int y)> AttackTargetCells { get; set; } = new();

    /// <summary>Index des aktuellen Angriffs (für FinalBoss Rotation)</summary>
    public int AttackRotationIndex { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // TELEGRAPH-DAUER
    // ═══════════════════════════════════════════════════════════════════════
    private const float TELEGRAPH_DURATION = 2.0f;
    private const float ATTACK_EFFECT_DURATION = 1.5f;

    // ═══════════════════════════════════════════════════════════════════════
    // BOUNDING BOX (Größer als normale Gegner)
    // ═══════════════════════════════════════════════════════════════════════

    public override (float left, float top, float right, float bottom) BoundingBox
    {
        get
        {
            // Boss-Zentrum ist in der Mitte des BossSize-Bereichs
            float halfSize = GameGrid.CELL_SIZE * BossSize * 0.4f;
            return (X - halfSize, Y - halfSize, X + halfSize, Y + halfSize);
        }
    }

    /// <summary>
    /// Prüft ob eine Grid-Zelle vom Boss belegt wird (für Multi-Cell Kollision)
    /// </summary>
    public bool OccupiesCell(int cellX, int cellY)
    {
        // Boss-Position ist die obere linke Ecke des Bereichs
        int baseX = GridX;
        int baseY = GridY;
        return cellX >= baseX && cellX < baseX + BossSize &&
               cellY >= baseY && cellY < baseY + BossSize;
    }

    /// <summary>
    /// Gibt alle Grid-Zellen zurück die der Boss belegt
    /// </summary>
    public IEnumerable<(int x, int y)> GetOccupiedCells()
    {
        int baseX = GridX;
        int baseY = GridY;
        for (int dy = 0; dy < BossSize; dy++)
            for (int dx = 0; dx < BossSize; dx++)
                yield return (baseX + dx, baseY + dy);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KONSTRUKTOR
    // ═══════════════════════════════════════════════════════════════════════

    public BossEnemy(float x, float y, BossType bossType) : base(x, y, EnemyType.Pontan)
    {
        BossKind = bossType;

        // Boss-Stats basierend auf Typ
        (BossSize, int hp, float cooldown, float speed) = bossType switch
        {
            BossType.StoneGolem => (2, 3, 18f, 30f),
            BossType.IceDragon => (2, 4, 16f, 35f),
            BossType.FireDemon => (2, 5, 15f, 40f),
            BossType.ShadowMaster => (2, 6, 14f, 45f),
            BossType.FinalBoss => (3, 8, 12f, 35f),
            _ => (2, 3, 18f, 30f)
        };

        MaxHitPoints = hp;
        HitPoints = hp;
        SpecialAttackCooldown = cooldown;
        SpecialAttackTimer = cooldown; // Erster Angriff nach vollem Cooldown

        // Speed überschreiben (Enemy-Konstruktor setzt EnemyType.Pontan-Speed)
        // Wir verwenden Reflection-freie Lösung: Speed wird im Enemy-Konstruktor aus EnemyType gelesen
        // → Boss-Speed setzen wir nicht über den Konstruktor, sondern der Boss bewegt sich
        //   mit seiner eigenen Update-Logik
    }

    /// <summary>
    /// Boss-spezifische Bewegungsgeschwindigkeit (statt EnemyType-Speed)
    /// </summary>
    public float BossSpeed => BossKind switch
    {
        BossType.StoneGolem => IsEnraged ? 40f : 30f,
        BossType.IceDragon => IsEnraged ? 45f : 35f,
        BossType.FireDemon => IsEnraged ? 50f : 40f,
        BossType.ShadowMaster => IsEnraged ? 55f : 45f,
        BossType.FinalBoss => IsEnraged ? 45f : 35f,
        _ => 30f
    };

    /// <summary>
    /// Boss-Punkte beim Besiegen
    /// </summary>
    public int BossPoints => BossKind switch
    {
        BossType.StoneGolem => 10000,
        BossType.IceDragon => 15000,
        BossType.FireDemon => 20000,
        BossType.ShadowMaster => 25000,
        BossType.FinalBoss => 50000,
        _ => 10000
    };

    // ═══════════════════════════════════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════════════════════════════════

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        if (IsDying) return;

        // Enrage bei 50% HP
        if (!IsEnraged && HitPoints <= MaxHitPoints / 2)
        {
            IsEnraged = true;
        }

        // Spezial-Angriff Timer
        if (!IsAttacking && !IsTelegraphing)
        {
            SpecialAttackTimer -= deltaTime;
            if (SpecialAttackTimer <= 0)
            {
                // Telegraph starten (2s Warnung)
                TelegraphTimer = TELEGRAPH_DURATION;
            }
        }

        // Telegraph Countdown
        if (IsTelegraphing && !IsAttacking)
        {
            TelegraphTimer -= deltaTime;
            if (TelegraphTimer <= 0)
            {
                TelegraphTimer = 0;
                IsAttacking = true;
                AttackDuration = ATTACK_EFFECT_DURATION;
            }
        }

        // Aktiver Angriff
        if (IsAttacking)
        {
            AttackDuration -= deltaTime;
            if (AttackDuration <= 0)
            {
                // Angriff beendet, Cooldown starten
                IsAttacking = false;
                AttackDuration = 0;
                AttackTargetCells.Clear();
                float cd = IsEnraged ? SpecialAttackCooldown * 0.6f : SpecialAttackCooldown;
                SpecialAttackTimer = cd;

                // FinalBoss: Nächsten Angriff rotieren
                if (BossKind == BossType.FinalBoss)
                    AttackRotationIndex = (AttackRotationIndex + 1) % 4;
            }
        }
    }

    /// <summary>
    /// Boss-Bewegung mit größerer Kollisions-Box
    /// </summary>
    public void MoveBoss(float deltaTime, GameGrid grid)
    {
        if (IsDying || MovementDirection == Direction.None)
            return;

        FacingDirection = MovementDirection;

        float speed = BossSpeed;
        float dx = MovementDirection.GetDeltaX() * speed * deltaTime;
        float dy = MovementDirection.GetDeltaY() * speed * deltaTime;

        float newX = X + dx;
        float newY = Y + dy;

        if (CanBossMoveTo(newX, newY, grid))
        {
            X = newX;
            Y = newY;
        }
        else if (dx != 0 && CanBossMoveTo(newX, Y, grid))
        {
            X = newX;
        }
        else if (dy != 0 && CanBossMoveTo(X, newY, grid))
        {
            Y = newY;
        }
        else
        {
            MovementDirection = Direction.None;
        }

        // Grid-Bounds (Boss braucht mehr Platz)
        float margin = GameGrid.CELL_SIZE * 0.5f;
        float maxX = (grid.Width - BossSize) * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE * 0.5f;
        float maxY = (grid.Height - BossSize) * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE * 0.5f;
        X = Math.Clamp(X, margin, maxX);
        Y = Math.Clamp(Y, margin, maxY);
    }

    /// <summary>
    /// Prüft ob der Boss sich an die neue Position bewegen kann (alle besetzten Zellen)
    /// </summary>
    private bool CanBossMoveTo(float newX, float newY, GameGrid grid)
    {
        // Zellen die der Boss an der neuen Position belegen würde
        int baseGridX = (int)MathF.Floor(newX / GameGrid.CELL_SIZE);
        int baseGridY = (int)MathF.Floor(newY / GameGrid.CELL_SIZE);

        for (int dy = 0; dy < BossSize; dy++)
        {
            for (int dx = 0; dx < BossSize; dx++)
            {
                int cx = baseGridX + dx;
                int cy = baseGridY + dy;

                var cell = grid.TryGetCell(cx, cy);
                if (cell == null) return false;

                // Wände und Blöcke blockieren
                if (cell.Type == CellType.Wall || cell.Type == CellType.Block)
                    return false;

                // Bomben blockieren
                if (cell.Bomb != null)
                    return false;
            }
        }

        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FACTORY
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Boss an Grid-Position erstellen (Mitte des BossSize-Bereichs)
    /// </summary>
    public static BossEnemy CreateAtGrid(int gridX, int gridY, BossType bossType)
    {
        // Boss-Zentrum: Mitte des Bereichs
        int size = bossType switch
        {
            BossType.FinalBoss => 3,
            _ => 2
        };
        float x = gridX * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE * size / 2f;
        float y = gridY * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE * size / 2f;
        return new BossEnemy(x, y, bossType);
    }
}
