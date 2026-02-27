# BomberBlast (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Bomberman-Klon mit SkiaSharp Rendering, AI Pathfinding und mehreren Input-Methoden.
Landscape-only auf Android. Grid: 15x10. Zwei Visual Styles: Classic HD + Neon/Cyberpunk.

**Version:** 2.0.20 (VersionCode 30) | **Package-ID:** org.rsdigital.bomberblast | **Status:** Geschlossener Test

## Haupt-Features

### SkiaSharp Rendering (GameRenderer - 7 Partial Classes)
- Volle 2D-Engine via SKCanvasView (Avalonia.Skia)
- Zwei Visual Styles: Classic HD + Neon/Cyberpunk (IGameStyleService)
- 60fps Game Loop via DispatcherTimer (16ms) in GameView.axaml.cs, InvalidateSurface() treibt PaintSurface
- DPI-Handling: `canvas.LocalClipBounds` statt `e.Info.Width/Height`
- GC-Optimierung: Gepoolte SKPaint/SKFont/SKPath (6 per-frame Allokationen eliminiert), HUD-String-Caching (11 Cache-Felder: Survival-Timer, Combo, Enemies, Speed, Curse), gecachter SKMaskFilter für HUD-Glow
- **Shader-Optimierung**: Alle per-Frame SKShader-Allokationen eliminiert. Background/Vignette/DynamicLighting-Shader beim Init gecacht. Grid-Border-Transitions (Ice/Lava/Teleporter) nutzen 2-Step-Alpha-DrawRect statt LinearGradient. ExplosionShaders (FlameLayer/FlameTongues/HeatHaze) nutzen Solid-Color statt Gradient. Fog-Overlay nutzt Stroke-Ringe statt RadialGradient+SKPaint-Allokation
- **IEnumerable→List**: Render()/CollectLightSources()/TrailSystem.Update() nehmen List<T> statt IEnumerable<T> (kein Interface-Dispatch-Overhead)
- Rainbow-Explosion-Skin: HSL-Farben nur alle 3 Frames aktualisiert (`_rainbowUpdateCounter % 3`)
- HUD: Side-Panel rechts (TIME, SCORE, COMBO mit Timer-Bar, LIVES, BOMBS/FIRE mit Mini-Icons, PowerUp-Liste mit Glow)
- **Partial Classes**: GameRenderer.cs (Core/Palette/Viewport), .Grid.cs (Boden/Wände/Blöcke/ProceduralTextures), .Characters.cs (Spieler+12 Gegner), .Bosses.cs (5 Bosse), .Items.cs (PowerUps/Bomben/Exit), .Atmosphere.cs (Hintergrund/Vignette/Schatten/Fackeln), .HUD.cs
- **ReducedEffects**: Deaktiviert alle atmosphärischen Systeme (Weather, Ambient, DynamicLighting, Trails, ShaderEffects, Vignette, MoodLighting, BackgroundElements)
- **Boden-Cache**: Alle 150 Floor-Tiles als SKBitmap gecacht (1 DrawBitmap statt ~750 Draw-Calls/Frame), invalidiert bei Welt-/Style-Wechsel

### Atmosphärische Subsysteme (5 Systeme, alle struct-basiert)
| System | Beschreibung |
|--------|-------------|
| DynamicLighting | Radius-basierte Lichtquellen (Bomben, Explosionen, Lava, Eis, PowerUps, Exit, Bosse, Fackeln), SKBlendMode.Screen |
| WeatherSystem | Welt-spezifische Wetter-Partikel (Blätter, Funken, Tropfen, Asche, Sand, Blasen etc.), struct-Pool 80 max |
| AmbientParticleSystem | Hintergrund-Partikel (Glühwürmchen, Dampf, Kristalle, Vögel, Glut etc.), struct-Pool 60 max |
| ShaderEffects | GPU-basierte Post-Processing (SkSL Water Ripples + CPU-Fallback, Color Grading, Chromatic Aberration, Damage Flash, Heat Shimmer) |
| TrailSystem | Charakter-Spuren (Spieler-Fußabdrücke, Ghost-Afterimages, Pontan-Feuer, Boss-Eis/Lava-Trails), struct-Pool 40 max |

### Prozedurale Texturen (ProceduralTextures.cs)
- Noise2D/Fbm (Perlin-ähnlich), CellRandom (deterministisch pro Zelle)
- 12 Textur-Funktionen: DrawGrassBlades, DrawCracks, DrawSandGrain, DrawBrickPattern, DrawWoodGrain, DrawIceCrystals, DrawMossPatches, DrawMetalRivets, DrawCoralGrowth, DrawEmberCracks, DrawMarbleVeins, DrawCloudWisps
- Verwendet in GameRenderer.Grid.cs für 10 Welt-spezifische Boden/Wand/Block-Texturen

### SkiaSharp Zusatz-Visualisierungen (14 Renderer)
| Renderer | Beschreibung |
|----------|-------------|
| GameRenderer | Haupt-Spiel-Rendering (Grid, Entities, Explosions, HUD, Boss-Rendering mit HP-Bar + Attack-Telegraph) |
| ExplosionShaders | CPU-basierte Flammen: Arm-Rendering (Bezier-Pfade, FBM Noise), Heat Haze |
| ParticleSystem | Struct-Pool (300 max), 4 Formen (Rectangle, Circle, Spark, Ember), Glow-Effekte |
| ScreenShake | Explosions-Shake (3px) + Player-Death-Shake (5px) |
| GameFloatingTextSystem | Score-Popups, Combo-Text, PowerUp-Text (Struct-Pool 20 max) |
| TutorialOverlay | 4-Rechteck-Dimming + Text-Bubble + Highlight |
| HelpIconRenderer | Statische Enemy/Boss/PowerUp/BombCard Icons für HelpView, CollectionView, DeckView. DrawEnemy (12 Typen), DrawBoss (5 Typen), DrawPowerUp (12 Typen), DrawBombCard (14 Typen mit Farben aus GameRenderer.Items.cs) |
| HudVisualization | Animierter Score-Counter (Ziffern rollen hoch) + pulsierender Timer (<30s) + PowerUp-Icons mit Glow |
| LevelSelectVisualization | Level-Thumbnails mit 10 Welt-Farben + Welt-spezifische Mini-Muster + Gold-Shimmer Sterne + Lock-Overlay |
| AchievementIconRenderer | 5 Kategorie-Farben, Trophy bei freigeschaltet, Schloss+Fortschrittsring bei gesperrt |
| GameOverVisualization | Großer Score mit Glow + Score-Breakdown Balken + Medaillen (Gold/Silber/Bronze) + Coin-Counter |
| DiscoveryOverlay | Erstentdeckungs-Hint (Gold-Rahmen, NEU!-Badge, Titel+Beschreibung, Fade-In+Scale-Bounce, Auto-Dismiss 5s) |
| ShopIconRenderer | 12 prozedurale Shop-Upgrade-Icons (Bombe/Flamme/Blitz/Herz/Stern/Uhr/Schild/Münzen/Kleeblatt/Eis/Feuer/Schleim), gepoolte SKPaint |
| MenuBackgroundRenderer | Animierter Menü-Hintergrund mit 7 Themes (BackgroundTheme Enum): Default (Bomben+Funken+Flammen), Dungeon (Fackeln+Fledermäuse+Steine), Shop (Münzen+Shimmer+Gems), League (Trophäen+Sterne+Podest), BattlePass (XP-Orbs+Streifen+Badges), Victory (Confetti+Fireworks+Gold), LuckySpin (Regenbogen+Glitzer+Lichtstreifen). Max 60 Partikel/Theme, struct-basiert, gepoolte SKPaint |
| DungeonMapRenderer | Dungeon Node-Map (Slay the Spire-inspiriert): 10 Reihen × 2-3 Nodes, farbige Kreise (30px) mit Raum-Typ-Icons, Verbindungslinien (gestrichelt/durchgezogen/gold), Modifikator-Badges, Pulsierender Glow-Ring für aktuellen Node, vertikaler Scroll |
| TornMetalRenderer | Prozeduraler "Torn Metal" Button-Hintergrund (SkiaSharp): Metallischer Gradient, gezackte Kanten mit abgebrochenen Ecken, Risse/Kratzer, Nieten, Metallic-Sheen. Deterministisch per Seed. Statische Klasse mit gepoolten SKPaint/SKPath |

### Input-Handler (3x)
- **FloatingJoystick**: Touch-basiert, zwei Modi: Floating (erscheint wo getippt, Standard) + Fixed (immer sichtbar unten links). Bomb-Button weiter in die Spielfläche gerückt (80px/60px Offset statt 30px/20px). Mobile-optimierte Sizes: Joystick-Radius 75dp, Deadzone 15%, Bomb-Button 70dp, Detonator 48dp, Hit-Zone 1.6x Visual. Richtungs-Hysterese (1.15x) gegen Flackern bei ~45°
- **Pre-Turn Buffering** (Player.cs): Bei senkrechtem Richtungswechsel wird die gewünschte Richtung gepuffert wenn der Spieler nicht am Zellzentrum ist. Spieler bewegt sich weiter in der alten Richtung, Turn wird automatisch ausgeführt sobald Querachse innerhalb 40% des Zellzentrums liegt. Snap der Querachse bei Turn für pixelgenaues Grid-Alignment
- **Keyboard**: Arrow/WASD + Space (Bomb) + E (Detonate) + T (ToggleSpecialBomb) + Escape (Pause) → Desktop Default
- **Gamepad**: D-Pad (Key.Up/Down/Left/Right, Priorität über Analog-Stick) + Analog-Stick (4-Wege-Quantisierung, Deadzone 0.25) + Face-Buttons (A=Bomb, B/X=Detonate, Y=ToggleSpecialBomb, Start=Pause). Kein visuelles Rendering
- InputManager verwaltet aktiven Handler, auto-detect Desktop vs Android, JoystickFixed-Setting persistiert
- **Auto-Switch**: Touch→Joystick, WASD/Space/E→Keyboard, GamepadButton/AnalogStick→Gamepad. Pfeiltasten an aktiven Handler (geteilt zwischen Keyboard + Gamepad D-Pad)
- **Android Controller**: MainActivity.DispatchKeyEvent (Face-Buttons, Keycode.ButtonA/B/X/Y/Start/Select/Menu) + DispatchGenericMotionEvent (Analog-Stick, Axis.X/Y)

### AI (EnemyAI.cs + AStar.cs)
- A* Pathfinding (Object-Pooled PriorityQueue, HashSet, Dictionaries)
- BFS Safe-Cell Finder (Pooled Queues)
- Danger-Zone: **Einmal pro Frame** vorberechnet via `PreCalculateDangerZone()` (nicht pro Gegner)
- Kettenreaktions-Erkennung (iterativ, max 5 Durchläufe)
- 12 Enemy-Typen (8 Basis + 4 neue: Tanker/Ghost/Splitter/Mimic)
- **Boss-AI**: Eigene `UpdateBossAI()` Methode - kein A*-Pathfinding, direkter Richtungs-Check zum Spieler, Multi-Cell Kollisionsprüfung (`CanBossMoveInDirection`), steht still während Telegraph/Angriff, Enraged-Modus halbiert Decision-Timer (1.0s→0.5s)

### Boss-System (BossEnemy.cs)
- 5 Boss-Typen: StoneGolem, IceDragon, FireDemon, ShadowMaster, FinalBoss
- Jedes 10. Level = Boss-Level (L10-L100), Boss-Typ Repeat alle 2 Welten
- BossEnemy erbt von Enemy, eigene BoundingBox (Multi-Cell), MoveBoss() mit Multi-Cell Kollision
- HP-System: 3-8 HP je nach Typ, Enrage bei 50% HP (schneller + aggressiver)
- Spezial-Angriffe: Telegraph-Timer (2s Warnung) → Attack-Effect (1.5s) → Cooldown (12-18s, kürzer bei Enrage)
- Level.BossKind Property bestimmt welcher Boss spawnt, Boss wird in Arena-Mitte platziert (Blöcke werden freigeräumt)
- 10 neue RESX-Keys in 6 Sprachen (Boss-Namen + Angriffs-Texte + BossFight)

### Coin-Economy + Shop
- **CoinService**: Persistente Coin-Waehrung (Level-Score ÷ 3 → Coins bei Level-Complete, ÷ 6 bei Game Over)
- **Effizienz-Bonus**: Skaliert nach Welt (1-10), belohnt wenige Bomben (≤5/≤8/≤12)
- **ShopService**: 9 permanente Upgrades (StartBombs, StartFire, StartSpeed, ExtraLives, ScoreMultiplier, TimeBonus, ShieldStart, CoinBonus, PowerUpLuck)
- **Upgrade-Preise**: 1.500 - 35.000 Coins, Max-Levels: 1-3, Shop-Gesamtkosten: ~190.000 Coins
- **ShieldStart**: Spieler startet mit Schutzschild (absorbiert 1 Gegnerkontakt, Cyan-Glow)
- **CoinBonus**: +25%/+50% extra Coins pro Level
- **PowerUpLuck**: 1/2 zusaetzliche zufaellige PowerUps pro Level
- **Dungeon-Trennung**: Shop-Upgrades gelten NUR in Story/Daily/QuickPlay/Survival. Im Dungeon: Base-Stats (1 Bombe, 1 Feuer, kein Speed/Shield), dann Dungeon-Buffs addiert. Karten-Deck wird in beiden Modi geladen.

### Level-Gating (ProgressService)
- 100 Story-Level in 10 Welten (World 1-10 a 10 Level)
- Welt-Freischaltung: 0/0/10/25/45/70/100/135/175/220 Sterne
- Stern-System: 3 Sterne pro Level (Zeit-basiert)
- Fail-Counter fuer Level-Skip

### Progressive Feature-Freischaltung (MainMenuViewModel)
- Features werden basierend auf `HighestCompletedLevel` freigeschaltet
- Level 0-2: Nur Story, Settings, Help, Profile
- Level 3+: + Shop
- Level 5+: + Survival, QuickPlay
- Level 8+: + DailyChallenge, LuckySpin
- Level 10+: + Achievements, Statistics, Collection (1. Boss besiegt)
- Level 15+: + Deck, DailyMissions, WeeklyMissions
- Level 20+: + Dungeon
- Level 30+: + League, BattlePass
- "NEU!"-Badges via `IPreferencesService` (`feature_seen_{name}`) - Badge verschwindet nach erstem Besuch
- `MarkFeatureSeen(string)` wird in den Navigation-Commands aufgerufen

## Premium & Ads

### Premium-Modell
- **Preis**: 1,99 EUR (`remove_ads`)
- Kostenlos spielbar, Upgrades grindbar, Ads optional

### Fullscreen/Immersive Mode (Android)
- **Aktivierung**: OnCreate + OnResume in MainActivity (WindowInsetsController)
- **Modus**: SystemBars ausgeblendet, TransientBarsBySwipe (Wisch-Geste zeigt Bars kurz an)
- **Landscape-Spiel**: Maximale Bildschirmfläche, keine Status-/Navigationsleiste

### Ad-Banner-Spacer (MainView)
- **MainView**: Grid mit `RowDefinitions="*,Auto"` → Row 0 Content-Panel, Row 1 Ad-Spacer (64dp)
- **IsAdBannerVisible**: Property im MainViewModel, gesteuert per Route (Game=false, andere=BannerVisible)
- **AdsStateChanged Event**: Reagiert auf Show/Hide des Banners
- **Dialoge/Overlays**: `Grid.RowSpan="2"` (über beide Rows, nicht abgeschnitten)

### Banner im GameView
- **Deaktiviert**: Kein Banner während Gameplay (seit 15.02.2026)
- Banner wird beim Betreten des GameView versteckt, beim Verlassen wieder angezeigt
- BannerTopOffset immer 0 (kein Viewport-Offset mehr nötig)

### Rewarded (5 Placements)
1. `continue` → GameOver: Coins verdoppeln (1x pro Versuch)
2. `level_skip` → GameOver: Level ueberspringen (nach 2 Fails)
3. `power_up` → LevelSelect: Power-Up Boost (ab Level 20, alle 3 PowerUps)
4. `score_double` → GameView: Score verdoppeln (nach Level-Complete)
5. `revival` → GameOver: Weitermachen / Wiederbelebung (1x pro Versuch)

## App-spezifische Services

| Service | Zweck |
|---------|-------|
| ISoundService | Audio-Abstraktion (NullSoundService Desktop, AndroidSoundService Android) |
| IProgressService | Level-Fortschritt, Sterne, Fail-Counter, World-Gating |
| IHighScoreService | Top 10 Scores (sqlite-net-pcl) |
| IGameStyleService | Visual Style Persistenz (Classic/Neon) |
| ICoinService | Coin-Balance, AddCoins, TrySpendCoins |
| IGemService | Gem-Balance (zweite Währung), AddGems, TrySpendGems |
| IShopService | PlayerUpgrades Persistenz, Preise, Kauf-Logik |
| ITutorialService | 6-Schritte Tutorial fuer Level 1 (Move, Bomb, Hide, PowerUp, DefeatEnemies, Exit) |
| IDailyRewardService | 7-Tage Daily Login Bonus (500-5000 Coins, Tag 5 Extra-Leben) + Comeback-Bonus (>3 Tage inaktiv → 2000 Coins + 5 Gems) |
| IStarterPackService | Einmaliges Starterpaket nach Level 5: 5000 Coins + 20 Gems + 3 Rare-Karten (Coin-Kauf 4999) |
| ICustomizationService | Spieler/Gegner-Skins (Default, Gold, Neon, Cyber, Retro + 3 Gem-Skins: Crystal/Shadow/Phoenix), TryPurchasePlayerSkinWithGems() |
| IReviewService | In-App Review nach Level 3-5, 14-Tage Cooldown |
| IAchievementService | 66 Achievements in 5 Kategorien (Progress, Mastery, Combat, Skill, Challenge), JSON-Persistenz |
| IDiscoveryService | Erstentdeckungs-Tracking (PowerUps/Mechaniken), Preferences-basiert |
| IDailyChallengeService | Tägliche Herausforderung, Streak-Tracking, Score-Persistenz |
| IPlayGamesService | Google Play Games Services v2 (Leaderboards, Online-Achievements, Auto-Sign-In) |
| ILuckySpinService | Glücksrad: 8 gewichtete Segmente, 1x gratis/Tag, JSON-Persistenz |
| IWeeklyChallengeService | Wöchentliche Missionen: 5/Woche aus 8er-Pool, Montag-Reset, JSON-Persistenz |
| IDailyMissionService | Tägliche Missionen: 3/Tag aus 8er-Pool, Mitternacht-UTC-Reset, JSON-Persistenz |
| ICardService | Karten-System: 14 Bomben-Karten mit Raritäten, Deck (4+1 Slots, 5. Slot für 20 Gems freischaltbar), Upgrade (Bronze→Silber→Gold), Drops nach Level-Complete |
| IDungeonService | Dungeon-Run Roguelike-Modus: Run-State, Floor-Belohnungen, 16 Buffs (12+4 Legendary), Raum-Typen, Modifikatoren, Node-Map, Ascension, Synergies, DungeonCoins, JSON-Persistenz |
| IDungeonUpgradeService | 8 permanente Dungeon-Upgrades (DungeonCoins-Währung): StartBombs/Fire/Speed, ExtraBuffChoice, BossGoldBonus, Shield, CardDropBoost, ReviveCostReduction |
| ICollectionService | Sammlungs-Album: Gegner/Bosse/PowerUp-Tracking (Encounter/Defeat), Meilenstein-Belohnungen, aggregiert Card+Customization |
| IFirebaseService | Firebase REST API Client: Anonymous Auth + Realtime Database CRUD, plattformübergreifend via HttpClient |
| ILeagueService | Liga-System: 5 Tiers (Bronze→Diamant), 14-Tage-Saisons, Firebase Online-Rangliste + NPC-Backfill, Punkte/Rangliste/Auf-Abstieg |
| ICloudSaveService | Cloud Save: Local-First Sync, 35 Persistenz-Keys, Debounce 5s, Konflikt-Resolution (TotalStars→Wealth→Cards→Timestamp) |
| IRotatingDealsService | Rotierende Angebote: 3 tägliche + 1 wöchentliches Deal mit 20-50% Rabatt, Seeded Random per Datum, JSON-Persistenz |

## Architektur-Entscheidungen

