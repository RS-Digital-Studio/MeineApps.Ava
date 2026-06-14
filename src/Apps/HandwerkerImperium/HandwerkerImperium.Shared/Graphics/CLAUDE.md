# Graphics — SkiaSharp-Renderer (59 Dateien)

App-eigene SkiaSharp-Visualisierungen. Alle Renderer mit Instanz-SKPaint/SKFont/SKPath/SKShader
implementieren `IDisposable` mit `_disposed`-Guard.
SkiaSharp-Grundlagen/Gotchas (Paint-Lifecycle, DPI, MaskFilter-Leak) →
[MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).

---

## IDisposable-Klassifizierung

| Klasse | Typ |
|--------|-----|
| Alle MiniGame-Renderer (Sawing, Pipe, Wiring, Painting, Blueprint, RoofTiling, DesignPuzzle, Inspection, Forge, Invent) | `IDisposable`, Instanz-Felder |
| `LuckySpinWheelRenderer`, `OdometerRenderer`, `WorkshopSceneRenderer` | `IDisposable`, Instanz-Felder |
| `CityRenderer`, `CityWeatherSystem`, `GameJuiceEngine` | `IDisposable`, Instanz-Felder |
| `MarketChartRenderer`, `MaterialIconRenderer`, `PrestigeCinematicRenderer`, `RewardCeremonyRenderer` | `IDisposable`, Instanz-Felder |
| `GameTabBarRenderer`, `GameBackgroundRenderer`, `ScreenTransitionRenderer` | `IDisposable`, Instanz-Felder |
| `ResearchTreeRenderer`, `GuildResearchTreeRenderer`, `ResearchCelebrationRenderer` | `IDisposable`, Instanz-Felder |
| `ResearchActiveRenderer`, `ResearchBackgroundRenderer`, `ResearchBranchBannerRenderer`, `ResearchLabRenderer`, `ResearchTabRenderer` | `IDisposable`, Instanz-Felder |
| `GuildHallHeaderRenderer`, `GuildHallSceneRenderer`, `GuildBossRenderer`, `GuildLeagueBadgeRenderer` | `IDisposable`, Instanz-Felder |
| `GuildAchievementRenderer`, `GuildWarDashboardRenderer`, `GuildWarLogRenderer`, `GuildResearchBackgroundRenderer` | `IDisposable`, Instanz-Felder |
| `PrestigeRoadmapRenderer`, `CoinFlyAnimation` | `IDisposable`, Instanz-Felder |
| **`WorkshopCardRenderer`**, **`WorkshopGameCardRenderer`**, **`MeisterHansRenderer`** | `static class` — kein `IDisposable` nötig |
| **`GameCardRenderer`**, **`ResearchIconRenderer`**, **`GuildResearchIconRenderer`** | `static class` — kein `IDisposable` nötig |
| **`FtueSpotlightRenderer`**, **`CjkFontResolver`**, **`RarityFrameRenderer`** | `static class` — kein `IDisposable` nötig |
| **`ResearchItemRenderer`**, **`CraftTextures`** | `static class` — kein `IDisposable` nötig |
| **`FireworksRenderer`**, **`LoadingScreenRenderer`**, **`WorkerAvatarRenderer`** | `sealed class`, alle Felder `static readonly` — kein `IDisposable` |
| **`AnimationManager`**, **`EasingFunctions`** | keine SKPaint-Felder — kein `IDisposable` |

`App.DisposeServices()` → `GameJuiceEngine.Dispose()` → Renderer-Dispose-Kaskaden.

---

## Schlüsseldateien

