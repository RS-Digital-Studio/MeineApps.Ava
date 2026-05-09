# BomberBlast — Bomberman-Klon (SkiaSharp)

Vollständige 2D-Spiel-Engine im Bomberman-Stil. Landscape-only auf Android.
Grid 15×10. Zwei Visual-Styles: Classic HD + Neon/Cyberpunk. SkiaSharp-Rendering,
eigenes Icon-System, AI-Pathfinding, Roguelike-Dungeon-Modus und Liga-System.

| Aspekt | Wert |
|--------|------|
| Aktuelle Version | v2.0.55 (VersionCode 65) |
| Package-ID | org.rsdigital.bomberblast |
| Status | Produktion |
| Premium-Modell | 1,99 EUR `remove_ads` |

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
│   │   ├── LevelGeneration/     # LevelLayoutGenerator, ILevelGenerator, MutatorEffects
│   │   └── Dungeon/             # DungeonSynergyResolver
│   ├── Graphics/                # Alle Renderer (14 Stück)
│   │   ├── GameRenderer.cs      # Kern: Palette, Viewport, SaveLayer-Stack
│   │   ├── GameRenderer.Grid.cs # Tiles + Blocks (aufgeteilt in .Tiles/.Blocks/.GridFx)
│   │   ├── GameRenderer.Characters.cs
│   │   ├── GameRenderer.Bosses.cs
│   │   ├── GameRenderer.Items.cs
│   │   ├── GameRenderer.Atmosphere.cs
│   │   ├── GameRenderer.HUD.cs
│   │   ├── GameRenderer.Events.cs  # Saisonale Partikel-Overlays (Halloween/Christmas/NewYear/Summer)
│   │   ├── CinematicSequencer.cs   # Lightweight-Event-Sequencer für Boss-Reveal/Victory
│   │   ├── SubtitleSystem.cs       # Struct-Pool-Captions (max 4 aktiv)
│   │   ├── FogOfWarSystem.cs       # 3-Zustand Memory-FoW für L50+/Master-Mode
│   │   ├── MenuBackgroundCanvas.cs # 7 Themes, struct-Pool 60 Partikel
│   │   ├── GameButtonCanvas.cs     # Torn-Metal-Button mit SKCanvas
│   │   └── ...weitere Renderer
│   ├── Icons/                   # Eigenes Neon-Arcade Icon-System (152 Icons)
│   ├── Input/                   # NeonJoystick, InputManager
│   ├── Models/                  # Entities, Level, Dungeon, PowerUp, Bomb-Typen
│   ├── Services/                # 29 Services (alle als Interface)
│   └── ViewModels/              # 23 ViewModels (alle Singletons per Lazy<T>)
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

**Extrahierte Pure-Logic-Klassen** (−836 Zeilen aus GameEngine):

| Klasse | Pfad | Pattern |
|--------|------|---------|
| `LevelLayoutGenerator` | `Core/LevelGeneration/` | Singleton, DI, gepoolte interne Listen |
| `MutatorEffects` | `Core/LevelGeneration/` | Static, pure Funktion, Context als Parameter |
| `SpecialExplosionEffects` | `Core/Combat/` | Static, 13 Handle*-Methoden, ExplosionEffectsContext |
| `EnemyPositionIndex` | `Core/Combat/` | Singleton, O(1)-Lookup, Lazy-Rebuild per Dirty-Flag |
| `SurvivalSpawner` | `Core/Modes/` | Static, zustandslos, SurvivalMode hält State |
| `DungeonSynergyResolver` | `Core/Dungeon/` | Static, pure Funktion, wertet 5 Synergie-Regeln |
| `ComboSystem` | `Core/Combat/` | Instanz-Klasse, per `_comboSystem`-Field in GameEngine |

**Callback-Pattern für Engine-Mutationen** (in `ExplosionEffectsContext`):
`DestroyBlock`, `KillEnemy`, `ProcessExplosion` als Delegates — diese mutieren engine-interne
Score/Events/State-Machine und gehören nicht in eine Extract-Datei.

### IGameMode-Pattern

