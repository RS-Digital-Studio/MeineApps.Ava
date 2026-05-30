# Graphics — SkiaSharp-Renderer

App-eigene SkiaSharp-Visualisierungen (Retro-Tech-Charakter). Nutzen `SkiaThemeHelper` +
Helpers aus [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md). SkiaSharp-Grundlagen/Gotchas
(Paint-Lifecycle, DPI, MaskFilter-Leak) → dort dokumentiert.

## Dateien

| Datei | Zweck |
|-------|-------|
| `VfdDisplayVisualization.cs` | 7-Segment-VFD-Röhren-Simulation: Cyan-Grün (#00FFB0)/Rot bei Fehler, Ghost-Segmente, subtiles Flicker (~7 Hz). `_glowPaint.MaskFilter` im Field-Initializer (kein Leak). Timer 33 ms ab `OnAttachedToVisualTree`. |
| `ResultBurstVisualization.cs` | Expandierender Lichtring + 8 Partikel-Strahlen bei `=` (Cubic ease-out, 500 ms). |
| `FunctionGraphVisualization.cs` | Mini-Graph für sin/cos/tan/sqrt/log/ln/x²/1/x. Asymptoten → Pfad-Unterbrechung statt `DrawLine`. |
| `CalculatorBackgroundRenderer.cs` | "Digital Circuit Board"-Hintergrund (4 Layer, ~5 fps, gecachte Paints, Shader-Cache nur bei Größenänderung). |
| `RechnerPlusSplashRenderer.cs` | Splash (Tasten-Matrix + LCD). Erbt von `SplashRendererBase`. |

## Gotcha — geteilter Timer

VFD-Flicker **und** FunctionGraph-Glow-Pulsierung teilen denselben DispatcherTimer (33 ms,
`_vfdAnimTime` als `animTime`-Parameter), der in `CalculatorView.axaml.cs` läuft. Ein separater
Timer für den FunctionGraph würde die CPU-Last verdoppeln.
