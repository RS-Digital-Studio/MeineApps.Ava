namespace BomberBlast.Models.Entities;

/// <summary>
/// Movement direction for entities
/// </summary>
public enum Direction
{
    None,
    Up,
    Down,
    Left,
    Right
}

public static class DirectionExtensions
{
    /// <summary>
    /// Get X offset for direction (-1, 0, or 1)
    /// </summary>
    public static int GetDeltaX(this Direction direction)
    {
        return direction switch
        {
            Direction.Left => -1,
            Direction.Right => 1,
            _ => 0
        };
    }

    /// <summary>
    /// Get Y offset for direction (-1, 0, or 1)
    /// </summary>
    public static int GetDeltaY(this Direction direction)
    {
        return direction switch
        {
            Direction.Up => -1,
            Direction.Down => 1,
            _ => 0
        };
    }

    /// <summary>
    /// Get opposite direction
    /// </summary>
    public static Direction Opposite(this Direction direction)
    {
        return direction switch
        {
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            _ => Direction.None
        };
    }

    /// <summary>
    /// Alle Kardinalrichtungen (statisch, keine Allokation)
    /// </summary>
    private static readonly Direction[] Cardinals = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

    public static Direction[] GetCardinalDirections() => Cardinals;

    /// <summary>
    /// Convert direction to angle in degrees (for sprite rotation)
    /// </summary>
    public static float ToAngle(this Direction direction)
    {
        return direction switch
        {
            Direction.Up => 0f,
            Direction.Right => 90f,
            Direction.Down => 180f,
            Direction.Left => 270f,
            _ => 0f
        };
    }
}
