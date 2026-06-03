# Models — DB-Entitäten & Enums

Datenmodelle für SQLite (sqlite-net-pcl) und Enums. Keine Logik hier — Berechnungs- und
Scheduling-Logik gehört in `Services/`. Generische Conventions →
[Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Inhalt |
|-------|--------|
| `TimerItem.cs` | `[Table("Timers")]`, `ObservableObject`. Alle Property-Änderungen via `SetProperty()`. Computed: `Duration`, `RemainingTime`, `ProgressFraction`, `ProgressBrush`, `ProgressRingColor` (gecacht), `RemainingTimeFormatted`. |
| `AlarmItem.cs` | `[Table("Alarms")]`, `ObservableObject`. `IsEnabled` via `SetProperty()` für UI-Reaktivität. Computed: `Time` (TimeOnly), `NextTriggerDateTime`, `RepeatDaysFormatted` (lokalisiert). `NotifyLocalizationChanged()` für Sprach-Wechsel. |
| `TimerPreset.cs` | `[Table("TimerPresets")]`. Name + Dauer + `AutoRepeat`. Computed: `Duration`, `DurationFormatted`. |
| `FocusSession.cs` | `[Table("FocusSessions")]`. Pomodoro-Session mit Datum + Phase + Dauer. Basis für Statistik-Abfragen. |
| `ShiftSchedule.cs` | `[Table("ShiftSchedules")]`. Schichtplan-Konfiguration (Typ, Startdatum, Gruppe, Wake-Times). |
| `ShiftException.cs` | `[Table("ShiftExceptions")]`. Ausnahmen pro Datum (Urlaub/Krank/Schichttausch). |
| `CustomShiftPattern.cs` | `[Table("CustomPatterns")]`. Kommagetrennte `ShiftType`-Werte als `Pattern`-String. `PatternSummary` (lokalisierte Kürzel via `LocalizationManager.GetString()`). |
| `StopwatchLap.cs` | Kein DB-Mapping (reines `record`). Rundenzeiten mit Best/Worst-Flags, Delta zur Vorrundenzeit, farblicher Hervorhebung. |
| `SoundItem.cs` | Kein DB-Mapping (reines `record`). `Id` (Lookup-Key) + `Name` (Anzeige) + `IsSystem` + nullable `Uri`. |
| `TimerState.cs` | Enum: `Stopped`, `Running`, `Paused`, `Finished`. |
| `WeekDays.cs` | `[Flags]`-Enum: `None`, `Monday`–`Sunday`, `Weekdays`, `Weekend`, `EveryDay`. |
| `PomodoroPhase.cs` | Enum: `Work`, `ShortBreak`, `LongBreak`. |
| `ChallengeType.cs` | Enum: `None`, `Math`, `Shake`. |
| `ChallengeDifficulty.cs` | Enum (in `ChallengeType.cs`): `Easy`, `Medium`, `Hard`. |
| `MathChallenge.cs` | Generierte Aufgabe mit `Question` + `Answer`. |
| `ExceptionType.cs` | Enum für `ShiftException`: `Vacation`, `Substitute`, `ShiftSwap`, `Sick`, `Other`. |
| `ShiftType.cs` | Enum: `Free`, `Early`, `Late`, `Night`, `Vacation`, `Substitute`, `Sick`. |
| `ShiftPatternType.cs` | Enum: `FifteenShift`, `TwentyOneShift`, `Custom`. |

## TimerItem — ProgressBrush & ProgressRingColor

Beide Computed Properties cachen den letzten berechneten Wert per `_progressBrushColor`-String-
Vergleich. Wechsel erfolgt nur bei tatsächlichem Farbwechsel (>30% Grün, 10–30% Amber, <10% Rot):
```csharp
if (_progressBrush == null || _progressBrushColor != currentColor)
    _progressBrush = new SolidColorBrush(Color.Parse(currentColor));
```
Kein `new SolidColorBrush` pro Tick — verhindert GC-Pressure bei laufendem Timer.

## AlarmItem — RepeatDaysFormatted

Lokalisiert via `LocalizationManager.GetString()` (kein Service-Inject nötig, da
`LocalizationManager` statisch ist). `NotifyLocalizationChanged()` muss nach Sprach-Wechsel
aufgerufen werden — `OnLanguageChanged` in `MainViewModel` ist zuständig dafür (über
`AlarmViewModel.NotifyAlarmLocalization()`).
