# BomberBlast — Bomberman-Klon (SkiaSharp)

Vollständige 2D-Spiel-Engine im Bomberman-Stil. Landscape-only auf Android.
Grid 15×10. Zwei Visual-Styles: Classic HD + Neon/Cyberpunk. SkiaSharp-Rendering,
eigenes Icon-System, AI-Pathfinding, Roguelike-Dungeon-Modus und Liga-System.

| Aspekt | Wert |
|--------|------|
| Version | v2.0.56 (VersionCode 66) |
| Package-ID | org.rsdigital.bomberblast |
| Status | Produktion |
| Premium-Modell | 1,99 EUR `remove_ads` |
| Mandat | Code-only / CC0 / prozedural — keine externen Audio-/Art-Pipelines, keine Voice-Talents |

---

## Projektstruktur

```
src/Apps/BomberBlast/
├── BomberBlast.Shared/
│   ├── Core/                    # GameEngine (Partial-Classes), Models, Services, AI
│   │   ├── GameEngine.cs        # Kern: DI-Felder, Events, Update-Loop, Dispose
│   │   ├── GameEngine.Collision.cs
│   │   ├── GameEngine.Explosion.cs
│   │   ├── GameEngine.Level.cs  # Level-Start, Mode-Dispatch, LevelComplete, Victory
│   │   ├── GameEngine.Render.cs # Render-Delegation an GameRenderer
│   │   ├── GameTimer.cs         # Ablauf-Timer mit Warning/Expired-Events
│   │   ├── Modes/               # IGameMode-Implementierungen (8 Modi + IGameMode)
│   │   ├── Combat/              # ComboSystem, SpecialExplosionEffects, EnemyPositionIndex
│   │   ├── LevelGeneration/     # LevelGenerator (ILevelGenerator-Impl.), ILevelGenerator, MutatorEffects
│   │   ├── Audio/               # AudioBus, AudioBusMixer, SoundVariationPool, AudioSpatial
│   │   └── Dungeon/             # DungeonSynergyResolver
│   ├── Graphics/                # Alle Renderer
│   │   ├── GameRenderer.cs              # Kern: Palette, Viewport, SaveLayer-Stack
│   │   ├── GameRenderer.Grid.cs         # Dispatcher: delegiert an .Tiles/.Blocks/.GridFx
│   │   ├── GameRenderer.Grid.Tiles.cs
│   │   ├── GameRenderer.Grid.Blocks.cs
│   │   ├── GameRenderer.Grid.GridFx.cs  # FogOverlay, Transitions, Afterglow
│   │   ├── GameRenderer.Characters.cs
│   │   ├── GameRenderer.Bosses.cs
│   │   ├── GameRenderer.Items.cs        # Bomben, PowerUps, Exit
│   │   ├── GameRenderer.Atmosphere.cs
│   │   ├── GameRenderer.HUD.cs          # Side-Panel rechts mit Time/Score/Combo/Lives/Deck
│   │   ├── GameRenderer.Events.cs       # Saisonale Partikel-Overlays (Halloween/Christmas/NewYear/Summer)
│   │   ├── CinematicSequencer.cs        # Lightweight-Event-Sequencer für Boss-Reveal/Victory
│   │   ├── SubtitleSystem.cs            # Struct-Pool-Captions (max 4 aktiv)
│   │   ├── FogOfWarSystem.cs            # 3-Zustand Memory-FoW für L50+/Master-Mode
│   │   ├── MenuBackgroundRenderer.cs    # 7 Themes, struct-Pool 60 Partikel
│   │   ├── UltraComboFlash.cs           # Vignette-Flash (ULTRA-Combo + Damage-Flash, /#18)
│   │   ├── OutlineRenderHelper.cs       # Outline-Pass via Dilate-ImageFilter (
│   │   └── ...weitere Renderer
│   ├── Icons/                   # Eigenes Neon-Arcade Icon-System (152 Icons)
│   ├── Input/                   # NeonJoystick, InputManager
│   ├── Models/                  # Entities, Level, Dungeon, PowerUp, Bomb-Typen
│   │   └── Levels/LevelLayoutGenerator.cs  # Static: Welt-Layout-Rotation, Boss/Bonus-Level-Config
│   ├── Navigation/              # NavigationCoordinator, BottomTabController, NavigationRouteParser (MainViewModel-Feature-Module)
│   ├── Services/                # Services (alle als Interface), inkl. DialogPresenter, ILogger-Provider
│   └── ViewModels/              # ViewModels (Singletons), inkl. ChildViewModelRegistry + LifecycleHub (MainViewModel-Feature-Module)
├── BomberBlast.Android/
└── BomberBlast.Desktop/
```

---

## GameEngine-Architektur

### Partial-Class-Struktur

Die `GameEngine` ist eine God-Class (~5.100 LOC), aufgeteilt in 5 Partial-Files:

| Partial | Verantwortung |
|---------|--------------|
| `GameEngine.cs` | DI-Felder, Events (kein `On`-Prefix), `Update()`-Loop, Dispose-Chain |
| `GameEngine.Collision.cs` | Spieler/Gegner-Kollision, `EnemyPositionIndex`-Nutzung |
| `GameEngine.Explosion.cs` | Bombe-Zündung, SpecialExplosionEffects-Delegation, Kettenreaktion |
| `GameEngine.Level.cs` | Level-Start/Complete/Victory, Boss-Reveal-Cinematic, Mode-Dispatch |
| `GameEngine.Render.cs` | Delegiert an `GameRenderer`, setzt Renderer-Properties pro Frame |

Sonstige Kern-Files in `Core/`:

| Datei | Zweck |
|-------|-------|
| `SoundManager.cs` | Pitch/Pan-Variation-Wrapper für ISoundService |
| `GameLoopSettings.cs` | TargetFps (30/60) via IPreferencesService |
| `GameState.cs` | State-Enum (Menu/Playing/Paused/GameOver/…) |
| `FixedTimestepRunner.cs` | 60-Hz-Akkumulator (Foundation, nicht integriert) |
| `Audio/AudioBus.cs` | Volume-Bus-System (Master/SFX/Music) |
| `Audio/AudioBusMixer.cs` | Bus-Mixing-Logic |
| `Audio/SoundVariationPool.cs` | Pitch/Volume-Variation-Pool für wiederholte SFX |
| `Audio/AudioSpatial.cs` | Stereo-Pan-Berechnung (GridX → Pan) |

**Extrahierte Pure-Logic-Klassen** (−836 Zeilen aus GameEngine):

| Klasse | Pfad | Pattern |
|--------|------|---------|
| `LevelGenerator` | `Core/LevelGeneration/` | Singleton via DI (`ILevelGenerator`), gepoolte interne Listen |
| `MutatorEffects` | `Core/LevelGeneration/` | Static, pure Funktion, Context als Parameter |
| `SpecialExplosionEffects` | `Core/Combat/` | Static, 13 Handle*-Methoden, ExplosionEffectsContext |
| `EnemyPositionIndex` | `Core/Combat/` | Singleton, O(1)-Lookup, Lazy-Rebuild per Dirty-Flag |
| `SurvivalSpawner` | `Core/Modes/` | Static, zustandslos, SurvivalMode hält State |
| `DungeonSynergyResolver` | `Core/Dungeon/` | Static, pure Funktion, wertet 5 Synergie-Regeln |
| `ComboSystem` | `Core/Combat/` | Instanz-Klasse, per `_comboSystem`-Field in GameEngine |

**Namens-Klarstellung** — zwei verschiedene Klassen mit ähnlichem Namen:
- `Core/LevelGeneration/LevelGenerator` — DI-Singleton, erzeugt Level-Inhalte (PowerUps, Exit, Spawns).
  Implementiert `ILevelGenerator`. Registriert als `services.AddSingleton<ILevelGenerator, LevelGenerator>()`.
- `Models/Levels/LevelLayoutGenerator` — Static-Klasse, bestimmt welches Layout + Mutator + Boss-Typ
  ein Level bekommt. Keine DI, kein State. Wird von `GameEngine.Level.cs` direkt aufgerufen.

**Callback-Pattern für Engine-Mutationen** (in `ExplosionEffectsContext`):
`DestroyBlock`, `KillEnemy`, `ProcessExplosion` als Delegates — diese mutieren engine-interne
Score/Events/State-Machine und gehören nicht in eine Extract-Datei.

### IGameMode-Pattern

```csharp
// Core/Modes/IGameMode.cs — Mode-Plugin-Framework
interface IGameMode {
    string ModeTag { get; }
    void Initialize(GameModeContext ctx);
    void UpdateLogic(float deltaTime, GameModeContext ctx);  // läuft parallel zu Bool-Flags
    bool OnLevelComplete(GameModeContext ctx);               // true → Engine feuert Event, false → Mode managed selbst
    void OnGameOver(GameModeContext ctx);
    void Cleanup(GameModeContext ctx);
}
```

8 konkrete Implementierungen in `Core/Modes/GameModes.cs`:
`StoryMode`, `MasterMode`, `DailyChallengeMode`, `QuickPlayMode`, `SurvivalMode`,
`DungeonMode`, `BossRushMode`, `DailyRaceMode` — alle erben von `GameModeBase`
(no-op-Defaults).

**Status**: Mode-Klassen laufen parallel zu den existierenden Bool-Flags
(`_isStoryMode`, `_isSurvivalMode`, etc.). Tatsächliche Mode-Logic-Migration aus GameEngine
in Mode-Klassen ist ein eigener Sprint. Neue Modi MÜSSEN `IGameMode` implementieren — kein
weiterer Bool-Flag in `GameEngine.cs`.

**Property-Alias-Pattern für State-Migration**: DungeonMode-State (13 Felder) lebt in
`DungeonMode`-Klasse. GameEngine hat private Properties mit identischem Namen die intern
auf `_currentMode as DungeonMode` delegieren. 30+ Aufrufstellen bleiben unverändert.

```csharp
private bool _phantomWalkActive {
    get => DungeonModeState?.PhantomWalkActive ?? false;
    set { if (DungeonModeState is { } d) d.PhantomWalkActive = value; }
}
private DungeonMode? DungeonModeState => _currentMode as DungeonMode;
```

**Bool-Flags bleiben** (`_isBossRushMode`, `_isDailyRace` etc.) als Hot-Path-Convenience —
Pattern-Match wäre pro Frame teurer. State wandert in Mode-Klassen; Routing-Switch bleibt am Bool.

**WICHTIG**: `UpdateLogic`/`OnLevelComplete`-Hooks werden aktuell NICHT aus dem Engine-Update-Loop
aufgerufen. Foundation steht, Migration existierender Mode-Logic ist ein eigener Sprint.

### Game Loop + Render Loop

```
DispatcherTimer (16ms) → GameView.OnTimerTick()
    → GameEngine.Update(deltaTime)          # Physik, AI, Bomben, State
    → canvas.InvalidateSurface()            # Triggert PaintSurface
    → GameEngine.RenderFrame(canvas)        # Render-Delegation
```

- `MAX_DELTA_TIME = 0.05f` (50ms Cap gegen Spiral-of-Death)
- `GameLoopSettings.TargetFps` (30/60 FPS) via `IPreferencesService` → `_renderTimer.Interval`
- `FixedTimestepRunner.cs` existiert als Foundation (60-Hz-Akkumulator), aber **nicht** integriert
  (bleibt Voraussetzung für Replay/Anti-Cheat/Async-PvP)

### DI-Konfiguration

- **25 ViewModels** (alle Singleton), **37 Services** (alle Singleton)
- **15 spät-unlocked Child-VMs** als `Lazy<T>` injiziert (ShopVM, AchievementsVM, DeckVM usw.)
  → verwaltet vom `ChildViewModelRegistry` (siehe MainViewModel-Kompositor unten)
- **11 Eager-VMs** für frühe Interaktion: MainMenu, Game, LevelSelect, Settings, Help,
  HighScores, GameOver, Victory, BossRush, PlayHub, BottomTabBar
- **Zirkuläre Abhängigkeiten** via `Lazy<T>` + `LazyServiceExtensions.cs`
- **GameEngine**, **GameRenderer**, **GameViewModel** als `Lazy<GameViewModel>`
  → Startup-Ersparnis 200-500ms (schwere SKPaint/SKFont-Allokationen erst beim ersten Game-Start)

---

## MainViewModel-Kompositor + Feature-Module

