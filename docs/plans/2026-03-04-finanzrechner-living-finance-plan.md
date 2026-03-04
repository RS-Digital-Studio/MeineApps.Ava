# FinanzRechner "Living Finance" - Implementierungsplan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** FinanzRechner von "funktional aber langweilig" zu "Fintech Premium" upgraden mit neuen SkiaSharp-Renderern, allen 5 MeineApps.UI Behaviors und ueberarbeiteten Card-Styles.

**Architecture:** 3 neue SkiaSharp-Renderer (FinanceDashboardRenderer, CalculatorHeaderRenderer, CardGlowRenderer) plus Integration aller 5 bestehenden Behaviors (CountUp, StaggerFadeIn, TapScale, FadeIn, SwipeToReveal) in alle Views. Bestehende Funktionalitaet bleibt 100% erhalten.

**Tech Stack:** Avalonia 11.3, SkiaSharp 3.119.2, CommunityToolkit.Mvvm, MeineApps.UI Behaviors/Shaders

---

## Referenz: Wichtige Pfade

```
src/Apps/FinanzRechner/FinanzRechner.Shared/
  Views/MainView.axaml              # Haupt-Container, Tab-Navigation
  Views/HomeView.axaml              # Dashboard (Hero, Budget, Transactions, Calculators)
  Views/ExpenseTrackerView.axaml    # Transaktions-Liste
  Views/StatisticsView.axaml        # Statistiken + Charts
  Views/BudgetsView.axaml           # Budget-Verwaltung
  Views/SettingsView.axaml          # Einstellungen
  Views/CompoundInterestView.axaml  # + 5 weitere Calculator-Views
  Graphics/                         # Bestehende SkiaSharp-Renderer
  ViewModels/MainViewModel.cs       # Navigation, Tab-Switching
  ViewModels/MainViewModel.Home.cs  # Home-Dashboard Logik

src/UI/MeineApps.UI/
  Behaviors/                        # TapScale, CountUp, FadeIn, StaggerFadeIn, SwipeToReveal
  SkiaSharp/Shaders/               # SkiaGlowEffect, SkiaShimmerEffect
  SkiaSharp/SkiaGradientRing.cs    # Gradient-Fortschrittsring
  SkiaSharp/SkiaParticleSystem.cs  # Struct-basierte Partikel
  SkiaSharp/EasingFunctions.cs     # Easing-Funktionen
  SkiaSharp/SkiaThemeHelper.cs     # Theme-Farben -> SKColor
```

**xmlns fuer Behaviors** (in jeder View die Behaviors nutzt):
```xml
xmlns:i="using:Avalonia.Xaml.Interactivity"
xmlns:uiBehaviors="using:MeineApps.UI.Behaviors"
```

**xmlns fuer SkiaSharp-Controls** (wo SkiaGradientRing genutzt wird):
```xml
xmlns:skiaControls="using:MeineApps.UI.SkiaSharp"
```

---

## Task 1: FinanceDashboardRenderer erstellen

**Files:**
- Create: `src/Apps/FinanzRechner/FinanzRechner.Shared/Graphics/FinanceDashboardRenderer.cs`

**Kontext:** Subtiler animierter Hintergrund fuer den Hero-Header in der HomeView. Floating Finanz-Symbole, Gradient-Mesh, Grid-Linien, Glow-Partikel. Struct-basiert fuer 60fps.

**Step 1: Renderer implementieren**

Erstelle `FinanceDashboardRenderer.cs` mit:
- Namespace: `FinanzRechner.Graphics`
- Statische Klasse mit `Render(SKCanvas canvas, SKRect bounds, float time)` Methode
- Struct `DashboardParticle` mit X, Y, VelocityX, VelocityY, Size, Alpha, Symbol (char), Phase
- Pool: `DashboardParticle[16]` (Struct-Array, kein GC)
- Gecachte SKPaint/SKFont Instanzen (private static readonly)
- Elemente:
  1. **Gradient-Mesh**: Langsam rotierende Gradient-Farben (Primary/Secondary/Accent via SkiaThemeHelper, 5-8% Opacity)
  2. **Grid-Linien**: Horizontale + vertikale Linien die an ein Boersen-Chart erinnern (3-5% Opacity, leicht animiert)
  3. **Floating Symbole**: EUR, $, %, Trendpfeile (Unicode) die langsam aufsteigen und verblassen (30-60s Lebensdauer)
  4. **Glow-Dots**: 12-16 kleine leuchtende Punkte die langsam driften (MaskFilter.CreateBlur)
