# BomberBlast (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Bomberman-Klon mit SkiaSharp Rendering, AI Pathfinding und mehreren Input-Methoden.
Landscape-only auf Android. Grid: 15x10. Zwei Visual Styles: Classic HD + Neon/Cyberpunk.

**Version:** 2.0.4 (VersionCode 13) | **Package-ID:** org.rsdigital.bomberblast | **Status:** Geschlossener Test

## Haupt-Features

### SkiaSharp Rendering (GameRenderer.cs)
- Volle 2D-Engine via SKCanvasView (Avalonia.Skia)
- Zwei Visual Styles: Classic HD + Neon/Cyberpunk (IGameStyleService)
- 60fps Game Loop via DispatcherTimer (16ms) in GameView.axaml.cs, InvalidateSurface() treibt PaintSurface
- DPI-Handling: `canvas.LocalClipBounds` statt `e.Info.Width/Height`
- GC-Optimierung: Gepoolte SKPaint/SKFont/SKPath (6 per-frame Allokationen eliminiert)
- HUD: Side-Panel rechts (TIME, SCORE, BOMBS/FIRE mit Mini-Icons, PowerUp-Liste mit Glow)

### Input-Handler (2x)
- **FloatingJoystick**: Touch-basiert, zwei Modi: Floating (erscheint wo getippt, Standard) + Fixed (immer sichtbar unten links). Bomb-Button weiter in die Spielfläche gerückt (80px/60px Offset statt 30px/20px)
- **Keyboard**: Arrow/WASD + Space (Bomb) + E (Detonate) + Escape (Pause) → Desktop Default
- InputManager verwaltet aktiven Handler, auto-detect Desktop vs Android, JoystickFixed-Setting persistiert

### AI (EnemyAI.cs + AStar.cs)
- A* Pathfinding (Object-Pooled PriorityQueue, HashSet, Dictionaries)
- BFS Safe-Cell Finder (Pooled Queues)
- Danger-Zone: **Einmal pro Frame** vorberechnet via `PreCalculateDangerZone()` (nicht pro Gegner)
- Kettenreaktions-Erkennung (iterativ, max 5 Durchläufe)
- 8 Enemy-Typen (unterschiedliche Speed/AI-Logik)

### Coin-Economy + Shop
- **CoinService**: Persistente Coin-Waehrung (Level-Score ÷ 3 → Coins bei Level-Complete, ÷ 6 bei Game Over)
- **Effizienz-Bonus**: Skaliert nach Welt (1-5), belohnt wenige Bomben (≤5/≤8/≤12)
- **ShopService**: 9 permanente Upgrades (StartBombs, StartFire, StartSpeed, ExtraLives, ScoreMultiplier, TimeBonus, ShieldStart, CoinBonus, PowerUpLuck)
- **Upgrade-Preise**: 1.500 - 35.000 Coins, Max-Levels: 1-3, Shop-Gesamtkosten: ~190.000 Coins
- **ShieldStart**: Spieler startet mit Schutzschild (absorbiert 1 Gegnerkontakt, Cyan-Glow)
- **CoinBonus**: +25%/+50% extra Coins pro Level
- **PowerUpLuck**: 1/2 zusaetzliche zufaellige PowerUps pro Level

### Level-Gating (ProgressService)
- 50 Story-Level in 5 Welten (World 1-5 a 10 Level) + Arcade-Modus
- Welt-Freischaltung: 0/10/25/45/70 Sterne
- Stern-System: 3 Sterne pro Level (Zeit-basiert)
- Fail-Counter fuer Level-Skip

## Premium & Ads

### Premium-Modell
- **Preis**: 3,99 EUR (`remove_ads`)
- Kostenlos spielbar, Upgrades grindbar, Ads optional

