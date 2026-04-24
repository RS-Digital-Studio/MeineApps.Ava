using BomberBlast.Models.Grid;

namespace BomberBlast.Models.Entities;

/// <summary>
/// Explosion effect (damages entities, destroys blocks)
/// </summary>
public class Explosion : Entity
{
    /// <summary>Dauer der Explosion in Sekunden (länger = Flammen besser sichtbar)</summary>
    public const float DURATION = 0.9f;

    /// <summary>Source bomb that created this explosion</summary>
    public Bomb? SourceBomb { get; }

    /// <summary>Time remaining for explosion</summary>
    public float Timer { get; private set; }

    /// <summary>List of cells affected by this explosion</summary>
    public List<ExplosionCell> AffectedCells { get; } = new();

    public override float AnimationSpeed => 8f;

    public Explosion(Bomb sourceBomb) : base(sourceBomb.X, sourceBomb.Y)
    {
        SourceBomb = sourceBomb;
        Timer = DURATION;
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        Timer -= deltaTime;

        // Struct-basierte Iteration: Index-Zugriff + Zurückschreiben nötig
        float progress = 1f - (Timer / DURATION);
        for (int i = 0; i < AffectedCells.Count; i++)
        {
            var cell = AffectedCells[i];
            cell.Progress = progress;
            AffectedCells[i] = cell;
        }

        if (Timer <= 0)
        {
            IsMarkedForRemoval = true;
            IsActive = false;
        }
    }

    /// <summary>
    /// Calculate explosion spread and add affected cells
    /// </summary>
    public void CalculateSpread(GameGrid grid, int range)
    {
        int centerX = GridX;
        int centerY = GridY;

        // Center cell
        AddCell(centerX, centerY, ExplosionCellType.Center, grid);

        // Spread in all four directions
        SpreadInDirection(grid, centerX, centerY, range, -1, 0, ExplosionCellType.LeftEnd, ExplosionCellType.HorizontalMiddle);
        SpreadInDirection(grid, centerX, centerY, range, 1, 0, ExplosionCellType.RightEnd, ExplosionCellType.HorizontalMiddle);
        SpreadInDirection(grid, centerX, centerY, range, 0, -1, ExplosionCellType.TopEnd, ExplosionCellType.VerticalMiddle);
        SpreadInDirection(grid, centerX, centerY, range, 0, 1, ExplosionCellType.BottomEnd, ExplosionCellType.VerticalMiddle);
    }

    private void SpreadInDirection(GameGrid grid, int startX, int startY, int range,
        int dx, int dy, ExplosionCellType endType, ExplosionCellType middleType)
    {
        for (int i = 1; i <= range; i++)
        {
            int x = startX + dx * i;
            int y = startY + dy * i;

            var cell = grid.TryGetCell(x, y);
            if (cell == null)
                break;

            // Walls stop explosions completely
            if (cell.Type == CellType.Wall)
                break;

            // Blocks stop explosions but get destroyed. Die Zelle wird als "BlockHit"
            // markiert (kein Schaden an Entities hinter dem Block, auch nicht wenn
            // ein Gegner die freiwerdende Zelle während der Explosion-Dauer betritt).
            if (cell.Type == CellType.Block)
            {
                AddCell(x, y, endType, grid, isBlockHit: true);
                break;
            }

            // Choose type based on position
            var type = (i == range) ? endType : middleType;
            AddCell(x, y, type, grid);
        }
    }

    private void AddCell(int x, int y, ExplosionCellType type, GameGrid grid, bool isBlockHit = false)
    {
        var gridCell = grid.TryGetCell(x, y);
        if (gridCell == null)
            return;

        AffectedCells.Add(new ExplosionCell
        {
            X = x,
            Y = y,
            Type = type,
            Progress = 0,
            IsBlockHit = isBlockHit
        });

        // Mark grid cell as exploding
        gridCell.IsExploding = true;
        gridCell.ExplosionDirection = type switch
        {
            ExplosionCellType.Center => ExplosionDirection.Center,
            ExplosionCellType.HorizontalMiddle => ExplosionDirection.HorizontalMiddle,
            ExplosionCellType.VerticalMiddle => ExplosionDirection.VerticalMiddle,
            ExplosionCellType.LeftEnd => ExplosionDirection.LeftEnd,
            ExplosionCellType.RightEnd => ExplosionDirection.RightEnd,
            ExplosionCellType.TopEnd => ExplosionDirection.TopEnd,
            ExplosionCellType.BottomEnd => ExplosionDirection.BottomEnd,
            _ => ExplosionDirection.Center
        };
    }

    /// <summary>Nachglüh-Dauer nach Explosionsende</summary>
    public const float AFTERGLOW_DURATION = 0.4f;

    /// <summary>
    /// Explosionsmarker vom Grid entfernen und Nachglühen starten
    /// </summary>
    public void ClearFromGrid(GameGrid grid)
    {
        foreach (var cell in AffectedCells)
        {
            var gridCell = grid.TryGetCell(cell.X, cell.Y);
            if (gridCell != null)
            {
                gridCell.IsExploding = false;
                gridCell.ExplosionProgress = 0;
                gridCell.AfterglowTimer = AFTERGLOW_DURATION;
            }
        }
    }

    protected override int GetAnimationFrameCount() => 4;
}

/// <summary>
/// Einzelne Explosions-Zelle (struct für weniger Heap-Allokationen)
/// </summary>
public struct ExplosionCell
{
    public int X;
    public int Y;
    public ExplosionCellType Type;
    public float Progress;
    /// <summary>
    /// True wenn diese Zelle ein zerstörbarer Block ist, der gerade zerstört wird.
    /// Entities (Spieler/Gegner) sollen hier KEINEN Schaden bekommen —
    /// der Block blockiert den Explosions-Radius, auch wenn er zerfällt.
    /// Verhindert den Bug: "Gegner läuft während der Block zerfällt in die Zelle rein und stirbt".
    /// </summary>
    public bool IsBlockHit;
}

/// <summary>
/// Type of explosion sprite to render
/// </summary>
public enum ExplosionCellType
{
    Center,
    HorizontalMiddle,
    VerticalMiddle,
    LeftEnd,
    RightEnd,
    TopEnd,
    BottomEnd
}
