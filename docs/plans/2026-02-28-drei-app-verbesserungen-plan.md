# Drei-App-Verbesserungspaket - Implementierungsplan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** ~35 Verbesserungen über HandwerkerRechner, FinanzRechner und FitnessRechner implementieren (UX-Fixes, neue Features, Animationen, neue Rechner).

**Architecture:** Shared-First (EasingFunctions + AnimatedVisualizationBase in MeineApps.UI), dann App-Sequential (HandwerkerRechner → FinanzRechner → FitnessRechner). Bestehende Patterns strikt folgen (static Render(), CraftEngine/FinanceEngine/FitnessEngine, CountUpBehavior, RESX 6-Sprachen).

**Tech Stack:** Avalonia 11.3, SkiaSharp 3.119.2, CommunityToolkit.Mvvm, MeineApps.UI (Shared), sqlite-net, 6-Sprachen RESX

**Design-Dokument:** `docs/plans/2026-02-28-drei-app-verbesserungen-design.md`

---

## Phase 0: Shared Infrastructure

### Task 0.1: EasingFunctions nach MeineApps.UI verschieben

**Files:**
- Move: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/EasingFunctions.cs` → `src/UI/MeineApps.UI/SkiaSharp/EasingFunctions.cs`
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/EasingFunctions.cs` (Weiterleitung)
- Modify: Alle HandwerkerImperium-Dateien die `HandwerkerImperium.Graphics.EasingFunctions` nutzen

**Step 1: Datei kopieren und Namespace ändern**

Kopiere `HandwerkerImperium.Shared/Graphics/EasingFunctions.cs` nach `MeineApps.UI/SkiaSharp/EasingFunctions.cs`. Ändere den Namespace:

```csharp
namespace MeineApps.UI.SkiaSharp;

/// <summary>
/// Mathematische Easing-Funktionen für alle Animationen.
/// Alle Funktionen nehmen t (0.0 bis 1.0) und geben den transformierten Wert zurück.
/// </summary>
public static class EasingFunctions
{
    // ... identischer Inhalt wie bisher
}
```

**Step 2: HandwerkerImperium-Original durch Weiterleitung ersetzen**

```csharp
// HandwerkerImperium.Shared/Graphics/EasingFunctions.cs
// Weiterleitung auf die Shared-Version in MeineApps.UI
global using EasingFunctions = MeineApps.UI.SkiaSharp.EasingFunctions;
```

Alternativ: Alle `using HandwerkerImperium.Graphics;` durch `using MeineApps.UI.SkiaSharp;` ersetzen in den Dateien die EasingFunctions nutzen. Dann die alte Datei löschen.

**Step 3: Build prüfen**

```bash
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared
dotnet build src/UI/MeineApps.UI
```

**Step 4: Commit**

```bash
git add src/UI/MeineApps.UI/SkiaSharp/EasingFunctions.cs
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/EasingFunctions.cs
git commit -m "refactor: EasingFunctions nach MeineApps.UI verschoben"
```

---

### Task 0.2: AnimatedVisualizationBase erstellen

**Files:**
- Create: `src/UI/MeineApps.UI/SkiaSharp/AnimatedVisualizationBase.cs`

**Step 1: AnimatedVisualizationBase implementieren**

```csharp
// src/UI/MeineApps.UI/SkiaSharp/AnimatedVisualizationBase.cs
using System;

namespace MeineApps.UI.SkiaSharp;

/// <summary>
/// Basis für SkiaSharp-Renderer die eine Einschwing-Animation beim ersten Render unterstützen.
/// Verwendung: Statische Instanz pro Visualization, StartAnimation() bei Datenwechsel aufrufen,
/// AnimationProgress in Render() verwenden.
/// </summary>
public class AnimatedVisualizationBase
{
    private float _animationProgress;
    private bool _isAnimating;
    private DateTime _animationStart;

    /// <summary>Animationsdauer in Millisekunden (Default: 600ms).</summary>
    public float AnimationDurationMs { get; set; } = 600f;

    /// <summary>Easing-Funktion (Default: EaseOutCubic).</summary>
    public Func<float, float> EasingFunction { get; set; } = EasingFunctions.EaseOutCubic;

    /// <summary>Aktueller Animationsfortschritt (0.0-1.0, bereits mit Easing).</summary>
    public float AnimationProgress => _animationProgress;

    /// <summary>True wenn Animation noch läuft.</summary>
    public bool IsAnimating => _isAnimating;

    /// <summary>Startet die Animation von 0. Mehrfach-Aufruf startet neu.</summary>
    public void StartAnimation()
    {
        _animationStart = DateTime.UtcNow;
        _isAnimating = true;
        _animationProgress = 0f;
    }

    /// <summary>
    /// Aktualisiert den Animationsfortschritt. Vor jedem Render() aufrufen.
    /// Gibt true zurück solange die Animation läuft (für InvalidateSurface-Loop).
    /// </summary>
    public bool UpdateAnimation()
    {
        if (!_isAnimating)
        {
            _animationProgress = 1f;
            return false;
        }

        var elapsed = (float)(DateTime.UtcNow - _animationStart).TotalMilliseconds;
        var rawProgress = Math.Clamp(elapsed / AnimationDurationMs, 0f, 1f);

        _animationProgress = EasingFunction(rawProgress);

        if (rawProgress >= 1f)
        {
            _isAnimating = false;
            _animationProgress = 1f;
            return false;
        }

        return true;
    }

    /// <summary>Setzt Animation sofort auf Ende (kein Einschwingen).</summary>
    public void SkipAnimation()
    {
        _isAnimating = false;
        _animationProgress = 1f;
    }
}
```

**Step 2: Build prüfen**

```bash
dotnet build src/UI/MeineApps.UI
```

**Step 3: CLAUDE.md aktualisieren**

In `src/UI/MeineApps.UI/CLAUDE.md` unter `SkiaSharp/` hinzufügen:
```
│   ├── EasingFunctions.cs           # Mathematische Easing-Funktionen (aus HI verschoben)
│   ├── AnimatedVisualizationBase.cs # Basis für animierte Renderer
```

**Step 4: Commit**

```bash
git add src/UI/MeineApps.UI/SkiaSharp/AnimatedVisualizationBase.cs
git add src/UI/MeineApps.UI/CLAUDE.md
git commit -m "feat: AnimatedVisualizationBase für animierte SkiaSharp-Renderer"
```

---

## Phase 1: HandwerkerRechner

### Task 1.1: Beschreibungstexte auf Home-Cards

