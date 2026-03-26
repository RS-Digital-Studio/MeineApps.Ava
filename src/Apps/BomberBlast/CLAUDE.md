# BomberBlast (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Bomberman-Klon mit SkiaSharp Rendering, AI Pathfinding und mehreren Input-Methoden.
Landscape-only auf Android. Grid: 15x10. Zwei Visual Styles: Classic HD + Neon/Cyberpunk.

**Version:** 2.0.28 (VersionCode 38) | **Package-ID:** org.rsdigital.bomberblast | **Status:** Geschlossener Test

## Icon-System (Eigene Neon Arcade Icons)

- **Kein Material.Icons** - eigenes GameIcon-System mit 152 Icons
- `Icons/GameIcon.cs`: Custom `PathIcon`-Ableitung mit `StyleKeyOverride => typeof(PathIcon)`
- `Icons/GameIconKind.cs`: Enum mit allen verfuegbaren Icons
- `Icons/GameIconPaths.cs`: Eigene geometrische SVG-Pfade im "Neon Arcade" Stil (nur M/L/H/V/Z)
- `Icons/GameIconRenderer.cs`: SkiaSharp-Renderer fuer Icons auf SKCanvas (gecachte SKPath)
- **Design-Sprache**: Oktagone (8 Seiten, flach) statt Kreise, scharfe Kanten, Arcade-Aesthetik
- **Converter**: `StringToGameIconKindConverter` fuer String→GameIconKind in XAML-Bindings
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
- **FloatingJoystick**: Touch-basiert, zwei Modi: Floating (Standard) + Fixed. Radius 75dp, Deadzone Fixed 15% / Floating 5%, Richtungs-Hysterese (1.15x)
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
- **Gem-Trickle**: 2 Gems bei erstmaligem 3-Sterne-Abschluss (Story-Modus) via GameTrackingService.OnFirstThreeStars() — 100 Level × 2G = 200G, reicht für 1 Legendary-Skin
- **Premium-Multiplikator**: 2x Coins bei LevelComplete (IsPremium), 3x bei GameOver-Trostcoins
- **Effizienz-Bonus**: Skaliert nach Welt (1-10), belohnt wenige Bomben
- **ShopService**: 9 permanente Upgrades (StartBombs, StartFire, StartSpeed, ExtraLives, ScoreMultiplier, TimeBonus, ShieldStart, CoinBonus, PowerUpLuck)
- **Preise**: 1.000 - 35.000 Coins (StartBombs/StartFire: 1.000/3.500/10.000), Max-Levels: 1-3, Gesamt: ~189.000 Coins
- **Dungeon-Trennung**: Shop-Upgrades gelten NUR in Story/Daily/QuickPlay/Survival. Im Dungeon: Base-Stats + Dungeon-Buffs

### Level-Gating (ProgressService)
- 100 Story-Level in 10 Welten (World 1-10 a 10 Level)
- Welt-Freischaltung: 0/0/10/25/45/70/100/135/175/220/240 Sterne
- Stern-System: 3 Sterne pro Level (Zeit-basiert), Fail-Counter für Level-Skip

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
2. `level_skip` → GameOver: Level überspringen (nach 2 Fails)
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
- SKCanvasView Rad-Rendering, Spin-Animation (min. 5 Drehungen, Ease-Out)

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

- **7-Tage-Zyklus**: 500-5000 Coins, Tag 5 Extra-Leben
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
- **Eintritt**: 1x/Tag gratis, 500 Coins, 5 Gems (BAL-31: von 10 gesenkt), oder Rewarded Ad (1x/Tag)
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
- **Starter Pack**: 2500C + 10G + 2 Rare-Karten (ab Level 5, einmaliges Gratis-Geschenk)
- **Rotating Deals**: 3 tägliche + 1 wöchentliches Angebot, 20-50% Rabatt, Seeded Random. Kein Coins→Gems-Pathway (Economy-Trennung)
- **Extended Gem-Sinks**: Karten für Gems, Extra Spin (3G), Dungeon-Revive (15G), 5. Deck-Slot (20G), BP Premium (150G)
- **Gem-IAP**: 4 Pakete (100G/0,99EUR, 500G/3,99EUR, 1500G/7,99EUR, 5000G/14,99EUR)
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