- `Initialize()` Methode die Partikel zufaellig verteilt
- `Update(float deltaTime)` Methode die Partikel bewegt und recycelt

Orientiere dich am Pattern von `FinanzRechnerSplashRenderer.cs` (gleicher Ordner) fuer Stil und Caching-Pattern. Nutze `SkiaThemeHelper.Primary`, `.Secondary`, `.Accent`, `.Background` fuer Theme-Farben.

**Step 2: Build pruefen**

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
```

**Step 3: In HomeView integrieren**

In `HomeView.axaml`: SKCanvasView als erstes Kind des Hero-Header-Borders hinzufuegen (hinter dem Content via Panel-Stacking). Der Hero-Header ist das erste Element in der StackPanel (Zeile ~18). Den Gradient-Background des Hero-Headers beibehalten, den Renderer als zusaetzliche Ebene darueber legen.

In `HomeView.axaml.cs` (oder als Inline-Handler): PaintSurface Event verdrahten, `FinanceDashboardRenderer.Render()` aufrufen. Timer fuer Animation (DispatcherTimer 16ms = ~60fps) der `InvalidateSurface()` aufruft.

**Step 4: Build + visuell pruefen**

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
dotnet run --project src/Apps/FinanzRechner/FinanzRechner.Desktop
```

**Step 5: Commit**

```bash
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Graphics/FinanceDashboardRenderer.cs
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/HomeView.axaml
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/HomeView.axaml.cs
git commit -m "feat(FinanzRechner): FinanceDashboardRenderer fuer animierten Hero-Header"
```

---

## Task 2: CalculatorHeaderRenderer erstellen

**Files:**
- Create: `src/Apps/FinanzRechner/FinanzRechner.Shared/Graphics/CalculatorHeaderRenderer.cs`
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/CompoundInterestView.axaml` (+ 5 weitere)

**Kontext:** Jeder der 6 Finanz-Rechner bekommt einen individuellen animierten Header statt dem statischen farbigen Banner.

**Step 1: Renderer implementieren**

Erstelle `CalculatorHeaderRenderer.cs` mit:
- Namespace: `FinanzRechner.Graphics`
- Statische Klasse
- Enum `CalculatorType { CompoundInterest, SavingsPlan, Loan, Amortization, Yield, Inflation }`
- Haupt-Methode: `Render(SKCanvas canvas, SKRect bounds, float time, CalculatorType type)`
- Pro Typ eine private Methode:
  - `RenderCompoundInterest`: Exponential-Kurve die sich aufbaut (wachsend), gruene Toene
  - `RenderSavingsPlan`: Aufsteigende Stufen/Treppen die von links nach rechts wachsen, blaue Toene
  - `RenderLoan`: Abnehmende Kurve mit sinkenden Balken, orange Toene
  - `RenderAmortization`: Gestapelte Balken (Tilgung+Zinsen) die sich aufbauen, rote Toene
  - `RenderYield`: Aufsteigende Trendlinie mit Glow-Trail, lila Toene
  - `RenderInflation`: Schrumpfende Muenze mit Kaufkraft-Verlust-Animation, teal Toene
- Alle Animationen: EaseOutCubic, 2-3s Loop, subtile Ausfuehrung (Hintergrund, nicht ablenkend)
- Gecachte SKPaint/SKPath Instanzen
- Farben: Die bestehenden Akzentfarben der Rechner beibehalten (Gruen, Blau, Orange, Rot, Lila, Teal)

**Step 2: Build pruefen**

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
```

**Step 3: In Calculator-Views integrieren**

In jeder der 6 Calculator-Views: Den bestehenden Header-Banner `Border` (CornerRadius="0,0,24,24") um eine SKCanvasView ergaenzen. Die bisherige Hintergrund-Farbe (IncomeBrush etc.) bleibt als Base-Layer, der Renderer zeichnet darueber.

Die Views sind:
- `CompoundInterestView.axaml` (Header Zeile ~13-46)
- `SavingsPlanView.axaml`
- `LoanView.axaml`
- `AmortizationView.axaml`
- `YieldView.axaml`
- `InflationView.axaml`