**Files:**
- Modify: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/Views/MainView.axaml`

**Step 1: TextBlock für Beschreibung hinzufügen**

In jeder Calculator-Card (2x3 Grid) einen zweiten TextBlock unter dem Titel einfügen. Beispiel für Fliesen-Karte:

```xml
<StackPanel VerticalAlignment="Center" Spacing="4">
  <Border CornerRadius="12" Width="48" Height="48" HorizontalAlignment="Center">
    <!-- Icon wie bisher -->
  </Border>
  <TextBlock Text="{Binding CalcTilesLabel}"
             FontSize="13" FontWeight="Bold" TextAlignment="Center"
             Foreground="{DynamicResource TextPrimaryBrush}" />
  <TextBlock Text="{Binding CalcTilesDescLabel}"
             FontSize="11" TextAlignment="Center" MaxLines="2"
             TextTrimming="CharacterEllipsis"
             Foreground="{DynamicResource TextSecondaryBrush}" />
</StackPanel>
```

Das `Spacing` des StackPanel von `8` auf `4` reduzieren. Die `Height` der Button-Cards ggf. von `120` auf `130` erhöhen.

Für alle 11 Rechner-Karten (Free: Tiles, Wallpaper, Paint, Flooring, Concrete; Premium: Drywall, Electrical, Metal, Garden, RoofSolar, Stairs) denselben TextBlock einfügen.

**Step 2: RESX-Keys hinzufügen (falls noch nicht vorhanden)**

In `AppStrings.resx` (+ .de/.en/.es/.fr/.it/.pt) prüfen ob `CalcTilesDesc`, `CalcWallpaperDesc`, etc. existieren. Falls nicht, 11 Keys × 6 Sprachen anlegen. Die Properties im MainViewModel (`CalcTilesDescLabel` etc.) sollten bereits vorhanden sein - prüfen und ggf. ergänzen.

**Step 3: Build prüfen**

```bash
dotnet build src/Apps/HandwerkerRechner/HandwerkerRechner.Shared
```

**Step 4: Commit**

```bash
git add src/Apps/HandwerkerRechner/
git commit -m "feat(HandwerkerRechner): Beschreibungstexte auf Home-Cards"
```

---

### Task 1.2: Ergebnis-Hauptzahl vergrößern (Hero-Value)

**Files:**
- Modify: Alle 11 Calculator-Views in `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/Views/`

**Step 1: Result-Section überarbeiten**

In jeder Calculator-View die Result-Section umbauen. Das Hauptergebnis (z.B. `TilesWithWasteDisplay`) bekommt:

```xml
<!-- Hero-Value: Hauptergebnis groß und prominent -->
<Border Background="{DynamicResource AccentBrush}" CornerRadius="12" Padding="16,12"
        Margin="0,4,0,8">
  <StackPanel>
    <TextBlock Text="{loc:Translate TilesNeeded}" FontSize="12"
               Foreground="#CCFFFFFF" />
    <TextBlock Text="{Binding TilesWithWasteDisplay}" FontSize="28" FontWeight="Bold"
               Foreground="White" />
  </StackPanel>
</Border>

<!-- Sekundärwerte: kleiner, muted -->
<Grid ColumnDefinitions="*,Auto">
  <TextBlock Text="{loc:Translate TotalArea}" FontSize="14"
             Foreground="{DynamicResource TextSecondaryBrush}" />
  <TextBlock Grid.Column="1" Text="{Binding AreaDisplay}" FontSize="14"
             Foreground="{DynamicResource TextPrimaryBrush}" />
</Grid>
```

Dieses Pattern analog zu FinanzRechner (CompoundInterestView) anwenden. Das bisherige `FontSize="16"` für das Hauptergebnis durch die Hero-Box ersetzen.

**Step 2: Für alle 11 Views anwenden**

- `Floor/TileCalculatorView.axaml` → TilesWithWasteDisplay
- `Floor/WallpaperCalculatorView.axaml` → RollsNeededDisplay
- `Floor/PaintCalculatorView.axaml` → LitersNeededDisplay
- `Floor/FlooringCalculatorView.axaml` → PlanksNeededDisplay
- `Floor/ConcreteCalculatorView.axaml` → ConcreteBagsDisplay
- `Premium/DrywallView.axaml` → PlatesNeededDisplay
- `Premium/ElectricalView.axaml` → Hauptergebnis (je nach aktuellem Sub-Rechner)
- `Premium/MetalView.axaml` → WeightDisplay
- `Premium/GardenView.axaml` → Hauptergebnis
- `Premium/RoofSolarView.axaml` → Hauptergebnis
- `Premium/StairsView.axaml` → StairsCountDisplay

**Step 3: Build + Commit**

---

### Task 1.3: CountUp-Animation auf Ergebnis-Hauptzahlen

**Files:**
- Modify: Alle 11 Calculator-Views

**Step 1: CountUpBehavior auf Hero-Value anbinden**

Für jede View den CountUpBehavior auf den Hero-Value TextBlock:

```xml
xmlns:behaviors="using:MeineApps.UI.Behaviors"
xmlns:i="using:Avalonia.Xaml.Interactivity"

<TextBlock Text="{Binding TilesWithWasteDisplay}" FontSize="28" FontWeight="Bold"
           Foreground="White">
  <i:Interaction.Behaviors>
    <behaviors:CountUpBehavior TargetValue="{Binding Result.TilesWithWaste}"
                               Format="F0" Suffix=" Stk." Duration="500" />
  </i:Interaction.Behaviors>
</TextBlock>
```

**Wichtig:** `TargetValue` muss an den numerischen Wert binden (nicht den formatierten String). Prüfen ob die Result-Records (`TileResult`, etc.) die nötigen numerischen Properties exponieren.

Format pro Rechner:
- Fliesen: `F0` + " Stk."
- Tapete: `F0` + " Rollen"
- Farbe: `F1` + " L"
- Laminat: `F0` + " Stk."
- Beton: `F0` + " Säcke"
- etc.

**Step 2: Build + Commit**

---

### Task 1.4: Live-Berechnung mit 300ms Debounce

**Files:**
- Modify: Alle 11 Calculator-ViewModels

**Step 1: Debounce-Pattern in jedem ViewModel implementieren**

Analog zum FinanzRechner-Pattern (`ScheduleAutoCalculate()` mit `Timer`). In jedem Calculator-ViewModel:

```csharp
private Timer? _debounceTimer;

// Auf JEDE Input-Property:
partial void OnRoomLengthChanged(double value) => ScheduleAutoCalculate();
partial void OnRoomWidthChanged(double value) => ScheduleAutoCalculate();
partial void OnTileLengthChanged(int value) => ScheduleAutoCalculate();
partial void OnTileWidthChanged(int value) => ScheduleAutoCalculate();
partial void OnWastePercentageChanged(double value) => ScheduleAutoCalculate();

