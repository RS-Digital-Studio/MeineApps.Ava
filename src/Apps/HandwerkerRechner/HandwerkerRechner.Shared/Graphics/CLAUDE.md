# Graphics — SkiaSharp-Renderer

App-eigene SkiaSharp-Visualisierungen (Blueprint-Professional-Charakter). Nutzen
`SkiaBlueprintCanvas` + `SkiaThemeHelper` aus [MeineApps.UI](../../../../../UI/MeineApps.UI/CLAUDE.md).
SkiaSharp-Grundlagen/Gotchas (Paint-Lifecycle, DPI, MaskFilter-Leak) → dort dokumentiert.

## Renderer-Übersicht

| Datei | Typ | Beschreibung |
|-------|-----|--------------|
| `BlueprintBackgroundRenderer.cs` | Background | Animiert (5 Layer: Gradient, Blueprint-Grid mit Drift, Maßband-Markierungen, 8 Tool-Silhouetten, Vignette). Instanz-basiert, `IDisposable`, 0 GC/Frame, ~5 FPS. |
| `TileVisualization.cs` | Floor | 2D-Grundriss mit Fliesengitter, Verschnitt-Fliesen rot schraffiert. |
| `FlooringVisualization.cs` | Floor | Dielen-Verlegung mit 50%-Versatz, 3 Holzfarben. |
| `WallpaperVisualization.cs` | Floor | Wand-Abwicklung mit vertikalen Bahnen, Rapport-Versatz gestrichelt. |
| `PaintVisualization.cs` | Floor | Wand mit Farbschichten + Kannen-Icons (max 10, ×N bei mehr). |
| `ConcreteVisualization.cs` | Floor | 3 Sub-Typen + Mischverhältnis-Leiste (Zement:Sand:Kies farbig). |
| `StairsVisualization.cs` | Premium | Seitenansicht Treppenprofil, Winkel-Arc, DIN-Farbcode. |
| `RoofSolarVisualization.cs` | Premium | 3 Sub-Typen: Dachdreieck+Winkel, Ziegelraster, Solar-Panel-Layout mit Kompass. |
| `DrywallVisualization.cs` | Premium | Wandschnitt mit CW/UW-Ständerwerk, Plattenaufteilung. |
| `ElectricalVisualization.cs` | Premium | 3 Sub-Typen: Spannungsabfall-Kurve, Kosten-Balken, Ohmsches Dreieck. |
| `MetalVisualization.cs` | Premium | 2 Sub-Typen: 6 Profil-Querschnitte, Gewindebohrung-Kreis. |
| `GardenVisualization.cs` | Premium | 3 Sub-Typen: Pflastermuster, Erdschichten-Profil (3 Schichten + Grasnarbe + Wurzeln + Stein-Textur), Teichfolie-Draufsicht. |
| `PlasterVisualization.cs` | Premium | Wandquerschnitt mit Mauerwerk-Textur + proportionaler Putzschicht, Bemaßung, Sack-Info. |
| `ScreedVisualization.cs` | Premium | Bodenquerschnitt mit Untergrund (Kies-Textur) + Estrichschicht (proportional, farblich nach Typ). |
| `InsulationVisualization.cs` | Premium | Wandquerschnitt mit Mauerwerk + Dämmschicht (4 Materialtypen: EPS-Kreise, XPS-Linien, Mineralwolle-Wellen, Holzfaser-Striche). |
| `CableSizingVisualization.cs` | Premium | Kabelquerschnitt (Kupfer/Alu mit Isolierung), Spannungsabfall-Balken (VDE-Grenzlinie). |
| `GroutVisualization.cs` | Premium | Fliesengitter mit proportionalen Fugenlinien, Bemaßungen, Info-Box. |
| `CostBreakdownVisualization.cs` | Shared | Horizontale gestapelte Kostenbalken mit Segmenten, Prozent-Labels, Legende. Wiederverwendbar. |
| `MaterialStackVisualization.cs` | Shared | Material-Icon-Reihe (10 Typen: Eimer, Sack, Rolle, Paket, Box, Platte, Kabel, Stange) mit Mengen. |
| `MaterialCompareVisualization.cs` | Shared | Vergleichs-Balken (statisch, kein AnimatedVisualizationBase). |
| `HourlyRateVisualization.cs` | Shared | Stundenverrechnungssatz-Darstellung (statisch). |
| `AreaMeasureVisualization.cs` | Shared | Aufmaß-Fläche mit Beschriftung (statisch). |
| `HandwerkerRechnerSplashRenderer.cs` | Splash | "Das Maßband": Holz-Hintergrund + gelbes Maßband als Fortschrittsbalken (cm-Markierungen, Bleistift), 12 Sägespäne-Partikel. Erbt von `SplashRendererBase`. |

## Renderer-Pattern

Alle **statischen** Renderer: `public static void Render(SKCanvas, SKRect, ...)` mit gecachten
`static readonly SKPaint`-Feldern, inklusive `_layerPaint` für Alpha-Fade-In (`SaveLayer`).

`BlueprintBackgroundRenderer` ist **instanz-basiert** (zustandsbehaftet: Drift-Offset, Partikel-
Positionen) — kein `static`-Pattern, wird in `MainView` als Field gehalten und in
`OnDetachedFromVisualTree` disposed.

## Animation-Pattern

17 Renderer erben von `AnimatedVisualizationBase` (`StartAnimation()`/`NeedsRedraw`-Feedback-Loop).
3 Renderer (`HourlyRate`, `MaterialCompare`, `AreaMeasure`) sind statisch ohne Animation —
Code-Behind ruft direkt `InvalidateSurface()` bei PropertyChanged.

## Lokalisierung in Visualisierungen

6 Visualisierungen (Tile, Grout, Garden, Concrete, Stairs, Electrical) holen Labels via
`LocalizationManager.Service?.GetString("VizXxx")`. **Hardcodierte deutsche Strings sind verboten** —
englische Nutzer würden „Verschnitt" statt „Waste" sehen.

## Gotcha — InsulationVisualization Pre-Computed Arrays

`InsulationVisualization` nutzt `_epsRandoms`/`_woodFiberRandoms` als pre-computed `static`
Arrays — `Random.Shared.NextDouble()` pro Frame würde sichtbar wackeln (unterschiedliche Partikel
bei jedem Redraw statt stabiler Textur).
