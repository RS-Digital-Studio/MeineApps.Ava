using BomberBlast.Models.Grid;

namespace BomberBlast.AI.PathFinding;

/// <summary>
/// A* pathfinding implementation for enemy AI
/// </summary>
public sealed class AStar
{
    private readonly GameGrid _grid;

    // Gepoolte Collections fuer FindPath() - vermeidet Heap-Allokationen pro Aufruf
    private readonly PriorityQueue<(int X, int Y), float> _openSet = new();
    private readonly HashSet<(int, int)> _closedSet = new();
    private readonly Dictionary<(int, int), (int, int)> _cameFrom = new();
    private readonly Dictionary<(int, int), float> _gScore = new();

    // Gepoolte Collections fuer FindSafeCell()
    private readonly HashSet<(int, int)> _bfsVisited = new();
    private readonly Queue<(int x, int y, int dist)> _bfsQueue = new();

    // Richtungen als statisches Array - wird nie neu allokiert
    private static readonly (int dx, int dy)[] Directions = { (-1, 0), (1, 0), (0, -1), (0, 1) };

    // Gepoolte Neighbor-Liste fuer GetNeighbors()
    private readonly List<(int X, int Y)> _neighbors = new(4);

    // Gepoolte Liste fuer ReconstructPath() (vermeidet Allokation pro Pfadsuche)
    private readonly List<(int x, int y)> _pathBuffer = new();

    // Singleton leere Queue (vermeidet Allokation bei fehlgeschlagener Pfadsuche)
    private static readonly Queue<(int x, int y)> EmptyPath = new();

    public AStar(GameGrid grid)
    {
        _grid = grid;
    }

    /// <summary>
    /// Find path from start to goal
    /// </summary>
    /// <param name="startX">Start grid X</param>
    /// <param name="startY">Start grid Y</param>
    /// <param name="goalX">Goal grid X</param>
    /// <param name="goalY">Goal grid Y</param>
    /// <param name="canPassWalls">Whether entity can pass through blocks</param>
    /// <param name="avoidBombs">Whether to avoid bomb cells</param>
    /// <returns>Queue of grid positions to follow, or empty if no path</returns>
    public Queue<(int x, int y)> FindPath(int startX, int startY, int goalX, int goalY,
        bool canPassWalls = false, bool avoidBombs = true)
    {
        // Gepoolte Collections zuruecksetzen
        _openSet.Clear();
        _closedSet.Clear();
        _cameFrom.Clear();
        _gScore.Clear();

        _gScore[(startX, startY)] = 0;
        _openSet.Enqueue((startX, startY), Heuristic(startX, startY, goalX, goalY));

        while (_openSet.Count > 0)
        {
            var current = _openSet.Dequeue();

            if (current.X == goalX && current.Y == goalY)
            {
                return ReconstructPath(_cameFrom, (goalX, goalY));
            }

            _closedSet.Add((current.X, current.Y));

            GetNeighbors(current.X, current.Y, canPassWalls, avoidBombs);
            foreach (var neighbor in _neighbors)
            {
                if (_closedSet.Contains((neighbor.X, neighbor.Y)))
                    continue;

                float tentativeG = _gScore[(current.X, current.Y)] + 1;

                if (!_gScore.ContainsKey((neighbor.X, neighbor.Y)) ||
                    tentativeG < _gScore[(neighbor.X, neighbor.Y)])
                {
                    _cameFrom[(neighbor.X, neighbor.Y)] = (current.X, current.Y);
                    _gScore[(neighbor.X, neighbor.Y)] = tentativeG;

                    float fScore = tentativeG + Heuristic(neighbor.X, neighbor.Y, goalX, goalY);
                    _openSet.Enqueue((neighbor.X, neighbor.Y), fScore);
                }
            }
        }

        // Kein Pfad gefunden - Singleton statt neue Allokation
        return EmptyPath;
    }

    /// <summary>
    /// Find nearest safe cell (not threatened by bombs)
    /// </summary>
    public (int x, int y)? FindSafeCell(int startX, int startY, HashSet<(int, int)> dangerZone,
        bool canPassWalls = false)
    {
        // Gepoolte Collections zuruecksetzen
        _bfsVisited.Clear();
        _bfsQueue.Clear();

        _bfsQueue.Enqueue((startX, startY, 0));
        _bfsVisited.Add((startX, startY));

        while (_bfsQueue.Count > 0)
        {
            var (x, y, dist) = _bfsQueue.Dequeue();

            // Found safe cell
            if (!dangerZone.Contains((x, y)))
            {
                return (x, y);
            }

            // Don't search too far
            if (dist > 10)
                continue;

            GetNeighbors(x, y, canPassWalls, avoidBombs: false);
            foreach (var neighbor in _neighbors)
            {
                if (!_bfsVisited.Contains((neighbor.X, neighbor.Y)))
                {
                    _bfsVisited.Add((neighbor.X, neighbor.Y));
                    _bfsQueue.Enqueue((neighbor.X, neighbor.Y, dist + 1));
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Fuellt _neighbors mit begehbaren Nachbarzellen (keine Heap-Allokation)
    /// </summary>
    private void GetNeighbors(int x, int y, bool canPassWalls, bool avoidBombs)
    {
        _neighbors.Clear();

        foreach (var (dx, dy) in Directions)
        {
            int nx = x + dx;
            int ny = y + dy;

            var cell = _grid.TryGetCell(nx, ny);
            if (cell == null)
                continue;

            // Check walkability
            if (cell.Type == CellType.Wall)
                continue;

            if (cell.Type == CellType.Block && !canPassWalls)
                continue;

            if (avoidBombs && cell.Bomb != null)
                continue;

            _neighbors.Add((nx, ny));
        }
    }

    private float Heuristic(int x1, int y1, int x2, int y2)
    {
        // Manhattan distance
        return Math.Abs(x2 - x1) + Math.Abs(y2 - y1);
    }

    private Queue<(int x, int y)> ReconstructPath(Dictionary<(int, int), (int, int)> cameFrom, (int x, int y) current)
    {
        _pathBuffer.Clear();
        _pathBuffer.Add(current);

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            _pathBuffer.Add(current);
        }

        _pathBuffer.Reverse();

        // Start-Position überspringen
        if (_pathBuffer.Count > 1)
            _pathBuffer.RemoveAt(0);

        return new Queue<(int x, int y)>(_pathBuffer);
    }
}
