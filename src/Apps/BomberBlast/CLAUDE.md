# BomberBlast (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Bomberman-Klon mit SkiaSharp Rendering, AI Pathfinding und mehreren Input-Methoden.
Landscape-only auf Android. Grid: 15x10. Zwei Visual Styles: Classic HD + Neon/Cyberpunk.

**Version:** 2.0.0 | **Package-ID:** org.rsdigital.bomberblast | **Status:** Geschlossener Test

## Haupt-Features

### SkiaSharp Rendering (GameRenderer.cs)
- Volle 2D-Engine via SKCanvasView (Avalonia.Skia)
- Zwei Visual Styles: Classic HD + Neon/Cyberpunk (IGameStyleService)
- 60fps Game Loop (render-driven via GameViewModel.InvalidateCanvasRequested, kein DispatcherTimer)
- DPI-Handling: `canvas.LocalClipBounds` statt `e.Info.Width/Height`
- GC-Optimierung: Gepoolte SKPaint/SKFont/SKPath (6 per-frame Allokationen eliminiert)
- HUD: Side-Panel rechts (TIME, SCORE, BOMBS/FIRE mit Mini-Icons, PowerUp-Liste mit Glow)

### Input-Handler (4x)
- **FloatingJoystick**: Touch-basiert, draggable (Android Default)
- **SwipeGesture**: Wisch-Richtung
- **ClassicDPad**: On-screen D-Pad Buttons
- **Keyboard**: Arrow/WASD + Space (Bomb) + E (Detonate) + Escape (Pause) → Desktop Default
- InputManager verwaltet aktiven Handler, auto-detect Desktop vs Android

### AI (EnemyAI.cs + AStar.cs)
- A* Pathfinding (Object-Pooled PriorityQueue, HashSet, Dictionaries)
- BFS Safe-Cell Finder (Pooled Queues)
- Danger-Zone Calculation (Pooled HashSet, manuelle Loops statt LINQ)
- 8 Enemy-Typen (unterschiedliche Speed/AI-Logik)

### Coin-Economy + Shop
- **CoinService**: Persistente Coin-Waehrung (Score → Coins: 1:1 bei Level-Complete, 0.5x bei Game Over)
- **ShopService**: 6 permanente Upgrades (StartBombs, StartFire, StartSpeed, ExtraLives, ScoreMultiplier, TimeBonus)
- **Upgrade-Preise**: 3.000 - 75.000 Coins, Max-Levels: 1-3

### Level-Gating (ProgressService)
- 50 Story-Level in 5 Welten (World 1-5 a 10 Level) + Arcade-Modus
- Welt-Freischaltung: 0/10/25/45/70 Sterne
- Stern-System: 3 Sterne pro Level (Zeit-basiert)
- Fail-Counter fuer Level-Skip

## Premium & Ads

### Premium-Modell
- **Preis**: 3,99 EUR (`remove_ads`)
- Kostenlos spielbar, Upgrades grindbar, Ads optional

### Banner im GameView (Level-basiert)
- **Level 1-4**: Kein Banner (ungestoertes Spielerlebnis)
- **Ab Level 5**: Banner oben (Top-Position), Viewport verschiebt sich nach unten
- **GameRenderer.BannerTopOffset** (55f): Grid + HUD werden nach unten verschoben, Controls (D-Pad/Bomb) bleiben unten
- **IAdService.SetBannerPosition(true)**: Wechselt nativen Android-Banner von Bottom auf Top
- Beim Verlassen des GameView: Position zurueck auf Bottom (Standard fuer andere Views)

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
| ITutorialService | 5-Schritte Tutorial fuer Level 1 (Move, Bomb, Hide, PowerUp, Exit) |
| IDailyRewardService | 7-Tage Daily Login Bonus (500-5000 Coins, Tag 5 Extra-Leben) |
| ICustomizationService | Spieler/Gegner-Skins (Default, Gold, Neon, Cyber, Retro) |
| IReviewService | In-App Review nach Level 3-5, 14-Tage Cooldown |
| IAchievementService | 16 Achievements in 5 Kategorien, JSON-Persistenz |

## Architektur-Entscheidungen

- **Game Loop**: Render-driven Update (OnPaintSurface macht GameEngine.Update + Render)
- **Touch-Koordinaten**: Proportionale Skalierung (Render-Bounds / Control-Bounds Ratio) fuer DPI-korrektes Mapping
- **Invalidierung**: IMMER `InvalidateSurface()` (InvalidateVisual feuert NICHT PaintSurface bei SKCanvasView)
- **Keyboard Input**: Window-Level KeyDown/KeyUp in MainWindow.axaml.cs → GameViewModel
- **DI**: 10 ViewModels, 13 Services, GameEngine + GameRenderer + SpriteSheet in App.axaml.cs (GameRenderer per DI in GameEngine injiziert)
- **GameEngine Partial Classes**: GameEngine.cs (Kern), .Collision.cs, .Explosion.cs, .Level.cs, .Render.cs

## Game Juice & Effects

