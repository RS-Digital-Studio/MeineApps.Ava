using BomberBlast.Models.Grid;

namespace BomberBlast.Models.Entities;

/// <summary>
/// Hilfsklasse fuer Kollisionspruefung ohne Array-Allokation
/// </summary>
public static class CollisionHelper
{
    /// <summary>
    /// Prueft ob eine Position begehbar ist (4 Ecken inline, keine Allokation)
    /// </summary>
    public static bool CanMoveTo(float newX, float newY, float halfSize, GameGrid grid, Func<Cell, bool> isBlocked)
    {
        // Oben-links
        var cell = grid.TryGetCell((int)((newX - halfSize) / GameGrid.CELL_SIZE), (int)((newY - halfSize) / GameGrid.CELL_SIZE));
        if (cell == null || isBlocked(cell)) return false;

        // Oben-rechts
        cell = grid.TryGetCell((int)((newX + halfSize) / GameGrid.CELL_SIZE), (int)((newY - halfSize) / GameGrid.CELL_SIZE));
        if (cell == null || isBlocked(cell)) return false;

        // Unten-links
        cell = grid.TryGetCell((int)((newX - halfSize) / GameGrid.CELL_SIZE), (int)((newY + halfSize) / GameGrid.CELL_SIZE));
        if (cell == null || isBlocked(cell)) return false;

        // Unten-rechts
        cell = grid.TryGetCell((int)((newX + halfSize) / GameGrid.CELL_SIZE), (int)((newY + halfSize) / GameGrid.CELL_SIZE));
        if (cell == null || isBlocked(cell)) return false;

        return true;
    }
}