### Fullscreen/Immersive Mode (Android)
- **Aktivierung**: OnCreate + OnResume in MainActivity (WindowInsetsController)
- **Modus**: SystemBars ausgeblendet, TransientBarsBySwipe (Wisch-Geste zeigt Bars kurz an)
- **Landscape-Spiel**: Maximale Bildschirmfläche, keine Status-/Navigationsleiste

### Ad-Banner-Spacer (MainView)
- **MainView**: Grid mit `RowDefinitions="*,Auto"` → Row 0 Content-Panel, Row 1 Ad-Spacer (50dp)
- **IsAdBannerVisible**: Property im MainViewModel, gesteuert per Route (Game=false, andere=BannerVisible)
- **AdsStateChanged Event**: Reagiert auf Show/Hide des Banners
- **Dialoge/Overlays**: `Grid.RowSpan="2"` (über beide Rows, nicht abgeschnitten)

### Banner im GameView
- **Deaktiviert**: Kein Banner während Gameplay (seit 15.02.2026)
- Banner wird beim Betreten des GameView versteckt, beim Verlassen wieder angezeigt
- BannerTopOffset immer 0 (kein Viewport-Offset mehr nötig)

### Rewarded (4 Placements)
1. `continue` → GameOver: Coins verdoppeln / Weitermachen (1x pro Versuch)
2. `level_skip` → GameOver: Level ueberspringen (nach 3 Fails)
3. `power_up` → LevelSelect: Power-Up Boost (ab Level 20, alle 3 PowerUps)
4. `score_double` → GameView: Score verdoppeln (nach Level-Complete)

## App-spezifische Services

| Service | Zweck |
|---------|-------|
| ISoundService | Audio-Abstraktion (NullSoundService Desktop, AndroidSoundService Android) |
| IProgressService | Level-Fortschritt, Sterne, Fail-Counter, World-Gating |
| IHighScoreService | Top 10 Scores (sqlite-net-pcl) |
| IGameStyleService | Visual Style Persistenz (Classic/Neon) |
| ICoinService | Coin-Balance, AddCoins, TrySpendCoins |
| IShopService | PlayerUpgrades Persistenz, Preise, Kauf-Logik |
| ITutorialService | 6-Schritte Tutorial fuer Level 1 (Move, Bomb, Hide, PowerUp, DefeatEnemies, Exit) |
| IDailyRewardService | 7-Tage Daily Login Bonus (500-5000 Coins, Tag 5 Extra-Leben) |
| ICustomizationService | Spieler/Gegner-Skins (Default, Gold, Neon, Cyber, Retro) |
| IReviewService | In-App Review nach Level 3-5, 14-Tage Cooldown |
| IAchievementService | 16 Achievements in 5 Kategorien, JSON-Persistenz |
| IDailyChallengeService | Tägliche Herausforderung, Streak-Tracking, Score-Persistenz |

## Architektur-Entscheidungen

