# FinanzRechner "Living Finance" Premium Redesign

**Datum:** 2026-03-04
**Status:** Genehmigt
**Ziel:** FinanzRechner von "funktional aber langweilig" zu "Fintech Premium" upgraden

---

## Kontext

FinanzRechner hat bereits gute SkiaSharp-Charts (DonutChart, TrendLine, StackedArea, etc.) und einige Game-Juice-Elemente (FloatingText, Celebration, Pulse-Animationen). Aber im Vergleich zu BomberBlast/HandwerkerImperium fehlt das visuelle "Wow" - die App wirkt flach und kalt zwischen den funktionalen Elementen.

**Richtung:** Fintech Premium (wie Revolut/N26) - serioes aber lebendig.

---

## 1. Neue SkiaSharp-Renderer

### A) FinanceDashboardRenderer (HomeView Hintergrund)

- Subtiler animierter Hintergrund hinter dem Hero-Header
- Floating Finanz-Symbole (EUR, $, %, Trendpfeile) die langsam aufsteigen und verblassen
- Sanfter Gradient-Mesh der sich ueber 30s verschiebt (Primary -> Secondary -> Accent Farben des Themes)
- Grid-Linien die an ein Boersenchart erinnern (sehr dezent, 5-8% Opacity)
- Partikel: 12-16 Struct-basierte Glow-Dots die langsam driften
- Datei: `src/Apps/FinanzRechner/FinanzRechner.Shared/Graphics/FinanceDashboardRenderer.cs`

### B) CalculatorHeaderRenderer (6 individuelle Header)

Jeder Rechner bekommt einen einzigartigen animierten Mini-Header statt dem statischen farbigen Banner:

| Rechner | Animation |
|---------|-----------|
| Zinseszins | Wachsende Exponential-Kurve die sich aufbaut |
| Sparplan | Aufsteigende Stufen/Treppen-Pattern |
| Kredit | Abnehmende Kurve mit Tilgungsfortschritt |
| Tilgungsplan | Gestapelte Balken die sich aufbauen |
| Rendite | Aufsteigende Trendlinie mit Glow |
| Inflation | Schrumpfende Muenze/Kaufkraft-Visualisierung |

- Datei: `src/Apps/FinanzRechner/FinanzRechner.Shared/Graphics/CalculatorHeaderRenderer.cs`
- Statische Methoden pro Rechner-Typ, aufgerufen via enum Parameter

### C) CardGlowRenderer (Status-basierter Edge-Glow)

- Nutzt bestehenden `SkiaGlowEffect` aus MeineApps.UI
- Budget-Cards: Gruener Glow (<80%), Gelber Glow (80-100%), Roter pulsierender Glow (>100%)
- Hero-Header: Subtiler Glow basierend auf Bilanz (gruen wenn positiv, rot wenn negativ)
- Calculator-Ergebnis-Cards: Kurzer Gold-Flash wenn Berechnung fertig
- Datei: `src/Apps/FinanzRechner/FinanzRechner.Shared/Graphics/CardGlowRenderer.cs`

---

## 2. Behaviors aktivieren (alle 5 aus MeineApps.UI)

| Behavior | Wo | Konfiguration |
|----------|-----|---------------|
| CountUpBehavior | Hero-Bilanz, Einnahmen/Ausgaben-Pills, Statistik-Summenwerte, Rechner-Ergebnisse | Duration=800ms, Format="N2", Suffix=" EUR" |
| StaggerFadeInBehavior | Recent Transactions, Budget-Liste, Kategorie-Breakdown, Calculator-Grid-Karten | StaggerDelay=40ms, BaseDuration=300ms |
| TapScaleBehavior | Alle Buttons, Calculator-Karten, Kategorie-Chips, Tab-Bar-Items, FAB | PressedScale=0.95 |
| FadeInBehavior | Tab-Content bei Wechsel, Dialog-Oeffnung, Rechner-Ergebnis-Sektion | Duration=250ms, SlideFromBottom=true |
| SwipeToRevealBehavior | Transaktions-Liste (Delete), Dauerauftraege (Delete/Edit) | SwipeThreshold=80, RevealWidth=80 |

---

## 3. Ueberarbeitete Card-Styles

- Glasmorphismus-Touch: Cards bekommen subtile Transparenz + Border mit 10% white
- Hover/Press States: TransformOperationsTransition mit translateY(-2px) + vergroesserter BoxShadow
- Kategorie-Accent: 4px farbiger Seitenstreifen links statt oben (moderner Fintech-Look)
- Abgerundete Ecken einheitlich: CornerRadius 16 (statt gemischt 12/24)

---

## 4. View-spezifische Aenderungen

### HomeView (Dashboard)
- Hero-Header: FinanceDashboardRenderer als Hintergrund
- Bilanz mit CountUp, Income/Expense Pills mit CountUp
- Budget-Gauge: SkiaGradientRing statt BudgetGaugeVisualization (moderner, Glow + Pulse)
- Recent Transactions: StaggerFadeIn + SwipeToReveal
- Calculator-Grid: StaggerFadeIn + TapScale

### ExpenseTrackerView
- Transaktions-Liste: StaggerFadeIn + SwipeToReveal
- Month-Navigation: TapScale auf Buttons
- Filter-Chips: TapScale

### StatisticsView
- Period-Chips: TapScale + aktiver Chip bekommt subtilen Shimmer (SkiaShimmerEffect)
- Summary-Band: CountUp auf allen 3 Werten
- Charts: FadeIn beim Laden

### BudgetsView
- Budget-Cards: StaggerFadeIn + CardGlowRenderer (Status-Glow)
- Budget-Gauge: SkiaGradientRing mit Glow
- Add-Button (FAB): TapScale

### Calculator-Views (alle 6)
- Header: CalculatorHeaderRenderer mit individuellem Theme
- Ergebnis-Anzeige: CountUp + Gold-Flash
- Input-Felder: Subtiler Focus-Ring

### SettingsView
- Theme-Karten: TapScale + Shimmer auf selected Theme
- Settings-Cards: FadeIn beim Laden

### Dialoge (Quick-Add, AddBudget, AddExpense, AddRecurring)
- FadeIn + Scale-Animation (verfeinern)
- Kategorie-Chips: TapScale + Farb-Transition

---

## 5. Technische Randbedingungen

- Alle neuen Renderer: Struct-basierte Pools (kein GC-Druck)
- SKPaint/SKFont/SKPath: Gecacht, nicht pro Frame erstellt
- Theme-Integration: SkiaThemeHelper fuer Farben
- Performance-Ziel: 60fps auf Android (Samsung A-Serie)
- Bestehende Funktionalitaet darf NICHT brechen
