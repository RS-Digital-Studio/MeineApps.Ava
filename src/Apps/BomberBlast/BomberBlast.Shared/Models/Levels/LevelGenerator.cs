using BomberBlast.Models.Entities;

namespace BomberBlast.Models.Levels;

/// <summary>
/// Generiert Level mit Welt-spezifischen Mechaniken, variablen Layouts und Boss-Leveln.
/// 50 Story-Level in 5 Welten (je 10), Boss alle 10 Level, Bonus alle 5 Level.
/// </summary>
public static class LevelGenerator
{
    // Layout-Rotation pro Welt (abwechslungsreich statt immer Classic)
    private static readonly LevelLayout[][] WorldLayouts =
    [
        // Welt 1 (Forest): Einfache Layouts zum Einlernen
        [LevelLayout.Classic, LevelLayout.Classic, LevelLayout.Cross, LevelLayout.Classic],
        // Welt 2 (Industrial): Enge Gänge + Eis
        [LevelLayout.Classic, LevelLayout.TwoRooms, LevelLayout.Maze, LevelLayout.Cross],
        // Welt 3 (Cavern): Labyrinth + Förderbänder
        [LevelLayout.Maze, LevelLayout.Spiral, LevelLayout.Classic, LevelLayout.Diagonal],
        // Welt 4 (Sky): Offene Räume + Teleporter
        [LevelLayout.Arena, LevelLayout.Cross, LevelLayout.TwoRooms, LevelLayout.Classic],
        // Welt 5 (Inferno): Alles kombiniert
        [LevelLayout.Diagonal, LevelLayout.Arena, LevelLayout.Spiral, LevelLayout.Maze]
    ];

    /// <summary>
    /// Generiert ein Level für eine bestimmte Levelnummer (1-50)
    /// </summary>
    public static Level GenerateLevel(int levelNumber)
    {
        var level = new Level
        {
            Number = levelNumber,
            Name = $"Stage {levelNumber}",
            TimeLimit = 200,
            Seed = levelNumber * 12345
        };

        int world = GetWorld(levelNumber);

        // Boss-Level: Jedes 10. Level (10, 20, 30, 40, 50)
        if (levelNumber % 10 == 0 && levelNumber <= 50)
        {
            ConfigureBossLevel(level, levelNumber, world);
            return level;
        }

        // Bonus-Level: Jedes 5. Level (aber nicht 10/20/30/40/50 = Boss)
        if (levelNumber % 5 == 0 && levelNumber <= 50)
        {
            ConfigureBonusLevel(level, levelNumber, world);
            return level;
        }

        // Normales Level
        ConfigureEnemies(level, levelNumber);
        ConfigurePowerUps(level, levelNumber);
        ConfigureBlockDensity(level, levelNumber);

        // Welt-Mechanik zuweisen (ab Welt 2)
        AssignWorldMechanic(level, levelNumber, world);

        // Layout-Variation (nicht jedes Level Classic)
        AssignLayout(level, levelNumber, world);

        // Boss music für Welt 5
        if (world == 5)
            level.MusicTrack = "boss";

        return level;
    }

