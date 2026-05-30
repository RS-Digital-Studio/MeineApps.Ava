# Graphics — SkiaSharp-Renderer

App-eigene SkiaSharp-Visualisierungen (Living-Finance-Charakter, Smaragd `#10B981`).
Nutzen `SkiaThemeHelper` + Helpers aus [MeineApps.UI](../../../../../UI/MeineApps.UI/CLAUDE.md).
SkiaSharp-Grundlagen/Gotchas (Paint-Lifecycle, DPI, MaskFilter-Leak) → dort dokumentiert.

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `FinanceBackgroundRenderer.cs` | Animierter Hintergrund MainView (~5 fps: Smaragd-Gradient, Chart-Linien, Mini-Balken, Sparkle, Vignette). Wird mit `Grid.RowSpan=3` hinter allen Layern gezeichnet. |
| `FinanceDashboardRenderer.cs` | Animierter Hero-Header auf HomeView (Gradient-Mesh, Grid-Linien, Glow-Dots, Floating-Symbole). Läuft auf 60-fps DispatcherTimer (→ Gotcha unten). |
| `CalculatorHeaderRenderer.cs` | 6 individuelle animierte Header-Hintergründe pro Rechner-View. |
| `CardGlowRenderer.cs` | Status-basierter Edge-Glow auf Karten (Budget-Status grün/gelb/rot, Bilanz, Berechnungs-Flash). |
| `FinanzRechnerSplashRenderer.cs` | Splash "Das wachsende Kapital" (Aktien-Chart, Münz-Stapel, Gold-Partikel). Erbt von `SplashRendererBase`. |
| `SparklineVisualization.cs` | Mini-Sparkline mit Gradient-Füllung (30-Tage-Ausgaben-Trend auf HomeView). |
| `BudgetMiniRingVisualization.cs` | Kompakte Mini-Ringe für Budget-Kategorien-Übersicht auf HomeView. |
| `TrendLineVisualization.cs` | 2 Spline-Kurven (Einnahmen/Ausgaben) mit Gradient-Füllung (StatisticsView). |
| `StackedAreaVisualization.cs` | 2 gestapelte Flächen für CompoundInterest-, SavingsPlan- und Inflation-Rechner. |
| `AmortizationBarVisualization.cs` | Gestapelte Balken (Tilgung + Zinsen pro Jahr, AmortizationView). |
| `BudgetGaugeVisualization.cs` | Halbkreis-Tachometer — **nicht mehr verwendet**, ersetzt durch `SkiaGradientRing` aus MeineApps.UI. |
| `ChartHelper.cs` | Gemeinsame Y-Achsen-Skalierung und Label-Formatierung für alle Chart-Renderer. |

---

## Shared-Renderer aus `MeineApps.UI.SkiaSharp`

- **DonutChartVisualization**: HomeView, StatisticsView, ExpenseTrackerView, LoanView, YieldView.
- **LinearProgressVisualization**: Budget-Fortschrittsbalken in BudgetsView.
- **SkiaGradientRing**: Gesamt-Budget in HomeView + BudgetsView.

---

## View → Renderer Zuordnung

| View | Renderer |
|------|---------|
| MainView | FinanceBackgroundRenderer (Grid.RowSpan=3) |
| HomeView | FinanceDashboardRenderer + SkiaGradientRing + SparklineVisualization + BudgetMiniRingVisualization + Expense-DonutChartVisualization |
| StatisticsView | 2× DonutChartVisualization + TrendLineVisualization |
| ExpenseTrackerView | Kategorie-DonutChartVisualization |
| BudgetsView | SkiaGradientRing + LinearProgressVisualization pro Kategorie |
| CompoundInterest/SavingsPlan/InflationView | StackedAreaVisualization |
| AmortizationView | AmortizationBarVisualization |
| LoanView / YieldView | DonutChartVisualization |

---

## Gotcha — 60-fps DispatcherTimer

`FinanceDashboardRenderer` läuft auf einem 60-fps-Timer in `HomeView`. Avalonia 12 detacht
unsichtbare Tabs **nicht** aus dem Visual Tree → Timer würde bei Tab-Wechsel weiter ticken.
`HomeView.UpdateTimerState()` abonniert `MainViewModel.IsHomeActive` und stoppt/startet den
Timer synchron — CPU/Akku bleiben bei Tab-Wechsel auf 0.
