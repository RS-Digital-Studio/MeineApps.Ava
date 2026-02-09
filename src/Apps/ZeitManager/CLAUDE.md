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

- `ITimerService` → TimerService (In-Memory Timer Management + Snooze)
- `IAudioService` → AudioService (6 Toene, Console.Beep / BEL Fallback)
- `IAlarmSchedulerService` → AlarmSchedulerService (30s Check-Timer, Weekday-Matching, Double-Trigger-Schutz)
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

- **UI-Thread:** System.Timers.Timer feuert auf ThreadPool → `Dispatcher.UIThread.Post()` fuer Property-Updates
- **Stopwatch Undo:** TimeSpan _offset Pattern (Stopwatch unterstuetzt keine direkte Elapsed-Zuweisung)
- **Thread-Safety:** TimerService nutzt `lock(_lock)` fuer _timers List, AudioService lock-swap fuer CTS, DesktopNotificationService ConcurrentDictionary
- **AlarmItem:** Erbt ObservableObject, IsEnabled nutzt SetProperty fuer UI-Notification
- **CustomShiftPattern:** ShortName() nutzt LocalizationManager.GetString() fuer lokalisierte Schicht-Kuerzel

## Abhaengigkeiten

- MeineApps.Core.Ava, MeineApps.UI
- sqlite-net-pcl + SQLitePCLRaw.bundle_green
- **Kein MeineApps.Core.Premium - komplett werbefrei!**

## Changelog (Highlights)

- **v2.0.0-review**: DatabaseService SemaphoreSlim, StopwatchVM Dispatcher, AlarmScheduler Double-Trigger-Schutz, Delete-Bestaetigungen, Android Runtime Permissions
- **v2.0.0-gamejuice**: FloatingTextOverlay + CelebrationOverlay eingebaut
