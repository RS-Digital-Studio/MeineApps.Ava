# Services — Domain-Logik

Alle Services sind **Singleton** (in `App.axaml.cs` registriert). Thread-Safety via `lock(_lock)`
für Listen-Zugriffe und `SemaphoreSlim` für DB-Init. Generische Conventions →
[Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Interface | Zweck |
|-------|-----------|-------|
| `TimerService.cs` | `ITimerService` | In-Memory Timer-Management: Create/Start/Pause/Stop/Delete/Extend/Snooze/AutoRepeat. Foreground-Notification-Callbacks für Android. `TimerFinished`-Event → MainViewModel. |
| `AlarmSchedulerService.cs` | `IAlarmSchedulerService` | 60s-Check-Timer, Weekday-Matching, Double-Trigger-Schutz via `_triggeredToday`. Urlaubsmodus (PauseAll). `AlarmTriggered` + `AlarmPermissionMissing`-Events. |
| `AudioService.cs` | `IAudioService` | Desktop-Impl: WAV-Bytes via `WavGenerator` in TempFile, Wiedergabe via `Process.Start`. Lock-swap auf CTS bei parallelen Aufrufen. |
| `DatabaseService.cs` | `IDatabaseService` | SQLite (sqlite-net-pcl). DB-Pfad: `LocalApplicationData/zeitmanager.db3`. Tabellen: `Timers`, `Alarms`, `ShiftSchedule`, `ShiftException`, `CustomShiftPattern`, `TimerPreset`, `FocusSession`. Init via `SemaphoreSlim`-Guard (Double-Check). |
| `ShiftScheduleService.cs` | `IShiftScheduleService` | 15-Schicht (3 Gruppen Mo-Fr) + 21-Schicht (5 Gruppen 24/7). Berechnung + Ausnahmen (Urlaub/Krank/Schichttausch). |
| `DesktopShakeDetectionService.cs` | `IShakeDetectionService` | Simuliert Shake per Button-Aufruf (kein Accelerometer auf Desktop). |
| `DesktopNotificationService.cs` | `INotificationService` | Desktop-Fallback: `ConcurrentDictionary`-basiertes Scheduling, keine nativen Notifications. |
| `IAlarmSchedulerService.cs` | — | `InitializeAsync()`, `AddAlarm()`/`RemoveAlarm()`, `PauseAll(days)`, `PausedUntil`, `IsAllPaused`. Events: `AlarmTriggered`, `AlarmPermissionMissing`. |
| `ITimerService.cs` | — | CRUD + `GetRemainingTime(timer)`, `RunningTimerCount`. Events: `TimerFinished`, `TimerTick`, `TimersChanged`. |
| `IAudioService.cs` | — | `AvailableSounds`, `SystemSounds`, `PlayAsync(id, loop)`, `PlayUriAsync(uri, loop)`, `Stop()`, `PickSoundAsync()`. |
| `IDatabaseService.cs` | — | `InitializeAsync()` + CRUD für alle 7 Tabellen. |
| `INotificationService.cs` | — | `ScheduleAlarm()`, `CancelAlarm()`, `CanScheduleExactAlarms()`, `ShowTimerNotification()`. |
| `IShakeDetectionService.cs` | — | `StartListening()`, `StopListening()`. Event: `ShakeDetected`. |

## Thread-Safety-Regeln

- **`TimerService`**: `lock(_lock)` für `_timers`-Listen-Zugriffe. `System.Timers.Timer` feuert
  auf ThreadPool → `Dispatcher.UIThread.Post()` für alle Property-Updates auf `TimerItem`.
- **`AlarmSchedulerService`**: `lock(_lock)` für `_activeAlarms`. `_triggeredToday`-HashSet
  (nur im Lock zugegriffen) verhindert Doppel-Auslösung innerhalb eines Tages. Datum-Reset
  über `_lastTriggerDate`.
- **`AudioService`**: Lock-swap auf CTS — neuer `PlayAsync`-Aufruf cancelt den laufenden
  ohne Deadlock.
- **`DatabaseService`**: `SemaphoreSlim(1,1)` um `InitializeAsync()` — Double-Check-Guard
  verhindert parallele Schema-Erstellung.

## AlarmSchedulerService — Double-Trigger-Schutz

Der 60s-Check-Timer prüft alle aktiven Alarme gegen `DateTime.Now`. `_triggeredToday` speichert
bereits gefeuerte Alarm-IDs für den aktuellen Tag (`DateOnly.FromDateTime(DateTime.Now)`).
Bei Datumswechsel (`_lastTriggerDate != today`) wird das Set geleert.

```csharp
// Gotcha: DateTime.UtcNow für PausedUntil-Vergleich, aber DateOnly.FromDateTime(DateTime.Now)
// für _lastTriggerDate — konsistent halten oder auf Local umstellen.
if (dt > DateTime.UtcNow) PausedUntil = dt;
```

## TimerService — Foreground-Callbacks

Damit `TimerForegroundService` (Android) aktualisiert wird, setzt `MainActivity.OnCreate`
(nach `base.OnCreate`) zwei Callbacks direkt auf dem `TimerService`-Objekt:
```csharp
timerService.ForegroundNotificationCallback = (name, remaining) =>
    TimerForegroundService.UpdateNotification(this, name, remaining);
timerService.StopForegroundCallback = () => TimerForegroundService.StopService(this);
```
Auf Desktop bleiben die Callbacks null → kein Foreground-Service-Aufruf.