private void ScheduleAutoCalculate()
{
    _debounceTimer?.Dispose();
    _debounceTimer = new Timer(_ =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => CalculateCommand.Execute(null)),
        null, 300, Timeout.Infinite);
}
```

In `Cleanup()` bzw. einem neuen `IDisposable`-Pattern:
```csharp
public void Cleanup()
{
    _debounceTimer?.Dispose();
    _debounceTimer = null;
    // ... bestehende Cleanup-Logik
}
```

In `Reset()`:
```csharp
[RelayCommand]
private void Reset()
{
    _debounceTimer?.Dispose();
    _debounceTimer = null;
    // ... bestehende Reset-Logik
}
```

**Step 2: Für alle 11 VMs anwenden**

Jedes VM hat unterschiedliche Input-Properties. Für jedes `[ObservableProperty]` das ein Input ist, den `partial void OnXxxChanged()` → `ScheduleAutoCalculate()` hinzufügen.

**Step 3: Build + Commit**

---

### Task 1.5: SkiaSharp-Visualisierungen animiert

**Files:**
- Modify: Alle 13 Visualization-Dateien in `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/Graphics/`
- Modify: Alle 11 Calculator-View Code-Behinds

**Step 1: Animation-Instanz pro Visualization**

Jede statische Visualization-Klasse bekommt eine statische `AnimatedVisualizationBase`-Instanz:

```csharp
using MeineApps.UI.SkiaSharp;

public static class TileVisualization
{
    private static readonly AnimatedVisualizationBase _animation = new()
    {
        AnimationDurationMs = 500f,
        EasingFunction = EasingFunctions.EaseOutCubic
    };

    // Neue Methode: Animation starten (vom Code-Behind aufgerufen)
    public static void StartAnimation() => _animation.StartAnimation();
    public static bool NeedsRedraw => _animation.IsAnimating;

    public static void Render(SKCanvas canvas, SKRect bounds,
        float roomLengthM, float roomWidthM, float tileLengthCm, float tileWidthCm,
        float wastePercent, bool hasResult)
    {
        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

        // Bestehende Render-Logik, aber mit progress multipliziert:
        // z.B. Fliesen-Reihen nur bis progress * totalRows zeichnen
        // z.B. Alpha = (byte)(255 * progress) für Fade-In
        // z.B. Scale-Transform: canvas.Scale(progress, progress, cx, cy)
        // ...
    }
}
```

**Step 2: Code-Behind Animation-Loop**

Im View Code-Behind nach Berechnung:

```csharp
private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
{
    var canvas = e.Surface.Canvas;
    canvas.Clear();
    var bounds = canvas.LocalClipBounds;

    if (_vm?.HasResult == true)
    {
        TileVisualization.Render(canvas, bounds, _vm.RoomLength, ...);

        // Animation-Loop: weitere Frames anfordern
        if (TileVisualization.NeedsRedraw)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                (sender as SKCanvasView)?.InvalidateSurface(),
                Avalonia.Threading.DispatcherPriority.Render);
        }
    }
}
```

Im ViewModel oder Code-Behind bei Berechnung:
```csharp
// Nach Calculate(): Animation starten
TileVisualization.StartAnimation();
TileCanvas.InvalidateSurface();
```

**Step 3: Animationstyp pro Renderer**

| Renderer | Animations-Effekt |
|----------|-------------------|
| TileVisualization | Fliesen legen sich Reihe für Reihe (rows * progress) |
| WallpaperVisualization | Bahnen rollen von oben nach unten |
| PaintVisualization | Farbe füllt sich von unten (Höhe * progress) |
| FlooringVisualization | Planken schieben von links ein |
| ConcreteVisualization | Füllstand steigt |
| DrywallVisualization | Platten erscheinen sequentiell |
| ElectricalVisualization | Leitungen zeichnen sich progressiv |
| MetalVisualization | Querschnitt baut sich auf |
| GardenVisualization | Pflanzen wachsen hoch |
| RoofSolarVisualization | Module erscheinen + Sonnenstrahlen |
| StairsVisualization | Stufen bauen sich von unten auf |
| CostBreakdownVisualization | Balken wachsen (height * progress) |
| MaterialStackVisualization | Stapel wachsen |

**Step 4: Build + Commit**

---

### Task 1.6: History-Tab als 4. Tab

**Files:**
- Create: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/ViewModels/HistoryViewModel.cs`
- Create: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/Views/HistoryView.axaml`
- Create: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/Views/HistoryView.axaml.cs`
- Modify: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/ViewModels/MainViewModel.cs`
- Modify: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/Views/MainView.axaml`
- Modify: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/App.axaml.cs`
- Modify: RESX-Dateien (6 Sprachen)

**Step 1: HistoryViewModel erstellen**

```csharp
public partial class HistoryViewModel : ObservableObject
{
    private readonly ICalculationHistoryService _historyService;
    private readonly ILocalizationService _localization;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IPurchaseService _purchaseService;

    public event Action<string>? NavigationRequested; // Route zur Wiederherstellung

    [ObservableProperty] private ObservableCollection<CalculationHistoryGroup> _groups = new();
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _isExtendedHistory; // Ad-Feature

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        var allHistory = await _historyService.GetHistoryAsync();
        // Gruppieren nach Calculator-Typ, max 5 (Free) oder 30 (Extended)
        var maxItems = _isExtendedHistory ? 30 : 5;
        // ... Gruppierung + UI-Update
    }

    [RelayCommand]
    private async Task WatchAdForExtendedHistory()
    {
        // Rewarded Ad → isExtendedHistory = true
    }

    [RelayCommand]
    private void OpenCalculation(CalculationHistoryItem item)
    {
        // Route konstruieren: "TileCalculatorPage?historyId=xyz"
        NavigationRequested?.Invoke($"{item.CalculatorType}Page");
    }
}
```

**Step 2: HistoryView.axaml erstellen**

ScrollViewer mit gruppierten Einträgen (Rechner-Icon + Typ + Datum + Hauptergebnis). EmptyStateView wenn leer.

**Step 3: MainViewModel erweitern (4. Tab)**

```csharp
// Tab-Routing: 0=Home, 1=Projects, 2=History, 3=Settings
public bool IsHistoryTab => SelectedTab == 2;
public bool IsSettingsTab => SelectedTab == 3; // war 2