`MainViewModel` ist ein **Compositor** (~480 LOC), keine God-VM. Die fünf Feature-Module sind
eigenständige Singletons, MainViewModel bündelt sie und forwarded ihren State an die
`MainView.axaml`-Bindings. Kein einziger AXAML-Change war für die Aufteilung nötig — alle
bestehenden Bindings (`{Binding MenuVm}`, `{Binding ActiveView}`, `{Binding IsAnyDialogOpen}`,
`{Binding IsShopSpinTab}` usw.) treffen weiterhin auf MainViewModel-Forwarder-Properties.

| Modul | Pfad | Verantwortung |
|-------|------|---------------|
| `INavigationCoordinator` | `Navigation/NavigationCoordinator.cs` | `ActiveView` (Source-of-Truth) + komplettes Routing (`NavigateToRouteAsync` mit 26 Routen-Cases, `NavigateTo(NavigationRequest)`, `HideAll`). CloudSave-Init-Race-Guard mit 3s-Cap. |
| `IBottomTabController` | `Navigation/BottomTabController.cs` | 5 Sub-Tab-Bools, `IsBottomTabBarVisible`, 10 `SwitchToXxxTab`-Methoden, bidirektionale `ActiveView ↔ BottomTab`-Sync mit `IBottomTabHub`. |
| `IDialogPresenter` | `Services/DialogPresenter.cs` | Alert + Confirm (`ShowConfirmAsync` mit `TaskCompletionSource`-Roundtrip) + `IsAnyDialogOpen`-Aggregat (inkl. WhatsNew-Flag). |
| `IChildViewModelRegistry` | `ViewModels/ChildViewModelRegistry.cs` | 11 Eager + 15 Lazy VMs, idempotente `EnsureXxx()`-Methoden, VM-spezifische Sub-Wirings (Shop/Dungeon/BattlePass/GemShop), `RefreshAllLocalizedTexts`, `WireCommon`. |
| `ILifecycleHub` | `ViewModels/LifecycleHub.cs` | `HandleBackPressed` (hierarchische Android-Back-Navigation), `CloudSaveInitTask` (Ctor-gestarteter Cloud-Pull), `OnAdUnavailable`. |

### Kommunikations-Pattern

- **State-Sync**: Jedes Modul feuert ein `StateChanged`/`ActiveViewChanged`/`VmInstantiated`-Event.
  MainViewModel subscribt und ruft `OnPropertyChanged` für die betroffenen Forwarder-Properties.
- **Navigation-Routing**: Child-VMs feuern `INavigable.NavigationRequested` → `ChildViewModelRegistry`
  aggregiert das in sein eigenes `NavigationRequested`-Event → MainViewModel routet an
  `NavigationCoordinator.NavigateTo`.
- **Game-Juice**: `IFloatingTextEmitter`/`ICelebrationEmitter` der VMs laufen über `IGameEventBus`
  (`RaiseFloatingText`/`RaiseCelebration`), nicht mehr durch MainViewModel.
- **Abhängigkeits-Richtung** (gerichtet, kein Zirkel beim Container-Aufbau):
  `ChildViewModelRegistry` → (keine Modul-Deps) ·
  `BottomTabController` → `ChildViewModelRegistry` ·
  `NavigationCoordinator` → `ChildViewModelRegistry` + `BottomTabController` ·
  `LifecycleHub` → alle vier anderen.
  Die zwei Zirkel (`BottomTabController`↔`NavigationCoordinator`,
  `NavigationCoordinator`↔`LifecycleHub`) werden über **lazy Provider-Lambdas** in der
  DI-Factory aufgelöst — das Lambda läuft erst zur Laufzeit, nicht beim Container-Aufbau.

### Testbarkeits-Helfer

VM-Typen sind `sealed` → NSubstitute kann sie nicht mocken. Damit die fehleranfällige Logik
trotzdem unit-testbar bleibt, sind zwei reine Helfer extrahiert:
- `ChildViewModelWiring.Wire(vm, onNavigate, eventBus)` — die `WireCommon`-Logik isoliert.
- `NavigationRouteParser.Parse(route)` / `RequiresCloudSaveInit(baseRoute)` — Compound-Route-Auflösung
  + BaseRoute/Query-Trennung + CloudSave-Gating.

### `EnsureXxxVm`-Pattern (jetzt in der Registry)

```csharp
// ChildViewModelRegistry — idempotente Lazy-Init pro Child-VM
public ShopViewModel EnsureShop()
{
    if (_shopVm is { } existing) return existing;
    var vm = _shopVmLazy.Value;
    WireCommon(vm);                          // Navigation + Game-Juice
    vm.PurchaseSucceeded += ...;             // VM-spezifisches Sub-Wiring
    _shopVm = vm;
    VmInstantiated?.Invoke(nameof(ShopVm));  // → MainViewModel.OnPropertyChanged(name)
    return vm;
}
```

---

## Render-Pipeline

### GameRenderer — 10 Partial-Classes

```
GameRenderer.cs                # Kern: Viewport (canvas.LocalClipBounds), Palette, SaveLayer-Reihenfolge
GameRenderer.Grid.cs           # Dispatcher: delegiert an .Tiles/.Blocks/.GridFx
GameRenderer.Grid.Tiles.cs     # Floor-Tiles, Boden-Cache (150 Tiles als SKBitmap)
GameRenderer.Grid.Blocks.cs    # Destructible/Indestructible Blocks, Block-Fragmente
GameRenderer.Grid.GridFx.cs    # FogOverlay, Transitions, Afterglow
GameRenderer.Characters.cs
GameRenderer.Bosses.cs
GameRenderer.Items.cs          # Bomben, PowerUps, Exit
GameRenderer.Atmosphere.cs
GameRenderer.HUD.cs            # Side-Panel rechts mit Time/Score/Combo/Lives/Deck
GameRenderer.Events.cs         # Saisonale Partikel-Overlays
```

**SaveLayer-Reihenfolge** (außen → innen):
1. Cinematic-Zoom (`canvas.Scale(zoomFactor, pivotX, pivotY)`) — von `CinematicSequencer`
2. ScreenShake-Translate
3. Spielfeld-Content (Grid + Entities + HUD)
4. Post-Processing-Layer (Colorblind-ColorMatrix, ShaderEffects)
5. Subtitle-Overlay (zwischen Tutorial-Overlay und Colorblind-Layer)

**Input-Controls werden NICHT vom Cinematic-Zoom betroffen** — Joystick bleibt lesbar.

### SkiaSharp-Caching-Patterns (KRITISCH für Performance)

```csharp
// SKPath: NIEMALS new SKPath() im Render-Loop
_tilePath.Rewind();   // behält nativen Buffer, keine Re-Allokation
// Nicht Reset()! Reset() gibt nativen Buffer frei → Re-Allokation bei MoveTo

// SKMaskFilter: einmal cachen, nie pro Frame
private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, 8f);
// Immer dispose des alten vor Neuzuweisung:
_paint.MaskFilter?.Dispose();
_paint.MaskFilter = SKMaskFilter.CreateBlur(...);

// SKShader: gecacht beim Init, kein ToShader() pro Frame
// Alle Background/Vignette/DynamicLighting-Shader beim Init gecacht
// Frame-Skipping: alle 2 Frames, kein pro-Frame ToShader()

// toString im Render-Loop: statische String-Arrays für bekannte Wertebereiche
```

**Gepoolte statische Felder** (Beispiele):
- `_charPath1`, `_charPath2`, `_bgPath`, `_irisClipPath`, `_torchPath`, `_tempPath`,
  `_poolPath1`, `_poolPath2` — alle mit `Rewind()`
- `FinalBossElementColors`, `FinalBossGemColors` — `static readonly SKColor[]`
- `BlockFragSpreadMulX/Y`, `BlockFragRotMul` — `static readonly float[]`

**GameAssetService** (AI-generierte WebP-Bitmaps):
- LRU-Cache 30 MB (früher 50 MB — Sicherheitsmarge für 3-GB-RAM Mid-Tier-Android)
- `ConcurrentDictionary + Lazy<Task>` für Deduplication beim gleichzeitigen Laden
- `GetBitmap()` für preloaded Assets, `GetOrLoadBitmap()` für lazy-loaded
- `Evict()`: Native-Bitmap-Leak-Schutz via `_pendingDispose` als `ConcurrentQueue`
  mit Drain auf UI-Thread (`Dispatcher.UIThread.Post`) — NICHT direkt im Background-Thread disposen
- `GameAssetService.Current`: Statischer Accessor für statische Renderer-Klassen ohne DI
- `GameAssetService.PlatformAssetLoader`: Android `Assets.Open()` in MainActivity gesetzt

### Atmosphärische Subsysteme (5 Systeme, struct-basiert)

| System | Pool-Größe | Beschreibung |
|--------|-----------|--------------|
| `DynamicLighting` | — | Radius-basierte Lichtquellen, `SKBlendMode.Screen` |
| `WeatherSystem` | 80 Structs | Welt-spezifische Partikel (Blätter, Asche, Blasen) |
| `AmbientParticleSystem` | 60 Structs | Hintergrund-Partikel (Glühwürmchen, Dampf, Kristalle) |
| `ShaderEffects` | — | GPU SkSL Water Ripples + CPU-Fallback, Color Grading, Heat Shimmer |
| `TrailSystem` | 40 Structs | Charakter-Spuren, Ghost-Afterimages, Boss-Trails |

**Adaptives Frame-Skipping**: 5-Frame-Ring-Buffer der Frame-Zeiten. Wenn Durchschnitt > 40ms
(< 25 FPS) → alle atmosphärischen Systeme für ≥ 500ms ausgesetzt. Hysterese-Exit bei < 28ms.
`SkipAtmosphere`-Property kombiniert manuellen `ReducedEffects`-Toggle mit der adaptiven Entscheidung.

**Boden-Cache**: 150 Floor-Tiles als `SKBitmap` gecacht, invalidiert bei Welt-/Style-Wechsel.

**Prozedurale Texturen** (`ProceduralTextures.cs`): Noise2D/Fbm (Perlin-ähnlich), CellRandom
(deterministisch pro Zelle), 12 Textur-Funktionen für 10 Welt-spezifische Tiles.

### DPI-Handling

```csharp
// IMMER canvas.LocalClipBounds verwenden:
var bounds = canvas.LocalClipBounds;
float width = bounds.Width;
float height = bounds.Height;
// NIEMALS e.Info.Width/Height — das sind physische Pixel (DPI > 1 → Clipping)
```

### BloomEffect (`Graphics/BloomEffect.cs`)

GPU-Bloom via `SKRuntimeEffect` (SkSL). 2 Render-Pässe:
1. Threshold-Pass — BT.601-Luminanz-Filter, isoliert helle Pixel
2. 5×5-Box-Blur (SkSL-Loop) + Additive Blend (`SKBlendMode.Plus`) zurück auf Hauptbild

`Preload` in `LoadingPipeline` — kompiliert die SkSL-Shader beim App-Start.
`IsAvailable`, `InitErrors`, `DisposeSharedResources` analog `ShaderEffects`-Pattern.
Tier-Gate: nur Ultra (deaktiviert bei Battery/Thermal).

---

## Hardware-Profile + Performance-Adaption

**`HardwareTier`-Enum** (Low/Medium/High/Ultra) + `IHardwareProfileService`:
- Auto-Detection via `ProcessorCount` + GC-Memory-Heuristik
- User-Override mit Persistenz
- `Battery-Save`-Toggle (persistiert, senkt Tier um 1)
- `Thermal-Throttle`-Hook (transient)
- `OnMemoryTrimRequested(trimLevel)` — bei Level ≥ 40 wird `MemoryPressure` aktiv (60s
  transient). Senkt effektiven Tier um 1 Stufe + Bloom aus.
- `IsNetworkAvailable` getter/setter + `NetworkStateChanged`-Event (für CloudSaveService-Subscription)

**Tier-Auswirkungen**:

| Tier | ParticleSystem-Cap | Bloom | AudioSpatial-Reverb |
|------|-------------------|-------|---------------------|
| Low | 300 | aus | aus |
| Medium | 800 | aus | aus |
| High | 1200 | aus | an |
| Ultra | 1500 | an (außer Battery/Thermal) | an |

`ShouldEnableBloom()` nur für Ultra. `ParticleSystem.EffectiveMaxParticles` wird dynamisch
gesetzt. **Adaptives Frame-Skipping** (siehe Render-Pipeline) bleibt unabhängig orthogonal aktiv.

---

## Input-System

### InputManager

