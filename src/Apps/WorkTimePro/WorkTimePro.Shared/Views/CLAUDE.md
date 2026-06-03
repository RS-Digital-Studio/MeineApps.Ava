# Views — UI-Patterns & AXAML-Struktur

11 App-Views + `MainWindow` (12 AXAML-Dateien). Alle Views nutzen `x:CompileBindings="True"` + `x:DataType`.
Generische MVVM/AXAML-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainView.axaml(.cs)` | Haupt-Container: Tab-Bar (5 Tabs), Content-Area (Today/Week/Calendar/Stats/Settings), Sub-Page-Overlays, Ad-Banner-Spacer. |
| `MainWindow.axaml(.cs)` | Desktop-Shell (`Window`). Enthält nur `MainView`. |
| `TodayView.axaml(.cs)` | Heute-Ansicht: Status-Ring (`SkiaGradientRing`), CheckIn/Out-Button, LiveData, Timeline, Earnings-Ticker. |
| `WeekOverviewView.axaml(.cs)` | Wochenübersicht: `WeekBarVisualization` + `LinearProgressVisualization`. |
| `CalendarView.axaml(.cs)` | Monatskalender mit Heatmap + Overlay-Pattern. |
| `StatisticsView.axaml(.cs)` | Statistiken (5 Perioden), Rewarded-Ad-Gate für Quartal/Jahr. |
| `SettingsView.axaml(.cs)` | Einstellungen-Formular, kein Speichern-Button. |
| `DayDetailView.axaml(.cs)` | Tagesdetail: Buchungs-Liste, manuelle Einträge, Lock-Status. |
| `MonthOverviewView.axaml(.cs)` | Monatsübersicht: `MonthlyBarChartVisualization`. |
| `YearOverviewView.axaml(.cs)` | Jahresübersicht: Heatmap-Kalender, Navigation zu MonthOverview. |
| `VacationView.axaml(.cs)` | Urlaubs-Verwaltung: Quota-Gauge, Eintrag-Liste, Rewarded-Ad-Gate. |
| `ShiftPlanView.axaml(.cs)` | Schichtplanung: Muster + Einzelzuweisungen. |

## Overlay-Pattern (IsAnyOverlayVisible)

Views mit Overlays deaktivieren den `ScrollViewer` per Hit-Test, sobald ein Overlay aktiv ist.
Auf Android ist ZIndex für Hit-Testing wirkungslos — Touch fällt sonst durch das Overlay in
die darunterliegende ScrollView.

```csharp
// Im ViewModel (VM-spezifische Overlay-Properties werden verknüpft):
public bool IsAnyOverlayVisible => IsOverlayA || IsOverlayB;
partial void OnIsOverlayAChanged(bool value) => OnPropertyChanged(nameof(IsAnyOverlayVisible));
```

```xml
<!-- Im AXAML: -->
<ScrollViewer IsHitTestVisible="{Binding !IsAnyOverlayVisible}">
```

Betrifft: `DayDetailView`, `StatisticsView`, `VacationView`, `YearOverviewView`.

## Ad-Banner-Layout (MainView)

`RowDefinitions="*,Auto,Auto"` → Row 0 Content, Row 1 Ad-Spacer (**64dp** für WorkTimePro), Row 2 Tab-Bar.
`IsAdBannerVisible` Binding auf den Spacer — wird von `MainViewModel` gesteuert (`_adService.BannerVisible` Property).

## Keyboard-Shortcuts (Desktop)

`MainView.axaml.cs` `OnKeyDown`:
- `F5` → `LoadDataCommand`
- `1`–`5` → Tab-Wechsel (via `SelectXxxTabCommand`)
- `Escape` → Sub-Page schließen (`GoBackCommand`)
- `Ctrl+Z` → Undo (`UndoLastActionCommand`, nur wenn `IsUndoVisible`)

Shortcuts NICHT im ViewModel — reine UI-Tastatur-Navigation, akzeptables Code-Behind.

## DataTemplate x:DataType

`VacationView`- und `CalendarView`-ComboBox DataTemplates nutzen `x:DataType="vm:VacationTypeItem"` —
sonst Reflection statt Compiled Bindings.

## Tab-Bar Höhe

Tab-Bar-Höhe: **56dp** (kein SkiaSharp-Renderer, Ad-Banner-Spacer separat mit 64dp).