[RelayCommand] private void SelectHistoryTab()
{
    CurrentPage = null; SelectedTab = 2;
    HistoryViewModel.LoadHistoryCommand.Execute(null);
}
[RelayCommand] private void SelectSettingsTab() { CurrentPage = null; SelectedTab = 3; }
```

**Step 4: MainView.axaml Tab-Leiste anpassen (4 Tabs)**

History-Icon: `ClockOutline` oder `History`.

**Step 5: DI-Registrierung + RESX-Keys (6 Sprachen)**

Keys: `TabHistory`, `HistoryEmpty`, `HistoryExtendedHint`, `WatchAdForHistory`

**Step 6: Build + Commit**

---

### Task 1.7: Wandflächen-Abzüge bei Farbe + Tapete

**Files:**
- Modify: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/ViewModels/Floor/PaintCalculatorViewModel.cs`
- Modify: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/ViewModels/Floor/WallpaperCalculatorViewModel.cs`
- Modify: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/Views/Floor/PaintCalculatorView.axaml`
- Modify: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/Views/Floor/WallpaperCalculatorView.axaml`
- Modify: RESX-Dateien

**Step 1: Properties im ViewModel hinzufügen**

```csharp
// Abzüge
[ObservableProperty] private bool _showDeductions;
[ObservableProperty] private int _doorCount;
[ObservableProperty] private double _doorWidth = 0.8;
[ObservableProperty] private double _doorHeight = 2.0;
[ObservableProperty] private int _windowCount;
[ObservableProperty] private double _windowWidth = 1.2;
[ObservableProperty] private double _windowHeight = 1.0;

private double CalculateDeductionArea()
{
    return (_doorCount * _doorWidth * _doorHeight) +
           (_windowCount * _windowWidth * _windowHeight);
}
```

In `Calculate()` die Abzugsfläche von der Wandfläche subtrahieren.

**Step 2: View-Expander für Abzüge**

```xml
<Expander Header="{loc:Translate DeductionsOptional}" IsExpanded="{Binding ShowDeductions}">
  <StackPanel Spacing="8">
    <!-- Türen: Anzahl + Maße -->
    <!-- Fenster: Anzahl + Maße -->
  </StackPanel>
</Expander>
```

**Step 3: RESX-Keys (6 Sprachen)**

Keys: `DeductionsOptional`, `DoorCount`, `DoorWidth`, `DoorHeight`, `WindowCount`, `WindowWidth`, `WindowHeight`, `DeductedArea`

**Step 4: Build + Commit**

---

### Task 1.8: Materialkosten bei allen Rechnern

**Files:**
- Modify: Alle 11 Calculator-ViewModels + Views

**Step 1: Optionales Preis-Feld + Kosten-Ergebnis**

In jedem ViewModel:
```csharp
[ObservableProperty] private double _pricePerUnit;
[ObservableProperty] private bool _showCost; // true wenn pricePerUnit > 0
[ObservableProperty] private string _estimatedCostDisplay = "";

partial void OnPricePerUnitChanged(double value)
{
    ShowCost = value > 0;
    if (HasResult) CalculateCommand.Execute(null);
}
```

In der Calculate-Methode: `EstimatedCostDisplay = $"{totalUnits * PricePerUnit:F2} €";`

In jeder View unter den Inputs ein optionales Preis-Feld:
```xml
<Grid ColumnDefinitions="*,Auto" Margin="0,8,0,0">
  <TextBox Watermark="{loc:Translate PricePerUnit}" Text="{Binding PricePerUnit}" />
  <TextBlock Grid.Column="1" Text="€" VerticalAlignment="Center" Margin="8,0,0,0" />
</Grid>
```

Im Result-Bereich:
```xml
<Grid ColumnDefinitions="*,Auto" IsVisible="{Binding ShowCost}">
  <TextBlock Text="{loc:Translate EstimatedCost}" />
  <TextBlock Grid.Column="1" Text="{Binding EstimatedCostDisplay}" FontWeight="Bold"
             Foreground="{DynamicResource AccentBrush}" />
</Grid>
```

**Step 2: RESX-Keys**

Keys: `PricePerUnit`, `EstimatedCost` (in allen 6 Sprachen)

**Step 3: Build + Commit**

---

### Task 1.9: Fugenmasse-Output im Fliesen-Rechner

**Files:**
- Modify: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/ViewModels/Floor/TileCalculatorViewModel.cs`
- Modify: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/Views/Floor/TileCalculatorView.axaml`
- Modify: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/Models/CraftEngine.cs` (optional)

**Step 1: Fugenbreite-Input + Fugenmasse-Output**

```csharp
[ObservableProperty] private double _groutWidthMm = 3.0; // Default 3mm
[ObservableProperty] private string _groutMassDisplay = "";

// In Calculate():
// Formel: Fugenmasse (kg) = Fläche(m²) × Fugenbreite(mm) × Fugentiefe(mm) × Dichte(1.6) / 1000
// Vereinfacht: kg ≈ Fläche × ((TileL+TileW) / (TileL×TileW)) × Fugenbreite × Fugentiefe × 1.6
var groutKg = CalculateGroutMass(result.RoomArea, TileLength/100.0, TileWidth/100.0, GroutWidthMm);
GroutMassDisplay = $"{groutKg:F1} kg";
```

**Step 2: View-Anpassung**

Fugenbreite-Slider (1-10mm) im Input-Bereich. Fugenmasse-Anzeige im Result-Bereich.

**Step 3: RESX-Keys + Build + Commit**

---

### Task 1.10-1.14: Fünf neue Rechner

Jeder neue Rechner folgt exakt dem TileCalculator-Pattern. Für jeden Rechner:

1. **CraftEngine.cs erweitern** (neue Calculate-Methode + Result-Record)
2. **ViewModel erstellen** (Transient, Input-Properties, Calculate, Reset, Debounce, SaveToHistory)
3. **View.axaml erstellen** (Header, ScrollViewer, ActionBar, SKCanvasView, Result-Card)
4. **Visualization.cs erstellen** (static class, static SKPaint, Render + AnimatedVisualizationBase)
5. **MainViewModel** (neue Route in CreateCalculatorVm switch, neue NavigateToXxxCommand, Home-Card Property)
6. **MainView.axaml** (neue Calculator-Card im Home-Grid, DataTemplate)
7. **App.axaml.cs** (DI-Registrierung als Transient)
8. **RESX-Keys** (6 Sprachen, ~20 Keys pro Rechner)

#### Task 1.10: Putz-Rechner

**Neue Dateien:**
- `ViewModels/Premium/PlasterCalculatorViewModel.cs`
- `Views/Premium/PlasterCalculatorView.axaml` + `.cs`
- `Graphics/PlasterVisualization.cs`

**CraftEngine-Erweiterung:**
```csharp
public record PlasterResult(double Area, double ThicknessMm, string PlasterType,
    double PlasterKg, int BagsNeeded, double EstimatedCost);

public PlasterResult CalculatePlaster(double areaSqm, double thicknessMm, string plasterType)
{
    // Dichte: Innenputz ~1.0 kg/m²/mm, Außenputz ~1.2, Kalkputz ~0.9, Gipsputz ~0.8
    var density = plasterType switch { "Außen" => 1.2, "Kalk" => 0.9, "Gips" => 0.8, _ => 1.0 };
    var totalKg = areaSqm * thicknessMm * density;
    var bags = (int)Math.Ceiling(totalKg / 30.0); // 30kg Säcke
    return new PlasterResult(areaSqm, thicknessMm, plasterType, totalKg, bags, 0);
}
```