- **Game Loop**: DispatcherTimer (16ms) in GameView → InvalidateSurface() → OnPaintSurface → GameEngine.Update + Render
- **Touch-Koordinaten**: Proportionale Skalierung (Render-Bounds / Control-Bounds Ratio) fuer DPI-korrektes Mapping
- **Invalidierung**: IMMER `InvalidateSurface()` (InvalidateVisual feuert NICHT PaintSurface bei SKCanvasView)
- **Keyboard Input**: Window-Level KeyDown/KeyUp in MainWindow.axaml.cs → GameViewModel
- **DI**: 11 ViewModels, 14 Services, GameEngine + GameRenderer + SpriteSheet in App.axaml.cs (GameRenderer + IAchievementService per DI in GameEngine injiziert)
- **GameEngine Partial Classes**: GameEngine.cs (Kern), .Collision.cs, .Explosion.cs, .Level.cs, .Render.cs
- **12 PowerUp-Typen**: BombUp, Fire, Speed, Wallpass, Detonator, Bombpass, Flamepass, Mystery, Kick, LineBomb, PowerBomb, Skull
- **Exit-Cell-Cache**: `_exitCell` in GameEngine, gesetzt bei RevealExit/Block-Zerstörung → Kollisions-Check + RenderExit ohne Grid-Iteration
- **Coin-Berechnung**: `_scoreAtLevelStart` → Coins basieren auf Level-Score (nicht kumulierter Gesamtscore), verhindert Inflation
- **Pontan-Strafe**: Gestaffelt via Timer (`_pontanPunishmentActive`), nicht alle 4 auf einmal
- **Slow-Motion**: `_slowMotionFactor` auf deltaTime multipliziert in UpdatePlaying, Ease-Out Kurve
- **AI Danger-Zone**: Einmal pro Frame vorberechnet, iterative Kettenreaktions-Erkennung (max 5 Durchläufe)
- **Achievements**: IAchievementService in GameEngine injiziert, automatische Prüfung bei Level-Complete/Kill/Wave/Stars
- **ExplosionCell**: Struct statt Class (weniger Heap-Allokationen)
- **GetTotalStars**: Gecacht in ProgressService, invalidiert bei Score-Änderung
- **Score-Multiplikator**: Nur auf Level-Score angewendet (nicht kumulierten Gesamt-Score)
- **Timer**: Läuft in Echtzeit (`realDeltaTime`), nicht durch Slow-Motion beeinflusst

## Game Juice & Effects

- **FloatingText (UI)**: "x2!" (gold) bei Coins-Verdopplung, "LevelComplete" (gruen) - View-Overlays
- **In-Game FloatingText**: `Graphics/GameFloatingTextSystem.cs` - Struct-Pool (20 max), Score-Popups (+100, +400), Combo-Text (x2!, MEGA x5!), PowerUp-Collect-Text (+SPEED, +FIRE, +KICK, +LINE, +POWER, CURSED!)
- **Combo-System**: Kills innerhalb 2s-Fenster → Combo-Bonus (x2: +200, x3: +500, x4: +1000, x5+: +2000) mit farbigem Floating Text
- **Timer-Warnung**: Pulsierender roter Bildschirmrand unter 30s, Intensitaet steigt mit sinkender Zeit
- **Danger Telegraphing**: Rote pulsierende Warnzonen auf Zellen im Explosionsradius aktiver Bomben (Zuendschnur < 0.8s), Intensitaet steigt mit sinkender Zuendzeit
- **Celebration**: Confetti bei Welt-Freischaltung
- **ScreenShake**: Explosion (3px, 0.2s), PlayerDeath (5px, 0.3s) via `Graphics/ScreenShake.cs`, Timer-Clamp verhindert negativen Progress
- **Hit-Pause**: Frame-Freeze bei Enemy-Kill (50ms), Player-Death (100ms)
- **Partikel-System**: `Graphics/ParticleSystem.cs` - Struct-Pool (200 max), bei Block-Zerstoerung/Enemy-Kill/PowerUp/Exit
- **Explosions-Blitz**: Weißer Flash in den ersten 20% der Explosionsdauer (visueller Impact)
- **Explosions-Nachglühen**: 0.4s warmer Schimmer auf Zellen nach Explosionsende (Cell.AfterglowTimer)
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
- **Welt-Themes**: 5 Farbpaletten pro Style (Forest/Industrial/Cavern/Sky/Inferno), WorldPalette in GameRenderer
- **Sterne-Animation**: 3 Sterne bei Level-Complete mit gestaffelter Scale-Bounce Animation (0.3s Delay)
- **PowerUp-Einsammel-Animation**: Shrink + Spin + Fade (0.3s) bei Collect statt sofortigem Entfernen
- **Welt-/Wave-Ankündigung**: Großer "WORLD X!" / "WAVE X!" Text bei Welt-Wechsel (Story) und Meilensteinen (Arcade, alle 5 Waves)
- **Coin-Floating-Text**: "+X Coins" (gold) über dem Exit bei Level-Complete
- **Button-Animationen**: GameButton-Style mit Scale-Transition (1.05x hover, 0.95x pressed) in allen Menüs
- **Shop-Kauf-Feedback**: PurchaseSucceeded → Confetti + FloatingText, InsufficientFunds → roter FloatingText
- **Achievement-Toast**: AchievementUnlocked Event → goldener FloatingText "Achievement: [Name]!"
- **Coin-Counter-Animation**: GameOverView zählt Coins von 0 hoch (~30 Frames, DispatcherTimer)
- **MainMenu-Hintergrund**: SKCanvasView Partikelsystem (25 farbige Punkte, langsam aufsteigend, ~30fps)
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

