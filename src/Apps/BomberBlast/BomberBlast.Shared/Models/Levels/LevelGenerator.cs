using BomberBlast.Models.Entities;

namespace BomberBlast.Models.Levels;

/// <summary>
/// Generiert Level mit Welt-spezifischen Mechaniken, variablen Layouts und Boss-Leveln.
/// 100 Story-Level in 10 Welten (je 10), Boss alle 10 Level, Bonus alle 5 Level.
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
        [LevelLayout.Diagonal, LevelLayout.Arena, LevelLayout.Spiral, LevelLayout.Maze],
        // Welt 6 (Ruinen): Labyrinthe und Symmetrie
        [LevelLayout.Labyrinth, LevelLayout.Symmetry, LevelLayout.Maze, LevelLayout.Classic],
        // Welt 7 (Ozean): Inseln und offene Flächen
        [LevelLayout.Islands, LevelLayout.Arena, LevelLayout.Cross, LevelLayout.Symmetry],
        // Welt 8 (Vulkan): Chaos und Spiralen
        [LevelLayout.Chaos, LevelLayout.Spiral, LevelLayout.Labyrinth, LevelLayout.Diagonal],
        // Welt 9 (Himmelsfestung): Symmetrie und Inseln
        [LevelLayout.Symmetry, LevelLayout.Islands, LevelLayout.Arena, LevelLayout.Labyrinth],
        // Welt 10 (Schattenwelt): Alles durcheinander
        [LevelLayout.Chaos, LevelLayout.Labyrinth, LevelLayout.Islands, LevelLayout.Symmetry]
    ];

    /// <summary>
    /// Generiert ein Level für eine bestimmte Levelnummer (1-100).
    /// highestCompleted filtert PowerUps auf freigeschaltete Typen.
    /// </summary>
    public static Level GenerateLevel(int levelNumber, int highestCompleted = int.MaxValue)
    {
        int world = GetWorld(levelNumber);

        // Timer skaliert nach Welt (frühe Level kürzer, späte länger)
        int baseTimer = world switch
        {
            0 => 130,   // Welt 1: Wenige einfache Gegner
            1 => 145,   // Welt 2: Etwas mehr Gegner
            2 => 160,   // Welt 3: Erste Mechaniken
            3 => 170,   // Welt 4: Teleporter
            4 => 180,   // Welt 5: Lava + komplexere Gegner
            5 => 185,   // Welt 6: Fallende Decke
            6 => 190,   // Welt 7: Strömung + Ghost/Splitter
            7 => 200,   // Welt 8: Erdbeben + Mimic
            8 => 210,   // Welt 9: Plattform-Lücken
            _ => 220    // Welt 10: Nebel + alle Mechaniken
        };

        var level = new Level
        {
            Number = levelNumber,
            Name = $"Stage {levelNumber}",
            TimeLimit = baseTimer,
            Seed = levelNumber * 12345
        };

        // Boss-Level: Jedes 10. Level
        if (levelNumber % 10 == 0 && levelNumber <= 100)
        {
            ConfigureBossLevel(level, levelNumber, world);
            FilterLockedPowerUps(level, highestCompleted);
            return level;
        }

        // Bonus-Level: Jedes 5. Level (aber nicht Boss-Level)
        if (levelNumber % 5 == 0 && levelNumber <= 100)
        {
            ConfigureBonusLevel(level, levelNumber, world);
            FilterLockedPowerUps(level, highestCompleted);
            return level;
        }

        // Normales Level
        ConfigureEnemies(level, levelNumber);
        ConfigurePowerUps(level, levelNumber);
        FilterLockedPowerUps(level, highestCompleted);
        ConfigureBlockDensity(level, levelNumber);

        // Welt-Mechanik zuweisen (ab Welt 2)
        AssignWorldMechanic(level, levelNumber, world);

        // Layout-Variation (nicht jedes Level Classic)
        AssignLayout(level, levelNumber, world);

        // Boss music für Welt 5+ und letzte Level jeder Welt
        if (world >= 5)
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
    /// Quick-Play-Level generieren (deterministisch via Seed, Schwierigkeit steuerbar).
    /// difficulty 1-10 mappt auf Welt-Schwierigkeit.
    /// </summary>
    public static Level GenerateQuickPlayLevel(int seed, int difficulty)
    {
        var random = new Random(seed);
        difficulty = Math.Clamp(difficulty, 1, 10);

        var level = new Level
        {
            Number = difficulty * 10, // Nutze Welt-Endpunkt als Basis (für Welt-Palette)
            Name = "Quick Play",
            TimeLimit = 180 - (difficulty - 1) * 10, // 180s (Diff 1) → 90s (Diff 10)
            Seed = seed,
            BlockDensity = 0.30f + difficulty * 0.025f // 0.325 → 0.55
        };

        // Gegner-Konfiguration basierend auf Schwierigkeit
        level.Enemies.Clear();
        ConfigureQuickPlayEnemies(level, difficulty, random);

        // Layout-Pattern zufällig (wie Daily Challenge, kein BossArena)
        var layouts = difficulty switch
        {
            <= 3 => new[] { LevelLayout.Classic, LevelLayout.Cross, LevelLayout.Arena, LevelLayout.TwoRooms },
            <= 6 => new[] { LevelLayout.Classic, LevelLayout.Cross, LevelLayout.Arena, LevelLayout.Maze, LevelLayout.Spiral, LevelLayout.Diagonal },
            _ => new[] { LevelLayout.Maze, LevelLayout.Spiral, LevelLayout.Diagonal, LevelLayout.Labyrinth, LevelLayout.Symmetry, LevelLayout.Islands, LevelLayout.Chaos }
        };
        level.Layout = layouts[random.Next(layouts.Length)];

        // Mechaniken basierend auf Schwierigkeit (ab Diff 3)
        if (difficulty >= 3)
        {
            var availableMechanics = difficulty switch
            {
                3 or 4 => new[] { WorldMechanic.None, WorldMechanic.Ice, WorldMechanic.Conveyor },
                5 or 6 => new[] { WorldMechanic.None, WorldMechanic.Ice, WorldMechanic.Conveyor, WorldMechanic.Teleporter, WorldMechanic.LavaCrack },
                7 or 8 => new[] { WorldMechanic.Ice, WorldMechanic.Conveyor, WorldMechanic.Teleporter, WorldMechanic.LavaCrack, WorldMechanic.FallingCeiling, WorldMechanic.Current },
                _ => new[] { WorldMechanic.Conveyor, WorldMechanic.Teleporter, WorldMechanic.LavaCrack, WorldMechanic.FallingCeiling, WorldMechanic.Current, WorldMechanic.Earthquake, WorldMechanic.PlatformGap, WorldMechanic.Fog }
            };
            level.Mechanic = availableMechanics[random.Next(availableMechanics.Length)];
        }

        // PowerUps: Basis immer vorhanden, Extra basierend auf Schwierigkeit
        level.PowerUps.Clear();
        level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.BombUp });
        level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Fire });
        level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Speed });

        if (difficulty >= 3)
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Kick });

        // 50% Chance auf ein Extra-PowerUp (wie Daily Challenge)
        if (random.NextDouble() > 0.5)
        {
            var extraPool = difficulty switch
            {
                <= 4 => new[] { PowerUpType.Kick, PowerUpType.Wallpass, PowerUpType.Detonator },
                <= 7 => new[] { PowerUpType.Wallpass, PowerUpType.Detonator, PowerUpType.LineBomb, PowerUpType.Kick },
                _ => new[] { PowerUpType.Detonator, PowerUpType.LineBomb, PowerUpType.PowerBomb, PowerUpType.Flamepass }
            };
            level.PowerUps.Add(new PowerUpPlacement { Type = extraPool[random.Next(extraPool.Length)] });
        }

        // Ab Schwierigkeit 5: Chance auf Skull
        if (difficulty >= 5 && random.NextDouble() < 0.4)
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Skull });

        // Kein Boss (BossKind = null)
        // Boss-Music ab Schwierigkeit 8
        if (difficulty >= 8)
            level.MusicTrack = "boss";

        return level;
    }

    /// <summary>
    /// Quick-Play Gegner basierend auf Schwierigkeit konfigurieren
    /// </summary>
    private static void ConfigureQuickPlayEnemies(Level level, int difficulty, Random random)
    {
        // Difficulty 1-2: Ballom, Onil (2-3 Gegner)
        // Difficulty 3-4: + Doll, Minvo (3-4 Gegner)
        // Difficulty 5-6: + Ovapi, Kondoria (4-5 Gegner)
        // Difficulty 7-8: + Tanker, Ghost (5-6 Gegner)
        // Difficulty 9-10: + Splitter, Mimic, Pontan (6-8 Gegner)
        var enemyPool = difficulty switch
        {
            1 or 2 => new[] { EnemyType.Ballom, EnemyType.Onil },
            3 or 4 => new[] { EnemyType.Onil, EnemyType.Doll, EnemyType.Minvo, EnemyType.Ballom },
            5 or 6 => new[] { EnemyType.Doll, EnemyType.Minvo, EnemyType.Ovapi, EnemyType.Kondoria },
            7 or 8 => new[] { EnemyType.Minvo, EnemyType.Kondoria, EnemyType.Ovapi, EnemyType.Tanker, EnemyType.Ghost },
            _ => new[] { EnemyType.Tanker, EnemyType.Ghost, EnemyType.Splitter, EnemyType.Mimic, EnemyType.Pontan, EnemyType.Kondoria }
        };

        int totalEnemies = difficulty switch
        {
            1 or 2 => 2 + random.Next(2), // 2-3
            3 or 4 => 3 + random.Next(2), // 3-4
            5 or 6 => 4 + random.Next(2), // 4-5
            7 or 8 => 5 + random.Next(2), // 5-6
            _ => 6 + random.Next(3)        // 6-8
        };

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
    }

    /// <summary>
    /// Survival-Level generieren: Offene Arena, wenige Blöcke, kein Exit.
    /// Gegner spawnen kontinuierlich über die GameEngine.
    /// </summary>
    public static Level GenerateSurvivalLevel()
    {
        var level = new Level
        {
            Number = 0,
            Name = "Survival",
            TimeLimit = 99999, // Effektiv kein Zeitlimit (Timer zählt nicht herunter)
            Seed = Environment.TickCount,
            BlockDensity = 0.2f, // Wenige Blöcke für offene Arena
            Layout = LevelLayout.Arena
        };

        // Nur 2 leichte Startgegner (weitere spawnen über UpdateSurvivalSpawning)
        level.Enemies.Clear();
        level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ballom, Count = 2 });

        // Basis-PowerUps in Blöcken
        level.PowerUps.Clear();
        level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.BombUp });
        level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Fire });
        level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Speed });
        level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Kick });

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
            6 when levelInWorld >= 3 => WorldMechanic.FallingCeiling,
            7 when levelInWorld >= 3 => WorldMechanic.Current,
            8 when levelInWorld >= 3 => WorldMechanic.Earthquake,
            9 when levelInWorld >= 3 => WorldMechanic.PlatformGap,
            10 when levelInWorld >= 2 => WorldMechanic.Fog,
            _ => WorldMechanic.None
        };
    }

    private static void AssignLayout(Level level, int levelNumber, int world)
    {
        int levelInWorld = ((levelNumber - 1) % 10) + 1;
        int layoutIndex = Math.Clamp(world - 1, 0, WorldLayouts.Length - 1);

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
            6 => WorldMechanic.FallingCeiling,
            7 => WorldMechanic.Current,
            8 => WorldMechanic.Earthquake,
            9 => WorldMechanic.PlatformGap,
            10 => WorldMechanic.Fog,
            _ => WorldMechanic.None
        };

        // Boss-Typ basierend auf Welt (Repeat alle 2 Welten)
        level.BossKind = world switch
        {
            1 or 2 => BossType.StoneGolem,
            3 or 4 => BossType.IceDragon,
            5 or 6 => BossType.FireDemon,
            7 or 8 => BossType.ShadowMaster,
            9 or 10 => BossType.FinalBoss,
            _ => BossType.StoneGolem
        };

        // Begleit-Gegner je nach Boss-Level
        level.Enemies.Clear();
        switch (world)
        {
            case 1: // L10: StoneGolem + leichte Gegner
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ballom, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Onil, Count = 1 });
                break;
            case 2: // L20: StoneGolem (Repeat) + mehr Gegner
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Doll, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Minvo, Count = 2 });
                break;
            case 3: // L30: IceDragon
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Onil, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Kondoria, Count = 1 });
                break;
            case 4: // L40: IceDragon (Repeat)
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pass, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Doll, Count = 2 });
                break;
            case 5: // L50: FireDemon
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Minvo, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ovapi, Count = 1 });
                break;
            case 6: // L60: FireDemon (Repeat)
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Tanker, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ghost, Count = 1 });
                break;
            case 7: // L70: ShadowMaster
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ghost, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Splitter, Count = 1 });
                break;
            case 8: // L80: ShadowMaster (Repeat)
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Mimic, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pass, Count = 2 });
                break;
            case 9: // L90: FinalBoss
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Tanker, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ghost, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Splitter, Count = 1 });
                break;
            case 10: // L100: FinalBoss (Repeat) - Maximale Schwierigkeit
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Tanker, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ghost, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Mimic, Count = 2 });
                level.TimeLimit = 300; // Mehr Zeit für Endboss
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
        if (world >= 7)
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.PowerBomb });
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

            // === Welt 6 (Ruinen): Tanker einführen ===
            case >= 51 and <= 54:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pass, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Tanker, Count = 1 + (levelNumber - 51) / 2 });
                break;
            case >= 56 and <= 59:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Tanker, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pass, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pontan, Count = 1 });
                break;

            // === Welt 7 (Ozean): Ghost + Splitter einführen ===
            case >= 61 and <= 64:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ghost, Count = 1 + (levelNumber - 61) / 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Splitter, Count = 2 });
                break;
            case >= 66 and <= 69:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ghost, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Splitter, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Tanker, Count = 1 });
                break;

            // === Welt 8 (Vulkan): Mimic einführen ===
            case >= 71 and <= 74:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Mimic, Count = 1 + (levelNumber - 71) / 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Tanker, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pass, Count = 1 });
                break;
            case >= 76 and <= 79:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Mimic, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ghost, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pontan, Count = 1 });
                break;

            // === Welt 9 (Himmelsfestung): Alle neuen Typen ===
            case >= 81 and <= 84:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ghost, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Tanker, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Splitter, Count = 1 + (levelNumber - 81) / 2 });
                break;
            case >= 86 and <= 89:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ghost, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Mimic, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pontan, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Tanker, Count = 1 });
                break;

            // === Welt 10 (Schattenwelt): Maximale Schwierigkeit ===
            case >= 91 and <= 94:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ghost, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Mimic, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Tanker, Count = 1 + (levelNumber - 91) / 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Pontan, Count = 1 });
                break;
            case >= 96 and <= 99:
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Tanker, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Ghost, Count = 2 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Splitter, Count = 1 });
                level.Enemies.Add(new EnemySpawn { Type = EnemyType.Mimic, Count = 1 });
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
        else if (levelNumber <= 50)
        {
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Fire });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.PowerBomb });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Flamepass });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Skull });
        }
        else if (levelNumber <= 70)
        {
            // Welt 6-7: Stärkere PowerUps, mehr Auswahl
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Fire });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Speed });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Kick });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.PowerBomb });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Skull });
        }
        else if (levelNumber <= 90)
        {
            // Welt 8-9: PowerBombs + LineBombs + Flamepass
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Fire });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.LineBomb });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.PowerBomb });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Flamepass });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Skull });
        }
        else
        {
            // Welt 10: Alles, auch gefährlich
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.PowerBomb });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.LineBomb });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Flamepass });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Detonator });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Skull });
            level.PowerUps.Add(new PowerUpPlacement { Type = PowerUpType.Skull });
        }
    }

    /// <summary>
    /// Entfernt PowerUps die noch nicht freigeschaltet sind (basierend auf höchstem abgeschlossenem Level).
    /// </summary>
    private static void FilterLockedPowerUps(Level level, int highestCompleted)
    {
        if (highestCompleted >= int.MaxValue) return; // Kein Filter nötig
        level.PowerUps.RemoveAll(p => p.Type.GetUnlockLevel() > Math.Max(highestCompleted, level.Number));
    }

    private static void ConfigureBlockDensity(Level level, int levelNumber)
    {
        // Skaliert von 0.35 (Level 1) bis 0.60 (Level 100), begrenzt für Spielbarkeit
        level.BlockDensity = Math.Min(0.65f, 0.35f + (levelNumber / 100f) * 0.30f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DUNGEON-RUN (Roguelike-Floors)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generiert einen Dungeon-Floor mit steigender Schwierigkeit.
    /// Floor 5/10/15/... = Boss-Floors.
    /// </summary>
    public static Level GenerateDungeonFloor(int floor, int seed)
    {
        var rng = new Random(seed + floor * 1000);

        bool isBoss = floor % 5 == 0;
        bool isMiniBoss = floor % 10 == 5;
        bool isEndBoss = floor % 10 == 0;

        // Schwierigkeits-Skalierung: Floor 1 = leicht, Floor 10 = hart, ab 11 noch härter
        float difficultyScale = Math.Min(floor / 10f, 1f);
        float bonusScale = floor > 10 ? 1f + (floor - 10) * 0.05f : 1f;

        // Block-Dichte steigt mit Floor
        float blockDensity = 0.3f + difficultyScale * 0.2f;

        // Timer kürzer bei höheren Floors
        int timeLimit = Math.Max(90, 180 - floor * 8);

        // Layout-Pattern basierend auf Floor
        var patterns = new[] { LevelLayout.Classic, LevelLayout.Cross, LevelLayout.Arena, LevelLayout.Maze, LevelLayout.TwoRooms };
        LevelLayout layout = isBoss ? LevelLayout.BossArena : patterns[rng.Next(patterns.Length)];

        // Gegner-Pool basierend auf Floor
        var enemyPool = GetDungeonEnemyPool(floor);
        int enemyCount = isBoss ? 2 : Math.Min(3 + floor / 2, 8);

        var enemies = new List<EnemySpawn>();
        for (int i = 0; i < enemyCount; i++)
        {
            var type = enemyPool[rng.Next(enemyPool.Length)];
            // Zusammenfassen wenn gleicher Typ schon vorhanden
            var existing = enemies.Find(e => e.Type == type);
            if (existing != null)
                existing.Count++;
            else
                enemies.Add(new EnemySpawn { Type = type, Count = 1 });
        }

        // Mechaniken ab Floor 3
        WorldMechanic mechanic = WorldMechanic.None;
        if (floor >= 3 && rng.NextDouble() < 0.4 + difficultyScale * 0.3)
        {
            var availableMechanics = new[] { WorldMechanic.Ice, WorldMechanic.Conveyor, WorldMechanic.Teleporter, WorldMechanic.LavaCrack };
            mechanic = availableMechanics[rng.Next(availableMechanics.Length)];
        }
        if (floor >= 7 && mechanic == WorldMechanic.None && rng.NextDouble() < 0.3)
        {
            mechanic = WorldMechanic.Fog;
        }

        // Boss-Level
        BossType? bossKind = null;
        if (isBoss)
        {
            if (isEndBoss)
            {
                // End-Boss rotiert durch alle 5 Typen
                bossKind = (BossType)((floor / 10 - 1) % 5);
            }
            else if (isMiniBoss)
            {
                // Mini-Boss: StoneGolem oder IceDragon (leichtere)
                bossKind = rng.Next(2) == 0 ? BossType.StoneGolem : BossType.IceDragon;
            }
        }

        // PowerUps basierend auf Floor
        var powerUps = new List<PowerUpPlacement>
        {
            new() { Type = PowerUpType.BombUp },
            new() { Type = PowerUpType.Fire },
            new() { Type = PowerUpType.Speed }
        };

        if (floor >= 3)
            powerUps.Add(new PowerUpPlacement { Type = PowerUpType.Kick });
        if (floor >= 5 && rng.NextDouble() < 0.5)
            powerUps.Add(new PowerUpPlacement { Type = PowerUpType.Wallpass });
        if (floor >= 7 && rng.NextDouble() < 0.4)
            powerUps.Add(new PowerUpPlacement { Type = PowerUpType.Detonator });

        var level = new Level
        {
            Number = floor,
            Name = $"Floor {floor}",
            TimeLimit = timeLimit,
            Seed = seed + floor * 1000,
            BlockDensity = blockDensity,
            Layout = layout,
            Mechanic = mechanic,
            MusicTrack = isBoss ? "boss" : "gameplay",
            BossKind = bossKind
        };

        level.Enemies.Clear();
        level.Enemies.AddRange(enemies);
        level.PowerUps.Clear();
        level.PowerUps.AddRange(powerUps);

        return level;
    }

    /// <summary>
    /// Gegner-Pool basierend auf Dungeon-Floor-Nummer
    /// </summary>
    private static EnemyType[] GetDungeonEnemyPool(int floor)
    {
        return floor switch
        {
            <= 2 => [EnemyType.Ballom, EnemyType.Onil],
            <= 4 => [EnemyType.Onil, EnemyType.Doll, EnemyType.Minvo, EnemyType.Ballom],
            <= 6 => [EnemyType.Doll, EnemyType.Minvo, EnemyType.Ovapi, EnemyType.Kondoria],
            <= 8 => [EnemyType.Minvo, EnemyType.Ovapi, EnemyType.Kondoria, EnemyType.Tanker, EnemyType.Ghost],
            _ => [EnemyType.Ovapi, EnemyType.Tanker, EnemyType.Ghost, EnemyType.Splitter, EnemyType.Mimic, EnemyType.Pontan]
        };
    }
}