- **Game Loop**: DispatcherTimer (16ms) in GameView → InvalidateSurface() → OnPaintSurface → GameEngine.Update + Render. MAX_DELTA_TIME = 0.05f (50ms Cap, verhindert Physik-Sprünge bei Lag-Spikes)
- **Touch-Koordinaten**: Proportionale Skalierung (Render-Bounds / Control-Bounds Ratio) fuer DPI-korrektes Mapping
- **Invalidierung**: IMMER `InvalidateSurface()` (InvalidateVisual feuert NICHT PaintSurface bei SKCanvasView)
- **Keyboard Input**: Window-Level KeyDown/KeyUp in MainWindow.axaml.cs → GameViewModel
- **DI**: 23 ViewModels (alle Singleton), 29 Services, GameEngine + GameRenderer in App.axaml.cs (GameRenderer + IAchievementService + IDiscoveryService + IPlayGamesService + IWeeklyChallengeService + IDailyMissionService + ICardService + IDungeonService + IDungeonUpgradeService + ILeagueService per DI in GameEngine injiziert). IFirebaseService als Singleton registriert (LeagueService nimmt es per Constructor). Lazy-Injection: 4 Services (BattlePass, Card, League, DailyMission) erhalten IAchievementService via SetAchievementService() nach ServiceProvider-Build. Lazy-Injection: GemService + CardService erhalten IWeeklyChallengeService + IDailyMissionService via SetMissionServices(). Lazy-Injection: CustomizationService erhält IGemService via SetGemService() für Gem-Skin-Käufe
- **GameEngine Partial Classes**: GameEngine.cs (Kern), .Collision.cs, .Explosion.cs, .Level.cs, .Render.cs
- **GameEngine Events**: Kein "On"-Prefix (Convention-konform): `GameOver`, `LevelComplete`, `Victory`, `ScoreChanged`, `CoinsEarned`, `PauseRequested`, `DirectionChanged`, `DungeonFloorComplete`, `DungeonBuffSelection`, `DungeonRunEnd`
- **GameEngine Dispose**: Wird via `App.DisposeServices()` aufgerufen (Desktop: ShutdownRequested, Android: OnDestroy). Disposed: GameEngine, GameRenderer, GameViewModel, IFirebaseService
- **HUD-Labels**: Gecacht in GameEngine-Feldern, aktualisiert bei Level-Start und LanguageChanged (nicht pro Frame)
- **Cloud Save**: `_cloudSaveInitTask` Task wird gespeichert (kein Fire-and-Forget), verhindert Race Condition
- **12 PowerUp-Typen**: BombUp, Fire, Speed, Wallpass, Detonator, Bombpass, Flamepass, Mystery, Kick, LineBomb, PowerBomb, Skull
- **PowerUp-Freischaltung**: Level-basiert via `GetUnlockLevel()` Extension. Story-Mode filtert gesperrte PowerUps. DailyChallenge: Alle verfügbar
- **Discovery-System**: `IDiscoveryService` (Preferences-basiert), `DiscoveryOverlay` (SkiaSharp), pausiert Spiel bei Erstentdeckung
- **Exit-Cell-Cache**: `_exitCell` in GameEngine, gesetzt bei RevealExit/Block-Zerstörung → Kollisions-Check + RenderExit ohne Grid-Iteration
- **Coin-Berechnung**: `_scoreAtLevelStart` → Coins basieren auf Level-Score (nicht kumulierter Gesamtscore), verhindert Inflation
- **Pontan-Strafe**: Gestaffelt via Timer (`_pontanPunishmentActive`), welt-skaliert (W1: 1 Pontan/8s/5s Gnadenfrist, W2: 2/6s/3s, W3+: 3/5s/0s)
- **Pfad-Invalidierung bei Block-Zerstörung**: `InvalidateEnemyPaths()` setzt alle Gegner-AI-Timer auf 0 + leert Pfad-Cache → sofortige Neuberechnung
- **Slow-Motion**: `_slowMotionFactor` auf deltaTime multipliziert in UpdatePlaying, Ease-Out Kurve
- **AI Danger-Zone**: Einmal pro Frame vorberechnet, iterative Kettenreaktions-Erkennung (max 5 Durchläufe)
- **Achievements**: IAchievementService in GameEngine injiziert, automatische Prüfung bei Level-Complete/Kill/Wave/Stars
- **ExplosionCell**: Struct statt Class (weniger Heap-Allokationen)
- **Dirty-Lists**: `_destroyingCells`, `_afterglowCells`, `_specialEffectCells` ersetzen 3x volle 150-Zellen Grid-Iteration pro Frame. Zellen werden bei Explosion/Effekt-Start registriert, bei Ablauf via Rückwärts-Iteration entfernt. Reduziert Update-Aufwand von O(150) auf O(aktive Zellen)
- **Achievement Dictionary-Lookup**: `_achievementLookup` Dictionary<string,Achievement> für O(1) TryUnlock/UpdateProgress statt O(n) List.Find
- **CollectionService Debounce-Save**: `_isDirty` Flag + 5s Debounce-Intervall statt sofortigem Save bei jedem Record-Aufruf. FlushIfDirty() bei GameOver/LevelComplete
- **CollectionView/DeckView SkiaSharp-Icons**: Echte Gegner/Boss/PowerUp/Bomben-Grafiken statt generischer MaterialIcons. SKCanvasView in AXAML mit PaintSurface-Handler im Code-Behind. CollectionEntry hat optionale Typ-Enums (EnemyType?, BossType?, PowerUpType?, BombType?) die in CollectionService Build-Methoden gesetzt und über CollectionDisplayItem durchgereicht werden. Kosmetik nutzt weiter MaterialIcons
- **GetTotalStars**: Gecacht in ProgressService, invalidiert bei Score-Änderung
- **Score-Multiplikator**: Nur auf Level-Score angewendet (nicht kumulierten Gesamt-Score)
- **Timer**: Läuft in Echtzeit (`realDeltaTime`), nicht durch Slow-Motion beeinflusst

### Spezial-Bomben-System (13 Typen, 3 Shop + 10 Karten/Drops)
- **BombType Enum**: Normal, Ice, Fire, Sticky, Smoke, Lightning, Gravity, Poison, TimeWarp, Mirror, Vortex, Phantom, Nova, BlackHole
- **3 Shop-Bomben**: Ice (Frost 3s, 50% Slow), Fire (Lava 3s, Schaden), Sticky (Kettenreaktion + Klebe 1.5s)
- **10 neue Bomben** (Phase 1 Feature-Expansion):
  - Smoke (Rare): 3x3 Nebelwolke, Gegner-AI läuft 4s zufällig (EnemyAI Konfusion)
  - Lightning (Rare): Blitz springt zu 3 nächsten Gegnern (ignoriert Wände)
  - Gravity (Rare): Zieht alle Gegner im 3-Zellen-Radius 1 Zelle zum Zentrum
  - Poison (Rare): Gift-Zellen (3s), Gegner verlieren HP beim Betreten (periodischer Schaden)
  - TimeWarp (Epic): Alles im Radius 5s auf 50% verlangsamt (inkl. Bomben-Timer)
  - Mirror (Epic): Explosion kopiert sich in Gegenrichtung (doppelte Reichweite)
  - Vortex (Epic): Spiralförmige Explosion, trifft mehr Zellen als linear
  - Phantom (Epic): Explosion durchdringt 1 unzerstörbare Wand
  - Nova (Legendary): 360-Grad Explosion (ALLE Zellen im Range), lässt PowerUp fallen
  - BlackHole (Legendary): Schwarzes Loch (3s), saugt Gegner ein (0.3x Speed), dann Explosion
- **Zellen-Properties**: IsFrozen/FreezeTimer, IsLavaActive/LavaTimer, IsSmokeCloud/SmokeTimer, IsPoisoned/PoisonTimer, IsGravityWell/GravityTimer, IsTimeWarped/TimeWarpTimer, IsBlackHole/BlackHoleTimer
- **Verlangsamungs-Stacking**: Frost (0.5x) + TimeWarp (0.5x) + BlackHole (0.3x) stacken multiplikativ auf deltaTime
- **EnemyAI Smoke-Konfusion**: Gegner auf IsSmokeCloud-Zellen nutzen GetRandomValidDirection() statt Pathfinding
- **Rendering**: 10 neue Bomben-Farben/Partikel in GameRenderer.Items.cs, 5 neue Zellen-Effekte in GameRenderer.Grid.cs

