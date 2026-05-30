# Graphics — SkiaSharp-Visualisierungen

App-eigene SkiaSharp-Visualisierungen (Warm Clockwork-Charakter, Amber #F7A833). Nutzen
`SkiaThemeHelper` + Helpers aus [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).
SkiaSharp-Grundlagen/Gotchas (Paint-Lifecycle, DPI, MaskFilter-Leak) → dort dokumentiert.

## Dateien

| Datei | Zweck |
|-------|-------|
| `ClockworkBackgroundRenderer.cs` | Animierter "Warm Clockwork"-Hintergrund (5 Layer): Warmer 3-Farben-Gradient (#382C22/#2A2018/#301A10), konzentrische Uhrenringe, Glühwürmchen-Partikel (Amber, Glow via gecachtem `_particleGlowMask`), 60 Tick-Markierungen, radiale Vignette. Struct-Pool (max 12 Partikel), gecachte Paints, ~5fps. Instance-basiert (`IDisposable`). |
| `StopwatchVisualization.cs` | Stoppuhr-Ring mit Sekundenzeiger + Nachleucht-Trail (6 Ghost-Positionen), Runden-Sektoren (farbige Bögen, 8 Farben), Sub-Dial (Minuten-Ring oben rechts), 60 Sekunden-Ticks, Rundenpunkte. Statisch. |
| `PomodoroVisualization.cs` | `RenderRing()`: Fortschrittsring mit Pulsier-Effekt (2Hz, nur bei `IsRunning`) auf aktivem Zyklus-Segment + Glow, innerer Session-Ring (Tages-Fortschritt als Segment-Bögen). `RenderWeeklyBars()`: 7-Balken-Diagramm mit CubicEaseOut-Einfahranimation. Statisch. |
| `PomodoroStatisticsVisualization.cs` | Monats-Heatmap (GitHub-Contributions-Stil): 7×5 Grid, 5 Intensitätsstufen, Wochentag-Labels, Heute-Highlight. HitTest via `Canvas.LocalClipBounds` für Tap-Interaktion. Statisch. |
| `TimerVisualization.cs` | Flüssigkeits-Füllung + Welleneffekt (SinKurve), Tropfen-Partikel (8 Stück, Array-Pool), Countdown-Ziffern (letzte 5s, Scale-Bounce 1.5→1.0 mit Glow), Ablauf-Burst (20 Confetti-Partikel bei Timer=0). `Reset()` für Partikel-State. Statisch. |
| `ZeitManagerSplashRenderer.cs` | Analoge Uhr mit Snap-Tick Sekundenzeiger, kreisförmiger Progress-Ring, 12 rotierende Zahnrad-Partikel, konzentrische Deko-Ringe. Erbt von `SplashRendererBase`. |

## Render-Loop-Anbindung

| Visualisierung | Timer | fps | Gestartet/Gestoppt |
|----------------|-------|-----|-------------------|
| `ClockworkBackgroundRenderer` | `DispatcherTimer` in `MainView` | ~5fps (200ms) | On/OffAttachedToVisualTree |
| `StopwatchVisualization` | `DispatcherTimer` in `StopwatchView` | 30fps | Nur wenn `IsRunning` |
| `PomodoroVisualization` | `DispatcherTimer` in `PomodoroView` | 30fps | Nur wenn `IsRunning` |
| `TimerVisualization` | `DispatcherTimer` in `TimerView` | 30fps | Nur wenn `ShowVisualization` |

**Warum selektive Render-Loops statt einem shared Timer?** Stoppuhr und Pomodoro laufen selten
gleichzeitig. Ein einzelner 30fps-Timer für beide würde unnötig CPU verbrauchen wenn kein Feature
aktiv ist. Anders als in RechnerPlus (geteilter VFD-Timer) sind die Kontexte hier unabhängig.

## `ClockworkBackgroundRenderer` — Speicher

Instance-basiert wegen Partikel-State. Gecachte `SKMaskFilter _particleGlowMask` im Field
(kein Leak im Render-Loop). Shader-Cache (`SKShader`) wird nur bei Größenänderung neu erstellt
(`_lastWidth`/`_lastHeight`-Vergleich). `Dispose()` löscht alle Paints + Filter + Shader.

## `TimerVisualization.Reset()`

Statische Klasse mit statischen Partikel-Arrays. `Reset()` muss bei jedem neuen
`StartRenderLoop()`-Aufruf in `TimerView` aufgerufen werden — sonst startet die Animation
mit dem Zustand des letzten Timers (z.B. bereits explodierten Burst-Partikeln).
