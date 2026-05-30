# Graphics — SkiaSharp-Renderer

Trading-spezifische SkiaSharp-Visualisierungen. Dark-Trading-Theme: Hintergrund `#1E1E2E`, Grid
`#3F3F5C`, Profit `#10B981`, Loss `#EF4444`, Primary `#3B82F6`. SkiaSharp-Grundlagen/Gotchas
(Paint-Lifecycle, DPI, MaskFilter-Leak) → [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `EquityChartRenderer.cs` | **Statische Klasse.** Linien-Chart der Equity-Kurve. Profit/Loss-Farbgebung je Sektion, Baseline-Markierung. Gecachte static SKPaint-Felder (kein pro-Frame Alloc). |
| `DrawdownChartRenderer.cs` | **Statische Klasse.** Underwater-Chart (negativer Drawdown vom Peak). Selbe Paint-Cache-Strategie wie Equity. |
| `InteractiveChartRenderer.cs` | **Instanz-Klasse** mit `ChartState`. Candlestick-Chart mit EMA-50/200, Bollinger Bands, Supertrend, Regime-Hintergrund, Crosshair, Zoom/Pan, Trade-Marker (Entry/Exit/SL/TP-Overlay). |
| `PnlCalendarRenderer.cs` | Tages-PnL-Heatmap im Kalender-Layout. Grün/Rot-Gradient pro Tag. |
| `FearGreedGaugeRenderer.cs` | Halbkreis-Gauge für Fear-&-Greed-Index / Markt-Sentiment. |
| `CorrelationMatrixRenderer.cs` | Matrix-Visualisierung der Asset-Cluster-Korrelationen. |
| `ChartState.cs` | Zoom/Pan/Scroll-Zustand für `InteractiveChartRenderer` (Viewport-Offset, Scale). |

## Paint-Cache-Strategie

Statische Renderer (`EquityChartRenderer`, `DrawdownChartRenderer`) halten gecachte `static readonly SKPaint`-Felder.
**Kein `Dispose()` auf gecachten Paints** — sie leben für die App-Lifetime.

```csharp
// RICHTIG: Gecachte statische Paints
private static readonly SKPaint ProfitLinePaint = new() { Color = ProfitColor, StrokeWidth = 2f, ... };

// FALSCH: Paints pro Frame neu erzeugen (GC-Druck)
canvas.DrawPath(path, new SKPaint { ... });
```

## InteractiveChartRenderer — Instanz vs. Statisch

`InteractiveChartRenderer` ist eine Instanz-Klasse, weil er `ChartState` (Zoom/Pan, Scroll-Position)
hält. Der `StrategyView` bzw. `BacktestView` besitzt die Instanz. Eine Instanz pro View — nicht teilen.

## Rendering-Koordinaten

**IMMER `canvas.LocalClipBounds`** für Bounds verwenden, nicht `e.Info.Width/Height` — bei
DPI > 1 geben `Info.Width/Height` physische Pixel zurück, die größer als der sichtbare Bereich sind
(Clipping-Bug auf High-DPI-Geräten). Gilt für alle Renderer hier.