**Route:** `"PlasterPage"` → `PlasterCalculatorViewModel`
**Icon:** `FormatPaint` (orange Gradient)
**Visualization:** Wandquerschnitt mit sichtbarer Putzschicht (Dicke proportional, progress = Schicht wächst)

#### Task 1.11: Estrich-Rechner

Analog. `EstrichResult`, `CalculateEstrich()`. Inputs: Fläche, Dicke(cm), Typ(Zement/Fließ/Anhydrit). Outputs: m³, kg, Säcke, Trocknungszeit.

#### Task 1.12: Dämmung-Rechner

`InsulationResult`, `CalculateInsulation()`. Inputs: Fläche, Ist-U-Wert, Soll-U-Wert, Material(EPS/XPS/Mineralwolle/Holzfaser). Outputs: Dämmdicke(cm), m², Kosten.

#### Task 1.13: Leitungsquerschnitt-Rechner

`CableSizingResult`, `CalculateCableSize()`. Inputs: Strom(A), Länge(m), Spannung(230/400V), Material(Cu/Al). Outputs: Mindest-Querschnitt(mm²), Spannungsabfall(%).

#### Task 1.14: Fugenmasse-Rechner (eigenständig)

`GroutResult`, `CalculateGrout()`. Inputs: Fläche, Fliesenmaße, Fugenbreite, Fugentiefe. Outputs: kg Fugenmasse, Eimer, Kosten.

**Für alle 5 neuen Rechner: Pro Rechner ~100 RESX-Einträge (6 Sprachen × ~17 Keys)**

**Build + Commit nach jedem Rechner einzeln!**

---

## Phase 2: FinanzRechner

### Task 2.1: Emoji-Icons in BudgetsView ersetzen

**Files:**
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/BudgetsView.axaml`
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Helpers/CategoryLocalizationHelper.cs`
- Modify: Model/ViewModel das `CategoryIcon` exponiert

**Step 1: CategoryLocalizationHelper auf MaterialIconKind umstellen**

```csharp
using Material.Icons;

public static MaterialIconKind GetCategoryMaterialIcon(ExpenseCategory category) => category switch
{
    ExpenseCategory.Food => MaterialIconKind.Cart,
    ExpenseCategory.Transport => MaterialIconKind.Car,
    ExpenseCategory.Housing => MaterialIconKind.Home,
    ExpenseCategory.Entertainment => MaterialIconKind.Movie,
    ExpenseCategory.Shopping => MaterialIconKind.Shopping,
    ExpenseCategory.Health => MaterialIconKind.Heart,
    ExpenseCategory.Education => MaterialIconKind.School,
    ExpenseCategory.Bills => MaterialIconKind.FileDocument,
    ExpenseCategory.Salary => MaterialIconKind.Briefcase,
    ExpenseCategory.Freelance => MaterialIconKind.Laptop,
    ExpenseCategory.Investment => MaterialIconKind.TrendingUp,
    ExpenseCategory.Gift => MaterialIconKind.Gift,
    ExpenseCategory.OtherIncome => MaterialIconKind.CashPlus,
    _ => MaterialIconKind.DotsHorizontal
};
```

**Step 2: BudgetsView.axaml TextBlock→MaterialIcon**

Von:
```xml
<TextBlock Text="{Binding CategoryIcon}" FontSize="24" />
```

Zu:
```xml
<mi:MaterialIcon Kind="{Binding CategoryMaterialIcon}" Width="24" Height="24"
                 Foreground="{DynamicResource PrimaryBrush}" />
```

Dafür braucht das BudgetDisplayItem/Model eine `CategoryMaterialIcon` Property (MaterialIconKind).

**Step 3: Build + Commit**

---

### Task 2.2: CountUp-Animation auf Rechner-Ergebnissen

**Files:**
- Modify: Alle 6 Calculator-Views in `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/Calculators/`

**Step 1: CountUpBehavior auf primäres Ergebnis**

Analog zu HandwerkerRechner Task 1.3. In CompoundInterestView z.B.:

```xml
<TextBlock FontSize="28" FontWeight="Bold" Foreground="White">
  <i:Interaction.Behaviors>
    <behaviors:CountUpBehavior TargetValue="{Binding Result.FinalAmount}"
                               Format="F2" Suffix=" €" Duration="500" />
  </i:Interaction.Behaviors>
</TextBlock>
```

Für alle 6 Rechner (CompoundInterest, SavingsPlan, Loan, Amortization, Yield, Inflation).

**Step 2: Build + Commit**

---

### Task 2.3: Chart-Einschwing-Animationen

**Files:**
- Modify: Alle Visualization-Klassen in `src/Apps/FinanzRechner/FinanzRechner.Shared/Graphics/`

**Step 1: AnimatedVisualizationBase integrieren**

Analog zu HandwerkerRechner Task 1.5. Für jede Visualization-Klasse:

| Visualization | Animation |
|---------------|-----------|
| StackedAreaVisualization | Y-Werte skalieren mit progress (0→Endwert) |
| AmortizationBarVisualization | Balken wachsen von unten (height * progress) |
| SparklineVisualization | Linie zeichnet sich von links nach rechts |
| BudgetMiniRingVisualization | Sweep wächst von 0 zum Endwert |
| TrendLineVisualization | Linie zeichnet sich progressiv |

**Step 2: Code-Behind Animation-Loop (wie Task 1.5)**

**Step 3: Build + Commit**

---

### Task 2.4: Budget-Tachometer Nadel animiert

**Files:**
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Graphics/BudgetGaugeVisualization.cs`

**Step 1: AnimatedVisualizationBase mit EaseOutBack**

```csharp
private static readonly AnimatedVisualizationBase _animation = new()
{
    AnimationDurationMs = 800f,
    EasingFunction = EasingFunctions.EaseOutBack
};

public static void StartAnimation() => _animation.StartAnimation();
public static bool NeedsRedraw => _animation.IsAnimating;