## Daily Reward & Monetarisierung (Phase 6)

- **7-Tage-Zyklus**: 500/1000/1500/2000/2500/3000/5000 Coins, Tag 5 Extra-Leben
- **Streak-Tracking**: UTC-basiert, Reset bei verpasstem Tag
- **Spieler-Skins**: Default, Gold, Neon, Cyber, Retro (Premium-Only: Gold, Neon, Cyber, Retro)
- **In-App Review**: Nach Level 3-5, 14-Tage Cooldown

## Achievement-System (Phase 7)

- 16 Achievements in 5 Kategorien: Progress (5), Mastery (3), Combat (3), Skill (3), Arcade (2)
- JSON-Persistenz via IPreferencesService
- **IAchievementService in GameEngine injiziert** → automatische Achievement-Prüfung bei:
  - Level-Complete → OnLevelCompleted (Welten, NoDamage, Efficient, Speedrun)
  - Enemy-Kill → OnEnemyKilled (kumulative Kills 100/500/1000)
  - Arcade Wave → OnArcadeWaveReached (Wave 10/25)
  - Stars → OnStarsUpdated (50/100/150 Sterne)
- **Speedrun-Fix**: Prüft `timeUsed <= 60s` (nicht `timeRemaining >= 60s`)
- **NoDamage-Tracking**: `_playerDamagedThisLevel` Flag in GameEngine
- AchievementsView mit Karten-Grid (Icon + Name + Beschreibung + Fortschritt)
- RESX-Keys fuer 6 Sprachen (33 Keys: AchievementsTitle + 16x Name/Desc)

## Audio-System (Phase 8)

- **AndroidSoundService** (`BomberBlast.Android/AndroidSoundService.cs`)
  - SoundPool fuer SFX (12 Sounds: explosion, place_bomb, fuse, powerup, player_death, enemy_death, exit_appear, level_complete, game_over, time_warning, menu_select, menu_confirm)
  - MediaPlayer fuer Musik (4 Tracks: menu, gameplay, boss, victory)
  - Assets in `Assets/Sounds/` (.ogg + .wav, versucht beide Formate)
- **SoundManager** (`Core/SoundManager.cs`): Wraps ISoundService mit Lautstaerke-/Enable-Settings, Crossfade-Logik (Update() Methode, Fade-Out/Fade-In bei Track-Wechsel)
- **ISoundService.SetMusicVolume(float)**: Für Crossfade-Steuerung (AndroidSoundService: MediaPlayer.SetVolume)
- **SoundServiceFactory** in App.axaml.cs (analog RewardedAdServiceFactory)
- **Sound-Assets**: CC0 Lizenz, Juhani Junkala (OpenGameArt.org), ~8.5 MB gesamt

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
- **Visuell**: Farbiger Floating Text (Orange x2-x3, Rot ab x4, "MEGA" ab x5)

### Slow-Motion
- **Trigger**: Letzter Gegner getötet ODER Combo x4+
- **Dauer**: 0.8s Echtzeit, Faktor 0.3 (30% Geschwindigkeit)
- **Easing**: Ease-Out (langsam → normal)
- **Timer/Combo**: Laufen in Echtzeit (`realDeltaTime`), nicht verlangsamt (kein Exploit)
- **Felder**: `_slowMotionTimer` + `_slowMotionFactor` in GameEngine

