# Views — AXAML-Views & UI-Patterns

Views für alle 4 Tabs + 19 Calculator-Overlays + Business-Views. Generische MVVM-Conventions
(Compiled Bindings, x:DataType, AutomationIds) → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainView.axaml(.cs)` | App-Shell: Blueprint-Hintergrund (RowSpan 3, IsHitTestVisible=false), 4-Tab-ContentControl, Calculator-Overlay (ContentControl mit ViewLocator). Background-Renderer-Loop (200ms, 5 FPS). |
| `MainWindow.axaml(.cs)` | Desktop-Fenster-Wrapper (minWidth 400, minHeight 700). |
| `CalculatorViewBase.cs` | Abstrakte Basisklasse für alle 19 Calculator-Views (DataContext-PropertyChanged-Pattern). |
| `HistoryView.axaml(.cs)` | Expander pro Rechner-Typ, SwipeToReveal-Löschen, Tap → Rechner öffnen. |
| `ProjectsView.axaml(.cs)` | CRUD-Liste, Foto-Thumbnails, Navigation zu Rechner mit Projekt-ID. |
| `QuoteView.axaml(.cs)` | Angebots-CRUD, Positions-Tabelle, MwSt/Marge-Felder, PDF-Export-Button. |
| `ProjectTemplatesView.axaml(.cs)` | 2 Sektionen (Eingebaut/Eigene), Anwenden-Dialog. |
| `SettingsView.axaml(.cs)` | Sprache, Region, Einheiten, Über-die-App, Feedback. |
| `Floor/TileCalculatorView.axaml(.cs)` | Fliesen-Inputs + `TileVisualization`-SKCanvasView. |
| `Floor/WallpaperCalculatorView.axaml(.cs)` | Tapeten-Inputs + `WallpaperVisualization`. |
| `Floor/PaintCalculatorView.axaml(.cs)` | Farb-Inputs + `PaintVisualization`. |
| `Floor/FlooringCalculatorView.axaml(.cs)` | Dielen-Inputs + `FlooringVisualization`. |
| `Floor/ConcreteCalculatorView.axaml(.cs)` | Beton-Inputs (3 Sub-Typen-Picker) + `ConcreteVisualization`. |
| `Premium/DrywallView.axaml(.cs)` | Trockenbau-Inputs + `DrywallVisualization`. |
| `Premium/ElectricalView.axaml(.cs)` | Elektriker 3 Sub-Typen (SegmentedControl) + `ElectricalVisualization`. |
| `Premium/MetalView.axaml(.cs)` | Metall-Profil/Material-Picker + `MetalVisualization`. |
| `Premium/GardenView.axaml(.cs)` | Garten 3 Sub-Typen + `GardenVisualization`. |
| `Premium/RoofSolarView.axaml(.cs)` | Dach/Solar 3 Sub-Typen + `RoofSolarVisualization`. |
| `Premium/StairsView.axaml(.cs)` | Treppen-Inputs + `StairsVisualization`. |
| `Premium/PlasterView.axaml(.cs)` | Putz-Inputs (PlasterType-Picker) + `PlasterVisualization`. |
| `Premium/ScreedView.axaml(.cs)` | Estrich-Inputs (ScreedType-Picker) + `ScreedVisualization`. |
| `Premium/InsulationView.axaml(.cs)` | Dämmung-Inputs (4 Materialtypen) + `InsulationVisualization`. |
| `Premium/CableSizingView.axaml(.cs)` | Kabel-Inputs (Kupfer/Alu, 1-/3-Phasen) + `CableSizingVisualization`. |
| `Premium/GroutView.axaml(.cs)` | Fugen-Inputs + `GroutVisualization`. |
| `Premium/HourlyRateView.axaml(.cs)` | Stundenrechner-Inputs + `HourlyRateVisualization` (statisch). |
| `Premium/MaterialCompareView.axaml(.cs)` | Material-Vergleich-Inputs + `MaterialCompareVisualization` (statisch). |
| `Premium/AreaMeasureView.axaml(.cs)` | Aufmaß-Inputs + `AreaMeasureVisualization` (statisch). |

## CalculatorViewBase — Pattern

Basisklasse für alle 19 Calculator-Views. Kapselt die `PropertyChanged`-Subscription auf
dem ViewModel mit sauberem Ab-/Anmeldung bei DataContext-Wechsel:

```csharp
// Abgeleitete View überschreibt:
protected override bool ShouldInvalidateOnPropertyChanged(string? propertyName)
    => propertyName?.Contains("Result") == true;  // Standard-Filter

protected override void OnResultPropertyChanged()
    => _visualization?.StartAnimation();  // oder InvalidateSurface()
```

Für statische Views (HourlyRate, MaterialCompare, AreaMeasure) — Filtert spezifische Properties
und ruft direkt `InvalidateSurface()` auf ohne Animation-Loop.

## Background-Render-Loop (MainView)

`BlueprintBackgroundRenderer` läuft als `DispatcherTimer` (200ms, ~5 FPS):
- Start: `OnDataContextChanged` in `MainView.axaml.cs`.
- Stop + Dispose: `OnDetachedFromVisualTree`.
- **Pausiert** wenn `CurrentPage != null` (Calculator-Overlay verdeckt Hintergrund → keine GPU-Arbeit).
- SKCanvasView: `Grid.RowSpan="3"` + `IsHitTestVisible="False"` hinter Content.

## ViewLocator-Routing

Kein `DataTemplate` in `MainView.axaml` — der globale ViewLocator (App.axaml) löst auf:

```
HandwerkerRechner.ViewModels.Floor.TileCalculatorViewModel → HandwerkerRechner.Views.Floor.TileCalculatorView
HandwerkerRechner.ViewModels.Premium.DrywallViewModel      → HandwerkerRechner.Views.Premium.DrywallView
```

Namens-Konvention: `{Namespace}.ViewModels.{Sub}.{Name}ViewModel` → `{Namespace}.Views.{Sub}.{Name}View`.

## Visualisierungs-Pattern (Calculator-Views)

```xml
<Border Classes="Card" Height="220" ClipToBounds="True"
        IsVisible="{Binding HasResult}">
    <avaloniaLabs:SKCanvasView PaintSurface="OnPaintVisualization" />
</Border>
```

Code-Behind mit Named-Handler-Pattern (explizites Unsubscribe bei DataContext-Wechsel via
`CalculatorViewBase`). 17 Views mit animiertem Renderer (erbt `AnimatedVisualizationBase`),
3 statisch (`HourlyRate`, `MaterialCompare`, `AreaMeasure`).