Verwaltet aktiven Handler, auto-detect Desktop vs Android:
- **Auto-Switch**: Touch → Joystick, WASD → Keyboard, GamepadButton → Gamepad
- **Android-Controller**: `MainActivity.DispatchKeyEvent` + `DispatchGenericMotionEvent`
- **Keyboard**: Pfeiltasten/WASD + Space (Bombe) + E (Detonate) + T (ToggleSpecialBomb) + Escape (Pause)
- **Gamepad**: D-Pad + Analog-Stick (4-Wege, Deadzone 0.25) + Face-Buttons

**NeonJoystick** (Custom Touch-Joystick):
- Zwei Modi: Floating (Standard, linke 60%) + Fixed (immer sichtbar unten links)
- Default für Neuinstallationen: Fixed (bessere 4-Wege-Bomberman-Bewegung)
- Radius 75dp, Bomb 52dp, Detonator 48dp
- Deadzone: Fixed 15% / Floating 5%, Richtungs-Hysterese 1.15×
- **Separate Pointer-IDs** (`_bombButtonPointerId` + `_detonatorPointerId`) —
  verhindert Button-Hang bei gleichzeitigem Tap
- **BombPressed-Race-Schutz**: `OnTouchEnd` setzt `_bombPressed`/`_detonatePressed`
  nach Konsum sofort auf false — Taps < 16ms bleiben nicht hängen
- **Performance**: 3 statisch gecachte `SKMaskFilter`, `SKPath` via `Rewind()`,
  Arrow-Path einmal gebaut + zweimal gezeichnet (Glow + Fill)
- **SoftGlow-Skip**: alle 2 Frames für Bomb/Detonator (bei Press immer), spart 2-4ms GPU

**Pre-Turn Buffering** (`Player.cs`): Richtung gepuffert wenn Spieler nicht am Zellzentrum,
Turn bei 40% Zellzentrum-Nähe.

---

## Game-Mechaniken-Patterns

### Player-Mechaniken

- **12 PowerUp-Typen**: BombUp, Fire, Speed, Wallpass, Detonator, Bombpass, Flamepass,
  Mystery, Kick, LineBomb, PowerBomb, Skull
- **PowerUp-Freischaltung**: Level-basiert via `GetUnlockLevel()`. Story filtert gesperrte.
- **Speed**: SpeedLevel 0-3, `BASE_SPEED(80) + Level * 20`
- **Kick**: Bombe gleitet in Blickrichtung (`SLIDE_SPEED 160f`), stoppt bei Hindernis
- **LineBomb**: Alle Bomben in Blickrichtung auf leeren Zellen (ab Level 30)
- **PowerBomb**: Range = FireRange + MaxBombs − 1, verbraucht alle Slots (ab Level 40)
- **Skull/Curse**: 4 Typen (Diarrhea/Slow/Constipation/ReverseControls), 10s Dauer,
  `Cure-PowerUp` (grünes Kreuz) als sofortige Abhilfe
- **Flamepass**: Schützt NUR vor Explosionen, nicht Gegnern
- **Discovery-System**: Pausiert Spiel bei Erstentdeckung, `DiscoveryOverlay` (SkiaSharp)

**Reborn-System (Master-Mode)**:
Nach L100-Abschluss: Master-Mode-Toggle im LevelSelect. Gegner × 1.5 Geschwindigkeit,
Typ-Upgrade (Ballom→Minvo, Onil→Pass, Doll→Pontan). Separater Persistenz-Pfad via
`IMasterModeService` — Normal-Stars bleiben unberührt.

### Boss-System

- **5 Boss-Typen**: StoneGolem, IceDragon, FireDemon, ShadowMaster, FinalBoss
- Jedes 10. Level = Boss-Level (L10-L100). Boss-Typ rotiert alle 2 Welten.
- `BossEnemy` erbt von `Enemy`, eigene BoundingBox (Multi-Cell), HP 3-8, Enrage bei 50%
- **Duo-Boss-Encounter**: Welt 9 (L90) = FinalBoss + ShadowMaster, Welt 10 (L100) = 2× FinalBoss
- **Spezial-Angriffe**: Telegraph (2s) → Attack (1.5s) → Cooldown (12-18s, kürzer bei Enrage)
- **5 Angriffe**: BlockRegen, Eisatem (Reihe), Lava-Welle, Teleport, rotierend (FinalBoss)
- **Kollision**: `OccupiesCell()` statt GridX/GridY. Shield absorbiert Angriffe.

**Boss-Banner-Pattern**: Typspezifischer Name im WorldAnnouncement-Overlay
(`STONE GOLEM`, `ICE DRAGON`, `FIRE DEMON`, `SHADOW MASTER`, `FINAL BOSS`).
Duo-Encounter mit `&` verbunden oder Plural-Form.

### Level-Generierung

`LevelLayoutGenerator` (umbenannt von `LevelGenerator` — eliminiert Namespace-Konflikt
mit `Core.LevelGeneration.LevelGenerator`):
- 11 Layout-Typen, Pool pro Welt 8 Layouts (Welt 1 einsteiger-freundlich, Welt 5+ alle)
- Tägliches deterministisches Level via `GenerateDailyChallengeLevel(seed)`
- `GetMutatorDisplayName`, `PlacePowerUps`, `PlaceExit`, `SpawnEnemies`, `SpawnBossAtPosition`
- Gepoolte interne Listen (`_blockCells`, `_farBlocks`, `_validPositions`) — keine Heap-Allokation pro Level-Start

**Mutator-System** (ab Welt 6, Level x3/x6/x9 jeder Welt):
AllPowerBombs, DoubleSpeed, InvisibleBlocks, NoTimer, MirrorControls.
`GameEngine._activeMutator` in allen 5 Modi zurückgesetzt.
Mutator-Level schenken 3 garantierte Sterne (Schwierigkeit = Belohnung, nicht Strafe).

### Dungeon-Modus (Roguelike)

**Ablauf**: Floor 1-4 normal, Floor 5 Mini-Boss, Floor 6-9 härter, Floor 10 End-Boss + Truhe,
ab Floor 11 +50% Skalierung.

**CloudSaveData — Schema-Migration (KRITISCH)**:

`DungeonRunState` selbst hat keine Versionierung — die Schema-Migration liegt in `CloudSaveSchemaMigrator`
(migriert das gesamte `CloudSaveData`-Objekt, das auch DungeonStats, LoadoutData, BossRushData etc. enthält):

```
V1 → V2: master_mode_status_v1, master_mode_active, deck_telemetry_v1, LoadoutData,
          BossRushData, DungeonStatsData als Defaults aufgefüllt
V2 → V3: Accessibility_ColorblindMode, HighContrast, UiScale, Subtitles,
          TargetFrameRate, AnalyticsConsent, CrashlyticsConsent als Defaults (Off/false/30fps)
```

`CloudSaveSchemaMigrator.CurrentSchemaVersion = 3`.
`CloudSaveSchemaMigrator.TryMigrateAndValidate(data, out error)` läuft VOR `ApplyCloudData`.
Bei Migrations-Fehler: Cloud-Stand verworfen + Logger-Warning (kein Push mit Leer-State).
`BuildCloudSaveData` setzt `Version = CloudSaveSchemaMigrator.CurrentSchemaVersion` (nie hardcoded).

**Account-Delete Reihenfolge** (DSGVO Art. 17, Local-First):
1. `Preferences.Clear` (atomar, lokal — Race-unkritisch)
2. Firebase-Liga-Einträge löschen (parallel via Task.WhenAll)
3. Cloud-Save überschreiben mit leerem Snapshot
Best-Effort: Lokale Daten werden IMMER gelöscht, auch bei Netzwerk-Fehler.

**16 Buffs**: 5 Common, 5 Rare, 2 Epic, 4 Legendary (Berserker/TimeFreeze/GoldRush/Phantom)
**5 Synergies**: Bombardier, Blitzkrieg, Festung, Midas, Elementar — via `DungeonSynergyResolver`
**8 Floor-Modifikatoren**: ab Floor 3, 30% Chance
**5 Raum-Typen**: Normal (W40), Elite (W20), Treasure (W15), Challenge (W15), Rest (W10)
**Node-Map**: 10×3 (Slay the Spire-inspiriert), Pfad-Auswahl

**Eintritt**: 1x/Tag gratis, 500 Coins, 3 Gems, oder Rewarded Ad (1x/Tag).
Datum-Tracking in `DungeonStats` (nicht in `RunState`) — verhindert App-Restart-Exploit.

**Dungeon-Trennung**: Shop-Upgrades gelten NICHT. Base-Stats + Dungeon-Buffs.

### Karten-/Deck-System (14 Bomben-Typen)

**Spezial-Bomben** (3 Shop + 10 Karten):
- Shop: Ice (Frost 3s, 50% Slow), Fire (Lava 3s, Schaden), Sticky (Kettenreaktion + Klebe 1.5s)
- Karten: Smoke, Lightning (3 Gegner), Gravity, Poison, TimeWarp (50% Slow 5s), Mirror
  (doppelte Reichweite), Vortex (Spiral), Phantom (durchdringt 1 Wand), Nova (360°), BlackHole (Sog)

**Verlangsamungs-Stacking**: Frost (0.5×) + TimeWarp (0.5×) + BlackHole (0.3×) multiplikativ.
**Deck**: 4 Basis-Slots + 1 freischaltbar (20 Gems). ActiveCardSlot per HUD-Tap wechselbar.
**Drop-Gewichtung**: 60% Common, 25% Rare, 12% Epic, 3% Legendary.
**Card-Crafting** (Coin-Sink): 5 Common + 2.000C → 1 Rare, 5 Rare + 8.000C → 1 Epic,
5 Epic + 25.000C → 1 Legendary.

### AI-System

- **A\*-Pathfinding**: Object-Pooled PriorityQueue, HashSet, Dictionaries
- **BFS Safe-Cell Finder**: Pooled Queues
- **Danger-Zone**: Einmal pro Frame via `PreCalculateDangerZone()`, Kettenreaktions-Erkennung (iterativ, max 5)
- **12 Enemy-Typen**: 8 Basis + Tanker/Ghost/Splitter/Mimic
- **Boss-AI**: Kein A*, direkter Richtungs-Check, Multi-Cell-Kollision, Enrage halbiert Decision-Timer
- **A\*-Spawn-Jitter**: `AIDecisionTimer = Random * AIDecisionInterval` im Enemy-Ctor
  → verteilt erste Pfadsuchen bei Mass-Spawn statt alle im selben Frame
- **AStarBudgetPerFrame = 5**: Absicherung bei Extremfällen (Fallback Random-Movement für 1 Frame)

**Enemy-Pin-Down-Fix** (`EnemyAI.CanMoveInDirection`):
`allowBombCell = false`-Parameter. `GetRandomValidDirection` macht Two-Pass:
Erst normal (Bomb-Cells blockiert), bei Count == 0 Last-Resort-Pass mit `allowBombCell: true`.
Wände/Blöcke/PlatformGaps bleiben in beiden Pässen blockierend.

### Combo-System (`ComboSystem.cs`)

Kills innerhalb 2s Fenster → Bonus:

| Combo | Score-Bonus | Besonderheit |
|-------|------------|--------------|
| ×2 | +200 | — |
| ×3 | +500 | — |
| ×4 | +1.000 | — |
| ×5 | +2.000 | MEGA, Slow-Mo 0.8s |
| ×6 | +4.000 | Window +0.5s |
| ×7 | +8.000 | — |
| ×8 | +15.000 | — |
| ×9 | +20.000 | — |
| ×10+ | +30.000 | ULTRA, Slow-Mo 1.2s |

Window-Verlängerung +0.5s bei ×6+. Slow-Motion-Multiplikator 1.5× bei ULTRA.
Chain-Kill 1.5× bei 3+ Kills. `_comboCount`/`_comboTimer` in GameEngine als read-only
Property-Aliasse auf `_comboSystem` (Renderer-Kompatibilität).

### Coin-Economy

- **CoinService**: Level-Score / 3 → Coins bei Complete (Welt 1: Score/2 für bessere Früh-Progression)
- **Gem-Trickle**: 3 Gems bei erstmaligem 3-Sterne-Abschluss
- **Premium-Multiplikator**: 2× Coins bei LevelComplete, 3× bei GameOver-Trostcoins
- **Shop**: 9 permanente Upgrades, Preise 700-17.000 Coins
- **StartSpeed**: MaxLevel 3, Preiskurve [1.200 / 2.500 / 7.000]
- **CoinBonus L2**: +60% (amortisiert nach ~10 Welt-3-Levels)