public static void Render(SKCanvas canvas, SKRect bounds,
    double percentage, string spentDisplay, string limitDisplay, bool isOverLimit)
{
    _animation.UpdateAnimation();
    float progress = _animation.AnimationProgress;

    // Nadel-Winkel: statt direkt `percentage`, jetzt `percentage * progress`
    float animatedPercentage = (float)(percentage * progress);
    // ... Rest der Render-Logik mit animatedPercentage
}
```

**Step 2: Build + Commit**

---

### Task 2.5-2.10: Sechs neue Rechner

Jeder neue Rechner folgt dem FinanzRechner-Pattern (Singleton-VM, FinanceEngine, Debounce bereits vorhanden, SkiaSharp-Chart).

Für jeden Rechner:
1. **FinanceEngine erweitern** (neue Calculate-Methode + Result-Record/Klasse)
2. **ViewModel erstellen** (Singleton, FinanceEngine + ILocalizationService, Debounce, IDisposable)
3. **View.axaml erstellen** (farbiger Hero-Box, Chart-SKCanvasView, Sekundärwerte)
4. **Visualization.cs erstellen** (Chart-Renderer, AnimatedVisualizationBase)
5. **MainViewModel** (neue OpenXxxCommand, IsXxxActive, Index-Zuweisung)
6. **MainView.axaml** (Calculator-Overlay IsVisible-Binding, DataTemplate)
7. **HomeView.axaml** (neue Karte im Calculator-Grid, eigene Farbgebung + Icon)
8. **App.axaml.cs** (DI als Singleton)
9. **RESX** (6 Sprachen, ~20 Keys)

#### Task 2.5: Netto-Brutto-Rechner

**Route-Index:** 6
**Icon:** `AccountCash` (Grün #10B981)
**Chart:** Donut (Netto vs. Abzüge: Lohnsteuer, Soli, KV, RV, AV, PV)
**Inputs:** Brutto, Steuerklasse(1-6), Kirchensteuer(bool), KV-Zusatzbeitrag(%)
**Outputs:** Netto, Lohnsteuer, Soli, Sozialabgaben einzeln

#### Task 2.6: Mehrwertsteuer-Rechner

**Icon:** `PercentOutline` (Amber #F59E0B)
**Inputs:** Betrag, Richtung(Netto→Brutto/Brutto→Netto), Satz(19%/7%/Custom)
**Outputs:** Netto, MwSt, Brutto
**Chart:** Einfache Balken (Netto + MwSt = Brutto)

#### Task 2.7: Mietrendite-Rechner

**Icon:** `HomeCity` (Blau #3B82F6)
**Inputs:** Kaufpreis, Nebenkosten(%), Monatskaltmiete, Hausgeld, Instandhaltung
**Outputs:** Brutto-/Netto-Rendite(%), Cashflow/Monat
**Chart:** Donut (Einnahmen vs. Kosten)

#### Task 2.8: Kreditvergleich-Rechner

**Icon:** `ScaleBalance` (Violett #8B5CF6)
**Inputs:** 2× (Kreditsumme, Zinssatz, Laufzeit, Sondertilgung)
**Outputs:** Vergleichstabelle
**Chart:** StackedArea (beide Kredite)

#### Task 2.9: Break-Even-Rechner

**Icon:** `ChartTimelineVariant` (Rot #EF4444)
**Inputs:** Investition, monatliche Ersparnis, laufende Kosten
**Outputs:** Amortisationszeit, Gesamt-ROI
**Chart:** Linienchart (Schnittpunkt markiert)

#### Task 2.10: Altersvorsorge-Rechner

**Icon:** `AccountClock` (Cyan #06B6D4)
**Inputs:** Alter, Renteneintritt, Brutto, Sparrate, Rendite
**Outputs:** Rentenlücke, Kapital bei Rente, empfohlene Sparrate
**Chart:** StackedArea (Rente + Vorsorge vs. Bedarf)

**Pro Rechner: ~120 RESX-Einträge (6 Sprachen × ~20 Keys)**

---

### Task 2.11: Sparziele-Feature

**Files:**
- Create: `src/Apps/FinanzRechner/FinanzRechner.Shared/Models/SavingsGoal.cs`
- Create: `src/Apps/FinanzRechner/FinanzRechner.Shared/Services/SavingsGoalService.cs`
- Create: `src/Apps/FinanzRechner/FinanzRechner.Shared/ViewModels/SavingsGoalViewModel.cs`
- Create: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/SavingsGoalView.axaml` + `.cs`
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/ViewModels/MainViewModel.cs`
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/MainView.axaml`
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/Views/HomeView.axaml` (Widget)
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/App.axaml.cs`

**Step 1: Model**

```csharp
public class SavingsGoal
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateTime? Deadline { get; set; }
    public decimal MonthlyContribution { get; set; }
    public string IconKind { get; set; } = "Target";
    public string Color { get; set; } = "#6366F1";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**Step 2: Service (JSON-basiert wie CalculationHistoryService)**

CRUD-Operationen, Fortschrittsberechnung, Prognose.

**Step 3: ViewModel**

ObservableCollection<SavingsGoal>, AddGoal, EditGoal, DeleteGoal, AddDeposit, Projection-Berechnung.

**Step 4: View**

- Ziel-Liste mit SkiaGradientRing pro Ziel
- Add/Edit-Dialog
- Prognose-Text ("Bei X €/Monat erreichst du dein Ziel am [Datum]")

**Step 5: Home-Widget**

Top-3 Sparziele als Mini-Ringe auf dem Home-Tab.

**Step 6: Navigation + DI**

Neuer Tab oder Sub-Page unter Tracker-Tab.

**Step 7: RESX (~30 Keys × 6 Sprachen)**

**Step 8: Build + Commit**

---

## Phase 3: FitnessRechner

### Task 3.1: Kalorienfarbe in FoodLog korrigieren

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/FoodSearchView.axaml`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/ProgressView.axaml`

**Step 1: ErrorBrush → TextSecondaryBrush**

In `FoodSearchView.axaml` (Zeile ~516-519):

Von:
```xml
<TextBlock Text="{Binding Calories, StringFormat='{}{0:F0} kcal'}"
           FontSize="16" FontWeight="Bold"
           Foreground="{DynamicResource ErrorBrush}" />
```

Zu:
```xml
<TextBlock Text="{Binding Calories, StringFormat='{}{0:F0} kcal'}"
           FontSize="16" FontWeight="Bold"
           Foreground="{DynamicResource TextSecondaryBrush}" />
```

Dasselbe in `ProgressView.axaml` im MealItemTemplate (Zeile ~17-40).

**Step 2: Build + Commit**

---

### Task 3.2: BMI-Gauge Zeiger-Animation

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/BmiGaugeRenderer.cs`

**Step 1: AnimatedVisualizationBase integrieren**

```csharp
private static readonly AnimatedVisualizationBase _animation = new()
{
    AnimationDurationMs = 600f,
    EasingFunction = EasingFunctions.EaseOutBack // Leichtes Überschwingen
};

public static void StartAnimation() => _animation.StartAnimation();
public static bool NeedsRedraw => _animation.IsAnimating;

public static void Render(SKCanvas canvas, SKRect bounds, float bmiValue, bool hasResult)
{
    _animation.UpdateAnimation();
    float progress = _animation.AnimationProgress;

    // Zeiger-Winkel mit Animation: animierter BMI von 0 bis bmiValue
    float animatedBmi = bmiValue * progress;
    float clampedBmi = Math.Clamp(animatedBmi, minBmi, maxBmi);
    // ... Rest wie bisher
}
```