- **FloatingText**: "x2!" (gold) bei Coins-Verdopplung, "LevelComplete" (gruen)
- **Celebration**: Confetti bei Welt-Freischaltung
- **ScreenShake**: Explosion (3px, 0.2s), PlayerDeath (5px, 0.3s) via `Graphics/ScreenShake.cs`
- **Hit-Pause**: Frame-Freeze bei Enemy-Kill (50ms), Player-Death (100ms)
- **Partikel-System**: `Graphics/ParticleSystem.cs` - Struct-Pool (200 max), bei Block-Zerstoerung/Enemy-Kill/PowerUp/Exit
- **Walk-Animation**: Prozedurales Wippen (sin-basiert) bei Spieler-/Gegnerbewegung
- **Neon Style**: Brightened Palette, 3D Block-Edges, Glow-Cracks, Outer-Glow HUD-Text
- **Mini-Icons**: Bomb/Flame Icons statt "B"/"F" Labels im HUD

## Tutorial-System (Phase 5)

- 5 interaktive Schritte: Move → PlaceBomb → Warning(Hide) → CollectPowerUp → FindExit
- Automatischer Start bei Level 1 wenn kein Fortschritt
- SkiaSharp Overlay (`Graphics/TutorialOverlay.cs`) mit Highlight-Box, Pfeil, Text-Bubble
- Skip-Button in jedem Schritt, Warning-Schritt mit 3s Auto-Advance
- RESX-Keys fuer 6 Sprachen (TutorialMove/Bomb/Hide/PowerUp/Exit/Skip)

## Daily Reward & Monetarisierung (Phase 6)

- **7-Tage-Zyklus**: 500/1000/1500/2000/2500/3000/5000 Coins, Tag 5 Extra-Leben
- **Streak-Tracking**: UTC-basiert, Reset bei verpasstem Tag
- **Spieler-Skins**: Default, Gold, Neon, Cyber, Retro (Premium-Only: Gold, Neon, Cyber, Retro)
- **In-App Review**: Nach Level 3-5, 14-Tage Cooldown

## Achievement-System (Phase 7)

- 16 Achievements in 5 Kategorien: Progress (5), Mastery (3), Combat (3), Skill (3), Arcade (2)
- JSON-Persistenz via IPreferencesService
- AchievementsView mit Karten-Grid (Icon + Name + Beschreibung + Fortschritt)
- RESX-Keys fuer 6 Sprachen (33 Keys: AchievementsTitle + 16x Name/Desc)

## Audio-System (Phase 8)

- **AndroidSoundService** (`BomberBlast.Android/AndroidSoundService.cs`)
  - SoundPool fuer SFX (12 Sounds: explosion, place_bomb, fuse, powerup, player_death, enemy_death, exit_appear, level_complete, game_over, time_warning, menu_select, menu_confirm)
  - MediaPlayer fuer Musik (4 Tracks: menu, gameplay, boss, victory)
  - Assets in `Assets/Sounds/` (.ogg + .wav, versucht beide Formate)
- **SoundManager** (`Core/SoundManager.cs`): Wraps ISoundService mit Lautstaerke-/Enable-Settings
- **SoundServiceFactory** in App.axaml.cs (analog RewardedAdServiceFactory)
- **Sound-Assets**: CC0 Lizenz, Juhani Junkala (OpenGameArt.org), ~8.5 MB gesamt

## Changelog Highlights

- **12.02.2026**: Umfangreiche Optimierung (9 Phasen): GameEngine in 5 Partial Classes aufgeteilt, Performance (Fisher-Yates, Exit-Cache, Array-Pooling), ScreenShake + Hit-Pause + Partikel-System, Tutorial (5 Schritte, SkiaSharp Overlay), Daily Reward (7-Tage-Zyklus), Spieler-Skins (5 Skins), In-App Review, 16 Achievements mit View, Android Audio-System (SoundPool + MediaPlayer, CC0 Assets von Juhani Junkala)
- **11.02.2026 (2)**: Umfangreicher Bug-Fix: DoubleScore Coins-Berechnung, PowerUpBoostDesc + BoostSpeed/Fire/Bomb RESX-Keys, Settings-Persistierung (InputManager + SoundManager), doppelte Render-Schleife entfernt, SKPath Memory-Leaks gefixt, per-Frame SKFont-Allokationen gecacht (DPadHandler/SwipeGestureHandler), doppelte Event-Subscriptions verhindert, Race-Condition in DestroyBlock, ShopVM IDisposable, GameRenderer per DI, ProgressService min. 1 Stern, SoundManager._currentMusic reset, PauseVM Events verbunden, SpawnPontan Zell-Validierung, Magic Numbers durch GameGrid.CELL_SIZE ersetzt, DateTime.UtcNow in HighScoreService, AdUnavailable Lambda-Leak gefixt
- **11.02.2026**: Banner-Ad im GameView erst ab Level 5, Top-Position (nicht stoerend fuer Controls/HUD/Sichtfeld). IAdService.SetBannerPosition + GameRenderer.BannerTopOffset
- **09.02.2026**: ShopVM.UpdateLocalizedTexts() bei Sprachwechsel, Nullable-Warnings in HighScoreService + ProgressService gefixt
- **08.02.2026**: FloatingText + Celebration Overlays, Ad-Banner Padding Fix
- **07.02.2026**: Score-Verdopplung, 4 Rewarded Ads, Coins-Economy + Shop, Neon Visual Fixes, Performance (Object Pooling AStar/EnemyAI)
- **06.02.2026**: Desktop Gameplay Fixes (DPI, Touch, Keyboard), Deep Code Review, 151 Lokalisierungs-Keys