### Raritäts-System (Phase 1 Feature-Expansion)
- **4 Stufen**: Common (#FFFFFF), Rare (#2196F3), Epic (#9C27B0), Legendary (#FFD700)
- **Rarity Enum** (`Models/Rarity.cs`): Extensions GetColor, GetGlowColor, GetGlowRadius, GetBorderWidth, GetNameKey
- **RarityRenderer** (`Graphics/RarityRenderer.cs`): DrawRarityBorder, DrawRarityGlow, DrawRarityShimmer, DrawRarityBackground, DrawRarityBadge, DrawComplete. Gepoolte SKPaint

### Gem-Währung (Phase 1 Feature-Expansion)
- **IGemService/GemService** (`Services/`): Zweite Währung neben Coins, NUR durch Gameplay verdienbar
- **Pattern**: Identisch zu CoinService (Balance, AddGems, TrySpendGems, CanAfford, BalanceChanged Event)
- **Persistenz**: IPreferencesService JSON, Key "GemData"

## Game Juice & Effects

- **FloatingText (UI)**: "x2!" (gold) bei Coins-Verdopplung, "LevelComplete" (gruen) - View-Overlays
- **In-Game FloatingText**: `Graphics/GameFloatingTextSystem.cs` - Struct-Pool (20 max), Score-Popups (+100, +400), Combo-Text (x2!, MEGA x5!), PowerUp-Collect-Text (+SPEED, +FIRE, +KICK, +LINE, +POWER, CURSED!)
- **Combo-System**: Kills innerhalb 2s-Fenster → Combo-Bonus (x2: +200, x3: +500, x4: +1000, x5+: +2000) mit farbigem Floating Text. Chain-Kill-Bonus: 1.5x Multiplikator bei 3+ Combo (Kettenreaktion), "CHAIN x{N}!" goldener Text
- **Haptic-Feedback**: `_vibration.VibrateLight()` bei PowerUp-Einsammlung (GameEngine.Collision.cs), Bomben-Platzierung (GameEngine.Explosion.cs), `_vibration.VibrateMedium()` bei Exit-Erscheinen (GameEngine.Level.cs)
- **Timer-Warnung**: Pulsierender roter Bildschirmrand unter 30s, Intensitaet steigt mit sinkender Zeit
- **Danger Telegraphing**: Rote pulsierende Warnzonen auf Zellen im Explosionsradius aktiver Bomben (Zuendschnur < 0.8s), Intensitaet steigt mit sinkender Zuendzeit
- **Celebration**: Confetti bei Welt-Freischaltung
- **ScreenShake**: Explosion (3px, 0.2s), PlayerDeath (5px, 0.3s) via `Graphics/ScreenShake.cs`, Timer-Clamp verhindert negativen Progress
- **Hit-Pause**: Frame-Freeze bei Enemy-Kill (50ms), Player-Death (100ms)
- **Partikel-System**: `Graphics/ParticleSystem.cs` - Struct-Pool (300 max), 4 Formen (Rectangle, Circle, Spark, Ember), Glow-Halo auf Funken/Glut
- **Flammen-Rendering (CPU)**: `Graphics/ExplosionShaders.cs` - Arm-basiert (durchgehende Bezier-Pfade statt Pro-Zelle), 3 Schichten (Glow + Hauptflamme + Kern), FBM-Noise-modulierte Ränder, natürliche Verjüngung zum Ende, Flammen-Zungen entlang der Arme
- **Wärme-Distortion (Heat Haze)**: Gradient-Overlay über Explosions-Bounding-Box, aufsteigender Wellen-Effekt
- **Explosions-Funken**: Elongierte Streifen in Flugrichtung + Glow-Halo + heller Kopf, 12 pro Explosion
- **Glut-Partikel**: Langsam aufsteigende glühende Punkte mit Pulsation + Glow, 9 pro Explosion
- **Doppelter Shockwave-Ring**: Äußerer diffuser Ring (orange, Glow) + innerer heller Ring (core-Farbe)
- **Explosions-Nachglühen**: 0.4s warmer Schimmer auf Zellen nach Explosionsende (mit Glow + hellem Kern)
- **Bomben-Pulsation**: Beschleunigt von 8→24Hz + stärkere Amplitude je näher an Explosion
- **Squash/Stretch**: Bomben-Platzierung (Birth-Bounce 0.3s, sin-basiert), Bomben-Slide (15% Stretch in Richtung), Gegner-Tod (Squash flacher+breiter), Spieler-Tod (2-Phasen: Stretch hoch → Squash flach)
- **Walk-Animation**: Prozedurales Wippen (sin-basiert) bei Spieler-/Gegnerbewegung
- **Slow-Motion**: 0.8s bei letztem Kill oder Combo x4+, Ease-Out (30%→100%), `_slowMotionFactor` auf deltaTime
- **Explosions-Shockwave**: Expandierender Ring (40% der Explosionsdauer), Stroke wird dünner
- **Iris-Wipe**: Level-Start Kreis öffnet sich, Level-Complete Kreis schließt sich (letzte Sekunde), goldener Rand-Glow
- **Neon Style**: Brightened Palette, 3D Block-Edges, Glow-Cracks, Outer-Glow HUD-Text
- **Mini-Icons**: Bomb/Flame Icons statt "B"/"F" Labels im HUD
- **Curse-Indikator**: Pulsierender violetter Glow um Spieler bei aktivem Curse, HUD zeigt Curse-Typ + Timer
- **Musik-Crossfade**: SoundManager.Update() mit Fade-Out/Fade-In beim Track-Wechsel (0.5s)
- **View-Transitions**: CSS-Klassen-basiert (Border.PageView + .Active), Opacity DoubleTransition (200ms) zwischen allen 9 Views
- **Welt-Themes**: 10 Farbpaletten pro Style (Forest/Industrial/Cavern/Sky/Inferno/Ruins/Ocean/Volcano/SkyFortress/ShadowRealm), WorldPalette in GameRenderer
- **Sterne-Animation**: 3 Sterne bei Level-Complete mit gestaffelter Scale-Bounce Animation (0.3s Delay)
- **PowerUp-Einsammel-Animation**: Shrink + Spin + Fade (0.3s) bei Collect statt sofortigem Entfernen
- **Welt-/Wave-Ankündigung**: Großer "WORLD X!" Text bei Welt-Wechsel (Story)
- **Coin-Floating-Text**: "+X Coins" (gold) über dem Exit bei Level-Complete
- **Button-Animationen**: GameButton-Style mit Scale-Transition (1.05x hover, 0.95x pressed) in allen Menüs
- **Shop-Kauf-Feedback**: PurchaseSucceeded → Confetti + FloatingText, InsufficientFunds → roter FloatingText
- **Achievement-Toast**: AchievementUnlocked Event → goldener FloatingText "Achievement: [Name]!"
- **Coin-Counter-Animation**: GameOverView zählt Coins von 0 hoch (~30 Frames, DispatcherTimer)
- **Menü-Hintergründe**: MenuBackgroundCanvas (wiederverwendbar, ~30fps) mit Bomberman-thematischem Hintergrund (Gradient, Grid, Bomben-Silhouetten, Funken-Partikel, Flammen-Wisps) in 12 Menü-Views
- **LevelSelect Welt-Farben**: Level-Buttons farblich nach Welt unterschieden (Forest grün, Industrial grau, etc.)
- **Tutorial-Replay**: "Tutorial wiederholen" Button in HelpView (ITutorialService.Reset + Level 1 starten)

## Tutorial-System (Phase 5)

- 6 interaktive Schritte: Move → PlaceBomb → Warning(Hide) → CollectPowerUp → DefeatEnemies → FindExit
- Automatischer Start bei Level 1 wenn kein Fortschritt
- SkiaSharp Overlay (`Graphics/TutorialOverlay.cs`) mit 4-Rechteck-Dimming (Alpha 100), halbtransparenter Text-Bubble (Alpha 128), Highlight-Box
- Highlight-Bereiche: InputControl, BombButton, GameField (40% Mitte), PowerUp/Exit (ganzes Spielfeld ohne HUD)
- HUD-Overlap vermieden: `gameAreaRight = screenWidth - 120f` (HUD_LOGICAL_WIDTH)
- Skip-Button in jedem Schritt, Warning-Schritt mit 3s Auto-Advance
- DefeatEnemies-Schritt: Wird getriggert wenn letzter Gegner getötet wird (GameEngine.Collision.cs)
- RESX-Keys fuer 6 Sprachen (TutorialMove/Bomb/Hide/PowerUp/DefeatEnemies/Exit/Skip)

## Survival-Modus (Phase 5)

- **Endloser Spielmodus**: Kein Exit, kein Level-Abschluss - kämpfe bis zum Tod
- **1 Leben**: Shop-Extra-Leben werden nicht angewendet, kein Continue möglich
- **Steigendes Spawning**: Alle 4s ein neuer Gegner, Intervall sinkt um 0.12s pro Spawn bis min. 0.8s
- **Gegner-Eskalation nach Zeit**: <20s Ballom, 20-45s Onil/Doll/Ballom, 45-90s Onil/Doll/Minvo/Doll, 90-150s Minvo/Ovapi/Tanker/Doll/Kondoria, 150s+ Ovapi/Tanker/Ghost/Pontan/Splitter/Mimic
- **Arena-Layout**: Offene Arena (BlockDensity 0.2), 2 Start-Balloms, 4 Basis-PowerUps (BombUp, Fire, Speed, Kick)
- **Timer**: 99999 (kein Pontan-Punishment), HUD zeigt KILLS (rot) + überlebte Zeit statt Countdown
- **Kill-Tracking**: Nutzt bestehendes `_enemiesKilled` → `SurvivalKills` Property
- **GameEngine**: `_isSurvivalMode` Flag, `StartSurvivalModeAsync()`, `UpdateSurvivalSpawning()`, `SpawnSurvivalEnemy()`
- **Spawn-Position**: Min. Manhattan-Distanz 4 zum Spieler, leere Zelle, orange Spawn-Partikel
- **GameOver**: Coins = Score/6 (bestehende Logik), GameOverView zeigt Kills + Zeit
- **Navigation**: MainMenu → Game?mode=survival → GameOver (TryAgain → //MainMenu/Game?mode=survival)
- **RESX-Keys**: 4 Keys in 6 Sprachen (SurvivalMode/SurvivalKills/SurvivalKillsLabel/SurvivalTimeLabel)

## Glücksrad / Lucky Spin (Phase 4)

- **Tägliches Glücksrad**: 1x gratis pro Tag, Extra-Spins per Rewarded Ad
- **9 gewichtete Segmente**: 50(w25), 100(w20), 250(w18), 500(w15), 100(w20), 750(w10), 250(w18), 5Gems(w8), 1500(w5/Jackpot)
- **SpinReward**: Coins + Gems Property (Gems-Segment: 5 Gems, Cyan-Farbe #00BCD4)
- **ILuckySpinService**: Spin(), ClaimFreeSpin(), IsFreeSpinAvailable, JSON-Persistenz (IPreferencesService, Key: "LuckySpinData")
- **LuckySpinViewModel**: SpinWheel/CollectReward/GoBack Commands, Spin-Animations-State (UpdateAnimation per DispatcherTimer), IGemService für Gem-Vergabe
- **LuckySpinView**: SKCanvasView Rad-Rendering (9 farbige Segmente, dynamische Segment-Anzahl, Zeiger-Dreieck, Münz-Mitte, Jackpot-Shimmer via IsJackpot, Glow-Ring)
- **Spin-Animation**: Start 720°/s, Ease-Out über letzte 2 Umdrehungen, min. 5 volle Drehungen, Segment-genaues Stoppen, dynamisch 360/segmentCount
- **Navigation**: MainMenu → LuckySpin → ".." (zurück), LuckySpinVm.FloatingTextRequested/CelebrationRequested
- **DI**: ILuckySpinService (Singleton), LuckySpinViewModel (Singleton, +IGemService), in MainViewModel verdrahtet
- **RESX-Keys**: 12 Keys in 6 Sprachen (LuckySpinTitle/Free/Ad/Collect/Total + SpinReward50/100/250/500/750/1500/5Gems)

## Weekly Challenge (Phase 6 + Phase 9.4)

- **5 wöchentliche Missionen**: Aus Pool von 14 Typen, deterministisch per ISO-Kalenderwoche generiert (Seeded Random)
- **14 Missions-Typen**: CompleteLevels, DefeatEnemies, CollectPowerUps, EarnCoins, SurvivalKills, UseSpecialBombs, AchieveCombo, WinBossFights, **+6 neue (Phase 9.4)**: CompleteDungeonFloors, CollectCards, EarnGems, PlayQuickPlay, SpinLuckyWheel, UpgradeCards
- **Montag-Reset**: UTC-basiert, Missionen wechseln jeden Montag 00:00 UTC
- **Belohnungen**: 350-700 Coins pro Mission, 2.000 Coins All-Complete-Bonus
- **IWeeklyChallengeService**: TrackProgress(type, amount), ClaimAllCompleteBonus(), JSON-Persistenz (IPreferencesService, Key: "WeeklyChallengeData")
- **WeeklyChallengeViewModel**: MissionItems mit Icon/Farbe pro Typ (14 Typen), Fortschrittsbalken, Zeitanzeige bis Reset
- **WeeklyChallengeView**: 2-Spalten Landscape-Layout (Missionen links, Stats rechts)
- **GameEngine-Hooks**: Collision.cs (PowerUps/Enemies/Combos/Boss), Explosion.cs (SpecialBombs), Level.cs (LevelComplete, DungeonFloor, QuickPlay), GameOverVM (EarnCoins)
- **Service-Hooks (Phase 9.4)**: CardService.AddCard() (CollectCards), CardService.TryUpgradeCard() (UpgradeCards), GemService.AddGems() (EarnGems) - alle via Lazy-Injection
- **ViewModel-Hooks (Phase 9.4)**: LuckySpinViewModel.CollectReward() (SpinLuckyWheel)
- **MainMenu-Integration**: Wöchentliche Herausforderungen Button mit "!"-Badge wenn Missionen offen
- **RESX-Keys**: 34 Keys in 6 Sprachen (22 original + 12 Phase 9.4)

## Daily Missions (Phase 14 + Phase 9.4)

- **3 tägliche Missionen**: Aus Pool von 14 Typen (gleiche wie Weekly), deterministisch per DayId generiert (Seeded Random)
- **Mitternacht-UTC-Reset**: Missionen wechseln jeden Tag 00:00 UTC
- **Belohnungen**: 100-300 Coins pro Mission, 500 Coins All-Complete-Bonus
- **Kleinere Ziele (Original 8)**: CompleteLevels 1-3, DefeatEnemies 5-15, CollectPowerUps 3-10, EarnCoins 300-1500, SurvivalKills 5-15, UseSpecialBombs 2-5, AchieveCombo 1-3, WinBossFights 1
- **Neue Ziele (Phase 9.4, 6 Typen)**: CompleteDungeonFloors 1-3, CollectCards 1-2, EarnGems 3-10, PlayQuickPlay 1-2, SpinLuckyWheel 1, UpgradeCards 1
- **IDailyMissionService**: TrackProgress(type, amount), ClaimAllCompleteBonus(), JSON-Persistenz (IPreferencesService, Key: "DailyMissionData")
- **Kombinierte Missions-View**: WeeklyChallengeView zeigt Daily (orange #FF9800) + Weekly (cyan #00BCD4) in 2-Spalten Layout
- **MainMenu**: "Missions"-Button (statt "Weekly Challenge") mit "!"-Badge wenn tägliche ODER wöchentliche Missionen offen
- **GameEngine-Hooks**: Parallel zu Weekly-Hooks, alle 14 Tracking-Typen in GameEngine + Services + ViewModels
- **RESX-Keys**: 33 Keys in 6 Sprachen (21 original + 12 Phase 9.4)

## Statistik-Seite (Phase 7)

- **Umfassende Statistiken**: 16 Stat-Karten in 4 Kategorien (Fortschritt, Kampf, Herausforderungen, Wirtschaft)
- **StatisticsViewModel**: 9 injizierte Services, 25+ ObservableProperties, OnAppearing() liest Daten aus allen Services
- **StatisticsView**: Landscape 2-Spalten-Layout (links: Fortschritt+Kampf, rechts: Herausforderungen+Wirtschaft)
- **Farbkodierung**: Fortschritt (#4CAF50 grün), Kampf (#F44336 rot), Herausforderungen (#FF9800 orange), Wirtschaft (#FFD700 gold)
- **MainMenu**: Statistik-Button neben Bestenliste (ChartLine Icon, grün)
- **Navigation**: MainMenu → Statistics → ".." (zurück)
- **RESX-Keys**: 22 Keys in 6 Sprachen (StatsTitle, StatsProgress, StatsCombat, etc.)

## Daily Challenge (Phase 10)

- **Tägliches Level**: Einzigartiges Level pro Tag, deterministisch via Seed (Datum-basiert: YYYY*10000+MM*100+DD)
- **Schwierigkeit**: ~Level 20-30, zufällige Mechanik + Layout (kein BossArena), 4-6 mittlere/starke Gegner, 180s Zeitlimit
- **Streak-System**: Konsekutive Tage mit Coin-Bonus (200/400/600/1000/1500/2000/3000), Reset bei >1 Tag Pause
- **Score-Tracking**: Best-Score pro Tag, TotalCompleted, CurrentStreak, LongestStreak
- **Ablauf**: MainMenu → DailyChallengeView → Game (mode=daily, level=seed) → GameOver → Score-Submit + Streak-Bonus
- **Navigation**: Eigene View (DailyChallengeView.axaml), DailyChallengeViewModel, IDailyChallengeService
- **Game-Engine Integration**: `StartDailyChallengeModeAsync(seed)`, `_isDailyChallenge` Flag, kein Continue, kein NextLevel (direkt GameOver nach LevelComplete)
- **LevelGenerator**: `GenerateDailyChallengeLevel(seed)` statische Methode, zufällige Mechanik/Layout/Gegner aus Seed
- **RESX-Keys**: 9 Keys in 6 Sprachen (DailyChallengeTitle/BestScore/Streak/LongestStreak/Completed/StreakBonus/CompletedToday/Play/Retry)

## Quick-Play Modus (Phase 11)

- **Einzelnes zufälliges Level**: Deterministisch via 5-stelligem Seed, Schwierigkeit 1-10 einstellbar
- **Kein Progress**: Keine Sterne, keine Level-Freischaltung, keine Achievements - reiner Spaß-Modus
- **Schwierigkeits-Mapping**: Difficulty 1-10 mappt auf Welt-Schwierigkeit (Gegner, Mechaniken, Layouts, Timer)
- **Timer**: 180s (Diff 1) bis 90s (Diff 10), Block-Dichte 0.325-0.55
- **Gegner-Pools**: Diff 1-2 (Ballom/Onil, 2-3), 3-4 (Onil/Doll/Minvo/Ballom, 3-4), 5-6 (Doll/Minvo/Ovapi/Kondoria, 4-5), 7-8 (+Tanker/Ghost, 5-6), 9-10 (+Splitter/Mimic/Pontan, 6-8)
- **Mechaniken**: Ab Diff 3 (Ice/Conveyor), bis Diff 9-10 (alle inkl. Fog/PlatformGap)
- **Ablauf**: MainMenu → QuickPlayView (Schwierigkeit + Seed) → Game (mode=quick, level=seed, difficulty=X) → GameOver
- **Seed-Sharing**: Spieler können Seeds teilen um das gleiche Level zu spielen
- **QuickPlayViewModel**: Schwierigkeits-Slider (1-10), Seed-Anzeige/Neugenerierung, Play-Button
- **QuickPlayView**: Landscape 2-Spalten Layout (links: Schwierigkeits-Slider mit Welt-Anzeige, rechts: Seed-Karte + großer Play-Button), Gradient-Background, CardElevated-Karten
- **Game-Engine**: `StartQuickPlayModeAsync(seed, difficulty)`, `_isQuickPlayMode` Flag, kein Continue, kein NextLevel, Coins werden vergeben
- **LevelGenerator**: `GenerateQuickPlayLevel(seed, difficulty)` statische Methode
- **RESX-Keys**: 7 Keys (QuickPlayTitle/Difficulty/WorldLevel/Seed/SeedHint/Play/NewSeed)

## Daily Reward & Monetarisierung (Phase 6)

- **7-Tage-Zyklus**: 500/1000/1500/2000/2500/3000/5000 Coins, Tag 5 Extra-Leben
- **Streak-Tracking**: UTC-basiert, Reset bei verpasstem Tag
- **Spieler-Skins**: Default, Gold, Neon, Cyber, Retro (Premium-Only: Gold, Neon, Cyber, Retro)
- **In-App Review**: Nach Level 3-5, 14-Tage Cooldown

## Achievement-System (Phase 7 + Phase 9.3)

- 66 Achievements in 5 Kategorien: Progress (17), Mastery (6), Combat (11), Skill (11), Challenge (1), + 20 neue Cross-Feature Achievements
- JSON-Persistenz via IPreferencesService
- **IAchievementService in GameEngine injiziert** → automatische Achievement-Prüfung bei:
  - Level-Complete → LevelComplete (Welten, NoDamage, Efficient, Speedrun, FirstVictory, NoDamage5/10, Speedrun5/10)
  - Enemy-Kill → OnEnemyKilled (kumulative Kills 100/500/1000/2500/5000)
  - Stars → OnStarsUpdated (50/100/150/200/250/300 Sterne)
  - Combo → OnComboReached (x3, x5, x7)
  - Bomb-Kick → OnBombKicked (25 kumulative Kicks)
  - Power-Bomb → OnPowerBombUsed (10 kumulative)
  - Curse überlebt → OnCurseSurvived (alle 4 Typen, Bit-Flags)
  - Daily Challenge → OnDailyChallengeCompleted (7er Streak, 30 Total)
  - Boss besiegt → OnBossDefeated (boss_slayer, boss_master mit 5 Bit-Flags)
  - Spezial-Bombe → OnSpecialBombUsed (50/100 kumulative)
  - Survival → OnSurvivalComplete (60s/180s/300s)
  - Weekly komplett → OnWeeklyWeekCompleted (10 Wochen)
  - Welt perfektioniert → OnWorldPerfected (Welt 1/5/10, alle 30 Sterne)
  - **Phase 9.3 neue Handler (14 Methoden)**:
  - Dungeon → OnDungeonFloorReached(floor), OnDungeonRunCompleted(), OnDungeonBossDefeated()
  - Battle Pass → OnBattlePassTierReached(tier) - via Lazy-Injection in BattlePassService
  - Karten → OnCardCollected(uniqueCount, maxLevel) - via Lazy-Injection in CardService
  - Liga → OnLeagueTierReached(tierIndex) - via Lazy-Injection in LeagueService
  - Daily Missions → OnDailyMissionCompleted() - via Lazy-Injection in DailyMissionService
  - Survival → OnSurvivalKillsReached(kills)
  - Bomben → OnLineBombUsed(), OnDetonatorUsed()
  - Glücksrad → OnLuckyJackpot() - via LuckySpinViewModel
  - Quick Play → OnQuickPlayMaxCompleted()
  - Sammlung → OnCollectionProgressUpdated(progressPercent) - via CollectionViewModel
- **Lazy-Injection Pattern**: BattlePassService, CardService, LeagueService, DailyMissionService erhalten IAchievementService via `SetAchievementService()` nach ServiceProvider-Build in App.axaml.cs (vermeidet zirkuläre DI)
- **Speedrun-Fix**: Prüft `timeUsed <= 60s` (nicht `timeRemaining >= 60s`)
- **NoDamage-Tracking**: `_playerDamagedThisLevel` Flag in GameEngine
- **CountBits()**: Statische Helper-Methode für Bit-Flag-Zählung (BossTypesDefeated, CurseTypesSurvived)
- AchievementsView mit Karten-Grid (SkiaSharp AchievementIconCanvas + Name + Beschreibung + Fortschritt)
- AchievementData: TotalEnemyKills, TotalStars, TotalBombsKicked, TotalPowerBombs, CurseTypesSurvived, NoDamageLevels, SpeedrunLevels, TotalBossKills, BossTypesDefeated, TotalSpecialBombs, BestSurvivalTime, WeeklyCompletions, **+10 neue Felder (Phase 9.3)**: TotalDailyMissions, BestDungeonFloor, TotalDungeonRuns, HighestBattlePassTier, TotalUniqueCards, HighestLeagueTier, BestSurvivalKills, TotalLineBombs, TotalDetonations, LuckyJackpots
- RESX-Keys fuer 6 Sprachen (131 Keys: AchievementsTitle + 45x Name/Desc + 20x Name/Desc Phase 9.3)

## Audio-System (Phase 8)

- **AndroidSoundService** (`BomberBlast.Android/AndroidSoundService.cs`)
  - SoundPool fuer SFX (12 Sounds: explosion, place_bomb, fuse, powerup, player_death, enemy_death, exit_appear, level_complete, game_over, time_warning, menu_select, menu_confirm)
  - MediaPlayer fuer Musik (4 Tracks: menu, gameplay, boss, victory)
  - Assets in `Assets/Sounds/` (.ogg + .wav, versucht beide Formate)
- **SoundManager** (`Core/SoundManager.cs`): Wraps ISoundService mit Lautstaerke-/Enable-Settings, Crossfade-Logik (Update() Methode, Fade-Out/Fade-In bei Track-Wechsel). `PlayBombExplosion(BombType)`: Spezial-Bomben-Sound-Differenzierung via Layering (Basis-Explosion + sekundärer SFX je nach Bomben-Kategorie: Ice/Gravity/TimeWarp→PowerUp-Layer, Fire/Nova/Vortex→doppelte Explosion, Lightning/Mirror→Fuse-Layer, BlackHole→TimeWarning-Layer)
- **ISoundService.SetMusicVolume(float)**: Für Crossfade-Steuerung (AndroidSoundService: MediaPlayer.SetVolume)
- **SoundServiceFactory** in App.axaml.cs (analog RewardedAdServiceFactory)
- **Sound-Assets**: CC0 Lizenz, Juhani Junkala (OpenGameArt.org), ~8.5 MB gesamt
- **AndroidSoundService Thread-Safety**: Alle MediaPlayer-Operationen per `lock(_musicLock)` synchronisiert. `PrepareAsync()` statt `Prepare()` (blockiert UI-Thread nicht)

## Architektur-Details

### Exit-Mechanik (klassisches Bomberman)
- **PlaceExit()**: Versteckt Exit unter einem Block weit vom Spawn (`Cell.HasHiddenExit = true`)
- **Block-Zerstörung**: Wenn Block mit `HasHiddenExit` gesprengt wird → `CellType.Exit` + Sound + Partikel
- **Fallback**: Wenn alle Gegner tot aber Exit-Block noch intakt → Exit wird automatisch aufgedeckt (via `RevealExit()`)
- **Level-Abschluss**: Spieler muss auf Exit-Zelle stehen UND alle Gegner besiegt haben (inkl. nachträglich gespawnte Pontans)
- **Exit-Feedback**: "DEFEAT ALL!" Floating Text wenn Spieler auf Exit steht aber Gegner leben

### Flamepass-Verhalten
- **Flamepass** schützt NUR vor Explosionen (geprüft in `GameEngine.Collision.cs`)
- **Player.Kill()** prüft NICHT HasFlamepass → Gegner-Kontakt tötet auch mit Flamepass

### Speed-PowerUp (staffelbar)
- **SpeedLevel** 0-3 statt binäres `HasSpeed` (Kompatibilitäts-Property bleibt)
- **Formel**: `BASE_SPEED(80) + SpeedLevel * SPEED_BOOST(20)` → 80/100/120/140
- **Jedes Speed-PowerUp** erhöht SpeedLevel um 1 (max 3)
- **HUD** zeigt "SPD" bei Level 1, "SPD x2"/"SPD x3" bei höheren Levels
- **Verlust bei Tod**: SpeedLevel wird auf 0 zurückgesetzt (non-permanent)

### Combo-System
- **Zeitfenster**: 2 Sekunden zwischen Kills
- **Tracking**: `_comboCount` + `_comboTimer` in GameEngine
- **Bonus-Punkte**: x2→+200, x3→+500, x4→+1000, x5+→+2000
- **Chain-Kill-Bonus**: Bei `_comboCount >= 3` (Kettenreaktion wahrscheinlich) → 1.5x Multiplikator auf Combo-Bonus
- **Visuell**: Farbiger Floating Text (Orange x2-x3, Rot ab x4, "MEGA" ab x5). Chain-Kills: "CHAIN x{N}!" in Gold (#FFC800)

### Slow-Motion
- **Trigger**: Letzter Gegner getötet ODER Combo x4+
- **Dauer**: 0.8s Echtzeit, Faktor 0.3 (30% Geschwindigkeit)
- **Easing**: Ease-Out (langsam → normal)
- **Timer/Combo**: Laufen in Echtzeit (`realDeltaTime`), nicht verlangsamt (kein Exploit)
- **Felder**: `_slowMotionTimer` + `_slowMotionFactor` in GameEngine

### Pontan-Strafe (Timer-Ablauf)
- **Welt-skaliert**: Welt 1: Max 1 Pontan, 8s Intervall, 5s Gnadenfrist. Welt 2: Max 2, 6s, 3s. Welt 3+: Max 3, 5s, sofort
- **Methoden statt Konstanten**: `GetPontanMaxCount()`, `GetPontanSpawnInterval()`, `GetPontanInitialDelay()` in GameEngine.cs
- **Mindestabstand**: 5 Zellen zum Spieler
- **Spawn-Partikel**: Rote Partikel am Spawn-Punkt
- **Vorwarnung**: Pulsierendes rotes "!" 1.5s vor Spawn (PreCalculateNextPontanSpawn → RenderPontanWarning)
- **Timer-Felder**: `_pontanPunishmentActive` + `_pontanSpawned` + `_pontanSpawnTimer` + `_pontanInitialDelay` + `_pontanWarningActive/X/Y`

### Iris-Wipe Transition
- **Level-Start**: Schwarzer Kreis öffnet sich vom Zentrum (SKPath mit CounterClockwise Clip)
- **Level-Complete**: Kreis schließt sich in der letzten Sekunde
- **Goldener Rand-Glow**: Ring am Iris-Rand bei Level-Start

### Explosions-Shockwave
- **Doppelter expandierender Ring**: In ersten 40% der Explosionsdauer
- **Äußerer Ring**: Orange mit MediumGlow, Stroke 6→3px
- **Innerer Ring**: ExplosionCore (hell), 85% Radius, Stroke 3→1.5px
- **Radius**: Wächst von 0 bis Bomben-Range * CELL_SIZE

### Kick-Bomb Mechanik
- **Aktivierung**: Spieler bewegt sich auf Bombe zu → Bombe gleitet in Blickrichtung
- **Voraussetzung**: Player.HasKick (via Kick-PowerUp, ab Level 16)
- **Slide-Physik**: `Bomb.SLIDE_SPEED = 160f`, `UpdateBombSlide()` pro Frame
- **Stopp**: Bei Wand, Block, anderer Bombe oder Gegner → Snap auf Grid-Zellenmitte
- **Grid-Tracking**: Alte Zelle wird freigeräumt, neue Zelle registriert während Slide

### Line-Bomb PowerUp
- **Aktivierung**: Wenn `HasLineBomb` + `ActiveBombs == 0` → `PlaceLineBombs()`
- **Verhalten**: Platziert alle verfügbaren Bomben in Blickrichtung auf leeren Zellen
- **Stopp**: Bei Wand, Block oder existierender Bombe
- **Fallback**: Wenn FacingDirection kein Delta hat → nach unten
- **Verfügbar**: Ab Level 26 (Story)

### Power-Bomb PowerUp
- **Aktivierung**: Wenn `HasPowerBomb` + `ActiveBombs == 0` → `PlacePowerBomb()`
- **Reichweite**: `FireRange + MaxBombs - 1` (skaliert mit Upgrades)
- **Slot-Verbrauch**: Belegt ALLE Bomb-Slots (verhindert Spam)
- **Verfügbar**: Ab Level 36 (Story)

### Skull/Curse System
- **Skull-PowerUp**: Negatives PowerUp, aktiviert zufälligen Curse für 10 Sekunden
- **4 Curse-Typen**:
  - `Diarrhea`: Automatische Bombenplatzierung alle 0.5s
  - `Slow`: Bewegungsgeschwindigkeit halbiert
  - `Constipation`: Kann keine Bomben platzieren
  - `ReverseControls`: Richtungseingaben invertiert
- **Visuell**: Pulsierender violetter Glow, HUD-Anzeige mit Timer und Typ-Abkürzung
- **Verfügbar**: Ab Level 20 (Story)

### Danger Telegraphing
- **Bedingung**: Nicht-manuelle Bomben mit Zündschnur < 0.8s
- **Darstellung**: Rote pulsierende Overlay-Zellen im Explosionsradius (4 Richtungen)
- **Intensität**: Steigt mit sinkender Zündzeit, Puls-Effekt (sin-basiert)
- **Berechnung**: Read-only Spread im Renderer (keine State-Mutation)

### Boss-System

- **BossEnemy** (`Models/Entities/BossEnemy.cs`): Erbt von Enemy, Multi-Cell (2x2 oder 3x3), eigene HP (3-8), Spezial-Angriffe mit Telegraph+Cooldown
- **5 Boss-Typen**: StoneGolem (Blockregen), IceDragon (Eisatem/Reihe einfrieren), FireDemon (Lava-Welle/halber Boden), ShadowMaster (Teleport), FinalBoss (rotiert alle 4)
- **Boss-Kollision** (GameEngine.Collision.cs): `OccupiesCell()` statt einzelner GridX/GridY. Boss-HP-Anzeige bei Treffer ("HIT! 3/5"). Enrage-Warnung bei 50% HP
- **Boss-Spezial-Angriffe** (GameEngine.cs): `UpdateBossAttacks()` nach UpdateEnemies. Telegraph (2s Warnung mit gelben Warnzellen) → Angriff (1.5s Effekt). Cooldown 12-18s (Enrage: 60%)
- **Boss-Bewegung** (GameEngine.Level.cs): `UpdateBossAI()` + `MoveBoss()` statt normaler EnemyAI/Move. Vereinfachte Richtungswahl (70% zum Spieler, 30% zufällig). Steht still während Telegraph/Angriff
- **Boss-Angriffs-Schaden**: Spieler auf AttackTargetCell während IsAttacking → Kill (Shield absorbiert)
- **Boss-Tod**: Extra Celebration (Punkte 10.000-50.000, Gold-Partikel, Shockwave, LEVEL_COMPLETE Sound)
- **Boss-Spawn**: In `SpawnEnemies()` via `Level.BossKind`. Arena-Mitte, Blöcke werden geräumt

### Spezial-Bomben (Phase 9)

- **3 Spezial-Bomben-Typen**: Ice (friert Gegner 3s ein), Fire (+2 Reichweite + Lava-Nachwirkung 3s), Sticky (klebt an Wänden, 5s Timer)
- **BombType Enum** (`Models/Entities/Bomb.cs`): Normal, Ice, Fire, Sticky. `Bomb.Type` Property steuert Rendering und Effekte
- **Cell-Effekte** (`Models/Grid/Cell.cs`): `IsFrozen`/`FreezeTimer` (Eis-Bombe), `IsLavaActive`/`LavaTimer` (Feuer-Bombe)
- **Typ-spezifisches Bomben-Rendering** (GameRenderer.cs): Farbe von Body/Glow/Fuse/Spark ändert sich pro Typ (Ice=Hellblau, Fire=Dunkelrot, Sticky=Grün). Spezial-Partikel: Ice=aufsteigende Frost-Punkte+Eis-Ring, Fire=Flammen-Partikel+Glow-Ring, Sticky=Schleim-Tropfen+Fäden
- **Eingefrorene Zellen-Rendering**: Hellblauer halbtransparenter Overlay, Eis-Kristall-Linien (3 Diagonalen), pulsierender Shimmer-Punkt. Intensität blendet in letzten 0.5s aus
- **Lava-Zellen-Rendering**: Rot-oranger Overlay mit pulsierendem Glow (sin-basiert), innerer heller Kern, 3 Lava-Blasen mit Wachsen/Aufsteigen/Platzen-Zyklus
- **Eingefrorene Gegner-Rendering**: Halbtransparenter blauer Overlay über Gegner-Sprite, weißer Frost-Rand, Eis-Kristalle. Kein Walk-Wobble auf eingefrorenen Zellen. Funktioniert auch für Bosse

### Karten-/Deck-System (Phase 2 Feature-Expansion)
- **14 Bomben-Karten**: Jeder BombType ist eine sammelbare Karte mit Raritaet + Level (1-3: Bronze/Silber/Gold)
- **CardCatalog** (`Models/Cards/CardCatalog.cs`): Statisches All-Array mit 14 Karten-Definitionen (BombType, NameKey, DescKey, Rarity, UsesPerLevel)
- **OwnedCard** (`Models/Cards/OwnedCard.cs`): Count (Duplikate), Level, BombType
- **BombCard** (`Models/Cards/BombCard.cs`): BombType, Rarity, Level, UsesPerLevel, UpgradeCost (UpgradeDuplicatesRequired + UpgradeCoinCost)
- **ICardService/CardService** (`Services/`): OwnedCards Dictionary, EquippedSlots (int[4+1]), AddCard, UpgradeCard, EquipCard, UnequipSlot, GetDropForLevel (gewichteter Random: 60% Common, 25% Rare, 12% Epic, 3% Legendary), TryUnlockSlot5(IGemService), IsSlot5Unlocked
- **Persistenz**: IPreferencesService JSON, Key "CardCollection", CardCollectionData mit Slot5Unlocked Flag
- **Deck**: 4 Basis-Slots + 1 freischaltbarer Slot (20 Gems), ActiveCardSlot im Gameplay per HUD-Tap wechselbar
- **HUD-Integration**: Max 5 Mini-Karten-Slots rechts im HUD (unter bestehendem Side-Panel), aktiver Slot hervorgehoben, verbleibende Uses als Zahl
- **Input**: Tap auf HUD-Slot wechselt ActiveCardSlot, Slot -1 = Normal-Bombe
- **GameEngine**: PlaceBomb() liest aus aktivem Card-Slot (Player.EquippedCards + ActiveCardSlot), Karten-Drop bei Level-Complete via ICardService.GetDropForLevel()
- **Karten-Upgrade**: Duplikate + Coins → nächste Stufe (Common: 3+500/5+2000, Rare: 3+1500/5+5000, Epic: 2+3000/4+10000, Legendary: 2+5000/3+20000)
- **DeckViewModel** (`ViewModels/DeckViewModel.cs`): Zeigt ALLE 13 Karten (besessene + nicht-besessene als gesperrt), sortiert Legendary→Common. BombType→MaterialIcon-Mapping (Ice=Snowflake, Fire=Fire, Sticky=Water, Smoke=WeatherFog, Lightning=LightningBolt, Gravity=Magnet, Poison=Skull, TimeWarp=ClockFast, Mirror=FlipHorizontal, Vortex=Tornado, Phantom=Ghost, Nova=Flare, BlackHole=CircleSlice8). Detail-Panel: Stärke-Multiplikator (1.0x/1.2x/1.4x), Upgrade-Fortschrittsbalken, Drop-Quellen je Rarität (Level/Boss/Dungeon mit Prozent). CardDisplayItem + DeckSlotItem mit IconName, LevelColorHex (Bronze/Silber/Gold), CardOpacity, ShowUpgradeBadge, ShowEquippedBadge
- **DeckView** (`Views/DeckView.axaml`): Landscape 2-Spalten (Karten-Grid links als WrapPanel 100x120px Karten mit Bomben-Icon + Raritäts-Border + Level-Farbe + Badges, Deck-Slots + Detail-Panel rechts 260px). Fix: `#Deck_Root.DataContext.Command` statt `$parent[ItemsControl]` ReflectionBinding (Crash-Fix). Sammlungs-Fortschrittsbalken im Header. Detail zeigt: Icon + Name + Rarität-Badge + Beschreibung + Level/Stärke + Einsätze + Upgrade-Fortschritt + 5 Equip-Buttons (5. nur bei freigeschaltetem Slot) + Fundorte. Nicht-besessene Karten: Locked-Info mit Beschreibung + Fundorte
- **MainMenu**: Deck-Button (CardsPlaying Icon, blau #1565C0)
- **Navigation**: MainMenu → Deck → ".." (zurück), DeckVm.FloatingTextRequested/CelebrationRequested
- **RESX-Keys**: 45 Keys in 6 Sprachen (37 original + 8 neue: DeckLabel, CardStrength, CardDropSource, CardUpgradeLabel, CardNotOwned, DropSourceLevel, DropSourceBoss, DropSourceDungeon)

### Dungeon Run / Roguelike-Modus (Phase 4 Feature-Expansion + Dungeon-Erweiterung)
- **Roguelike-Modus**: 1 Leben, steigende Schwierigkeit Floor für Floor, Buff-Auswahl, Boss alle 5 Floors
- **Run-Ablauf**: Floor 1-4 normal, Floor 5 Mini-Boss, Floor 6-9 härter, Floor 10 End-Boss mit Truhe, ab Floor 11 +50% Skalierung
- **Eintritt**: 1x/Tag gratis, 500 Coins, 10 Gems, oder Rewarded Ad (1x/Tag)
- **16 Dungeon-Buffs**: 5 Common (ExtraBomb, ExtraFire, SpeedBoost, CoinBonus, BombTimer), 5 Rare (Shield, ReloadSpecialBombs, EnemySlow, BlastRadius, PowerUpMagnet), 2 Epic (ExtraLife, FireImmunity), **4 Legendary** (Berserker +2B/+2F/-1Life, TimeFreeze 3s-Freeze, GoldRush 3x-Coins, Phantom Durch-Wände 5s/30s-CD)
- **Buff-Auswahl**: Nach Floor 2/4/5/7/9 - 3 zufällige Buffs gewichtet per Rarität (Common W10, Rare W6, Epic W3, Legendary W3-4), seeded Random (RunSeed + Floor*100)
- **Buff-Reroll**: 1x/Run kostenlos, weitere für 5 Gems (`DungeonRunState.FreeRerollsUsed`)
- **5 Synergies**: Bombardier (ExtraBomb+ExtraFire→+1 beides), Blitzkrieg (Speed+BombTimer→-0.5s Timer), Festung (Shield+ExtraLife→Shield-Regen 20s), Midas (CoinBonus+GoldRush→Gegner droppen Coins), Elementar (EnemySlow+FireImmunity→Lava verlangsamt Gegner)
- **Belohnungen**: Floor 1-4 (200-500 Coins + 10-30 DungeonCoins, 30-45% Karten-Drop), Floor 5 Boss (800 Coins + 50 DC, 5 Gems, 100% Rare), Floor 6-9 (600-1000 + 10-30 DC, 50-70%), Floor 10 Boss (2000+3000 Truhe + 100 DC, 15 Gems, 100% Epic)
- **5 Raum-Typen** (`Models/Dungeon/DungeonRoomType.cs`): Normal (W40), Elite (+50% Belohnungen, W20), Treasure (wenig Gegner, viele PowerUps, W15), Challenge (Spezial-Bedingung, W15), Rest (Kein Kampf, Heilung+Buff, W10 max 1/5 Floors)
- **8 Floor-Modifikatoren** (`Models/Dungeon/DungeonFloorModifier.cs`): Ab Floor 3, 30% Chance (LavaBorders, Darkness, DoubleSpawns, FastBombs, BigExplosions, CursedFloor, Regeneration, Wealthy)
- **Node-Map** (Slay the Spire-inspiriert): 10 Reihen × 2-3 Nodes, Spieler wählt Pfad, Floor 5+10 = Boss (1 Node). `DungeonMapNode` Model, `DungeonMapRenderer` (SkiaSharp), `DungeonService.GenerateMap(seed)`
- **8 Permanente Upgrades** (`IDungeonUpgradeService`): Mit DungeonCoins gekauft (50-300 DC). StartingBombs/Fire/Speed, ExtraBuffChoice (4 statt 3), BossGoldBonus (+25/50%), StartingShield, CardDropBoost (+15/30%), ReviveCostReduction (10 statt 15 Gems)
- **Ascension-System** (6 Stufen 0-5): Nach Floor 10 Clear → nächste Stufe. Stufe 1: +20% Gegner, +25% Coins. Stufe 5 Nightmare: Alles kombiniert, +150% Coins, exklusive Krone
- **DungeonRunState** (`Models/Dungeon/DungeonRunState.cs`): CurrentFloor, Lives, ActiveBuffs, CollectedCoins/Gems/CardDrops, IsActive, RunSeed, LastFreeRunDate/LastAdRunDate, CurrentRoomType, CurrentModifier, MapData, FreeRerollsUsed
- **DungeonStats**: TotalRuns, BestFloor, TotalCoinsEarned/GemsEarned/CardsEarned, DungeonCoins, AscensionLevel, HighestAscension
- **DungeonBuffCatalog** (`Models/Dungeon/DungeonBuff.cs`): 16 Buff-Definitionen mit Type, NameKey, DescKey, IconName, Rarity, Weight
- **IDungeonService/DungeonService** (`Services/`): StartRun, CompleteFloor, GenerateBuffChoices, ApplyBuff, EndRun, RerollBuffs, GenerateRoomType, GenerateFloorModifier, GenerateMap, SelectNode, GetActiveSynergies, JSON-Persistenz (Keys: "DungeonRunData", "DungeonStatsData", "DungeonUpgradeData")
- **IDungeonUpgradeService/DungeonUpgradeService** (`Services/`): GetAll, GetLevel, CanBuy, TryBuy, GetEffectValue - 8 permanente Upgrades, JSON-Persistenz (Key: "DungeonUpgradeData")
- **GameEngine**: `_isDungeonRun` Flag, `StartDungeonFloorAsync(floor, seed)`, `ApplyDungeonBuffs()` (inkl. permanente Upgrades), `ApplyDungeonFloorModifier()`, `_phantomWalkActive` Flag + Timer, Dungeon-Events (OnDungeonFloorComplete, OnDungeonBuffSelection, OnDungeonRunEnd). Übergibt `IsDungeonRun` + `DungeonActiveBuffs` an GameRenderer vor jedem Frame
- **HUD**: Dungeon-Buffs als farbige Mini-Icons (20x20px), Synergy-Badge (goldener Rahmen), Modifikator-Badge (farbig), Phantom-CD-Anzeige, Raum-Typ-Indikator
- **LevelGenerator**: `GenerateDungeonFloor(floor, seed)` mit Chunk-Templates, RoomType-Anpassungen (Elite: +2 Gegner, Treasure: 6-8 PowerUps, Challenge: SpeedRun/NoPowerUps/DoubleEnemies, Rest: leer)
- **DungeonViewModel** (`ViewModels/DungeonViewModel.cs`): 3 States (PreRun/BuffSelection/PostRun), Commands (Start/Select/Buff/Reroll/BuyUpgrade/SelectNode), MapNodes + UpgradeItems Collections, ActiveSynergies, AscensionLevel
- **DungeonView** (`Views/DungeonView.axaml`): Landscape 2-Spalten (Stats+Upgrades+Map links, Buff-Karten/Zusammenfassung rechts), Buff-Karten mit gestaffelter Einblend-Animation + Rarität-Glow
- **MainMenu**: Dungeon-Button (Sword Icon, dunkelviolett #4A148C, Highlight #E040FB)
- **Navigation**: MainMenu → Dungeon → Map-Node wählen → Game?mode=dungeon&floor=X&seed=Y → Buff-Auswahl/Nächster Floor → GameOver
- **RESX-Keys**: 96 Keys in 6 Sprachen (43 original + 53 neue: 17 Upgrades, 8 Legendary Buffs, 4 Room Types, 7 Modifiers, 12 Synergies+Reroll, 2 Ascension, 1 DungeonCoins)

### Sammlungs-Album (Phase 6 Feature-Expansion)
- **Enzyklopädie**: Alle 12 Gegner, 5 Bosse, 12 PowerUps, 14 Bomben-Karten, Kosmetik - als sammelbare Einträge
- **5 Kategorien**: Enemies (12 Typen), Bosses (5 Typen), PowerUps (12 Typen), Cards (14 via ICardService), Cosmetics (via ICustomizationService)
- **Tracking**: TimesEncountered/TimesDefeated pro Gegner+Boss, TimesCollected pro PowerUp - automatisch via GameEngine-Hooks
- **Meilenstein-Belohnungen**: 25% = 2.000 Coins, 50% = 5.000C + 10 Gems, 75% = 10.000C + 20 Gems, 100% = 25.000C + 50 Gems
- **CollectionEntry** (`Models/Collection/CollectionEntry.cs`): Id, Category, NameKey, LoreKey, IsDiscovered, Stats Dictionary
- **ICollectionService/CollectionService** (`Services/`): BuildAllEntries() aggregiert aus Card/Customization + eigenes Encounter-Tracking, JSON-Persistenz (Key: "CollectionData")
- **Verdeckte Einträge**: Nicht-entdeckte Items zeigen "???" statt Name, Lore-Text gesperrt
- **GameEngine-Hooks**: RecordEnemyEncountered/RecordEnemyDefeated in Collision.cs (Enemy-Kill + Sichtung), RecordPowerUpCollected in Collision.cs, RecordBossDefeated in Level.cs
- **CollectionViewModel** (`ViewModels/CollectionViewModel.cs`): CategoryItems (5 Kategorien), SelectedEntry, Fortschritts-Berechnung, Milestone-Claiming
- **CollectionView** (`Views/CollectionView.axaml`): Landscape 2-Spalten (Kategorie-Tabs + Grid links, Detail + Lore rechts)
- **Navigation**: MainMenu → Collection → ".." (zurück), CollectionVm.FloatingTextRequested/CelebrationRequested
- **RESX-Keys**: 66 Keys in 6 Sprachen (CollectionTitle, 12 EnemyName/Lore, 5 BossLore, 12 PowerUpName/Lore, UI-Keys)

### Liga-System (Phase 7 Feature-Expansion + Firebase Online-Rangliste)
- **Online-Rangliste via Firebase**: Local-First mit Firebase Realtime Database Sync im Hintergrund
- **5 Ligen**: Bronze→Silber→Gold→Platin→Diamant, 14-Tage-Saisons (deterministisch, Epoche 24.02.2026 UTC)
- **Firebase REST API**: Anonymous Auth + Realtime Database, plattformübergreifend via HttpClient (kein natives SDK)
- **Firebase-Pfad**: `league/s{saison}/{tier}/{uid}` (z.B. `league/s1/bronze/abc123`)
- **Spieler-Identität**: Firebase Anonymous Auth, Anzeigename "Bomber_" + letzte 4 Zeichen der UID (anpassbar via SetPlayerName)
- **NPC-Backfill**: Bei weniger als 20 echten Spielern werden NPCs generiert (Seeded Random, konsistent pro Saison+Tier)
- **Debounced Push**: Score-Änderungen werden 3s nach letzter Änderung zu Firebase gepusht (CancellationTokenSource)
- **Deterministische Saisons**: Alle Clients berechnen gleiche Saison-Nr aus Epoche (`(UtcNow - Epoch).TotalDays / 14 + 1`)
- **Aufstieg/Abstieg**: Top 30% steigen auf, Bottom 20% steigen ab (Bronze kein Abstieg, Diamant kein Aufstieg)
- **Punkte-Quellen**: Level-Complete (10 + Level/10), Boss-Kill (+20 in Level.cs, +25 in Collision.cs), Daily Challenge, Missions
- **Saison-Belohnungen**: Bronze 2.000C/10G, Silber 5.000C/20G, Gold 10.000C/35G, Platin 18.000C/50G, Diamant 30.000C/75G
- **Online-Status**: Grünes CloudCheck-Icon (online) / Rotes CloudOffOutline-Icon (offline) im Header
- **Echte Spieler**: Cyan (#00CED1) mit Punkt-Indikator, eigener Spieler in Gold (#FFD700), NPCs in Weiß
- **Refresh-Button**: Manuelles Aktualisieren der Rangliste aus Firebase
- **Lade-Indikator**: "Lade Rangliste..." mit CloudSync-Icon während Firebase-Abfrage
- **IFirebaseService/FirebaseService** (`Services/`): Anonymous Auth, GET/SET/UPDATE/PUSH/DELETE, Token-Refresh, 5s Timeout
- **Firebase-Models** (`Models/Firebase/`): FirebaseAuthResponse, FirebaseTokenResponse, FirebaseLeagueEntry
- **LeagueTier** (`Models/League/LeagueTier.cs`): Enum + Extensions (GetPromotionThreshold, GetColor, GetGlowColor, GetIconName, GetNameKey, GetSeasonReward, GetPromotionPercent, GetRelegationPercent)
- **LeagueData** (`Models/League/LeagueData.cs`): CurrentTier, Points, SeasonNumber, SeasonRewardClaimed + LeagueStats (TotalSeasons, HighestTier, TotalPointsEarned, TotalPromotions, BestSeasonPoints)
- **ILeagueService/LeagueService** (`Services/`): AddPoints (lokal + debounced Firebase-Push), GetLeaderboard (Firebase-Cache + NPC-Backfill), RefreshLeaderboardAsync, InitializeOnlineAsync, SetPlayerName, JSON-Persistenz (Keys: "LeagueData", "LeagueStatsData", "LeaguePlayerName")
- **LeagueViewModel** (`ViewModels/LeagueViewModel.cs`): TierName/Icon/Color, PointsText, RankText, SeasonCountdown, IsOnline, IsLoading, PlayerName, LeaderboardEntries, ClaimReward + RefreshLeaderboard + GoBack Commands
- **LeagueView** (`Views/LeagueView.axaml`): Landscape 2-Spalten (Liga-Info + Countdown + Belohnung + Stats links, Rangliste mit Refresh + Online-Status rechts)
- **DI**: IFirebaseService (Singleton) + ILeagueService (Singleton, nimmt IFirebaseService per Constructor)
- **MainMenu**: League-Button (Trophy Icon, #00CED1)
- **RESX-Keys**: 25 Keys in 6 Sprachen (LeagueTitle/Button, 5 Tier-Namen, Punkte/Rang/Countdown/Belohnungen/Stats)
- **Firebase-Projekt**: bomberblast-league (europe-west1), Credentials in FirebaseService.cs konfiguriert

### Cloud Save (Phase 8 Feature-Expansion)
- **Local-First**: Spiel funktioniert immer offline, Cloud ist optional
- **Sync-Strategie**: Pull bei App-Start, Push nach wichtigen Aktionen (Debounce 5s)
- **35 Persistenz-Keys**: Alle Services (Progress, Coins, Gems, Cards, Achievements, Customization, Liga, Dungeon, BattlePass, Collection etc.)
- **Konflikt-Resolution**: CloudSaveData.ChooseBest() - TotalStars → CoinBalance+GemBalance*100 → TotalCards → Timestamp (neuer gewinnt)
- **CloudSaveData** (`Models/CloudSaveData.cs`): Version, TimestampUtc, TotalStars, CoinBalance, GemBalance, TotalCards, Keys Dictionary<string,string>
- **ICloudSaveService/CloudSaveService** (`Services/`): TryLoadFromCloudAsync, SchedulePushAsync (Debounce 5s), ForceUploadAsync, ForceDownloadAsync, SetEnabled
- **NullCloudSaveService** (`Services/`): Desktop-Stub (no-op)
- **IPlayGamesService erweitert**: +SaveToCloudAsync(string), +LoadCloudSaveAsync()
- **AndroidPlayGamesService**: Lokale Datei-Persistenz als Zwischenspeicher (cloud_save/save_v1.json), TODO: Snapshots API via Java-Interop
- **SettingsView**: Cloud Save Sektion (Toggle, Status-Text, Sync/Download-Buttons)
- **MainViewModel**: TryLoadFromCloudAsync() bei App-Start (fire-and-forget)
- **App.axaml.cs**: CloudSaveServiceFactory Pattern (analog RewardedAdServiceFactory)
- **RESX-Keys**: 12 Keys in 6 Sprachen (CloudSaveTitle/Toggle/Status/Sync/Error)

### Battle Pass (Phase 9 Feature-Expansion)
- **IBattlePassService/BattlePassService** (`Services/`): 30-Tier Saison (30 Tage), XP-basierter Fortschritt, Free/Premium-Track, Belohnungen (Coins/Gems/CardPacks/Cosmetics)
- **BattlePassData** (`Models/BattlePass/`): SeasonNumber, CurrentTier, CurrentXp, ClaimedFreeTiers/PremiumTiers, IsPremium, SeasonStartDate
- **BattlePassTierDefinitions** (`Models/BattlePass/`): 30 Tiers mit Free+Premium Rewards, XP-Kurve pro Tier
- **BattlePassViewModel** (`ViewModels/`): Tier-Liste, XP-Fortschritt, Reward-Claims (Free/Premium), Premium-Kauf via Event
- **BattlePassView** (`Views/`): Tier-Liste mit Free/Premium-Track, XP-Balken, Saison-Countdown
- **BattlePassXpSources** (13 Quellen):
  - StoryLevelComplete (100), ThreeStarsFirstTime (50)
  - DailyChallenge (200), DailyMission (80), DailyMissionBonus (150)
  - WeeklyMission (120), WeeklyBonus (300), DungeonFloor (50), DungeonBoss (100)
  - BossKill (200), Survival60s (100), DailyLogin (50)
  - LuckySpin (30), CollectionMilestone25-100 (100-500), CardUpgrade (80), LeagueSeasonReward (150)
- **Cross-Feature XP-Hooks** (Phase 9.2):
  - GameEngine.Level.cs: level_complete, boss_kill, three_stars, daily_challenge, dungeon_floor, dungeon_boss
  - GameEngine.Collision.cs: boss_kill
  - GameEngine.cs: survival_60s
  - WeeklyChallengeService: weekly_mission, weekly_all_complete
  - DailyMissionService: daily_mission, daily_all_complete
  - MainMenuViewModel: daily_login (+ Liga 5 Punkte)
  - LuckySpinViewModel: lucky_spin
  - CollectionViewModel: collection_milestone (+ Liga Punkte skaliert nach %)
  - DeckViewModel: card_upgrade
  - LeagueViewModel: league_season_reward

### Animierter Menü-Hintergrund (MenuBackgroundCanvas) mit Theme-System
- **MenuBackgroundRenderer** (`Graphics/MenuBackgroundRenderer.cs`): Theme-basierter Renderer mit `BackgroundTheme` Enum (7 Themes)
  - `Initialize(int seed, BackgroundTheme theme)` + `Render(canvas, w, h, time)`
  - Struct-basierte Partikel pro Theme (max 60), gepoolte SKPaint (max 8/Theme), <2ms Renderzeit bei 30fps
  - **Default** (MainMenu, Settings, Help, HighScores etc.): Bomben-Silhouetten (8) + Funken (25) + Flammen-Wisps (6), Gradient #1A1A2E→#16213E
  - **Dungeon**: Violett/Dunkel (#1A0A2E→#0D0D1A), 4 Fackeln (orange Flacker) + 6 Fledermäuse (Sinus-Flug) + 8 fallende Steinbrocken
  - **Shop**: Gold-Gradient (#1A1A00→#2D2D00), 12 schwebende Münzen (rotierend) + 15 Shimmer-Punkte (Gold-Glitter) + 3 Gem-Silhouetten (Cyan)
  - **League**: Cyan/Teal (#0A1A2E→#0D2D3A), 6 Trophäen-Silhouetten (aufsteigend) + 20 Sterne-Funken + 4 Podest-Lichtstreifen
  - **BattlePass**: Lila/Orange (#1A0A2E→#2E1A0A), 15 XP-Orbs (aufsteigend) + 8 Streifen-Fragmente (diagonal) + 6 Tier-Badge-Silhouetten
  - **Victory**: Gold-Explosion (#2E1A00→#1A0A00), 30 Confetti (6 Farben, fallend+drehend) + 8 Firework-Bursts (zyklisch) + 12 Gold-Funken
  - **LuckySpin**: Regenbogen (#0A0A2E→#1A0A2E), 20 Regenbogen-Punkte (Orbit-Rotation) + 10 Glitzer-Sterne (Blink) + 4 Rad-Lichtstreifen
- **MenuBackgroundCanvas** (`Controls/MenuBackgroundCanvas.cs`): Wiederverwendbares UserControl (SKCanvasView + DispatcherTimer ~30fps)
  - `BackgroundTheme` StyledProperty → View setzt Theme in XAML: `<controls:MenuBackgroundCanvas Theme="Dungeon" />`
  - Auto-Start/Stop via AttachedToVisualTree/DetachedFromVisualTree
  - IsHitTestVisible=false (reiner Hintergrund), `canvas.LocalClipBounds` für DPI-korrekte Bounds
- **15 Views mit thematischem Hintergrund**:
  - Default: MainMenuView, CollectionView, DeckView, DailyChallengeView, WeeklyChallengeView, StatisticsView, HighScoresView, QuickPlayView, ProfileView
  - Dungeon: DungeonView
  - Shop: ShopView
  - League: LeagueView
  - BattlePass: BattlePassView
  - Victory: VictoryView (ersetzt XAML-Gradient)
  - LuckySpin: LuckySpinView

### Profil-Seite
- **ProfileViewModel** (`ViewModels/ProfileViewModel.cs`): 7 injizierte Services (ILeagueService, ICustomizationService, IProgressService, ICoinService, IGemService, IAchievementService, ILocalizationService)
  - PlayerName editierbar (max 16 Zeichen, LeagueService.SetPlayerName)
  - Aktiver Skin (Name, Farbe aus SkinDefinition.PrimaryColor) + Frame
  - Stats: Sterne, Coins, Gems, Liga-Tier mit Farbe, Achievement-Prozent
  - Commands: GoBack, SaveName
- **ProfileView** (`Views/ProfileView.axaml`): Landscape 2-Spalten (links 280px Spieler-Karte mit Avatar/Name/Save, rechts 2x3 Stats-Grid)
  - MenuBackgroundCanvas als Hintergrund
- **Navigation**: MainMenu → Profile → ".." (zurück), Statistik-Button durch Profil-Button ersetzt (AccountCircle Icon)
- **MainViewModel**: ProfileVm Property, IsProfileActive, case "Profile" in NavigateTo, HandleBackPressed
- **MainView**: ProfileBorder mit CSS-Klassen-Transition
- **RESX-Keys**: 12 Keys in 6 Sprachen (ProfileName/Hint/Save/Saved/Stars/Coins/Gems/League/Achievements/Skin/Frame/NoFrame)
- **DI**: ProfileViewModel als Singleton registriert (22 VMs total)

### Starter Pack (Feature 65)
- **Einmaliges Angebot**: 5000 Coins + 20 Gems + 3 Rare-Karten, verfügbar ab Level 5
- **IStarterPackService/StarterPackService** (`Services/`): IsAvailable, IsAlreadyPurchased, CheckEligibility, MarkAsPurchased
- **Persistenz**: IPreferencesService JSON, Key "StarterPackData"
- **Kauf**: Coin-basiert (4999 Coins) als Fallback, in MainMenuViewModel integriert
- **MainMenu**: `IsStarterPackAvailable` Property, `BuyStarterPackCommand`
- **RESX-Keys**: 3 Keys in 6 Sprachen (StarterPackTitle/Desc/Purchased)

### Comeback-Mechanik (Feature 68)
- **>3 Tage inaktiv → 2000 Coins + 5 Gems Bonus**
- **IDailyRewardService erweitert**: `CheckComebackBonus()` + `UpdateLastActivity()`
- **Tracking**: "LastActivityDate" in DailyRewardData, "ComebackClaimed" Flag
- **MainMenuViewModel**: Prüft Comeback in OnAppearing(), vergibt Belohnung mit FloatingText + Celebration
- **RESX-Keys**: 2 Keys in 6 Sprachen (ComebackTitle/ComebackBonus)

### Battle Pass XP-Boost (Feature 69)
- **2x XP für 20 Gems, 24h Dauer**
- **IBattlePassService erweitert**: `IsXpBoostActive`, `XpBoostExpiresAt`, `ActivateXpBoost()`
- **BattlePassData**: Neues Feld `XpBoostExpiresAt` (ISO 8601 UTC)
- **BattlePassService.AddXp()**: `if (IsXpBoostActive) amount *= 2`
- **BattlePassViewModel**: `ActivateXpBoostCommand`, `IsXpBoostActive`, `XpBoostTimeText`, `XpBoostButtonText`
- **RESX-Keys**: 4 Keys in 6 Sprachen (XpBoostTitle/Active/Price + ShopNotEnoughGems)

### Rotating Deals (Feature 66)
- **3 tägliche + 1 wöchentliches Angebot**: 20-50% Rabatt, Seeded Random per DayId/ISO-Kalenderwoche
- **RotatingDeal Model** (`Models/RotatingDeal.cs`): Id, TitleKey, OriginalPrice, DiscountedPrice, DiscountPercent, Currency, RewardType, RewardAmount, IsClaimed, DescriptionKey
- **4 Daily-Deal-Typen**: CoinPack (200-1000C, 20-40% Rabatt), GemPack (5-15 Gems), CardPack (1-3 Karten), UpgradeDiscount (500-3000C)
- **4 Weekly-Deal-Typen**: MegaCoinPack (2000-5000C, 30-50% Rabatt), MegaGemPack (20-50 Gems), RareCardBundle (2-4 Karten), PremiumBundle (1500C + 10 Gems)
- **IRotatingDealsService/RotatingDealsService** (`Services/`): GetTodaysDeals(), GetWeeklyDeal(), ClaimDeal(), JSON-Persistenz (Key: "RotatingDealsData"), ClaimedDealIds Cleanup
- **ShopViewModel**: DailyDeals + WeeklyDeal ObservableProperties, BuyDealCommand, RefreshDailyDeals()
- **RESX-Keys**: 12 Deal-Keys in 6 Sprachen (DailyDealsTitle/WeeklyDealTitle/DealDiscount/DealClaimed + 8 Deal-Typ-Namen)

### Gem-Only Cosmetics (Feature 67)
- **3 exklusive Gem-Skins**: Crystal (50 Gems, Epic, #00BCD4), Shadow (100 Gems, Legendary, #4A148C), Phoenix (200 Gems, Legendary, #FF6D00)
- **SkinDefinition erweitert**: Neues `GemPrice` Property (int, default 0), 3 neue Skins in PlayerSkins.All mit GlowColor
- **ICustomizationService erweitert**: `TryPurchasePlayerSkinWithGems(string skinId)`
- **CustomizationService**: `_gemService` Feld + `SetGemService()` Lazy-Injection, Gem-Kauf via TrySpendGems
- **ShopViewModel**: GemSkinItems Sektion, BuyGemSkinCommand, SelectGemSkinCommand, RefreshGemSkinItems()
- **RESX-Keys**: 4 Keys in 6 Sprachen (GemSkinsTitle, SkinCrystal, SkinShadow, SkinPhoenix)

### Extended Gem-Sinks (Feature 70)
- **Karten für Gems kaufen** (DeckViewModel): Rare 15 Gems, Epic 30 Gems, Legendary 75 Gems. BuyCardForGemsCommand, GetGemPriceForRarity(), UpdateGemBuyState()
- **Extra Spin für Gems** (LuckySpinViewModel): Bereits implementiert als BuySpinWithGems (GEM_SPIN_COST=3)
- **Dungeon-Wiederbelebung für Gems** (DungeonViewModel): 15 Gems, ReviveForGemsCommand, setzt Lives=1 bei Tod. OnDungeonPlayerDied(), CanReviveForGems Property
- **5. Deck-Slot für 20 Gems** (DeckViewModel+CardService): CardCatalog.MaxDeckSlots=5, DefaultDeckSlots=4, Slot5UnlockCost=20. ICardService.IsSlot5Unlocked + TryUnlockSlot5(IGemService). CardCollectionData.Slot5Unlocked Flag (JSON-persistiert). DeckViewModel: UnlockSlot5Command, IsSlot5Unlocked/CanUnlockSlot5/UnlockSlot5Text Properties. DeckView: Unlock-Button unter Deck-Slots (Cyan #00BCD4), 5. Equip-Button im Detail. HUD MAX_CARD_SLOTS auf 5 erhöht. Gem-Badge im Deck-Header
- **Rewarded Ad Cooldown 60s** (RewardedAdCooldownTracker): Statische Klasse für globalen 60s Cooldown zwischen Rewarded Ads. Integriert in alle 12 Ad-Placements über 8 ViewModels (GameOver, Game, Victory, LevelSelect, LuckySpin, Dungeon, Main, Shop). CanShowAd check vor Button-Aktivierung, RecordAdShown() nach erfolgreicher Ad
- **RESX-Keys**: 7 Keys in 6 Sprachen (BuyCardGems, ExtraSpinGems, DungeonReviveGems, DungeonRevived, InsufficientGems, DeckUnlockSlot5, DeckSlot5Unlocked)

## AAA Visual Redesign (alle 21 Content-Views)

Alle UI-Views wurden auf AAA-Game-Studio-Niveau aufgewertet mit konsistenten Design-Patterns:

### Angewandte Design-Patterns
| Pattern | Beschreibung |
|---------|-------------|
| Farbige Akzent-Borders | 3px oben oder links, farblich zum Sektions-Thema passend |
| Gradient-Hero-Sections | `LinearGradientBrush` von getönter Farbe (#15XXXXXX) → SurfaceColor |
| BoxShadow auf Karten | `"0 2 8 0 #25000000"` auf allen card-artigen Borders |
| Verbesserte Typographie | Größere FontSizes, SemiBold/Bold Hierarchie, farbige Akzent-Texte |
| Gradient-Trenner | `Height="2" CornerRadius="1"`, transparente Enden |
| Farbige Badge-Borders | 1px Borders mit `#40XXXXXX` passend zum Akzent |

### Polierte Views (21 von 24 - 3 brauchen kein Redesign)
- **Phase 3**: MainMenuView, LevelSelectView, StatisticsView, DailyChallengeView, LeagueView, ProfileView, WeeklyChallengeView, CollectionView
- **Phase 4**: HighScoresView, HelpView, SettingsView, ShopView, GameOverView, VictoryView, DungeonView, BattlePassView, LuckySpinView, DeckView, GemShopView, AchievementsView
- **Phase 5**: QuickPlayView
- **Kein Redesign nötig**: MainWindow (Shell), MainView (Container), GameView (SkiaSharp)

### Torn Metal Buttons (SkiaSharp, alle Action-Buttons)
Alle Action-Buttons verwenden prozedural generierte "Torn Metal" Hintergründe via `GameButtonCanvas` (SKCanvasView) + `TornMetalRenderer`.

**Pattern**: `Panel > GameButtonCanvas (Hintergrund) + Button (Background=Transparent, Foreground=White)`

**Dateien**:
- `Graphics/TornMetalRenderer.cs`: Statischer Renderer (DrawMetalBody, DrawCracks, DrawScratches, DrawRivets, DrawHighlight, DrawEdgeGlow), gepoolte SKPaint/SKPath
- `Controls/GameButtonCanvas.cs`: SKCanvasView mit 3 StyledProperties (ButtonColor, DamageLevel, ButtonSeed), IsHitTestVisible=false

**DamageLevel Convention**: CTA=0.5, Success=0.3, Danger=0.7, Gold/Premium=0.6, Secondary=0.2-0.3

**ButtonSeed Ranges** (jeder Button braucht einzigartigen Seed):
| View | Seeds | Buttons |
|------|-------|---------|
| MainMenu | 10-32 | 12 (Story, Continue, Survival, QuickPlay, Dungeon, BattlePass, Cards, League, Challenges, Shop, Profile, Settings) |
| GameOver | 40-45 | 6 (TryAgain, DoubleCoins, ContinueAd, PaidContinue, LevelSkip, MainMenu) |
| Victory | 50-51 | 2 (MainMenu/CTA, Shop) |
| QuickPlay | 60-61 | 2 (Play, NewSeed) |
| Dungeon | 70-74 | 5 (FreeStart, CoinsStart, GemsStart, AdStart, Retry) |
| LuckySpin | 80-82 | 2 (Spin, Collect) |
| DailyChallenge | 90 | 1 (Play) |
| BattlePass | 100-103 | 4 (XpBoost, FreeClaim, PremiumClaim, PremiumUpgrade) |
| GemShop | 110-112 | 3 (via Binding) |
| LevelSelect | 120 | 1 (AcceptBoost) |
| Shop | 130-132 | 3 (SelectSkin, PurchaseSkin, PurchaseUpgrade) |
| Deck | 140-142 | 2-3 (UnlockSlot5, UpgradeCard, BuyCardGems) |
| Collection | 150 | 1 (ClaimMilestone) |
| WeeklyChallenge | 155-156 | 2 (DailyBonus, WeeklyBonus) |
| Settings | 160-168 | 9 (Leaderboards, GPGS, CloudSync, CloudDownload, BuyPremium, Restore, Reset, ClearHighscores, Privacy) |
| Help | 170 | 1 (ReplayTutorial) |
| Profile | 175 | 1 (SaveName) |
| League | 180-181 | 2 (ClaimReward, Refresh) |

## Changelog Highlights

- **27.02.2026 (41)**: **Steuerungs-Optimierung: Touch-Targets + Pre-Turn Buffering**: (1) **FloatingJoystick Touch-Targets** (FloatingJoystick.cs): Joystick-Radius 60→75dp, Deadzone 0.12→0.15, Bomb-Button 50→70dp, Detonator 40→48dp, Hit-Zone-Multiplikator 1.3→1.6x (Mobile-Best-Practices). Richtungs-Hysterese 1.15x gegen Flackern bei ~45°. (2) **Pre-Turn Buffering** (Player.cs): Bei senkrechtem Richtungswechsel wird die gewünschte Richtung gepuffert wenn Querachse >40% vom Zellzentrum entfernt. Spieler bewegt weiter in alter Richtung, Turn wird automatisch bei Zellzentrum-Nähe ausgeführt mit Querachsen-Snap. Eliminiert das Problem, dass Turns auf dem exakten Frame am Grid-Zentrum getimed werden müssen. IsPerpendicular() + TryExecuteTurn() Helper. _bufferedDirection + _lastMovingDirection Felder, Reset in Respawn/ResetForNewGame. (3) **Bomben-Bug-Fix** (GameEngine.cs): InputManager.Update() zurück NACH UpdatePlayer() verschoben - Bomb-Consume-Pattern erfordert BombPressed-Check VOR Consume (v2.0.18, VC 28). Build 0 Fehler.
- **27.02.2026 (40)**: **Performance-Optimierung: Shader + Input + HUD**: (1) **Input-Reihenfolge** (GameEngine.cs): InputManager.Update() vor UpdatePlayer() verschoben (1 Frame weniger Input-Latenz). (2) **FloatingJoystick** (FloatingJoystickHandler.cs): Deadzone 0.15→0.12, Hysterese-Check mit ~10° Toleranz, PointerId-Tracking für Multi-Touch-Stabilität, HandleTouchReleased resettet bei korrektem PointerId. (3) **SKShader-Caching** (GameRenderer.cs/Atmosphere.cs): Background-Gradient, Vignette-Shader, DynamicLighting-Shader beim Init gecacht statt pro-Frame erstellt. (4) **HUD-String-Caching** (GameRenderer.HUD.cs): 11 Cache-Felder (SurvivalTimer, Combo, Enemies, Speed, Curse), gecachter SKMaskFilter für Glow, Strings nur bei Wert-Änderung aktualisiert. (5) **IEnumerable→List** (GameRenderer.cs/Atmosphere.cs/TrailSystem.cs): Render(), CollectLightSources(), TrailSystem.Update() von IEnumerable<T> auf List<T> umgestellt (kein Interface-Dispatch). (6) **Grid-Border-Shader eliminiert** (GameRenderer.Grid.cs): Ice/Lava/Teleporter-Borders von LinearGradient auf 2-Step-Alpha-DrawRect umgestellt (bis zu 12 native Shader-Allokationen pro sichtbare Transition-Zelle eliminiert). (7) **ExplosionShader optimiert** (ExplosionShaders.cs): DrawFlameLayerGradient (5-Stop-Gradient→Solid Color), DrawArmFlameTongues (3-Stop→Solid), DrawHeatHaze (3-Stop→2-Step-Alpha). (8) **Fog-Overlay** (GameRenderer.Grid.cs): RadialGradient+new SKPaint→2 Stroke-Ringe auf _fillPaint. Insgesamt ~50-100 native Shader-Allokationen pro Frame eliminiert. Build 0 Fehler.
- **27.02.2026 (39)**: **Release v2.0.14 (VC 24)**: 3 Lokalisierungs-Fixes (hardcodierte "Coins"/"Day X"/"Gems!" Strings → lokalisiert via RESX in 6 Sprachen), neuer "Coins" RESX-Key in allen 6 Sprachen + Designer.cs. Android-Gerätetest auf Sony Xperia XQ-CC54 (7 Screenshots für Play Store). Version 2.0.13→2.0.14, VersionCode 23→24, Shared-Version 2.0.10→2.0.14.
- **27.02.2026 (38)**: **3 Code-Quality-Fixes**: (1) **Cloud Save Fire-and-Forget Race Condition** (MainViewModel.cs): `_ = Task.Run(...)` mit TryLoadFromCloudAsync → `_cloudSaveInitTask = Task.Run(...)` (Task gespeichert statt verworfen, vermeidet unbeobachtete Exceptions und Race Conditions). (2) **HighScoreService DateTime-Serialisierung** (HighScoreService.cs): ScoreData.Date (DateTime) → ScoreData.DateUtc (string, ISO 8601 "O" Format) + ParseDateSafe() mit CultureInfo.InvariantCulture + DateTimeStyles.RoundtripKind. Abwärtskompatibel: Altes DateTime-Property konvertiert automatisch beim Deserialisieren. Englischer Catch-Kommentar → Deutsch. (3) **GameEngine/GameRenderer Dispose** (App.axaml.cs + MainActivity.cs): Statische App.DisposeServices() Methode disposed GameEngine, GameRenderer, GameViewModel, IFirebaseService. Desktop: desktop.ShutdownRequested Event. Android: MainActivity.OnDestroy(). Build 0 Fehler (auf clean master verifiziert, 24 vorbestehende Fehler in anderen unstaged Dateien).
- **27.02.2026 (37)**: **3 Code-Fixes**: (1) `_isDungeonRun = false` in StartStoryModeAsync/StartDailyChallengeModeAsync/StartQuickPlayModeAsync/StartSurvivalModeAsync (Bug: Dungeon-Flag blieb nach Dungeon-Run aktiv, HUD zeigte Dungeon-Buffs in anderen Modi). (2) HUD-Label-Strings pro Frame via GetString() geladen → 8 gecachte Felder (`_hudLabelKills` etc.) + `CacheHudLabels()` Methode + LanguageChanged-Event-Subscription (8 Dictionary-Lookups/Frame eliminiert). (3) `_vibration.VibrateLight()` in PlaceBomb/PlacePowerBomb/PlaceLineBombs (haptisches Feedback bei Bomben-Platzierung, konsistent mit PowerUp-Einsammlung). Build 0 Fehler.
- **26.02.2026 (36)**: **AAA Visual Redesign aller 21 Content-Views + Torn Metal Buttons**: (1) Konsistente Design-Patterns auf alle Menü-Views angewendet: Farbige 3px Akzent-Borders (oben/links) pro Sektion, Gradient-Hero-Backgrounds (getönte Farbe→SurfaceColor), BoxShadow auf allen Karten-Borders, größere Fonts + Bold/SemiBold-Hierarchie, Gradient-Trenner (Height=2 CornerRadius=1), farbige Badge-Borders. Phase 3: 8 Views, Phase 4: 12 Views, Phase 5: QuickPlayView. (2) **Torn Metal Buttons**: TornMetalRenderer.cs (SkiaSharp, statisch, gepoolte SKPaint/SKPath) + GameButtonCanvas.cs (SKCanvasView, 3 StyledProperties). ~59 Action-Buttons in 18 Views konvertiert: Panel-Wrapper mit GameButtonCanvas (Hintergrund) + transparenter Button darüber. Prozedural generierte Risse, Kratzer, abgebrochene Ecken, Nieten, Metallic-Sheen. Deterministisch per Seed (10-181). DamageLevel nach Funktion (CTA=0.5, Success=0.3, Danger=0.7, Gold=0.6, Secondary=0.2). Build 0 Fehler.
- **24.02.2026 (35)**: **Dungeon-Erweiterung + Visuelle Aufwertung** (Phasen A1-A3, B1-B7, C): (1) **Theme-System** (A1): MenuBackgroundRenderer mit 7 BackgroundTheme-Varianten (Default/Dungeon/Shop/League/BattlePass/Victory/LuckySpin), je max 60 struct-basierte Partikel, MenuBackgroundCanvas.Theme StyledProperty, 15 Views mit thematischem Hintergrund. (2) **VictoryView Victory-Theme** (A2): XAML-Gradient durch Victory-Theme ersetzt (Confetti+Fireworks+Gold-Funken). (3) **Buff-Animationen** (A3): Gestaffelte Einblend-Animation (3 Karten, 200ms delay) + Rarität-Glow-Pulsation in DungeonView. (4) **Permanente Upgrades** (B1): IDungeonUpgradeService mit 8 Upgrades (50-300 DungeonCoins), DungeonCoins als dungeon-spezifische Währung (10-100 DC/Floor). (5) **4 Legendary Buffs** (B2): Berserker/TimeFreeze/GoldRush/Phantom mit Weight 3-4. (6) **Buff-Reroll + 5 Synergies** (B5): 1x gratis Reroll, 5 Gem-Rerolls, 5 Buff-Kombinationen (Bombardier/Blitzkrieg/Festung/Midas/Elementar). (7) **5 Raum-Typen** (B3): Normal/Elite/Treasure/Challenge/Rest mit gewichteter Zufallsauswahl. (8) **8 Floor-Modifikatoren** (B4): Ab Floor 3 30% Chance (LavaBorders/Darkness/DoubleSpawns/FastBombs/BigExplosions/CursedFloor/Regeneration/Wealthy). (9) **Dungeon Node-Map** (B6): Slay the Spire-inspirierte 10×3 Map mit DungeonMapRenderer (SkiaSharp), Pfad-Auswahl, Raum-Typ-Icons, Modifikator-Badges. (10) **Ascension 0-5** (B7): Eskalierende Schwierigkeit + Belohnungen nach Floor 10 Clear. (11) **53 neue RESX-Keys** (C): Alle 6 Sprachen + Designer.cs. Build 0 Fehler, AppChecker 105 PASS / 0 FAIL. 7 neue Dateien (DungeonUpgrade.cs, IDungeonUpgradeService.cs, DungeonUpgradeService.cs, DungeonRoomType.cs, DungeonFloorModifier.cs, DungeonMapNode.cs, DungeonMapRenderer.cs).
- **24.02.2026 (34)**: **Komplett-Audit abgeschlossen** (88 Findings, 10 Phasen): Build 0 Fehler, AppChecker 105 PASS / 0 FAIL. Designer.cs synchronisiert (755→1111 Properties). Alle deutschen Fallback-Strings auf EN normalisiert. 1111 RESX-Keys in 6 Sprachen vollständig.
- **24.02.2026 (33)**: Lokalisierung Phase 4 (Findings 27-35): (1) **HUD-Labels lokalisiert** (Finding 27): 8 gecachte Strings für Kills/Time/Score/Lives/Bombs/Speed/Power/Deck. (2) **NewHighScore lokalisiert** (Finding 28). (3) **GameEngine-Strings lokalisiert** (Finding 30): BossFight/World/DailyChallenge/QuickPlay/Survival/DefeatAll/BossHit/Enraged/EnemyHit/Cursed. (4) **Designer.cs Sync** (Finding 35): 356 fehlende Properties hinzugefügt (755→1111). (5) **Deutsche Fallbacks→EN** (Finding 33): ~30 StatisticsVM Fallbacks auf EN normalisiert.
- **24.02.2026 (32)**: Critical Fixes Phase 1-3 (Findings 1-8, 16-26): (1) **Ice-Cleanup Thread-Race** (Finding 1): async Task.Delay→Frame-basierter Timer mit _pendingIceCleanups. (2) **GameEngine Dispose Guard** (Finding 2): _disposed Flag. (3) **Victory Level 50→100** (Finding 4). (4) **Poison periodisch** (Finding 5): _poisonDamageTimer 2s Cooldown statt sofortigem Kill. (5) **Score-Verdopplung** (Finding 6): Nur Level-Anteil verdoppelt. (6) **SlowMotion-Schwelle** (Finding 7): Nur bei ≥4 Gegnern oder Boss/Survival. (7) **Random-Fix** (Finding 8): new Random()→_pontanRandom. (8) **CTS Dispose** (Finding 16): LeagueService+CloudSaveService. (9) **IDisposable** (Finding 17-18): FirebaseService+ShaderEffects. (10) **Event-Abmeldung** (Finding 19-20): InputManager+MainView. (11) **Leere catch→Logger** (Finding 21): 15+ Stellen. (12) **Fire-and-Forget Error-Handling** (Finding 22-23). (13) **Trace statt Debug** (Finding 24): AppLogger. (14) **DateTime.UtcNow** (Finding 26). (15) **Sprachnamen-Akzente** (Finding 34): Español/Français/Português.
- **24.02.2026 (31)**: Monetarisierungs-Fixes (Findings 58+64): (1) **5. Deck-Slot für 20 Gems** (Finding 58): CardCatalog.MaxDeckSlots 4→5, DefaultDeckSlots=4, Slot5UnlockCost=20. ICardService + CardService: IsSlot5Unlocked Property, TryUnlockSlot5(IGemService) Methode, CardCollectionData.Slot5Unlocked Persistenz. DeckViewModel: UnlockSlot5Command, IsSlot5Unlocked/CanUnlockSlot5/UnlockSlot5Text Properties, 5. Equip-Button im Detail-Panel. DeckView: Unlock-Button unter Deck-Slots (Cyan, LockOpenVariant Icon), Gem-Badge im Header. HUD MAX_CARD_SLOTS 4→5. DeckSlotItem.IsLocked Property. (2) **60s Rewarded Ad Cooldown** (Finding 64): RewardedAdCooldownTracker.cs (statische Klasse, CooldownSeconds=60, RecordAdShown/CanShowAd/IsOnCooldown/RemainingSeconds). Integriert in 12 Ad-Placements über 8 ViewModels: GameOverVM (continue/coin_multiplier/revival/level_skip), GameVM (score_double), VictoryVM (gem_bonus), LevelSelectVM (power_up), LuckySpinVM (lucky_spin/extra_daily_spin), DungeonVM (dungeon_extra_buff), MainVM (dungeon_run), ShopVM (free_shop_upgrade). 2 neue RESX-Keys in 6 Sprachen (DeckUnlockSlot5, DeckSlot5Unlocked).
- **24.02.2026 (30)**: Features 66/67/70 (Rotating Deals, Gem-Skins, Gem-Sinks): (1) **Rotating Deals** (Feature 66): RotatingDeal Model + IRotatingDealsService/RotatingDealsService (3 tägliche + 1 wöchentliches Angebot, Seeded Random per DayId/ISO-Kalenderwoche, 20-50% Rabatt, 4+4 Deal-Typen, ClaimDeal mit Payment+Reward, JSON-Persistenz). ShopViewModel: DailyDeals/WeeklyDeal Properties + BuyDealCommand + RefreshDailyDeals(). (2) **Gem-Only Cosmetics** (Feature 67): 3 neue Gem-exklusive Spieler-Skins (Crystal 50G Epic #00BCD4, Shadow 100G Legendary #4A148C, Phoenix 200G Legendary #FF6D00) in SkinDefinition.PlayerSkins.All. GemPrice Property auf SkinDefinition. ICustomizationService + CustomizationService: TryPurchasePlayerSkinWithGems + SetGemService Lazy-Injection. ShopViewModel: GemSkinItems Sektion + BuyGemSkinCommand + SelectGemSkinCommand. (3) **Extended Gem-Sinks** (Feature 70): DeckViewModel: BuyCardForGemsCommand (Rare 15, Epic 30, Legendary 75 Gems). DungeonViewModel: ReviveForGemsCommand (15 Gems, Lives=1). LuckySpinVM: Bereits vorhanden (BuySpinWithGems 3 Gems). App.axaml.cs: IRotatingDealsService DI + CustomizationService.SetGemService() Lazy-Injection. 21 neue RESX-Keys in 6 Sprachen.
- **24.02.2026 (29)**: Performance-Optimierungen (Findings 9-15): (1) **LINQ-Elimination PlaceExit/PlacePowerUps** (Finding 9): `.Where().ToList()` durch wiederverwendbares `_blockCells` List-Feld ersetzt (0 Allokationen/Frame). (2) **Enemy-Position-Cache** (Finding 10): HashSet `_enemyPositionCache` in `UpdateBombSlide()` statt foreach über alle Gegner (O(1) statt O(n) Lookup pro sliding Bomb). (3) **Corner-Check Array-Elimination** (Finding 11): `cornersX`/`cornersY` float[4] Arrays durch 4 direkte Variablen ersetzt (8 weniger Heap-Allokationen pro Kollisions-Check). (4) **Dirty-Lists statt Grid-Iteration** (Finding 12): 3 neue Listen `_destroyingCells`, `_afterglowCells`, `_specialEffectCells` ersetzen 3x volle 150-Zellen Grid-Iteration pro Frame. Alle 10 HandleXxxExplosion-Methoden registrieren betroffene Zellen in den Dirty-Lists. `UpdateDestroyingBlocks()`, `UpdateAfterglowCells()`, `UpdateSpecialBombEffects()` iterieren nur noch aktive Zellen mit Rückwärts-Iteration + RemoveAt. (5) **Object Pooling** (Finding 13): Bewusst nicht implementiert - Bomb/Explosion haben readonly Constructor-Parameter, Allokationsdruck gering (1-5/Frame). (6) **CollectionService Debounce-Save** (Finding 14): `_isDirty` + 5s Debounce statt sofortigem Save bei jedem RecordXxx()-Aufruf. `MarkDirty()` + `FlushIfDirty()` Pattern (analog AchievementService). GameTrackingService.FlushIfDirty() ruft auch Collection.FlushIfDirty(). (7) **Achievement Dictionary-Lookup** (Finding 15): `Dictionary<string, Achievement> _achievementLookup` statt `List.Find()` - O(1) statt O(n) bei TryUnlock(), UpdateProgress(), ApplyProgress(). + Build-Fixes: MainViewModel LogWarning Signatur, AchievementsVM EmptyStateText, CollectionVM EmptyItemsText Properties.
- **24.02.2026 (28)**: UI/UX-Fixes (Findings 47-50): (1) **ShopView Template-Deduplizierung** (Finding 47): 6 identische inline Skin-DataTemplates durch 1 gemeinsames `SkinItemTemplate` in UserControl.Resources ersetzt. SkinDisplayItem.PreviewIconKind (MaterialIconKind) steuert das Kategorie-Icon im Gradient-Kreis. ShopView von ~1036 auf ~690 Zeilen reduziert. (2) **Empty States** (Finding 48): AchievementsView zeigt TrophyBroken-Icon + Text wenn CategoryGroups leer. CollectionView zeigt BookOpenBlankVariant-Icon + Text wenn Items leer. ScrollViewer nur sichtbar bei vorhandenen Daten. (3) **MainMenuView FontSize** (Finding 49): TotalEarnedText + VersionText FontSize 10→12 (bessere Lesbarkeit). (4) **Invincibility Blink-Feedback** (Finding 50): Schnelleres Blinken (20Hz statt 10Hz) in den letzten 0.5s von Unverwundbarkeit/Spawn-Schutz. Korrekter Timer je Zustand (InvincibilityTimer vs SpawnProtectionTimer).
- **24.02.2026 (27)**: Game Design Verbesserungen (Findings 51-55): (1) **Haptic bei PowerUp** (Finding 51): `_vibration.VibrateLight()` nach PowerUp-Einsammlung in GameEngine.Collision.cs. (2) **Chain-Kill-Bonus** (Finding 52): 1.5x Combo-Bonus-Multiplikator bei `_comboCount >= 3` (Kettenreaktionen), "CHAIN x{N}!" goldener Floating Text (#FFC800), Tracking via `_tracking.OnComboReached()`. (3) **Dungeon-Buffs im HUD** (Finding 53): Aktive Dungeon-Buffs als farbige Mini-Icons (20x20px, Buchstaben-Kürzel) im Side-Panel unter DECK-Sektion. `IsDungeonRun` + `DungeonActiveBuffs` Properties in GameRenderer, Daten-Übergabe in GameEngine.Render.cs, RenderDungeonBuffIcons() + GetDungeonBuffInfo() in GameRenderer.HUD.cs. (4) **MAX_DELTA_TIME reduziert** (Finding 54): 0.1f→0.05f in GameViewModel.cs (50ms Cap statt 100ms, verhindert Physik-Sprünge bei Lag-Spikes). (5) **Spezial-Bomben-Sound** (Finding 55): PlayBombExplosion(BombType) in SoundManager.cs mit Sound-Layering (Basis-Explosion + sekundärer SFX je Kategorie: Ice/Gravity/TimeWarp→PowerUp, Fire/Nova/Vortex→doppelte Explosion, Lightning/Mirror→Fuse, BlackHole→TimeWarning). TriggerExplosion() in GameEngine.Explosion.cs nutzt neue Methode für nicht-normale Bomben.
- **24.02.2026 (26)**: Bug-Fixes (4 Bugs aus Game-Studio-Analyse): (1) **CustomizationService Premium-Check** (HOCH): IPurchaseService als Dependency hinzugefügt, IsPlayerSkinOwned/IsBombSkinOwned/IsExplosionSkinOwned prüfen jetzt _purchaseService.IsPremium für Premium-Only Skins mit CoinPrice=0 (vorher: immer true → Premium-Skins ohne Premium nutzbar). (2) **SpawnEnemies Retry-Loop** (MITTEL): Fallback-Position bei leerer validPositions-Liste nutzt jetzt 40 Versuche statt 1 (Gegner wurden stillschweigend übersprungen bei ungültiger Position). (3) **ShopVM OnBalanceChanged Performance** (MITTEL): 6 unnötige ObservableCollection-Rebuilds bei Balance-Änderung entfernt (SkinDisplayItem.CanBuy hängt nicht vom Balance ab, Rebuild nur bei Kauf/Auswahl). (4) **DeckView/CollectionView responsive Spaltenbreiten** (NIEDRIG): Feste 260px durch proportionale Spalten ersetzt (DeckView: `*,260`→`1.5*,*`, CollectionView: `260,*`→`*,1.5*`).
- **24.02.2026 (25)**: UI/UX-Optimierung (6 Phasen, 26 Verbesserungen): **Phase 1 Sicherheit**: (1) GemShopViewModel: ConfirmationRequested Event + Bestätigungsdialog vor IAP-Kauf. (2) GameOverViewModel: Bestätigungsdialog vor Level-Skip (Premium + Free). (3) ShopViewModel: Detailliertes Fehler-Feedback bei fehlgeschlagenem Kauf ("Benötigt X, du hast Y"), FloatingTextRequested Event, FreeUpgradeReady FloatingText nach Ad. (4) LuckySpinViewModel: FloatingText "Ad nicht verfügbar" bei Ad-Fehler. (5) VictoryViewModel: CelebrationRequested in OnAppearing. (6) DungeonViewModel: FloatingText bei nicht-verfügbarer Ad. (7) SettingsView: Restore-Button immer sichtbar, RestoreButtonText wechselt zwischen "Wiederherstellen"/"Validieren" je nach IsPremium. (8) MainViewModel: GemShopVm.ConfirmationRequested + ShopVm.FloatingTextRequested verdrahtet. **Phase 2 Touch-Targets**: (9) MainMenuView: ColumnDefinitions `*,1.5*,1.5*`→`*,1.2*,1.2*`, Gem-Badge Padding 14,6→16,8 + Icon 18→20, Story Mode Height 50→56, alle 5 Utility-Buttons Height 36→44 + FontSize 11→12 + Icons 14→16. (10) GameOverView: Score-Sektion in ScrollViewer gewrappt (Overflow-Schutz). (11) HelpView: PowerUp/Enemy Icons 32→48px. (12) CollectionView/DeckView: Item-Padding erhöht. **Phase 3 Margins**: (13) GemShopView Bottom-Margin 12→80. (14) WeeklyChallengeView doppeltes Margin bereinigt. **Phase 4 Visuell**: (15) GameOverView: PaidContinue Background→#FFD700 (Gold). (16) VictoryView: Crown-Icon Puls-Animation (Opacity 1.0→0.7→1.0, 2s loop), Danke-Text FontSize 20→24 + FontWeight SemiBold + TextPrimaryBrush. (17) HighScoresView: Trophy-Icons für Top 3 (Gold/Silber/Bronze) + IsRank1/2/3/IsRankOther Properties. (18) LevelSelectView: Lock-Overlay #80000000→#60000000. (19) BattlePassView: Scroll-Hinweis-Gradient am rechten Rand. (20) AchievementsView: Kategorie-Streifen 4→8px. (21) DailyChallengeView: CompletedToday Badge #40→#60 Opacity. **Phase 5 Animationen**: (22) MainMenuView: Daily Reward Popup Opacity-Transition 250ms. (23) ProfileView: Save-Button nur bei Namensänderung aktiv (IsNameChanged Property). **Phase 6 Polish**: (24) QuickPlayViewModel: Seed als 6-stelliger Hex-String statt Dezimal. 8 neue RESX-Keys in 6 Sprachen (PurchaseFailedDetail, SkipLevelConfirm/Message, AdUnavailable, ValidatePurchase, FreeUpgradeReady, GemPurchaseConfirm).
- **23.02.2026 (23)**: Menü-Hintergründe + Sammlung + Profil + Deck-Fix: (1) Deck-View Crash gefixt: EquipToSlot/UnequipSlot Signatur von string→int (XAML übergibt Int via CommandParameter). (2) MenuBackgroundRenderer + MenuBackgroundCanvas erstellt: Animierter Bomberman-Hintergrund (Gradient, Grid, 8 Bomben-Silhouetten, 25 Funken, 6 Flammen-Wisps), struct-basiert, ~30fps. (3) Animierten Hintergrund in 12 Views eingebaut (MainMenu, Collection, Deck, League, DailyChallenge, WeeklyChallenge, LuckySpin, Statistics, BattlePass, Dungeon, QuickPlay, HighScores). MainMenuView alte Partikel-Code (~125 Zeilen) entfernt. (4) CollectionView visuell aufgewertet: Karten 80x90→100x115px, Raritäts-Border mit Glow (Enemies=#F44336, Bosses=#FFD700, PowerUps=#4CAF50, Cards=#2196F3, Cosmetics=#9C27B0), LockOutline statt HelpCircleOutline für nicht-entdeckte Items, Kategorie-Header mit ProgressBar, Meilenstein-Fortschrittsbalken, erweitertes Detail-Panel mit farbigem Border. 2 neue Converter: StringToColorBrushConverter, BoolToCardBackgroundConverter. CategoryProgressPercent + MilestoneProgressPercent Properties. (5) ProfileView + ProfileViewModel erstellt: Spielername editieren (max 16 Zeichen, LeagueService), Stats-Übersicht (Sterne/Coins/Gems/Liga/Achievements), Skin/Frame-Anzeige. Landscape 2-Spalten Layout. MainMenu Statistik→Profil-Button umgestellt. 12 neue RESX-Keys in 6 Sprachen. DI: ProfileViewModel registriert (22 VMs, 26 Services).
- **22.02.2026 (22)**: Liga-System auf Firebase umgebaut: (1) IFirebaseService/FirebaseService erstellt (Anonymous Auth + REST API CRUD, identisches Pattern wie HandwerkerImperium). (2) Firebase-Models (FirebaseAuthResponse, FirebaseTokenResponse, FirebaseLeagueEntry) in Models/Firebase/. (3) ILeagueService komplett überarbeitet: +IsOnline, +IsLoading, +PlayerName, +SetPlayerName(), +RefreshLeaderboardAsync(), +InitializeOnlineAsync(), +LeaderboardUpdated Event, LeagueLeaderboardEntry +IsRealPlayer. (4) LeagueService komplett neugeschrieben: Local-First mit Firebase-Sync, deterministische Saisons (Epoche 24.02.2026), NPC-Backfill auf 20 Einträge, Debounced Firebase-Push (3s), Firebase-Pfad `league/s{saison}/{tier}/{uid}`. (5) LeagueViewModel erweitert: IsLoading/IsOnline/PlayerName Properties, RefreshLeaderboardCommand (async), Firebase-Init in OnAppearing. (6) LeagueView.axaml: Online/Offline-Indikator (CloudCheck grün/CloudOffOutline rot), Refresh-Button, Lade-Indikator, Echtpsieler-Cyan-Punkt. (7) App.axaml.cs: IFirebaseService als Singleton registriert. Firebase-Projekt bomberblast-league (europe-west1) konfiguriert.
- **24.02.2026 (24)**: Gem-IAP-Shop: GemShopViewModel + GemShopView (Landscape 2-Spalten: Gem-Balance links, 3 Kauf-Pakete rechts). 3 Gem-Pakete via IPurchaseService.PurchaseConsumableAsync (gem_pack_small 100 Gems/0,99EUR, gem_pack_medium 500 Gems/3,99EUR, gem_pack_large 1500 Gems/7,99EUR). GemPackageItem Model-Klasse. MainMenuView Gem-Badge als tappbarer Button (GoToGemShopCommand). GoGemShop NavigationRequest Record. MainViewModel erweitert (GemShopVm Property, IsGemShopActive, Navigation/HideAll/HandleBackPressed). MainView GemShopBorder + CSS-Klassen-Toggle. DI: GemShopViewModel als Singleton registriert (23 VMs). 8 neue RESX-Keys in 6 Sprachen (GemShopTitle/Description, GemPackSmall/Medium/Large, GemPackPopular/BestValue, GemPurchaseSuccess).
- **22.02.2026 (21)**: Deck-Builder UI+Mechanik Redesign: DeckView komplett überarbeitet nach HandwerkerImperium-Muster. (1) Crash-Fix: ReflectionBinding `$parent[ItemsControl]` → `#Deck_Root.DataContext` Pattern (identisch CollectionView-Fix). (2) Alle 13 Karten sichtbar (nicht nur besessene): Unbesessene als gesperrt mit Lock-Icon + "???" Name + Drop-Quellen-Info. (3) BombType→MaterialIcon-Mapping: 13 individuelle Icons (Snowflake, Fire, Water, WeatherFog, LightningBolt, Magnet, Skull, ClockFast, FlipHorizontal, Tornado, Ghost, Flare, CircleSlice8). (4) Premium-Karten-Design: 100x120px Tiles, Raritäts-Border + Glow-Icon-Farbe, Level-Farben (Bronze/Silber/Gold), Upgrade-Badge (grün) + Equipped-Badge (blau). (5) Sammlungs-Fortschrittsbalken im Header. (6) Erweitertes Detail-Panel (260px): Rarität-Badge, Stärke-Multiplikator (1.0x/1.2x/1.4x), Upgrade-Fortschrittsbalken, Fundorte je Rarität (Level 60%/Boss/Dungeon). (7) Sortierung Legendary→Common. (8) 8 neue RESX-Keys in 6 Sprachen (DeckLabel, CardStrength, CardDropSource, CardUpgradeLabel, CardNotOwned, DropSourceLevel, DropSourceBoss, DropSourceDungeon).
- **21.02.2026 (20)**: Arcade-Modus komplett entfernt: `_isArcadeMode`, `_arcadeWave`, `ArcadeWave`, `IsArcadeMode`, `StartArcadeModeAsync()`, `OnArcadeWaveReached()`, `HighestArcadeWave` entfernt. `GetStartLives()` Parameter `isArcade` entfernt. `PlayGamesIds.LeaderboardArcadeHighscore` entfernt. `IBattlePassService.ArcadeWave10Plus` XP-Quelle entfernt. LevelGenerator: `GenerateArcadeLevel()`, `ConfigureArcadeEnemies()`, `ConfigureArcadePowerUps()` entfernt. 4 Arcade-Achievements entfernt (arcade_10/25/50/100), Achievement-Anzahl 70→66. `AchievementCategory.Arcade` → `AchievementCategory.Challenge` umbenannt (Survival-Achievements). MainMenu: Arcade-Button + HighScores-Button entfernt, jetzt 2-Spalten Layout (Survival + QuickPlay). Utility-Bar: 6→5 Spalten (ohne HighScores). HighScoresView: PlayArcadeToScore entfernt. StatisticsView: HighestWave-Anzeige entfernt. GameOverViewModel: IsArcadeMode Property entfernt.
- **21.02.2026 (19)**: Phase 9.3 Feature-Expansion (Achievement-Erweiterung 50→70): 20 neue Achievements in 5 neuen Bereichen (Dungeon, Karten, Liga, Daily Missions, Cross-Feature). 10 neue AchievementData Tracking-Felder (TotalDailyMissions, BestDungeonFloor, TotalDungeonRuns, HighestBattlePassTier, TotalUniqueCards, HighestLeagueTier, BestSurvivalKills, TotalLineBombs, TotalDetonations, LuckyJackpots). 14 neue IAchievementService-Methoden implementiert. Lazy-Injection Pattern: 4 Services (BattlePass, Card, League, DailyMission) erhalten IAchievementService via SetAchievementService() nach ServiceProvider-Build. Hooks in GameEngine (5 Stellen: LineBomb, Detonator, Survival-Kills, Dungeon-Floor/Boss/Run, QuickPlay-Max), Services (4 Services: BattlePass Tier-Up, Card-Collection, League-Promotion, Daily-Mission-Complete), ViewModels (2: LuckySpinVM Jackpot, CollectionVM Progress). ApplyProgress() um 5 neue Tracking-Felder erweitert. 40 neue RESX-Keys in 6 Sprachen (20x Name + 20x Desc).
- **21.02.2026 (18)**: Phase 9.2 Feature-Expansion (Cross-Feature XP/Punkte-Hooks): BattlePassXpSources um 6 neue Quellen erweitert (LuckySpin 30, CollectionMilestone25-100 100-500, CardUpgrade 80, LeagueSeasonReward 150). MainMenuViewModel: IBattlePassService + ILeagueService injiziert, BP XP (50) + Liga (5) bei Daily Login Claim. LuckySpinViewModel: IBattlePassService injiziert, BP XP (30) bei CollectReward. CollectionViewModel: IBattlePassService + ILeagueService injiziert, BP XP (100-500) + Liga (5-20) bei Meilenstein-Claim. DeckViewModel: IBattlePassService injiziert, BP XP (80) bei Karten-Upgrade. LeagueViewModel: IBattlePassService injiziert, BP XP (150) bei Liga-Saison-Belohnung.
- **21.02.2026 (17)**: Phase 8 Feature-Expansion (Cloud Save): Local-First Cloud Sync mit 35 Persistenz-Keys. CloudSaveData Model (Version, TotalStars, CoinBalance, GemBalance, TotalCards, Keys Dictionary für Konflikt-Resolution). ICloudSaveService/CloudSaveService (Debounce 5s, Pull bei App-Start, Push nach Aktionen). NullCloudSaveService (Desktop-Stub). IPlayGamesService erweitert um SaveToCloudAsync/LoadCloudSaveAsync. AndroidPlayGamesService mit lokaler Datei-Persistenz als Zwischenspeicher (TODO: Snapshots API). SettingsView Cloud Save Sektion (Toggle, Status, Sync/Download-Buttons). CloudSaveServiceFactory Pattern in App.axaml.cs. 12 neue RESX-Keys in 6 Sprachen. DI: ICloudSaveService registriert (21 VMs, 25 Services).
- **21.02.2026 (16)**: Phase 7 Feature-Expansion (Liga-System): Simuliertes offline Ranking mit 5 Ligen (Bronze→Diamant), 14-Tage-Saisons, 20 NPC-Gegner (Seeded Random). LeagueTier Enum + Extensions (Models/League/). ILeagueService/LeagueService (JSON-Persistenz, NPC-Generierung, Auf-/Abstieg Top 30%/Bottom 20%). LeagueViewModel + LeagueView (2-Spalten Landscape, Rangliste + Liga-Info). Punkte-Hooks in GameEngine.Level.cs (10 + Level/10 + 20 Boss-Bonus) und Collision.cs (+25 Boss-Kill). MainMenu League-Button (Trophy Icon, #00CED1). 25 neue RESX-Keys in 6 Sprachen. DI: ILeagueService + LeagueViewModel registriert (21 VMs, 24 Services).
- **21.02.2026 (15)**: Phase 6 Feature-Expansion (Sammlungs-Album): Enzyklopädie mit 5 Kategorien (12 Gegner, 5 Bosse, 12 PowerUps, 14 Karten, Kosmetik). CollectionEntry Model, ICollectionService/CollectionService (Aggregation aus Card/Customization + eigenes Encounter-Tracking, JSON-Persistenz). Meilenstein-Belohnungen (25/50/75/100% → Coins + Gems). GameEngine-Hooks für automatisches Tracking (RecordEnemyEncountered/Defeated/PowerUpCollected/BossDefeated). CollectionViewModel + CollectionView (2-Spalten Landscape, Kategorie-Grid + Detail). MainMenu Collection-Button (BookOpenPageVariant Icon, #E65100). 66 neue RESX-Keys in 6 Sprachen. DI: ICollectionService + CollectionViewModel registriert (20 VMs, 23 Services).
- **21.02.2026 (14)**: Phase 4 Feature-Expansion (Dungeon Run - Roguelike-Modus): Kompletter Dungeon-Run-Modus mit 10+ Floors, steigender Schwierigkeit, Boss alle 5 Floors. DungeonRunState + DungeonStats + DungeonFloorReward (Models/Dungeon/). 12 Dungeon-Buffs mit gewichteter Auswahl (DungeonBuffCatalog). IDungeonService/DungeonService (JSON-Persistenz, Run-Management, Floor-Belohnungen, Karten-Drops). GameEngine Dungeon-Integration (_isDungeonRun Flag, StartDungeonFloorAsync, ApplyDungeonBuffs, Dungeon-Events). LevelGenerator.GenerateDungeonFloor() mit Chunk-Templates + Gegner-Pool. DungeonViewModel (3 States: PreRun/BuffSelection/PostRun, 6 Commands) + DungeonView (Landscape 2-Spalten). MainMenu Dungeon-Button (Sword Icon, #4A148C). GameViewModel mode=dungeon Support (floor + seed Parameter). 43 neue RESX-Keys in 6 Sprachen. DI: IDungeonService + DungeonViewModel registriert (19 VMs, 22 Services).
- **21.02.2026 (13)**: Phase 2 Feature-Expansion (Karten-System + Deck-Builder): 14 Bomben als sammelbare Karten mit Raritaet (Common/Rare/Epic/Legendary) + Level (Bronze/Silber/Gold). CardCatalog (Models/Cards/), ICardService/CardService (JSON-Persistenz, gewichteter Random-Drop, Upgrade-Logik), DeckViewModel + DeckView (2-Spalten Landscape, WrapPanel Sammlung + Deck-Slots + Detail). GameEngine Deck-Integration (PlaceBomb aus aktivem Card-Slot, Karten-Drop bei Level-Complete). HUD 4 Mini-Karten-Slots (Tap zum Wechseln, Uses-Anzeige, Raritaets-Rahmen). MainMenu Deck-Button. Player.EquippedCards + ActiveCardSlot. 37 neue RESX-Keys in 6 Sprachen. DI: ICardService + DeckViewModel registriert (18 VMs, 21 Services).
- **21.02.2026 (12)**: Phase 1 Feature-Expansion (Fundament): Raritäts-System (Models/Rarity.cs: Common/Rare/Epic/Legendary mit Farben/Glow/Shimmer), RarityRenderer (Graphics/RarityRenderer.cs: DrawRarityBorder/Glow/Shimmer/Background/Badge/Complete mit gepoolten SKPaint). Gem-Währung (IGemService/GemService: zweite Währung, Pattern von CoinService, Key "GemData"). 10 neue Bomben-Typen (BombType Enum +Smoke/Lightning/Gravity/Poison/TimeWarp/Mirror/Vortex/Phantom/Nova/BlackHole), 10 HandleXxxExplosion() Methoden in GameEngine.Explosion.cs. 5 neue Zellen-Effekte (Cell.cs: IsSmokeCloud/IsPoisoned/IsGravityWell/IsTimeWarped/IsBlackHole mit Timern). Verlangsamungs-Stacking (Frost+TimeWarp+BlackHole multiplikativ). EnemyAI Smoke-Konfusion. 10 neue Bomben-Renderings in GameRenderer.Items.cs (Farben + Partikel). 5 neue Zellen-Effekt-Renderings in GameRenderer.Grid.cs. 35 neue RESX-Keys in 6 Sprachen (Gems/Rarity/Bomben-Namen+Beschreibungen/Effekt-Texte). DI: IGemService registriert.
- **21.02.2026 (11)**: Bugfixes: (1) Leben-Reset bei Level-Wechsel: NextLevelAsync() rief ApplyUpgrades() nicht auf → Spieler behielt Restleben statt zurückgesetzt zu werden. Fix: ApplyUpgrades() vor LoadLevelAsync() in NextLevelAsync(). (2) Gegner bleiben bei Bombe stehen: PlaceBomb() rief nicht InvalidateEnemyPaths() auf → Gegner liefen weiter auf altem Pfad und standen vor Bombe still bis AIDecisionTimer ablief. Fix: InvalidateEnemyPaths() nach jeder PlaceBomb() (auch Diarrhea-Curse). (3) TryFollowCachedPath() verbessert: Bei kompromittiertem Pfad (DangerZone oder Hindernis) wird jetzt sofort GetRandomSafeDirection() aufgerufen statt nur false zurückzugeben → Gegner weichen sofort aus statt stehenzubleiben.
- **21.02.2026 (10)**: Shop-Upgrade-Icons: ShopUpgradeIconCanvas.cs (bindbare SKCanvasView mit UpgradeTypeIndex/IconColorArgb StyledProperties) + ShopIconRenderer.cs (12 prozedurale SkiaSharp-Illustrationen statt Material.Icons). ShopDisplayItem.cs: UpgradeTypeIndex + IconColorArgb Computed Properties. ShopView.axaml: MaterialIcon durch ShopUpgradeIconCanvas in Upgrade-DataTemplate ersetzt. Jeder der 12 UpgradeTypes hat einzigartiges Icon (Bombe+Zündschnur, Flamme+Gradient, Blitz+Speedlines, Herz+EKG, Stern+Funkeln, Uhr+Zeiger, Schild+Energiekreuz, Münzstapel, Kleeblatt, Schneeflocken-Bombe, Flammen-Bombe, Schleim-Bombe). Gepoolte SKPaint-Objekte, Lighten/Darken Helper.
- **21.02.2026 (9)**: AI-Fix + Pontan-Balance: InvalidateEnemyPaths() in GameEngine.cs - bei Block-Zerstörung (UpdateDestroyingBlocks) werden alle Gegner-AI-Timer auf 0 gesetzt + Pfad-Cache geleert → Gegner berechnen sofort neue Wege statt auf altem Pfad stehenzubleiben. Pontan-Strafe welt-skaliert: PONTAN_MAX_COUNT/PONTAN_SPAWN_INTERVAL als Konstanten durch GetPontanMaxCount()/GetPontanSpawnInterval()/GetPontanInitialDelay() Methoden ersetzt. Welt 1: 1 Pontan max, 8s Intervall, 5s Gnadenfrist. Welt 2: 2 Pontans, 6s, 3s. Welt 3+: 3 Pontans, 5s, sofort (wie bisher). _pontanInitialDelay Feld hinzugefügt, OnTimeExpired() nutzt Gnadenfrist.
- **21.02.2026 (8)**: Quick-Play Backend: Neuer Spielmodus mit einstellbarer Schwierigkeit (1-10) und Seed-basierter Level-Generierung. LevelGenerator.GenerateQuickPlayLevel(seed, difficulty) mit skalierbaren Gegnern, Mechaniken, Layouts und PowerUps. GameEngine.StartQuickPlayModeAsync() mit _isQuickPlayMode Flag (kein Progress, kein Continue, nach LevelComplete direkt GameOver). QuickPlayViewModel mit Schwierigkeits-Slider und Seed-Anzeige/-Neugenerierung. MainMenuViewModel navigiert jetzt zu QuickPlayView statt direkt ins Spiel. MainViewModel: QuickPlayVm Property + IsQuickPlayActive + NavigateTo/HideAll/HandleBackPressed erweitert. GameViewModel.SetParameters um difficulty-Parameter erweitert. DI-Registrierung in App.axaml.cs (17 ViewModels).
- **21.02.2026 (7)**: Spezial-Bomben-Rendering in GameRenderer: RenderBomb() erweitert mit typ-spezifischen Farben (Ice=Hellblau/Cyan, Fire=Dunkelrot/Orange, Sticky=Grün/Gelbgrün) für Body, Glow, Fuse und Spark. RenderBombTypeParticles() mit 3 Partikel-Stilen: Ice (5 aufsteigende Frost-Punkte + Schimmer-Ring), Fire (6 Flammen-Partikel + pulsierender Glow-Ring), Sticky (4 Schleim-Tropfen mit Wachsen/Fallen-Zyklus + 3 Schleim-Fäden). RenderSpecialBombCellEffects() rendert eingefrorene Zellen (blauer Overlay + 3 Eis-Kristall-Linien + weißer Shimmer) und Lava-Zellen (rot-oranger Overlay + innerer Glow + 3 Lava-Blasen mit Animations-Zyklus). RenderFrozenEnemyOverlay() zeichnet Frost-Overlay über Gegner auf eingefrorenen Zellen (blauer halbtransparenter Oval + weißer Frost-Rand + Eis-Kristalle). Walk-Wobble deaktiviert für eingefrorene Gegner. Alle Effekte verwenden gepoolte SKPaint-Objekte (keine per-Frame Allokationen).
- **21.02.2026 (6)**: Spezial-Bomben GameEngine-Logik: PlaceBomb() setzt Bomb.Type basierend auf Player.ActiveSpecialBombType + SpecialBombCount. TriggerExplosion() ruft HandleIceExplosion (Frost 3s + blaue Partikel), HandleFireExplosion (Lava-Nachwirkung 3s + Glut-Partikel), HandleStickyExplosion (Kettenreaktion + Klebe-Frost 1.5s + grüne Partikel). UpdateSpecialBombEffects() baut FreezeTimer/LavaTimer auf Zellen ab. Frost-Verlangsamung: Gegner (inkl. Boss) auf IsFrozen-Zellen bekommen 0.5x deltaTime. Lava-Schaden: Spieler auf IsLavaActive-Zellen stirbt (Shield absorbiert, Flamepass schützt). ApplyUpgrades() setzt HasIceBomb/HasFireBomb/HasStickyBomb via ShopService, gibt 3 SpecialBombCount pro Level.
- **21.02.2026 (5)**: Boss-Level-Generierung + Boss-AI: Level.BossKind Property (BossType?) steuert Boss-Spawn. ConfigureBossLevel() überarbeitet: Boss-Typ-Mapping (StoneGolem W1-2, IceDragon W3-4, FireDemon W5-6, ShadowMaster W7-8, FinalBoss W9-10), spezifische Begleit-Gegner pro Welt (leicht→schwer). SpawnEnemies() platziert Boss in Arena-Mitte (WIDTH/2, HEIGHT/2), räumt Blöcke im Boss-Bereich frei. Boss-AI in EnemyAI.cs: Kein A*-Pathfinding, direkter Richtungs-Check zum Spieler, CanBossMoveInDirection() mit Multi-Cell Kollision, steht still bei Telegraph/Angriff, Enraged halbiert Decision-Timer. UpdateEnemies() ruft MoveBoss() für BossEnemy auf. 10 neue RESX-Keys in 6 Sprachen (BossStoneGolem/IceDragon/FireDemon/ShadowMaster/FinalBoss, BossAttackBlockRain/IceBreath/LavaWave/Teleport, BossFight).
- **21.02.2026 (4)**: Boss-Fight Kollision + Spezial-Angriffe: Multi-Cell Explosions-Kollision (OccupiesCell statt GridX/GridY), Boss-HP-Anzeige bei Treffer, Enrage-Warnung bei 50% HP. 5 Spezial-Angriffe: StoneGolem (Blockregen, 3-4 zufällige Blöcke), IceDragon (Eisatem, ganze Reihe wird 3s zu Eis), FireDemon (Lava-Welle, halber Boden gefährlich), ShadowMaster (Teleport zu zufälliger Position), FinalBoss (rotiert durch alle 4). Telegraph-System (2s gelbe Warnung → 1.5s roter Angriff). Boss-Bewegung via UpdateBossAI (70% Richtung Spieler, 30% zufällig). Boss-Tod mit Gold-Partikel-Celebration + Score 10k-50k. Spieler-Schaden durch AttackTargetCells (Shield absorbiert).
- **21.02.2026 (3)**: Boss-Rendering in GameRenderer: RenderEnemy() erkennt BossEnemy und delegiert an RenderBoss(). 5 Boss-Typ-Renderer (StoneGolem: Fels+Arme+rote Augen+Enrage-Risse, IceDragon: Oval+Flügel mit Flap-Animation+Frost-Aura, FireDemon: Rund+Flammenkrone+Flammen-Aura, ShadowMaster: Umhang+Kapuze+Schatten-Wisps, FinalBoss 3x3: Schwarz+Goldkrone+4-Element-Akzente+Multi-Color-Aura). HP-Balken über Boss (Grün/Gelb/Rot, pulsierender Rand bei Enrage). Boss-Angriffs-Telegraph (dunkelrote Warnzonen auf AttackTargetCells, Intensität steigend, Glow-Rand bei hoher Intensität). Boss-Death-Animation (größerer Squash/Stretch + Farb-Blitz). Alle Bosse mit Walk-Wobble, Neon-Glow, Telegraph/Attack-Pulsation.
- **21.02.2026 (2)**: Level-Expansion Phase 1 (50→100 Level, 10 Welten): 5 neue Welten (Ruinen/Ozean/Vulkan/Himmelsfestung/Schattenwelt) mit je 10 Leveln. 4 neue Gegner-Typen: Tanker (2 Hits nötig, Rüstungs-Rendering), Ghost (periodische Unsichtbarkeit, transparentes Rendering), Splitter (teilt sich bei Tod in 2 Mini-Splitter), Mimic (tarnt sich als Block, Hinterhalt bei Spielernähe). 5 neue Welt-Mechaniken: FallingCeiling (zufällige Blöcke nach 60s), Current (Strömung drückt Spieler), Earthquake (Blöcke verschieben sich alle 30s), PlatformGap (Lücken = sofortiger Tod), Fog (eingeschränkte Sicht, 5.5-Zellen-Radius). 4 neue Layout-Patterns: Labyrinth, Symmetrie, Inseln, Chaos. 10 neue WorldPalettes (Classic+Neon pro Welt). Stern-Gates für Welten 6-10 (70/100/135/175/220). LevelSelect auf 10 Welten erweitert. 5 neue Achievements (world6-world10, 2000-5000 Coins). 30 neue RESX-Keys in 6 Sprachen.
- **21.02.2026**: Gerätetest-Bugfixes (6 Fixes): (1) Pause-Overlay Touch-Fix: GameCanvas.IsHitTestVisible wird bei Overlay-Sichtbarkeit deaktiviert (PropertyChanged auf IsPaused/ShowScoreDoubleOverlay), damit Overlay-Buttons Touch-Events empfangen. (2) MainMenu "Einstellungen"-Button Text abgeschnitten: FontSize 12→11, Icon 16→14, Spacing 4→3, TextTrimming=CharacterEllipsis. (3) ShopView Bottom-Margin 80→100dp (Upgrade-Preise hinter Ad-Banner verdeckt). (4) SettingsView Sprache-ComboBox: Foreground auf TextPrimaryBrush gesetzt (Text war im Dark-Theme unsichtbar). (5) first_victory Achievement bei jedem Level-Complete freigeschaltet statt nur Level 1: `if (level == 1)` Bedingung hinzugefügt. (6) HighScoreService Dummy-Daten (AAA/BBB/CCC) entfernt: AddDefaultScores() komplett gelöscht, leere Bestenliste zeigt Empty-State.
- **20.02.2026 (3)**: Premium-Redesign + Steuerungs-Optimierung: Premium-Preis 3,99€→1,99€ in allen 6 Sprachen. 8 neue RESX-Keys (PremiumFeature3xCoins/AutoDouble/FreeBoost/FreeSkip/FreeContinue, SkipLevelFree, BoostFree, ContinueFree) in 6 Sprachen. Premium-Feature-Liste im Shop (5 Vorteile: 3x Coins, Auto-Score-Double, Free Boost/Skip/Continue). Kostenlose Premium-Aktionen (SkipLevel/Boost/Continue ohne Ad für Premium-User). Joystick-Steuerung: Deadzone 0.08→0.15, Richtungswechsel-Hysterese (~10° Toleranz), Bomb-Button Hitzone 1.5x→1.15x, Stuck-Detection 167ms→417ms, Corner-Assist konstant 4px, Multi-Touch Pointer-ID Tracking (kein Joystick-Verlust bei Zweit-Finger), haptisches Feedback bei Richtungswechsel (15ms Tick).
- **20.02.2026 (2)**: UX-Optimierung (7 Bereiche): (1) Bestätigungsdialoge für teure Shop-Käufe (≥3000 Coins) und Paid-Continue via ConfirmationRequested Event (TaskCompletionSource<bool>). (2) GameOver Button-Hierarchie: "Try Again" als Primary CTA (grün, 56px, oben), Motivations-Texte je nach Ergebnis. (3) Gesperrte Level: Feedback-Text bei Tipp auf gesperrtes Level + Best-Score-Anzeige pro Level in LevelSelect. (4) MainMenu asymmetrisch umstrukturiert (Spielmodi 3* links, Menü 2* rechts). (5) Achievement-Kategorien (5 Gruppen mit Fortschrittsanzeige, farbige Akzent-Bars). Premium-Preis (3,99€) direkt am Button sichtbar. (6) ReducedEffects-Toggle: Deaktiviert ScreenShake, ParticleSystem, Hit-Pause, Slow-Motion via Enabled-Properties + InputManager-Persistenz. (7) Navigations-Sounds: SoundManager in MainViewModel injiziert, SFX_MENU_SELECT bei View-Wechsel. 12 neue RESX-Keys in 6 Sprachen.
- **20.02.2026**: Stabilitäts-Fix (6 Crash-Ursachen): (1) CancellationToken in HandleGameOver/HandleLevelComplete gegen Race Condition bei Back-Button während Delay, (2) try-catch um NavigateTo mit Fallback zum Hauptmenü, (3) Activity-Lifecycle-Check (`IsFinishing`/`IsDestroyed`) in AndroidPlayGamesService vor allen GPGS-Client-Aufrufen, (4) `MediaPlayer.PrepareAsync()` statt `Prepare()` + `lock(_musicLock)` Thread-Safety in AndroidSoundService (ANR-Fix), (5) AchievementService Save-Debounce (Dirty-Flag + 500ms Intervall + FlushIfDirty bei GameOver/LevelComplete), (6) Alle ViewModels auf Singleton umgestellt (waren Transient, aber effektiv Singleton da von MainViewModel gehalten).
- **19.02.2026 (5)**: Google Play Games Services v2 Integration: IPlayGamesService Interface + NullPlayGamesService (Desktop) + AndroidPlayGamesService (Linked File in Premium.Ava). PlayGamesIds.cs mit Mapping für 24 Achievements + 1 Leaderboard (TODO-Platzhalter). Auto-Sign-In bei App-Start (GPGS v2 Standard). Achievement-Sync: AchievementService sendet bei TryUnlock() automatisch an GPGS. Leaderboard-Sync: Total-Stars an Leaderboard. SettingsView: Google Play Games Sektion (Toggle, Status, Leaderboards/Achievements Buttons). NuGet: Xamarin.GooglePlayServices.Games.V2 121.0.0.2. AndroidManifest: com.google.android.gms.games.APP_ID meta-data. Resources/values/games.xml für Game Services Project-ID. 4 neue RESX-Keys (PlayGamesSection/Enabled/ShowLeaderboards/ShowGpgsAchievements) in 6 Sprachen.
- **19.02.2026 (4)**: UI-Polish + Achievements-Erweiterung: 8 neue Achievements (24 total): first_victory (Progress), combo3/combo5 (Skill), daily_streak7/daily_complete30 (Progress), kick_master/power_bomber (Combat), curse_survivor (Skill). Neue IAchievementService-Methoden: OnComboReached, OnBombKicked, OnPowerBombUsed, OnCurseSurvived, OnDailyChallengeCompleted. AchievementData erweitert: TotalBombsKicked, TotalPowerBombs, CurseTypesSurvived (Bit-Flags). GameEngine-Hooks: Combo nach Bonus, Kick in TryKickBomb, PowerBomb in PlacePowerBomb, Curse-Ende in UpdatePlayer (curseBeforeUpdate/nach Update). DailyChallengeViewModel: IAchievementService injiziert für Daily-Achievement-Tracking. Skin-Auswahl im Shop (ICustomizationService). AchievementIconCanvas (bindbare SKCanvasView für DataTemplates). MedalCanvas (Gold/Silber/Bronze im GameOver). Victory-Screen für Level 50. Daily Reward 7-Tage-Popup. Premium Feature-Liste. SettingsView Icon-Fix: AdOff→AdsOff. 25 neue RESX-Keys in 6 Sprachen.
- **19.02.2026 (3)**: Final Polish + Balancing: ShieldStart Preis 15.000→8.000 Coins. Daily Challenge "Neu!"-Badge im MainMenu (IDailyChallengeService + IsDailyChallengeNew Property). TotalEarned-Anzeige im MainMenu (CoinService.TotalEarned). Pontan-Spawn-Warnung (pulsierendes rotes "!" 1.5s vor Spawn, vorberechnete Position, PreCalculateNextPontanSpawn + SpawnPontanAtWarningPosition). Flammen-Zungen Clamp-Fix (min 1 statt 3, Divisor 12→15). 2 neue RESX-Keys (TotalEarned, DailyChallengeNew) in 6 Sprachen.
- **19.02.2026 (2)**: Visual Polish + Achievement-Belohnungen: Combo-Anzeige mit sanftem Alpha-Fade bei Timer < 0.5s (kein abruptes Verschwinden). HUD-Font-Size FP-Rundungs-Fix (save/restore statt multiply/divide). Danger-Warning Frequenz-Cap (max 15Hz statt 25Hz, kein Strobe). Curse-Indikator Alpha 80→140 (besser sichtbar). Afterglow Alpha 70→100 (kräftiger). Floating Text sqrt-Easing (länger lesbar). GameOver Coin-Counter mit Ease-Out Animation (frame-basiert statt step-basiert). Achievement Coin-Belohnungen: 500-5000 Coins pro Achievement (CoinReward Property auf Achievement Model, ICoinService in AchievementService injiziert, Belohnung bei TryUnlock(), Toast zeigt "+X Coins", AchievementsView zeigt Reward-Text in Gold).
- **19.02.2026**: Game Juice + UX + Monetarisierung Optimierung: Spieler-Tod Partikel-Burst (orange/rot). PowerUp Pop-Out Animation (BirthTimer/BirthScale mit sin-basiertem Overshoot + Gold-Partikel). Eskalierende Kettenreaktions-Effekte (ChainDepth auf Bomb → mehr Shake/Sparks/Embers pro Ketten-Stufe). Combo-Anzeige im HUD (pulsierender Text + schrumpfende Timer-Bar, Farbe nach Stärke: Orange/Rot/MEGA). Near-Miss Feedback auf GameOver-Screen (zeigt Punkte bis zum nächsten Stern innerhalb 30% Schwelle). GameOver DoubleCoins als Primary CTA (größer, Gold, GameButton-Klasse). First Victory Celebration (goldener "ERSTER SIEG!" Text + extra Gold-Partikel bei Level 1 Erstabschluss). Revival-Ad (5. Rewarded Placement, Ad-Unit-ID fehlt noch → Robert muss diese erstellen). Paid Continue (199 Coins, Alternative zu Ad). Level-Skip ab 2 Fails (vorher 3). Explosion DURATION 1.0→0.9s. Performance: DangerZone 5→3 Iterationen, AStar EmptyPath Singleton, Touch-Scale-Caching, _mechanicCells Grid-Cache. IProgressService.GetBaseScoreForLevel() für Near-Miss-Berechnung. 3 neue RESX-Keys (NearMissStars/PaidContinue/FirstVictory) in 6 Sprachen.
- **18.02.2026 (2)**: PowerUp-Freischaltungssystem + Discovery-Hints: PowerUps werden level-basiert freigeschaltet (12 Stufen: BombUp/Fire/Speed=1, Kick=10, Mystery=15, Skull/Wallpass=20, Detonator/Bombpass=25, LineBomb=30, Flamepass=35, PowerBomb=40). 4 Welt-Mechaniken ebenfalls (Ice=13, Conveyor=23, Teleporter=33, LavaCrack=42). LevelGenerator filtert gesperrte PowerUps in Story-Mode (Daily: alle verfügbar). Shop: 2 neue Sektionen "Fähigkeiten" + "Welt-Mechaniken" mit Lock/Unlock-Status. DiscoveryOverlay (SkiaSharp): Gold-Rahmen, NEU!-Badge, Titel+Beschreibung, Fade-In+Scale-Bounce, Auto-Dismiss 5s oder Tap, pausiert Spiel. IDiscoveryService (Preferences-basiert, Comma-separated HashSet). 34 neue RESX-Keys in 6 Sprachen. 4 neue Dateien: IDiscoveryService, DiscoveryService, DiscoveryOverlay, PowerUpDisplayItem.
- **18.02.2026 (3)**: Code-Cleanup: SpriteSheet.cs Platzhalter entfernt (samt DI + Referenzen), ProgressService.GetHighestCompletedLevel() (dead code), ParticleSystem._sparkPath (ungenutzt). ReviewService redundantes if-else vereinfacht. DiscoveryService doppelte Key-Generierung zu GetKeyFromId(id, suffix) zusammengeführt. ShopViewModel RefreshPowerUpItems/RefreshMechanicItems via CreateDisplayItem() konsolidiert. GameEngine: _discoveryHintActive durch _discoveryOverlay.IsActive ersetzt (doppelte Truth Source eliminiert), TryShowDiscoveryHint() Helper extrahiert. GameView.axaml.cs veralteten e.Info Fallback entfernt. ShopView.axaml MechanicItems Template: CardOpacity + bedingte Unlock-Borders (grün/Häkchen vs grau/Schloss) ergänzt.
- **18.02.2026**: Spieler-Stuck-Bug an Außenwänden gefixt: Grid-Bounds-Clamping verschärft (Hitbox darf nie in Außenwand-Zellen ragen, Minimum = CELL_SIZE + halfSize), Ice/Conveyor-Mechaniken auf 4-Ecken-Kollisionsprüfung umgestellt (vorher nur Mittel-Zelle → konnte Spieler in ungültige Position schieben), Stuck-Recovery eingebaut (nach 10 Frames ohne Bewegung trotz Input → Snap zum nächsten begehbaren Zellzentrum).
- **17.02.2026**: Explosions- und Bomben-Visuals komplett überarbeitet: CPU-basierte Flammen mit arm-basiertem Rendering (durchgehende Bezier-Pfade mit FBM-Noise-modulierten Rändern statt Pro-Zelle → nahtlose Übergänge), 3 Schichten (Glow + Hauptflamme + Kern) + Flammen-Zungen, natürliche Verjüngung zum Ende. Plasma-Energie-Ring um Bomben ab 50% Zündschnur, Wärme-Distortion (Heat Haze) über Explosions-Bereich, doppelter Shockwave-Ring (äußerer diffuser + innerer heller Ring), Partikel-System erweitert auf 300 mit 4 Formen (Rectangle/Circle/Spark/Ember) + Glow + Luftwiderstand + Rotation, 12 Funken-Partikel + 9 Glut-Partikel pro Explosion, Nachglühen mit Glow + hellem Kern, Funken-Glow-Halo am Bomben-Zünder. Datei: `Graphics/ExplosionShaders.cs`.
- **16.02.2026**: 4 neue SkiaSharp-Visualisierungen: HudVisualization (animierter Score-Counter mit rollenden Ziffern + pulsierender Timer unter 30s mit Farbwechsel normal→warning→critical + PowerUp-Icons mit Glow-Aura), LevelSelectVisualization (Level-Thumbnails mit 5 Welt-Farbpaletten + Gold-Shimmer Sterne + Lock-Overlay), AchievementIconRenderer (5 Kategorie-Farben + Trophy-Symbol bei freigeschaltet + Schloss+Fortschrittsring bei gesperrt), GameOverVisualization (großer Score mit pulsierendem Glow + Score-Breakdown Balken + Gold/Silber/Bronze Medaillen mit Shimmer + Coin-Counter mit Münz-Icon).
- **15.02.2026 (4)**: HelpView SkiaSharp-Icons: HelpIconRenderer.cs (statische DrawEnemy/DrawPowerUp Methoden, identische Render-Logik wie GameRenderer ohne Animationen), SKCanvasView (32x32) pro Gegner- und PowerUp-Karte in HelpView.axaml, 4 fehlende PowerUps ergänzt (Kick/LineBomb/PowerBomb/Skull), 8 RESX-Keys (Name+Desc) in 6 Sprachen, PaintSurface-Handler in HelpView.axaml.cs.
- **15.02.2026 (3)**: Daily Challenge Feature: IDailyChallengeService (Streak-System, Score-Tracking, Coin-Bonus 200-3000 pro Streak-Tag), DailyChallengeView mit Stats-Karten (Best Score, Streak, Longest Streak, Total Completed, Streak-Bonus), LevelGenerator.GenerateDailyChallengeLevel(seed) deterministisch aus Datum, GameEngine.StartDailyChallengeModeAsync + _isDailyChallenge Flag (kein Continue, kein NextLevel), MainMenu-Button (orange, #FF6B00), 9 RESX-Keys in 6 Sprachen, DI-Registrierung (15 Services + 11 ViewModels).
- **15.02.2026 (2)**: Welt-Mechaniken + Layout-Patterns + Balancing: 5 Welt-Mechaniken implementiert (Ice=40% Speed-Boost, Conveyor=40px/s Push, Teleporter=gepaarte Portale mit Cooldown, LavaCrack=periodischer Schaden 4s-Zyklus). 8 Layout-Patterns in GameGrid (Classic, Cross, Arena, Maze, TwoRooms, Spiral, Diagonal, BossArena). Boss-Ankündigung "BOSS FIGHT!" mit 2.5s Timer. SkiaSharp-Rendering für alle 4 neuen Zelltypen (Classic+Neon: Ice=blaue Reflexion+Shimmer, Conveyor=Metall+animierte Chevrons, Teleporter=rotierende Bogenringe farbcodiert, LavaCrack=Zickzack-Risse+pulsierendes Glühen). Shop-Balancing: ScoreMultiplier Gesamtkosten 55k→34k. Daily Reward: Streak-Reset Gnade 1→3 Tage. Grid-Align + Corner-Assist Bewegungsfix in Player.cs.
- **15.02.2026**: Steuerung vereinfacht: Swipe/DPad-Handler komplett entfernt (Dateien gelöscht), nur Joystick mit zwei Modi (Floating/Fixed). Settings: 3 RadioButtons → ToggleSwitch für "Fester Joystick". Bomb-Button repositioniert (80px/60px Offset statt 30px/20px). Banner-Ads im Gameplay deaktiviert (HideBanner beim Betreten, ShowBanner beim Verlassen). Neuer RESX-Key JoystickModeFixed in 6 Sprachen. InputManager-Migration für alte Swipe/DPad-Settings.
- **14.02.2026 (14)**: LevelSelect Redesign (WorldGroups mit farbigen Sektionen, UniformGrid 10-Spalten, Lock-Overlay), Tutorial-Overlay Fix (4-Rechteck-Dimming statt SaveLayer+Clear, reduziertes Alpha, DefeatEnemies-Schritt hinzugefügt, HUD-Overlap-Fix), ScrollViewer-Fix in 6 Views (Padding→Margin auf Kind-Element + VerticalScrollBarVisibility=Auto), Achievements-Button im MainMenu hinzugefügt.
- **13.02.2026 (13)**: Scroll-Padding + Coin-Anzeige Fix: Bottom-Padding in allen 6 ScrollViewern von 60dp auf 80dp erhoeht (ShopView, LevelSelectView, HighScoresView, HelpView, AchievementsView, SettingsView) + Bottom-Spacer in HelpView/SettingsView auf 80dp. LevelSelectViewModel: BalanceChanged-Subscription hinzugefuegt → CoinsText aktualisiert sich live bei Coin-Aenderungen (z.B. Rewarded Ad). IDisposable implementiert fuer saubere Event-Unsubscription.
- **13.02.2026 (12)**: Immersive-Mode-Fix: OnWindowFocusChanged Override hinzugefügt → EnableImmersiveMode() wird bei Fokus-Wechsel erneut aufgerufen (z.B. nach Ad-Anzeige, Alt-Tab). Vorher blieben Status-/Navigationsleiste nach Fokus-Verlust sichtbar. EnableImmersiveMode() refactored: Native WindowInsetsController (API 30+) + SystemUiFlags Fallback (< API 30).
- **13.02.2026 (11)**: Fullscreen + Ad-Spacer + Bugfixes: Fullscreen/Immersive Mode in MainActivity (OnCreate+OnResume, WindowInsetsController SystemBars hide + TransientBarsBySwipe), Ad-Banner-Spacer (MainView Panel→Grid mit 50dp Spacer Row, IsAdBannerVisible im MainViewModel mit AdsStateChanged-Event, versteckt im Game-View), Input-Reset Bug gefixt (\_inputManager.Reset() in LoadLevelAsync → keine Geister-Bewegung im nächsten Level), MainMenu-Partikel canvas.Clear(Transparent) → keine Partikel-Spuren mehr. Rewarded-Ad-Timeout 30s→8s (RewardedAdHelper). CelebrationOverlay 2.5s→1.5s, FloatingTextOverlay 1.5s→1.2s (betrifft alle Apps).
- **13.02.2026 (10)**: UI/UX-Overhaul (15 Punkte): Musik-Crossfade (ISoundService.SetMusicVolume, SoundManager.Update Fade-Logik), View-Transitions (CSS-Klassen PageView+Active mit Opacity DoubleTransition 200ms), 5 Welt-Farbpaletten (Forest/Industrial/Cavern/Sky/Inferno, WorldPalette in GameRenderer, Classic+Neon), Sterne-Animation bei Level-Complete (Scale-Bounce, gestaffelter Delay), PowerUp-Einsammel-Animation (Shrink+Spin+Fade 0.3s), Welt-Ankündigungen (großer Text bei Story-Welt-Wechsel), Coin-Floating-Text über Exit, GameButton-Style mit Scale-Transition (alle Menü-Views), Shop-Kauf-Feedback (Confetti+FloatingText bei Erfolg, roter Text bei zu wenig Coins), Achievement-Toast (AchievementUnlocked Event → goldener FloatingText), Coin-Counter-Animation (GameOverView zählt hoch), MainMenu-Hintergrund-Partikel (SKCanvasView, 25 farbige Punkte ~30fps), LevelSelect Welt-basierte Button-Farben, Tutorial-Replay Button in HelpView. 1 RESX-Key (ReplayTutorial) in 6 Sprachen.
- **13.02.2026 (9)**: Balancing + Shop-Erweiterung + Bug-Fix: Level-Complete Bug gefixt (StartGameLoop() fehlte nach Score-Verdopplungs-Overlay), HandleLevelComplete Delay 3s→1s (Engine hat eigene Iris-Wipe). Coin-Balancing: Score÷3=Coins (statt 1:1), Game-Over÷6 (statt ÷2), Effizienz-Bonus skaliert nach Welt (1-5). 3 neue Shop-Upgrades: ShieldStart (Cyan-Glow, absorbiert 1 Gegnerkontakt, 15.000), CoinBonus (+25%/+50%, 8.000/25.000), PowerUpLuck (1-2 extra PowerUps, 5.000/15.000). Shop-Gesamt: 190.000 Coins (vorher ~68.000). 6 RESX-Keys in 6 Sprachen.
- **13.02.2026 (8)**: Round 8 Feature-Implementation (6 Features aus Best-Practices-Recherche): Kick-Bomb Mechanik (Bomb.IsSliding/SlideDirection, UpdateBombSlide, TryKickBomb bei Spielerbewegung auf Bombe), Line-Bomb PowerUp (alle Bomben in Blickrichtung platzieren, PlaceLineBombs), Power-Bomb PowerUp (Mega-Bombe Range=FireRange+MaxBombs-1, verbraucht alle Slots), Skull/Curse System (4 CurseTypes: Diarrhea/Slow/Constipation/ReverseControls, 10s Dauer, violetter Glow), Danger Telegraphing (RenderDangerWarning pulsierend rot bei Zündschnur <0.8s), Squash/Stretch Animationen (Birth-Bounce Bomben 0.3s, Slide-Stretch 15%, Enemy-Tod Squash, Player-Tod 2-Phasen). PowerUpType.cs +4 Enum-Werte +CurseType Enum, Player.cs Curse-System +3 HasX Properties, Bomb.cs Kick/Slide, GameEngine.cs ReverseControls+Diarrhea+TryKickBomb, GameEngine.Explosion.cs PlacePowerBomb+PlaceLineBombs+UpdateBombSlide, GameRenderer.cs Danger+Squash/Stretch+4 neue PowerUp-Icons+Curse-HUD, LevelGenerator.cs neue PowerUps in Level-Progression.
- **13.02.2026 (7)**: Round 7 Deep-Analysis (alle Dateien, 16 Findings): Bugs: Achievement-Sterne vor Score-Speicherung geprüft → SetLevelBestScore in CompleteLevel() verschoben (B-R7-1/2), "DEFEAT ALL!" FloatingText Spam jeden Frame → 2s Cooldown (B-R7-3), LastEnemyKillPoints kumuliert statt Level-Score (B-R7-6), Speed-Boost PowerUp ineffektiv bei bestehendem Speed → SpeedLevel+1 (B-R7-7), PlayerDied-State stoppt Welt (Bomben/Explosionen/Gegner) → klassisches Bomberman-Verhalten (B-R7-15), Countdown "3-2-1" bei nur 2s → START_DELAY=3f (U-R7-1). Systematisch: (int)-Cast statt MathF.Floor bei Pixel→Grid in 4 Dateien (GameEngine.Explosion, CollisionHelper, GameGrid, GameRenderer) → alle 12 Stellen gefixt (B-R7-4/10/11/12). Tutorial-Warning-Timer nutzt Echtzeit statt Slow-Motion-deltaTime (B-R7-13). Gameplay: Exit-Platzierung weniger vorhersagbar → Zufallswahl aus Blöcken ab 60% Maximaldistanz (G-R7-1). Android-Crash: SettingsVM.OpenPrivacyPolicy Process.Start → UriLauncher.OpenUri (B-R7-16). Performance: HighScoreService.GetTopScores LINQ eliminiert (P-R7-1).
- **12.02.2026 (6)**: Round 6 Deep-Analysis + Komplett-Fixes: Bugs: Timer+Combo laufen in Echtzeit (kein Slow-Motion Exploit), Score-Multiplikator nur auf Level-Score, Victory-Coins Doppel-Credit gefixt, Exit-Prüfung inkl. Pontans + "DEFEAT ALL!" Feedback, Player.IsMarkedForRemoval entfernt, Pontan-Random als Klassenfeld, GridX/GridY mit MathF.Floor, GameOver Tap Race Condition gefixt. Achievements: IAchievementService in GameEngine injiziert (war komplett disconnected), automatische Prüfung bei Level-Complete/Kill/Wave/Stars, Speedrun-Logik gefixt (timeUsed statt timeRemaining), NoDamage-Tracking via Flag. Performance: DangerZone einmal pro Frame (PreCalculateDangerZone), GetTotalStars gecacht, ExplosionCell als struct.
- **12.02.2026 (5)**: Deep-Analyse + Komplett-Optimierung: Bugs: Coin-Inflation gefixt (Level-Score statt Total-Score), CoinService DateTime.Today→UtcNow.Date, Enemy-Spawn Fallback Wand-Check. Performance: RenderExit nutzt gecachte exitCell (150-Zellen-Iteration eliminiert). AI: Danger-Zone Kettenreaktionen (iterativ bis keine neuen Bomben), Low-Intel sofortige Umkehr bei Wand, Stuck-Timer 1.0→0.5s. Game-Feel: Slow-Motion bei letztem Kill/Combo x4+ (0.8s, 30%), Explosions-Shockwave (expandierender Ring 40%), Iris-Wipe Level-Transition (Kreis öffnet/schließt sich mit Gold-Rand-Glow). Code-Qualität: leere CheckWinCondition entfernt, Explosion.HasDealtDamage entfernt, Particle.IsActive entfernt. Pontan-Strafe gestaffelt (1/3s statt 4 sofort, Mindestabstand 5)
- **12.02.2026 (4)**: Deep-Code-Review + Optimierung: B1 Pause-Button Hit-Test X/Y-Fix (BannerTopOffset korrekt auf Y), B3 Enemy Hitbox harmonisiert (CanMoveTo 0.3→0.35 wie BBox), B4 ScreenShake Timer-Clamp (kein negativer Progress), P1 CheckExitReveal LINQ→manuelle Schleife, P2 AStar ReconstructPath gepoolte Liste, P3 Exit-Cell-Cache (kein Grid-Scan pro Frame), P4 Random-Seed Environment.TickCount statt DateTime.Millisecond, P5 Entity.Guid entfernt (16B/Entity gespart), C1 SpriteSheet Dead-Code entfernt (100+ Zeilen), G1 Explosions-Blitz (weißer Flash erste 20%), G2 Nachglühen (0.4s warmer Schimmer nach Explosion), G4 Bomben-Pulsation beschleunigt (8→24Hz je näher Explosion)
- **12.02.2026 (3)**: Game Juice: Combo-System (2s-Fenster, Bonus-Punkte, Floating Text), Score-Popups bei Enemy-Kill (+100/+400 gold), PowerUp-Collect-Text (+SPEED/+FIRE etc. farbig), Timer-Warnung (pulsierender roter Rand unter 30s), Speed-PowerUp staffelbar (Level 0-3, +20/Level), GameFloatingTextSystem (Struct-Pool 20 max, gecachte SKPaint/SKFont)
- **12.02.2026 (2)**: Bug-Fixes: Flamepass schützte fälschlich vor Gegnern (Kill()-Check entfernt), PlaceBlocks LINQ→Fisher-Yates, Gegner-Explosions-Kollision Rückwärts-Iteration, LevelComplete-Overlay nutzt gecachten LastTimeBonus, Exit-Mechanik klassisch (unter Block versteckt mit HasHiddenExit), ScreenShake Division-by-Zero beim ersten Trigger gefixt, SwipeGestureHandler setzt Richtung bei TouchEnd zurück (endlose Bewegung behoben), SFX_FUSE Sound beim Bomben-Platzieren, GameView DetachedFromVisualTree Cleanup (DispatcherTimer-Speicherleck behoben)
- **12.02.2026**: Umfangreiche Optimierung (9 Phasen): GameEngine in 5 Partial Classes aufgeteilt, Performance (Fisher-Yates, Exit-Cache, Array-Pooling), ScreenShake + Hit-Pause + Partikel-System, Tutorial (5 Schritte, SkiaSharp Overlay), Daily Reward (7-Tage-Zyklus), Spieler-Skins (5 Skins), In-App Review, 16 Achievements mit View, Android Audio-System (SoundPool + MediaPlayer, CC0 Assets von Juhani Junkala)
- **11.02.2026 (2)**: Umfangreicher Bug-Fix: DoubleScore Coins-Berechnung, PowerUpBoostDesc + BoostSpeed/Fire/Bomb RESX-Keys, Settings-Persistierung (InputManager + SoundManager), doppelte Render-Schleife entfernt, SKPath Memory-Leaks gefixt, per-Frame SKFont-Allokationen gecacht (DPadHandler/SwipeGestureHandler), doppelte Event-Subscriptions verhindert, Race-Condition in DestroyBlock, ShopVM IDisposable, GameRenderer per DI, ProgressService min. 1 Stern, SoundManager._currentMusic reset, PauseVM Events verbunden, SpawnPontan Zell-Validierung, Magic Numbers durch GameGrid.CELL_SIZE ersetzt, DateTime.UtcNow in HighScoreService, AdUnavailable Lambda-Leak gefixt
- **11.02.2026**: Banner-Ad im GameView erst ab Level 5, Top-Position (nicht stoerend fuer Controls/HUD/Sichtfeld). IAdService.SetBannerPosition + GameRenderer.BannerTopOffset
- **09.02.2026**: ShopVM.UpdateLocalizedTexts() bei Sprachwechsel, Nullable-Warnings in HighScoreService + ProgressService gefixt
- **08.02.2026**: FloatingText + Celebration Overlays, Ad-Banner Padding Fix
- **07.02.2026**: Score-Verdopplung, 4 Rewarded Ads, Coins-Economy + Shop, Neon Visual Fixes, Performance (Object Pooling AStar/EnemyAI)
- **06.02.2026**: Desktop Gameplay Fixes (DPI, Touch, Keyboard), Deep Code Review, 151 Lokalisierungs-Keys