**Overflow-Guard** in `CoinService`/`GemService`:
`(long)Balance + amount` + Clamp auf `int.MaxValue`. `Load()` clampt < 0 auf 0 + Corruption-Flag.

### Liga-System (Firebase)

- **5 Ligen**: Bronze → Diamant, 14-Tage-Saisons
- **LeagueSubTier-Enum** (I/II/III): Bronze/Silver/Gold/Platinum jeweils 3 Sub-Tiers, Diamond
  bleibt single (Endgame). Helper `GetSubTier(points)`, `GetDisplayName()`,
  `GetSubTierThreshold()`, `GetSubTierCeiling()`.
- **Firebase REST API**: Anonymous Auth, `league/s{saison}/{tier}/{uid}`
- **Rate-Limit in Firebase-Rules**: Write nur alle 60s pro UID via Server-Timestamp `updatedMs`
  (`{".sv":"timestamp"}` — nicht client-manipulierbar)
- **NPC-Backfill**: Bei < 20 echten Spielern, Seeded Random
- **Profanity-Filter**: Unicode-NFKD + Strip + Lowercase → deckt Leetspeak + Zero-Width-Tricks
- **Report-Button**: `reports/{reportedUid}/{reporterUid}` mit Rate-Limit 24h pro Paar

**Daily-Race-Leaderboard**: deterministischer Seed via `yyyy * 10000 + MM * 100 + dd` —
alle Spieler weltweit bekommen identisches Level. Schema: `league/s{saison}/daily_race/{date}/{tier}/{uid}`.
Spezifische Firebase-Rule muss VOR `$tier`-Wildcard stehen (Firebase prefer specific over wildcard).

### Live-Service-Patterns

**EventCalendarService** (`IEventCalendarService`): Wöchentlicher Event-Calendar, deterministisch
via ISO-Wochen-Seed (`(year × 7 + week) % poolSize`). Pool von 8 Wochen-Event-Typen
(DoubleXp, DoubleCoins, CardRain, BossWeek, DungeonRush, LeagueRumble, MissionMadness,
LuckyWeek). 12-Wochen-Vorschau-API + Server-Override-Hook.

**BattlePass-Theme-Rotation**: `BattlePassTheme`-Enum (Classic/Cyberpunk/Halloween/Winter/
Summer/Mech/Underwater/Sengoku/DiaDeLosMuertos/Steampunk). `BattlePassData.Theme` ist
deterministisch aus `SeasonNumber` abgeleitet (Saison 1 = Classic, dann rotiert).
`BattlePassThemeExtensions` mit Akzent-/Sekundär-Farben, Icon-Hints, RESX-Keys.

**LuckySpin Pity-Counter** (Lootbox-Compliance UK/China): Nach 50 Spins ohne Jackpot
garantierter Hit. `SpinsSinceLastJackpot` persistiert. `GetDropRates()`-API für
Compliance-Disclosure.

**FirstPurchase-Multiplier** (`IFirstPurchaseService`): ×2 Multiplier auf ersten IAP-Kauf,
persistiert + Cloud-Save-synced (`FirstPurchaseClaimed`-Key). Anti-Reinstall-Exploit-Schutz.

**Cosmetic-Volumen**: 98 Cosmetic-Definitionen total — 32 Trails (`TrailDefinitions.All`),
33 Frames (`FrameDefinitions.All`), 33 Victories (`VictoryDefinitions.All`). Welt-thematisch
(Pumpkin/Snowflake/CherryBlossom/Neon/Bone/Ocean/Samurai/Mech/Beach/Steampunk) +
Karriere-Status-Rewards (Champion/PrestigeAura/Diamond/Master/Ascension) +
BattlePass-Saison-Exclusives (SeasonStreak/BPMastery).

### Retention & Onboarding (`IRetentionService`)

- `RegisterFirstWin()` — idempotent (1× Trigger für First-Win-Cinematic, Anti-Reinstall-Re-Trigger)
- FTUE-Skin-Tracking, `DaysSinceLastSession`, D1/D7-Window-Detection
- `ComebackEligible`-Logik (≥ 3 Tage inaktiv + Cooldown gegen Multi-Comeback-Spam)
- `TouchSession()` in `LoadingPipeline`
- Cloud-Save-Sync für 5 Retention-Keys

**First-Win-Cinematic** (`GameEngine.PlayFirstWinCinematic`, 4-stufig, 4s, Royal-Match-Pattern):
1. Gold-Burst um Spieler + Pull-Back + Stinger + Achievement-Vibration
2. Multi-Color-Konfetti aus 6 Punkten in elliptischem Muster
3. Mid-Burst zentral + Subtitle "[FIRST VICTORY!]"
4. Mega-Gold-Explosion + Pull-Back + Floating-Text "ERSTER SIEG!"

### DSGVO-Datenexport (`IDataExportService`)

DSGVO Art. 20 (Recht auf Datenübertragbarkeit). JSON + Human-Readable-Export.
Profil, Fortschritt, Liga, Wirtschaft, Achievements, Consent-Flags, Shop-Upgrades.
Account-Delete (`IAccountDeletionService`) deckt Art. 17 ab (siehe Dungeon-Modus oben).

### Cloud-Save-System

- Local-First, 35 Persistenz-Keys, Pull bei App-Start, Push-Debounce 5s
- Konflikt-Resolution: TotalStars → Wealth → Cards → Timestamp
- `PersistenceHealth`: Zentrale Static-Klasse. Services rufen `ReportCorruption(name, ex)` bei
  JSON-Parse-Fehlern auf. `CloudSaveService` prüft `WasCorruptionDetected` in ALLEN drei
  Sync-Pfaden — erzwingt Cloud-Pull statt Push bei Corruption (Data-Loss-Prävention)

### Rewarded-Ads-Cooldown-Pattern

```csharp
// RewardedAdCooldownTracker: Hybrid-Cooldown
// Environment.TickCount64 (monoton, gegen Clock-Skew rückwärts)
// PLUS persistierte DateTime.UtcNow in Preferences (gegen App-Restart-Bypass)
// OR-verknüpft: Cooldown aktiv wenn eine der Uhren im 60s-Fenster
// Negative UTC-Differenzen zählen als "gerade geschehen"
```

**5 Rewarded-Placements**:
1. `continue` — GameOver: Coins verdoppeln (1× pro Versuch)
2. `level_skip` — GameOver: Level überspringen (ab 1. Fail)
3. `power_up` — LevelSelect: Power-Up Boost (ab Level 20)
4. `score_double` — GameView: Score verdoppeln (nach Level-Complete)
5. `revival` — GameOver: Wiederbelebung (1× pro Versuch)

### Anti-Cheat-Hybridtimer (Date-Manipulation-Schutz)

Das `RewardedAdCooldownTracker`-Pattern (Tick + persistierte UTC) wird auch von drei zeitbasierten
Reward-Services genutzt, um "Datum vorstellen → claim → zurückstellen → re-claim"-Glitches zu blockieren:

| Service | Reward | Tick-Key | Schwelle |
|---------|--------|----------|----------|
| `CoinService` | Daily-Bonus 500 Coins | `LastBonusTickCount` | 20 h |
| `RetentionService` | Comeback-Pack (3-Tage-Abwesenheit) | `Retention_ComebackLastClaimTicks` | ~3 Tage |
| `BattlePassService` | XP-Boost (24 h) | `BattlePassData.XpBoostStartTicks` | 24 h |

Bei Process-Reboot (TickCount64 springt zurück) verlässt sich der Service auf das UTC-Datum
allein — Reboot ist eine legitime neue Session.

### Overlay-Hit-Test-Aggregate (Android-Sicherheit)

Statt einzelner `IsHitTestVisible="{Binding !IsPaused}"`-Bindings nutzen die Hauptviews ein
zentrales Aggregat-Flag pro Modal-Layer:

- `GameViewModel.IsAnyOverlayOpen` = `IsPaused || ShowScoreDoubleOverlay || IsContextHelpVisible || IsLoading`
  → `GameView.GameCanvas.IsHitTestVisible="{Binding !IsAnyOverlayOpen}"`
  → via `[NotifyPropertyChangedFor]` automatisch neu berechnet.
- `IDialogPresenter.IsAnyDialogOpen` = `IsAlertDialogVisible || IsConfirmDialogVisible || IsWhatsNewVisible`
  → `MainViewModel.IsAnyDialogOpen` ist ein Forwarder darauf, `MainView.Pages-Panel.IsHitTestVisible="{Binding !IsAnyDialogOpen}"`
  → `DialogPresenter.StateChanged` triggert `MainViewModel.OnPropertyChanged(nameof(IsAnyDialogOpen))`.

Verhindert auf Android das ZIndex-Hit-Test-Problem (Taps gehen durch Overlay durch).

### Firebase-REST-Sicherheit

- **Bearer-Header statt URL-Query**: `FirebaseService.BuildAuthenticatedRequest()` setzt
  `Authorization: Bearer <token>` als Header. Verhindert Token-Leak in Proxy-Logs, Crashlytics-
  Stacktraces und Firebase-Audit-Logs.
- **ServerValue.TIMESTAMP**: `LeagueService` + `FirebaseClanService.SendChatAsync` verwenden
  die Firebase-Sentinel-Konstante `Dictionary<string,string> { [".sv"] = "timestamp" }` statt
  Client-`DateTime.UtcNow` — verhindert Rate-Limit-/Reihenfolge-Spoofing.
- **HttpContent-Reuse-Schutz**: 401-Retry erstellt neuen `StringContent` (HttpContent darf
  nicht zweimal gesendet werden).
- **Read-fresh-before-write**: `LeaveClanAsync` re-fetched den Server-Snapshot vor Mutation
  (Race-Schutz gegen gleichzeitige Member-Beitritte).

### Cloud-Save-Konfliktauflösung

- `CloudSaveData.ChooseBest` Vergleichsreihenfolge: TotalStars → CoinBalance+GemBalance*100 →
  TotalCards → **Keys.Count** → Timestamp → **Cloud-Default**. Tie-Default ist Cloud-authoritative,
  damit Erstlogin auf neuem Gerät den leeren lokalen State nicht über Cloud schreibt.
- `BuildCloudSaveData` schreibt **alle** SyncKeys (auch leere) → kein Cherry-Pick-Mischzustand.
- `ApplyCloudData` reset Sync-Keys lokal vor Cloud-Apply für fehlende Keys.
- **Init-Race-Schutz**: `NavigateToRouteAsync` `await`-et `_cloudSaveInitTask` (3s-Cap) bevor
  in Game/LevelSelect/Dungeon/DailyChallenge/WeeklyChallenge/Deck/Collection navigiert wird.

### Render-Lifecycle-Robustheit

- `GameEngine.Render` ist mit `try/finally` + SaveCount-Backstop umschlossen → Sub-Render-
  Exceptions hängen keine Save-Frames mehr auf dem Canvas-Stack (verhinderte doppelten
  Zoom/Shake im Folge-Frame).
- `InputManager.Dispose` ist **idempotent** (`_disposed`-Guard). GameEngine.Dispose disposed
  InputManager **nicht** — Lifetime gehört dem DI-Container.
- `GameRenderer` wird in `App.DisposeServices` **nicht** disposed — Android-OnDestroy ist oft
  kein echter Process-Kill, Renderer-Reuse nach Resume würde sonst mit disposed SKPaint crashen.
- `ListBox + VirtualizingStackPanel Horizontal`: `BattlePassView.Tiers` (60+ Items) ist die
  einzige echt virtualisierte Liste (Avalonia 12 hat keinen VirtualizingWrapPanel).

### Audio-System

- **AndroidSoundService**: SoundPool für SFX (12 + 6 Sounds) + MediaPlayer für Musik (4 + 6 Tracks)
- **AudioBus 7-Kanal-System** (`Core/Audio/AudioBus.cs`): Master/Music/Ambient/Sfx/Ui/Voice/Cinematic.
  `AudioBusMixer` mischt Bus-Volumes mit Persistenz, Sidechain-Ducking und Recovery-Hüllkurven.
- **SoundVariationPool**: Anti-Repeat-Pool (Brawl-Stars-Pattern, Suffix `_a/_b/_c/_d`).
  27 Pool-Variants + 5 Cinematic-Stinger-Files aus Kenney CC0 Packs gemapped auf Pool-Keys.
- **AudioSpatial**: Distance-Falloff, Stereo-Pan, Equal-Power-Crossfade, Reverb-Preset-Mapping.
  `PlaySoundPanned(key, pan)` mit Stereo-Pan via `bomb.GridX / Grid.Width` (Landscape-Achse).