Jede View braucht:
1. SKCanvasView im Header-Border
2. PaintSurface Handler der `CalculatorHeaderRenderer.Render(canvas, bounds, time, CalculatorType.Xxx)` aufruft
3. Timer (einmalig beim Laden starten, beim Verlassen stoppen)

**Step 4: Build + visuell pruefen**

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
dotnet run --project src/Apps/FinanzRechner/FinanzRechner.Desktop
```

**Step 5: Commit**

```bash
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Graphics/CalculatorHeaderRenderer.cs
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/*View.axaml
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/*View.axaml.cs
git commit -m "feat(FinanzRechner): Individuelle animierte Calculator-Header"
```

---

## Task 3: CardGlowRenderer erstellen

**Files:**
- Create: `src/Apps/FinanzRechner/FinanzRechner.Shared/Graphics/CardGlowRenderer.cs`

**Kontext:** Status-basierter Edge-Glow auf Cards. Nutzt `SkiaGlowEffect` aus MeineApps.UI als Basis.

**Step 1: Renderer implementieren**

Erstelle `CardGlowRenderer.cs` mit:
- Namespace: `FinanzRechner.Graphics`
- Statische Klasse
- Methoden:
  - `RenderBudgetGlow(SKCanvas canvas, SKRect bounds, float time, float budgetPercent)`:
    - <80%: Gruener Glow (SkiaGlowEffect.DrawEdgeGlow mit SKColors gruen)
    - 80-100%: Gelber Glow (Warning)
    - >100%: Roter pulsierender Glow (schnellerer pulseSpeed)
  - `RenderBalanceGlow(SKCanvas canvas, SKRect bounds, float time, bool isPositive)`:
    - Positiv: Subtiler gruener Edge-Glow
    - Negativ: Subtiler roter Edge-Glow
  - `RenderCalculationFlash(SKCanvas canvas, SKRect bounds, float time, float flashProgress)`:
    - Kurzer Gold-Flash (0->1->0 ueber 0.5s) wenn Berechnung fertig
    - Nutzt SkiaGlowEffect.DrawEdgeGlow mit Gold-Farbe und hoher Intensitaet bei flashProgress ~0.5

**Step 2: Build pruefen**

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
```

**Step 3: Commit**

```bash
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Graphics/CardGlowRenderer.cs
git commit -m "feat(FinanzRechner): CardGlowRenderer fuer Status-Glow auf Cards"
```

---

## Task 4: Behaviors in HomeView integrieren

**Files:**
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/HomeView.axaml`

**Kontext:** HomeView bekommt CountUp, StaggerFadeIn, TapScale und FadeIn Behaviors.

**Step 1: xmlns-Imports hinzufuegen**

In HomeView.axaml die fehlenden xmlns hinzufuegen:
```xml
xmlns:i="using:Avalonia.Xaml.Interactivity"
xmlns:uiBehaviors="using:MeineApps.UI.Behaviors"
```

**Step 2: CountUpBehavior auf Geldbetraege**

Auf folgende TextBlocks CountUpBehavior anwenden:
- Hero-Header Bilanz (der grosse Betrag oben)
- Income-Pill Betrag
- Expense-Pill Betrag

```xml
<TextBlock FontSize="28" FontWeight="Bold" Text="{Binding TotalBalance, StringFormat={}{0:N2} €}">
  <!-- ERSETZEN DURCH: -->
<TextBlock FontSize="28" FontWeight="Bold">
  <i:Interaction.Behaviors>
    <uiBehaviors:CountUpBehavior TargetValue="{Binding TotalBalance}" Duration="800" Format="N2" Suffix=" €" />
  </i:Interaction.Behaviors>
</TextBlock>
```

**Step 3: StaggerFadeInBehavior auf Listen**

- Recent Transactions: StaggerFadeIn auf das ItemTemplate-Border der Transaction-Liste
- Calculator-Grid: StaggerFadeIn auf die 6 Calculator-Karten

Fuer Recent Transactions (im ItemsControl ItemTemplate):
```xml
<Border Classes="Card" ...>
  <i:Interaction.Behaviors>
    <uiBehaviors:StaggerFadeInBehavior StaggerDelay="40" BaseDuration="300" />
  </i:Interaction.Behaviors>
  <!-- bestehender Content -->
</Border>
```

Fuer Calculator-Grid (auf jede der 6 Karten):
```xml
<Border Classes="Card" ... >
  <i:Interaction.Behaviors>
    <uiBehaviors:StaggerFadeInBehavior StaggerDelay="60" BaseDuration="300" FixedIndex="0" />
    <!-- FixedIndex 0-5 pro Karte -->
  </i:Interaction.Behaviors>
  <!-- bestehender Content -->
</Border>
```

**Step 4: TapScaleBehavior auf interaktive Elemente**

- Calculator-Karten (alle 6)
- Quick-Add FAB Button
- Category-Chips im Quick-Add Dialog

```xml
<Button Classes="Primary" ...>
  <i:Interaction.Behaviors>
    <uiBehaviors:TapScaleBehavior PressedScale="0.95" />
  </i:Interaction.Behaviors>
  <!-- bestehender Content -->
</Button>
```

**Step 5: Build pruefen**

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
```

**Step 6: Commit**

```bash
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/HomeView.axaml
git commit -m "feat(FinanzRechner): Behaviors in HomeView (CountUp, StaggerFadeIn, TapScale)"
```

---

## Task 5: Behaviors in ExpenseTrackerView integrieren

**Files:**
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/ExpenseTrackerView.axaml`

**Step 1: xmlns-Imports + Behaviors hinzufuegen**

- `xmlns:i` und `xmlns:uiBehaviors` hinzufuegen
- **StaggerFadeInBehavior** auf Transaction-Liste Items
- **SwipeToRevealBehavior** auf Transaction-Items (Swipe links -> Delete-Button freilegen)
- **TapScaleBehavior** auf Month-Navigation Buttons, Sort/Filter Buttons, Category-Chips
- **FadeInBehavior** auf die Haupt-Content-Sektion

Fuer SwipeToReveal (im ItemTemplate der Transaktions-Liste):
```xml
<Panel ClipToBounds="True">
  <!-- Delete-Layer (hinter dem Content) -->
  <Border Background="#EF4444" HorizontalAlignment="Right" Width="80" CornerRadius="0,12,12,0">
    <Button Command="{Binding $parent[ItemsControl].((vm:ExpenseTrackerViewModel)DataContext).DeleteExpenseCommand}"
            CommandParameter="{Binding}"
            Background="Transparent">
      <mi:MaterialIcon Kind="Delete" Foreground="White" Width="24" Height="24" />
    </Button>
  </Border>
  <!-- Content-Layer (verschiebbar) -->
  <Border Background="{DynamicResource CardBrush}" CornerRadius="12" Padding="12">
    <i:Interaction.Behaviors>
      <uiBehaviors:SwipeToRevealBehavior SwipeThreshold="80" RevealWidth="80" />
    </i:Interaction.Behaviors>
    <!-- bestehender Transaction-Content -->
  </Border>
</Panel>
```

**Step 2: Build pruefen**

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
```

**Step 3: Commit**

```bash
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/ExpenseTrackerView.axaml
git commit -m "feat(FinanzRechner): Behaviors in ExpenseTrackerView (Swipe, Stagger, TapScale)"
```

---

## Task 6: Behaviors in StatisticsView integrieren

**Files:**
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/StatisticsView.axaml`

**Step 1: xmlns + Behaviors**

- **CountUpBehavior** auf Summary-Band Werte (Einnahmen, Ausgaben, Bilanz)
- **TapScaleBehavior** auf Period-Chips (Week, Month, Quarter, HalfYear, Year)
- **FadeInBehavior** auf Chart-Sektionen
- **StaggerFadeInBehavior** auf Kategorie-Breakdown Listen

**Step 2: Shimmer auf aktivem Period-Chip**

Den aktiven Period-Chip (selected=true) mit einem SkiaShimmerEffect versehen. Dafuer in StatisticsView.axaml.cs:
- Wenn Chip selected: SKCanvasView als Overlay einblenden die SkiaShimmerEffect.DrawShimmerOverlay zeichnet
- ODER einfacher: CSS-basierte Shimmer-Animation (Opacity-Pulse 0.85->1.0->0.85, 3s infinite) - das ist bereits als PremiumShimmer-Style in MainView.axaml vorhanden, wiederverwenden

**Step 3: Build + Commit**

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/StatisticsView.axaml
git commit -m "feat(FinanzRechner): Behaviors in StatisticsView (CountUp, TapScale, FadeIn)"
```

---

## Task 7: Behaviors in BudgetsView integrieren

**Files:**
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/BudgetsView.axaml`

**Step 1: xmlns + Behaviors**

- **StaggerFadeInBehavior** auf Budget-Cards
- **TapScaleBehavior** auf FAB + Edit-Buttons
- **FadeInBehavior** auf Dialog-Content
- **CountUpBehavior** auf Budget-Betraege

**Step 2: CardGlowRenderer auf Budget-Cards**

In BudgetsView.axaml oder BudgetsView.axaml.cs:
- SKCanvasView als Overlay auf jeder Budget-Card
- `CardGlowRenderer.RenderBudgetGlow()` mit dem jeweiligen Budget-Prozentsatz aufrufen
- Timer fuer Animation (kann vom MainView-Timer gesteuert werden oder eigener)

Alternative (einfacher): Per CSS-Klasse `.overBudget` / `.warningBudget` / `.okBudget` den Border mit farbigem BoxShadow versehen. Weniger aufwendig als SKCanvasView pro Card, aber weniger Premium.

Empfehlung: CSS-BoxShadow fuer Budget-Status + SKCanvasView-Glow nur auf der Gesamt-Budget-Gauge.

**Step 3: Build + Commit**

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/BudgetsView.axaml
git commit -m "feat(FinanzRechner): Behaviors + Status-Glow in BudgetsView"
```

---

## Task 8: Behaviors in SettingsView + Dialoge integrieren

**Files:**
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/SettingsView.axaml`
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/MainView.axaml` (Quick-Add Dialog)

**Step 1: SettingsView**

- **TapScaleBehavior** auf Theme-Karten (die 4 Theme-Previews)
- **FadeInBehavior** auf Settings-Cards
- **Shimmer** auf selected Theme-Karte (PremiumShimmer CSS-Klasse)

**Step 2: MainView Dialoge**

- Quick-Add Dialog: **TapScaleBehavior** auf Kategorie-Chips und Type-Buttons (Expense/Income)
- Quick-Add Dialog: **FadeInBehavior** auf Dialog-Content (ergaenzt die bestehende Scale-Animation)
- Calculator-Overlay: **FadeInBehavior** auf Calculator-Views beim Oeffnen

**Step 3: Build + Commit**

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/SettingsView.axaml
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/MainView.axaml
git commit -m "feat(FinanzRechner): Behaviors in Settings + Dialoge (TapScale, FadeIn, Shimmer)"
```

---

## Task 9: SkiaGradientRing fuer Budget-Gauge

**Files:**
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/HomeView.axaml`
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/BudgetsView.axaml`

**Kontext:** BudgetGaugeVisualization (Halbkreis-Tachometer) durch SkiaGradientRing ersetzen - moderner, mit Glow und Pulse.

**Step 1: HomeView Budget-Gauge ersetzen**

Die bestehende SKCanvasView fuer BudgetGaugeVisualization in HomeView.axaml finden und durch SkiaGradientRing ersetzen:

```xml
<!-- ALT: SKCanvasView mit BudgetGaugeVisualization -->
<!-- NEU: -->
<skiaControls:SkiaGradientRing
    Width="140" Height="140"
    Value="{Binding BudgetUsagePercent}"
    StartColor="#22C55E"
    EndColor="#EF4444"
    GlowEnabled="True"
    ShowTickMarks="False"
    IsPulsing="{Binding IsBudgetCritical}" />
```

xmlns hinzufuegen: `xmlns:skiaControls="using:MeineApps.UI.SkiaSharp"`

**Step 2: BudgetsView Gesamt-Budget-Gauge ersetzen**

Analog in BudgetsView.axaml die Gesamt-Budget SKCanvasView durch SkiaGradientRing ersetzen.

**Step 3: ViewModel-Properties pruefen/anpassen**

In `MainViewModel.Home.cs` pruefen ob `BudgetUsagePercent` (0.0-1.0 als double) und `IsBudgetCritical` (bool) existieren. Falls nicht, hinzufuegen:
- `BudgetUsagePercent`: Gesamtausgaben / Gesamtbudget-Limit (clamped auf 0-1)
- `IsBudgetCritical`: true wenn >90%

**Step 4: Build + visuell pruefen**

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
dotnet run --project src/Apps/FinanzRechner/FinanzRechner.Desktop
```

**Step 5: Commit**

```bash
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/HomeView.axaml
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/BudgetsView.axaml
git add src/Apps/FinanzRechner/FinanzRechner.Shared/ViewModels/MainViewModel.Home.cs
git commit -m "feat(FinanzRechner): SkiaGradientRing statt BudgetGauge (mit Glow + Pulse)"
```

---

## Task 10: Card-Styles ueberarbeiten

**Files:**
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/MainView.axaml` (Styles-Sektion)
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/HomeView.axaml`

**Kontext:** Cards bekommen Glasmorphismus-Touch, einheitliche CornerRadius, und farbige Seitenstreifen.

**Step 1: Neue Card-Styles definieren**

In MainView.axaml Styles-Sektion (Zeile ~13) neue Styles hinzufuegen:

```xml
<!-- Fintech Premium Card -->
<Style Selector="Border.FintechCard">
  <Setter Property="Background" Value="{DynamicResource CardBrush}" />
  <Setter Property="CornerRadius" Value="16" />
  <Setter Property="BorderBrush" Value="{DynamicResource BorderSubtleBrush}" />
  <Setter Property="BorderThickness" Value="1" />
  <Setter Property="Padding" Value="16" />
  <Setter Property="Margin" Value="16,8" />
  <Setter Property="BoxShadow" Value="0 2 8 0 #20000000" />
  <Setter Property="RenderTransform" Value="scale(1)" />
  <Setter Property="RenderTransformOrigin" Value="50%,50%" />
  <Setter Property="Transitions">
    <Transitions>
      <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.2" Easing="CubicEaseOut" />
      <BoxShadowsTransition Property="BoxShadow" Duration="0:0:0.2" />
    </Transitions>
  </Setter>
</Style>
<Style Selector="Border.FintechCard:pointerover">
  <Setter Property="RenderTransform" Value="translateY(-2px)" />
  <Setter Property="BoxShadow" Value="0 4 16 0 #30000000" />
</Style>
```

**Step 2: Bestehende Cards migrieren**

In HomeView.axaml die bestehenden `Classes="Card"` durch `Classes="FintechCard"` ersetzen wo sinnvoll (Calculator-Karten, Budget-Card, Recent-Transactions-Card).

WICHTIG: `RenderTransform="scale(1)"` MUSS gesetzt sein bei `TransformOperationsTransition` (bekannter Gotcha - Crash ohne initialen Wert auf Android).

**Step 3: Kategorie-Accent Seitenstreifen**

Fuer Calculator-Grid-Karten: 4px farbiger Streifen links statt oben. Bestehenden oberen Accent-Balken entfernen, stattdessen `BorderBrush` links setzen via `BorderThickness="4,0,0,0"` und farbigem `BorderBrush`.

**Step 4: Build + Commit**

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/MainView.axaml
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/HomeView.axaml
git commit -m "feat(FinanzRechner): Fintech Premium Card-Styles mit Glasmorphismus-Touch"
```

---

## Task 11: Calculator-Views Ergebnis-Animation

**Files:**
- Modify: 6 Calculator-Views (axaml + axaml.cs)
- Genutzt: `CardGlowRenderer.RenderCalculationFlash()`

**Kontext:** Wenn eine Berechnung durchgefuehrt wird, sollen die Ergebnis-Zahlen mit CountUp hochzaehlen und die Ergebnis-Card einen kurzen Gold-Flash bekommen.

**Step 1: CountUpBehavior auf Ergebnis-TextBlocks**

In jeder Calculator-View die Ergebnis-Anzeige mit CountUpBehavior versehen:
- Endbetrag, Zinsen, Gesamtbetrag etc.
- Duration=600ms, Format="N2", Suffix=" EUR"

**Step 2: Gold-Flash bei Berechnung**

Option A (einfacher): CSS-Animation `CalculationFlash` - Border-BorderBrush blinkt kurz Gold (#FFD700) auf und fadet zurueck.
Option B (premium): SKCanvasView Overlay mit CardGlowRenderer.RenderCalculationFlash()

Empfehlung: Option A fuer schnellere Umsetzung, spaeter optional auf Option B upgraden.

```xml
<Style Selector="Border.CalculationResult.Flash">
  <Style.Animations>
    <Animation Duration="0:0:0.6" FillMode="Forward">
      <KeyFrame Cue="0%">
        <Setter Property="BorderBrush" Value="Transparent" />
      </KeyFrame>
      <KeyFrame Cue="30%">
        <Setter Property="BorderBrush" Value="#FFD700" />
      </KeyFrame>
      <KeyFrame Cue="100%">
        <Setter Property="BorderBrush" Value="Transparent" />
      </KeyFrame>
    </Animation>
  </Style.Animations>
</Style>
```

**Step 3: Build + Commit**

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/*View.axaml
git commit -m "feat(FinanzRechner): CountUp + Gold-Flash auf Calculator-Ergebnisse"
```

---

## Task 12: Tab-Bar + Navigation aufwerten

**Files:**
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/MainView.axaml`

**Kontext:** Tab-Bar Items bekommen TapScale und die Tab-Content-Wechsel bekommen FadeIn + SlideFromBottom.

**Step 1: TapScaleBehavior auf Tab-Items**

Jeder der 4 Tab-Buttons (Home, Tracker, Statistics, Settings) in der Navigation Bar bekommt:
```xml
<i:Interaction.Behaviors>
  <uiBehaviors:TapScaleBehavior PressedScale="0.92" />
</i:Interaction.Behaviors>
```

**Step 2: Verbesserter Tab-Content Transition**

Die bestehende `Border.TabContent` Opacity-Transition (150ms) ggf. durch eine Kombination aus Opacity + translateY ersetzen fuer einen "Slide-Up"-Effekt:

```xml
<Style Selector="Border.TabContent.Active">
  <Setter Property="Opacity" Value="1" />
  <Setter Property="RenderTransform" Value="translateY(0px)" />
</Style>
<Style Selector="Border.TabContent:not(.Active)">
  <Setter Property="Opacity" Value="0" />
  <Setter Property="RenderTransform" Value="translateY(8px)" />
</Style>
```

WICHTIG: `RenderTransform="translateY(0px)"` als initialer Wert noetig (Gotcha).

**Step 3: Build + Commit**

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/MainView.axaml
git commit -m "feat(FinanzRechner): Tab-Bar TapScale + verbesserter Tab-Content Transition"
```

---

## Task 13: RecurringTransactionsView Behaviors

**Files:**
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/RecurringTransactionsView.axaml` (falls vorhanden als eigene View)

**Step 1: Behaviors hinzufuegen**

- **StaggerFadeInBehavior** auf Dauerauftrags-Liste
- **SwipeToRevealBehavior** auf Dauerauftrags-Items (Delete/Edit)
- **TapScaleBehavior** auf Add-Button und Action-Buttons

**Step 2: Build + Commit**

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
git add src/Apps/FinanzRechner/FinanzRechner.Shared/Views/RecurringTransactionsView.axaml
git commit -m "feat(FinanzRechner): Behaviors in RecurringTransactionsView"
```

---

## Task 14: Finaler Build + AppChecker + CLAUDE.md Update

**Files:**
- Modify: `src/Apps/FinanzRechner/CLAUDE.md`

**Step 1: Solution Build**

```bash
dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln
```

**Step 2: AppChecker laufen lassen**

```bash
dotnet run --project tools/AppChecker FinanzRechner
```

Eventuelle Findings fixen.

**Step 3: CLAUDE.md aktualisieren**

In `src/Apps/FinanzRechner/CLAUDE.md` die neuen Renderer, Behaviors und Styles dokumentieren:
- Neue Graphics-Dateien (FinanceDashboardRenderer, CalculatorHeaderRenderer, CardGlowRenderer)
- Behavior-Nutzung pro View
- Neue Card-Styles (FintechCard)
- SkiaGradientRing statt BudgetGaugeVisualization

**Step 4: Desktop visuell pruefen**

```bash
dotnet run --project src/Apps/FinanzRechner/FinanzRechner.Desktop
```

Alle Views durchklicken: Home, Tracker, Statistics, Budgets, Settings, alle 6 Calculator.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(FinanzRechner): Living Finance Premium Redesign komplett"
```
