# Core — GameEngine & Simulation

Herz der Spiellogik. `GameEngine` (~7.450 LOC, 5 Partials), Modes, Combat, Audio, Multiplayer-
und Replay-Foundation. Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

---

## GameEngine (Partial-Klassen)

| Datei | Verantwortung |
|-------|--------------|
| `GameEngine.cs` | DI-Felder, Events (kein `On`-Prefix), `Update()`-Loop, Dispose-Chain |
| `GameEngine.Collision.cs` | Spieler/Gegner-Kollision, `EnemyPositionIndex`-Nutzung |
| `GameEngine.Explosion.cs` | Bombe-Zündung, `SpecialExplosionEffects`-Delegation, Kettenreaktion |
| `GameEngine.Level.cs` | Level-Start/Complete/Victory, Boss-Reveal-Cinematic, Mode-Dispatch |
| `GameEngine.Render.cs` | Delegiert an `GameRenderer`, setzt Renderer-Properties pro Frame |

### Game Loop

```
DispatcherTimer (16ms) → GameView.OnTimerTick()
    → GameEngine.Update(deltaTime)          # Physik, AI, Bomben, State
    → canvas.InvalidateSurface()            # Triggert PaintSurface
    → GameEngine.RenderFrame(canvas)        # Render-Delegation
```

`MAX_DELTA_TIME = 0.05f` (50ms Cap, Spiral-of-Death-Schutz).

### Render-Lifecycle-Robustheit

`GameEngine.Render` ist mit `try/finally` + SaveCount-Backstop umschlossen → Sub-Render-
Exceptions hängen keinen SaveLayer-Stack auf. `InputManager.Dispose()` ist idempotent
(`_disposed`-Guard) — GameEngine disposes ihn **nicht** (DI-Container-Lifetime).

---

## IGameMode-Plugin-Framework

```csharp
interface IGameMode {
    string ModeTag { get; }
    void Initialize(GameModeContext ctx);
    void UpdateLogic(float deltaTime, GameModeContext ctx);
    bool OnLevelComplete(GameModeContext ctx);
    void OnGameOver(GameModeContext ctx);
    void Cleanup(GameModeContext ctx);
}
```

8 Implementierungen in `Modes/GameModes.cs` (alle erben von `GameModeBase` mit no-op-Defaults):
`StoryMode`, `MasterMode`, `DailyChallengeMode`, `QuickPlayMode`, `SurvivalMode`,
`DungeonMode`, `BossRushMode`, `DailyRaceMode`.

**Property-Alias-Pattern für State-Migration** (DungeonMode):

```csharp
private bool _phantomWalkActive {
    get => DungeonModeState?.PhantomWalkActive ?? false;
    set { if (DungeonModeState is { } d) d.PhantomWalkActive = value; }
}
private DungeonMode? DungeonModeState => _currentMode as DungeonMode;
```

30+ Aufrufstellen bleiben unverändert, State lebt im Mode-Objekt.

**Wichtig**: `UpdateLogic`/`OnLevelComplete`-Hooks werden aktuell aufgerufen
(`GameEngine.Update` ruft `_currentMode?.UpdateLogic` + `OnGameOver`). Bool-Flag-Routing-
Switch bleibt als Hot-Path-Convenience — Property-Match wäre pro Frame teurer.

---

## Unterordner

### `Modes/`

| Datei | Zweck |
|-------|-------|
| `IGameMode.cs` | Plugin-Interface |
| `GameModes.cs` | 8 Implementierungen + `GameModeBase` |
| `SurvivalSpawner.cs` | Static, zustandslos — `SurvivalMode` hält den State |

### `Combat/`

| Datei | Zweck |
|-------|-------|
| `ComboSystem.cs` | Instanz-Klasse: Kill-Fenster, Score-Bonus, Slow-Mo-Trigger. In `GameEngine` als `_comboSystem`-Field. `_comboCount/_comboTimer` in GameEngine sind read-only Aliases → Renderer-Kompatibilität |
| `SpecialExplosionEffects.cs` | Static, 13 `Handle*`-Methoden, `ExplosionEffectsContext` (Callback-Delegates für Engine-Mutations) |
| `EnemyPositionIndex.cs` | Singleton (DI), O(1)-Spatial-Lookup via Dirty-Flag-Rebuild |

### `LevelGeneration/`

| Datei | Zweck |
|-------|-------|
| `ILevelGenerator.cs` | Interface: `PlacePowerUps`, `PlaceExit`, `SpawnEnemies`, `SpawnBossAtPosition` |
| `LevelGenerator.cs` | DI-Singleton-Impl (`AddSingleton<ILevelGenerator, LevelGenerator>()`). Gepoolte interne Listen (`_blockCells`, `_farBlocks`, `_validPositions`). |
| `MutatorEffects.cs` | Static, pure Funktion, Context als Parameter. 5 Mutator-Typen (ab Welt 6, Level x3/x6/x9). |