- **SoundManager-API**: `PlayPooled()`, `PlayAt(grid)`, `PlayStinger(key)`, `PlayVoice(key)`.
- **Pitch-Variation**: `SoundManager.PlaySound` wendet ±5% Pitch-Random + ±10% Volume-Variation
  auf wiederholte SFX an — eliminiert akustisches Stutter bei Bomben-Spam
- **ISoundService**: `pitch` + `pan`-Parameter als Default-Interface-Methoden (backward-compat)
- **Thread-Safety**: `lock(_musicLock)` für MediaPlayer

**Stinger-Verkabelung** (Cinematic-Bus):

| Konstante | Trigger |
|-----------|---------|
| `STINGER_BOSS_REVEAL` | Boss-Cinematic-Start |
| `STINGER_COMBO_MEGA` | Combo ×5 |
| `STINGER_COMBO_ULTRA` | Combo ×10 |
| `STINGER_VICTORY` | Boss-Kill, Konami-Code-Reward |
| `STINGER_DEFEAT` | GameOver |

**Multi-Stage-Pontan-Warning**: 3-stufige Eskalation statt 1.5s allein. 3.0s Audio-Cue + Subtitle,
1.5s Indicator-Bestand, 0.5s Trauma-Spike-Crescendo.

**Externes Audio-Roadmap** (mit Mandat "kein Geld" abgewählt):
- Hauseigener Composer + adaptive Layered Music pro Welt (€15k–€80k Budget-Item)
- Voice-Talents (DE/EN/ES/FR/IT/PT) für Boss-Roar, Player-Reactions, Announcer-Lines
- LUFS-Mastering aller Audio-Assets auf -16 LUFS Mobile-Standard

### Vibrations-System

`IVibrationService` mit 12 Pattern-Methoden (Default-Implementations):
`VibrateBombPlant`, `VibrateSpecialBomb`, `VibratePickUp`, `VibrateShieldHit`,
`VibrateDeath`, `VibrateLevelComplete`, `VibrateBossRoar`, `VibrateCurse`,
`VibrateCombo`, `VibrateAchievement`.

`AndroidVibrationService`: Native `VibrationEffect.CreateWaveform` mit eigenem Pattern pro Typ.
Beispiel: `VibrateBombPlant = [0, 10, 20, 10]` (Doppel-Tick).

### Trauma-ScreenShake (Squirrel Eiserloh)

```csharp
// Akkumulierendes Trauma-Modell (nicht intensity/duration)
// Trauma akkumuliert sich (mehrere Explosionen → stärkerer Shake)
// Shake = MaxAmplitude * trauma² (quadratisch — kleine Werte kaum spürbar)
// TraumaDecay = 1.5/s linear
// Distanz-Skalierung: TriggerAt(amount, distanceCells, falloffCells=4)
// Bestand-API kompatibel: Trigger(intensity, duration) mappt auf Trauma
```

**PullBackFactor** (`TriggerPullBack(magnitude, duration)`): Sin/Smoothstep-Hüllkurve, max 15%
Zoom-Out. Renderer wendet via `canvas.Scale` um Spielfeld-Mitte. Trigger bei Boss-Reveal,
Mega-Combo, First-Win-Cinematic.

### Squash & Stretch + Crit-Indicator

`Player.SquashScaleX` / `SquashScaleY` Computed-Properties (subtile Sprite-Skalierung beim
Bewegen, ±5% Wobble bei 3 Hz, kollabiert auf 0.6 beim Tod). Renderer wendet via `canvas.Scale`.

Crit-Indicator auf Combo-Floating-Text: Größen-Pop nach Combo-Stufe (×2-3 = 18f / ×4-6 = 22f /
×7-9 = 26f / ×10+ = 32f) + längere Lifetime (1.5s → 2.5s bei ULTRA). Hades-Pattern.

### Cinematic-Director (`CinematicSequencer.cs`)

Lightweight Event-Sequencer mit ordered Event-List:
`Play(durationSeconds, events)` / `Update(deltaTime)` / `Stop()`

Events sind `(triggerSeconds, Action)`-Paare. `GameEngine.Update` tickt den Sequencer pro Frame.

**Boss-Reveal-Cinematic** (1.5s): Gold-Funken-Burst → Welt-Akzent-Burst →
Floating-Stinger mit Boss-Name → Weißer Burst + `VibrateBossRoar`.

**Victory-Cinematic** (2.5s): 4 Konfetti-Wellen aus verschiedenen Positionen.
`Cinematic.Stop()` als ERSTE Zeile in allen `StartXxxModeAsync()`-Methoden
(verhindert Weiterlaufen bei Mode-Wechsel während Cinematic).

### Subtitle-System (`SubtitleSystem.cs`)

Struct-Pool-basiert, max 4 aktive Captions. Fade-In/Out am unteren Bildrand.
Nur aktiv wenn `IAccessibilityService.SubtitlesEnabled == true`.
Trigger: Boss-Spawn, Time-Warning, Player-Death, Level-Complete, Ultra-Combo,
Victory-Fanfare. Throttling bei Ultra-Combo (alle 5 Combos: `_comboCount % 5 == 0`).

### Accessibility-System

`IAccessibilityService` (Singleton): `ColorblindMode` (Off/Deuteranopia/Protanopia/Tritanopia),
`HighContrast`, `UiScale` (0.75/1.0/1.25/1.5), `SubtitlesEnabled`.

**Colorblind-ColorMatrix** (Brettenmacher/Vienot):
`GameEngine.Render` legt SaveLayer mit `SKColorFilter.CreateColorMatrix` über Spielfeld.
Filter gecacht, nur bei Modus-Wechsel neu erzeugt (kein per-Frame-Allocation).

**UiScale**: 17 `_overlayFont.Size = X`-Stellen mit `_overlayUiScale`-Multiplikator.
**HighContrast für Floating-Text**: `_outlinePaint.StrokeWidth` 2× bei aktiv.
**Privacy-Sektion in Settings**: 2 ToggleSwitches (CrashlyticsConsent + AnalyticsConsent).

### Fog of War (`FogOfWarSystem.cs`)

| Zustand | Alpha-Overlay | Bedeutung |
|---------|---------------|-----------|
| Unknown | 235 | Zelle nie gesehen |
| Explored | 140 | Zelle gesehen, aktuell außer Sichtweite |
| Visible | 0 | Aktuell im Sichtfeld |

Aktivierung: L50-L59 (Radius 5), L60-L99 (Radius 4), Master-Mode immer (Radius 4).
Update 1× pro Frame in `GameEngine.Update`. Render-RLE: Zusammenhängende gleich-alpha
Zellen pro Zeile gemerged → ~150 DrawCalls → ~30 DrawCalls.
Position-Cache: Update-Pass wird übersprungen wenn Grid-Position seit letztem Frame unverändert.

### Telemetrie / DSGVO

**FPS-Bucket**: `GameEngine.Render` 5-Sekunden-Frame-Tick-Buffer, alle 5s FPS-Bucket
(15/30/45/60+) via `ITelemetryService.SetFpsBucket`. Game-Mode + Level als Custom-Keys.

**Memory-Telemetrie**: `GC.GetTotalMemory(false)` alle 60s auf Background-Thread via `Task.Run`.
Setzt Crashlytics-Custom-Keys `memory_mb`, `gc_gen0/1/2` für Crash-Filterung nach Memory-Pressure.

**Firebase-Integration aktiv ab v2.0.56**:

- `AndroidTelemetryService` ruft `Firebase.Crashlytics.FirebaseCrashlytics.Instance` — `SetCrashlyticsCollectionEnabled` braucht `Java.Lang.Boolean.True` (Java-Binding-Quirk), .NET-Exception wird via `new Java.Lang.Throwable($"...")` in eine Java-Throwable gewrappt. User-ID = SHA256-Hash der `Android.Provider.Settings.Secure.ANDROID_ID` (DSGVO-konform, nicht reversibel auf User-Identitaet).
- `AndroidAnalyticsService` ruft `Firebase.Analytics.FirebaseAnalytics.GetInstance(context)`. `LogEvent` konvertiert `IReadOnlyDictionary<string,object>` zu `Android.OS.Bundle` (PutString/PutInt/PutLong/PutDouble/PutFloat; bool → 1/0). Consent-Check via `IPreferencesService.Get("AnalyticsConsent", false)`.
- `AndroidPushNotificationService` braucht `Activity`-Referenz (RequestPermissions auf Android 13+). FCM-Token via `FirebaseMessaging.Instance.GetToken()` + `Android.Gms.Tasks.IOnCompleteListener`. Permission-Resolution via `TaskCompletionSource`, das `MainActivity.OnRequestPermissionsResult` aufloest. AlarmManager mit `SetExactAndAllowWhileIdle` (Android 12+ Fallback: `SetAndAllowWhileIdle` wenn `CanScheduleExactAlarms() == false`).
- `BomberBlastMessagingService` (FirebaseMessagingService-Subclass, im Manifest unter `org.rsdigital.bomberblast.BomberBlastMessagingService`): OnNewToken → `AndroidPushNotificationService.RaiseTokenRefresh()` (internal static, weil Events nur in der Definitions-Klasse gefeuert werden koennen). OnMessageReceived ignoriert Notification-Payload (System zeigt), behandelt nur Data-Payload (eigene Notification.Builder).
- `NotificationReceiver` (BroadcastReceiver) postet Local-Notifications mit `Notification.BigTextStyle` aus AlarmManager-PendingIntent-Extras.
- 3 Notification-Channels: `bomberblast_daily` (Low), `bomberblast_liveops` (Default, Manifest-Default), `bomberblast_important` (High mit Vibration).

NuGet-Versionen (BOM 33.5-konform): `Xamarin.Firebase.Crashlytics 119.4.4` + `Xamarin.Firebase.Analytics 123.2.0` (musste auf 123.x weil Measurement-Base 123.x transitiv aus Ads.Lite 124.x kommt — sonst R8-Dexer-Crash an `zzov`-Duplicate) + `Xamarin.Firebase.Messaging 124.1.2`. Tasks-Paket kommt transitiv.

Setup-Anleitung: `src/Apps/BomberBlast/FIREBASE_SETUP.md`.

---

## Determinismus-Foundation

**`Core/DeterministicRandom.cs`** (xoshiro256+, Public Domain, SplitMix64-Seed-Expansion):
Plattform-unabhängig deterministisch — gleicher Seed = identische Bit-Sequenz auf x64/ARM64.
`GetState/SetState` für Replay-Roundtrip. Schneller als `.NET-Random`.

**`Core/ReplayCapture.cs`**: 1-Byte-pro-Tick Input-Stream (Direction/Bomb/Detonate, Schema-V1).
`Serialize`/`Deserialize`. 108k-Tick-Soft-Cap = 30 min @ 60 Hz, ~36 KB Raw / ~5-10 KB nach RLE.

**Status**: Foundation steht, GameEngine.Update-Integration ist ein eigener Sprint
(alle engine-internen Random-Calls auf `DeterministicRandom` umstellen, Sim-Tick + Render-Pass
trennen). `FixedTimestepRunner` als 60-Hz-Akkumulator existiert.

## Multiplayer-Foundation

**`Core/Multiplayer/MultiplayerMode.cs`** — Mode-Enum (Single/LocalCoop/LocalVersus/AsyncGhost/
RealtimeServer) + `PlayerSlot` + `MultiplayerSpawnPositions` (P1=(1,1) / P2=(13,8) —
gegenüberliegende Ecken).

**`Core/Multiplayer/PlayerInputSnapshot.cs`** — struct mit 2-Byte-Wire-Format
(Slot+Direction+Bomb+Detonate+ToggleSpecial). 1.4 KB/s @ 60 Hz × 2 Spieler.

**`Core/Multiplayer/InputBuffer.cs`** — Ring-Buffer (120 Ticks Default) mit `Push` /
`PeekLatest` / `PeekHistorical` / `Clear`. Foundation für Lag-Compensation + Rollback-Netcode.

**`Core/Multiplayer/GameStateSnapshot.cs`** — struct mit P1+P2-State + RNG-State + FNV-1a-Hash.
Anti-Cheat-Vergleich: Server-Hash != Client-Hash → Manipulation/Desync. `IsIdenticalTo`-Helper.

**Status**: nur Foundation. Echte 2P-Lokal-Co-Op-Engine-Integration (dual Player-Spawning,
dual Input-Routing, Co-Op-Camera, Game-Over-bei-beide-tot) und Pi-Server-Stack mit SignalR-Hub
+ Auth + Lobby-Matchmaking sind eigene multi-Wochen-Sprints.