    /// <summary>
    /// Daily-Challenge-Level generieren. Deterministisch basierend auf Seed (Datum).
    /// Schwierigkeit ~Level 20-30, zufällige Mechanik + Layout, immer fair spielbar.
    /// </summary>
    public static Level GenerateDailyChallengeLevel(int seed)
    {
        var random = new Random(seed);
        var level = new Level
        {
            Number = 99, // Spezielle Nummer für Daily
            Name = "Daily Challenge",
            TimeLimit = 180,
            Seed = seed,
            BlockDensity = 0.35f + (float)(random.NextDouble() * 0.2) // 0.35-0.55
        };

        // Zufällige Mechanik (gewichtet, None auch möglich für Abwechslung)
        var mechanics = new[] { WorldMechanic.None, WorldMechanic.Ice, WorldMechanic.Conveyor, WorldMechanic.Teleporter, WorldMechanic.LavaCrack };
        level.Mechanic = mechanics[random.Next(mechanics.Length)];

        // Zufälliges Layout (kein BossArena)
        var layouts = new[] { LevelLayout.Classic, LevelLayout.Cross, LevelLayout.Arena, LevelLayout.Maze, LevelLayout.TwoRooms, LevelLayout.Spiral, LevelLayout.Diagonal };
        level.Layout = layouts[random.Next(layouts.Length)];

        // Gegner: Mix aus mittleren/starken Gegnern (Schwierigkeit ~Level 20-30)
        level.Enemies.Clear();
        int totalEnemies = 4 + random.Next(3); // 4-6 Gegner
        var enemyPool = new[] { EnemyType.Onil, EnemyType.Doll, EnemyType.Minvo, EnemyType.Kondoria, EnemyType.Ovapi };
        for (int i = 0; i < totalEnemies; i++)
        {
            var type = enemyPool[random.Next(enemyPool.Length)];
            // Zusammenfassen wenn gleicher Typ schon vorhanden
            var existing = level.Enemies.Find(e => e.Type == type);
            if (existing != null)
                existing.Count++;
            else
                level.Enemies.Add(new EnemySpawn { Type = type, Count = 1 });
        }

        // PowerUps: Gute Mischung
        level.PowerUps.Clear();
        level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.BombUp });
        level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Fire });
        level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Speed });

        // 50% Chance auf ein Extra-PowerUp
        if (random.NextDouble() > 0.5)
        {
            var extraPool = new[] { PowerUpType.Kick, PowerUpType.Wallpass, PowerUpType.Detonator, PowerUpType.LineBomb };
            level.PowerUps.Add(new PowerUpPlacement { Type = extraPool[random.Next(extraPool.Length)] });
        }

        return level;
    }

    /// <summary>
    /// Arcade-Level generieren
    /// </summary>
    public static Level GenerateArcadeLevel(int wave)
    {
        var level = new Level
        {
            Number = wave,
            Name = $"Wave {wave}",
            TimeLimit = Math.Max(120, 200 - wave * 5),
            Seed = wave * 54321 + Environment.TickCount,
            BlockDensity = Math.Min(0.7f, 0.4f + wave * 0.02f)
        };

        ConfigureArcadeEnemies(level, wave);
        ConfigureArcadePowerUps(level, wave);

        // Ab Wave 10: Welt-Mechaniken einmischen
        if (wave >= 10 && wave < 15)
            level.Mechanic = WorldMechanic.Ice;
        else if (wave >= 15 && wave < 20)
            level.Mechanic = WorldMechanic.Conveyor;
        else if (wave >= 20 && wave < 25)
            level.Mechanic = WorldMechanic.Teleporter;
        else if (wave >= 25)
            level.Mechanic = WorldMechanic.LavaCrack;

        // Layout-Rotation in Arcade
        if (wave % 5 == 0)
            level.Layout = LevelLayout.Arena;
        else if (wave % 7 == 0)
            level.Layout = LevelLayout.Maze;

        return level;
    }

    private static int GetWorld(int levelNumber) => ((levelNumber - 1) / 10) + 1;

    // ═══════════════════════════════════════════════════════════════════════
    // WELT-MECHANIKEN + LAYOUTS
    // ═══════════════════════════════════════════════════════════════════════

    private static void AssignWorldMechanic(Level level, int levelNumber, int world)
    {
        // Welt 1: Keine Spezial-Mechanik (Tutorial-Welt)
        // Welt 2+: Mechanik ab dem 3. Level der Welt (damit Spieler sich erstmal eingewöhnt)
        int levelInWorld = ((levelNumber - 1) % 10) + 1;

        level.Mechanic = world switch
        {
            2 when levelInWorld >= 3 => WorldMechanic.Ice,
            3 when levelInWorld >= 3 => WorldMechanic.Conveyor,
            4 when levelInWorld >= 3 => WorldMechanic.Teleporter,
            5 when levelInWorld >= 2 => WorldMechanic.LavaCrack,
            _ => WorldMechanic.None
        };
    }

    private static void AssignLayout(Level level, int levelNumber, int world)
    {
        int levelInWorld = ((levelNumber - 1) % 10) + 1;
        int layoutIndex = world - 1;
        if (layoutIndex < 0 || layoutIndex >= WorldLayouts.Length)
            layoutIndex = 0;

        var layouts = WorldLayouts[layoutIndex];
        // Level 1-2 der Welt immer Classic (Eingewöhnung), danach rotierend
        if (levelInWorld <= 2)
            level.Layout = LevelLayout.Classic;
        else
            level.Layout = layouts[(levelInWorld - 3) % layouts.Length];
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BOSS-LEVEL (jedes 10. Level)
    // ═══════════════════════════════════════════════════════════════════════

    private static void ConfigureBossLevel(Level level, int levelNumber, int world)
    {
        level.IsBossLevel = true;
        level.Layout = LevelLayout.BossArena;
        level.MusicTrack = "boss";
        level.TimeLimit = 240; // Mehr Zeit für Boss
        level.BlockDensity = 0.25f; // Weniger Blöcke, mehr Kampfraum
        level.Name = $"Boss - World {world}";

        // Welt-Mechanik auch im Boss-Level (ab Welt 2)
        level.Mechanic = world switch
        {
            2 => WorldMechanic.Ice,
            3 => WorldMechanic.Conveyor,
            4 => WorldMechanic.Teleporter,
            5 => WorldMechanic.LavaCrack,
            _ => WorldMechanic.None
        };

        // Boss-Gegner: Mehrere starke Gegner des Welt-Typs + ein "Boss" (Pontan-Variante)
        level.Enemies.Clear();
        switch (world)
        {
            case 1: // Forest Boss: Viele Onils + schneller Doll
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Onil, Count = 3 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Doll, Count = 2 });
                break;
            case 2: // Industrial Boss: Minvos auf Eis
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Minvo, Count = 3 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Kondoria, Count = 1 });
                break;
            case 3: // Cavern Boss: Kondorias + Ovapis mit Förderbändern
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Kondoria, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ovapi, Count = 2 });
                break;
            case 4: // Sky Boss: Pass + Teleporter-Chaos
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pass, Count = 3 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pontan, Count = 1 });
                break;
            case 5: // Inferno Final Boss: Alles auf Maximum
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pass, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pontan, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Kondoria, Count = 1 });
                break;
        }

        // Gute PowerUps für den Boss-Kampf
        level.PowerUps.Clear();
        level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.BombUp });
        level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Fire });
        level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Speed });
        if (world >= 3)
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Kick });
        if (world >= 4)
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Detonator });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BONUS-LEVEL (jedes 5. Level, außer Boss)
    // ═══════════════════════════════════════════════════════════════════════

    private static void ConfigureBonusLevel(Level level, int levelNumber, int world)
    {
        level.IsBonusLevel = true;
        level.TimeLimit = 45;
        int bonusType = (levelNumber / 5) % 4; // 4 verschiedene Bonus-Typen

        switch (bonusType)
        {
            case 0: // Coin-Rush: Viele schwache Gegner, viele PowerUps
                level.Name = "Bonus: Coin Rush";
                level.BlockDensity = 0.3f;
                level.Layout = LevelLayout.Arena;
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ballom, Count = 8 });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.BombUp });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Fire });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Speed });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Mystery });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.BombUp });
                break;

            case 1: // Speed-Run: Wenige Gegner, wenig Blöcke, schnell durchrennen
                level.Name = "Bonus: Speed Run";
                level.BlockDensity = 0.15f;
                level.TimeLimit = 30;
                level.Layout = LevelLayout.Cross;
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ballom, Count = 3 });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Speed });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Speed });
                break;

            case 2: // Demolition: Viele Blöcke sprengen, alle PowerUps drin
                level.Name = "Bonus: Demolition";
                level.BlockDensity = 0.7f;
                level.Layout = LevelLayout.Classic;
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ballom, Count = 4 });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Fire });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Fire });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.BombUp });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.BombUp });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Kick });
                break;

            case 3: // Mystery: Nur Mystery-PowerUps (Glück/Pech)
                level.Name = "Bonus: Mystery";
                level.BlockDensity = 0.4f;
                level.Layout = LevelLayout.Spiral;
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ballom, Count = 5 });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Mystery });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Mystery });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Mystery });
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Skull });
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GEGNER-KONFIGURATION (unverändert von vorher)
    // ═══════════════════════════════════════════════════════════════════════

    private static void ConfigureEnemies(Level level, int levelNumber)
    {
        level.Enemies.Clear();

        switch (levelNumber)
        {
            case >= 1 and <= 5:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ballom, Count = 2 + levelNumber / 2 });
                break;
            case >= 6 and <= 9:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ballom, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Onil, Count = 2 + (levelNumber - 6) / 2 });
                break;
            case >= 11 and <= 14:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Onil, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Doll, Count = 1 + (levelNumber - 11) / 2 });
                break;
            case >= 16 and <= 19:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Doll, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Minvo, Count = 2 + (levelNumber - 16) / 2 });
                break;
            case >= 21 and <= 24:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Minvo, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Kondoria, Count = 1 + (levelNumber - 21) / 2 });
                break;
            case >= 26 and <= 29:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Kondoria, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ovapi, Count = 2 + (levelNumber - 26) / 2 });
                break;
            case >= 31 and <= 34:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ovapi, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pass, Count = 1 + (levelNumber - 31) / 2 });
                break;
            case >= 36 and <= 39:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pass, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pontan, Count = 1 + (levelNumber - 36) / 3 });
                break;
            case >= 41 and <= 49:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Minvo, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Kondoria, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pass, Count = 1 + (levelNumber - 41) / 3 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pontan, Count = 1 });
                break;
        }
    }

    private static void ConfigurePowerUps(Level level, int levelNumber)
    {
        level.PowerUps.Clear();

        if (levelNumber <= 5)
        {
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.BombUp });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Fire });
        }
        else if (levelNumber <= 15)
        {
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.BombUp });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Fire });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Speed });
        }
        else if (levelNumber <= 25)
        {
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Fire });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Speed });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Kick });
            if (levelNumber >= 20)
                level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Skull });
        }
        else if (levelNumber <= 35)
        {
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Fire });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Wallpass });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.LineBomb });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Skull });
        }
        else
        {
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Fire });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.PowerBomb });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Flamepass });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Skull });
        }
    }

    private static void ConfigureBlockDensity(Level level, int levelNumber)
    {
        level.BlockDensity = 0.35f + (levelNumber / 50f) * 0.25f;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ARCADE
    // ═══════════════════════════════════════════════════════════════════════

    private static void ConfigureArcadeEnemies(Level level, int wave)
    {
        level.Enemies.Clear();

        int baseCount = Math.Min(2 + wave / 3, 5);

        if (wave <= 3)
        {
            level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ballom, Count = baseCount });
        }
        else if (wave <= 6)
        {
            level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ballom, Count = 1 });
            level.Enemies.Add(new EnemySpawn { Type = EnemyType.Onil, Count = baseCount - 1 });
        }
        else if (wave <= 10)
        {
            level.Enemies.Add(new EnemySpawn { Type = EnemyType.Onil, Count = 1 });
            level.Enemies.Add(new EnemySpawn { Type = EnemyType.Doll, Count = 1 });
            level.Enemies.Add(new EnemySpawn { Type = EnemyType.Minvo, Count = baseCount - 2 });
        }
        else
        {
            level.Enemies.Add(new EnemySpawn { Type = EnemyType.Minvo, Count = 1 });
            level.Enemies.Add(new EnemySpawn { Type = EnemyType.Kondoria, Count = 1 });
            level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pass, Count = 1 });
            if (wave >= 15)
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pontan, Count = 1 });
        }
    }

    private static void ConfigureArcadePowerUps(Level level, int wave)
    {
        level.PowerUps.Clear();

        var pool = new List<PowerUpType>
        {
            PowerUpType.BombUp, PowerUpType.Fire, PowerUpType.Speed,
            PowerUpType.Wallpass, PowerUpType.Detonator, PowerUpType.Bombpass,
            PowerUpType.Flamepass, PowerUpType.Mystery, PowerUpType.Kick
        };
        if (wave >= 5) pool.Add(PowerUpType.LineBomb);
        if (wave >= 8) pool.Add(PowerUpType.PowerBomb);

        var random = new Random(wave * 11111);
        int powerUpCount = Math.Max(1, 4 - wave / 5);

        for (int i = 0; i < powerUpCount; i++)
        {
            var type = pool[random.Next(pool.Count)];
            level.PowerUps.Add(new PowerUpPlacement { Type = type });
        }

        // Ab Wave 5: 40% Chance auf Skull (max 1)
        if (wave >= 5 && random.NextDouble() < 0.4)
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Skull });
    }
}
