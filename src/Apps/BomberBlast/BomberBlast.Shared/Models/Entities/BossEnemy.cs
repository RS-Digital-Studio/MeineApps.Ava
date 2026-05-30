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

    /// <summary>
    ///.1 : Boss-Modifier-System. 8 Modifier × 5 Bosse = 40 Variationen.
    /// Wird beim Spawn random zugewiesen (ab Welt 5+, 30% Chance). Modifier-Effekte werden
    /// von BossEnemy.Update + EnemyAI ausgewertet (z.B. Healing regeneriert HP, Shielded blockt
    /// 1 Hit pro Cooldown). Phase-2-Variante via Phase-Tracking erlaubt verschiedene Attack-Patterns.
    /// </summary>
    public BossModifier Modifier { get; set; } = BossModifier.None;

    /// <summary>
    ///.1 : Aktuelle Boss-Phase (1=Default, 2=Enraged-Mode-Variant).
    ///  wird beim Enrage-Threshold aktiv und schaltet Attack-Pattern um.
    /// </summary>
    public int CurrentPhase { get; private set; } = 1;

    /// <summary>
    /// Welle 1 v2.0.58 : Mini-Boss-Flag — Boss spawnt mit 50% HP + 50% Punkte.
    /// Wird auf L7/L17/.../L97 als Mid-World-Encounter eingesetzt.
    /// </summary>
    public bool IsMiniBoss { get; init; }

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

    /// <summary>
    ///.4 : Anticipation-Scale fuer Big-Attacks.
    /// In den letzten 120ms vor Attack-Trigger zieht sich der Boss-Sprite zusammen
    /// (0.85x scale) — Hades-Pattern fuer "Wind-Up". 1.0 wenn nicht in Wind-Up-Phase.
    /// Renderer wendet via canvas.Scale auf Boss-Sprite an.
    /// </summary>
    public float AnticipationScale
    {
        get
        {
            if (TelegraphTimer <= 0 || TelegraphTimer > 0.12f) return 1f;
            // 120ms → 0ms: linear von 1.0 auf 0.85 → wieder zurueck (Sin-Pop)
            float t = TelegraphTimer / 0.12f;       // 1 → 0
            float pop = MathF.Sin((1f - t) * MathF.PI);  // 0 → 1 → 0
            return 1f - pop * 0.15f;                // 1.0 → 0.85 → 1.0
        }
    }

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
    /// Bosse haben bereits eine custom verkleinerte BoundingBox (0.4x BossSize).
    /// Kein zusätzlicher Shrink — sonst wäre die Hitbox zu klein um den Spieler zu treffen.
    /// </summary>
    protected override float HitboxScale => 1.0f;

    /// <summary>
    /// Linke obere Grid-Zelle des belegten BossSize×BossSize-Bereichs.
    /// <para>
    /// WICHTIG: X/Y sind die MITTE des Boss (Render-Konvention, siehe <see cref="CreateAtGrid"/>:
    /// <c>x = gridX*CELL + CELL*size/2</c>). Die belegten Zellen werden daher aus der LINKEN KANTE
    /// (<c>X - BossSize*CELL/2</c>) abgeleitet, NICHT aus <c>GridX = floor(X/CELL)</c> (= Zelle der
    /// Mitte). Sonst lägen Kollisions-/Explosions-Treffer-/Movement-Zellen gegenüber dem gezeichneten
    /// Sprite um ~1 Zelle verschoben. Eine einzige Quelle für alle Occupancy-Konsumenten.
    /// </para>
    /// </summary>
    public int OccupancyBaseX => (int)MathF.Floor((X - BossSize * GameGrid.CELL_SIZE / 2f) / GameGrid.CELL_SIZE);

    /// <summary>Obere Grid-Zeile des belegten Bereichs — siehe <see cref="OccupancyBaseX"/>.</summary>
    public int OccupancyBaseY => (int)MathF.Floor((Y - BossSize * GameGrid.CELL_SIZE / 2f) / GameGrid.CELL_SIZE);

    /// <summary>
    /// Prüft ob eine Grid-Zelle vom Boss belegt wird (für Multi-Cell Kollision)
    /// </summary>
    public bool OccupiesCell(int cellX, int cellY)
    {
        int baseX = OccupancyBaseX;
        int baseY = OccupancyBaseY;
        return cellX >= baseX && cellX < baseX + BossSize &&
               cellY >= baseY && cellY < baseY + BossSize;
    }

    /// <summary>
    /// Gibt alle Grid-Zellen zurück die der Boss belegt
    /// </summary>
    public IEnumerable<(int x, int y)> GetOccupiedCells()
    {
        int baseX = OccupancyBaseX;
        int baseY = OccupancyBaseY;
        for (int dy = 0; dy < BossSize; dy++)
            for (int dx = 0; dx < BossSize; dx++)
                yield return (baseX + dx, baseY + dy);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KONSTRUKTOR
    // ═══════════════════════════════════════════════════════════════════════

    public BossEnemy(float x, float y, BossType bossType, bool miniBoss = false) : base(x, y, EnemyType.Pontan)
    {
        BossKind = bossType;
        IsMiniBoss = miniBoss;

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

        // Welle 1 v2.0.58: Mini-Boss hat halbe HP (mindestens 1).
        if (miniBoss)
        {
            hp = Math.Max(1, hp / 2);
        }

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
    /// Boss-Punkte beim Besiegen. Mini-Bosse geben 50%.
    /// </summary>
    public int BossPoints
    {
        get
        {
            int basePoints = BossKind switch
            {
                BossType.StoneGolem => 10000,
                BossType.IceDragon => 15000,
                BossType.FireDemon => 20000,
                BossType.ShadowMaster => 25000,
                BossType.FinalBoss => 50000,
                _ => 10000
            };
            return IsMiniBoss ? basePoints / 2 : basePoints;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════════════════════════════════

    //.1 : Boss-Modifier-Effekte
    // v2.0.60 (B-A16): HEALING_REGEN_PER_SECOND von 5f→2.5f. Bei 5f × 12-18s Cooldown
    // regenerierte ein Boss mit MaxHP 3-8 effektiv 60-90 HP pro Cycle = unbesiegbar.
    // Plus: float-Akkumulator-Fix (vorher: (int)Math.Round(5*0.016)=0 → de-facto kein Heal).
    // Plus: HEAL_HP_CAP_FACTOR cappt Regen auf 50% MaxHP — Boss kann sich in Enrage nicht
    // mehr selbst aus der Sterbephase retten.
    /// <summary>Healing-Modifier: HP/s Regeneration im Out-of-Combat-State.</summary>
    private const float HEALING_REGEN_PER_SECOND = 2.5f;
    /// <summary>Healing-Modifier: Regen-Cap als Bruchteil von MaxHitPoints (verhindert Enrage-Selbstrettung).</summary>
    private const float HEAL_HP_CAP_FACTOR = 0.5f;
    private float _healingAccumulator;
    /// <summary>Summoner-Modifier: Spawn-Cooldown fuer Mini-Enemies.</summary>
    private const float SUMMONER_SPAWN_COOLDOWN = 8f;
    private float _summonerSpawnTimer;
    /// <summary>Shielded-Modifier: HitPoints werden um 1 Schicht erhoeht solange Schild aktiv.</summary>
    public bool HasShield { get; private set; }
    private const float SHIELD_RECHARGE_COOLDOWN = 15f;
    private float _shieldRechargeTimer;
    /// <summary>Burning-Modifier: Lava-Spur-Positionen mit Lebenszeit (3s).</summary>
    public List<(int x, int y, float ttl)> BurningTrail { get; } = new();
    private float _burningTrailEmitTimer;

    /// <summary>Wird vom GameEngine angefordert: True wenn Boss einen Mini-Enemy spawnen will.</summary>
    public bool TryConsumeSummonRequest()
    {
        if (Modifier != BossModifier.Summoner) return false;
        if (_summonerSpawnTimer > 0) return false;
        _summonerSpawnTimer = SUMMONER_SPAWN_COOLDOWN;
        return true;
    }

    /// <summary>Wird beim Spawn aufgerufen: Initialer Shield wenn Shielded-Modifier aktiv.</summary>
    public void InitializeShieldIfNeeded()
    {
        if (Modifier == BossModifier.Shielded) HasShield = true;
    }

    /// <summary>Wird bei Hit aufgerufen — Shielded absorbiert 1 Hit pro Cooldown.</summary>
    public bool ConsumeShieldHit()
    {
        if (!HasShield) return false;
        HasShield = false;
        _shieldRechargeTimer = SHIELD_RECHARGE_COOLDOWN;
        return true;
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        if (IsDying) return;

        // Enrage bei 50% HP
        if (!IsEnraged && HitPoints <= MaxHitPoints / 2)
        {
            IsEnraged = true;
            //.1 : Phase-Wechsel beim Enrage — Boss bekommt .
            // Renderer/AI koennen darauf reagieren (z.B. anderes Attack-Pattern).
            CurrentPhase = 2;
        }

        //.1 : Modifier-Effekte pro Frame anwenden.
        ApplyModifierEffects(deltaTime);

        // Spezial-Angriff Timer
        if (!IsAttacking && !IsTelegraphing)
        {
            SpecialAttackTimer -= deltaTime;
            if (SpecialAttackTimer <= 0)
            {
                // v2.0.60 (B-A17): Berserk-Modifier verkürzt Telegraph, Mindestzeit 1.5s.
                // Vorher: TELEGRAPH_DURATION * 0.5f = 1.0s — unter menschlicher Reaktionszeit
                // für Bomb-Place + Move (~200-300ms Reaktion + ~700ms Bomb-Set + Move).
                // 1.5s sichert Reaktionsfenster. In Enrage zusätzlich ×0.85 = 1.275s (vorher 0.6s).
                TelegraphTimer = Modifier == BossModifier.Berserk
                    ? MathF.Max(1.5f, TELEGRAPH_DURATION * 0.75f)
                    : TELEGRAPH_DURATION;
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
                // Frenzy-Modifier halbiert Cooldown zusaetzlich in Enrage-Phase.
                if (Modifier == BossModifier.Frenzy && IsEnraged) cd *= 0.5f;
                SpecialAttackTimer = cd;

                // FinalBoss: Nächsten Angriff rotieren
                if (BossKind == BossType.FinalBoss)
                    AttackRotationIndex = (AttackRotationIndex + 1) % 4;
            }
        }
    }

    /// <summary>.1 : Boss-Modifier-Effekte pro Frame.</summary>
    private void ApplyModifierEffects(float deltaTime)
    {
        switch (Modifier)
        {
            case BossModifier.Healing:
                // v2.0.60 (B-A16): Float-Akkumulator statt direkter int-Round-Addition.
                // Vorher: (int)Math.Round(2.5f * 0.016f) = 0 → Heilung tat nichts.
                // Jetzt: deltaTime-Anteile akkumulieren, ganzzahlige HP-Wende bei ≥1.
                // Cap bei MaxHitPoints/2 verhindert Selbst-Rettung im Enrage.
                if (!IsAttacking && !IsTelegraphing)
                {
                    int heal_cap = Math.Max(1, (int)MathF.Floor(MaxHitPoints * HEAL_HP_CAP_FACTOR));
                    if (HitPoints < heal_cap)
                    {
                        _healingAccumulator += HEALING_REGEN_PER_SECOND * deltaTime;
                        if (_healingAccumulator >= 1f)
                        {
                            int delta = (int)_healingAccumulator;
                            _healingAccumulator -= delta;
                            HitPoints = Math.Min(heal_cap, HitPoints + delta);
                        }
                    }
                    else
                    {
                        // HP über Cap → Akkumulator-Reset, damit kein "Pop" nach Damage entsteht.
                        _healingAccumulator = 0f;
                    }
                }
                break;

            case BossModifier.Summoner:
                _summonerSpawnTimer -= deltaTime;
                if (_summonerSpawnTimer < 0) _summonerSpawnTimer = 0;
                break;

            case BossModifier.Shielded:
                _shieldRechargeTimer -= deltaTime;
                if (_shieldRechargeTimer <= 0 && !HasShield)
                {
                    HasShield = true;
                    _shieldRechargeTimer = 0;
                }
                break;

            case BossModifier.Burning:
                _burningTrailEmitTimer -= deltaTime;
                if (_burningTrailEmitTimer <= 0 && MovementDirection != Direction.None)
                {
                    BurningTrail.Add((GridX, GridY, 3.0f));  // 3s TTL
                    _burningTrailEmitTimer = 0.3f;            // alle 0.3s emit
                }
                break;
        }

        // Burning-Trail TTL-Update + Cleanup (egal welcher Modifier — sicher gegen Stale-Trail)
        for (int i = BurningTrail.Count - 1; i >= 0; i--)
        {
            var entry = BurningTrail[i];
            entry.ttl -= deltaTime;
            if (entry.ttl <= 0)
                BurningTrail.RemoveAt(i);
            else
                BurningTrail[i] = entry;
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

        //.1 : Fast-Modifier gibt +25% Bewegungsgeschwindigkeit.
        float speed = BossSpeed * (Modifier == BossModifier.Fast ? 1.25f : 1f);
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

        // Grid-Bounds (Boss braucht mehr Platz). X/Y = Mitte → min = Mitte bei top-left 0,
        // max = Mitte bei top-left (Dim - BossSize). Konsistent mit der Occupancy-Konvention;
        // CanBossMoveTo verhindert den Wand-Eintritt ohnehin früher (dies ist nur ein Backstop).
        float marginX = BossSize * GameGrid.CELL_SIZE / 2f;
        float maxX = (grid.Width - BossSize) * GameGrid.CELL_SIZE + BossSize * GameGrid.CELL_SIZE / 2f;
        float maxY = (grid.Height - BossSize) * GameGrid.CELL_SIZE + BossSize * GameGrid.CELL_SIZE / 2f;
        X = Math.Clamp(X, marginX, maxX);
        Y = Math.Clamp(Y, marginX, maxY);
    }

    /// <summary>
    /// Prüft ob der Boss sich an die neue Position bewegen kann (alle besetzten Zellen)
    /// </summary>
    private bool CanBossMoveTo(float newX, float newY, GameGrid grid)
    {
        // Zellen die der Boss an der neuen Position belegen würde. newX/newY ist die MITTE —
        // top-left aus der linken Kante ableiten (konsistent mit OccupancyBaseX/Y).
        int baseGridX = (int)MathF.Floor((newX - BossSize * GameGrid.CELL_SIZE / 2f) / GameGrid.CELL_SIZE);
        int baseGridY = (int)MathF.Floor((newY - BossSize * GameGrid.CELL_SIZE / 2f) / GameGrid.CELL_SIZE);

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
    /// Boss an Grid-Position erstellen (Mitte des BossSize-Bereichs).
    /// Welle 1 v2.0.58 : Optional miniBoss-Flag fuer Mid-World-Encounter (halbe HP/Punkte).
    /// </summary>
    public static BossEnemy CreateAtGrid(int gridX, int gridY, BossType bossType, bool miniBoss = false)
    {
        // Boss-Zentrum: Mitte des Bereichs
        int size = bossType switch
        {
            BossType.FinalBoss => 3,
            _ => 2
        };
        float x = gridX * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE * size / 2f;
        float y = gridY * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE * size / 2f;
        return new BossEnemy(x, y, bossType, miniBoss);
    }
}