```csharp
// Core/Modes/IGameMode.cs
interface IGameMode {
    string ModeTag { get; }
    void Initialize(GameModeContext ctx);
    void UpdateLogic(GameModeContext ctx, float deltaTime);  // noch nicht im Update-Loop
    void OnLevelComplete(GameModeContext ctx);               // noch nicht im Update-Loop
    void OnGameOver(GameModeContext ctx);
    void Cleanup(GameModeContext ctx);
}
```

8 konkrete Implementierungen in `Core/Modes/GameModes.cs`:
`StoryMode`, `MasterMode`, `DailyChallengeMode`, `QuickPlayMode`, `SurvivalMode`,
`DungeonMode`, `BossRushMode`, `DailyRaceMode` — alle erben von `GameModeBase`
(no-op-Defaults).

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

- **23 ViewModels** (alle Singleton), **29 Services** (alle Singleton)
- **14 spät-unlocked Child-VMs** als `Lazy<T>` injiziert (ShopVM, AchievementsVM, DeckVM usw.)
  → `EnsureXxxVm()`-Methode mit idempotentem Guard, erst beim ersten Navigations-Ziel instanziiert
- **9 Eager-VMs** für frühe Interaktion: MainMenu, Game, LevelSelect, Settings, Help,
  HighScores, GameOver, Pause, Victory
- **Zirkuläre Abhängigkeiten** via `Lazy<T>` + `LazyServiceExtensions.cs`
- **GameEngine**, **GameRenderer**, **GameViewModel** als `Lazy<GameViewModel>` in MainViewModel
  → Startup-Ersparnis 200-500ms (schwere SKPaint/SKFont-Allokationen erst beim ersten Game-Start)

---

## Render-Pipeline

### GameRenderer — 7 Partial-Classes

```
GameRenderer.cs           # Kern: Viewport (canvas.LocalClipBounds), Palette, SaveLayer-Reihenfolge
GameRenderer.Grid.cs      # Aufgeteilt in .Tiles/.Blocks/.GridFx (FogOverlay, Transitions, Afterglow)
GameRenderer.Characters.cs
GameRenderer.Bosses.cs
GameRenderer.Items.cs     # Bomben, PowerUps, Exit
GameRenderer.Atmosphere.cs
GameRenderer.HUD.cs       # Side-Panel rechts mit Time/Score/Combo/Lives/Deck
GameRenderer.Events.cs    # Saisonale Partikel-Overlays
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
- **LineBomb**: Alle Bomben in Blickrichtung auf leeren Zellen (ab Level 26)
- **PowerBomb**: Range = FireRange + MaxBombs − 1, verbraucht alle Slots (ab Level 36)
- **Skull/Curse**: 4 Typen (Diarrhea/Slow/Constipation/ReverseControls), 6s Dauer,
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

**DungeonRunState — Schema-Migration (KRITISCH)**:
```
V1 → V2: master_mode_status_v1, master_mode_active, deck_telemetry_v1, LoadoutData,
          BossRushData, DungeonStatsData als Defaults aufgefüllt
V2 → V3: Accessibility_ColorblindMode, HighContrast, UiScale, Subtitles,
          TargetFrameRate, AnalyticsConsent, CrashlyticsConsent als Defaults (Off/false/30fps)