**Step 2: Code-Behind Animation-Trigger**

Im BmiView Code-Behind bei Berechnung `BmiGaugeRenderer.StartAnimation()` + InvalidateSurface + Loop.

**Step 3: Build + Commit**

---

### Task 3.3: Kalorien-Ring Auffüll-Animation

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/CalorieRingRenderer.cs`

**Step 1: Animation analog zu BMI-Gauge**

```csharp
private static readonly AnimatedVisualizationBase _animation = new()
{
    AnimationDurationMs = 800f,
    EasingFunction = EasingFunctions.EaseOutCubic
};

// In DrawRing(): sweepAngle *= progress
```

**Step 2: Build + Commit**

---

### Task 3.4: Wasser-Glas Wellen animieren

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/HomeView.axaml`

**Step 1: SkiaWaterGlass WaveEnabled aktivieren**

Prüfen ob bereits `SkiaWaterGlass` aus MeineApps.UI verwendet wird. Falls ja:
```xml
<controls:SkiaWaterGlass WaveEnabled="True" FillPercent="{Binding WaterProgress}" />
```

Falls eigene Implementierung: `WaveEnabled="True"` setzen.

**Step 2: Build + Commit**

---

### Task 3.5: Food-Search Stagger-Fade

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/FoodSearchView.axaml`

**Step 1: StaggerFadeInBehavior auf Suchergebnis-Items**

Im DataTemplate der Suchergebnisse:
```xml
<Border Classes="Card Compact" Margin="0,4" Padding="12">
  <i:Interaction.Behaviors>
    <behaviors:StaggerFadeInBehavior StaggerDelay="40" BaseDuration="200" />
  </i:Interaction.Behaviors>
  <!-- Inhalt -->
</Border>
```

**Step 2: Build + Commit**

---

### Task 3.6: Wasser-Portionsoptionen

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/HomeView.axaml`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/ViewModels/MainViewModel.cs`
- Modify: RESX (6 Sprachen)

**Step 1: 4 Buttons statt einem**

```xml
<Grid ColumnDefinitions="*,*,*,*" ColumnSpacing="8">
  <!-- 200ml Glas -->
  <Button Grid.Column="0" Command="{Binding QuickAddWaterCommand}" CommandParameter="200"
          CornerRadius="12" Padding="8">
    <StackPanel Spacing="2" HorizontalAlignment="Center">
      <mi:MaterialIcon Kind="GlassWater" Width="20" Height="20" HorizontalAlignment="Center" />
      <TextBlock Text="200ml" FontSize="11" TextAlignment="Center" />
    </StackPanel>
  </Button>
  <!-- 330ml Flasche -->
  <Button Grid.Column="1" CommandParameter="330">
    <mi:MaterialIcon Kind="BottleSoda" />
    <TextBlock Text="330ml" />
  </Button>
  <!-- 500ml -->
  <Button Grid.Column="2" CommandParameter="500">
    <mi:MaterialIcon Kind="BottleWine" />
    <TextBlock Text="500ml" />
  </Button>
  <!-- Custom -->
  <Button Grid.Column="3" Command="{Binding ShowCustomWaterInputCommand}">
    <mi:MaterialIcon Kind="PencilOutline" />
    <TextBlock Text="{loc:Translate Custom}" />
  </Button>
</Grid>
```

**Step 2: Custom-Eingabe (kleiner Dialog/Overlay)**

ShowCustomWaterInput → bool-Property → kleines NumericUpDown + OK-Button.

**Step 3: RESX-Keys + Build + Commit**

---

### Task 3.7: Mahlzeit-Templates

**Files:**
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Models/MealTemplate.cs`
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Services/MealTemplateService.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/ViewModels/FoodSearchViewModel.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/FoodSearchView.axaml`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/App.axaml.cs`
- Modify: RESX

**Step 1: Model**

```csharp
public class MealTemplate
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string MealType { get; set; } = "Breakfast"; // Breakfast/Lunch/Dinner/Snack
    public string ItemsJson { get; set; } = "[]"; // JSON-serialisiert
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class MealTemplateItem
{
    public string FoodName { get; set; } = "";
    public double Calories { get; set; }
    public double Protein { get; set; }
    public double Carbs { get; set; }
    public double Fat { get; set; }
    public double Grams { get; set; }
}
```

**Step 2: Service (sqlite-net)**

CRUD, GetByMealType, SaveCurrentMealAsTemplate.

**Step 3: FoodSearchViewModel erweitern**

"Meine Mahlzeiten" Tab/Chips-Auswahl, Template-Liste, Template anwenden.

**Step 4: RESX (~15 Keys × 6 Sprachen) + Build + Commit**

---

### Task 3.8: Körpermaße-Tracking

**Files:**
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Models/BodyMeasurement.cs`
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Services/BodyMeasurementService.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/ViewModels/ProgressViewModel.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/ProgressView.axaml`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/App.axaml.cs`
- Modify: RESX

**Step 1: Model**

```csharp
public class BodyMeasurement
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public double? Waist { get; set; }   // cm
    public double? Hip { get; set; }
    public double? Chest { get; set; }
    public double? Biceps { get; set; }
    public double? Thigh { get; set; }
}
```

**Step 2: Service (sqlite-net)**

CRUD, GetLatest, GetHistory(dayCount).

**Step 3: 5. Sub-Tab "Maße" in ProgressView**

Neues `IsMeasurementsTab` + Tab-Button + StackPanel. Eingabe-Form + Trend-Chart (SkiaSharp).

**Step 4: Taillien-Hüft-Verhältnis berechnen**

WHR = Waist / Hip, mit Bewertung (< 0.85 gut für Frauen, < 0.90 gut für Männer).

**Step 5: RESX (~20 Keys × 6 Sprachen) + Build + Commit**

---

### Task 3.9: Gewichts-Projektion

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/ViewModels/ProgressViewModel.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/ProgressView.axaml`
- Modify: Gewichts-Chart-Renderer (ggf. `Graphics/HealthTrendVisualization.cs`)
- Modify: RESX

**Step 1: Lineare Regression berechnen**

