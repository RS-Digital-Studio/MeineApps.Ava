using BomberBlast.AI.PathFinding;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;

namespace BomberBlast.AI;

/// <summary>
/// Enemy AI controller using behavior-based decisions
/// </summary>
public class EnemyAI
{
    private readonly GameGrid _grid;
    private readonly AStar _pathfinder;
    private readonly Random _random = new();

    // Detection ranges (in cells)
    private const int LOW_INTEL_DETECTION = 3;
    private const int NORMAL_INTEL_DETECTION = 5;
    private const int HIGH_INTEL_DETECTION = 8;

    // Gepoolte Collections - vermeidet Heap-Allokationen pro Frame
    private readonly HashSet<(int, int)> _dangerZone = new();
    private readonly List<Direction> _validDirections = new(4);

    public EnemyAI(GameGrid grid)
    {
        _grid = grid;
        _pathfinder = new AStar(grid);
    }

    /// <summary>
    /// Gefahrenzone einmal pro Frame vorberechnen (statt pro Gegner → Performance)
    /// </summary>
    public void PreCalculateDangerZone(IEnumerable<Bomb> bombs)
    {
        CalculateDangerZone(bombs);
    }

    /// <summary>
    /// Update enemy AI and set movement direction.
    /// PreCalculateDangerZone() muss vorher aufgerufen werden!
    /// </summary>
    public void Update(Enemy enemy, Player player, float deltaTime)
    {
        if (enemy.IsDying || !enemy.IsActive)
            return;

        // Boss-Gegner haben eigene AI-Logik
        if (enemy is BossEnemy boss)
        {
            UpdateBossAI(boss, player, deltaTime);
            return;
        }

        // Smoke-Konfusion: Gegner auf Rauchzellen laufen zufällig (ignorieren Pathfinding)
        var smokeCell = _grid.TryGetCell(enemy.GridX, enemy.GridY);
        if (smokeCell != null && smokeCell.IsSmokeCloud)
        {
            // Zufällige Richtung wählen, ohne DangerZone zu berücksichtigen
            enemy.MovementDirection = GetRandomValidDirection(enemy);
            enemy.Move(deltaTime, _grid);
            return;
        }

        // Check if it's time for a new decision
        if (enemy.AIDecisionTimer > 0)
        {
            // Continue current movement
            enemy.Move(deltaTime, _grid);
            return;
        }

        // Reset decision timer
        enemy.AIDecisionTimer = enemy.AIDecisionInterval * (0.8f + _random.NextSingle() * 0.4f);

        // Vorberechnete Gefahrenzone verwenden
        var dangerZone = _dangerZone;

        // Check if we're in danger
        bool inDanger = dangerZone.Contains((enemy.GridX, enemy.GridY));

        // Priority 1: Evade if in danger
        if (inDanger)
        {
            if (TryEvade(enemy, dangerZone))
            {
                enemy.Move(deltaTime, _grid);
                return;
            }
        }

        // Priority 2: Behavior based on intelligence
        switch (enemy.Intelligence)
        {
            case EnemyIntelligence.Low:
                UpdateLowIntelligence(enemy, player);
                break;

            case EnemyIntelligence.Normal:
                UpdateNormalIntelligence(enemy, player, dangerZone);
                break;

            case EnemyIntelligence.High:
                UpdateHighIntelligence(enemy, player, dangerZone);
                break;
        }

        // Check for stuck state and force direction change
        if (enemy.StuckTimer > 0.5f)
        {
            enemy.MovementDirection = GetRandomValidDirection(enemy);
            enemy.StuckTimer = 0;
        }

        enemy.Move(deltaTime, _grid);
    }

    private void UpdateLowIntelligence(Enemy enemy, Player player)
    {
        // Low intelligence: Einfache Hin-und-Her-Bewegung
        // Richtungswechsel bei Blockade oder zufällig

        // Sofortiger Richtungswechsel wenn aktuelle Richtung blockiert ist
        if (enemy.MovementDirection != Direction.None && !CanMoveInDirection(enemy, enemy.MovementDirection))
        {
            enemy.MovementDirection = GetRandomValidDirection(enemy);
            return;
        }

        if (enemy.MovementDirection == Direction.None || _random.NextDouble() < 0.1)
        {
            // Zufällige Richtung, Präferenz für aktuelle Richtung
            if (enemy.MovementDirection != Direction.None && _random.NextDouble() < 0.7)
            {
                if (CanMoveInDirection(enemy, enemy.MovementDirection))
                    return;
            }

            enemy.MovementDirection = GetRandomValidDirection(enemy);
        }
    }

