using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Models.Levels;
using MeineApps.Core.Ava.Localization;

namespace BomberBlast.Core.LevelGeneration;

/// <summary>
/// Zustandslose Default-Implementierung von <see cref="ILevelGenerator"/>.
/// Extrahiert aus GameEngine.Level.cs (v2.0.30+) um GameEngine-God-Class zu entschlacken.
///
/// Keine DI-Dependencies ausser <see cref="ILocalizationService"/> (fuer Mutator-Namen).
/// Keine Game-Events, keine Sounds, kein Tracking.
/// </summary>
public sealed class LevelGenerator : ILevelGenerator
{
    private readonly ILocalizationService _localizationService;

    // Wiederverwendbare Listen (keine Heap-Allokation pro Aufruf — analog zur alten Partial-Class-Variante)
    private readonly List<Cell> _blockCells = new(150);
    private readonly List<Cell> _farBlocks = new(50);
    private readonly List<(int x, int y)> _validPositions = new(150);

    public LevelGenerator(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public string GetMutatorDisplayName(LevelMutator mutator)
    {
        string key = mutator switch
        {
            LevelMutator.AllPowerBombs => "MutatorAllPowerBombs",
            LevelMutator.DoubleSpeed => "MutatorDoubleSpeed",
            LevelMutator.InvisibleBlocks => "MutatorInvisibleWalls",
            LevelMutator.NoTimer => "MutatorNoTimer",
            LevelMutator.MirrorControls => "MutatorMirrorControls",
            _ => ""
        };

        if (string.IsNullOrEmpty(key)) return "";

        string fallback = mutator switch
        {
            LevelMutator.AllPowerBombs => "All PowerBombs",
            LevelMutator.DoubleSpeed => "Double Speed",
            LevelMutator.InvisibleBlocks => "Invisible Walls",
            LevelMutator.NoTimer => "No Timer",
            LevelMutator.MirrorControls => "Mirror Controls",
            _ => ""
        };

        return _localizationService.GetString(key) ?? fallback;
    }

    public void PlacePowerUps(LevelGenerationContext context)
    {
        var grid = context.Grid;
        var random = context.Random;
        var level = context.CurrentLevel;

        _blockCells.Clear();
        for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                if (grid[x, y].Type == CellType.Block)
                    _blockCells.Add(grid[x, y]);

        if (_blockCells.Count == 0 || level?.PowerUps == null)
            return;

        var blocks = _blockCells;

        // Fisher-Yates Shuffle (in-place, keine LINQ-Allokation)
        for (int i = blocks.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (blocks[i], blocks[j]) = (blocks[j], blocks[i]);
        }

        int blockIndex = 0;
        foreach (var powerUp in level.PowerUps)
        {
            if (blockIndex >= blocks.Count)
                break;

            Cell targetCell;
            if (powerUp.X.HasValue && powerUp.Y.HasValue)
            {
                targetCell = grid.TryGetCell(powerUp.X.Value, powerUp.Y.Value) ?? blocks[blockIndex++];
            }
            else
            {
                targetCell = blocks[blockIndex++];
            }

            if (targetCell.Type == CellType.Block)
            {
                targetCell.HiddenPowerUp = powerUp.Type;
            }
        }

        // PowerUpLuck-Upgrade: Zusätzliche zufällige PowerUps
        int extraPowerUps = context.PowerUpLuckLevel;
        // Sprint 7.1 AAA-Audit #14: Hero-PowerUp-Multiplier — LuckyLola (1.20) gibt 20% mehr PowerUps.
        if (extraPowerUps > 0 && Math.Abs(context.HeroPowerUpMultiplier - 1.0f) > 0.001f)
        {
            extraPowerUps = (int)Math.Round(extraPowerUps * context.HeroPowerUpMultiplier);
        }
        if (extraPowerUps > 0)
        {
            var basicPowerUps = new[] { PowerUpType.BombUp, PowerUpType.Fire, PowerUpType.Speed };
            for (int i = 0; i < extraPowerUps && blockIndex < blocks.Count; i++)
            {
                var cell = blocks[blockIndex++];
                if (cell.Type == CellType.Block && cell.HiddenPowerUp == null)
                {
                    cell.HiddenPowerUp = basicPowerUps[random.Next(basicPowerUps.Length)];
                }
            }
        }
    }

    public void PlaceExit(LevelGenerationContext context)
    {
        var grid = context.Grid;
        var random = context.Random;
        var level = context.CurrentLevel;

        // Wiederverwendbare Liste: Bloecke ohne HiddenPowerUp bevorzugen
        _blockCells.Clear();
        for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var c = grid[x, y];
                if (c.Type == CellType.Block && c.HiddenPowerUp == null)
                    _blockCells.Add(c);
            }

        var blocks = _blockCells;

        // Fallback: Wenn ALLE Blöcke ein HiddenPowerUp haben → alle Blöcke nehmen
        // Exit hat Priorität über PowerUp (wird unten auf null gesetzt)
        if (blocks.Count == 0)
        {
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid[x, y].Type == CellType.Block)
                        _blockCells.Add(grid[x, y]);

            if (blocks.Count == 0)
                return;
        }

