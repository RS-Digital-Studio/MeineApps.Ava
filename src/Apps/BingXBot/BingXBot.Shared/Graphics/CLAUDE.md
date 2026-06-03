# Graphics — SkiaSharp-Renderer

Trading-spezifische SkiaSharp-Visualisierungen. Dark-Trading-Theme: Hintergrund `#1E1E2E`, Grid
`#3F3F5C`, Profit `#10B981`, Loss `#EF4444`, Primary `#3B82F6`. SkiaSharp-Grundlagen/Gotchas
(Paint-Lifecycle, DPI, MaskFilter-Leak) → [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `EquityChartRenderer.cs` | **Statische Klasse.** Linien-Chart der Equity-Kurve. Profit/Loss-Farbgebung je Sektion, Baseline-Markierung. Gecachte `static readonly SKPaint`-Felder (kein pro-Frame Alloc). |
| `DrawdownChartRenderer.cs` | **Statische Klasse.** Underwater-Chart (negativer Drawdown vom Peak). Selbe Paint-Cache-Strategie wie Equity. |
| `InteractiveChartRenderer.cs` | **Instanz-Klasse** mit `State`-Property (`ChartState`). Candlestick-Chart mit EMA-50/200, Bollinger Bands, Supertrend, Regime-Hintergrund, Crosshair, Zoom/Pan, Trade-Marker (Entry/Exit/SL/TP-Overlay). Wird von `BtcTickerViewModel` als Property gehalten. |
| `PnlCalendarRenderer.cs` | **Statische Klasse.** Tages-PnL-Heatmap im Kalender-Layout. Grün/Rot-Gradient pro Tag. |
| `FearGreedGaugeRenderer.cs` | **Statische Klasse.** Halbkreis-Gauge für Fear-&-Greed-Index / Markt-Sentiment. |
| `CorrelationMatrixRenderer.cs` | **Statische Klasse.** Matrix-Visualisierung der Asset-Cluster-Korrelationen. |
| `ChartState.cs` | Zoom/Pan/Scroll-Zustand für `InteractiveChartRenderer` (Viewport, Crosshair, Drag-State, Indikator-Toggles). Wird als `InteractiveChartRenderer.State`-Property gehalten — nicht extern instanziieren. |

## Paint-Cache-Strategie

Statische Renderer halten gecachte `static readonly SKPaint`-Felder.
**Kein `Dispose()` auf gecachten Paints** — sie leben für die App-Lifetime.

```csharp
// RICHTIG: Gecachte statische Paints
private static readonly SKPaint ProfitLinePaint = new() { Color = ProfitColor, StrokeWidth = 2f, ... };

// FALSCH: Paints pro Frame neu erzeugen (GC-Druck)
canvas.DrawPath(path, new SKPaint { ... });
```

## InteractiveChartRenderer — Instanz vs. Statisch

`InteractiveChartRenderer` ist eine Instanz-Klasse, weil er `ChartState` (Zoom/Pan, Scroll-Position,
Indikator-Toggles) als `State`-Property hält. Die Instanz lebt im `BtcTickerViewModel` als Property
`ChartRenderer`. Eine Instanz pro ViewModel — nicht teilen.

## Rendering-Koordinaten

Alle Renderer nehmen `SKRect bounds` als Parameter entgegen. Der aufrufende Code gibt
`canvas.LocalClipBounds` oder ein daraus abgeleitetes Rect weiter — **nicht** `e.Info.Width/Height`
direkt als Bounds verwenden, da diese bei DPI > 1 physische Pixel liefern (Clipping-Bug auf
High-DPI-Geräten).
