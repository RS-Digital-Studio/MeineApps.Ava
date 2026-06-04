# Graphics — SkiaSharp-Render-Pipeline

Alle Renderer und visuellen Subsysteme. Nutzen `SkiaThemeHelper` + Helpers aus
[MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).
SkiaSharp-Grundlagen/Gotchas (DPI, MaskFilter-Leak, Render-Loop) → dort dokumentiert.
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

---

## GameRenderer (10 Partial-Classes)

| Datei | Verantwortung |
|-------|--------------|
| `GameRenderer.cs` | Kern: `canvas.LocalClipBounds` (nie `e.Info.Width/Height`!), Palette, SaveLayer-Reihenfolge |
| `GameRenderer.Grid.cs` | Dispatcher: delegiert an `.Tiles` / `.Blocks` / `.GridFx` |
| `GameRenderer.Grid.Tiles.cs` | Floor-Tiles, Boden-Cache (150 Tiles als `SKBitmap`, invalidiert bei Welt-/Style-Wechsel) |
| `GameRenderer.Grid.Blocks.cs` | Destructible/Indestructible Blocks, Block-Fragmente |
| `GameRenderer.Grid.GridFx.cs` | FogOverlay, Transitions, Afterglow |
| `GameRenderer.Characters.cs` | Spieler-Sprite (Squash/Stretch, iFrame-30%-Alpha-SaveLayer) |
| `GameRenderer.Bosses.cs` | Boss-Sprites (Multi-Cell, Outline-Pass, Anticipation-Scale) |
| `GameRenderer.Items.cs` | Bomben (Welt-themed BombFxTheme), PowerUps, Exit |
| `GameRenderer.Atmosphere.cs` | Dispatcht an WeatherSystem, AmbientParticleSystem, DynamicLighting, ShaderEffects, TrailSystem |
| `GameRenderer.HUD.cs` | Side-Panel rechts: Time/Score/Combo/Lives/Deck |
| `GameRenderer.Events.cs` | Saisonale Partikel-Overlays (Halloween/Christmas/NewYear/Summer) |

### SaveLayer-Reihenfolge (außen → innen)

1. Cinematic-Zoom (`canvas.Scale(zoomFactor, pivotX, pivotY)` via `CinematicSequencer`)
2. ScreenShake-Translate
3. Spielfeld-Content (Grid + Entities + HUD)
4. Post-Processing (Colorblind-ColorMatrix, ShaderEffects)
5. Subtitle-Overlay
6. UltraComboFlash / DamageFlash

**Input-Controls werden NICHT vom Cinematic-Zoom betroffen** — Joystick bleibt lesbar.

---

## Atmosphärische Subsysteme

| Datei | Pool | Beschreibung |
|-------|------|--------------|
| `DynamicLighting.cs` | — | Radius-basierte Lichtquellen, `SKBlendMode.Screen` |
| `WeatherSystem.cs` | 80 Structs | Welt-spezifische Partikel (Blätter, Asche, Blasen) |
| `AmbientParticleSystem.cs` | 60 Structs | Hintergrund-Partikel (Glühwürmchen, Dampf, Kristalle) |
| `ShaderEffects.cs` | — | GPU SkSL Water Ripples + CPU-Fallback, Color Grading, Heat Shimmer |
| `TrailSystem.cs` | 40 Structs | Charakter-Spuren, Ghost-Afterimages, Boss-Trails |
| `ParticleSystem.cs` | dynamisch | General-Purpose-Pool, Cap via `HardwareTier` |

**Adaptives Frame-Skipping**: 5-Frame-Ring-Buffer (`_frameTimeBuffer[5]`). Wenn Ø > 50ms
(≤ 20 FPS) → alle atmosphärischen Systeme für ≥ 1,0 s ausgesetzt (`SkipHoldMinSeconds`).
Hysterese-Exit bei Ø < 36ms (≥ 27 FPS). `SkipAtmosphere` kombiniert manuellen
`ReducedEffects`-Toggle mit adaptiver Entscheidung (`_adaptiveSkipActive`).

---

## Weitere Renderer

