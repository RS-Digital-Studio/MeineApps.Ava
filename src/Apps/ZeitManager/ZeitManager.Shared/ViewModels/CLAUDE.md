# ViewModels — Zeit-Logik & State

Alle ViewModels sind **Singleton** (in `App.axaml.cs` registriert) und werden vom `MainViewModel`
gehalten. Nur UI-Logik — Domain-Operationen delegieren an Services. Generische MVVM-Conventions →
[Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainViewModel.cs` | Tab-Navigation (5 Tabs), Alarm-Overlay-Steuerung, Back-Press-Flow, Event-Relay (FloatingText, Celebration, ExitHint), Onboarding-State, Snackbar. |
| `TimerViewModel.cs` | Timer-CRUD, Quick-Timer, Extend, Presets (DB), Delete/DeleteAll-Confirm, `ShowVisualization`-Flag für SkiaSharp-Render-Loop. |
| `StopwatchViewModel.cs` | `System.Diagnostics.Stopwatch` + `_offset` (Undo-Pattern), Rundenzeiten mit Best/Worst, Centisecond-Precision, `TotalElapsedSeconds` für SkiaSharp. |
| `PomodoroViewModel.cs` | Phasen-Tracking (Work/ShortBreak/LongBreak), Konfiguration (Preferences), Streak, `FocusSession`-DB-Persistierung, Statistik-Daten (Wochen + Heatmap). |
| `AlarmViewModel.cs` | Alarm-CRUD, Editor-Felder (Zeit, Name, Weekdays, Challenge, Ton), `IsShiftScheduleMode`, `ShiftScheduleViewModel`-Embedding. |
| `AlarmOverlayViewModel.cs` | Overlay-State für Timer-Ende + Alarm-Auslösung. `ShowForTimer()` / `ShowForAlarm()`. `DismissRequested`-Event → MainViewModel schaltet `IsAlarmOverlayVisible = false`. |
| `SettingsViewModel.cs` | Sprache, Vibration, Snooze-Dauer. `MessageRequested` → Snackbar im MainViewModel. |
| `ShiftScheduleViewModel.cs` | 15/21-Schicht-Berechnung, Kalender-Ansicht, Ausnahmen-CRUD. Eingebettet in `AlarmViewModel`. |

## MainViewModel — Back-Navigation

Reihenfolge in `HandleBackPressed()`:
1. `IsAlarmOverlayVisible` → true: Alarm-Overlay ist nicht per Back schließbar (Benutzer muss
   Dismiss/Snooze drücken).
2. Tab-spezifische Overlays: Timer (DeleteAllConfirm, DeleteConfirm, IsCreatingTimer), Pomodoro
   (IsConfigVisible, IsStatisticsView), Alarm (IsPauseDialogVisible, IsDeleteConfirmVisible,
   IsEditMode, IsShiftScheduleMode).
3. Nicht auf Tab 0 → `SelectedTabIndex = 0`.
4. Tab 0 → Double-Back-to-Exit via `BackPressHelper`.

## MainViewModel — Event-Relay

Events von Kind-ViewModels werden im `MainViewModel`-Ctor verdrahtet und nach oben weitergeleitet:
- `_stopwatchViewModel.FloatingTextRequested` + `_pomodoroViewModel.FloatingTextRequested` →
  `FloatingTextRequested` (MainView hört zu).
- `_pomodoroViewModel.CelebrationRequested` → `CelebrationRequested`.
- `_timerService.TimerFinished` → `AlarmOverlayViewModel.ShowForTimer()` + Celebration.
- `_alarmScheduler.AlarmTriggered` → `AlarmOverlayViewModel.ShowForAlarm()`.
- `_settingsViewModel.MessageRequested` + `_timerViewModel.MessageRequested` → `ShowSnackbar()`.
- Alle Events werden in `Dispose()` sauber abgemeldet.

## Initialisierung (WaitForInitializationAsync)

`MainViewModel.WaitForInitializationAsync()` wartet auf `TimerViewModel` und `AlarmViewModel` —
beide laden ihre Daten aus der DB asynchron. Die Loading-Pipeline ruft diese Methode auf, bevor
der Splash verschwindet. Ohne das Wait könnten Timer/Alarme erst nach Splash-FadeOut sichtbar
werden (Race Condition).

## Stoppuhr — Undo-Pattern

`System.Stopwatch` unterstützt keine direkte Elapsed-Zuweisung. Beim Undo wird daher ein
`_offset`-Feld akkumuliert statt `Elapsed` direkt zu setzen:
```csharp
// Undo: gespeicherten Zustand (Offset, Laps) aus Snapshot wiederherstellen
_offset = _undoOffset;
_laps = _undoLaps;
```
`TotalElapsedSeconds` ist eine separate `[ObservableProperty]`-double, um String-Parsing im
SkiaSharp-Renderer zu vermeiden.

## PomodoroViewModel — Statistik-Daten

`WeekDays` (7 `DayStatistic`-Einträge mit `DayName`, `Sessions`, `IsToday`) und
`HeatmapDays` (Array von `(Date, Count)`-Tupeln) werden nach jeder Session-Persistierung aktualisiert.
Die `PomodoroView` rendert sie direkt per SkiaSharp — kein ItemsControl.