        Cell exitCell;
        if (level?.ExitPosition != null)
        {
            exitCell = grid.TryGetCell(level.ExitPosition.Value.x, level.ExitPosition.Value.y)
                ?? blocks[random.Next(blocks.Count)];
        }
        else
        {
            // Exit aus den entferntesten Blöcken zufällig wählen (nicht immer der gleiche Spot)
            // Sammle alle Blöcke die mindestens 60% der maximalen Distanz haben
            int maxDist = 0;
            for (int i = 0; i < blocks.Count; i++)
            {
                int dist = Math.Abs(blocks[i].X - 1) + Math.Abs(blocks[i].Y - 1);
                if (dist > maxDist) maxDist = dist;
            }

            int threshold = (int)(maxDist * 0.6f);
            _farBlocks.Clear();
            for (int i = 0; i < blocks.Count; i++)
            {
                int dist = Math.Abs(blocks[i].X - 1) + Math.Abs(blocks[i].Y - 1);
                if (dist >= threshold)
                    _farBlocks.Add(blocks[i]);
            }

            exitCell = _farBlocks.Count > 0
                ? _farBlocks[random.Next(_farBlocks.Count)]
                : blocks[random.Next(blocks.Count)];
        }

        // Exit unter dem Block verstecken (klassisches Bomberman)
        exitCell.HiddenPowerUp = null;
        exitCell.HasHiddenExit = true;
    }

    public List<Enemy> SpawnEnemies(LevelGenerationContext context)
    {
        var result = new List<Enemy>(16);
        var grid = context.Grid;
        var random = context.Random;
        var level = context.CurrentLevel;

        if (level?.Enemies == null)
            return result;

        // Gültige Spawn-Positionen (nicht in Spieler-Nähe, nicht auf Wänden/Blöcken)
        _validPositions.Clear();
        for (int x = 1; x < GameGrid.WIDTH - 1; x++)
        {
            for (int y = 1; y < GameGrid.HEIGHT - 1; y++)
            {
                if (x <= 3 && y <= 3)
                    continue;

                var cell = grid[x, y];
                if (cell.Type == CellType.Empty)
                {
                    _validPositions.Add((x, y));
                }
            }
        }

        foreach (var spawn in level.Enemies)
        {
            for (int i = 0; i < spawn.Count; i++)
            {
                (int x, int y) pos;
                if (spawn.X.HasValue && spawn.Y.HasValue)
                {
                    pos = (spawn.X.Value, spawn.Y.Value);
                }
                else if (_validPositions.Count > 0)
                {
                    int index = random.Next(_validPositions.Count);
                    pos = _validPositions[index];
                    _validPositions.RemoveAt(index);
                }
                else
                {
                    // Fallback: Bis zu 40 Versuche für gültige Position
                    bool placed = false;
                    pos = (0, 0);
                    for (int attempt = 0; attempt < 40; attempt++)
                    {
                        pos = (random.Next(3, GameGrid.WIDTH - 2), random.Next(3, GameGrid.HEIGHT - 2));
                        var fallbackCell = grid.TryGetCell(pos.x, pos.y);
                        if (fallbackCell != null && fallbackCell.Type == CellType.Empty)
                        {
                            placed = true;
                            break;
                        }
                    }
                    if (!placed) continue; // Nur nach 40 Fehlversuchen aufgeben
                }

                // Sprint 6.1 AAA-Audit #12: Elite-Modifier — ab Welt 3, 8% Chance.
                // Elites haben 1.2x Speed, 2x HP, 3x Points (Enemy-Konstruktor multiplikativ).
                bool isElite = false;
                if (level.Number > 0)
                {
                    int eliteWorldId = (level.Number - 1) / 10 + 1;
                    if (eliteWorldId >= 3 && random.Next(100) < 8)
                        isElite = true;
                }
                var enemy = Enemy.CreateAtGrid(pos.x, pos.y, spawn.Type, isElite);
                result.Add(enemy);
            }
        }

        // Boss(e) spawnen wenn Boss-Level
        if (level.BossKind.HasValue)
        {
            bool isDuoBoss = level.BossKind2.HasValue;
            var bossType1 = level.BossKind.Value;
            // Sprint 6.1 AAA-Audit #15: Boss-Modifier nur fuer Single-Boss-Encounter
            // (Duo-Boss = bereits Endgame-Schwierigkeit). Welt-ID aus Level.Number ableiten.
            int worldId = level.Number > 0 ? Math.Max(1, (level.Number - 1) / 10 + 1) : 1;
            var modifierRng = !isDuoBoss ? random : null;

            // Einzel-Boss: Mitte. Duo-Boss: Links und Rechts getrennt
            int boss1X = isDuoBoss ? GameGrid.WIDTH / 4 : GameGrid.WIDTH / 2;
            int boss1Y = GameGrid.HEIGHT / 2;

            var boss1 = SpawnBossAtPosition(grid, boss1X, boss1Y, bossType1, worldId, modifierRng);
            if (boss1 != null) result.Add(boss1);

            // Zweiter Boss (Duo-Encounter Welt 9+10). isDuoBoss garantiert BossKind2.HasValue.
            if (isDuoBoss)
            {
                var bossType2 = level.BossKind2!.Value;
                int boss2X = GameGrid.WIDTH * 3 / 4;
                int boss2Y = GameGrid.HEIGHT / 2;

                var boss2 = SpawnBossAtPosition(grid, boss2X, boss2Y, bossType2, worldId, rng: null);
                if (boss2 != null) result.Add(boss2);
            }
        }

        return result;
    }

    /// <summary>Spawnt einen Boss an einer Grid-Position und räumt die Zellen frei.</summary>
    private static BossEnemy SpawnBossAtPosition(GameGrid grid, int gridX, int gridY, BossType bossType)
        => SpawnBossAtPosition(grid, gridX, gridY, bossType, worldId: 1, rng: null);

    /// <summary>
    /// Sprint 6.1 AAA-Audit #15: Boss-Spawn mit Modifier-Roll basierend auf Welt-ID.
    /// Welt 1-4 nie Modifier, Welt 5-9 30%, Welt 10 60%.
    /// </summary>
    private static BossEnemy SpawnBossAtPosition(GameGrid grid, int gridX, int gridY, BossType bossType,
        int worldId, Random? rng)
    {
        int bossSize = bossType == BossType.FinalBoss ? 3 : 2;

        // Sicherstellen dass die Boss-Zellen frei sind (Blöcke entfernen)
        for (int dy = 0; dy < bossSize; dy++)
        {
            for (int dx = 0; dx < bossSize; dx++)
            {
                var cell = grid.TryGetCell(gridX + dx, gridY + dy);
                if (cell != null && cell.Type == CellType.Block)
                {
                    cell.Type = CellType.Empty;
                    cell.HasHiddenExit = false;
                    cell.HiddenPowerUp = null;
                }
            }
        }

        var boss = BossEnemy.CreateAtGrid(gridX, gridY, bossType);

        // Sprint 6.1 AAA-Audit #15: Boss-Modifier-Roll. NUR fuer Story-Bosse (BossSequenceLevel),
        // nicht fuer BossRush oder Duo-Boss-Encounter (zu viel Komplexitaet bei x2 Bossen).
        if (rng != null)
        {
            boss.Modifier = BomberBlast.Models.Entities.BossModifierExtensions.RollForWorld(worldId, rng);
            boss.InitializeShieldIfNeeded();
        }
        return boss;
    }
}