    private void UpdateNormalIntelligence(Enemy enemy, Player player, HashSet<(int, int)> dangerZone)
    {
        // Normal intelligence: Erratic movement, sometimes chases player

        // 30% chance to chase player if within detection range
        int detectionRange = NORMAL_INTEL_DETECTION;
        int distanceToPlayer = enemy.ManhattanDistanceTo(player.GridX, player.GridY);

        if (distanceToPlayer <= detectionRange && _random.NextDouble() < 0.3)
        {
            // Use cached path if valid
            if (TryFollowCachedPath(enemy, dangerZone))
                return;

            // Calculate new path
            var path = _pathfinder.FindPath(
                enemy.GridX, enemy.GridY,
                player.GridX, player.GridY,
                enemy.CanPassWalls, avoidBombs: true);

            if (path.Count > 0)
            {
                // Cache the path
                enemy.Path = path;
                enemy.TargetPosition = (player.GridX, player.GridY);

                var next = path.Peek();
                enemy.MovementDirection = GetDirectionTo(enemy.GridX, enemy.GridY, next.x, next.y);
                return;
            }
        }

        // Otherwise, random movement
        if (enemy.MovementDirection == Direction.None || _random.NextDouble() < 0.2)
        {
            enemy.MovementDirection = GetRandomValidDirection(enemy);
        }
    }

    private void UpdateHighIntelligence(Enemy enemy, Player player, HashSet<(int, int)> dangerZone)
    {
        // High intelligence: Actively chases player, avoids bombs
        // Uses state machine with hysteresis to prevent rapid switching

        int detectionRange = HIGH_INTEL_DETECTION;
        int distanceToPlayer = enemy.ManhattanDistanceTo(player.GridX, player.GridY);

        // Hysteresis: different thresholds for entering vs leaving chase mode
        bool shouldChase = enemy.AIState == EnemyAIState.Chasing
            ? distanceToPlayer <= (int)(detectionRange * 1.3f)  // Keep chasing until further away
            : distanceToPlayer <= detectionRange;                // Start chasing at normal range

        if (shouldChase)
        {
            // Switch to chasing state
            enemy.AIState = EnemyAIState.Chasing;

            // Use cached path if valid and safe
            if (TryFollowCachedPath(enemy, dangerZone))
                return;

            // Calculate new path
            var path = _pathfinder.FindPath(
                enemy.GridX, enemy.GridY,
                player.GridX, player.GridY,
                enemy.CanPassWalls, avoidBombs: true);

            if (path.Count > 0)
            {
                var next = path.Peek();

                // Don't move into danger zone
                if (!dangerZone.Contains((next.x, next.y)))
                {
                    // Cache the path
                    enemy.Path = path;
                    enemy.TargetPosition = (player.GridX, player.GridY);

                    enemy.MovementDirection = GetDirectionTo(enemy.GridX, enemy.GridY, next.x, next.y);
                    return;
                }
            }
        }
        else
        {
            // Switch to wandering state
            enemy.AIState = EnemyAIState.Wandering;
        }

        // Patrol or wander
        if (enemy.MovementDirection == Direction.None || _random.NextDouble() < 0.15)
        {
            enemy.MovementDirection = GetRandomSafeDirection(enemy, dangerZone);
        }
    }

    /// <summary>
    /// Try to follow cached path. Returns true if valid step taken.
    /// </summary>
    private bool TryFollowCachedPath(Enemy enemy, HashSet<(int, int)> dangerZone)
    {
        if (enemy.Path.Count == 0)
            return false;

        // Check if we reached the next waypoint
        var nextWaypoint = enemy.Path.Peek();
        if (enemy.GridX == nextWaypoint.x && enemy.GridY == nextWaypoint.y)
        {
            enemy.Path.Dequeue();
            if (enemy.Path.Count == 0)
            {
                enemy.TargetPosition = null;
                return false;
            }
            nextWaypoint = enemy.Path.Peek();
        }

        // Check if next waypoint is safe
        if (dangerZone.Contains((nextWaypoint.x, nextWaypoint.y)))
        {
            // Pfad kompromittiert → sofort sichere Richtung wählen
            enemy.Path.Clear();
            enemy.TargetPosition = null;
            enemy.MovementDirection = GetRandomSafeDirection(enemy, dangerZone);
            return enemy.MovementDirection != Direction.None;
        }

        // Check if path is still valid (no new obstacles)
        if (!CanMoveInDirection(enemy, GetDirectionTo(enemy.GridX, enemy.GridY, nextWaypoint.x, nextWaypoint.y)))
        {
            // Hindernis auf dem Weg → sofort Ausweichrichtung wählen
            enemy.Path.Clear();
            enemy.TargetPosition = null;
            enemy.MovementDirection = GetRandomSafeDirection(enemy, dangerZone);
            return enemy.MovementDirection != Direction.None;
        }

        enemy.MovementDirection = GetDirectionTo(enemy.GridX, enemy.GridY, nextWaypoint.x, nextWaypoint.y);
        return true;
    }

