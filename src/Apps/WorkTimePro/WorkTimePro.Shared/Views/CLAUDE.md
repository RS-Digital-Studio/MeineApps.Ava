# Views — UI-Patterns & AXAML-Struktur

12 AXAML-Views + `MainWindow`. Alle Views nutzen `x:CompileBindings="True"` + `x:DataType`.
Generische MVVM/AXAML-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainView.axaml(.cs)` | Haupt-Container: Tab-Bar (5 Tabs), Content-Area (Today/Week/Calendar/Stats/Settings), Sub-Page-Overlays, Ad-Banner-Spacer. |
| `MainWindow.axaml(.cs)` | Desktop-Shell (`Window`). Enthält nur `MainView`. |
| `TodayView.axaml(.cs)` | Heute-Ansicht: Status-Ring (`CircularProgressControl`, `SkiaGradientRing`), CheckIn/Out-Button, LiveData, Timeline, Earnings-Ticker. |
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

Alle Views mit Overlays (z.B. Edit-Overlay, Bestätigungs-Dialog) nutzen:

```csharp
// Im ViewModel:
public bool IsAnyOverlayVisible => IsEditOverlayVisible || IsConfirmDeleteVisible;
partial void OnIsEditOverlayVisibleChanged(bool value) => OnPropertyChanged(nameof(IsAnyOverlayVisible));
```

```xml
<!-- Im AXAML: -->
<ScrollViewer IsHitTestVisible="{Binding !IsAnyOverlayVisible}">
```

Verhindert Touch-Durch-Fall bei ZIndex-Overlays (Android: ZIndex für Hit-Testing wirkungslos).
Betrifft: `DayDetailView`, `StatisticsView`, `VacationView`, `YearOverviewView`.

## Ad-Banner-Layout (MainView)

`RowDefinitions="*,Auto,Auto"` → Row 0 Content, Row 1 Ad-Spacer (56dp für WorkTimePro), Row 2 Tab-Bar.
`IsAdBannerVisible` Binding auf den Spacer — wird von `MainViewModel` gesteuert (`_adService.ShowBanner()`).

## Keyboard-Shortcuts (Desktop)

`MainView.axaml.cs` `OnKeyDown`:
- `F5` → Refresh
- `1`–`5` → Tab-Wechsel
- `Escape` → Sub-Page schließen
- `Ctrl+Z` → Undo

Shortcuts NICHT im ViewModel — reine UI-Tastatur-Navigation, akzeptables Code-Behind.

## Compiled Bindings

`x:CompileBindings="True"` + `x:DataType="vm:XxxViewModel"` auf jeder View-Root.
`VacationView`-ComboBox DataTemplate: `x:DataType="vm:VacationTypeItem"` (sonst Reflection).

## Tab-Bar Höhe

Tab-Bar-Höhe: **56dp** (kein SkiaSharp-Renderer, Ad-Banner-Spacer separat).
