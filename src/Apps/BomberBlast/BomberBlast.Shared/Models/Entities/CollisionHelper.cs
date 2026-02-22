using BomberBlast.Models.Grid;

namespace BomberBlast.Models.Entities;

/// <summary>
/// Hilfsklasse fuer Kollisionspruefung ohne Array-Allokation.
/// Spezialisierte Methoden statt Func-Lambdas um Closure-Allokationen zu vermeiden.
/// </summary>
public static class CollisionHelper
{
    /// <summary>
    /// Spieler-Kollisionsprüfung (berücksichtigt Wallpass + Bombpass)
    /// </summary>
    public static bool CanMoveToPlayer(float newX, float newY, float halfSize, GameGrid grid, bool wallpass, bool bombpass)
    {
        // Oben-links (MathF.Floor für korrekte Behandlung negativer Werte)
        var cell = grid.TryGetCell((int)MathF.Floor((newX - halfSize) / GameGrid.CELL_SIZE), (int)MathF.Floor((newY - halfSize) / GameGrid.CELL_SIZE));
        if (cell == null || !cell.IsWalkable(wallpass, bombpass)) return false;

        // Oben-rechts
        cell = grid.TryGetCell((int)MathF.Floor((newX + halfSize) / GameGrid.CELL_SIZE), (int)MathF.Floor((newY - halfSize) / GameGrid.CELL_SIZE));
        if (cell == null || !cell.IsWalkable(wallpass, bombpass)) return false;

        // Unten-links
        cell = grid.TryGetCell((int)MathF.Floor((newX - halfSize) / GameGrid.CELL_SIZE), (int)MathF.Floor((newY + halfSize) / GameGrid.CELL_SIZE));
        if (cell == null || !cell.IsWalkable(wallpass, bombpass)) return false;

        // Unten-rechts
        cell = grid.TryGetCell((int)MathF.Floor((newX + halfSize) / GameGrid.CELL_SIZE), (int)MathF.Floor((newY + halfSize) / GameGrid.CELL_SIZE));
        if (cell == null || !cell.IsWalkable(wallpass, bombpass)) return false;

        return true;
    }

    /// <summary>
    /// Gegner-Kollisionsprüfung (Wall/Block/Bomb Checks, optional WallPass)
    /// </summary>
    public static bool CanMoveToEnemy(float newX, float newY, float halfSize, GameGrid grid, bool canPassWalls)
    {
        // Oben-links (MathF.Floor für korrekte Behandlung negativer Werte)
        var cell = grid.TryGetCell((int)MathF.Floor((newX - halfSize) / GameGrid.CELL_SIZE), (int)MathF.Floor((newY - halfSize) / GameGrid.CELL_SIZE));
        if (cell == null || IsBlockedForEnemy(cell, canPassWalls)) return false;

        // Oben-rechts
        cell = grid.TryGetCell((int)MathF.Floor((newX + halfSize) / GameGrid.CELL_SIZE), (int)MathF.Floor((newY - halfSize) / GameGrid.CELL_SIZE));
        if (cell == null || IsBlockedForEnemy(cell, canPassWalls)) return false;

        // Unten-links
        cell = grid.TryGetCell((int)MathF.Floor((newX - halfSize) / GameGrid.CELL_SIZE), (int)MathF.Floor((newY + halfSize) / GameGrid.CELL_SIZE));
        if (cell == null || IsBlockedForEnemy(cell, canPassWalls)) return false;

        // Unten-rechts
        cell = grid.TryGetCell((int)MathF.Floor((newX + halfSize) / GameGrid.CELL_SIZE), (int)MathF.Floor((newY + halfSize) / GameGrid.CELL_SIZE));
        if (cell == null || IsBlockedForEnemy(cell, canPassWalls)) return false;

        return true;
    }

    private static bool IsBlockedForEnemy(Cell cell, bool canPassWalls)
    {
        return cell.Type == CellType.Wall ||
               (cell.Type == CellType.Block && !canPassWalls) ||
               // PlatformGap-Zellen sind tödliche Lücken - Gegner ohne Wallpass meiden sie
               (cell.Type == CellType.PlatformGap && !canPassWalls) ||
               cell.Bomb != null;
    }
}