```csharp
private void CalculateWeightProjection()
{
    if (WeightEntries.Count < 3) { ShowProjection = false; return; }

    var recent = WeightEntries.TakeLast(14).ToList();
    // Lineare Regression: y = mx + b
    var n = recent.Count;
    var xs = recent.Select((e, i) => (double)i).ToArray();
    var ys = recent.Select(e => e.Weight).ToArray();
    var xMean = xs.Average(); var yMean = ys.Average();
    var m = xs.Zip(ys, (x, y) => (x - xMean) * (y - yMean)).Sum()
          / xs.Select(x => (x - xMean) * (x - xMean)).Sum();

    if (Math.Abs(m) < 0.001) { ProjectionText = "Gewicht stabil"; return; }

    var daysToGoal = (WeightGoal - ys.Last()) / m;
    if (daysToGoal > 0 && daysToGoal < 365)
    {
        var targetDate = DateTime.Today.AddDays(daysToGoal);
        ProjectionText = $"Prognose: {targetDate:dd.MM.yyyy}";
    }
}
```

**Step 2: Strichlinie im Gewichts-Chart**

Im Chart-Renderer eine gestrichelte Linie vom letzten Datenpunkt zum projizierten Zieldatum.

**Step 3: RESX + Build + Commit**

---

### Task 3.10: Level-Up Full-Screen-Overlay

**Files:**
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/LevelUpOverlayRenderer.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/MainView.axaml`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/MainView.axaml.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/ViewModels/MainViewModel.cs`

**Step 1: LevelUpOverlayRenderer (adaptiert von HandwerkerImperium RewardCeremonyRenderer)**

```csharp
public static class LevelUpOverlayRenderer
{
    private static readonly AnimatedVisualizationBase _animation = new()
    {
        AnimationDurationMs = 3000f,
        EasingFunction = EasingFunctions.EaseOutCubic
    };

    private static readonly SkiaParticleManager _confetti = new(80);

    public static void StartCeremony(int level)
    {
        _currentLevel = level;
        _animation.StartAnimation();
        // Confetti-Burst starten
        _confetti.AddBurst(60, rng => SkiaParticlePresets.CreateConfetti(rng, 0, 0));
    }

    public static void Render(SKCanvas canvas, SKRect bounds)
    {
        var stillAnimating = _animation.UpdateAnimation();
        float p = _animation.AnimationProgress;

        // Dunkler Backdrop (Alpha bis 180)
        // Scale-In Kreis mit Level-Nummer (EaseOutBack)
        // "Level X!" Text + XP-Anzeige
        // Confetti-Partikel

        _confetti.Update(0.016f);
        _confetti.DrawAsConfetti(canvas);
    }
}
```

**Step 2: SKCanvasView Overlay in MainView.axaml**

```xml
<skia:SKCanvasView x:Name="LevelUpCanvas" Grid.RowSpan="99" ZIndex="100"
                    IsVisible="{Binding ShowLevelUpOverlay}"
                    IsHitTestVisible="True"
                    PaintSurface="OnLevelUpPaintSurface"
                    PointerPressed="OnLevelUpTapped" />
```

**Step 3: MainViewModel LevelUp-Event verdrahten**

```csharp
private void OnLevelUp(int newLevel)
{
    ShowLevelUpOverlay = true;
    // Overlay zeigen, nach 3s oder Tap ausblenden
}
```

**Step 4: Build + Commit**

---

### Task 3.11: Onboarding (3-4 Screens)

**Files:**
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/ViewModels/OnboardingViewModel.cs`
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/OnboardingView.axaml` + `.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/PreferenceKeys.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/ViewModels/MainViewModel.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/MainView.axaml`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/App.axaml.cs`
- Modify: RESX (6 Sprachen)

**Step 1: PreferenceKey**

```csharp
public const string OnboardingCompleted = "onboarding_completed";
```

**Step 2: OnboardingViewModel**

```csharp
public partial class OnboardingViewModel : ObservableObject
{
    [ObservableProperty] private int _currentStep; // 0-3
    [ObservableProperty] private double _height;   // cm
    [ObservableProperty] private double _weight;   // kg
    [ObservableProperty] private int _goalType;    // 0=Abnehmen, 1=Halten, 2=Zunehmen
    [ObservableProperty] private double _goalWeight;

    public event Action? OnboardingCompleted;

    [RelayCommand] private void Next() { if (CurrentStep < 3) CurrentStep++; else Finish(); }
    [RelayCommand] private void Skip() => Finish();

    private void Finish()
    {
        // Werte in Preferences speichern
        // BMI berechnen, Kalorien-Ziel setzen, Wasser-Ziel setzen
        _preferences.Set(PreferenceKeys.OnboardingCompleted, true);
        OnboardingCompleted?.Invoke();
    }
}
```

**Step 3: OnboardingView (4 Screens)**

Carousel oder Panel mit 4 IsVisible-gesteuerten StackPanels:
1. Willkommen + Größe
2. Gewicht
3. Ziel (3 Buttons: Abnehmen/Halten/Zunehmen) + Zielgewicht
4. Zusammenfassung (BMI, Kalorien-Ziel, Wasser-Ziel) + "Los geht's" Button

**Step 4: MainViewModel First-Start-Check**

```csharp
public async Task InitializeAsync()
{
    if (!_preferences.Get(PreferenceKeys.OnboardingCompleted, false))
    {
        ShowOnboarding = true;
        return;
    }
    // ... normale Initialisierung
}
```

**Step 5: RESX (~25 Keys × 6 Sprachen)**

Keys: `OnboardingWelcome`, `OnboardingHeight`, `OnboardingWeight`, `OnboardingGoal`, `GoalLoseWeight`, `GoalMaintain`, `GoalGainWeight`, `OnboardingReady`, `LetsGo`, etc.

**Step 6: Build + Commit**

---

## Abschluss-Tasks (nach jeder Phase)

### Nach jeder Phase:

1. **AppChecker laufen lassen:**
```bash
dotnet run --project tools/AppChecker HandwerkerRechner
dotnet run --project tools/AppChecker FinanzRechner
dotnet run --project tools/AppChecker FitnessRechner
```

2. **CLAUDE.md aktualisieren:**
- `src/Apps/HandwerkerRechner/CLAUDE.md`
- `src/Apps/FinanzRechner/CLAUDE.md`
- `src/Apps/FitnessRechner/CLAUDE.md`
- `src/UI/MeineApps.UI/CLAUDE.md`
- `F:\Meine_Apps_Ava\CLAUDE.md` (falls neue Troubleshooting-Einträge)

3. **Gesamte Solution bauen:**
```bash
dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln
```

---

## Zusammenfassung

| Phase | Tasks | Neue Dateien | RESX-Keys |
|-------|-------|-------------|-----------|
| Phase 0 | 2 | 2 | 0 |
| Phase 1 | 14 | ~25 (5 neue Rechner + History) | ~120 |
| Phase 2 | 11 | ~20 (6 neue Rechner + Sparziele) | ~150 |
| Phase 3 | 11 | ~12 (Templates, Maße, Onboarding, LevelUp) | ~80 |
| **Gesamt** | **38** | **~59** | **~350 × 6 = ~2100** |
