using BomberBlast.Graphics;
using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using SkiaSharp;

namespace BomberBlast.Core.Modes;

/// <summary>
/// Survival-Modus Gegner-Spawner (v2.0.39+ Extract aus GameEngine.Level.cs).
///
/// Enthaelt die Spawn-Logik fuer den Endlos-Modus: Zeit-basierte Gegner-Typ-Auswahl,
/// Intervall-Verringerung und Position-Suche. Zustandslos (Static) — alle Felder
/// (Survival-State) bleiben in <see cref="GameEngine"/> und werden ueber
/// <see cref="SurvivalSpawnContext"/> uebergeben.
///
/// Beweggruende:
/// 1. GameEngine.Level.cs schrumpft um ~60 Zeilen.
/// 2. Spawn-Algorithmus (Zeit-Schwellen + Typ-Pools) wird isoliert testbar.
/// 3. Konstanten (MIN_INTERVAL, DECREASE_PER_SPAWN) ziehen mit der Logik um.
///
/// Side-Effects: Schreibt in ctx.Enemies (Add) und ctx.ParticleSystem (Emit).
/// SetEnemiesRemainingDirty-Callback aktualisiert das GameEngine-Cache-Flag.
/// </summary>
public static class SurvivalSpawner
{
    /// <summary>Minimales Spawn-Intervall in Sekunden (Untergrenze nach Verringerung).</summary>
    public const float MIN_SPAWN_INTERVAL = 0.8f;

    /// <summary>Pro Spawn schrumpft das Intervall um diesen Wert (in Sekunden).</summary>
    public const float SPAWN_DECREASE = 0.12f;

    /// <summary>Erstes Spawn-Intervall in Sekunden (auch initial Reset-Wert).</summary>
    public const float INITIAL_SPAWN_INTERVAL = 4f;

    /// <summary>
    /// Pro Frame aufrufen. Verringert <c>SpawnTimer</c>; spawnt einen Gegner sobald 0 erreicht;
    /// reduziert <c>SpawnInterval</c> bis <see cref="MIN_SPAWN_INTERVAL"/> als Untergrenze.
    /// Erhoeht <c>TimeElapsed</c>.
    ///
    /// v2.0.51 — Phase 8: State liegt jetzt im SurvivalMode-Object (CurrentMode-Slot).
    /// Direkter Property-Zugriff statt ref-Parameter — keine Sync-Logic in der Engine nötig.
    /// </summary>
    public static void Update(SurvivalSpawnContext ctx, float deltaTime, SurvivalMode mode)
    {
        mode.TimeElapsed += deltaTime;
        mode.SpawnTimer -= deltaTime;

        if (mode.SpawnTimer > 0)
            return;

        // Gegner spawnen
        SpawnEnemy(ctx, mode.TimeElapsed);

        // Intervall verringern (wird schwerer ueber Zeit)
        mode.SpawnInterval = MathF.Max(MIN_SPAWN_INTERVAL, mode.SpawnInterval - SPAWN_DECREASE);
        mode.SpawnTimer = mode.SpawnInterval;
    }

    /// <summary>
    /// Einen einzelnen Gegner spawnen. Typ basiert auf Ueberlebenszeit (zeit-eskalierende Pools).
    /// Position-Suche: bis zu 40 Versuche, mindestens 4 Manhattan-Distanz vom Spieler entfernt,
    /// nur auf leeren Zellen (kein Block, keine Bombe).
    /// </summary>
    private static void SpawnEnemy(SurvivalSpawnContext ctx, float timeElapsed)
    {
        var type = ChooseEnemyType(ctx.PontanRandom, timeElapsed);

        for (int attempts = 0; attempts < 40; attempts++)
        {
            int x = ctx.PontanRandom.Next(2, GameGrid.WIDTH - 1);
            int y = ctx.PontanRandom.Next(2, GameGrid.HEIGHT - 1);

            if (Math.Abs(x - ctx.PlayerGridX) + Math.Abs(y - ctx.PlayerGridY) < 4)
                continue;

            var cell = ctx.Grid.TryGetCell(x, y);
            if (cell == null || cell.Type != CellType.Empty || cell.Bomb != null)
                continue;

            var enemy = Enemy.CreateAtGrid(x, y, type);
            ctx.Enemies.Add(enemy);
            ctx.OnEnemySpawned();

            float spawnX = x * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            float spawnY = y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            ctx.ParticleSystem.Emit(spawnX, spawnY, 6, new SKColor(255, 100, 50), 50f, 0.4f);
            return;
        }
    }

    /// <summary>
    /// Waehlt den Enemy-Typ basierend auf Ueberlebenszeit. Pure Funktion fuer Testbarkeit.
    /// </summary>
    private static EnemyType ChooseEnemyType(Random random, float timeElapsed) => timeElapsed switch
    {
        < 20  => EnemyType.Ballom,
        < 45  => random.Next(3) switch { 0 => EnemyType.Onil, 1 => EnemyType.Doll, _ => EnemyType.Ballom },
        < 90  => random.Next(4) switch { 0 => EnemyType.Onil, 1 => EnemyType.Doll, 2 => EnemyType.Minvo, _ => EnemyType.Doll },
        < 150 => random.Next(5) switch { 0 => EnemyType.Minvo, 1 => EnemyType.Ovapi, 2 => EnemyType.Tanker, 3 => EnemyType.Doll, _ => EnemyType.Kondoria },
        _     => random.Next(6) switch { 0 => EnemyType.Ovapi, 1 => EnemyType.Tanker, 2 => EnemyType.Ghost, 3 => EnemyType.Pontan, 4 => EnemyType.Splitter, _ => EnemyType.Mimic }
    };
}

/// <summary>
/// Context fuer <see cref="SurvivalSpawner"/>: alle State-Refs die der Spawner braucht.
/// Felder werden in GameEngine.cs einmalig initialisiert (Lazy-Init via Getter analog _explosionCtx).
/// </summary>
public sealed class SurvivalSpawnContext
{
    public required GameGrid Grid { get; init; }
    public required List<Enemy> Enemies { get; init; }
    public required ParticleSystem ParticleSystem { get; init; }
    public required Random PontanRandom { get; init; }

    /// <summary>Zugriff auf Player-Grid-Position (live-Read aus Engine).</summary>
    public required Func<int> GetPlayerGridX { get; init; }
    public required Func<int> GetPlayerGridY { get; init; }

    /// <summary>Convenience: liefert den aktuellen Player-X-Grid-Wert.</summary>
    public int PlayerGridX => GetPlayerGridX();
    public int PlayerGridY => GetPlayerGridY();

    /// <summary>Callback wenn ein Gegner gespawnt wurde — GameEngine setzt seinen Dirty-Flag.</summary>
    public required Action OnEnemySpawned { get; init; }
}