## Polish-Foundation

**`LoadingTips.cs`**: 33 globale + 10 welt-spezifische Tipps mit Anti-Repeat-Picker.
30%-Chance auf welt-spezifischen Tipp wenn `worldIndex` angegeben. Eindeutige Default-Hints
pro Key (kein Spam-Risiko bei fehlender RESX). `LocalizationManager.GetString` als
Resolve-Pfad. `TotalTipCount` für UI-Browser.

**`KonamiCodeDetector.cs`**: Klassisches Easter-Egg
(Up Up Down Down Left Right Left Right Bomb Detonate). 3s-Timeout zwischen Schritten,
1× pro Session. `CodeTriggered`-Event → 1500 Coins-Bonus + Gold-Konfetti + Floating-Text +
Vibration + Victory-Stinger. `InputManager.TickKonamiDetector(deltaTime)` im Engine-Update-Loop
mit Edge-Detection für Direction (Movement-Wechsel), Bomb-Press, Detonate-Press.
Subscription beim Dispose abgemeldet.

## Diagnostik (`GameEngineEventSource`)

`Core/Diagnostics/GameEngineEventSource.cs` — EventSource-Provider "BomberBlast-Engine".
11 Event-Typen über 7 Keywords (Frame/Sim/Render/AI/Gameplay/Memory/Network).

**Markers**: FrameStart/End, SimTickStart/End, RenderStart/End, AStarSearch, ExplosionTriggered,
MemoryTrimRequested, HardwareTierChanged, NetworkStateChanged.

Aktivierbar via `dotnet-trace collect --providers BomberBlast-Engine`.

## Test-Foundation

**Property-Based-Tests** (`PropertyBasedTests.cs`): randomisierte Iteration (1000-10000 Loops,
deterministischer Seed=42 für CI-Reproduzierbarkeit). Validiert Invarianten:
- ScreenShake-Trauma in [0,1]
- AudioSpatial-Pan in [-1,1]
- Distance-Volume monoton fallend
- Equal-Power-Crossfade-Identität sin²+cos²=1
- Sub-Tier-Roundtrip-Konsistenz
- EventCalendar-Determinismus
- Pool-Pick-Validität
- HardwareProfile-Cap-Untergrenze
- LuckySpin-Pity-Garantie

**Performance-Smoke-Tests** (`PerformanceSmokeTests.cs`): Stopwatch-basierte Schwellwert-Checks
(10k ComboSystem-Operations <1s, 60s-ScreenShake-Sim <500ms, 100k AudioSpatial-Calls <1s).

**Visual-Regression** (`VisualRegressionHelper.cs`): SkiaSharp-basierte Screenshot-Vergleiche.
`ComputeDiffRate(actual, baseline, tolerance)` → Pixel-Diff-Quote [0..1].
`CreateDiffBitmap` → rotes Diff-Overlay für CI-Artifact.
Tolerance-Strategie: 5 RGB-Units Default (Anti-Aliasing-typisch). 1% Pixel-Diff für robust.

---

## Icon-System (Eigene Neon-Arcade Icons)

- **Kein Material.Icons** — eigenes GameIcon-System mit 152 Icons
- `Icons/GameIcon.cs`: `PathIcon`-Ableitung mit `StyleKeyOverride => typeof(PathIcon)`
  (PFLICHT — sonst rendert das Control nicht, Avalonia 11 findet kein Template)
- `Icons/GameIconKind.cs`: Enum aller Icons
- `Icons/GameIconPaths.cs`: SVG-Pfade im Neon-Arcade-Stil (nur M/L/H/V/Z)
- `Icons/GameIconRenderer.cs`: SkiaSharp-Renderer für Icons auf SKCanvas (gecachte SKPath)
- **Design-Sprache**: Oktagone (8 Seiten, flach), scharfe Kanten, Arcade-Ästhetik
- XAML-Namespace: `xmlns:icons="using:BomberBlast.Icons"`

**AppChecker-False-Positive**: Material.Icons-Check schlägt für BomberBlast immer an —
AppChecker kennt das GameIcon-System nicht. Kein Bug, bewusste Konvention.

---

## Game Juice / Visual-Patterns

### Neon-Arcade-Theme

| Pattern | Beschreibung |
|---------|-------------|
| Neon-Joystick | Oktagonal, Orange-Glow #FF6B35, Cyan-Akzent #22D3EE, Gold-Trail #FFDD33 |
| Torn-Metal-Buttons | `TornMetalRenderer` + `GameButtonCanvas` (ButtonSeed 10-181 für Determinismus) |
| Floating-Text | Score-Popups, Combo-Text, PowerUp (Struct-Pool 20) |
| Currency-Pulse | `IsCoinsPulse`/`IsGemsPulse`, 280ms Auto-Reset via Dispatcher + Task.Delay |
| Iris-Wipe | Level-Start öffnet, Level-Complete schließt, Gold-Rand |
| Slow-Motion | 0.8s bei letztem Kill / Combo ×4+, Ease-Out 30%→100% |
| Hit-Pause | Frame-Freeze bei Kill (50ms), Death (100ms) |
| Squash/Stretch | Bomben-Birth-Bounce, Slide-Stretch, Gegner/Spieler-Tod |
| Walk-Animation | Prozedurales sin-basiertes Wippen |
| Boss-Banner | Typspezifischer Name, Duo-Encounter mit `&` |
| Kombination-Text | `x{n}` / `MEGA x{n}` (×5+) / `ULTRA x{n}` (×10+) |
| Welt-Themes | 10 Farbpaletten, WorldPalette |
| Confetti | Two-Pass (ohne Glow + mit Glow), spart Paint-State-Wechsel |
| Event-Partikel | Saisonal: Halloween (Funken), Christmas (Schneeflocken), NewYear (Sterne), Summer (Blasen) |

**DamageLevel-Convention für Torn-Metal-Buttons**:
CTA=0.5, Success=0.3, Danger=0.7, Gold=0.6, Secondary=0.2-0.3

---

## Game-Juice-Patterns (-3 )

### Welt-Themed Bomb-FX 

`BombFxTheme[]` Lookup-Table im `GameRenderer` (10 Welten × 3 Visual-Styles = 30 Themes).
Greift in `RenderBomb()` und `UpdateExplosionSkinColors()` nur wenn Default-Skin aktiv ist
(Custom-Cosmetics behalten ihre Farben). Bei Welt-Wechsel wird `_bombFxTheme` in
`SetWorldTheme()` neu gesetzt, anschliessend `UpdateExplosionSkinColors()` aufgerufen.
Public `GetWorldAccentColor()` gibt die Welt-Akzent-Farbe zurueck (von UltraComboFlash genutzt).

### Vignette-Flash (.2 + 3.3 / #7, #18)

`UltraComboFlash`-Klasse als generischer RadialGradient-Flash (200ms Default-Dauer).
Zwei Verwendungen:
- `_ultraFlash`: Bei Combo == x10 mit Welt-Akzent-Farbe (200ms snap+fade).
- `_damageFlash`: Bei Player-Hit mit Rot (50ms snap + 250ms decay) — kuerzer fuer Hit-Feedback.

Bei `ReducedEffects` wird Flash unterdrueckt (Photosensitivity-Schutz). Render im
GameEngine.Render-Loop nach allen Overlays, vor Subtitles.

`TriggerWithDuration(color, attack, decay)` erlaubt individuelle Flash-Profile.
SKShader gecacht solange Geometrie + Farbe stabil.

### Player i-Frame Visualisierung 

Statt komplettem Verstecken (return) im Blink-Modus jetzt 30%-Alpha-SaveLayer im
Player-Renderer. Spieler bleibt sichtbar, "fuehlt sich respektiert". Schnelleres
Blinken in den letzten 0.5s als Feedback fuer auslaufenden Schutz.

### Anticipation-Frames 

`Player.SquashScaleX/Y` werden im Player-Renderer via `canvas.Scale` angewandt
(war als Property vorhanden, aber nicht verkabelt — jetzt aktiv).
- Bomb-Place: 80ms Sin-Pop-Squash (X +20% / Y -15%) via `TriggerBombPlaceAnticipation()`.
- Boss-Big-Attack: Letzte 120ms vor Attack-Trigger zieht sich Boss-Sprite auf 0.85x
  zusammen — `BossEnemy.AnticipationScale` Computed-Property auf `TelegraphTimer`.

### Outline-Pass 

`OutlineRenderHelper.RenderWithOutline(canvas, action, color, radius)` als statischer
Helper. Mechanik: SaveLayer mit Dilate-ImageFilter + ColorFilter (SrcIn-Blend) macht
Pass 1 (Outline-Ring), dann renderAction() fuer Pass 2 (Original-Sprite drueber).
Cache: 1 Filter-Allokation pro Process.

Verwendung in Player/Boss/Enemy-Renderern fuer Style-Vereinheitlichung
(Vektor-Sprites + AI-WebP-Bitmaps bekommen den gleichen visuellen Anker).
Performance: ~2x DrawCalls pro Outline-Entity → empfohlen fuer 5-10 Entities pro Frame.

---

## Live-Ops-Patterns ( + 4 )

### Remote Config 

`IRemoteConfigService` mit drei Implementierungen:
- `NullRemoteConfigService`: Liefert immer Default-Werte (.4c Stub).
- `DefaultsRemoteConfigService` (Standard): Laedt Werte aus eingebetteter
  `Resources/remote_config_defaults.json`. App funktioniert vollstaendig ohne
  Firebase-Backend, FirebaseRemoteConfigService kommt spaeter als Android-Override.
- `FirebaseRemoteConfigService` (zukuenftig): Cloud-Fetch + ueberschreibt Defaults
  via `SetOverride(key, value)`.

`RemoteConfigKeys` (statische Klasse): 23 vordefinierte Keys fuer
Event-Toggles / Drop-Raten / Preise / Combat-Tuning / Live-Ops.
Initialisierung im App.axaml.cs `InitializeServicesAndUi()` parallel zu Telemetry/Push.

### Funnel-Event-Telemetrie 

Erweiterte `AnalyticsEvents`-Konstanten (40+ Funnel-Events) + `AnalyticsParams`-Konstanten
(Tipp-sicheres Param-Handling fuer Firebase-Dashboards).

Verkabelte Events:
- `level_start` / `level_complete` / `level_failed` (mit time_ms, stars, deaths, cause)
- `boss_encounter` / `boss_defeated` (boss_type, time_ms, damage_taken)
- `combo_tier_reached` (tier=5/10) bei MEGA + ULTRA
- `tutorial_step_complete` / `tutorial_complete` via TutorialService.StepCompleted-Event
- `daily_login` (consecutive_days, day, multiplier) in MainMenuVM.ApplyDailyReward
- `rewarded_ad_request` / `rewarded_ad_completed` via `ShowAdWithTelemetryAsync` Extension
- `feature_unlocked` via FeatureUnlockChoreographer 

GameEngine-Felder: `_levelElapsedSeconds` (tickt nur in Playing-State), `_deathsInLevel`
(reset bei Level-Start, +1 bei jedem Player-Tod). Beide nullified in LoadLevelAsync().

### Re-Engagement Push 

`IReEngagementScheduler` plant lokale D1/D3/D7-Notifications via existierendem
`IPushNotificationService.ScheduleLocalNotification`. MainActivity ruft
`ScheduleAll()` in OnPause + `CancelAll()` in OnResume — keine Reminder fuer
aktive Spieler.

Trigger-Logik:
- D1 (24h): "Daily-Reward wartet" — nur wenn `IDailyRewardService.IsRewardAvailable`
- D3 (72h): "Battle-Pass laeuft in N Tagen ab" — nur wenn CurrentTier < MaxTier
- D7 (168h): "Wir vermissen dich" — one-shot pro 7-Tage-Cooldown via Pref-Flag

DSGVO-konform: Respektiert POST_NOTIFICATIONS-Permission (Android 13+).
Texte lokalisiert in 6 Sprachen via RESX.

### What's-New-Modal 

`IWhatsNewService` + `WhatsNewService` mit hardcoded Eintraegen pro Version.
`CurrentVersion` aus Assembly (Single-Source-of-Truth.4a).
`ShouldShow` true wenn neue Version + Eintraege vorhanden + Erstinstall-Schutz.
`MarkSeen()` setzt `LastSeenVersion` Pref.

`WhatsNewViewModel` mit `Closed`-Event + Spaeter/Verstanden-Commands.
UI-Modal-View ist deferred — Service+VM-API stehen bereit.

