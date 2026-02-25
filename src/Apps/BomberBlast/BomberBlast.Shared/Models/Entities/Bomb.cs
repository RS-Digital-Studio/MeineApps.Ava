namespace BomberBlast.Models.Entities;

/// <summary>
/// Bomben-Typ (Normal oder Spezial)
/// </summary>
public enum BombType
{
    Normal,
    /// <summary>Friert Gegner für 3s ein (Slow + blau)</summary>
    Ice,
    /// <summary>+2 Extra-Reichweite, Lava-Nachwirkung (3s Schaden)</summary>
    Fire,
    /// <summary>Klebt an Wänden/Gegnern, 5s Timer</summary>
    Sticky,

    // === Neue Bomben-Typen (Phase 1) ===

    /// <summary>3x3 Nebelwolke, Gegner-AI läuft 4s zufällig (Rare)</summary>
    Smoke,
    /// <summary>Blitz springt zu 3 nächsten Gegnern, ignoriert Wände (Rare)</summary>
    Lightning,
    /// <summary>Zieht alle Gegner im 3-Zellen-Radius 1 Zelle zum Zentrum (Rare)</summary>
    Gravity,
    /// <summary>Gift-Zellen (3s), Gegner verlieren HP beim Betreten (Rare)</summary>
    Poison,
    /// <summary>Alles im Radius 5s auf 50% verlangsamt inkl. Bomben-Timer (Epic)</summary>
    TimeWarp,
    /// <summary>Explosion kopiert sich in Gegenrichtung, doppelte Reichweite (Epic)</summary>
    Mirror,
    /// <summary>Spiralförmige Explosion, trifft mehr Zellen als linear (Epic)</summary>
    Vortex,
    /// <summary>Explosion durchdringt 1 unzerstörbare Wand (Epic)</summary>
    Phantom,
    /// <summary>360-Grad Explosion (ALLE Zellen im Range), lässt PowerUp fallen (Legendary)</summary>
    Nova,
    /// <summary>Schwarzes Loch, saugt Gegner 3s ein, dann Explosion (Legendary)</summary>
    BlackHole
}

/// <summary>
/// Bomb entity that explodes after a timer
/// </summary>
public class Bomb : Entity
{
    /// <summary>Default fuse time in seconds (original NES)</summary>
    public const float DEFAULT_FUSE_TIME = 3.0f;

    /// <summary>Owner player (for bomb count tracking)</summary>
    public Player Owner { get; }

    /// <summary>Explosion range in cells</summary>
    public int Range { get; }

    /// <summary>Time remaining until explosion</summary>
    public float FuseTimer { get; private set; }

    /// <summary>Whether bomb is about to explode (for visual warning)</summary>
    public bool IsAboutToExplode => FuseTimer <= 0.5f;

    /// <summary>Whether player is still standing on bomb (just placed)</summary>
    public bool PlayerOnTop { get; set; } = true;

    /// <summary>Whether bomb is manual detonation (Detonator power-up)</summary>
    public bool IsManualDetonation { get; }

    /// <summary>Whether bomb should explode on next update</summary>
    public bool ShouldExplode { get; set; }

    /// <summary>Whether bomb has already exploded</summary>
    public bool HasExploded { get; private set; }

    // Kick/Slide-Mechanik
    /// <summary>Ob die Bombe gerade gleitet (durch Kick)</summary>
    public bool IsSliding { get; set; }
    /// <summary>Gleitrichtung</summary>
    public Direction SlideDirection { get; set; } = Direction.None;
    /// <summary>Gleitgeschwindigkeit (Pixel/Sekunde)</summary>
    public const float SLIDE_SPEED = 160f;
    /// <summary>Belegte Bomben-Slots (1 fuer normal, MaxBombs fuer PowerBomb)</summary>
    public int SlotsConsumed { get; set; } = 1;
    /// <summary>Kettenreaktions-Tiefe (0 = direkt platziert, 1+ = durch andere Bombe ausgelöst)</summary>
    public int ChainDepth { get; set; }

    /// <summary>Spezial-Bomben-Typ (Normal = Standard)</summary>
    public BombType Type { get; set; } = BombType.Normal;

    public override float AnimationSpeed => 4f;

    public Bomb(float x, float y, Player owner, int range, bool isManual = false) : base(x, y)
    {
        Owner = owner;
        Range = range;
        FuseTimer = DEFAULT_FUSE_TIME;
        IsManualDetonation = isManual;
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        if (HasExploded)
            return;

        // Manual detonation bombs don't count down
        if (!IsManualDetonation)
        {
            FuseTimer -= deltaTime;
            if (FuseTimer <= 0)
            {
                ShouldExplode = true;
            }
        }
    }

    /// <summary>
    /// Trigger explosion (called by game engine)
    /// </summary>
    public void Explode()
    {
        if (HasExploded)
            return;

        HasExploded = true;
        IsActive = false;
        IsMarkedForRemoval = true;

        // Decrease owner's active bomb count
        if (Owner != null)
        {
            Owner.ActiveBombs = Math.Max(0, Owner.ActiveBombs - SlotsConsumed);
        }
    }

    /// <summary>
    /// Trigger explosion from chain reaction
    /// </summary>
    public void TriggerChainReaction()
    {
        ShouldExplode = true;
    }

    /// <summary>
    /// Bombe in angegebene Richtung kicken
    /// </summary>
    public void Kick(Direction direction)
    {
        if (IsSliding || HasExploded) return;
        IsSliding = true;
        SlideDirection = direction;
        PlayerOnTop = false; // Kick hebt PlayerOnTop sofort auf
    }

    /// <summary>
    /// Gleiten stoppen (Bombe hat Hindernis getroffen)
    /// </summary>
    public void StopSlide()
    {
        IsSliding = false;
        SlideDirection = Direction.None;
        // An nächste Zellenmitte einrasten
        X = GridX * Grid.GameGrid.CELL_SIZE + Grid.GameGrid.CELL_SIZE / 2f;
        Y = GridY * Grid.GameGrid.CELL_SIZE + Grid.GameGrid.CELL_SIZE / 2f;
    }

    /// <summary>
    /// Reduziert die Zündschnur-Zeit (z.B. durch Dungeon-Synergien)
    /// </summary>
    public void ReduceFuse(float amount)
    {
        FuseTimer = MathF.Max(0.5f, FuseTimer - amount);
    }

    protected override int GetAnimationFrameCount() => 4;

    /// <summary>
    /// Bombe an Grid-Position erstellen
    /// </summary>
    public static Bomb CreateAtGrid(int gridX, int gridY, Player owner)
    {
        float x = gridX * Grid.GameGrid.CELL_SIZE + Grid.GameGrid.CELL_SIZE / 2f;
        float y = gridY * Grid.GameGrid.CELL_SIZE + Grid.GameGrid.CELL_SIZE / 2f;
        return new Bomb(x, y, owner, owner.FireRange, owner.HasDetonator);
    }
}