    private bool TryEvade(Enemy enemy, HashSet<(int, int)> dangerZone)
    {
        // Optimized: Only test 4 cardinal directions for escape
        // Early exit when safe direction found
        var bestDirection = Direction.None;

        foreach (var dir in DirectionExtensions.GetCardinalDirections())
        {
            if (!CanMoveInDirection(enemy, dir))
                continue;

            int nx = enemy.GridX + dir.GetDeltaX();
            int ny = enemy.GridY + dir.GetDeltaY();

            // Check if this cell is safe
            if (!dangerZone.Contains((nx, ny)))
            {
                // Found safe direction - use it immediately (early exit)
                enemy.MovementDirection = dir;
                return true;
            }

            // Remember we can at least move somewhere
            if (bestDirection == Direction.None)
            {
                bestDirection = dir;
            }
        }

        // No immediate safe direction found - use pathfinder for complex escape
        var safeCell = _pathfinder.FindSafeCell(
            enemy.GridX, enemy.GridY,
            dangerZone, enemy.CanPassWalls);

        if (safeCell.HasValue)
        {
            var direction = GetDirectionTo(
                enemy.GridX, enemy.GridY,
                safeCell.Value.x, safeCell.Value.y);

            if (direction != Direction.None && CanMoveInDirection(enemy, direction))
            {
                enemy.MovementDirection = direction;
                return true;
            }
        }

        // Last resort: move anywhere we can
        if (bestDirection != Direction.None)
        {
            enemy.MovementDirection = bestDirection;
            return true;
        }

        return false;
    }

    private HashSet<(int, int)> CalculateDangerZone(IEnumerable<Bomb> bombs)
    {
        // Gepooltes HashSet wiederverwenden statt neu allokieren
        _dangerZone.Clear();

        // Phase 1: Alle Bomben und ihre Explosionsbereiche berechnen
        foreach (var bomb in bombs)
        {
            if (!bomb.IsActive)
                continue;

            AddBombDangerZone(bomb.GridX, bomb.GridY, bomb.Range);
        }

        // Phase 2: Kettenreaktionen berücksichtigen
        // Bomben in der Gefahrenzone anderer Bomben lösen Kettenreaktionen aus
        bool changed = true;
        int iterations = 0;
        while (changed && iterations < 3) // Max 3 Iterationen (Performance + ausreichend für Ketten)
        {
            changed = false;
            iterations++;
            foreach (var bomb in bombs)
            {
                if (!bomb.IsActive)
                    continue;

                // Prüfe ob diese Bombe in der Gefahrenzone einer anderen liegt
                if (_dangerZone.Contains((bomb.GridX, bomb.GridY)))
                {
                    // Berechne auch den Bereich dieser Bombe (falls noch nicht geschehen)
                    int countBefore = _dangerZone.Count;
                    AddBombDangerZone(bomb.GridX, bomb.GridY, bomb.Range);
                    if (_dangerZone.Count > countBefore)
                        changed = true;
                }
            }
        }

        return _dangerZone;
    }

    /// <summary>
    /// Gefahrenzone einer einzelnen Bombe zum Set hinzufügen
    /// </summary>
    private void AddBombDangerZone(int bx, int by, int range)
    {
        _dangerZone.Add((bx, by));

        foreach (var dir in DirectionExtensions.GetCardinalDirections())
        {
            for (int i = 1; i <= range; i++)
            {
                int nx = bx + dir.GetDeltaX() * i;
                int ny = by + dir.GetDeltaY() * i;

                var cell = _grid.TryGetCell(nx, ny);
                if (cell == null || cell.Type == CellType.Wall)
                    break;

                _dangerZone.Add((nx, ny));

                if (cell.Type == CellType.Block)
                    break;
            }
        }
    }

    private Direction GetRandomValidDirection(Enemy enemy)
    {
        // Gepoolte Liste statt LINQ .Where().ToList()
        _validDirections.Clear();
        foreach (var d in DirectionExtensions.GetCardinalDirections())
        {
            if (CanMoveInDirection(enemy, d))
                _validDirections.Add(d);
        }

        if (_validDirections.Count == 0)
            return Direction.None;

        return _validDirections[_random.Next(_validDirections.Count)];
    }

    private Direction GetRandomSafeDirection(Enemy enemy, HashSet<(int, int)> dangerZone)
    {
        // Gepoolte Liste statt LINQ .Where().ToList()
        _validDirections.Clear();
        foreach (var d in DirectionExtensions.GetCardinalDirections())
        {
            if (!CanMoveInDirection(enemy, d))
                continue;

            int nx = enemy.GridX + d.GetDeltaX();
            int ny = enemy.GridY + d.GetDeltaY();
            if (!dangerZone.Contains((nx, ny)))
                _validDirections.Add(d);
        }

        if (_validDirections.Count == 0)
            return GetRandomValidDirection(enemy);

        return _validDirections[_random.Next(_validDirections.Count)];
    }

