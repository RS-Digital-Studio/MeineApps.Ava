# ZeitManager - Timer, Stoppuhr, Wecker

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## Status

**Version:** 2.0.0 | **Package-ID:** com.meineapps.zeitmanager | **Preis:** Kostenlos (werbefrei)

## App-Beschreibung

Multi-Timer, Stoppuhr mit Rundenzeiten, Wecker mit Challenges, Schichtplan-Rechner (15/21-Schicht). Komplett werbefrei, keine Premium-Features.

## Features

- **Timer:** Mehrere gleichzeitig, Quick-Timer (1/5/10/15/30 min), Snooze, 6 Toene
- **Stoppuhr:** Rundenzeiten, Undo-Funktion, Centisecond-Precision
- **Wecker:** CRUD, Weekday-Toggles, Challenge-Support (Math/Shake), Tonauswahl, Snooze
- **Schichtplan:** 15-Schicht (3 Gruppen Mo-Fr) + 21-Schicht (5 Gruppen 24/7), Kalender-Ansicht
- **Fullscreen Alarm-Overlay:** Pulsier-Animation, Dismiss + Snooze Buttons
- **Game Juice:** FloatingTextOverlay (Stoppuhr-Runden, Timer fertig) + CelebrationOverlay (Confetti bei Timer-Ende)

## App-spezifische Services

- `ITimerService` → TimerService (In-Memory Timer Management + Snooze + System-Notifications)
- `IAudioService` → AudioService (6 Toene, Console.Beep / BEL Fallback)
- `IAlarmSchedulerService` → AlarmSchedulerService (10s Check-Timer, Weekday-Matching, Double-Trigger-Schutz + System-Notifications via INotificationService)
- `IShiftScheduleService` → ShiftScheduleService (15/21-Schicht Berechnung)
- `INotificationService` → Plattform-spezifisch via `ConfigurePlatformServices` (Android: AndroidNotificationService, Desktop: DesktopNotificationService)

## Android-Services

```
ZeitManager.Android/Services/
├── TimerForegroundService.cs    # Foreground Service mit Notification (Timer-Countdown)
├── AlarmReceiver.cs             # BroadcastReceiver fuer Wecker-Ausloesung
├── BootReceiver.cs              # BOOT_COMPLETED → Wecker neu planen
└── AndroidNotificationService.cs # NotificationChannels + AlarmManager
```

**AndroidManifest Permissions:** FOREGROUND_SERVICE, SCHEDULE_EXACT_ALARM, RECEIVE_BOOT_COMPLETED, POST_NOTIFICATIONS, VIBRATE, USE_FULL_SCREEN_INTENT, WAKE_LOCK

## Architektur-Entscheidungen

- **Alarm/Timer-Notifications (Hintergrund):** AlarmSchedulerService und TimerService nutzen INotificationService, um System-Notifications zu planen (Android: AlarmManager.SetAlarmClock, Desktop: Task.Delay). Dadurch funktionieren Alarme/Timer auch wenn die App minimiert/geschlossen ist. AlarmViewModel nutzt IAlarmSchedulerService statt direkt die DB, damit Notifications konsistent geplant/gecancelt werden.
- **AlarmActivity:** Dedizierte Android Activity (ShowWhenLocked, TurnScreenOn) fuer Fullscreen-Alarm über Lockscreen. Wird von AlarmReceiver gestartet (via AlarmManager). Buttons (Dismiss/Snooze) lokalisiert via `App.Services.GetService<ILocalizationService>()`.
- **UI-Thread:** System.Timers.Timer feuert auf ThreadPool → `Dispatcher.UIThread.Post()` fuer Property-Updates
- **Stopwatch Undo:** TimeSpan _offset Pattern (Stopwatch unterstuetzt keine direkte Elapsed-Zuweisung)
- **Thread-Safety:** TimerService und AlarmSchedulerService nutzen `lock(_lock)` fuer List-Zugriffe, AudioService lock-swap fuer CTS, DesktopNotificationService ConcurrentDictionary
- **AlarmItem:** Erbt ObservableObject, IsEnabled nutzt SetProperty fuer UI-Notification
- **CustomShiftPattern:** ShortName() nutzt LocalizationManager.GetString() fuer lokalisierte Schicht-Kuerzel

## Abhaengigkeiten

- MeineApps.Core.Ava, MeineApps.UI
- sqlite-net-pcl + SQLitePCLRaw.bundle_green
- **Kein MeineApps.Core.Premium - komplett werbefrei!**

## Changelog (Highlights)

- **11.02.2026 (3)**: Timer startet automatisch nach Erstellung (wie Quick-Timer)
- **11.02.2026 (2)**: Optimierungen: TimerService Recovery nach App-Kill (Running-Timer Ablauf prüfen, Paused-Timer validieren, Finished melden), TimerViewModel Validierung (Duration <= 0 → MessageRequested mit TimerDurationInvalid), AlarmSchedulerService Intervall 30s→10s (verhindert verpasste Alarme), AlarmViewModel SnoozeDuration-Validierung (min. 1 Min.) + ToggleAlarm Double-Tap-Guard, AlarmOverlayViewModel "min"-Text lokalisiert, 1 neuer RESX-Key (TimerDurationInvalid) in allen 6 Sprachen
- **11.02.2026**: Bugfix-Review: AlarmSchedulerService Thread-Safety (lock um _activeAlarms), TimerService Notification-Text lokalisiert, AlarmActivity Buttons lokalisiert (Dismiss/Snooze), AlarmOverlayViewModel von Transient zu Singleton
- **v2.0.0-notifications**: AlarmSchedulerService + TimerService mit INotificationService verbunden → Alarme/Timer funktionieren jetzt auch bei minimierter App (Android AlarmManager + AlarmReceiver + AlarmActivity). AlarmViewModel nutzt AlarmSchedulerService statt direkt DB.
- **v2.0.0-review**: DatabaseService SemaphoreSlim, StopwatchVM Dispatcher, AlarmScheduler Double-Trigger-Schutz, Delete-Bestaetigungen, Android Runtime Permissions
- **v2.0.0-gamejuice**: FloatingTextOverlay + CelebrationOverlay eingebaut
