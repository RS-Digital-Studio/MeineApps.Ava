# BomberBlast (Avalonia)

> Für Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Bomberman-Klon mit SkiaSharp Rendering, AI Pathfinding und mehreren Input-Methoden.
Landscape-only auf Android. Grid: 15x10. Zwei Visual Styles: Classic HD + Neon/Cyberpunk.

**Version:** 2.0.36 (VersionCode 46) | **Package-ID:** org.rsdigital.bomberblast | **Status:** Produktion

v2.0.36 enthält alle Inhalte von v2.0.35 plus Performance-Pass (10 Findings: SKPath.Rewind() statt Reset(), FoW Update-Skip + Render-RLE, EnemyPositionIndex Lazy-Rebuild, BlackHole/Poison-Skip, DungeonMap PathEffect-Cache, FinalBoss-Arrays static, Block-Destroy-Arrays static, HUD-Glow im Skip-Modus, Confetti Two-Pass) sowie Bestand-Bugfix für Enemy-Pin-Down bei Bombe vor Korridor (`GetRandomValidDirection` Last-Resort-Pass mit `allowBombCell`). Geschätzter Performance-Gewinn: 2-4 ms/Frame = 5-10 FPS Mid-Tier-Android.

**v2.0.36 Polish-Pass (01.05.2026, basierend auf 8-Agent-Audit):**
- **CRIT-1**: GameAssetService.Evict() Native-Bitmap-Leak gefixt — `_pendingDispose` als ConcurrentQueue mit Drain auf UI-Thread (`Dispatcher.UIThread.Post`) statt direktem Background-Dispose, schützt vor Render-Race (use-after-free auf Android).
- **PERF-1**: Boss-Asset-Pfade als static readonly Array (5 Strings) statt pro Frame interpoliert. ~7 KB/s GC-Druck weniger in Boss-Levels.
- **PERF-3**: Asset-Cache von 50 MB auf 30 MB Default — Sicherheitsmarge für 200-EUR-Mid-Tier-Android (3 GB RAM).
- **PERF-6**: GameViewModel + GameEngine + GameRenderer als `Lazy<GameViewModel>` in MainViewModel — spart 100-200ms Time-to-Interactive auf Low-End-Android, weil schwere SkPaint/SkFont/SkMaskFilter-Allokationen erst beim ersten Game-Start passieren statt in der Splash/Loading-Pipeline. EnsureGameVm() idempotent. DisposeServices() prüft GameVm != null bevor Engine/Renderer disposed werden (sonst würde Shutdown sie erst instanziieren).
- **BAL-2**: CoinBonus L2 von +25% auf +60% erhöht. L2-Preis 17.000 amortisiert sich jetzt nach ~10 Welt-3-Levels (vorher 15). Switch-Expression in GameEngine.Level.cs statt linearer Formel.
- **CONV-1**: GameEngine `IAppLogger` injiziert; `Debug.WriteLine` für Master-Mode-Downgrade durch `_logger.LogWarning` ersetzt — auch im Release-Build (Android LogCat) sichtbar.
- **ARCH-1**: `BomberBlast.Models.Levels.LevelGenerator` (statische Layout-Klasse, 960 Zeilen) zu `LevelLayoutGenerator` umbenannt. Eliminiert den dokumentierten Namespace-Konflikt mit `Core.LevelGeneration.LevelGenerator` (DI-Service). App.axaml.cs + GameEngine.cs verwenden jetzt vereinfachte Imports statt vollqualifizierter Typen.
- **L10N**: SurvivalMode "Überleben", DailyTimeRemaining "Neu in", DungeonUpgradesTitle "Verbesserungen" (DE-EN-Copies behoben). DungeonTitle/MutatorActive bleiben als Gaming-Anglizismen.
- **UX-2**: Welt-Gate-Hint im LevelSelect — gesperrte Welten zeigen jetzt "Noch X ★" (Differenz statt absoluter Schwellwert) plus Hint "Tipp: Wiederhole Level Y" mit niedrigstem verbesserbaren Level. Neue RESX-Key `WorldLockHint` in allen 6 Sprachen.

**v2.0.36 Review-Fixes (nach code-review-Audit):**
- **REVIEW-CRIT-1**: PERF-6 Lazy-Refactor wirkte nur auf Desktop, weil `MainActivity.OnCreate` `App.Services.GetService<GameViewModel>()` und `<GameEngine>()` direkt aufrief → sofortige Auflösung. Fix: `AndroidVibrationService` (BomberBlast.Android) + neue `IVibrationService.VibrateTick()`-Methode (15ms). GameEngine ruft `_vibration.VibrateTick()` selbst im DirectionChanged-Handler auf — keine MainActivity-Subscription mehr nötig. MainActivity-DispatchKeyEvent/DispatchGenericMotionEvent nutzen jetzt `_mainVm.GameVm`-Property statt eager DI-Auflösung. Damit wirkt Lazy auch auf Android.
- **REVIEW-CRIT-2**: NavigateTo "Game"-Case Race — `IsGameActive=true` wurde VOR `EnsureGameVm()` gesetzt → ein Frame mit IsGameActive=true + GameVm=null möglich. Reihenfolge umgedreht: erst Ensure, dann Active.
- **REVIEW-VERB-1**: Boss-Asset-Pfade waren dreifach dupliziert (GameRenderer.Bosses + LoadingPipeline + konzeptionell GameAssetPaths). Single Source of Truth in `GameAssetPaths.BossAssetPaths` + `GetBossAssetPath(BossType)`. Beide Verwender ziehen daraus.

## Icon-System (Eigene Neon Arcade Icons)

- **Kein Material.Icons** - eigenes GameIcon-System mit 152 Icons
- `Icons/GameIcon.cs`: Custom `PathIcon`-Ableitung mit `StyleKeyOverride => typeof(PathIcon)`
- `Icons/GameIconKind.cs`: Enum mit allen verfügbaren Icons
- `Icons/GameIconPaths.cs`: Eigene geometrische SVG-Pfade im "Neon Arcade" Stil (nur M/L/H/V/Z)
- `Icons/GameIconRenderer.cs`: SkiaSharp-Renderer für Icons auf SKCanvas (gecachte SKPath)
- **Design-Sprache**: Oktagone (8 Seiten, flach) statt Kreise, scharfe Kanten, Arcade-Aesthetik
- **Converter**: `StringToGameIconKindConverter` für String→GameIconKind in XAML-Bindings
- XAML-Namespace: `xmlns:icons="using:BomberBlast.Icons"`

## AI-generierte Visual Assets (Dark Fantasy Arcade)

- **Stil**: Dark Fantasy Arcade - dramatische Beleuchtung, leuchtende Akzente auf dunklem Hintergrund
- **Checkpoint**: DreamShaper XL Alpha2, DPM++ 2M Karras, 30 Steps, CFG 7.0
- **Pipeline**: SDXL txt2img (1024x1024) → RealESRGAN 4x → Lanczos Downscale → WebP
- **GameAssetService**: LRU-Cache (50MB), ConcurrentDictionary + Lazy<Task> Deduplication
  - `GameAssetService.Current`: Statischer Accessor für Renderer (statische Klassen ohne DI)
  - `GameAssetService.PlatformAssetLoader`: Android Assets.Open() in MainActivity gesetzt
  - Desktop: `avares://BomberBlast.Shared/Assets/visuals/{path}`
  - Preload in LoadingPipeline: Splash, Menu-BGs, Bosse
- **Hybrid-Rendering**: Renderer laden AI-Bitmap, Fallback auf prozedurales Rendering
- **Lade-Strategien**: `GetBitmap()` für preloaded Assets (Bosse, Menu-BGs), `GetOrLoadBitmap()` für lazy-loaded Assets (Welten, Enemies, PowerUps, Shop, Achievements) — triggered async Laden, nächster Frame hat Bitmap
- **Asset-Ordner**: `Assets/visuals/` mit Unterordnern: bosses/, cards/, worlds/, enemies/, powerups/, menu_bg/, shop/, achievements/
- **164 Assets in 4 Phasen**: Splash, Bosse, Karten, Welten, Gegner, PowerUps, Shop, Dungeon, Skins
- **Modifizierte Renderer**: MenuBackgroundRenderer, HelpIconRenderer, ShopIconRenderer, LevelSelectVisualization, AchievementIconRenderer, GameRenderer.Bosses

## Haupt-Features

### SkiaSharp Rendering (GameRenderer - 7 Partial Classes)
- Volle 2D-Engine via SKCanvasView (Avalonia.Skia)
- Zwei Visual Styles: Classic HD + Neon/Cyberpunk (IGameStyleService)
- 60fps Game Loop via DispatcherTimer (16ms) in GameView.axaml.cs, InvalidateSurface() treibt PaintSurface
- DPI-Handling: `canvas.LocalClipBounds` statt `e.Info.Width/Height`
- GC-Optimierung: Gepoolte SKPaint/SKFont/SKPath, HUD-String-Caching (inkl. SurvivalKills), gecachter SKMaskFilter, gecachter Enrage-SKColorFilter, separater Combo-SKFont. Statische Cleanup()-Methoden in ExplosionShaders, MenuBackgroundRenderer, HelpIconRenderer. GameOverVisualization/HudVisualization/RarityRenderer: Gecachte MaskFilter+Font+SolidColor statt pro-Frame-Allokation
- **SKPath-Pooling**: _charPath1/_charPath2, _bgPath, _irisClipPath/_starPath, _torchPath, _tempPath, _poolPath1/_poolPath2. Alle mit Rewind() wiederverwendet
- **Shader-Optimierung**: Alle per-Frame SKShader-Allokationen eliminiert. Background/Vignette/DynamicLighting-Shader beim Init gecacht. Grid-Border-Transitions nutzen 2-Step-Alpha statt LinearGradient. DynamicLighting: 3 statische MaskFilter-Tiers statt pro-Licht Allokation
- **EnemyPositionCache**: Lists via Clear() wiederverwendet, periodischer Cleanup alle 120 Frames
- **IEnumerable→List**: Render()/CollectLightSources()/TrailSystem.Update() nehmen List<T> statt IEnumerable<T>
- Rainbow-Explosion-Skin: HSL-Farben nur alle 3 Frames aktualisiert
- HUD: Side-Panel rechts (TIME, SCORE, COMBO, LIVES, BOMBS/FIRE, PowerUp-Liste, Dungeon-Buffs, Karten-Slots)
- **Partial Classes**: GameRenderer.cs (Core/Palette/Viewport), .Grid.cs, .Characters.cs, .Bosses.cs, .Items.cs, .Atmosphere.cs, .HUD.cs
- **ReducedEffects**: Deaktiviert alle atmosphärischen Systeme
- **Boden-Cache**: Alle 150 Floor-Tiles als SKBitmap gecacht, invalidiert bei Welt-/Style-Wechsel
- **EnemiesRemaining-Cache**: Dirty-Flag statt pro-Frame O(n) Iteration über Gegner-Liste
- **Torch-Position-Cache**: Fackel-Positionen einmalig pro Level gecacht statt pro-Frame Grid-Scan (117→max 4 Iterationen)
- **HeatShimmer**: Direkte Koordinaten statt canvas.Save/Translate/Restore pro Band
- **GameAssetService**: LINQ-freies EvictOldest (manuelles Min statt OrderBy)
- **Compiled Bindings**: Alle 23 Views nutzen `x:CompileBindings="True"` + `x:DataType`
- **RarityRenderer**: 4 statische gecachte MaskFilter (3 Glow-Radien + 1 Shimmer), gecachter BadgeFont, SolidColor statt Shader in DrawRarityBackground
- **CollectLightSources**: Kein 150-Zellen-Grid-Scan mehr. Lava/Eis via `specialEffectCells` Dirty-Liste, Exit direkt via `exitCell` Parameter
- **ShadowRealm**: MaskFilter-Blur auf dekorativen DrawOvals entfernt (alpha=40, kaum sichtbar)
- **Splitter-Offsets**: Statisches Array statt pro-Kill Heap-Allokation
- **Touch-Targets**: Alle Buttons auf min. 44dp (Haupt-Navigation 48dp) - Android-Mindestgröße
- **KeyFrame-Safety**: Keine TranslateTransform.Y in Style.Animations (nur Opacity in KeyFrames)
- **Overlay-String-Caching**: Alle State-Overlay-Texte (Starting/Paused/LevelComplete/GameOver/Victory) gecacht bei State-Wechsel statt pro-Frame GetString()+Format()
- **_bossBitmapPaint**: Instanz-Feld statt static (Thread-Safety bei per-Frame ColorFilter-Mutation)

### Atmosphärische Subsysteme (5 Systeme, alle struct-basiert)
| System | Beschreibung |
|--------|-------------|
| DynamicLighting | Radius-basierte Lichtquellen (Bomben, Explosionen, Lava, Eis, PowerUps, Exit, Bosse, Fackeln), SKBlendMode.Screen |
| WeatherSystem | Welt-spezifische Wetter-Partikel (Blätter, Funken, Tropfen, Asche, Sand, Blasen etc.), struct-Pool 80 max |
| AmbientParticleSystem | Hintergrund-Partikel (Glühwürmchen, Dampf, Kristalle, Vögel, Glut etc.), struct-Pool 60 max |
| ShaderEffects | GPU-basierte Post-Processing (SkSL Water Ripples + CPU-Fallback, Color Grading, Chromatic Aberration, Damage Flash, Heat Shimmer) |
| TrailSystem | Charakter-Spuren (Spieler-Fußabdrücke, Ghost-Afterimages, Boss-Trails), struct-Pool 40 max |