| Datei | Zweck |
|-------|-------|
| `GameJuiceEngine.cs` | ScreenShake, RadialBurst, CoinsFlyToWallet, SparkleEffect, Vignette (Struct-Pool max 200). `ReduceMotion`-Flag |
| `FpsProfile.cs` | Plattformadaptive FPS-Profile (Low/Medium/High). `CurrentChanged`-Event für Live-Update |
| `GameTabBarRenderer.cs` | Tab-Bar-Renderer (SkiaSharp, kein XAML). Ersetzt XAML-Tab-Bar komplett |
| `CityRenderer.cs` | AI-Bitmap + Wetter-Overlay (saisonal, Event-gesteuert) |
| `CityWeatherSystem.cs` | Regen+Regenbogen, Sonne+Shimmer, Blätter, Schnee, Kirschblüten (160 Struct-Pool, ×2 bei Events) |
| `WorkshopCardRenderer.cs` | `static` — 10 thematische Szenen (AI-Bitmap + Level-Overlays: Sterne Lv250+, Gold-Aura Lv500+, Shimmer Lv1000) |
| `WorkshopGameCardRenderer.cs` | `static` — AI-Bitmap, gecacht im `GameAssetService`. `Initialize(assetService)` in `App.axaml.cs` |
| `WorkerAvatarRenderer.cs` | Pixel-Art (6 Hauttöne, Tier-Farbe+Sterne, Mood, RarityFrame, Idle-Bobbing+Blinzeln). Gecacht in `GameAssetService`. `InitializeAssetService(assetService)` in `App.axaml.cs` |
| `MeisterHansRenderer.cs` | `static` — 4 Stimmungen, Idle-Bobbing, Blinzel-Animation (120×120). `Initialize(assetService)` in `App.axaml.cs` |
| `OdometerRenderer.cs` | Animierte Geld-Anzeige, rollende Ziffern, Suffix-Crossfade, Gold-Flash |
| `CoinFlyAnimation.cs` | 8–16 Münzen auf Bezier-Kurven, HUD-Pulse bei Ankunft |
| `LuckySpinWheelRenderer.cs` | 8 Segmente, Nieten-Rand, Spin-Animation ~60fps. Gecacht: 11 SKPaint + 1 SKFont + 13 SKShader + 2 SKMaskFilter |
| `PrestigeCinematicRenderer.cs` | 4-Phasen (Money→Badge→Multiplier→Reward), 14s, Skip+Tap-To-Continue, Auto-Dismiss nach 8s |
| `RewardCeremonyRenderer.cs` | Full-Screen: Scale-In, Confetti (120), Feuerwerk, 5 CeremonyTypes, 4s Tap-to-Dismiss |
| `ResearchIconRenderer.cs` | `static`, gecachte `_cachedPath` + `_labelFont` + `_crownFont` — alle Icons sequenziell |
| `GuildResearchIconRenderer.cs` | `static`, gecachte `_cachedPath` |
| `ResearchTreeRenderer.cs` | Forschungsbaum, Branch-Tabs |
| `GuildResearchTreeRenderer.cs` | Gilden-Forschungsbaum |
| `MaterialIconRenderer.cs` | DI-Singleton. Procedural 128×128 Bitmaps pro ProductId (gecacht). 3 SKPaint + 1 SKFont |
| `MarketChartRenderer.cs` | Preis-Verlaufskurve für Material-Markt-Heatmap |
| `HandwerkerImperiumSplashRenderer.cs` | Splash (Werkstatt-Szene + Meister Hans). Erbt von `SplashRendererBase` |
| `ScreenTransitionRenderer.cs` | View-Übergangs-Effekte |
| `FtueSpotlightRenderer.cs` | `static` — FTUE-Spotlight-Overlay (Spotlight-Ausschnitt + abgedunkelter Rest) |
| `CjkFontResolver.cs` | `static` — Fallback-Font-Resolver für CJK-Zeichen in SkiaSharp-Text |
| `GameBackgroundRenderer.cs` | App-Hintergrund-Renderer für MainView |
| `AnimationManager.cs` | Zentrale Animation-State-Verwaltung für mehrere gleichzeitige Animationen |
| `EasingFunctions.cs` | Easing-Bibliothek (CubicEaseOut, CubicEaseIn, Bounce, Spring, ...) |
| `CraftTextures.cs` | `static` — Prozedurale Craft-Texturen (Holz, Metall, Stein, Leder), Shader-Cache bei Bounds-Änderung |
| `RarityFrameRenderer.cs` | `static` — Seltenheits-Rahmen (Common/Uncommon/Rare/Epic/Legendary) für Worker-Karten |
| `GameCardRenderer.cs` | `static` — Karten-Layout (Hintergrund, Typ-Farbe, Text) |
| `PrestigeRoadmapRenderer.cs` | Prestige-Fortschritts-Übersicht (Tier-Roadmap) |
| **Guild-Renderer** | `GuildHallHeaderRenderer`, `GuildHallSceneRenderer`, `GuildBossRenderer`, `GuildLeagueBadgeRenderer`, `GuildAchievementRenderer`, `GuildWarDashboardRenderer`, `GuildWarLogRenderer`, `GuildResearchBackgroundRenderer` — Gilden-Views |
| **Research-Renderer** | `ResearchActiveRenderer`, `ResearchBackgroundRenderer`, `ResearchBranchBannerRenderer`, `ResearchCelebrationRenderer`, `ResearchItemRenderer`, `ResearchLabRenderer`, `ResearchTabRenderer` — Forschungs-Views |

---

## FPS-Profile

| Kontext | Low | Medium | High |
|---------|-----|--------|------|
| MiniGame | 24fps | 30fps | 30fps |
| Research/Workshop/GuildResearch | 15fps | 20fps | 24fps |
| Dashboard Idle | 5fps | 10fps | 10fps |
| Dashboard bei Effekten | 15fps | 24fps | 30fps |
| WorkerAvatar | 5fps | 8fps | 10fps |
| MainView (BG+TabBar) | 10fps | 15fps | 15fps |

Platform-Default: Android=Medium, Desktop=High. `FpsProfile.SetCurrent(quality)` feuert
`FpsProfile.CurrentChanged`-Event; `WorkerAvatarControl` und andere Views subscriben für Live-Update.