| Datei | Zweck |
|-------|-------|
| `BloomEffect.cs` | GPU-Bloom via `SKRuntimeEffect` (SkSL). 2 Pässe: Threshold + 5×5-Box-Blur + Additive Blend. Nur Ultra-Tier (außer Battery/Thermal). `Preload` in LoadingPipeline. |
| `CinematicSequencer.cs` | Lightweight Event-Sequencer: ordered `(triggerSeconds, Action)`-Liste. Boss-Reveal (1.5s), Victory (2.5s). |
| `SubtitleSystem.cs` | Struct-Pool max 4, Fade-In/Out, nur wenn `SubtitlesEnabled`. Throttling bei Ultra-Combo (alle 5). |
| `FogOfWarSystem.cs` | 3-Zustände (Unknown/Explored/Visible). RLE-Optimierung: ~150 → ~30 DrawCalls. Position-Cache. |
| `UltraComboFlash.cs` | `_ultraFlash` (x10, Welt-Akzent, 200ms) + `_damageFlash` (Hit, Rot, 50ms+250ms). Bei `ReducedEffects` unterdrückt. |
| `OutlineRenderHelper.cs` | Static: Dilate-ImageFilter + ColorFilter (SrcIn) = Outline-Ring, dann Original. ~2x DrawCalls, für 5-10 Entities empfohlen. |
| `ScreenShake.cs` | Squirrel-Eiserloh-Trauma-Modell: Trauma akkumuliert, Shake = MaxAmplitude × trauma². TraumaDecay 1.5/s. |
| `GameFloatingText.cs` | Struct-Pool 20: Score-Popups, Combo-Text, PowerUp. Crit-Pop je Combo-Stufe. |
| `ProceduralTextures.cs` | Noise2D/Fbm (Perlin-ähnlich), CellRandom (deterministisch per Zelle), 12 Textur-Funktionen. |
| `MenuBackgroundRenderer.cs` | 7 Themes, struct-Pool 60 Partikel. |
| `BomberBlastColors.cs` | Statische Farb-Konstanten (WorldPalette, BombFxTheme, Neon-Arcade-Akzente). |
| `BomberBlastSplashRenderer.cs` | Splash-Renderer. Erbt von `SplashRendererBase`. |
| `ExplosionShaders.cs` | SkSL-Shader für Explosions-Effekte. |
| `VictoryAnimator.cs` | Konfetti-Animationen (Two-Pass: ohne/mit Glow). |
| `GameOverVisualization.cs` | Spiel-Ende-Screen-Effekte. |
| `DungeonMapRenderer.cs` | Node-Map-Darstellung (10×3 Slay-the-Spire-inspiriert). |
| `DiscoveryOverlay.cs` | PowerUp-Erstentdeckungs-Overlay (pausiert Spiel). |
| `TutorialOverlay.cs` | Tutorial-Schritt-Overlays. |
| `TornMetalRenderer.cs` | Torn-Metal-Texturen für Buttons (deterministisch via Seed). |
| `FrameRenderer.cs` | Spieler-Frame-Cosmetics (Trägerstruktur für 33 Definitionen). |
| `HudVisualization.cs` | Hilfsfunktionen für HUD-Elemente. |
| `LevelSelectVisualization.cs` | Welt-Karten-Visualisierung im LevelSelect. |
| `AchievementIconRenderer.cs` | Trophäen-Icons mit Rarity-Glow. |
| `ShopIconRenderer.cs` | Shop-Upgrade-Icons. |
| `RarityRenderer.cs` | Rarity-Glow-Effekte (Common/Rare/Epic/Legendary). |
| `HelpIconRenderer.cs` | Hilfe-Screen-Icons. |
| `EmptyStateRenderer.cs` | Leere-Listen-Illustrationen. |

---

## SkiaSharp-Caching-Patterns (KRITISCH)

```csharp
// SKPath: NIEMALS new SKPath() im Render-Loop
_tilePath.Rewind();   // behält nativen Buffer — NICHT Reset() (gibt Buffer frei)
// Ausnahme nach Rewind() in FogOfWarSystem: FillType explizit zurücksetzen
_bgPath.FillType = SKPathFillType.Winding;

// SKMaskFilter: einmal cachen, nie pro Frame neu erstellen
private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, 8f);
// Bei Neuzuweisung: alten disposen
_paint.MaskFilter?.Dispose();
_paint.MaskFilter = SKMaskFilter.CreateBlur(...);

// SKPaint + SKColorFilter (+ ImageFilter): pro Frame pro Entity = nativer, FINALISIERBARER Müll
// → Gen0/Gen1-GC-Pause = periodischer Frame-Freeze (auf Mono-AOT-Android verstärkt). using/Dispose
// gibt nur den Handle frei, der managed Wrapper muss trotzdem durch die Finalizer-Queue.
// OutlineRenderHelper cacht Paint+ColorFilter Single-Slot (wie den Dilate-Filter). Render-Call-Sites
// vermeiden Per-Frame-Closures via gecachter Delegates + mutable "current"-Felder (kein Lambda/foreach).
// WICHTIG: Der volle Spielfeld-Render läuft bereits ab GameState.Starting (Countdown), nicht erst
// ab Playing — Per-Frame-Allokationen erzeugen den Stutter also ab Frame 1.

// DPI: IMMER canvas.LocalClipBounds — nie e.Info.Width/Height (physische Pixel)
var bounds = canvas.LocalClipBounds;

// toString im Render-Loop: statische String-Arrays für bekannte Wertebereiche
```

**BombFxTheme**: Drei statische Arrays (`ClassicBombFx`, `NeonBombFx`, `RetroBombFx`),
je 10 Einträge (eine Welt pro Eintrag). `SetWorldTheme(worldIndex)` wählt das passende Array
nach `_styleService.CurrentStyle` aus und setzt `_bombFxTheme`. Custom-Cosmetics behalten
ihre Farben (nur Default-Skin). Bei Welt-Wechsel: `SetWorldTheme()` → `UpdateExplosionSkinColors()`.

**ShaderEffects.Logger**: Statischer Sink, wird in `App.axaml.cs` nach
`BuildServiceProvider()` gesetzt. `DisposeSharedResources()` in `App.DisposeServices()`.