### Prozedurale Texturen (ProceduralTextures.cs)
- Noise2D/Fbm (Perlin-ähnlich), CellRandom (deterministisch pro Zelle)
- 12 Textur-Funktionen für 10 Welt-spezifische Boden/Wand/Block-Texturen

### SkiaSharp Renderer (14 Stück)
| Renderer | Beschreibung |
|----------|-------------|
| GameRenderer | Haupt-Spiel-Rendering (Grid, Entities, Explosions, HUD, Boss-HP + Attack-Telegraph) |
| ExplosionShaders | CPU-basierte Flammen: Arm-Rendering (Bezier-Pfade, FBM Noise), Heat Haze |
| ParticleSystem | Struct-Pool (300 max), 4 Formen, Glow-Effekte |
| ScreenShake | Explosions-Shake (3px) + Player-Death-Shake (5px) |
| GameFloatingTextSystem | Score-Popups, Combo-Text, PowerUp-Text (Struct-Pool 20 max) |
| TutorialOverlay | 4-Rechteck-Dimming + Text-Bubble + Highlight |
| HelpIconRenderer | Statische Enemy/Boss/PowerUp/BombCard Icons, gepoolte SKPaint + gecachte SKMaskFilter |
| HudVisualization | Animierter Score-Counter + pulsierender Timer + PowerUp-Icons mit Glow |
| LevelSelectVisualization | Level-Thumbnails mit 10 Welt-Farben + Gold-Shimmer Sterne + Lock-Overlay |
| AchievementIconRenderer | 5 Kategorie-Farben, Trophy/Lock-Overlay mit Fortschrittsring |
| GameOverVisualization | Score mit Glow + Breakdown-Balken + Medaillen + Coin-Counter |
| DiscoveryOverlay | Erstentdeckungs-Hint (Gold-Rahmen, NEU!-Badge, Auto-Dismiss 5s) |
| ShopIconRenderer | 12 prozedurale Shop-Upgrade-Icons, gepoolte SKPaint |
| MenuBackgroundRenderer | 7 Themes (Default/Dungeon/Shop/League/BattlePass/Victory/LuckySpin), max 60 struct-Partikel/Theme |
| DungeonMapRenderer | Dungeon Node-Map (Slay the Spire-inspiriert): 10 Reihen x 2-3 Nodes, Raum-Typ-Icons |
| TornMetalRenderer | Prozeduraler "Torn Metal" Button-Hintergrund (Metallischer Gradient, gezackte Kanten, Risse, Nieten) |

