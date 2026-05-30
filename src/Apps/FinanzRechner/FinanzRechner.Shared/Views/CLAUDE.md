# Views — AXAML-Views & UI-Patterns

Alle 18 Views haben `x:CompileBindings="True"` + `x:DataType` — falsche Property-Bindings
fliegen beim Build auf. Generische AXAML-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainView.axaml(.cs)` | Shell: 4-Tab-Bar (56 dp), Ad-Spacer (64 dp), FinanceBackgroundRenderer (Grid.RowSpan=3) |
| `HomeView.axaml(.cs)` | Dashboard: FinanceDashboardRenderer, SkiaGradientRing, Sparkline, MiniRing, Expense-Donut, Quick-Add FAB, Calculator-Grid |
| `ExpenseTrackerView.axaml(.cs)` | Expense-Liste, Monatsnavigation, SwipeToReveal, Undo-Snackbar, AddExpense-Overlay |
| `StatisticsView.axaml(.cs)` | 2× Donut, TrendLine, Monatsvergleich, CSV/PDF-Export |
| `SettingsView.axaml(.cs)` | Backup, Restore-Overlay (Merge/Replace), Währung, Sprache, Premium |
| `BudgetsView.axaml(.cs)` | SkiaGradientRing, LinearProgress pro Kategorie, ContextFlyout |
| `AccountsView.axaml(.cs)` | Konto-Liste, Überweisungs-Formular |
| `SavingsGoalsView.axaml(.cs)` | Sparzielliste + Einzahlungs-Dialog |
| `DebtTrackerView.axaml(.cs)` | Schuldenliste + Zahlungsformular |
| `CustomCategoriesView.axaml(.cs)` | Icon- und Farbwahl für eigene Kategorien |
| `RecurringTransactionsView.axaml(.cs)` | Dauerauftragsliste, SwipeToReveal, Toggle |
| `Calculators/CompoundInterestView.axaml(.cs)` | Zinseszins-Formular + StackedAreaVisualization |
| `Calculators/SavingsPlanView.axaml(.cs)` | Sparplan-Formular + StackedAreaVisualization |
| `Calculators/LoanView.axaml(.cs)` | Kredit-Formular + DonutChartVisualization |
| `Calculators/AmortizationView.axaml(.cs)` | Tilgungsplan-Formular + AmortizationBarVisualization |
| `Calculators/YieldView.axaml(.cs)` | Rendite-Formular + DonutChartVisualization |
| `Calculators/InflationView.axaml(.cs)` | Inflations-Formular + StackedAreaVisualization |
| `MainWindow.axaml(.cs)` | Desktop-Host-Fenster (nur Desktop-Lifetime) |

---

## Ad-Banner Layout

```
MainView Grid: RowDefinitions="*,Auto,Auto"
  Row 0  Content (HomeView, TrackerView, …)
  Row 1  Ad-Spacer (64 dp) — Adaptive Banner kann 50–60 dp+ hoch sein
  Row 2  Tab-Bar (56 dp)
```

`FinanceBackgroundRenderer` wird mit `Grid.Row="0" Grid.RowSpan="3"` hinter allen Layern gezeichnet.

---

## HomeView 60-fps-Timer-Pattern

Der Dashboard-Hintergrund (`FinanceDashboardRenderer`) läuft auf einem 60-fps DispatcherTimer.
Avalonia 12 detacht `IsVisible="False"`-Tabs **nicht** aus dem Visual Tree — ohne Schutz würde der
Timer bei Tab-Wechsel weiter ticken (CPU/Akku-Verschwendung).

`HomeView.UpdateTimerState()` abonniert `MainViewModel.IsHomeActive` und stoppt/startet den
Timer synchron beim Tab-Wechsel.

---

## Overlay-Pattern (kein Popup)

Overlay-Panels (QuickAdd, RestoreDialog, BudgetAnalysis) sind inline `Border`-Elemente mit
`IsVisible`-Binding, **nicht** `Avalonia.Controls.Primitives.Popup`. Grund: Popup erzeugt auf
Desktop ein separates OS-Fenster — inkonsistentes Verhalten gegenüber Android.

Konsequenz: Overlays müssen per ZIndex-Equivalent (Grid-Reihenfolge) oder separatem Content-Slot
gestapelt werden; der Hit-Test greift zuverlässig auf Allen Plattformen.

---

## Behaviors pro View

| View | Behaviors |
|------|-----------|
| HomeView | CountUpBehavior (Saldo/Einnahmen/Ausgaben, 800 ms CubicEaseOut), StaggerFadeInBehavior (Recent-Items 40 ms, Calc-Karten 60 ms FixedIndex 0–5), TapScaleBehavior (0,92–0,97) |
| ExpenseTrackerView | FadeInBehavior (250 ms SlideFromBottom), StaggerFadeInBehavior (40 ms), SwipeToRevealBehavior (80 px → roter Delete-Layer), TapScaleBehavior |
| BudgetsView | FadeInBehavior, StaggerFadeInBehavior (50 ms), CountUpBehavior (Spent/Remaining/Limit), TapScaleBehavior, AlertLevelToBoxShadowConverter, ContextFlyout |
| RecurringTransactionsView | FadeInBehavior, StaggerFadeInBehavior (40 ms), SwipeToRevealBehavior, TapScaleBehavior |
| Calculator-Views (alle 6) | CountUpBehavior (Geldbeträge, 600 ms), TapScaleBehavior (Berechnen-Button 0,95), Gold-Flash (ResultFlash CSS) |

---

## Gotchas

**Kombinierter StaggerFadeIn + TapScale auf Calculator-Karten:** Beide Behaviors setzen
unterschiedliche `RenderTransform`-Typen. Lösung: Panel-Wrapper erhält StaggerFadeIn (Scale-X/Y),
Button-Kind erhält TapScale (Tap-Scale) — zwei unabhängige RenderTransform-Ebenen.

**Undo-Countdown:** ScaleX 1→0 über 5 s als visueller Fortschrittsbalken in Undo-Snackbars.

**ItemsControl statt ItemsRepeater:** Avalonia-12-ItemsRepeater hat Re-Mount-Probleme bei
`Clear+Add`. Bei realistischen User-Daten (< 100 Tagesgruppen, < 30 Transaktionen pro Tag)
ist Virtualisierung overkill und würde StaggerFadeIn/SwipeToReveal-Behaviors brechen.