---

## WorkerAvatarControl (in `Controls/`)

Custom `Control`-Ableitung. Gemeinsamer statischer `DispatcherTimer` (`s_sharedTimer`) für alle
Instanzen — ein Tick für alle statt pro-Instanz-Timer. Statische `s_bitmapPaint` + `s_blinkPaint`.
`WeakReference`-Liste (`s_instances`) für Auto-Cleanup toter Controls.

---

## SKPath/SKFont-Caching-Pattern (GC-Reduktion bei 30fps)

Gecachte Instanz- oder Klassenfelder statt `using var` pro Frame:

| Renderer | Gecachte Felder |
|----------|----------------|
| `WiringGameRenderer` | 8 SKPaint + 3 MaskFilter + 1 SKPath + 1 SKFont |
| `SawingGameRenderer` | 10 SKPaint + 1 SKPath + 3 SKShader (Bounds-basierter Cache, Toleranz 2dp) |
| `BlueprintGameRenderer` | 21 SKPaint + 3 SKFont + 1 SKPath + 1 SKShader (BG-Cache per Bounds) |
| `LuckySpinWheelRenderer` | 11 SKPaint + 1 SKFont + 13 gecachte SKShader + 2 SKMaskFilter |

**Shader-Cache-Pattern**: Nur bei Bounds-Änderung neu erstellen (`CraftTextures`, `ForgeGame`, `WiringGame`, `SawingGame`).

**Translate-Cache-Pattern** (Shader mit konstanter Farbe+Form, nur Position variiert): Shader
einmal um den lokalen Ursprung (0,0) cachen und per `canvas.Translate(...)` positionieren. Eine
reine Translation ist pixelidentisch zum Neu-Erzeugen an variabler Position (kein Resampling) —
im Gegensatz zu `Scale`. Genutzt von `ForgeGame` (Hammer-Stiel/-Kopf) und `InventGame` (Scan-Linie).
Funktioniert NICHT bei variabler Größe (Feuer-Flammen in `ForgeGame`: variable Höhe → Skalierung
nötig → bleibt per-Frame) oder variabler Farbe (Werkstück-Gradient: temperaturabhängig → bleibt per-Frame).

**Dash-PathEffect-Bucket-Cache** (Marching-Ants): Animierte `SKPathEffect.CreateDash`-Phase auf
wenige diskrete Buckets quantisieren (`(int)(_time*v) % period`) und Effekte in
`Dictionary<int, SKPathEffect>` cachen (Lebensdauer = Bucket-Anzahl). `ResearchTree` (13/8 Buckets).
Animation bleibt fürs Auge flüssig.

**SKRoundRect vermeiden**: Für reines Zeichnen die `canvas.DrawRoundRect(rect, rx, ry, paint)`-Overload
nutzen (kein SKRoundRect-Heap-Objekt). Wo Clipping zwingend ein SKRoundRect braucht (`ClipRoundRect`):
wiederverwendbares Feld + `SetRect()`/`SetRectRadii()` statt `new` pro Frame (`ResearchActive`-Flask-Clip).

**SKPathMeasure wiederverwenden**: Ein Feld + `SetPath(path, false)` statt `new SKPathMeasure(path, false)`
pro Frame (`WiringGame` Strom-Puls).

---

## Scroll-Performance

Während Scroll: City-Canvas + Workshop-Cards + Background + TabBar komplett pausiert
(0 `InvalidateSurface/s`). 250ms Ruhezeit nach letztem `ScrollChanged`.
`DashboardView.IsScrolling` → MainView pausiert alle Canvases.

Max-Modus Debounce: `GetMaxAffordableUpgrades` (Math.Pow-Schleife) auf nicht-sichtbaren Tabs nur alle 2s.

---

## Gotcha — Shader-Preload in LoadingPipeline

Alle SkSL-Shader (GameJuiceEngine, Blur-Effekte) werden in der
`HandwerkerImperiumLoadingPipeline` vorkompiliert — **nicht** lazy beim ersten Render.
Doppelter Preload ist NICHT erlaubt (redundant + Speicher-Verschwendung). Prüfen ob
ein Shader bereits im `GameAssetService` liegt bevor ein zweiter Preload-Aufruf hinzugefügt wird.

## Gotcha — SKCanvasView leer nach IsVisible-Toggle

`InvalidateSurface()` auf unsichtbarer `SKCanvasView` wird ignoriert. Nach Sichtbar-Werden
IMMER Daten erneut setzen/`Calculate()` aufrufen — KEIN `if (!HasResult)`-Guard.

## Gotcha — DPI-Bounds

```csharp
// IMMER canvas.LocalClipBounds verwenden:
var bounds = canvas.LocalClipBounds;
// NIEMALS e.Info.Width/Height — das sind physische Pixel (DPI > 1 → Rechts-Clipping)
```