### Pontan-Strafe (Timer-Ablauf)
- **Gestaffeltes Spawning**: 1 Pontan alle 3s statt 4 auf einmal
- **Mindestabstand**: 5 Zellen zum Spieler (statt vorher 4)
- **Spawn-Partikel**: Rote Partikel am Spawn-Punkt
- **Timer-Felder**: `_pontanPunishmentActive` + `_pontanSpawned` + `_pontanSpawnTimer`

### Iris-Wipe Transition
- **Level-Start**: Schwarzer Kreis öffnet sich vom Zentrum (SKPath mit CounterClockwise Clip)
- **Level-Complete**: Kreis schließt sich in der letzten Sekunde
- **Goldener Rand-Glow**: Ring am Iris-Rand bei Level-Start

### Explosions-Shockwave
- **Expandierender Ring**: In ersten 40% der Explosionsdauer
- **Radius**: Wächst von 0 bis Bomben-Range * CELL_SIZE
- **Stroke**: Wird dünner werdend (4px→2px), Farbe = ExplosionCore

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
- **Verfügbar**: Ab Level 26 (Story), ab Wave 5 (Arcade)

### Power-Bomb PowerUp
- **Aktivierung**: Wenn `HasPowerBomb` + `ActiveBombs == 0` → `PlacePowerBomb()`
- **Reichweite**: `FireRange + MaxBombs - 1` (skaliert mit Upgrades)
- **Slot-Verbrauch**: Belegt ALLE Bomb-Slots (verhindert Spam)
- **Verfügbar**: Ab Level 36 (Story), ab Wave 8 (Arcade)

### Skull/Curse System
- **Skull-PowerUp**: Negatives PowerUp, aktiviert zufälligen Curse für 10 Sekunden
- **4 Curse-Typen**:
  - `Diarrhea`: Automatische Bombenplatzierung alle 0.5s
  - `Slow`: Bewegungsgeschwindigkeit halbiert
  - `Constipation`: Kann keine Bomben platzieren
  - `ReverseControls`: Richtungseingaben invertiert
- **Visuell**: Pulsierender violetter Glow, HUD-Anzeige mit Timer und Typ-Abkürzung
- **Verfügbar**: Ab Level 20 (Story), ab Wave 5 mit 40% Chance (Arcade, max 1)

### Danger Telegraphing
- **Bedingung**: Nicht-manuelle Bomben mit Zündschnur < 0.8s
- **Darstellung**: Rote pulsierende Overlay-Zellen im Explosionsradius (4 Richtungen)
- **Intensität**: Steigt mit sinkender Zündzeit, Puls-Effekt (sin-basiert)
- **Berechnung**: Read-only Spread im Renderer (keine State-Mutation)

## Changelog Highlights