### Feature-Unlock-Choreographie 

`IFeatureUnlockChoreographer` mit Queue-basierter sequentieller Anzeige.
Trigger: `OnLevelComplete(level)` und `OnAchievementUnlocked(id)`.
Unlock-Schwellen: L10 → DailyChallenge, L20 → Dungeon, L30 → LineBomb,
L40 → PowerBomb, L50 → BossRush, L100 → MasterMode. ach_master_100 → ChampionSkin.

Pref-Flag pro `FeatureId`: jedes Feature wird nur einmal pro Lebenszeit gezeigt.
Logged Funnel-Event `feature_unlocked` . UI-Thread-Event
`FeatureUnlocked` mit `FeatureUnlockEvent` (TitleKey/DescKey/HeroAssetPath/CtaNavTarget).

---

## Logging-Pattern (Welle 6 — voll auf Microsoft.Extensions.Logging)

Alle Services, Engine-Klassen und ViewModels nutzen `ILogger<T>` per Constructor Injection
(`IAppLogger`/`AppLogger`-Fassade ist entfernt). Die `LoggerFactory` haengt drei eigene
Provider an (Code-only, keine NuGet-Sinks):

- `Services.Logging.TraceLoggerProvider` — LogCat auf Android, Debug-Output auf Desktop
- `Services.Logging.FileLoggerProvider` — rollende Log-Datei `{LocalAppData}/BomberBlast/logs/app.log`
  (512 KB Cap, 1 Backup). Ueberlebt App-Crashes.
- `Services.Logging.CrashlyticsLoggerProvider` — Bridge zu `ITelemetryService`:
  - `LogError(ex, msg)` → `telemetry.LogNonFatal(ex, ctx)` (non-fatal Crash-Report mit Stack-Trace)
  - `LogWarning` / `LogError` ohne Exception → `telemetry.Log(...)` (Breadcrumb)
  - `LogInformation` → Breadcrumb nur im DEBUG-Build (Quota fuer Warnings/Errors reservieren)
  - `Trace`/`Debug`-Eintraege werden in Crashlytics nie weitergegeben (Noise-Schutz)

`ITelemetryService` wird im `CrashlyticsLoggerProvider` lazy via `IServiceProvider` aufgeloest —
verhindert Zirkularitaeten waehrend der DI-Aufbauphase. Bei fehlendem Telemetry-Service bleibt
der Bridge inaktiv, Trace + File loggen unabhaengig weiter.

Build-Filtering: `LogLevel.Trace` im DEBUG, `LogLevel.Information` im Release
(`SetMinimumLevel` in `LoggerFactory.Create`).

**Statische Logger-Sinks** (`ShaderEffects.Logger`, `PersistenceHealth.Logger`): werden in
`App.axaml.cs` nach `Services.BuildServiceProvider()` gesetzt — `ShaderEffects` ueber
`GetRequiredService<ILogger<ShaderEffects>>()`, `PersistenceHealth` (static class)
ueber `ILoggerFactory.CreateLogger(nameof(PersistenceHealth))`.

**Strukturierte Logs**: Templates statt String-Interpolation verwenden, damit Crashlytics
+ File-Logs aussagekraeftige Custom-Keys bekommen.
- Falsch: `_logger.LogError($"Fehler bei Route '{route}'", ex)`
- Richtig: `_logger.LogError(ex, "Fehler bei Route '{Route}'", route)`

---

## Crash-Recovery 

`OnFrameworkInitializationCompleted` inkrementiert `BomberBlast_AppCrashCount`
VOR der Init-Phase, `RunLoadingAsync` setzt nach Pipeline-Erfolg auf 0 zurueck.
Bei `>= 3` Crashes in Folge greift Safe-Mode: optionale Services (Telemetry/
Analytics/Push/RemoteConfig) werden mit Try/Catch uebersprungen damit die App
garantiert startet — User kommt zumindest ans Settings-Menue.

Public API: `App.ResetCrashRecoveryCounter()` fuer Settings-Screen.

---

## Tutorial-Phasen 

Tutorial-Schritte sind in 3 Phasen gruppiert: T1 Movement, T2 Bombs, T3 PowerUps.
`TutorialPhase`-Enum + `TutorialStep.IsFirstOfPhase`-Flag.
`ITutorialService.PhaseChanged`-Event feuert beim Phasen-Wechsel —
Tutorial-Overlay kann einen Banner anzeigen (": Bomben-Mechanik").
RESX-Texte fuer Phase-Banner in 6 Sprachen.

---

## Welt-Story-Beats 

`IWorldStoryService` liefert Cutscene-Daten fuer Welt-Start (Intro) und
Welt-Boss-Sieg (Outro mit Cliffhanger). 10 Welt-Intros + 9 Welt-Outros
(Welt 10 ist das Ende). Pref-Flags HasSeenIntro/HasSeenOutro pro Welt —
Cutscenes one-shot pro Lebenszeit. Texte voll lokalisiert in 6 Sprachen.

`StingerKey`-Field fuer Audio-Verkabelung (boss_reveal fuer Intros ab Welt 2,
victory fuer alle Outros).

---

## Elite-Enemies + Boss-Modifier 

`Enemy.IsElite`-Property + Konstruktor-Parameter `isElite: false`. Elite-Modifier:
1.2x Speed, 2x HitPoints, 3x Points, lila pulsierender Glow im Renderer.

`BossEnemy.Modifier`-Property mit `BossModifier`-Enum (8 Modifier × 5 Bosse =
40 Variationen). `BossModifierExtensions.RollForWorld(world, rng)` weist deterministisch
zu (30% ab W5, 60% W10). `CurrentPhase`-Property wird beim Enrage-Threshold von 1 auf 2
gewechselt — erlaubt Phase-2-Variant-Attack-Patterns.

HINWEIS: Modifier-Effekte (Shielded/Healing/Summoner/...) sind separate Implementierungs-
Sprints —.1 ist Foundation (Enum + Property + Spawn-Roll).

---

## Hero/Character-System 

`HeroDefinition` mit 5 hardcoded Heroes: Default, SpeedySam, BrickBoris, TwinTina,
LuckyLola. Stats: StartMaxBombs/FireRange/SpeedLevel/Lives + Multiplier
(Coin/PowerUp/BlockDrop) + `HeroTrait`-Enum.

`IHeroService` mit ActiveHero + Unlock-API. Persistiert ActiveHeroId + Unlocked-Set.
Default-Hero IMMER unlocked. Unlock-Conditions: Achievement-IDs oder "gems_NN"
fuer Direct-Buy. RESX-Texte (Name + Desc) in 6 Sprachen.

HINWEIS: Engine-Integration (Player.ApplyHero(activeHero) beim Spawn, Stat-Boni
in Coin/PowerUp/Block-Calculations) ist deferred.

---

## Multiplayer-Foundation 

`IMultiplayerSessionService` verwaltet `MultiplayerMode` + Persistenz. `IsCoopEnabled`
/ `IsVersusEnabled` fuer Engine-Abfrage. Foundation aufbaut auf bestehender
`Core/Multiplayer/`-Klassen (PlayerInputSnapshot, InputBuffer, GameStateSnapshot).

HINWEIS: Echte Engine-Integration (Player2-Spawn, Dual-Input-Routing,
Co-Op-Camera, GameOver-bei-beide-tot, Splitscreen) ist eigener Multi-Wochen-Sprint.

---

## Clan-System Foundation 

`IClanService` mit `NullClanService` (Default). Domain-Models: ClanData, ClanMember,
ClanChatMessage, ClanRole. API: Create/Join/Leave/Pull-Chat/Send/Leaderboard.
Asynchron-Pattern (kein Live-Sync, alle 30s Pull).

HINWEIS: Echte Firebase-Realtime-DB-Integration mit Security-Rules + Profanity-Filter
+ Rate-Limits ist eigener 4-6-Wochen-Sprint.

---

## Wochen-Content-Pipeline 

`IWeeklyContentService` deterministisch via ISO-Wochen-Seed. 8 WeeklyModifier-Pool
(Ice+Speed, DoubleBombs, Phantom-Walls, ...) + 4 WeeklyReward-Pool (Cosmetic-
Trails/Frames/Victories) + 3 wechselnde Boss-Modifier pro Woche aus dem 8er-Pool
(Fisher-Yates-Shuffle).

`ISOWeek.GetWeekOfYear` stellt sicher dass alle Spieler weltweit gleiche
Wochen-Inhalte sehen. RemoteConfig-Override via.1 moeglich.

---

## Adaptive Music-Engine 

`AudioBusMixer.Boost(bus, multiplier, duration)` ergaenzt die bestehende Duck-API um
einen Boost-Pfad (Multiplier &gt;= 1.0, Cap 1.5 — Distortion-Schutz). Update-Loop
behandelt Recovery sowohl fuer Duck (Linear zu 1.0 mit 2.0/s) als auch Boost
(Linear zurueck zu 1.0 mit 0.5/s).

Engine-Trigger fuer Music-Boost:
- Last-Enemy-Drama (>=3 Enemies / Boss-Level): Music +20% fuer 4s
- ULTRA-Combo (x10+): Music +25% fuer 5s

Echte EQ-Sidechain auf Drum-Frequenzen + Tempo-Pitch-Shift braucht externe
Audio-DSP-Library (ManagedBass/SoundTouch) — deferred. LUFS-Mastering bleibt
externer Build-Skript-Schritt (ffmpeg).

---

## Replay-Foundation 