**Namens-Klarstellung**: `Core/LevelGeneration/LevelGenerator` ≠ `Models/Levels/LevelLayoutGenerator`.
`LevelLayoutGenerator` ist static, ohne DI, bestimmt Layout + Mutator + Boss-Typ per Level-Nummer.

### `Audio/`

| Datei | Zweck |
|-------|-------|
| `AudioBus.cs` | 7-Kanal-Volume-Bus (Master/Music/Ambient/Sfx/Ui/Voice/Cinematic) |
| `AudioBusMixer.cs` | Duck-API (`Duck(bus, multiplier, duration)`) + Boost-API (`Boost(bus, multiplier, duration)`, Cap 1.5). Recovery: Duck linear 2.0/s, Boost linear 0.5/s |
| `SoundVariationPool.cs` | Anti-Repeat-Pool (Brawl-Stars-Pattern). Suffix `_a/_b/_c/_d`. |
| `AudioSpatial.cs` | Stereo-Pan via GridX/Grid.Width. Distance-Falloff, Equal-Power-Crossfade. |

### `Dungeon/`

| Datei | Zweck |
|-------|-------|
| `DungeonSynergyResolver.cs` | Static, pure Funktion — wertet 5 Synergie-Regeln (Bombardier/Blitzkrieg/Festung/Midas/Elementar) |

### `Multiplayer/`

Nur Foundation. Engine-Integration ist eigener Sprint.

| Datei | Zweck |
|-------|-------|
| `MultiplayerMode.cs` | `MultiplayerMode`-Enum + `PlayerSlot` + `MultiplayerSpawnPositions` |
| `PlayerInputSnapshot.cs` | 2-Byte-Wire-Format (Slot+Direction+Bomb+Detonate+ToggleSpecial) |
| `InputBuffer.cs` | Ring-Buffer (120 Ticks): `Push`/`PeekLatest`/`PeekHistorical`/`Clear` |
| `GameStateSnapshot.cs` | P1+P2-State + RNG-State + FNV-1a-Hash. Anti-Cheat via Hash-Vergleich. |

### `Diagnostics/`

| Datei | Zweck |
|-------|-------|
| `GameEngineEventSource.cs` | ETW/EventSource "BomberBlast-Engine". 11 Events über 7 Keywords. Aktivierbar via `dotnet-trace --providers BomberBlast-Engine`. |

### Weitere Core-Dateien

| Datei | Zweck |
|-------|-------|
| `GameState.cs` | `GameState`-Enum (Menu/Playing/Paused/GameOver/…) |
| `GameTimer.cs` | Ablauf-Timer mit Warning/Expired-Events |
| `GameLoopSettings.cs` | TargetFps (30/60) via `IPreferencesService`. `Initialize()` in App.axaml.cs aufrufen. |
| `SoundManager.cs` | `PlayPooled()`, `PlayAt(grid)`, `PlayStinger(key)`, `PlayVoice(key)`. ±5% Pitch + ±10% Volume-Variation. |
| `DeterministicRandom.cs` | xoshiro256+, Public Domain, SplitMix64-Seed. `GetState/SetState` für Replay. |
| `FixedTimestepRunner.cs` | 60-Hz-Akkumulator. Via Flag im Engine-Update aktivierbar (Foundation, nicht default). |
| `IRngProvider.cs` | Interface für deterministischen RNG. DI: `DeterministicRngProvider`. Visual-Random bleibt `SystemRngProvider`. |
| `ReplayCapture.cs` | 1-Byte/Tick Input-Stream. Schema-V1. 108k-Tick-Cap = 30 min @ 60 Hz, ~5-10 KB RLE. |

---

## Wichtige Gotchas

### StartRenderLoop() — kein StopRenderLoop()

In `StartRenderLoop()` NUR `_renderTimer?.Stop()` aufrufen.
`StopRenderLoop()` setzt `_gameCanvas = null` → Render-Loop-Tod nach nächstem Tick.

### Cinematic.Stop() bei Mode-Wechsel

Als **erste Zeile** in allen `StartXxxModeAsync()`-Methoden — verhindert weiterlaufende
Boss-Reveal-Cinematics nach Mode-Wechsel.

### GC.GetTotalMemory auf Background-Thread

Niemals auf dem UI-Thread aufrufen — 1-5ms Frame-Spike auf Mono-AOT-Android.
Immer via `Task.Run` auf Background-Thread.