    private bool CanMoveInDirection(Enemy enemy, Direction direction)
    {
        int nx = enemy.GridX + direction.GetDeltaX();
        int ny = enemy.GridY + direction.GetDeltaY();

        var cell = _grid.TryGetCell(nx, ny);
        if (cell == null)
            return false;

        if (cell.Type == CellType.Wall)
            return false;

        if (cell.Type == CellType.Block && !enemy.CanPassWalls)
            return false;

        // PlatformGap-Zellen sind tödliche Lücken - Gegner meiden sie
        if (cell.Type == CellType.PlatformGap && !enemy.CanPassWalls)
            return false;

        if (cell.Bomb != null)
            return false;

        return true;
    }

    private Direction GetDirectionTo(int fromX, int fromY, int toX, int toY)
    {
        int dx = toX - fromX;
        int dy = toY - fromY;

        // Prefer horizontal/vertical based on greater difference
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            return dx > 0 ? Direction.Right : Direction.Left;
        }
        else if (dy != 0)
        {
            return dy > 0 ? Direction.Down : Direction.Up;
        }

        return Direction.None;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BOSS-AI (einfacher als normale AI: Direkt auf Spieler zu)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Boss-AI: Langsame Verfolgung des Spielers, kein Pathfinding.
    /// Während Telegraph/Angriff bleibt der Boss stehen.
    /// </summary>
    private void UpdateBossAI(BossEnemy boss, Player player, float deltaTime)
    {
        // Während Telegraph oder Angriff: Stehenbleiben
        if (boss.IsTelegraphing || boss.IsAttacking)
        {
            boss.MovementDirection = Direction.None;
            return;
        }

        // AI-Decision-Timer verwenden (wie bei normalen Gegnern)
        float decisionInterval = boss.IsEnraged ? 0.5f : 1.0f;

        if (boss.AIDecisionTimer > 0)
        {
            // Aktuelle Richtung beibehalten, Boss bewegt sich über MoveBoss()
            return;
        }

        // Neuen Timer setzen
        boss.AIDecisionTimer = decisionInterval * (0.8f + _random.NextSingle() * 0.4f);

        // Richtung zum Spieler berechnen (kein A*-Pathfinding, nur Direktions-Check)
        int dx = player.GridX - boss.GridX;
        int dy = player.GridY - boss.GridY;

        // Bevorzugte Richtung: Die Achse mit größerer Distanz
        Direction primaryDir;
        Direction secondaryDir;

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            primaryDir = dx > 0 ? Direction.Right : (dx < 0 ? Direction.Left : Direction.None);
            secondaryDir = dy > 0 ? Direction.Down : (dy < 0 ? Direction.Up : Direction.None);
        }
        else
        {
            primaryDir = dy > 0 ? Direction.Down : (dy < 0 ? Direction.Up : Direction.None);
            secondaryDir = dx > 0 ? Direction.Right : (dx < 0 ? Direction.Left : Direction.None);
        }

        // Primäre Richtung versuchen (prüft ob Boss sich dort hin bewegen kann)
        if (primaryDir != Direction.None && CanBossMoveInDirection(boss, primaryDir))
        {
            boss.MovementDirection = primaryDir;
            return;
        }

        // Sekundäre Richtung versuchen
        if (secondaryDir != Direction.None && CanBossMoveInDirection(boss, secondaryDir))
        {
            boss.MovementDirection = secondaryDir;
            return;
        }

        // Fallback: Zufällige begehbare Richtung
        _validDirections.Clear();
        foreach (var dir in DirectionExtensions.GetCardinalDirections())
        {
            if (CanBossMoveInDirection(boss, dir))
                _validDirections.Add(dir);
        }

        boss.MovementDirection = _validDirections.Count > 0
            ? _validDirections[_random.Next(_validDirections.Count)]
            : Direction.None;
    }

    /// <summary>
    /// Prüft ob der Boss sich in eine Richtung bewegen kann (Multi-Cell Kollision)
    /// </summary>
    private bool CanBossMoveInDirection(BossEnemy boss, Direction direction)
    {
        int baseX = boss.GridX + direction.GetDeltaX();
        int baseY = boss.GridY + direction.GetDeltaY();

        // Alle Zellen prüfen die der Boss an der neuen Position belegen würde
        for (int dy = 0; dy < boss.BossSize; dy++)
        {
            for (int dx = 0; dx < boss.BossSize; dx++)
            {
                int cx = baseX + dx;
                int cy = baseY + dy;

                var cell = _grid.TryGetCell(cx, cy);
                if (cell == null) return false;
                if (cell.Type == CellType.Wall || cell.Type == CellType.Block) return false;
                if (cell.Bomb != null) return false;
            }
        }

        return true;
    }
}