`Core.IRngProvider` (vorhanden) jetzt als DI-Singleton registriert mit
`DeterministicRngProvider` als Default (xoshiro256+ via DeterministicRandom).
`SystemRngProvider` bleibt fuer Visual-Random (Partikel-Jitter, Screen-Shake-
Offset — soll NICHT deterministisch sein, sonst sieht's kuenstlich aus).

Foundation-Komplettheit:
- `FixedTimestepRunner` (60Hz-Akkumulator) ist im Engine-Update verkabelt (opt-in via Flag)
- `ReplayCapture` (1 Byte/Tick Input-Stream) hat Serialize/Deserialize
- IRngProvider im DI

Was bleibt fuer Replay-System: Migration der 50+ Random-Calls in LevelGenerator/
EnemyAI/etc. auf injizierten IRngProvider. Eigener multi-Wochen-Sprint.

---

## Foundation-Services fuer geplante Refactors

Diese Services sind als API + Default-Impl im DI registriert, aber die zugehoerige
groesere Refactor-Arbeit (UI-Umbau / Migration) bleibt offen:

- `IGameEventBus`: Pub/Sub fuer UI-Events (FloatingText/Celebration/ExitHint/Message).
  Wird vom `ChildViewModelRegistry.WireCommon` aktiv genutzt — VMs routen Game-Juice
  über den Bus statt durch MainViewModel.
- `IBottomTabHub`: Tab-State + Pref-Persistenz. Vom `BottomTabController` genutzt.
  Der MainMenuView UI-Refactor (974 LOC) bleibt Game-Design-Sprint.
- Mode-Plugin-Foundation: GameEngine.Update ruft bereits `_currentMode?.UpdateLogic`
  + `OnGameOver`. Bool-Flag-Wegfall + Property-Aliasse-Aufloesung bleibt eigener
  Sprint mit Regression-Tests.

---

## Aktive Gotcha-Patterns

### SKPath.Rewind() statt Reset()

`Reset()` gibt nativen Path-Buffer frei → Re-Allokation beim nächsten `MoveTo`.
`Rewind()` behält die Kapazität. **IMMER `Rewind()`** in Render-Loops.
Ausnahme: Nach `_bgPath.Rewind()` in `FogOfWarSystem.Render` explizit
`FillType = SKPathFillType.Winding` zurücksetzen — Rewind beibehält FillType, aber
Atmosphere-Renderer nutzen `_bgPath` mit Default-FillType.

### Render-Loop-Disposal — Null-Safety

In `StartRenderLoop()` NUR `_renderTimer?.Stop()` aufrufen.
**NIEMALS `StopRenderLoop()`** — das setzt `_gameCanvas = null`, das Timer-Lambda
captured `this._gameCanvas` → Render-Loop-Tod nach nächstem Tick.

### GameAssetService — Native-Bitmap-Leak

`Evict()` darf SKBitmap NICHT direkt im Background-Thread disposen (use-after-free auf Android).
`_pendingDispose` als `ConcurrentQueue`, Drain immer via `Dispatcher.UIThread.Post`.

### SKCanvasView — 3-stufige VM-Subscription

`ContentControl + ViewLocator` setzt DataContext verzögert → `InvalidateCanvasRequested`
kann beim `StartGameLoop()` keinen Subscriber haben → Render-Timer startet nie.

```csharp
// TrySubscribeToViewModel() als zentrale idempotente Methode
// (1) OnDataContextChanged
// (2) OnLoaded als Backup
// (3) OnPaintSurface Safety-Net — startet Timer nach wenn kein Subscriber
```

### ComboSystem-Integration — Property-Aliasse

`_comboCount` und `_comboTimer` in GameEngine sind read-only Properties die auf
`_comboSystem` delegieren — Renderer-Code kann unverändert auf diese Properties zugreifen.
Nicht als Felder reimplementieren (würde Sync-Probleme erzeugen).

### PersistenceHealth — Push-Schutz bei Corruption

`CloudSaveService` prüft `WasCorruptionDetected` in ALLEN drei Sync-Pfaden
(Pull, SchedulePush, ForceUpload). Ohne diesen Check würde ein einzelner Parse-Fehler
die Cloud mit Leer-State überschreiben (Data-Loss).

### DeleteCloudSaveAsync — Version nicht hardcoden

```csharp
// FALSCH: Version = 1 (triggert V1→V3-Migration auf leerem Snapshot → Default-Auffüllung)
// RICHTIG:
Version = CloudSaveSchemaMigrator.CurrentSchemaVersion
```

### Firebase-Rules — Spezifisch vor Wildcard

`daily_race`-Rule MUSS vor `$tier`-Wildcard in `database.rules.bomberblast.json` stehen.
Ohne das wird `daily_race` als Tier-Name interpretiert → Permission-Denied.

### GC.GetTotalMemory auf Background-Thread

`GC.GetTotalMemory(false)` auf UI-Thread verursacht 1-5ms Frame-Spike auf Mono-AOT-Android
(Heap-Walk). IMMER via `Task.Run` auf Background-Thread ausführen.

### Cinematic.Stop() bei Mode-Wechsel

`_cinematic?.Stop()` als **erste Zeile** in allen `StartXxxModeAsync()`-Methoden.
Boss-Reveal-Cinematic kann sonst nach Mode-Wechsel weiterlaufen.

### HighContrast-Toggle und DSGVO-Consent

Firebase-Rules prüfen `updatedUtc` nicht mehr in `hasChildren` (seit ServerTimestamp-Migration).
Rule-Änderungen müssen in Firebase Console deployed werden — Datei lokal ist nicht automatisch live.

### Splash-Versions-String automatisch 

`App.axaml.cs.GetAppVersionString()` liest die Version aus
`typeof(App).Assembly.GetName().Version.ToString(3)`. Source-of-truth ist
`<Version>X.Y.Z</Version>` in `BomberBlast.Shared.csproj` — diese muss bei jedem
Release synchron mit `ApplicationDisplayVersion` in `BomberBlast.Android.csproj`
gehalten werden (sonst zeigt Splash eine andere Version als die installierte App).

---

## Services-Übersicht

| Service | Zweck |
|---------|-------|
| `ILogger<T>` (M.E.L.) | Standard Logging — drei Provider (Trace/File/Crashlytics), siehe Logging-Pattern oben |
| `ISoundService` | Audio (Pitch/Pan-Variation, räumliches Audio) |
| `IProgressService` | Level-Fortschritt, Sterne, Fail-Counter, World-Gating |
| `IHighScoreService` | Top-10 Scores (sqlite-net-pcl) |
| `IGameStyleService` | Visual-Style-Persistenz (Classic/Neon) |
| `ICoinService` | Coin-Balance, Overflow-Guard |
| `IGemService` | Gem-Balance (zweite Währung, NUR Gameplay) |
| `IShopService` | 9 permanente Upgrades |
| `ITutorialService` | 6-Schritte Tutorial für Level 1 |
| `IDailyRewardService` | 7-Tage Login-Bonus + Comeback-Bonus (> 3 Tage inaktiv) |
| `ICustomizationService` | Spieler-Skins (Coin + Gem-Skins) |
| `IAchievementService` | 66 Achievements in 5 Kategorien, JSON-Persistenz |
| `IDiscoveryService` | Erstentdeckungs-Tracking (PowerUps/Mechaniken) |
| `IDailyChallengeService` | Tägliche Herausforderung, Streak-Tracking |
| `IPlayGamesService` | Google Play Games Services v2 (Leaderboards, Online-Achievements) |
| `ILuckySpinService` | Glücksrad: 8 gewichtete Segmente, 1× gratis/Tag |
| `IWeeklyChallengeService` | 5 wöchentliche Missionen aus 14er-Pool, Montag-Reset |
| `IDailyMissionService` | 3 tägliche Missionen aus 14er-Pool, Mitternacht-UTC-Reset |
| `ICardService` | 14 Bomben-Karten, Deck, Upgrade, Crafting |
| `IDungeonService` | Roguelike-Run-State, 16 Buffs, Raum-Typen, Node-Map |
| `IDungeonUpgradeService` | 8 permanente Dungeon-Upgrades (DungeonCoins) |
| `ICollectionService` | Sammlungs-Album: Gegner/Bosse/PowerUp-Tracking |
| `IFirebaseService` | Firebase REST API: Anonymous Auth + Realtime Database |
| `ILeagueService` | Liga-System: 5 Tiers, 14-Tage-Saisons, Firebase + NPC-Backfill |
| `ICloudSaveService` | Cloud-Save: Local-First, 35 Keys, Debounce 5s |
| `IBattlePassService` | 30-Tier Saison, XP-basiert, Free/Premium-Track |
| `IRotatingDealsService` | 3 tägliche + 1 wöchentliches Angebot, 20-50% Rabatt |
| `IGameAssetService` | AI-generierte WebP-Bitmaps, LRU-Cache 30 MB |
| `IAccessibilityService` | Colorblind-Mode, HighContrast, UiScale, Subtitles |
| `IAccountDeletionService` | DSGVO Art. 17 Cascading-Delete (Local-First) |
| `IEventService` | Saisonale Events: Halloween/Christmas/NewYear/Summer |
| `IEventCalendarService` | Wöchentlicher Event-Calendar (deterministisch via ISO-Week) |
| `IBossRushService` | 5-Boss-Sequenz, ISO-8601-Year-Week-Reset |
| `ILoadoutService` | Pre-Run Boosts pro Story-Level (max 2 Boosts) |
| `IMasterModeService` | Master-Mode-Status pro Level, IsActive-Toggle |
| `IDeckTelemetryService` | Used/Plays/Wins pro BombType (Balance-Telemetrie) |
| `IGameTrackingService` | Session-Tracking, Spielzeit, Game-Events für Analytics |
| `IStarterPackService` | Starter-Pack-Angebot (Einmal-Kauf, erstes Start-Fenster) |
| `IReviewService` | Google In-App Review API (Trigger nach Meilenstein) |
| `ITelemetryService` | Crashlytics-Wrapper (NullTelemetryService auf Desktop) |
| `IAnalyticsService` | Firebase Analytics (NullAnalyticsService auf Desktop) |
| `IPushNotificationService` | FCM + AlarmManager (NullPushNotificationService auf Desktop) |
| `IRemoteConfigService` | Remote Config (DefaultsRemoteConfigService laedt embedded JSON.1) |
| `IReEngagementScheduler` | D1/D3/D7 lokale Push-Trigger (.3, MainActivity-OnPause/OnResume) |
| `IWhatsNewService` | Versions-Aenderungs-Modal  |
| `IFeatureUnlockChoreographer` | Queue-basierte Feature-Unlock-Overlays  |
| `IWorldStoryService` | Welt-Intro/Outro-Cutscenes  |
| `IHeroService` | 5 spielbare Heroes mit Stats + Unlock  |
| `IMultiplayerSessionService` | Multiplayer-Mode-Verwaltung (.2 Foundation) |
| `IClanService` | Clan-System (.3 Foundation, NullImpl bis Firebase live) |
| `IWeeklyContentService` | Wochen-Content-Pipeline (.4, ISO-Week-deterministisch) |
| `IRngProvider` | Deterministischer RNG (.2, Core-Namespace, Replay-Foundation) |
| `IGameEventBus` | Pub/Sub-Hub fuer UI-Events (.2, FloatingText/Celebration/ExitHint/Message) |
| `IBottomTabHub` | Bottom-Tab-Hub (.1 Foundation, 4 Tabs: Home/Play/Shop/Profile) |

---

## Conventions & MVVM-Patterns

### Navigation (Event-basiert)

```csharp
// GoGame-Record als NavigationRequest (typsicher)
NavigationRequested?.Invoke(new GoGame(Mode: "bossrush", Floor: 0));
// Zurück: NavigationRequested?.Invoke(new GoBack())
```

**ActiveView-Enum** (statt 17 IsXxxActive-Booleans):
`Classes.Active="{Binding ActiveView, Converter=..., ConverterParameter=Game}"` direkt in AXAML.
`IsXxxActive => ActiveView == ActiveView.Xxx` als Computed-Properties für Backward-Compat.

### Compiled Bindings (PFLICHT)

```axaml
<UserControl x:CompileBindings="True" x:DataType="vm:ShopViewModel">
    <!-- DataTemplates in ItemsControl MÜSSEN x:DataType angeben -->
    <!-- Parent-Commands in DataTemplates: -->
    {Binding $parent[ItemsControl].((vm:ShopViewModel)DataContext).PurchaseCommand}
```

### GameEngine-Events (kein `On`-Prefix)

`GameOver`, `LevelComplete`, `Victory`, `ScoreChanged`, `CoinsEarned`,
`PauseRequested`, `DirectionChanged`, `DungeonFloorComplete`,
`DungeonBuffSelection`, `DungeonRunEnd`

### ViewModels-Lifecycle

- `ChildViewModelRegistry.EnsureXxx()` als idempotenter Guard — instanziiert den Lazy-VM
  beim ersten Zugriff, verdrahtet Common-Events + Sub-Wirings (siehe MainViewModel-Kompositor).
- `IGameJuiceEmitter`: Einheitliches Interface für FloatingText + Celebration (implementiert
  von LevelSelectVM, MainMenuVM, ShopVM, GameOverVM, ProfileVM). Routing über `IGameEventBus`.
- `GameEngine.Dispose()`: Via `App.DisposeServices()` (Desktop: ShutdownRequested, Android: OnDestroy)

---

## Build-Befehle

```bash
# Shared bauen (häufigster Check)
dotnet build src/Apps/BomberBlast/BomberBlast.Shared

# Desktop starten
dotnet run --project src/Apps/BomberBlast/BomberBlast.Desktop

# Android Debug-Build
dotnet build src/Apps/BomberBlast/BomberBlast.Android

# Android Release-AAB (NUR auf Anfrage)
dotnet publish src/Apps/BomberBlast/BomberBlast.Android -c Release

# AppChecker
dotnet run --project tools/AppChecker BomberBlast

# Test-Suite
dotnet test tests/BomberBlast.Tests/

# Firebase RTDB-Rules deployen (Repo-Root, Projekt bomberblast-league)
npx firebase deploy --only database --project bomberblast-league --config firebase.bomberblast.json
```

**Build-Hygiene**: 0 Warnungen in Shared + Android. SkiaSharp 3.x Text-Rendering:
gepoolte `SKFont`-Objekte + `canvas.DrawText(text, x, y, SKTextAlign, SKFont, SKPaint)`.
Keine `SKPaint.TextSize/TextAlign/FakeBoldText` mehr (deprecated).

---

## Verweise

- Haupt-CLAUDE.md (`F:\Meine_Apps_Ava\CLAUDE.md`): Build-System, allgemeine Conventions,
  Troubleshooting, Keystore, Ad-Pattern, DI-Pattern
- `database.rules.bomberblast.json` (Repo-Root): Firebase-RTDB-Security-Rules (Liga + Daily-Race +
  Reports + Clans). Deployt via `firebase.bomberblast.json` (Repo-Root) auf Projekt `bomberblast-league`.
  Firebase-CLI verlangt dass `firebase.json` + Rules-Datei im selben Verzeichnis liegen — daher beide
  im Repo-Root, nicht im App-Ordner.
- `src/Apps/BomberBlast/FIREBASE_SETUP.md`: Crashlytics/Analytics/FCM-Setup-Anleitung (~1.5h)
- `src/Apps/BomberBlast/BOMBERBLAST_AAA_AUDIT.md`: Externer -Katalog (Roadmap-Referenz)
- `tests/BomberBlast.Tests/`: 643 Tests (xUnit)