### Input-Handler (3x)
- **NeonJoystick** (`Input/NeonJoystick.cs`): Custom Touch-Joystick im Neon-Arcade-Stil der App. Oktagonale Basis (wie GameIcon-System), Orange-Glow (#FF6B35), Cyan-Akzent-Ring (#22D3EE), 4 Richtungs-Pfeile die bei aktiver Richtung aufleuchten, Gold-Trail (#FFDD33) hinter dem Stick bei Bewegung, Idle-Pulsation im Fixed-Modus, Gold-Launch-Flash bei Touch-Start. Bomb-Button: Oktagonal mit Rot-Orange-Glow + pulsierender Cyan-Funke auf der Lunte. Detonator-Button: Oktagonal mit Cyan-Glow + stilisiertem Blitz. Zwei Modi: Floating (Standard, linke 60%) + Fixed (immer sichtbar unten links). Radius 75dp, Bomb 52dp, Detonator 48dp. Deadzone Fixed 15% / Floating 5%, Richtungs-Hysterese 1.15x. Following-Base wenn Finger aus Radius rutscht. Multi-Touch Pointer-ID-Tracking fuer gleichzeitigen Joystick+Bomb-Input. **Separate Pointer-IDs** fuer Bomb und Detonator (_bombButtonPointerId + _detonatorPointerId) — verhindert Button-Hang bei gleichzeitigem Tap. **BombPressed-Race-Schutz** — OnTouchEnd setzt _bombPressed/_detonatePressed nach Konsum sofort auf false, damit Taps <16ms nicht zwischen Update-Frames haengenbleiben. Performance: alle SKPaint gepoolt, SKPath via Rewind(), 3 statisch gecachte SKMaskFilter (Soft/Medium/Hard Blur), Trail als Struct-Array mit Age=999f-Init (keine Geister-Dots), Frame-Counter-basiertes SoftGlow-Skipping (alle 2 Frames, bei Press immer) fuer Bomb/Detonator spart 2-4ms GPU auf Low-End-Androids. Arrow-Path einmal gebaut + zweimal gezeichnet (Glow+Fill) statt doppelt bauen.
- **Pre-Turn Buffering** (Player.cs): Richtung wird gepuffert wenn Spieler nicht am Zellzentrum, Turn bei 40% Zellzentrum-Nähe
- **Keyboard**: Arrow/WASD + Space (Bomb) + E (Detonate) + T (ToggleSpecialBomb) + Escape (Pause)
- **Gamepad**: D-Pad + Analog-Stick (4-Wege, Deadzone 0.25) + Face-Buttons
- InputManager verwaltet aktiven Handler, auto-detect Desktop vs Android
- **Auto-Switch**: Touch→Joystick, WASD→Keyboard, GamepadButton→Gamepad
- **Android Controller**: MainActivity.DispatchKeyEvent + DispatchGenericMotionEvent

### AI (EnemyAI.cs + AStar.cs)
- A* Pathfinding (Object-Pooled PriorityQueue, HashSet, Dictionaries)
- BFS Safe-Cell Finder (Pooled Queues)
- Danger-Zone: Einmal pro Frame vorberechnet via `PreCalculateDangerZone()`, Kettenreaktions-Erkennung (iterativ, max 5)
- 12 Enemy-Typen (8 Basis + 4 neue: Tanker/Ghost/Splitter/Mimic)
- **Boss-AI**: Eigene `UpdateBossAI()` - kein A*, direkter Richtungs-Check zum Spieler, Multi-Cell Kollision, Enraged-Modus halbiert Decision-Timer

### Boss-System (BossEnemy.cs)
- 5 Boss-Typen: StoneGolem, IceDragon, FireDemon, ShadowMaster, FinalBoss
- Jedes 10. Level = Boss-Level (L10-L100), Boss-Typ Repeat alle 2 Welten
- BossEnemy erbt von Enemy, eigene BoundingBox (Multi-Cell), HP 3-8, Enrage bei 50%
- **Duo-Boss-Encounter**: Welt 9 (L90) = FinalBoss + ShadowMaster, Welt 10 (L100) = 2x FinalBoss
- `Level.BossKind2`: Optionaler zweiter Boss-Typ. `SpawnBossAtPosition()` für getrennte Links/Rechts-Platzierung
- `UpdateBossAI()`: Boss-zu-Boss-Kollisionsprüfung via `OccupiesCell()`, Ausweichen senkrecht zur Zielrichtung
- Keine Begleitgegner in Duo-Boss-Leveln (2 Bosse = genug Bedrohung). Timer erhöht (300s/360s)
- Spezial-Angriffe: Telegraph (2s) → Attack (1.5s) → Cooldown (12-18s, kürzer bei Enrage)
- Arena-Mitte platziert, Blöcke werden freigeräumt

### Coin-Economy + Shop
- **CoinService**: Level-Score / 3 → Coins bei Complete (Welt 1: Score/2 für bessere Früh-Progression), / 6 bei Game Over
- **Gem-Trickle**: 3 Gems bei erstmaligem 3-Sterne-Abschluss (Story-Modus) via GameTrackingService.OnFirstThreeStars() — 100 Level × 3G = 300G (v2.0.29+: von 2G→3G erhöht, löst Kopplung an Premium-Kauf)
- **Premium-Multiplikator**: 2x Coins bei LevelComplete (IsPremium), 3x bei GameOver-Trostcoins
- **Effizienz-Bonus**: Skaliert nach Welt (1-10), belohnt wenige Bomben. **Welt 1 großzügigere Schwellen (8/14/20 Bomben) statt 5/8/12 (BAL-32, seit v2.0.31)**, weil Einsteiger noch kein Deck haben und mehr Blöcke räumen müssen. Ab Welt 2 gelten die klassischen strengeren Schwellen als Skill-Maßstab.
- **ShopService**: 9 permanente Upgrades (StartBombs, StartFire, StartSpeed, ExtraLives, ScoreMultiplier, TimeBonus, ShieldStart, CoinBonus, PowerUpLuck)
- **Preise**: 700 - 17.000 Coins (StartBombs/StartFire: 700/2.500/7.000, StartSpeed: 1.200/2.500/7.000 [BAL-33 seit v2.0.31: MaxLevel 1 -> 3 erweitert, 3-stufige Progression wie Bombs/Fire]), Max-Levels: 1-3, Gesamt: ~138.600 Coins
- **Dungeon-Trennung**: Shop-Upgrades gelten NUR in Story/Daily/QuickPlay/Survival. Im Dungeon: Base-Stats + Dungeon-Buffs

### Level-Gating (ProgressService)
- 100 Story-Level in 10 Welten (World 1-10 a 10 Level)
- Welt-Freischaltung: 0/0/10/25/45/70/100/135/155/180/200 Sterne (v2.0.29+: Welt 9/10 abgesenkt; v2.0.36: Welt 8 auf 155, Welt 9/10 leicht justiert — Endgame realistisch erreichbar)
- Stern-System: 3 Sterne pro Level (Zeit-basiert), Fail-Counter für Level-Skip
- **Mutator-Bonus (v2.0.29+)**: Mutator-Level (ab Welt 6) schenken 3 garantierte Sterne bei Completion. Grund: DoubleSpeed/MirrorControls/InvisibleBlocks machen 3-Sterne-Runs statistisch unwahrscheinlich → Schwierigkeit = Belohnung, nicht Strafe. Implementiert in `GameEngine.Level.cs` via `scoreToSave = Max(playerScore, baseScore*3)`

### Progressive Feature-Freischaltung (MainMenuViewModel)
| Level | Features |
|-------|----------|
| 0-2 | Story, Settings, Help, Profile |
| 3+ | + Shop |
| 5+ | + Survival, QuickPlay |
| 8+ | + DailyChallenge, LuckySpin |
| 10+ | + Achievements, Statistics, Collection |
| 15+ | + Deck, DailyMissions, WeeklyMissions |
| 20+ | + Dungeon |
| 30+ | + League, BattlePass |

"NEU!"-Badges via `IPreferencesService` (`feature_seen_{name}`)

## Premium & Ads

### Premium-Modell
- **Preis**: 1,99 EUR (`remove_ads`)
- Kostenlos spielbar, Upgrades grindbar, Ads optional

### Fullscreen/Immersive Mode (Android)
- WindowInsetsController in OnCreate + OnResume + OnWindowFocusChanged
- TransientBarsBySwipe (Wisch-Geste zeigt Bars kurz an)

### Ad-Banner-Spacer (MainView)
- Grid `RowDefinitions="*,Auto"` → Row 0 Content, Row 1 Ad-Spacer (64dp)
- IsAdBannerVisible gesteuert per Route (Game=false, andere=BannerVisible)
- Dialoge/Overlays: `Grid.RowSpan="2"`

### Rewarded (5 Placements)
1. `continue` → GameOver: Coins verdoppeln (1x pro Versuch)
2. `level_skip` → GameOver: Level überspringen (ab 1. Fail, v2.0.29+: von 2 Fails auf 1 Fail gesenkt). Skip setzt Score auf `GetBaseScoreForLevel(Level)` → 1 garantierter Stern (Welt-Gating-kompatibel)
3. `power_up` → LevelSelect: Power-Up Boost (ab Level 20)
4. `score_double` → GameView: Score verdoppeln (nach Level-Complete)
5. `revival` → GameOver: Wiederbelebung (1x pro Versuch)

**Rewarded Ad Cooldown**: 60s global zwischen allen Placements (RewardedAdCooldownTracker)

## App-spezifische Services

| Service | Zweck |
|---------|-------|
| ISoundService | Audio (NullSoundService Desktop, AndroidSoundService Android) |
| IProgressService | Level-Fortschritt, Sterne, Fail-Counter, World-Gating |
| IHighScoreService | Top 10 Scores (sqlite-net-pcl) |
| IGameStyleService | Visual Style Persistenz (Classic/Neon) |
| ICoinService | Coin-Balance, AddCoins, TrySpendCoins |
| IGemService | Gem-Balance (zweite Währung), AddGems, TrySpendGems |
| IShopService | PlayerUpgrades Persistenz, Preise, Kauf-Logik |
| ITutorialService | 6-Schritte Tutorial für Level 1 |
| IDailyRewardService | 7-Tage Login Bonus + Comeback-Bonus (>3 Tage inaktiv) |
| IStarterPackService | Einmaliges Starterpaket nach Level 5 (5000 Coins + 20 Gems + 3 Rare-Karten) |
| ICustomizationService | Spieler-Skins (5 Coin + 3 Gem-Skins), TryPurchaseWithGems() |
| IReviewService | In-App Review nach Level 3-5, 14-Tage Cooldown |
| IAchievementService | 66 Achievements in 5 Kategorien, JSON-Persistenz |
| IDiscoveryService | Erstentdeckungs-Tracking (PowerUps/Mechaniken), Preferences-basiert |
| IDailyChallengeService | Tägliche Herausforderung, Streak-Tracking, Score-Persistenz |
| IPlayGamesService | Google Play Games Services v2 (Leaderboards, Online-Achievements, Auto-Sign-In) |
| ILuckySpinService | Glücksrad: 8 gewichtete Segmente, 1x gratis/Tag |
| IWeeklyChallengeService | 5 wöchentliche Missionen aus 14er-Pool, Montag-Reset |
| IDailyMissionService | 3 tägliche Missionen aus 14er-Pool, Mitternacht-UTC-Reset |
| ICardService | 14 Bomben-Karten, Deck (4+1 Slots), Upgrade (Bronze→Silber→Gold), Drops |
| IDungeonService | Dungeon-Run Roguelike: Run-State, 16 Buffs, Raum-Typen, Node-Map, Ascension, Synergies |
| IDungeonUpgradeService | 8 permanente Dungeon-Upgrades (DungeonCoins-Währung) |
| ICollectionService | Sammlungs-Album: Gegner/Bosse/PowerUp-Tracking, Meilenstein-Belohnungen |
| IFirebaseService | Firebase REST API: Anonymous Auth + Realtime Database CRUD |
| ILeagueService | Liga-System: 5 Tiers (Bronze→Diamant), 14-Tage-Saisons, Firebase + NPC-Backfill |
| ICloudSaveService | Cloud Save: Local-First Sync, 35 Keys, Debounce 5s, Konflikt-Resolution |
| IBattlePassService | 30-Tier Saison, XP-basiert, Free/Premium-Track, XP-Boost (2x 24h) |
| IRotatingDealsService | 3 tägliche + 1 wöchentliches Deal, 20-50% Rabatt |
| IGameAssetService | AI-generierte WebP-Bitmaps, LRU-Cache 50MB, Preload in Pipeline |

## Architektur-Entscheidungen

### Compiled Bindings in DataTemplates
- Alle DataTemplates MÜSSEN `x:DataType` angeben in CompileBindings="True" Views
- Parent-ViewModel-Commands in DataTemplates: `{Binding $parent[ItemsControl].((vm:XxxViewModel)DataContext).CommandName}`
- KEIN ReflectionBinding - immer Compiled Binding mit $parent[]-Syntax
- Referenz-Pattern: ShopView.axaml:111

### Performance-Conventions (Render-Loop)
- Shader pro Frame: Gecachter SKShader mit Frame-Skipping (alle 2 Frames), kein pro-Frame ToShader()
- ToString() in Render-Loops: Statische String-Arrays für bekannte Wertebereiche
- AStar-Ergebnisse: Enemy besitzt eigene Queue, Pfad wird via CopyPathFrom() kopiert - keine Queue-Referenz auf AStar halten
- SKPath-Pooling: ProceduralTextures nutzt statischen `_poolPath` mit `Rewind()`. GameRenderer nutzt `_tilePath` für Wand/Block-Details. KEIN `new SKPath()` im Render-Loop
- MaskFilter-Caching: HUD-Icon Glow gecacht in HudVisualization. DungeonMap Node-Glow gecacht. KEIN `CreateBlur()` pro Frame

### Security-Maßnahmen
- Spielername: Profanity-Filter in LeagueService.SetPlayerName() (HashSet Blocklist, case-insensitive)
- Firebase: Security Rules in Console setzen: `"$uid": { ".write": "auth.uid === $uid" }` + Score-Hard-Cap
- Premium/Coins: Client-seitig (akzeptabel für 1,99€ App, RestorePurchases bei Start)

### GameEngine-Extraktionen (v2.0.30+)

Gezielte Entschlackung der GameEngine-God-Class in 3 Phasen. **−836 Zeilen** aus GameEngine.

**Extrahierte Dateien unter `Core/LevelGeneration/` und `Core/Combat/`:**

| Datei | Zweck | Zeilen |
|-------|-------|-------:|
| `LevelGeneration/ILevelGenerator.cs` | Interface + `LevelGenerationContext` | 55 |
| `LevelGeneration/LevelGenerator.cs` | `GetMutatorDisplayName`, `PlacePowerUps`, `PlaceExit`, `SpawnEnemies`, `SpawnBossAtPosition` | 301 |
| `LevelGeneration/MutatorEffects.cs` | Static-Player-State-Modifier für 5 Mutator-Typen | 46 |
| `Combat/SpecialExplosionEffects.cs` | 13 Handle*-Methoden (Ice, Fire, Sticky, Smoke, Lightning, Gravity, Poison, TimeWarp, Mirror, Vortex, Phantom, Nova, BlackHole) + `ExplosionEffectsContext` | 518 |
| `Combat/EnemyPositionIndex.cs` | Gegner-Positions-Cache mit O(1)-Lookup (Rebuild + TryGetAt + Clear) | 94 |

**GameEngine-Zeilen-Reduktion:**

| Partial | Vorher | Nachher | Delta |
|---------|-------:|--------:|------:|
| `GameEngine.Level.cs` | 1580 | 1315 | −265 |
| `GameEngine.Explosion.cs` | 1209 | 673 | **−536** |
| `GameEngine.Collision.cs` | 608 | 549 | −59 |
| `GameEngine.cs` | 1818 | 1842 | +24 (Context-Setup) |
| **GameEngine gesamt** | **5941** | **5105** | **−836** |

**Architektur-Pattern der Extraktionen:**

1. **Pure Funktionen als Static** (`MutatorEffects`, `SpecialExplosionEffects`): Zustandslos, keine DI, Context als Parameter. Lazy-initialisierter `_explosionCtx` im Engine (Feld + Getter) vermeidet Allokation pro Frame.

2. **State-Container als Instance** (`LevelGenerator`, `EnemyPositionIndex`): DI-Singleton mit internen gepoolten Listen für Allocation-Free-Performance.

3. **Callbacks an Engine** (in `ExplosionEffectsContext`): `DestroyBlock`, `KillEnemy`, `ProcessExplosion` als Delegates weil sie engine-interne Invarianten (Score/Events/State-Machine) mutieren und nicht in eine Extract-Datei gehören.

### LevelGenerator-Extraktion (v2.0.30+)

- **`Core/LevelGeneration/ILevelGenerator.cs`** + **`LevelGenerator.cs`** + **`LevelGenerationContext`**: Extraktion von Level-Fabrik-Logik aus `GameEngine.Level.cs` (−238 Zeilen, 1580 → 1342). Enthält: `GetMutatorDisplayName`, `PlacePowerUps`, `PlaceExit`, `SpawnEnemies` (inkl. `SpawnBossAtPosition`).
- **Zustandslos (Singleton)**, eine einzige Dependency (`ILocalizationService` für Mutator-Namen). Kein Game-Event, kein Sound, kein Tracking — das bleibt in GameEngine.
- **Kommunikation per Context + Return-Values**: `LevelGenerationContext { Grid, CurrentLevel, Random, PowerUpLuckLevel }` in, `List<Enemy>` aus `SpawnEnemies`. GameEngine hängt die Ergebnisse selbst in `_enemies` und feuert `_tracking.OnBossEncountered(boss.BossKind)` für Boss-Instanzen.
- **Wiederverwendbare interne Listen** (`_blockCells`, `_farBlocks`, `_validPositions`) sind vom GameEngine in den Generator gewandert → keine Heap-Allokation pro Level-Start.
- **Bewusst NICHT extrahiert**: `ApplyMutatorEffects` (mutiert `_player.SpeedLevel/HasPowerBomb/FireRange` — gehört in Engine), `CollisionSystem` (zu enge Kopplung mit KillPlayer/Score/Events), `ExplosionSystem` (15 `Handle*`-Methoden mit zu vielen Engine-Mutationen — grenzwertiger Gewinn). Plan dokumentiert unter `C:\Users\rober\.claude\plans\compiled-tinkering-adleman-agent-a228d371b1b660417.md`.

### Build-Hygiene (v2.0.30+)

- **0 Warnungen in Shared + Android**: Alle deprecated APIs entweder migriert (SkiaSharp 2.x → 3.x: SKFont statt SKPaint.TextSize/TextAlign/FakeBoldText) oder gezielt mit `#pragma warning disable` + Begründungskommentar markiert (Android-Framework-Vorgaben: OnBackPressed, SetDecorFitsSystemWindows, SystemUiVisibility).
- **SkiaSharp 3.x Text-Rendering**: Paints enthalten KEINE TextSize/TextAlign/FakeBoldText mehr. Stattdessen: Gepoolte `SKFont`-Objekte + `canvas.DrawText(text, x, y, SKTextAlign, SKFont, SKPaint)` als Overload. Betrifft `DungeonMapRenderer`, `LuckySpinView`.

### Lokalisierung (v2.0.30+)

- **Alle Floating-Texte im GameEngine lokalisiert**: `FloatShield`, `FloatPhantom`, `FloatRegen`, `FloatFall`, `FloatBossBlockRain`, `FloatBossIceBreath`, `FloatBossLavaWave`, `FloatBossTeleport`, `FloatMimic`, `FloatLava`, `AdLoadFailed` in 6 Sprachen + Designer. Vorher waren Boss-Attack-Namen teils auf Deutsch hart-codiert ("BLOCKREGEN!", "EISATEM!", "LAVA-WELLE!") — jetzt sauber per `GetString(key) ?? fallback` lokalisiert.
- **Overlay-Strings sprachwechsel-aware**: `CacheHudLabels()` (triggered bei LanguageChanged) ruft jetzt auch die dynamischen Overlay-Cacher (`CacheStartingOverlayStrings`, `CacheLevelCompleteOverlayStrings`, `CacheGameOverOverlayStrings`, `CacheVictoryOverlayStrings`) auf, wenn bereits ein Level aktiv ist. Vorher blieben "Stage X"/"Score: X"/"Level X" in alter Sprache.

### Persistenz-Robustheit (v2.0.30+)

- **PersistenceHealth**: Zentrale Static-Klasse. CoinService/GemService/ProgressService/DailyRewardService rufen `PersistenceHealth.ReportCorruption(serviceName, ex)` bei JSON-Parse-Fehlern oder Negativ-Werten auf. CloudSaveService prüft `WasCorruptionDetected` in ALLEN drei Sync-Pfaden (Pull, SchedulePush, ForceUpload) und erzwingt Cloud-Pull statt Push, damit ein einzelner Parse-Fehler NICHT die Cloud mit Leer-State überschreibt (Data-Loss-Prävention).
- **RewardedAdCooldownTracker**: Hybrid-Cooldown-Schutz — `Environment.TickCount64` (monotone System-Uhr, gegen Clock-Skew rueckwaerts) PLUS persistierte `DateTime.UtcNow` in Preferences (gegen App-Restart-Bypass). OR-Verknüpft: Cooldown aktiv wenn eine der Uhren noch im 60s-Fenster. Negative UTC-Differenzen zählen als "gerade geschehen".
- **DailyRewardService**: `LastClaimDate` wird per `Max(UtcNow, previous)` geclampt → Clock-Skew vorwaerts kann den 7-Tage-Zyklus nicht mehr durchlaufen.
- **CoinService/GemService AddXxx**: Overflow-Guard via `(long)Balance + amount` + Clamp auf `int.MaxValue` → Silent-Negativ bei > 2,147 Mrd verhindert. Zusätzlich: `Load()` clampt Balance/TotalEarned < 0 auf 0 + Corruption-Flag → schützt gegen historische Overflow-Spuren aus pre-v2.0.30.
- **GameEngine**: Idempotenz-Guard in `CompleteLevel()` (`if (_state != Playing) return;`) → keine Race zwischen Tod/Exit im selben Frame. State-Guard in `OnTimeExpired` → kein Pontan-Spawn nach LevelComplete. `CompleteLevel()`-Reihenfolge: `SetLevelBestScore` → Tracking (Achievement/BattlePass/Liga/Missions) → `FlushIfDirty` → `_progressService.CompleteLevel(n)` als letzte Aktion → Crash zwischen Tracking und CompleteLevel verliert nur die Completion-Markierung (Replay möglich), nicht die Tracking-Daten.
- **NoTimer-Mutator**: `timeBonus` wird bei `Mutator == NoTimer` auf 0 gesetzt → verhindert Score-Farming (vorher: 99999 * Multiplier). Coin-Payout für Mutator-Level basiert auf `Max(echter Score, baseScore*3)` → Spieler bekommt 3 Sterne UND faire Coins, meidet Mutator-Level nicht wegen reduzierter Belohnung.
- **GameTrackingService.OnSurvivalEnded**: HashSet-Update + Save BEVOR AddCoins/AddGems → Meilenstein wird bei Crash nicht doppelt vergeben.
- **GameOverViewModel**: `_adInFlight`-Gate gegen Double-Continue-Race bei Rewarded Ad. Ad-Load-Fail zeigt FloatingText mit `AdLoadFailed`-RESX-Key.
- **LeagueService.NormalizeForProfanityCheck**: Unicode-NFKD + Strip Combining/Format/Control + Non-Alnum + Lowercase vor Blocklist-Contains-Check → "F.u.c.k", "fvck", "FÜCK", Zero-Width-Spaces werden erkannt. Blockliste enthält nur Tokens >= 4 Zeichen (vermeidet Substring-False-Positives wie "Cassandra"/"Passion"). Blockierte Namen werden zu `Player_XXXX` (UID-Suffix) statt "****" → Unterscheidbarkeit im Leaderboard bleibt.
- **LeagueService.StripInvisibleChars** (v2.0.31, 18.04.2026): `SetPlayerName()` entfernt `UnicodeCategory.Format` (Zero-Width-Space, ZWJ, RTL-Override, BOM) + `Control` aus dem Namen BEVOR der 16-Zeichen-Längen-Check greift. NonSpacingMark (Akzente wie "Müller") bleiben erhalten. Verhindert Leaderboard-Spoofing durch scheinbar leere oder visuell identische Namen.
- **Firebase-Rules** `bomberblast-league.rules.json` (v2.0.31): UID-gebundener Write, `.validate` für `name` (1-16 Zeichen String) + `points` (0-500.000) + `updatedUtc` (String) + `updatedMs` (Server-Timestamp). **Rate-Limit:** `.write` erlaubt neue Writes nur alle 60s pro UID via `(now - data.child('updatedMs').val()) >= 60000`. `updatedMs` wird vom Client per `{".sv":"timestamp"}`-Sentinel gesetzt und serverseitig aufgelöst — nicht manipulierbar. Schützt gegen Script-Cheating: max. 60 Writes/Stunde statt theoretisch unbegrenzt. Migration: Bestehende Einträge ohne `updatedMs` dürfen 1x geschrieben werden (`!data.child('updatedMs').exists()`). **Muss in Firebase Console deployed werden.**

- **Singleton-VM + Visual Tree**: GameView hat 3-stufige VM-Subscription: (1) OnDataContextChanged, (2) OnLoaded (für verzögertes ViewLocator-DataContext), (3) OnPaintSurface Safety-Net (startet Render-Timer nach wenn InvalidateCanvasRequested keinen Subscriber hatte). TrySubscribeToViewModel() als zentrale idempotente Methode
- **Game Loop**: DispatcherTimer (16ms), MAX_DELTA_TIME = 0.05f (50ms Cap)
- **Touch-Koordinaten**: Proportionale Skalierung (Render-Bounds / Control-Bounds Ratio)
- **Invalidierung**: IMMER `InvalidateSurface()` (nicht InvalidateVisual)
- **Keyboard Input**: Window-Level KeyDown/KeyUp in MainWindow.axaml.cs → GameViewModel
- **DI**: 23 ViewModels (Singleton), 29 Services. Zirkuläre Abhängigkeiten via `Lazy<T>`-Injection aufgelöst (LazyServiceExtensions.cs). Keine manuellen SetXxxService()-Aufrufe mehr
- **IGameJuiceEmitter**: Einheitliches Interface für FloatingText+Celebration Events. Implementiert von: LevelSelectVM, MainMenuVM, ShopVM, GameOverVM, ProfileVM und weiteren
- **GameEngine Partial Classes**: GameEngine.cs (Kern), .Collision.cs, .Explosion.cs, .Level.cs, .Render.cs
- **GameEngine Events**: Kein "On"-Prefix: `GameOver`, `LevelComplete`, `Victory`, `ScoreChanged`, `CoinsEarned`, `PauseRequested`, `DirectionChanged`, `DungeonFloorComplete`, `DungeonBuffSelection`, `DungeonRunEnd`
- **GameEngine Dispose**: Via `App.DisposeServices()` (Desktop: ShutdownRequested, Android: OnDestroy)
- **12 PowerUp-Typen**: BombUp, Fire, Speed, Wallpass, Detonator, Bombpass, Flamepass, Mystery, Kick, LineBomb, PowerBomb, Skull
- **PowerUp-Freischaltung**: Level-basiert via `GetUnlockLevel()`. Story filtert gesperrte PowerUps. DailyChallenge: alle verfügbar
- **Discovery-System**: Pausiert Spiel bei Erstentdeckung, DiscoveryOverlay (SkiaSharp)
- **Exit-Cell-Cache**: `_exitCell` in GameEngine für O(1) Zugriff
- **Coin-Berechnung**: `_scoreAtLevelStart` → Coins basieren auf Level-Score (nicht kumuliert)
- **Pontan-Strafe**: Welt-skaliert (W1: 1/8s/5s, W2: 2/6s/3s, W3+: 3/5s/0s), Vorwarnung 1.5s
- **Pfad-Invalidierung**: `InvalidateEnemyPaths()` bei Block-Zerstörung → sofortige AI-Neuberechnung
- **Slow-Motion**: 0.8s bei letztem Kill / Combo x4+, Ease-Out, Timer/Combo laufen in Echtzeit
- **Dirty-Lists**: `_destroyingCells`, `_afterglowCells`, `_specialEffectCells` statt Grid-Iteration
- **Achievement Dictionary-Lookup**: O(1) statt O(n) via `_achievementLookup`
- **CollectionService Debounce-Save**: `_isDirty` + 5s Debounce
- **GetTotalStars**: Gecacht in ProgressService, invalidiert bei Score-Änderung
- **Timer**: Läuft in Echtzeit (`realDeltaTime`), nicht durch Slow-Motion beeinflusst

### Spezial-Bomben-System (14 Typen)
- **BombType Enum**: Normal, Ice, Fire, Sticky, Smoke, Lightning, Gravity, Poison, TimeWarp, Mirror, Vortex, Phantom, Nova, BlackHole
- **3 Shop-Bomben**: Ice (Frost 3s, 50% Slow), Fire (Lava 3s, Schaden), Sticky (Kettenreaktion + Klebe 1.5s)
- **10 Karten-Bomben**: Smoke (Nebelwolke 4s), Lightning (Blitz zu 3 Gegnern), Gravity (Zug im 3-Zellen-Radius), Poison (Gift-Zellen 3s), TimeWarp (50% Slow 5s), Mirror (doppelte Reichweite), Vortex (Spiral-Explosion), Phantom (durchdringt 1 Wand), Nova (360° + PowerUp), BlackHole (Sog 3s + Explosion)
- **Verlangsamungs-Stacking**: Frost (0.5x) + TimeWarp (0.5x) + BlackHole (0.3x) multiplikativ
- **EnemyAI Smoke-Konfusion**: Zufallsbewegung statt Pathfinding

### Raritäts-System
- **4 Stufen**: Common (#FFFFFF), Rare (#2196F3), Epic (#9C27B0), Legendary (#FFD700)
- **RarityRenderer** (`Graphics/RarityRenderer.cs`): DrawRarityBorder/Glow/Shimmer/Background/Badge/Complete

### Gem-Währung
- **IGemService**: Zweite Währung neben Coins, NUR durch Gameplay verdienbar
- **Persistenz**: IPreferencesService JSON, Key "GemData"
- **Farbe**: Einheitlich Cyan `#00BCD4` in allen Views (Profil, Shop, HUD, Statistik, Floating-Text)

## Game Juice & Effects

| Effekt | Beschreibung |
|--------|-------------|
| Combo-System | Kills innerhalb 2s → Bonus (x2:+200 bis x5+:+2000), Chain-Kill 1.5x bei 3+ |
| Floating Text | Score-Popups, Combo, PowerUp-Collect, Coin-Verdopplung (Struct-Pool 20) |
| Haptic-Feedback | VibrateLight bei PowerUp/Bombe, VibrateMedium bei Exit |
| Timer-Warnung | Pulsierender roter Rand unter 30s |
| Danger Telegraphing | Rote Warnzonen bei Zündschnur < 0.8s |
| ScreenShake | Explosion (3px, 0.2s), PlayerDeath (5px, 0.3s) |
| Hit-Pause | Frame-Freeze bei Kill (50ms), Death (100ms) |
| Partikel-System | Struct-Pool (300), 4 Formen, Glow-Halo |
| Flammen-Rendering | CPU-basiert, Bezier-Pfade, 3 Schichten, FBM-Noise, Heat Haze |
| Explosions-Effekte | Funken (12), Glut (9), doppelter Shockwave-Ring, Nachglühen (0.4s) |
| Bomben-Pulsation | 8→24Hz beschleunigend + stärkere Amplitude |
| Squash/Stretch | Bomben-Birth-Bounce, Slide-Stretch, Gegner/Spieler-Tod |
| Walk-Animation | Prozedurales sin-basiertes Wippen |
| Slow-Motion | 0.8s bei letztem Kill / Combo x4+, Ease-Out 30%→100% |
| Iris-Wipe | Level-Start Kreis öffnet, Level-Complete Kreis schließt, Gold-Rand |
| Neon Style | Brightened Palette, 3D Block-Edges, Glow-Cracks, Outer-Glow HUD |
| Curse-Indikator | Pulsierender violetter Glow + HUD Typ + Timer |
| Musik-Crossfade | Fade-Out/Fade-In (0.5s) beim Track-Wechsel |
| View-Transitions | CSS-Klassen, Opacity DoubleTransition 200ms |
| Welt-Themes | 10 Farbpaletten pro Style, WorldPalette |
| Sterne-Animation | Scale-Bounce bei Level-Complete (gestaffelter Delay) |
| PowerUp-Einsammel | Shrink + Spin + Fade (0.3s) |
| Welt-Ankündigung | Großer "WORLD X!" Text bei Welt-Wechsel |
| Button-Animationen | Scale-Transition (1.05x hover, 0.95x pressed) |
| Menü-Hintergründe | MenuBackgroundCanvas (~30fps) mit 7 Themes in 15 Views |
| Splash-Screen | Cartoon-Bombe mit brennender Lunte, Feuer-Partikel, Explosions-Flash |

## Tutorial-System

- 6 interaktive Schritte: Move → PlaceBomb → Warning(Hide) → CollectPowerUp → DefeatEnemies → FindExit
- Automatischer Start bei Level 1 wenn kein Fortschritt
- SkiaSharp Overlay mit 4-Rechteck-Dimming, Text-Bubble, Highlight-Box
- Skip-Button, Warning-Schritt mit 3s Auto-Advance
- Tutorial-Replay in HelpView

## Survival-Modus

- Endloser Spielmodus: Kein Exit, 1 Leben, kein Continue
- Steigendes Spawning: Alle 4s, Intervall sinkt um 0.12s bis min 0.8s
- Gegner-Eskalation nach Zeit: <20s Ballom → 150s+ alle Typen inkl. Ghost/Pontan/Splitter
- Arena-Layout: BlockDensity 0.2, 4 Basis-PowerUps
- Timer: 99999 (kein Pontan), HUD zeigt KILLS + überlebte Zeit
- **Meilenstein-Belohnungen**: 60s=500C, 120s=1500C+3G, 180s=3000C+5G, 300s=5000C+10G
- Erstmalig volle Belohnung, danach 20% Coins (Gems nur beim ersten Mal)
- Persistenz: `SurvivalMilestonesReached` in Preferences (JSON HashSet<int>)
- Belohnungen direkt in `OnSurvivalEnded()` vergeben (kein separates Event)

## Challenge a Friend (Quick-Play)

- Seed + Schwierigkeit via UriLauncher.ShareText teilen
- QuickPlayViewModel: `ShareChallengeCommand`, `SetLastScore(int)` für Score-Sharing
- RESX-Key: `ChallengeShareText` mit Platzhaltern für Seed/Difficulty/Score

## Mutator-System (Story-Modus ab Welt 6)

- **5 Mutatoren**: AllPowerBombs, DoubleSpeed, InvisibleBlocks, NoTimer, MirrorControls
- **Zuweisung**: Level x3, x6, x9 jeder Welt ab Welt 6 (deterministisch via levelNumber % 5)
- **Level.Mutator**: Property auf Level-Model, `LevelMutator` Enum
- **GameEngine._activeMutator**: Wird bei Level-Start gesetzt, in allen 5 Modi zurückgesetzt (Story/Daily/Quick/Survival/Dungeon)
- **AllPowerBombs**: `_player.HasPowerBomb = true` + erhöhte FireRange
- **DoubleSpeed**: Spieler SpeedLevel +2, Gegner+Bosse 1.5x deltaTime-Multiplikator in UpdateEnemies()
- **InvisibleBlocks**: Blöcke in GameRenderer.Grid.cs nur sichtbar wenn Spieler Manhattan-Distanz <= 1
- **MirrorControls**: Nutzt bestehende ReverseControls-Logik (OR-Verknüpfung)
- **NoTimer**: TimeLimit 99999 im LevelGenerator
- **Renderer-Properties**: `ActiveMutator`, `PlayerGridX`, `PlayerGridY` auf GameRenderer, gesetzt in GameEngine.Render.cs
- **Ankündigung**: "Mutator: {Name}" als World-Announcement (2.5s)

## Feature-Freischaltungs-Celebrations

- Bei Erreichen einer Feature-Schwelle (L3/5/8/10/15/20/30) wird Celebration + FloatingText gezeigt
- Preferences-Key: `feature_celebration_level` verhindert Mehrfach-Auslösung
- Höchste neu erreichte Schwelle hat Priorität

## Gem-Trickle-System (erweitert)

- **Boss-Kill Gem-Drop**: 50% Chance auf 2-3 Gems bei jedem Boss-Kill (GameTrackingService)
- **Survival-Meilensteine**: Gems bei 120s/180s/300s (siehe Survival-Modus)
- **Gesamt-Quellen**: 3-Sterne (2G), Boss-Level-Erst (5G), Boss-Kill-Drop (2-3G/50%), Survival, BP, Weekly, Daily, Comeback

## Glücksrad / Lucky Spin

- 1x gratis/Tag, Extra-Spins per Ad oder 3 Gems
- 9 gewichtete Segmente: 100-3000 Coins + 5/10 Gems (Jackpot 3000C+10G, w5)
- [BAL-33 seit v2.0.31: Gem-Segment Weight 8 -> 12, Gem-Erwartungswert/Spin 0.66 -> 0.79 (+20%)]
- SKCanvasView Rad-Rendering, Spin-Animation (min. 5 Drehungen, Ease-Out)
- Gepoolte SKPaint + SKPath (Segment/Pointer), Dispose-Chain in OnDetachedFromVisualTree

## Weekly Challenge + Daily Missions

- **Architektur**: `TimedMissionServiceBase` (abstrakte Basisklasse) mit `DailyMissionService` und `WeeklyChallengeService` als Subtypen
- **Basisklasse**: Enthält gemeinsame Logik (GenerateMissions, RestoreMissions, TrackProgress, Load/Save, CheckPeriodReset)
- **Abstrakte Methoden**: GetPeriodId(), GetMissionPool(), OnMissionCompleted(), OnAllCompleteBonusClaimed(), NextResetDate
- **WeeklyMission-Modell**: Generischer Missions-Typ (wird für beide Perioden verwendet, Name historisch bedingt)
- **Weekly**: 5 Missionen/Woche aus 14er-Pool, Montag-Reset, 350-700 Coins + 2.000 All-Complete-Bonus
- **Daily**: 3 Missionen/Tag aus 14er-Pool, Mitternacht-UTC-Reset, 100-300 Coins + 500 All-Complete-Bonus
- **14 Missions-Typen**: CompleteLevels, DefeatEnemies, CollectPowerUps, EarnCoins, SurvivalKills, UseSpecialBombs, AchieveCombo, WinBossFights, CompleteDungeonFloors, CollectCards, EarnGems, PlayQuickPlay, SpinLuckyWheel, UpgradeCards
- Kombinierte Missions-View: Daily (orange) + Weekly (cyan) in 2-Spalten Layout
- GameEngine-Hooks + Service-Hooks + ViewModel-Hooks für alle 14 Tracking-Typen

## Statistik-Seite

- 16 Stat-Karten in 4 Kategorien: Fortschritt (grün), Kampf (rot), Herausforderungen (orange), Wirtschaft (gold)
- 9 injizierte Services, Landscape 2-Spalten-Layout

## Daily Challenge

- Tägliches Level: Deterministisch via Seed (Datum-basiert)
- Schwierigkeit ~Level 20-30, 180s Zeitlimit
- Streak-System: Coin-Bonus 200-3000, Reset bei >1 Tag Pause
- Kein Continue, nach LevelComplete direkt GameOver

## Quick-Play Modus

- Einzelnes zufälliges Level via 5-stelligem Seed, Schwierigkeit 1-10
- Kein Progress, keine Achievements - reiner Spaß-Modus
- Timer: 180s (Diff 1) bis 120s (Diff 10, Floor)
- Seed-Sharing möglich

## Daily Reward & Monetarisierung

- **7-Tage-Zyklus**: 500-8000 Coins, Tag 5 Extra-Leben, Tag 7 +15 Gems [BAL-33 seit v2.0.31: Tag 7 von 5000C+10G auf 8000C+15G hochgezogen, 7-Tage-Streak muss sich lohnen]
- **Comeback-Bonus**: >3 Tage inaktiv → 2000 Coins + 5 Gems
- **Spieler-Skins**: Default + 4 Premium (Gold/Neon/Cyber/Retro) + 3 Gem-Skins (Crystal 50G/Shadow 100G/Phoenix 200G)
- **In-App Review**: Nach Level 3-5, 14-Tage Cooldown

## Achievement-System

- 66 Achievements in 5 Kategorien: Progress (17), Mastery (6), Combat (11), Skill (11), Challenge (1) + 20 Cross-Feature
- IAchievementService in GameEngine injiziert → automatische Prüfung bei Level-Complete/Kill/Stars/Combo/Kick/PowerBomb/Curse/Daily/Boss/Spezial-Bombe/Survival/Weekly/Dungeon/BattlePass/Karten/Liga
- **Lazy-T-Injection**: 7 Services nutzen `Lazy<T>` für zirkuläre Dependencies (BattlePass, Card, League, DailyMission → Achievement; Gem, Card → Weekly/DailyMission; Customization → Gem; Dungeon → DungeonUpgrade)
- AchievementData: ~20 Tracking-Felder (TotalEnemyKills, TotalStars, BossTypesDefeated, BestDungeonFloor, etc.)

## Audio-System

- **AndroidSoundService**: SoundPool für SFX (12+6 Sounds) + MediaPlayer für Musik (4+6 Tracks)
- **SoundManager**: Crossfade-Logik, `PlayBombExplosion(BombType)` mit dediziertem SFX + Layering-Fallback
- **Dedizierte Bomben-SFX**: `bomb_ice`, `bomb_fire`, `bomb_lightning`, `bomb_gravity`, `bomb_vortex`, `bomb_blackhole`
- **ISoundService.TryPlaySound()**: Default-Interface-Methode (false), ermöglicht Fallback bei fehlenden Assets
- **Welt-Musik-Keys**: `world_forest` bis `world_inferno` (GetWorldMusicKey()), Fallback auf `gameplay`
- **Dungeon-Musik**: `MUSIC_DUNGEON` Key
- **Sound-Assets**: CC0 Lizenz, ~17.6 MB (12 Basis + 6 Bomben-SFX + 6 Musik-Tracks)
- **Lizenzen**: `Assets/sounds/LICENSES.md` (Kenney.nl, OpenGameArt CC0)
- **Thread-Safety**: `lock(_musicLock)` für MediaPlayer

## Architektur-Details

### Exit-Mechanik
- Exit unter Block versteckt (`Cell.HasHiddenExit`), bei Zerstörung aufgedeckt
- Fallback: Wenn alle Gegner tot aber Exit-Block intakt → automatisch aufgedeckt
- Level-Abschluss: Exit + alle Gegner besiegt (inkl. Pontans)

### Flamepass / Speed / Combo / Kick / LineBomb / PowerBomb / Skull
- **Flamepass**: Schützt NUR vor Explosionen, nicht Gegnern
- **Speed**: SpeedLevel 0-3, BASE_SPEED(80) + Level * 20
- **Combo**: 2s-Fenster, x2→+200 bis x5+→+2000, Chain-Kill 1.5x bei 3+
- **Kick**: Bombe gleitet in Blickrichtung (SLIDE_SPEED 160f), stoppt bei Hindernis
- **LineBomb**: Alle Bomben in Blickrichtung auf leeren Zellen (ab Level 26)
- **PowerBomb**: Range = FireRange + MaxBombs - 1, verbraucht alle Slots (ab Level 36)
- **Skull/Curse**: 4 Typen (Diarrhea/Slow/Constipation/ReverseControls), 10s Dauer (ab Level 20)

### Danger Telegraphing
- Nicht-manuelle Bomben mit Zündschnur < 0.8s → rote pulsierende Overlay-Zellen
- Read-only Spread im Renderer (keine State-Mutation)

### Boss-System Details
- **5 Angriffe**: StoneGolem (Blockregen), IceDragon (Eisatem/Reihe), FireDemon (Lava-Welle), ShadowMaster (Teleport), FinalBoss (rotiert alle 4)
- **Kollision**: `OccupiesCell()` statt GridX/GridY. Shield absorbiert Angriffe
- **Boss-Tod**: 10.000-50.000 Punkte, Gold-Partikel, Shockwave

### Karten-/Deck-System
- **14 Bomben-Karten**: Jeder BombType sammelbar mit Rarität + Level (1-3: Bronze/Silber/Gold)
- **Deck**: 4 Basis-Slots + 1 freischaltbar (20 Gems), ActiveCardSlot per HUD-Tap wechselbar
- **Karten-Upgrade**: Duplikate + Coins (Common: 3+500/5+2000, Rare: 3+1500/5+5000, Epic: 2+3000/4+10000, Legendary: 2+5000/3+20000)
- **Drop-Gewichtung**: 60% Common, 25% Rare, 12% Epic, 3% Legendary
- **Karten für Gems kaufen**: Rare 15G, Epic 30G, Legendary 75G

### Dungeon Run / Roguelike-Modus
- **Ablauf**: Floor 1-4 normal, Floor 5 Mini-Boss, Floor 6-9 härter, Floor 10 End-Boss + Truhe, ab Floor 11 +50% Skalierung
- **Eintritt**: 1x/Tag gratis, 500 Coins, 3 Gems (BAL-32 seit v2.0.31: von 5G auf 3G gesenkt; Spieler erreichen Dungeon ab Level 20 mit typisch 10-20 Gems, 3G erlaubt Paid-Run direkt nach Unlock statt ~40% der Spieler auszusperren), oder Rewarded Ad (1x/Tag)
- **Datum-Tracking**: LastFreeRunDate/LastAdRunDate in DungeonStats (nicht RunState) um App-Restart-Exploit zu verhindern
- **16 Buffs**: 5 Common, 5 Rare, 2 Epic, 4 Legendary (Berserker/TimeFreeze/GoldRush/Phantom)
- **Buff-Auswahl**: Nach Floor 2/4/5/7/9, 3 zufällige gewichtet per Rarität. 1x Reroll gratis, weitere 5 Gems
- **5 Synergies**: Bombardier, Blitzkrieg, Festung, Midas, Elementar
- **5 Raum-Typen**: Normal (W40), Elite (W20), Treasure (W15), Challenge (W15), Rest (W10). GenerateRoomType() zentral, Node-Map nutzt dieselbe Methode
- **8 Floor-Modifikatoren**: Ab Floor 3, 30% Chance. GenerateFloorModifier() zentral (LavaBorders registriert Zellen in specialEffectCells)
- **Node-Map**: 10x3 (Slay the Spire), Pfad-Auswahl
- **8 Permanente Upgrades**: DungeonCoins (50-300 DC)
- **Ascension 0-5**: Eskalierende Schwierigkeit + Belohnungen nach Floor 10 Clear
- **Belohnungen**: Floor 1-4 (200-500C + 10-30 DC), Floor 5 Boss (800C + 50 DC + 5 Gems), Floor 10 Boss (2000+3000C + 100 DC + 15 Gems)
- **Dungeon-Trennung**: Shop-Upgrades gelten NICHT, Base-Stats + Dungeon-Buffs

### Sammlungs-Album
- **5 Kategorien**: Enemies (12), Bosses (5), PowerUps (12), Cards (14), Cosmetics
- **Tracking**: Automatisch via GameEngine-Hooks (Encounter/Defeat/Collect)
- **Meilensteine**: 25%=2.000C, 50%=5.000C+10G, 75%=10.000C+20G, 100%=25.000C+50G
- Verdeckte Einträge ("???") bis zur Entdeckung

### Liga-System (Firebase)
- **5 Ligen**: Bronze→Diamant, 14-Tage-Saisons (Epoche 24.02.2026)
- **Firebase REST API**: Anonymous Auth, Pfad `league/s{saison}/{tier}/{uid}`
- **NPC-Backfill**: Bei <20 echten Spielern, Seeded Random
- **Aufstieg/Abstieg**: Top 30% auf, Bottom 20% ab
- **Punkte**: Level-Complete (10 + Level/10), Boss-Kill (+20/+25), Daily Challenge, Missions
- **Saison-Belohnungen**: Bronze 2.000C/10G bis Diamant 30.000C/75G
- **Firebase-Projekt**: bomberblast-league (europe-west1)

### Cloud Save
- Local-First, 35 Persistenz-Keys, Pull bei App-Start, Push Debounce 5s
- Konflikt-Resolution: TotalStars → Wealth → Cards → Timestamp

### Battle Pass
- 30-Tier Saison (30 Tage), XP-basiert, Free/Premium-Track
- **XP pro Tier**: 320 (T1-5), 400 (T6-10), 480 (T11-15), 560 (T16-20), 640 (T21-25), 720 (T26-30). Gesamt: 15.600 XP
- **13 XP-Quellen**: StoryLevel (100), ThreeStars (50), DailyChallenge (200), DailyMission (80), WeeklyMission (120), DungeonFloor (50), BossKill (200), Survival60s (100), DailyLogin (50), LuckySpin (30), CollectionMilestone (100-500), CardUpgrade (80), LeagueReward (150)
- **XP-Boost**: 2x für 20 Gems, 24h Dauer. Ablaufdatum gecacht als DateTime? (kein DateTime.Parse pro Aufruf)

### Menü-Hintergründe (MenuBackgroundCanvas)
- **7 Themes** (BackgroundTheme Enum): Default, Dungeon, Shop, League, BattlePass, Victory, LuckySpin
- Struct-basierte Partikel (max 60/Theme), gepoolte SKPaint, <2ms Renderzeit bei 30fps
- `Theme` StyledProperty in AXAML: `<controls:MenuBackgroundCanvas Theme="Dungeon" />`
- **15 Views** mit thematischem Hintergrund

### Profil-Seite
- Spielername editierbar (max 16 Zeichen, LeagueService)
- Stats: Sterne, Coins, Gems, Liga-Tier, Achievement-Prozent
- Aktiver Skin + Frame

### Monetarisierungs-Features
- **Starter Pack**: 5000C + 20G + 3 Rare-Karten (ab Level 5, einmaliges Gratis-Geschenk) [BAL-33 seit v2.0.31: von 2500C+10G+2R hochgezogen, Code/Doku-Mismatch behoben]
- **Rotating Deals**: 3 tägliche + 1 wöchentliches Angebot, 20-50% Rabatt, Seeded Random. Kein Coins→Gems-Pathway (Economy-Trennung)
- **Extended Gem-Sinks**: Karten für Gems, Extra Spin (3G), Dungeon-Revive (15G), 5. Deck-Slot (20G), BP Premium (150G)
- **Gem-IAP**: 4 Pakete (100G/0,99EUR, 600G/3,99EUR, 1500G/7,99EUR, 5000G/14,99EUR) [BAL-33 seit v2.0.31: Medium 500G -> 600G, damit Conversion-Paket nicht benachteiligt gegenueber Large/Mega]
- **Dungeon Master Pass**: Permanenter 2x DungeonCoin-Boost (IAP), gespeichert in DungeonUpgradeData
- **Battle Pass Premium**: Kaufbar via IAP (2,99 EUR) ODER 150 Gems. Gem-Alternative in BattlePassViewModel

## AAA Visual Redesign (21 Content-Views)

### Design-Patterns
| Pattern | Beschreibung |
|---------|-------------|
| Farbige Akzent-Borders | 3px oben/links, farblich zum Sektions-Thema |
| Gradient-Hero-Sections | LinearGradientBrush #15XXXXXX → SurfaceColor |
| BoxShadow | `"0 2 8 0 #25000000"` auf Karten-Borders |
| Typographie | Größere Fonts, SemiBold/Bold Hierarchie, farbige Akzente |
| Gradient-Trenner | Height=2, CornerRadius=1, transparente Enden |

### Torn Metal Buttons (SkiaSharp)
- `TornMetalRenderer.cs` (statisch, gepoolte SKPaint/SKPath) + `GameButtonCanvas.cs` (3 StyledProperties: ButtonColor, DamageLevel, ButtonSeed)
- DamageLevel Convention: CTA=0.5, Success=0.3, Danger=0.7, Gold=0.6, Secondary=0.2-0.3
- ~59 Buttons in 18 Views, deterministisch per Seed (10-181)

**ButtonSeed Ranges**:
| View | Seeds | Buttons |
|------|-------|---------|
| MainMenu | 10-32 | 12 |
| GameOver | 40-45 | 6 |
| Victory | 50-51 | 2 |
| QuickPlay | 60-62 | 3 |
| Dungeon | 70-74 | 5 |
| LuckySpin | 80-82 | 2 |
| DailyChallenge | 90 | 1 |
| BattlePass | 100-104 | 5 |
| GemShop | 110-113 | 4 |
| LevelSelect | 120 | 1 |
| Shop | 130-132 | 3 |
| Deck | 140-142 | 2-3 |
| Collection | 150 | 1 |
| WeeklyChallenge | 155-156 | 2 |
| Settings | 160-168 | 9 |
| Help | 170 | 1 |
| Profile | 175 | 1 |
| League | 180-181 | 2 |

---

## Audit-Ergebnisse & offene Optimierungen (20.04.2026)

Gesamt-Review (7 Agents: game-audit / code-review / performance / skiasharp / security / localize / health) am 20.04.2026 durchgefuehrt. Alle Critical + High + Medium + Low-Fixes in v2.0.31 umgesetzt. Ausnahme: Lazy-VM-Umbau (siehe unten).

**Gefixt in v2.0.31:**
- NeonJoystick BombPressed-Race bei Taps <16ms (OnTouchEnd setzt _bombPressed nach Konsum sofort auf false)
- NeonJoystick Multi-Touch PointerId-Konflikt (separate _bombButtonPointerId + _detonatorPointerId)
- NeonJoystick SoftGlow-Skip fuer Bomb/Detonator (alle 2 Frames, bei Press immer) — 2-4ms GPU auf Low-End
- NeonJoystick Trail-Init-Bug (TrailPoints mit Age=999f initialisiert, keine Geister-Dots beim ersten Frame)
- NeonJoystick Arrow-Path: einmal gebaut + zweimal gezeichnet (Glow+Fill) statt doppelt bauen
- Version-Drift Splash v2.0.30 -> v2.0.31 (SkiaLoadingSplash.AppVersion + Shared.csproj `<Version>`)
- InputManager Dispose-Luecke in App.DisposeServices geschlossen (20 SKPaint + 5 SKPath freigegeben)
- Starter-Pack Code/Doku-Mismatch: 2500C+10G+2R -> 5000C+20G+3R (gratis, Level 5)
- StartSpeed MaxLevel 1 -> 3 mit Preiskurve [1200, 2500, 7000] (Shop-Progression-Feel)
- Gem-Pack Medium 500G -> 600G (3,99EUR, G/EUR-Ratio von 125 auf 150 angehoben)
- LuckySpin Gem-Segment Weight 8 -> 12 (Erwartungswert 0.66 -> 0.79 Gems/Spin, +20%)
- DailyReward Tag-7-Jackpot: 5000C+10G -> 8000C+15G
- Profanity-Blocklist erweitert (~25 -> ~50 Tokens, mehr Sprachen, Leetspeak, Hass-Ideologie)
- LevelSelectVisualization SKPath-Allokationen eliminiert (static `_sharedPath` + `_starPath` via Rewind()) — ~300 Allocs/Scroll gespart
- LuckySpinView Dispose-Chain ergaenzt (6 SKPaint + SKFont + 2 SKPath) + SKPath-Pooling (Segment/Pointer, spart 270 Allocs/s)
- ShopViewModel: 11 `= new ObservableCollection<T>(items)` durch `ReloadCollection()` ersetzt (Clear+Add, kein Binding-Rebind). Spart 50-150ms pro Tab-Wechsel.

**Offene Optimierungen (bewusste Deferrals):**

Alle vier bewussten Deferrals aus v2.0.31 wurden in v2.0.34 umgesetzt (siehe "Updates v2.0.34" unten). Aktuell keine offenen PERF/SEC-Deferrals.

## Updates v2.0.34 (24.04.2026)

### Performance & Startup

- **Lazy-VM-Umbau (PERF-High)** — 14 spät-unlocked Child-VMs (ShopVM, AchievementsVM, DailyChallengeVM, LuckySpinVM, WeeklyChallengeVM, StatisticsVM, QuickPlayVM, DeckVM, DungeonVM, BattlePassVM, CollectionVM, LeagueVM, ProfileVM, GemShopVM) werden jetzt per `Lazy<T>` injiziert und erst beim ersten Navigations-Ziel instanziiert. Eager bleiben 9 VMs für frühe Interaktion (MainMenu, Game, LevelSelect, Settings, Help, HighScores, GameOver, Pause, Victory). Verdrahtung erfolgt in `EnsureXxxVm()`-Methoden mit idempotentem Guard. Geschätzte Startup-Ersparnis 200-500ms auf Mid-Tier-Android (ShopVM allein ist ~900 Zeilen und zieht mehrere Services nach).
- **Shader-Precompile** — `ShaderEffects.WaterRippleSkSL` wird jetzt statisch im Splash via `ShaderEffects.Preload()` kompiliert und von allen Instanzen wiederverwendet. Spart 50-200ms Kaltstart-Jitter beim ersten Ocean-Level-Frame (Welt 6). Dispose nicht mehr per Instanz (Referenz statt Besitz), sondern via `ShaderEffects.DisposeSharedResources()` beim App-Shutdown.
- **Asset-Preload-Strategie** — Neue `GameAssetPaths`-Helper-Klasse. LoadingPipeline preloaded jetzt 12 PowerUp-Icons + 12 Enemy-Icons + Welt-1-Hintergrund zusätzlich zu Splash/Menu-BG/Bosse. GameViewModel.SetParameters() triggert bei Level-Eingang fire-and-forget den welt-spezifischen Preload (Welt 2-10), damit der erste Frame nach Countdown volle Visuals hat. `_lastPreloadedWorldIndex` verhindert Doppeltriggering.

### Progression & Retention

- **Feature-Unlock bei L17/L18** — Schließt Dead-Zone L15→L20. L17: Expliziter `Missions`-Button im MainMenu (navigiert direkt zum Missions-Tab der Challenges-View, ohne Tab-Switch). L18: Neuer `Customize`-Button in der unteren Utility-Leiste (Shop/Profile/Customize/Settings, navigiert zu Profile für Skin/Trail/Victory-Wahl). Beide Unlocks haben Feature-Celebrations (FloatingText + Confetti) und NEU!-Badges via `IPreferencesService`.
- **Daily/Weekly-Mission-Pool** — 3 neue Skill-basierte Mission-Typen gegen Wiederholungs-Gefühl: `CompleteThreeStar` (3-Sterne-Abschluss), `NoDamageLevel` (Perfect-Run), `CompleteMutatorLevel` (Welt 6+). Höhere Coin-Rewards als Standard (Daily 300-350, Weekly 650-750). Tracking-Hooks in `GameTrackingService.OnStoryLevelCompleted()` basierend auf stars/noDamage/level-Parametern. 6 RESX-Keys (Name+Desc) × 6 Sprachen = 72 neue Lokalisierungs-Einträge.
- **Rotating Deals Pool-Erweiterung** — Daily: 4 → 7 Typen (neu: `EpicCardPack`, `GemCoinCombo`, `PowerUpLuckDeal`). Weekly: 4 → 6 Typen (neu: `LegendaryCardDrop`, `MasterBundle`). Reicht für 6-Wochen-Rotation ohne Wiederholung. 5 neue Title-Keys × 6 Sprachen.

### Monetarisierung

- **Rewarded-Placements (3 neue)** — `double_daily_reward` komplett implementiert: DailyReward-Popup hat jetzt zweiten Button "Watch Ad: 2x" (grün), der bei Premium-Usern automatisch 2x claimt ohne Ad, bei Free-Usern via Rewarded Ad + 60s Cooldown. Ad-Fail gibt normalen 1x Claim (kein Bestrafen). `bonus_card_drop` + `battle_pass_xp_boost` als Infrastruktur (AdConfig Switch-Cases + platzhalter Ad-Unit-IDs), VM-Hooks folgen nach Design-Entscheidung für Trigger-Punkte. TODO: Eigene Ad-Unit-IDs im AdMob-Dashboard erstellen (aktuell teilen sich die neuen IDs mit Gem-Bonus/CoinMultiplier/ScoreDouble).
- **Deck-Balancing-Telemetrie** — Neuer `IDeckTelemetryService` trackt `Used`/`Plays`/`Wins` pro BombType (13 Spezial-Typen). Hooks: GameEngine.PlaceBomb → `RecordBombPlaced`; GameEngine.CompleteLevel → `RecordLevelCompletedWithBombs` + `RecordLevelStartedWithBombs`; GameEngine.GameOver → `RecordLevelStartedWithBombs` (nur). Persistenz via Preferences-JSON. Optionale `FlushToRemoteAsync()` schreibt an Firebase `analytics/deck/{uid}`. Balance-Targets: keine Karte <5% Usage, keine >40%.

### Security & UGC-Moderation

- **Leaderboard Server-Timestamp** — `FirebaseLeagueEntry.UpdatedUtc` (client-gesetzt, spoofbar) entfernt. `UpdatedMs` ist jetzt die einzige Zeitstempel-Quelle — Firebase `ServerValue.TIMESTAMP` server-authoritativ, nicht client-manipulierbar. Security-Rules sollten Rate-Limit via UpdatedMs durchsetzen (min. 60s zwischen Writes pro UID).
- **Report-Button im Leaderboard** — Play-Store-Policy-Vorsorge für UGC (Player-Names). `ILeagueService.ReportPlayerAsync(reportedUid, reason)` schreibt an Firebase-Node `reports/{reportedUid}/{reporterUid}` mit Server-Timestamp + Reason. Reason wird auf whitelist begrenzt ("offensive_name"/"cheating"/"other") gegen Code-Injection. Self-Report blockiert. `LeagueDisplayEntry.CanReport` (IsRealPlayer && !IsPlayer && Uid!=leer) steuert Button-Sichtbarkeit. UI: kleiner AlertCircle-Button (#FF4D4D) rechts pro Leaderboard-Row. Empfohlene Firebase-Security-Rules: Rate-Limit 1 Report pro Reporter/Reported-Paar pro 24h, Moderations-Queue im Admin-Dashboard.

## Pass-3-Polish v2.0.35 (24.04.2026)

Dritter und finaler Review-Pass — 5 Findings, 0 Critical, commit-ready.

- **TutorialOverlay.WrapText Kerning-Fix** (HIGH): Vorher akkumulierte die Methode `MeasureText(" " + word)` inkrementell, was durch Kerning über 8-10 Worte um 5-10px driftete → falsche Bubble-Höhe. Jetzt wird nach jedem Wort-Append `MeasureText(currentLine.ToString())` als authoritative Breite gemessen. Rollback über StringBuilder-Length-Snapshot bei Overflow. Minimal-Overhead (1 Messung/Wort).
- **Hitbox-Semantik-Doku** (MEDIUM): XML-Kommentare in `Entity.HitboxScale` / `Entity.GetHitbox` erklären warum Width/Height statt BoundingBox als Shrink-Basis dient (konsistente 0.6× Kollisions-Radius bei allen Entities). `Player.BoundingBox` + `Enemy.BoundingBox`-Overrides bekommen Hinweis dass sie nicht mehr für CollidesWith relevant sind (nur noch Sprite-Referenz + Boss-Boss-Kollision in GameEngine.Level).
- **DeckTelemetryService `_isDisposed`-Guard** (LOW): `volatile bool _isDisposed` wird in `Dispose()` zuerst gesetzt. Alle Record*-Methoden haben einen frühen `if (_isDisposed) return;`-Guard. Verhindert dass nach `Dispose.SaveImmediate` noch ein weiterer Task spawnt (falls ein anderer Thread parallel RecordBombPlaced aufruft).

## Gameplay-Fixes v2.0.35 (Spieler-Feedback nach Testing)

### Tutorial-Text-Rahmen dynamisch
`TutorialOverlay.Render` hatte feste `bubbleWidth = 55% × 360` + `bubbleHeight = 54`. Lange DE/FR-Übersetzungen wurden abgeschnitten. Fix: `WrapText()` splittet Text in Zeilen basierend auf gemessener Font-Breite. Bubble-Höhe skaliert mit Zeilenzahl. Max-Width auf 80%/480 erhöht.

### Explosion tötet nicht mehr Gegner HINTER zerstörbarem Block
`Explosion.CalculateSpread` markiert die Block-End-Zelle jetzt als `IsBlockHit = true`. Kollisionscheck in `CheckCollisions` überspringt `IsBlockHit`-Zellen für Player- UND Enemy-Schaden. Verhindert Bug: Block zerfällt während Explosion noch aktiv ist (0.3s Destroy vs 0.9s Explosion), Gegner läuft in die freiwerdende Zelle → vorher getötet durch scheinbar "durchgeschossene" Explosion.

### Spieler-Enemy-Kollision mit Toleranz (statt Grid-Feld-Trigger)
`Entity.CollidesWith` nutzt jetzt verschrumpfte Hitbox via neue `HitboxScale`-Property (default 0.6 = 60% der Cell-Größe). Vorher: BoundingBox == CELL_SIZE → Spieler starb bereits bei großem Pixel-Abstand sobald er dasselbe Grid-Feld betrat. Jetzt: Echte visuelle Berührung erforderlich. `BossEnemy.HitboxScale = 1.0` override damit Boss-Hitbox nicht doppelt geschrumpft wird (Boss hat bereits custom 0.4x BoundingBox).

## Post-Review-Fixes v2.0.35 (24.04.2026)

Zwei Review-Passes ergaben 16 + 9 Findings (alle systematisch behoben):

### Pass 2 (Verifikation nach Pass-1-Fixes)

**Thread-Safety:**
- `DeckTelemetryService.ScheduleSave` fängt jetzt auch `ObjectDisposedException` (falls CTS parallel disposed wird). CTS wird NICHT mehr explizit disposed (GC-freigabe statt Race-Potential).
- `MasterModeService`: Neues `_sync`-Lock-Objekt. Alle `_levelStars`-Zugriffe (Get/Record/Totals/Reset/Load/Save/OnCloudStateLoaded) innerhalb des Locks. MasterLevelCleared-Event außerhalb des Locks gefeuert (Reentrance-Schutz).
- `CloudSaveService.CloudStateLoaded` wird via `Dispatcher.UIThread.Post` marshaled → Handler garantiert auf UI-Thread. Interface-Kontrakt entsprechend dokumentiert.
- `ShaderEffects._sharedWaterRippleTried` als `volatile` (ARM-Safety). Kommentar erklärt warum der Fast-Path-Read ohne Lock trotzdem korrekt ist.

**Lifecycle:**
- `IMasterModeService` + `IDeckTelemetryService` erben jetzt `IDisposable` (Interface-Contract).
- `App.DisposeServices` disposed `IDeckTelemetryService`, `IMasterModeService`, `ICloudSaveService`. Flusht pending DeckTelemetry-Save beim Shutdown (kein Datenverlust).

**Code-Hygiene:**
- `NeonJoystick._frameCounter`-Feld + Increment entfernt (war toter Code nach Flicker-Fix).
- `GameEngine.StartStoryModeAsync` loggt via `Debug.WriteLine` wenn Master-Mode-Downgrade passiert (Deep-Link-Debugging).

### Pass 1 (Ursprünglicher Review):

**Critical (Datenverlust-Risiko):**
- `CloudSaveService.SyncKeys` um `master_mode_status_v1`, `master_mode_active`, `deck_telemetry_v1` erweitert → Master-Mode-Progress + Champion-Skin + Deck-Telemetrie bleiben bei Geräte-Wechsel erhalten
- Neues `ICloudSaveService.CloudStateLoaded`-Event → `MasterModeService` + `DeckTelemetryService` abonnieren und invalidieren ihren internen Cache nach Cloud-Pull (sonst stale UI)

**High (Feature-Bugs):**
- `RotatingDealsService.ClaimDeal` Card-Case nutzt jetzt Pool-Rarity basierend auf `TitleKey` (`DealLegendaryCardDrop` → Legendary, `DealEpicCardPack` → Epic, sonst Rare) und respektiert `RewardAmount` (Bundle-Deals droppen mehrere Karten)
- `ShaderEffects.Preload`/`DisposeSharedResources` haben jetzt echten `lock (_sharedWaterRippleLock)` mit Double-Check-Pattern (vorher kein Lock)
- `Enemy._staggerRandom` ersetzt durch `Random.Shared` (explizit thread-safe ab .NET 6+)
- `MasterModeService.IsActive`-Getter prüft jetzt `IsUnlocked` (Corruption-Schutz)
- `GameEngine.StartStoryModeAsync` setzt `_isMasterMode = masterMode && _masterModeService.IsUnlocked` (Defense-in-Depth gegen Deep-Link-Manipulation)

**Medium (Polish):**
- `LevelSelectViewModel.ToggleMasterMode` setzt Service-State ZUERST, dann VM-Property (Source-of-Truth-Prinzip)
- `MainViewModel.NavigationRequestToRoute` loggt Fallback statt silent-return für unbekannte inner-Requests
- `FogOfWarSystem.Render` resettet `fillPaint.Color/StrokeWidth/Style` am Ende (Paint-Contract)
- `DeckTelemetryService` umgestellt auf 1s Save-Debounce (vorher blockierte jeder Bomb-Placement 1-5ms den UI-Thread)
- `MasterModeService.IsActive`-Setter loggt Warnung bei silent-fail

**Low (Code-Cleanup):**
- `GameTrackingService.OnMasterLevelCompleted` Gem-Reward-Check vereinfacht (redundanter `GetMasterStars==3`-Check entfernt)
- `GameTrackingService.OnStoryLevelCompleted` bekommt `isMutatorLevel`-Parameter → `CompleteMutatorLevel`-Mission trackt jetzt echte Mutator-Levels statt nur `level>50`
- Totes `_levelStartReportedToTelemetry`-Feld in `GameEngine` entfernt
- `AdConfig.cs` kommentiert klar welche Placements WIP sind (bonus_card_drop, battle_pass_xp_boost haben keine Call-Sites)

**Bonus-Fix: Bomb-Button-Flackern**
- `NeonJoystick.RenderBombButton`/`RenderDetonatorButton`: Glow-Layer wurde alle 2 Frames getoggelt → 15-Hz-Flicker. Jetzt durchgehend mit halbem Alpha (gleiche GPU-Kosten, keine sichtbare Toggle-Frequenz)
- Cyan-Funke-Pulse: 14 Hz → 6 Hz, ±30% → ±15% Amplitude
- Idle-Breath: 2.5 Hz → 1.5 Hz, ±10% → ±5% Amplitude
- Resultat: Button "glüht ruhig" statt "flackert nervös"

## Updates v2.0.35 (24.04.2026) — Performance + Master Mode + Fog of War

### Master-Champion-Skin Crown-Rendering

`GameRenderer.Characters.RenderChampionCrown()` zeichnet bei aktivem `master_champion`-Skin eine prozedurale Gold-Krone auf den Helm: 3 Zacken (Mitte höchster), pulsierender Gold-Glow (2.5 Hz), Rubinroter Edelstein zentriert, Perlen-Highlights auf den Zacken-Spitzen. SKPath-basiert, keine Assets nötig. Aufruf zwischen Helm-Glanzlicht und Gesicht-Block damit Krone korrekt überlagert.

### Fog of War System (L50+ / Master)

`FogOfWarSystem` (Graphics/FogOfWarSystem.cs) implementiert klassisches 3-Zustand-Memory-System:

| Zustand | Alpha-Overlay | Bedeutung |
|---------|---------------|-----------|
| Unknown | 235 | Zelle nie gesehen |
| Explored | 140 | Zelle gesehen, aktuell nicht im Sichtfeld |
| Visible | 0 | Zelle aktuell im Sichtfeld |

**Aktivierungs-Matrix:**

| Modus | Level | FoW? | Radius |
|-------|-------|------|--------|
| Story | 1-49 | nein | — |
| Story | 50-59 | ja | 5 Zellen |
| Story | 60-99 | ja | 4 Zellen |
| Story | 100 (Welt 10) | bereits `FogOverlay` (einfacher Sichtkreis, kein Memory) | — |
| Master Mode | 1-100 | ja | 4 Zellen |
| Dungeon / Survival / Quick-Play / Daily | alle | nein | — |

**Algorithmus:** Manhattan-Radius mit leichter Ecken-Abrundung (`dx*dx + dy*dy <= r*r + r`). Kein echtes Line-of-Sight (Blöcke blockieren nicht — passt zur Bomberman-Vogelperspektive). Update 1× pro Frame in `GameEngine.Update`.

**Rendering:** Per-Cell Rechtecke mit Visibility-basiertem Alpha + radialer Soft-Edge-Ring um den Spieler (zwei konzentrische Kreise mit Stroke, progressiver Alpha). Nutzt gepoolte `_fillPaint` von GameRenderer. Explored-Memory bleibt über Death/Respawn erhalten (Reset nur bei Level-Wechsel via `Enable()`).

**Architektur:**
- `FogOfWarSystem.Enable(width, height, revealRadius)` in `LoadLevelAsync` (oder `Disable()`)
- `Update(playerGridX, playerGridY)` in `GameEngine.Update` pro Frame (kostet <1ms für 15×10 Grid)
- `Render(canvas, playerX, playerY, fillPaint)` in `GameRenderer.Render` VOR canvas.Restore
- Dispose in GameRenderer-Dispose-Chain

### Performance / Stutter-Reduktion

### Performance / Stutter-Reduktion

- **A\*-Pathfinding Spawn-Jitter + Frame-Budget** — Enemy-Ctor setzt `AIDecisionTimer` jetzt auf `Random * AIDecisionInterval` (0-1.5s je nach Intelligence). Verteilt die ersten Pfadsuchen bei Mass-Spawn (Level-Start, Survival-Wellen, Mini-Splitter) statt alle im selben Frame zu rechnen. Zusätzlich EnemyAI.`AStarBudgetPerFrame = 5` als Absicherung bei Extremfällen: Wenn Budget erschöpft, fällt der Gegner für einen Frame auf Random-Movement zurück, kommt im nächsten Frame wieder dran (natürliches Auffalten durch Decision-Jitter 0.8-1.2×). Threading wurde bewusst NICHT gewählt, weil der A\*-Code intern-gepoolt ist (SKPaint, PriorityQueue, HashSets) und der Grid-State sich jeden Frame durch Bombs/brechende Blöcke ändert — Thread-Safety hätte Grid-Snapshots erfordert, was 400+ LOC neue Risiken bedeutet hätte.

- **Adaptive Frame-Skipping** — GameRenderer führt einen 5-Frame-Ring-Buffer der Frame-Zeiten. Wenn Durchschnitt > 40ms (unter 25 FPS, Stutter-Indikator), werden atmosphärische Systeme automatisch für mindestens 500ms ausgesetzt: WeatherSystem, AmbientParticleSystem, TrailSystem, Background-Elements, DynamicLighting, Post-Processing (Color Grading, Water Ripples, Damage Flash). Gameplay (Input, Collision, AI, Bomb-Explosion-Feedback) läuft voll weiter. Hysterese-Exit bei Avg < 28ms gegen Flackern. Neue Property `SkipAtmosphere` kombiniert manuellen `ReducedEffects`-Toggle mit der adaptiven Entscheidung.

### Master Mode (Endgame / New Game+)

**Unlock:** Nach L100-Abschluss im Normal-Modus wird Master Mode freigeschaltet (Feature-Celebration bei Level 100). Toggle im LevelSelect-Header (Krone-Icon wird aktiv, Header-Text ändert sich zu "Master Mode").

**Gameplay-Änderungen im Master-Modus:**
- Gegner-Geschwindigkeit × 1.5 (gleiche Formel wie DoubleSpeed-Mutator, nicht kombinierbar → max 1.5×)
- Gegner-Typ-Upgrade: Ballom→Minvo, Onil→Pass, Doll→Pontan, Minvo→Pass, Kondoria→Pontan, Ovapi→Pontan. Pass/Pontan sind schon Maximum. Spezialtypen (Tanker/Ghost/Splitter/Mimic) bleiben unverändert.
- Nutzt existing 100 Story-Level — kein neuer Content-Scope, reines Skalierungs-Feature.

**Persistenz (separater Pfad, kein Normal-Score-Update):**
- `IMasterModeService.RecordLevelCompleted(level, stars)` speichert pro Level die höchste Stern-Anzahl in Preferences-JSON
- Normal-Mode Stars bleiben unberührt — Master-Mode ist parallel
- Star-Berechnung lokal via `IProgressService.GetBaseScoreForLevel` (gleiche Thresholds 1×/2×/3× wie Normal)

**Rewards:**
- +1 Gem pro erstmaligem 3-Sterne-Master-Clear (100 Levels × 1G = 100G Endgame-Gems)
- Battle-Pass-XP: +150 pro Clear, +100 für 3 Sterne, +75 für No-Damage (höher als Normal wegen Schwierigkeit)
- Liga-Punkte: +15 + level/10 (+25 bei Boss-Levels alle 10)
- "master_champion"-Skin Unlock nach 100 Master-3-Sterne-Clears (UnlockOnly-Flag, nicht kaufbar)
- 3 neue Achievements (Mastery-Category): master_first (1 Clear, 500 Coins), master_25 (25 Clears, 2000 Coins), master_100 (100 3-Sterne, 10000 Coins)

**UI:**
- LevelSelect-Header: Toggle-Button mit Krone-Icon + Master-Status-Text "Master: X/100 (Y★)"
- Level-Thumbnails: Kleine gelbe Krone oben rechts wenn `HasMasterClear` (unabhängig vom aktiven Toggle)
- Achievement-View: Master-Achievements in Mastery-Category neben den stars_*-Achievements
- RESX-Keys: MasterModeTitle, MasterStatusFormat, MasterModeToggleTooltip, AchMasterFirst*, AchMaster25*, AchMaster100*, SkinMasterChampion × 6 Sprachen

**Architektur:**
- Neuer `IMasterModeService` (Singleton) — Status pro Level, Gesamt-Zähler, IsActive-Toggle
- `GoGame`-Navigation-Record bekommt `MasterMode`-Bool-Parameter (default false)
- `GameEngine._isMasterMode`-Flag wird in `StartStoryModeAsync(level, masterMode)` gesetzt, in allen anderen Start*-Methoden auf false zurückgesetzt
- `GameEngine.ApplyMasterModeEnemyUpgrade()` wird EINMAL nach `LoadLevelAsync()` aufgerufen und ersetzt upgradebare Gegner-Typen im `_enemies`-List
- `GameTrackingService.OnMasterLevelCompleted` orchestriert Reward/Achievement/Skin-Unlock — GameEngine.CompleteLevel ruft nur diesen einen Entry-Point auf, Rest bleibt in GameTrackingService

## Performance-Pass v2.0.36 (25.04.2026)

Tiefenanalyse durch parallele `skiasharp` + `performance` Agents nach v2.0.35-Audit. 10 Findings (alle Low-Risk) systematisch umgesetzt — auf v2.0.31-Audit aufbauend, fokussiert auf nach v2.0.32 hinzugekommene Features (Master Mode, Fog of War, Champion-Crown). Geschätzter Gesamt-Gewinn: 2-4ms/Frame = 5-10 FPS auf Mid-Tier-Android.

### Phase 1: Top-Wins
- **`SKPath.Reset()` → `Rewind()` (42 Stellen)** in `GameRenderer.{Items,Characters,HUD,Bosses,Grid}.cs` + `BomberBlastSplashRenderer.cs`. `Reset()` gibt nativen Path-Buffer frei und re-alloziert beim nächsten `MoveTo`; `Rewind()` behält die Kapazität → keine native Reallokation. Boss-Frames mit Enrage rufen das 8-12× pro Frame auf. Spart 0.3-0.8ms/Frame, bis zu 1.5ms in Boss-Frames. `_bgPath.Reset()` in Grid.cs:87 (RenderFogOverlay) ebenfalls auf Rewind() — danach explizit `FillType = SKPathFillType.Winding` zurücksetzen, weil Atmosphere-Renderer `_bgPath` mit Default-FillType nutzen und Rewind FillType beibehält.
- **FogOfWarSystem.Update Position-Cache** — `_lastPlayerGridX/Y` als int.MinValue-Sentinel. Wenn Grid-Position seit letztem Frame unverändert, kompletter Update-Pass übersprungen. Spielerwechselt Grid-Cells nur alle paar Frames (kontinuierliche Bewegung, diskretes Grid). Cache wird in `Enable()` invalidiert für sauberen ersten Update nach Level-Start. Spart 0.2-0.4ms/Frame in L50+ und Master-Modus.
- **FogOfWarSystem.Render Run-Length-Encoding** — Statt 150 einzelne `DrawRect`-Calls pro Frame (15×10 Grid) werden zusammenhängende Zellen mit gleichem Alpha-Wert pro Zeile zu einem `DrawRect` gemerged. Sentinel-Iteration `x == _width` schließt den letzten Run jeder Zeile ab. Bei typischer FoW-Verteilung (Spieler in der Mitte) spart das 60-80% der DrawCalls (~150 → ~30). Spart 0.5-1.0ms/Frame in L50+ und Master.

### Phase 2: Quick-Wins
- **EnemyPositionIndex Lazy-Rebuild** in `GameEngine.Collision.cs:243` — Index wird nicht mehr unbedingt rebuilt vor jedem Frame, sondern lazy nur beim ersten aktiven Explosion-Hit (`indexBuilt`-Flag). In ~95% der Frames (keine aktive Explosion) gespart: ~16-20 Dict-Operations pro Frame.
- **BlackHole/Poison Skip** in `GameEngine.Explosion.cs` — `UpdateBlackHolePull` + `UpdatePoisonDamage` haben Early-Return wenn `_specialEffectCells.Count == 0`. Vorher: Iteration über alle Gegner mit `TryGetCell`-Lookup pro Frame, auch ohne aktive Special-Effects. Spart 720 unnötige Lookups/s in Standard-Frames.
- **DungeonMapRenderer SKPathEffect-Cache** — `_dashIntervals` (static readonly float[]) + `_dashPathEffect` (static readonly SKPathEffect) statt pro Edge-Iteration `SKPathEffect.CreateDash(new[] { 6f, 4f }, 0)` allozieren. Vorher: bis zu 30 native Allokationen pro Frame in der DungeonMap-View. Cache wird nicht disposed (lebt für App-Lifetime, akzeptables Pattern für statische Renderer-Klassen).

### Phase 3: Polish
- **FinalBoss SKColor[]-Arrays static** in `GameRenderer.Bosses.cs` — `FinalBossElementColors`, `FinalBossGemColors`, `FinalBossAccents` als static readonly statt pro Frame neu allozieren. Werte sind konstant. Spart 3 Heap-Array-Allokationen pro FinalBoss-Frame.
- **Block-Destroy float[]-Arrays inline** in `GameRenderer.Grid.cs` — `BlockFragSpreadMulX/Y` und `BlockFragRotMul` als static readonly Multiplikatoren. Zur Laufzeit mit `spread`/`p2` multiplizieren statt drei `float[4]`-Arrays pro Frame allokieren. Spart Allocations bei mehreren parallelen Block-Zerstörungs-Animationen.
- **HUD-Glow im SkipAtmosphere-Modus deaktivieren** — Properties `HudTextGlowEffective`, `HudSmallGlowEffective`, `HudComboBlurEffective` in `GameRenderer.HUD.cs` geben `null` wenn `SkipAtmosphere == true` (Adaptive Frame-Skipping aktiv). Hilft bei Stutter-Recovery damit GPU-Blur nicht weiter Frame-Time frisst. Reine Property-Indirektion ohne Field-Änderungen.
- **Confetti Two-Pass-Rendering** in `GameEngine.Render.cs` — Bei Victory-Confetti werden alle Partikel mit `sparkle <= 0.7f` in Pass 0 (ohne Glow) und alle mit `sparkle > 0.7f` in Pass 1 (mit Glow) gerendert. Spart Paint-State-Wechsel von ~5-10 pro Frame auf 2 (1 pro Pass). Math wird 2× ausgeführt, ist aber günstig (Modulo + Sinus). LevelComplete-Confetti hat keinen Glow-Toggle und bleibt single-pass.

### Geprüft ohne Befund (false positives)
- LINQ in Hot-Path: kein Treffer
- SKPaint/SKPath/SKShader-Allokationen pro Frame: alle gepoolt
- `new List<>/Dictionary<>/HashSet<>` im Hot-Path: nur bei Lightning-Bombe (event-getrieben, akzeptabel)
- String-Interpolation `$"..."` in Render-Loop: HUD-Cache greift
- Closures/Lambdas pro Frame: keine
- Boxing in Hot-Path: keine

### Bestand-Bugfix: Enemy-Pin-Down bei Bombe vor Korridor
Während Spieler-Tests aufgefallen, aber unabhängig von der Performance-Pass — Bug existierte seit Einführung des `cell.Bomb != null`-Blocks in `EnemyAI.CanMoveInDirection`. Symptom: Wenn Spieler eine Bombe direkt vor einen Gegner in einem Korridor (Wände seitlich) platziert, blieb der Gegner permanent stehen.

**Root Cause:** `EnemyAI.cs:507-508` filtert Bomb-Cells in `CanMoveInDirection` aus. In Korridor-Situationen (Bomb-Cell in Bewegungsrichtung + Wände seitlich + Bomb-Range an verbleibender Cardinal-Direction) findet der Algorithmus keine valide Direction. `TryEvade` returnt false, `GetRandomValidDirection` returnt `Direction.None`, `Enemy.Move()` mit None bewegt nicht. `StuckTimer`-Mechanik ruft wieder `GetRandomValidDirection` auf → gleiches Resultat → permanent eingefroren.

**Fix:** `CanMoveInDirection` bekommt optionalen `bool allowBombCell = false`-Parameter. `GetRandomValidDirection` macht Two-Pass: Erst normaler Pass mit Default-Verhalten (Bomb-Cells blockiert), bei `Count == 0` Last-Resort-Pass mit `allowBombCell: true`. Wände/Blöcke/PlatformGaps bleiben in beiden Pässen blockierend. Side-Effect: Gegner kann auf Bomb-Cells laufen und dort sterben — das ist Bomberman-Mechanik (Spieler treibt Gegner gezielt in Bomben). `TryEvade` ruft `CanMoveInDirection` mit Default-Parameter, bleibt unverändert (wenn TryEvade fail, fängt Intelligence-Switch-Fallback via `GetRandomValidDirection` den Pin-Down).

## Endgame-Design-Empfehlung (Feature-Proposal für v2.0.36+: Ascension Mode)

Master Mode ist Phase 1 des Endgame-Hybrids. Phase 2 "Ascension Mode" kann auf Master aufbauen:

Story endet mit L100 — Long-Term-Retention-Risk. Zwei Optionen skizziert, Game-Design-Entscheidung ausstehend:

### Option A: Endless Ascent (Ascension-Mode)

Neuer Mode="endless" im GameEngine, Unlock nach L100-Abschluss. Spielt zufällig generierte Level (QuickPlay-Engine) mit progressiv steigendem Difficulty-Skalar basierend auf `AscensionLevel` (0-50). Jeder Level-Clear: AscensionLevel+1, alle 10 AscensionLevel: +2 Gems Reward. GameOver: Ascension-Highscore persistiert, zurück zum MainMenu.

**Infrastruktur nötig:** `IEndlessService` (AscensionLevel, BestAscension, Gems-Total), `StartEndlessAsync(ascension)` in GameEngine, EnemyCount/Speed/HP-Skalierung in LevelGenerator, `EndlessViewModel` + `EndlessView` (Leaderboard + Start-Button), MainMenu-Button ab L100, NavigationRequest `GoEndless`, RESX (~15 Keys × 6 Sprachen).

**Vorteil:** Klassisches Arcade-"Endless"-Feel, wenig Story-Dependency, Leaderboard-tauglich.

### Option B: Master Mode (New Game+)

Ab L100 erscheint im LevelSelect ein "Master Mode"-Toggle. Spieler kann L1-100 mit +50% Enemy-Speed + höherwertigen Gegner-Auswahl (Ballom→Minvo, Doll→Pass etc.) erneut spielen. Jedes Master-3-Sterne-Level: +1 Gem + Master-Marker im Thumbnail. Freischaltet einen "Master-Champion"-Skin nach 100 Master-3-Sterne-Clears.

**Infrastruktur nötig:** `IMasterModeService` (per-Level-Status), `masterMode`-Bool in GameEngine-Parameter + LevelGenerator, EnemyAI-Skalierung, UI-Toggle im LevelSelect, Thumbnail-Badge-Renderer-Anpassung, Achievement-Integration.

**Vorteil:** Reused existing 100 Level-Content, direkter Transfer der Deck/Shop-Progression, kein neuer Game-Mode nötig, native "Prestige"-Loop.

**Empfehlung:** Option B (Master Mode) für minimal-invasiven Scope und maximale Wiederverwendung. Option A als Follow-up für Leaderboard-fokussierte Spieler.

## IAP-Mix-Erweiterung (Feature-Proposal für v2.0.35+)

Aktueller IAP-Mix: `remove_ads` (1.99, non-consumable), `gem_pack_*` (4 Tiers, consumable), `battle_pass_premium` (saisonal, consumable), `dungeon_master_pass` (consumable, permanenter 2x-Buff). Fehlt: Progression-Bundles + Comeback-Angebote.

### Geplante neue Produkte

| Product-ID | Preis | Trigger | Reward |
|-----------|-------|---------|--------|
| `progression_bundle_l20` | 7.99 EUR | Einmalig nach L20-Abschluss, Offer-Popup | 10.000 Coins + 50 Gems + 3 Rare-Karten |
| `comeback_pack` | 4.99 EUR | >14 Tage inaktiv (DailyRewardService Hook) | 5.000 Coins + 25 Gems + 2 Epic-Karten |
| `elite_pass_season` | 4.99 EUR | Saisonal bei Saisonstart | +30% BattlePass-XP gesamte Season + Extra-Track mit 5 Legendary-Karten |

**Infrastruktur nötig:** `IPromoOfferService` (Eligibility + Purchase + Gewährung), Offer-Popup-UI (ähnlich StarterPack), PurchaseService-Product-IDs registrieren, saisonaler Elite-Pass-Tracker (Parallel zu BattlePass), Comeback-Eligibility in `IDailyRewardService`.

**Empfehlung:** Progression Bundle zuerst (höchste erwartete Take-Rate bei klarem "I want to skip the grind"-Moment), Comeback Pack zweites (Retention-Werkzeug), Elite Pass als Season-Feature.
