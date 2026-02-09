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
- 60fps Game Loop (DispatcherTimer, InvalidateSurface alle 16ms)
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

### Rewarded (4 Placements)
1. `continue` → GameOver: Coins verdoppeln / Weitermachen (1x pro Versuch)
2. `level_skip` → GameOver: Level ueberspringen (nach 3 Fails)
3. `power_up` → LevelSelect: Power-Up Boost (ab Level 20, alle 3 PowerUps)
4. `score_double` → GameView: Score verdoppeln (nach Level-Complete)

## App-spezifische Services

| Service | Zweck |
|---------|-------|
| ISoundService | Audio-Abstraktion (NullSoundService = Default) |
| IProgressService | Level-Fortschritt, Sterne, Fail-Counter, World-Gating |
| IHighScoreService | Top 10 Scores (sqlite-net-pcl) |
| IGameStyleService | Visual Style Persistenz (Classic/Neon) |
| ICoinService | Coin-Balance, AddCoins, TrySpendCoins |
| IShopService | PlayerUpgrades Persistenz, Preise, Kauf-Logik |

## Architektur-Entscheidungen

- **Game Loop**: Render-driven Update (OnPaintSurface macht GameEngine.Update + Render)
- **Touch-Koordinaten**: Proportionale Skalierung (Render-Bounds / Control-Bounds Ratio) fuer DPI-korrektes Mapping
- **Invalidierung**: IMMER `InvalidateSurface()` (InvalidateVisual feuert NICHT PaintSurface bei SKCanvasView)
- **Keyboard Input**: Window-Level KeyDown/KeyUp in MainWindow.axaml.cs → GameViewModel
- **DI**: 10 ViewModels, 8 Services, GameEngine + SpriteSheet in App.axaml.cs

## Game Juice

- **FloatingText**: "x2!" (gold) bei Coins-Verdopplung, "LevelComplete" (gruen)
- **Celebration**: Confetti bei Welt-Freischaltung
- **Neon Style**: Brightened Palette, 3D Block-Edges, Glow-Cracks, Outer-Glow HUD-Text
- **Mini-Icons**: Bomb/Flame Icons statt "B"/"F" Labels im HUD
- **Upgrade-Karten**: Farbige Akzente pro Typ, Max-Level Badge

## Changelog Highlights

- **09.02.2026**: ShopVM.UpdateLocalizedTexts() bei Sprachwechsel, Nullable-Warnings in HighScoreService + ProgressService gefixt
- **08.02.2026**: FloatingText + Celebration Overlays, Ad-Banner Padding Fix
- **07.02.2026**: Score-Verdopplung, 4 Rewarded Ads, Coins-Economy + Shop, Neon Visual Fixes, Performance (Object Pooling AStar/EnemyAI)
- **06.02.2026**: Desktop Gameplay Fixes (DPI, Touch, Keyboard), Deep Code Review, 151 Lokalisierungs-Keys