```

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
- **Firebase REST API**: Anonymous Auth, `league/s{saison}/{tier}/{uid}`
- **Rate-Limit in Firebase-Rules**: Write nur alle 60s pro UID via Server-Timestamp `updatedMs`
  (`{".sv":"timestamp"}` — nicht client-manipulierbar)
- **NPC-Backfill**: Bei < 20 echten Spielern, Seeded Random
- **Profanity-Filter**: Unicode-NFKD + Strip + Lowercase → deckt Leetspeak + Zero-Width-Tricks
- **Report-Button**: `reports/{reportedUid}/{reporterUid}` mit Rate-Limit 24h pro Paar

**Daily-Race-Leaderboard**: deterministischer Seed via `yyyy * 10000 + MM * 100 + dd` —
alle Spieler weltweit bekommen identisches Level. Schema: `league/s{saison}/daily_race/{date}/{tier}/{uid}`.
Spezifische Firebase-Rule muss VOR `$tier`-Wildcard stehen (Firebase prefer specific over wildcard).

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

### Audio-System

- **AndroidSoundService**: SoundPool für SFX (12 + 6 Sounds) + MediaPlayer für Musik (4 + 6 Tracks)
- **Pitch-Variation**: `SoundManager.PlaySound` wendet ±5% Pitch-Random + ±10% Volume-Variation
  auf wiederholte SFX an — eliminiert akustisches Stutter bei Bomben-Spam
- **Räumliches Audio**: `PlaySoundPanned(key, pan)` mit Stereo-Pan basierend auf `bomb.GridX / Grid.Width`
  → Bomben-Klang folgt der Bombe auf der Landscape-Achse
- **ISoundService**: `pitch` + `pan`-Parameter als Default-Interface-Methoden (backward-compat)
- **Thread-Safety**: `lock(_musicLock)` für MediaPlayer

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
// Shake = MaxAmplitude * trauma²  (quadratisch — kleine Werte kaum spürbar)
// TraumaDecay = 1.5/s linear
// Distanz-Skalierung: TriggerAt(amount, distanceCells, falloffCells=4)
// Bestand-API kompatibel: Trigger(intensity, duration) mappt auf Trauma
```

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

**Firebase-Service-Stubs** (bereit für Aktivierung nach Console-Setup):
`AndroidTelemetryService`, `AndroidAnalyticsService`, `AndroidPushNotificationService`
in `BomberBlast.Android/`. Alle erfüllen jeweilige Interfaces. Echte SDK-Aufrufe als
`// TODO`-Kommentare — nach NuGet-Install + Console-Setup in ~1.5h aktivierbar.
Setup-Anleitung: `src/Apps/BomberBlast/FIREBASE_SETUP.md`.

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
// FALSCH: Version = 1  (triggert V1→V3-Migration auf leerem Snapshot → Default-Auffüllung)
// RICHTIG:
Version = CloudSaveSchemaMigrator.CurrentSchemaVersion
```

### Firebase-Rules — Spezifisch vor Wildcard

`daily_race`-Rule MUSS vor `$tier`-Wildcard in `bomberblast-league.rules.json` stehen.
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

---

## Services-Übersicht

| Service | Zweck |
|---------|-------|
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
| `IBossRushService` | 5-Boss-Sequenz, ISO-8601-Year-Week-Reset |
| `ILoadoutService` | Pre-Run Boosts pro Story-Level (max 2 Boosts) |
| `IMasterModeService` | Master-Mode-Status pro Level, IsActive-Toggle |
| `IDeckTelemetryService` | Used/Plays/Wins pro BombType (Balance-Telemetrie) |
| `ITelemetryService` | Crashlytics-Wrapper (NullTelemetryService auf Desktop) |
| `IAnalyticsService` | Firebase Analytics (NullAnalyticsService auf Desktop) |
| `IPushNotificationService` | FCM + AlarmManager (NullPushNotificationService auf Desktop) |

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

- `MainViewModel.EnsureXxxVm()` als idempotenter Guard vor jeder Navigation
- `IGameJuiceEmitter`: Einheitliches Interface für FloatingText + Celebration (implementiert
  von LevelSelectVM, MainMenuVM, ShopVM, GameOverVM, ProfileVM)
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
```

**Build-Hygiene**: 0 Warnungen in Shared + Android. SkiaSharp 3.x Text-Rendering:
gepoolte `SKFont`-Objekte + `canvas.DrawText(text, x, y, SKTextAlign, SKFont, SKPaint)`.
Keine `SKPaint.TextSize/TextAlign/FakeBoldText` mehr (deprecated).

---

## Verweise

- Haupt-CLAUDE.md (`F:\Meine_Apps_Ava\CLAUDE.md`): Build-System, allgemeine Conventions,
  Troubleshooting, Keystore, Ad-Pattern, DI-Pattern
- `database.rules.json`: Firebase-Security-Rules (Liga + Daily-Race + Reports)
- `src/Apps/BomberBlast/FIREBASE_SETUP.md`: Crashlytics/Analytics/FCM-Setup-Anleitung (~1.5h)
- `src/Apps/BomberBlast/BOMBERBLAST_AAA_AUDIT.md`: Vollständiger Audit-Katalog
- `tests/BomberBlast.Tests/`: 286 Tests (xUnit)
