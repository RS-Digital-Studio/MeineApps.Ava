# ViewModels — Zeiterfassung & Navigation

Alle ViewModels sind **Singleton** (in `App.axaml.cs` registriert) und werden vom `MainViewModel`
gehalten. Nur UI-Logik — Berechnungen delegieren an Services. Generische MVVM-Conventions →
[Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainViewModel.cs` | Today-Tab (CheckIn/Out, Live-Timer, Undo, Notiz-Debounce, Earnings-Ticker), Back-Press-Flow, Child-VM-Holding, Settings-Propagation. |
| `MainViewModel.Navigation.cs` | Tab-Switching (5 Tabs), Sub-Page-Flags (`IsDayDetailActive` etc.), `HandleNavigation` (Route-Parser), Typed Event-Wiring (kein Reflection). |
| `WeekOverviewViewModel.cs` | Wochenübersicht — stellt Daten-Arrays für `WeekBarVisualization` (in `Graphics/`) bereit. |
| `CalendarViewModel.cs` | Kalender mit Heatmap — kein `LoadDataAsync()` im Konstruktor (Lazy: erst beim Tab-Wechsel). |
| `StatisticsViewModel.cs` | Statistiken (5 Perioden: Week/Month/Quarter/Year/Custom), Rewarded-Ad-Gate für erweiterte Statistiken. |
| `SettingsViewModel.cs` | Einstellungen mit Debounce-AutoSave (800ms via `ScheduleAutoSave`), `SettingsChanged` Event. |
| `DayDetailViewModel.cs` | Tagesdetail (SelectedDate, manuelle Buchungen, Lock/Unlock). |
| `MonthOverviewViewModel.cs` | Monatsübersicht — stellt Daten-Arrays für `MonthlyBarChartVisualization` (in `Graphics/`) bereit. |
| `YearOverviewViewModel.cs` | Jahresübersicht mit Heatmap-Kalender, navigiert zu MonthOverview. |
| `VacationViewModel.cs` | Urlaubsverwaltung (DayStatus-Typen, Resturlaub, Rewarded-Ad-Gate). |
| `ShiftPlanViewModel.cs` | Schichtplanung (wiederkehrende Muster, Einzelzuweisungen) — kein Premium-Gate. |

## MainViewModel — Kern-Architektur

```
Konstruktor:
 ├─ Child-VMs per Constructor Injection (alle 9 als Parameter)
 ├─ WireSubPageNavigation() für jeden Child-VM (typed, kein Reflection)
 ├─ SettingsVm.SettingsChanged → OnSettingsChanged
 ├─ _backPressHelper.ExitHintRequested → FloatingTextRequested (WorkTimePro nutzt FloatingText statt ExitHint)
 ├─ _timeTracking.StatusChanged → OnStatusChanged
 └─ _initTask = InitializeAsync()   ← sofort starten, kein fire-and-forget

1s-Timer:
 └─ OnUpdateTimerElapsed → Reentrancy-Guard (Interlocked) → ForgetExtensions.RunForget(UpdateLiveDataAsync)
     └─ GetLiveDataSnapshotAsync()  ← ein Snapshot statt 5+ DB-Queries/s
```

`WaitForInitializationAsync()` / `EnsureInitializedAsync()` — alle Commands und `ToggleTrackingAsync`
awaiten dies, um Race-Condition bei schnellem Tap nach Start zu verhindern.

## Tab-Navigation (5 Tabs)

| Tab-Index | VM-Property | Daten-Laden |
|-----------|-------------|-------------|
| 0 | `IsTodayActive` | `LoadDataAsync()` (Live-Timer) |
| 1 | `IsWeekActive` | `WeekVm.LoadDataAsync()` |
| 2 | `IsCalendarActive` | `CalendarVm.LoadDataAsync()` |
| 3 | `IsStatisticsActive` | `StatisticsVm.LoadDataAsync()` |
| 4 | `IsSettingsActive` | `SettingsVm.LoadDataAsync()` |

`OnCurrentTabChanged` → `LoadTabDataAsync(tab)` via `.Forget(ex => ...)` (async, keine Race-Conditions).

## Sub-Page-Navigation (5 Sub-Pages)

Sub-Pages werden durch `Is{Name}Active`-Flags gesteuert (kein Avalonia Shell):
`IsDayDetailActive`, `IsMonthActive`, `IsYearActive`, `IsVacationActive`, `IsShiftPlanActive`.

`HandleNavigation(route)` parst URL-ähnliche Routen:
- `"DayDetailPage?date=2026-02-13"` → `DayDetailVm.SelectedDate` setzen + `LoadDataAsync()`.
- `"month?date=2026-02-01"` → `MonthVm.SelectedMonth` setzen + `LoadDataAsync()`.
- `"WeekOverviewPage"` → Tab 1 (WeekVm) wechseln.
- `".."` / `"back"` → `GoBack()` / `CloseAllSubPages()`.

**DateTime in Routen:** IMMER `CultureInfo.InvariantCulture` + `DateTimeStyles.RoundtripKind` beim Parse
(ISO-Routen sind locale-unabhängig — globale Konvention, hier im Route-Parser angewendet).

## Settings-Pattern

`SettingsViewModel.ScheduleAutoSave`: enthält `_ = Task.Run(async () => { ... })` mit
`InvokeAsync(async () => await SaveSettingsAsync())`. Das innere Lambda muss `async` sein — ohne
`async` wird der innere Task verworfen und Exceptions werden stillschweigend geschluckt.

`SettingsChanged` ist `EventHandler<bool>` — `bool` signalisiert ob Tab-Reload nötig ist (strukturelle
Änderungen wie Arbeitszeitänderungen = `true`, kosmetische wie Reminder-Zeiten = `false`).

`_cachedSettings` in `MainViewModel`: wird in `LoadDataAsync` und `OnSettingsChanged` aktualisiert.
`UpdateLiveDataAsync` (1s-Timer) nutzt Cache statt DB-Query.

## Undo-Mechanismus (5s-Fenster)

Nach CheckIn/CheckOut: `ShowUndo()` setzt `IsUndoVisible = true`, startet 5s-Task mit
`CancellationTokenSource`. Bei `UndoLastActionAsync`: Eintrag löschen → WorkDay neu berechnen →
Status neu laden. Vor jedem neuen Undo: `_undoCts?.Cancel()` (verhindert parallele Timers).

## Notiz-Debounce

`OnTodayNoteChanged` → `_noteDebouncer.Trigger(async _ => { ... })` (1500ms, `AsyncDebouncer`).
Während `LoadDataAsync`: `using (_noteDebouncer.Pause())` — verhindert Race zwischen DB-Load
und User-Eingabe ohne Suppress-Flag, das bei Exception hängen bleiben kann.

## Earnings-Ticker

`TodayEarningsValue` (double Roh-Wert) — vom TodayView für CountUp-Animation (800ms EaseOut)
genutzt. Nur gesetzt wenn `settings.HourlyRate > 0`. Vermeidet fehleranfälliges String-Reparsing
pro Sekunde.

## Typed Event-Wiring (kein Reflection)

`WireSubPageNavigation(ObservableObject vm)` prüft `is INavigationSource` und `is IMessageSource`.
Handler-Delegates werden in `_navHandlers` / `_msgHandlers` gelistet → `Dispose()` meldet sie
sicher ab (kein Event-Leak bei Singleton-Lifetime).

`IDisposable.Dispose()` auf `MainViewModel`: Timer stoppen, alle Events abmelden, Child-VMs
die `IDisposable` implementieren (z.B. `SettingsViewModel`, `DayDetailViewModel`) ebenfalls disposen.

## Gotchas

- **CalendarVM kein Ctor-Load:** `CalendarViewModel` hat keinen `_ = LoadDataAsync()` im
  Konstruktor — doppeltes Laden (Ctor + Tab-Wechsel) wurde bereits entfernt.
- **FloatingText statt ExitHint:** WorkTimePro nutzt `FloatingTextRequested` für den Double-Back-
  Hinweis (statt `ExitHintRequested` wie andere Apps) — weil FloatingText bereits für andere
  UI-Meldungen verdrahtet ist.
- **Tab-Reload vs. Lazy:** `OnCurrentTabChanged` → `LoadTabDataAsync` lädt IMMER neu. Sub-Page-
  Commands (DayDetail, Month, Year, Vacation, ShiftPlan) sind `async` und awaiten `LoadDataAsync`.
- **Reentrancy-Guard 1s-Timer:** `_liveUpdateGate` per `Interlocked.CompareExchange` verhindert
  überlappende `UpdateLiveDataAsync`-Ticks bei DB-Latenz > 1s (`System.Timers.Timer` feuert mit
  `AutoReset=true` unabhängig vom Handler).