- **15.02.2026 (4)**: HelpView SkiaSharp-Icons: HelpIconRenderer.cs (statische DrawEnemy/DrawPowerUp Methoden, identische Render-Logik wie GameRenderer ohne Animationen), SKCanvasView (32x32) pro Gegner- und PowerUp-Karte in HelpView.axaml, 4 fehlende PowerUps ergänzt (Kick/LineBomb/PowerBomb/Skull), 8 RESX-Keys (Name+Desc) in 6 Sprachen, PaintSurface-Handler in HelpView.axaml.cs.
- **15.02.2026 (3)**: Daily Challenge Feature: IDailyChallengeService (Streak-System, Score-Tracking, Coin-Bonus 200-3000 pro Streak-Tag), DailyChallengeView mit Stats-Karten (Best Score, Streak, Longest Streak, Total Completed, Streak-Bonus), LevelGenerator.GenerateDailyChallengeLevel(seed) deterministisch aus Datum, GameEngine.StartDailyChallengeModeAsync + _isDailyChallenge Flag (kein Continue, kein NextLevel), MainMenu-Button (orange, #FF6B00), 9 RESX-Keys in 6 Sprachen, DI-Registrierung (14 Services + 11 ViewModels).
- **15.02.2026 (2)**: Welt-Mechaniken + Layout-Patterns + Balancing: 5 Welt-Mechaniken implementiert (Ice=40% Speed-Boost, Conveyor=40px/s Push, Teleporter=gepaarte Portale mit Cooldown, LavaCrack=periodischer Schaden 4s-Zyklus). 8 Layout-Patterns in GameGrid (Classic, Cross, Arena, Maze, TwoRooms, Spiral, Diagonal, BossArena). Boss-Ankündigung "BOSS FIGHT!" mit 2.5s Timer. SkiaSharp-Rendering für alle 4 neuen Zelltypen (Classic+Neon: Ice=blaue Reflexion+Shimmer, Conveyor=Metall+animierte Chevrons, Teleporter=rotierende Bogenringe farbcodiert, LavaCrack=Zickzack-Risse+pulsierendes Glühen). Shop-Balancing: ScoreMultiplier Gesamtkosten 55k→34k. Daily Reward: Streak-Reset Gnade 1→3 Tage. Grid-Align + Corner-Assist Bewegungsfix in Player.cs.
- **15.02.2026**: Steuerung vereinfacht: Swipe/DPad-Handler komplett entfernt (Dateien gelöscht), nur Joystick mit zwei Modi (Floating/Fixed). Settings: 3 RadioButtons → ToggleSwitch für "Fester Joystick". Bomb-Button repositioniert (80px/60px Offset statt 30px/20px). Banner-Ads im Gameplay deaktiviert (HideBanner beim Betreten, ShowBanner beim Verlassen). Neuer RESX-Key JoystickModeFixed in 6 Sprachen. InputManager-Migration für alte Swipe/DPad-Settings.
- **14.02.2026 (14)**: LevelSelect Redesign (WorldGroups mit farbigen Sektionen, UniformGrid 10-Spalten, Lock-Overlay), Tutorial-Overlay Fix (4-Rechteck-Dimming statt SaveLayer+Clear, reduziertes Alpha, DefeatEnemies-Schritt hinzugefügt, HUD-Overlap-Fix), ScrollViewer-Fix in 6 Views (Padding→Margin auf Kind-Element + VerticalScrollBarVisibility=Auto), Achievements-Button im MainMenu hinzugefügt.
- **13.02.2026 (13)**: Scroll-Padding + Coin-Anzeige Fix: Bottom-Padding in allen 6 ScrollViewern von 60dp auf 80dp erhoeht (ShopView, LevelSelectView, HighScoresView, HelpView, AchievementsView, SettingsView) + Bottom-Spacer in HelpView/SettingsView auf 80dp. LevelSelectViewModel: BalanceChanged-Subscription hinzugefuegt → CoinsText aktualisiert sich live bei Coin-Aenderungen (z.B. Rewarded Ad). IDisposable implementiert fuer saubere Event-Unsubscription.
- **13.02.2026 (12)**: Immersive-Mode-Fix: OnWindowFocusChanged Override hinzugefügt → EnableImmersiveMode() wird bei Fokus-Wechsel erneut aufgerufen (z.B. nach Ad-Anzeige, Alt-Tab). Vorher blieben Status-/Navigationsleiste nach Fokus-Verlust sichtbar. EnableImmersiveMode() refactored: Native WindowInsetsController (API 30+) + SystemUiFlags Fallback (< API 30).
- **13.02.2026 (11)**: Fullscreen + Ad-Spacer + Bugfixes: Fullscreen/Immersive Mode in MainActivity (OnCreate+OnResume, WindowInsetsController SystemBars hide + TransientBarsBySwipe), Ad-Banner-Spacer (MainView Panel→Grid mit 50dp Spacer Row, IsAdBannerVisible im MainViewModel mit AdsStateChanged-Event, versteckt im Game-View), Input-Reset Bug gefixt (\_inputManager.Reset() in LoadLevelAsync → keine Geister-Bewegung im nächsten Level), MainMenu-Partikel canvas.Clear(Transparent) → keine Partikel-Spuren mehr. Rewarded-Ad-Timeout 30s→8s (RewardedAdHelper). CelebrationOverlay 2.5s→1.5s, FloatingTextOverlay 1.5s→1.2s (betrifft alle Apps).
- **13.02.2026 (10)**: UI/UX-Overhaul (15 Punkte): Musik-Crossfade (ISoundService.SetMusicVolume, SoundManager.Update Fade-Logik), View-Transitions (CSS-Klassen PageView+Active mit Opacity DoubleTransition 200ms), 5 Welt-Farbpaletten (Forest/Industrial/Cavern/Sky/Inferno, WorldPalette in GameRenderer, Classic+Neon), Sterne-Animation bei Level-Complete (Scale-Bounce, gestaffelter Delay), PowerUp-Einsammel-Animation (Shrink+Spin+Fade 0.3s), Welt-/Wave-Ankündigungen (großer Text bei Story-Welt-Wechsel + Arcade-Wave-Meilensteine), Coin-Floating-Text über Exit, GameButton-Style mit Scale-Transition (alle Menü-Views), Shop-Kauf-Feedback (Confetti+FloatingText bei Erfolg, roter Text bei zu wenig Coins), Achievement-Toast (AchievementUnlocked Event → goldener FloatingText), Coin-Counter-Animation (GameOverView zählt hoch), MainMenu-Hintergrund-Partikel (SKCanvasView, 25 farbige Punkte ~30fps), LevelSelect Welt-basierte Button-Farben, Tutorial-Replay Button in HelpView. 1 RESX-Key (ReplayTutorial) in 6 Sprachen.
- **13.02.2026 (9)**: Balancing + Shop-Erweiterung + Bug-Fix: Level-Complete Bug gefixt (StartGameLoop() fehlte nach Score-Verdopplungs-Overlay), HandleLevelComplete Delay 3s→1s (Engine hat eigene Iris-Wipe). Coin-Balancing: Score÷3=Coins (statt 1:1), Game-Over÷6 (statt ÷2), Effizienz-Bonus skaliert nach Welt (1-5). 3 neue Shop-Upgrades: ShieldStart (Cyan-Glow, absorbiert 1 Gegnerkontakt, 15.000), CoinBonus (+25%/+50%, 8.000/25.000), PowerUpLuck (1-2 extra PowerUps, 5.000/15.000). Shop-Gesamt: 190.000 Coins (vorher ~68.000). 6 RESX-Keys in 6 Sprachen.
- **13.02.2026 (8)**: Round 8 Feature-Implementation (6 Features aus Best-Practices-Recherche): Kick-Bomb Mechanik (Bomb.IsSliding/SlideDirection, UpdateBombSlide, TryKickBomb bei Spielerbewegung auf Bombe), Line-Bomb PowerUp (alle Bomben in Blickrichtung platzieren, PlaceLineBombs), Power-Bomb PowerUp (Mega-Bombe Range=FireRange+MaxBombs-1, verbraucht alle Slots), Skull/Curse System (4 CurseTypes: Diarrhea/Slow/Constipation/ReverseControls, 10s Dauer, violetter Glow), Danger Telegraphing (RenderDangerWarning pulsierend rot bei Zündschnur <0.8s), Squash/Stretch Animationen (Birth-Bounce Bomben 0.3s, Slide-Stretch 15%, Enemy-Tod Squash, Player-Tod 2-Phasen). PowerUpType.cs +4 Enum-Werte +CurseType Enum, Player.cs Curse-System +3 HasX Properties, Bomb.cs Kick/Slide, GameEngine.cs ReverseControls+Diarrhea+TryKickBomb, GameEngine.Explosion.cs PlacePowerBomb+PlaceLineBombs+UpdateBombSlide, GameRenderer.cs Danger+Squash/Stretch+4 neue PowerUp-Icons+Curse-HUD, LevelGenerator.cs neue PowerUps in Level-Progression+Arcade-Pool.
- **13.02.2026 (7)**: Round 7 Deep-Analysis (alle Dateien, 16 Findings): Bugs: Achievement-Sterne vor Score-Speicherung geprüft → SetLevelBestScore in CompleteLevel() verschoben (B-R7-1/2), "DEFEAT ALL!" FloatingText Spam jeden Frame → 2s Cooldown (B-R7-3), LastEnemyKillPoints kumuliert statt Level-Score (B-R7-6), Speed-Boost PowerUp ineffektiv bei bestehendem Speed → SpeedLevel+1 (B-R7-7), Redundantes Lives=1 in Arcade entfernt (B-R7-8), PlayerDied-State stoppt Welt (Bomben/Explosionen/Gegner) → klassisches Bomberman-Verhalten (B-R7-15), Countdown "3-2-1" bei nur 2s → START_DELAY=3f (U-R7-1). Systematisch: (int)-Cast statt MathF.Floor bei Pixel→Grid in 4 Dateien (GameEngine.Explosion, CollisionHelper, GameGrid, GameRenderer) → alle 12 Stellen gefixt (B-R7-4/10/11/12). Tutorial-Warning-Timer nutzt Echtzeit statt Slow-Motion-deltaTime (B-R7-13). Gameplay: Exit-Platzierung weniger vorhersagbar → Zufallswahl aus Blöcken ab 60% Maximaldistanz (G-R7-1). Android-Crash: SettingsVM.OpenPrivacyPolicy Process.Start → UriLauncher.OpenUri (B-R7-16). Performance: HighScoreService.GetTopScores LINQ eliminiert (P-R7-1).
- **12.02.2026 (6)**: Round 6 Deep-Analysis + Komplett-Fixes: Bugs: Timer+Combo laufen in Echtzeit (kein Slow-Motion Exploit), Score-Multiplikator nur auf Level-Score, Victory-Coins Doppel-Credit gefixt, Exit-Prüfung inkl. Pontans + "DEFEAT ALL!" Feedback, Player.IsMarkedForRemoval entfernt, Pontan-Random als Klassenfeld, GridX/GridY mit MathF.Floor, GameOver Tap Race Condition gefixt. Achievements: IAchievementService in GameEngine injiziert (war komplett disconnected), automatische Prüfung bei Level-Complete/Kill/Wave/Stars, Speedrun-Logik gefixt (timeUsed statt timeRemaining), NoDamage-Tracking via Flag. Performance: DangerZone einmal pro Frame (PreCalculateDangerZone), GetTotalStars gecacht, ExplosionCell als struct.
- **12.02.2026 (5)**: Deep-Analyse + Komplett-Optimierung: Bugs: Arcade-Seed DateTime.Now→Environment.TickCount, Coin-Inflation gefixt (Level-Score statt Total-Score), CoinService DateTime.Today→UtcNow.Date, Enemy-Spawn Fallback Wand-Check. Performance: RenderExit nutzt gecachte exitCell (150-Zellen-Iteration eliminiert). AI: Danger-Zone Kettenreaktionen (iterativ bis keine neuen Bomben), Low-Intel sofortige Umkehr bei Wand, Stuck-Timer 1.0→0.5s. Game-Feel: Slow-Motion bei letztem Kill/Combo x4+ (0.8s, 30%), Explosions-Shockwave (expandierender Ring 40%), Iris-Wipe Level-Transition (Kreis öffnet/schließt sich mit Gold-Rand-Glow). Code-Qualität: leere CheckWinCondition entfernt, Explosion.HasDealtDamage entfernt, Particle.IsActive entfernt. Pontan-Strafe gestaffelt (1/3s statt 4 sofort, Mindestabstand 5)
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
