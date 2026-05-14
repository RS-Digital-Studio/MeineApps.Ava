using BomberBlast.Models.Grid;

namespace BomberBlast.Models.Entities;

/// <summary>
/// Base class for all game entities (player, enemies, bombs, etc.)
/// </summary>
public abstract class Entity
{
    /// <summary>Pixel position X (center of entity)</summary>
    public float X { get; set; }

    /// <summary>Pixel position Y (center of entity)</summary>
    public float Y { get; set; }

    /// <summary>Entity width in pixels</summary>
    public virtual int Width => GameGrid.CELL_SIZE;

    /// <summary>Entity height in pixels</summary>
    public virtual int Height => GameGrid.CELL_SIZE;

    /// <summary>Whether entity is active/alive</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Whether entity should be removed from game</summary>
    public bool IsMarkedForRemoval { get; set; }

    /// <summary>
    /// Sprint 5.4 AAA-Audit #11: Per-Entity-Toggle fuer den Outline-Pass.
    /// Vereinheitlicht den Look ueber inkonsistente Art-Styles (Vektor-Player +
    /// AI-WebP-Bosse/Enemies). Renderer prueft das Flag und zeichnet ggf. ueber
    /// <see cref="BomberBlast.Graphics.OutlineRenderHelper"/>. Default je Entity-Typ gesetzt.
    /// </summary>
    public bool RenderOutline { get; set; }

    /// <summary>Current animation frame</summary>
    public int AnimationFrame { get; set; }

    /// <summary>Animation timer</summary>
    public float AnimationTimer { get; set; }

    /// <summary>Animation speed (frames per second)</summary>
    public virtual float AnimationSpeed => 8f;

    /// <summary>Grid X position (column) - Floor für korrekte Behandlung negativer Werte</summary>
    public int GridX => (int)MathF.Floor(X / GameGrid.CELL_SIZE);

    /// <summary>Grid Y position (row) - Floor für korrekte Behandlung negativer Werte</summary>
    public int GridY => (int)MathF.Floor(Y / GameGrid.CELL_SIZE);

    /// <summary>Bounding box for collision detection</summary>
    public virtual (float left, float top, float right, float bottom) BoundingBox
    {
        get
        {
            float halfW = Width / 2f;
            float halfH = Height / 2f;
            return (X - halfW, Y - halfH, X + halfW, Y + halfH);
        }
    }

    protected Entity(float x, float y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// Update entity state
    /// </summary>
    /// <param name="deltaTime">Time since last update in seconds</param>
    public virtual void Update(float deltaTime)
    {
        UpdateAnimation(deltaTime);
    }

    /// <summary>
    /// Update animation frame
    /// </summary>
    protected virtual void UpdateAnimation(float deltaTime)
    {
        AnimationTimer += deltaTime;
        float frameDuration = 1f / AnimationSpeed;

        while (AnimationTimer >= frameDuration)
        {
            AnimationTimer -= frameDuration;
            AnimationFrame = (AnimationFrame + 1) % GetAnimationFrameCount();
        }
    }

    /// <summary>
    /// Get number of animation frames for current state
    /// </summary>
    protected virtual int GetAnimationFrameCount() => 4;

    /// <summary>
    /// Check collision with another entity.
    /// <para>v2.0.35: Nutzt verschrumpfte Hitboxes (60% der Zellgröße) damit
    /// Kollision nur bei echter visueller Berührung triggert. Vorher: Volle
    /// CELL_SIZE-Bounding-Box → Spieler "starb" wenn er auf dem gleichen Grid-Feld
    /// war obwohl pixel-distanz noch &gt;50% der Zellgröße. Neu: Shrink-Faktor 0.6
    /// lässt Spieler/Gegner bis ~60% physisch überlappen bevor Kollision zählt.
    /// Shrink kann pro Entity via <see cref="HitboxScale"/> überschrieben werden
    /// (Boss-Klassen nutzen ihre eigene multi-cell BoundingBox ohne Shrink).</para>
    /// </summary>
    public bool CollidesWith(Entity other)
    {
        if (!IsActive || !other.IsActive)
            return false;

        var a = GetHitbox();
        var b = other.GetHitbox();

        return a.left < b.right &&
               a.right > b.left &&
               a.top < b.bottom &&
               a.bottom > b.top;
    }

    /// <summary>
    /// Shrink-Faktor der Hitbox. Default 0.6 = 60% der CELL_SIZE (NICHT der
    /// BoundingBox-Größe). Gilt nur für <see cref="CollidesWith"/>. Boss-Klassen
    /// überschreiben auf 1.0, weil ihre BoundingBox bereits custom verkleinert ist.
    /// <para><b>Warum Width/Height statt BoundingBox?</b> Player/Enemy haben
    /// BoundingBox-Overrides (0.4×/0.35× CELL_SIZE). Würde GetHitbox diese
    /// zusätzlich × 0.6 verkleinern, wäre die Hitbox 0.24×/0.21× — viel zu klein.
    /// Stattdessen basiert der Shrink auf der unskalierten Entity-Größe (Width/Height
    /// = CELL_SIZE) für konsistent 0.6× Kollisions-Radius bei allen Entities.</para>
    /// </summary>
    protected virtual float HitboxScale => 0.6f;

    /// <summary>
    /// Innere Hitbox für Kollisionen. Nutzt bewusst Width/Height (CELL_SIZE) statt
    /// BoundingBox, damit alle Entities eine konsistente Shrink-Basis haben
    /// unabhängig von ihrer visuellen Darstellungs-Größe. Bei HitboxScale >= 1.0
    /// wird die BoundingBox direkt verwendet (Boss-Fall).
    /// </summary>
    private (float left, float top, float right, float bottom) GetHitbox()
    {
        float scale = HitboxScale;
        if (scale >= 1f) return BoundingBox;
        float halfW = (Width * scale) / 2f;
        float halfH = (Height * scale) / 2f;
        return (X - halfW, Y - halfH, X + halfW, Y + halfH);
    }

    /// <summary>
    /// Check if entity is at specific grid position
    /// </summary>
    public bool IsAtGridPosition(int gridX, int gridY)
    {
        return GridX == gridX && GridY == gridY;
    }

    /// <summary>
    /// Set position from grid coordinates (centered in cell)
    /// </summary>
    public void SetGridPosition(int gridX, int gridY)
    {
        X = gridX * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        Y = gridY * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
    }

    /// <summary>
    /// Calculate distance to another entity
    /// </summary>
    public float DistanceTo(Entity other)
    {
        float dx = other.X - X;
        float dy = other.Y - Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Calculate Manhattan distance to grid position
    /// </summary>
    public int ManhattanDistanceTo(int gridX, int gridY)
    {
        return Math.Abs(GridX - gridX) + Math.Abs(GridY - gridY);
    }
}
