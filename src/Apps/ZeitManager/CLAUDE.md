# ZeitManager - Timer, Stoppuhr, Wecker

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## Status

**Version:** 2.0.6 | **Package-ID:** com.meineapps.zeitmanager | **Preis:** Kostenlos (werbefrei)

## App-Beschreibung

Multi-Timer, Stoppuhr mit Rundenzeiten, Pomodoro-Timer, Wecker mit Challenges, Schichtplan-Rechner (15/21-Schicht). 5 Tabs: Timer, Stoppuhr, Pomodoro, Wecker/Schichtplan, Settings. Komplett werbefrei, keine Premium-Features.

## Features

- **Timer:** Mehrere gleichzeitig, Quick-Timer (1/5/10/15/30 min), +1/+5 Min Extend, Alle löschen, Snooze, AutoRepeat, Presets (DB-gespeichert), eingebaute + System-/benutzerdefinierte Töne
- **Stoppuhr:** Rundenzeiten mit Best/Worst-Markierung + Delta, Undo-Funktion, Centisecond-Precision, FadeInBehavior auf Runden
- **Wecker:** CRUD, Weekday-Toggles, Challenge-Support (Math + Shake mit UI), Tonauswahl (eingebaut + System-Ringtones + benutzerdefiniert), Snooze mit konfigurierbarer Dauer, ansteigende Lautstärke, Urlaubsmodus (Alarm-Pause mit WheelPicker 1-30 Tage)
- **Schichtplan:** 15-Schicht (3 Gruppen Mo-Fr) + 21-Schicht (5 Gruppen 24/7), Kalender-Ansicht, Ausnahmen (Urlaub/Krank/Schichttausch)
- **Fullscreen Alarm-Overlay:** Content-Swap statt ZIndex-Overlay (Avalonia ZIndex Hit-Testing funktioniert nicht auf Android). Normaler Content + Tab-Bar werden per `IsVisible="{Binding !IsAlarmOverlayVisible}"` versteckt, Alarm-Content wird als Ersatz angezeigt. Pulsier-Animation (nur Opacity, kein ScaleTransform in KeyFrames), Dismiss + Snooze Buttons, Math-Challenge + Shake-Challenge zum Aufwachen
- **Pomodoro:** PomodoroViewModel mit konfigurierbaren Zeiten (Work/ShortBreak/LongBreak), Zyklus-Tracking (Zyklen bis Langpause + CycleDots), Auto-Start nächste Phase, Phasen-Ringfarbe (PhaseBrush), Streak-Anzeige (Tage in Folge), Focus-Statistiken (Heute + Woche mit Balkendiagramm/DayStatistic), FocusSession DB-Persistierung, Celebration + FloatingText bei Session-Abschluss, Config-Dialog (Bottom-Sheet, Preferences-gespeichert), Aufgabenname pro Session (optional, TextBox im Config-Dialog), konfigurierbares Tagesziel (1-20 Sessions, Stepper im Config-Dialog)
- **Haptic-Feedback:** IHapticService (HeavyClick bei Alarm-Dismiss + Timer-Ende, Click bei Snooze), Android: VibrationEffect, Desktop: NoOp
- **Game Juice:** FloatingTextOverlay (Stoppuhr-Runden, Timer fertig) + CelebrationOverlay (Confetti bei Timer-Ende) + TapScaleBehavior (Quick-Timer) + FadeInBehavior (Stoppuhr-Runden) + StaggerFadeInBehavior (Timer-/Alarm-Listen) + CountUpBehavior (Pomodoro-Statistiken) + SwipeToRevealBehavior (Alarm Swipe-to-Delete) + Onboarding (TooltipBubble, 2 Schritte) + TimerVisualization in TimerView (Flüssigkeitsfüllung + Wellen + Tropfen + Confetti bei Timer=0, ~30fps) + Streak Gold-Shimmer (#FFD700 + StreakGoldPulse bei Streak > 3) + Wochen-Balken-Einfahranimation (CubicEaseOut, ~500ms) + Monats-Heatmap (GitHub-Style) + BounceEaseOut Dialog-Spring-Animations (Timer/Alarm/Pomodoro Bottom-Sheets) + EmptyPulse auf Empty-State-Icons

## App-spezifische Services

- `ITimerService` → TimerService (In-Memory Timer Management + Snooze + AutoRepeat + System-Notifications + ExtendTimer + DeleteAll)
- `IAudioService` → AudioService/AndroidAudioService (Eingebaute Töne + System-Ringtones + PlayUriAsync + PickSoundAsync)
- `IAlarmSchedulerService` → AlarmSchedulerService (60s Check-Timer, Weekday-Matching, Double-Trigger-Schutz + System-Notifications via INotificationService + Urlaubsmodus/PauseAll)
- `IShiftScheduleService` → ShiftScheduleService (15/21-Schicht Berechnung + Ausnahmen)
- `IShakeDetectionService` → DesktopShakeDetectionService/AndroidShakeDetectionService (Shake-Challenge: Desktop=Button-Simulation, Android=Accelerometer)
- `INotificationService` → Plattform-spezifisch via `ConfigurePlatformServices` (Android: AndroidNotificationService, Desktop: DesktopNotificationService)
- `IHapticService` → NoOpHapticService (Desktop) / AndroidHapticService (Android): Haptisches Feedback bei Alarm-Dismiss (HeavyClick), Snooze (Click), Timer-Ende (HeavyClick)

## Shared Audio-Klassen

```
ZeitManager.Shared/Audio/
├── WavGenerator.cs        # WAV-Daten generieren (Frequenz + Dauer)
├── SoundDefinitions.cs    # 6 eingebaute Töne mit Frequenz/Dauer
└── TimeFormatHelper.cs    # Shared HH:MM:SS.cs Formatierung
```

## Android-Services

```
ZeitManager.Android/Services/
├── TimerForegroundService.cs     # Foreground Service mit Notification (Timer-Countdown)
├── AlarmReceiver.cs              # BroadcastReceiver fuer Wecker-Ausloesung
├── BootReceiver.cs               # BOOT_COMPLETED → Wecker neu planen
├── AlarmActivity.cs              # Fullscreen Lockscreen-Alarm (Dismiss/Snooze, Gradual Volume, Custom Sound)
├── AndroidAudioService.cs        # System-Ringtones via RingtoneManager + PlayUri + PickSound
├── AndroidNotificationService.cs # NotificationChannels + AlarmManager + StableHash
└── AndroidShakeDetectionService.cs # Accelerometer-basierte Shake-Erkennung
```

**AndroidManifest Permissions:** FOREGROUND_SERVICE, SCHEDULE_EXACT_ALARM, RECEIVE_BOOT_COMPLETED, POST_NOTIFICATIONS, VIBRATE, USE_FULL_SCREEN_INTENT, WAKE_LOCK

## Architektur-Entscheidungen

- **Loading-Pipeline:** `ZeitManagerLoadingPipeline` (in `Loading/`) führt echtes Preloading aus: DB-Init + Shader-Kompilierung parallel, dann AlarmScheduler, ViewModel-Erstellung. `SkiaLoadingSplash` zeigt Fortschrittsring + Statustext. App.axaml.cs setzt DataContext erst nach Pipeline-Abschluss (statt synchron). Bisheriges fire-and-forget `_ = InitializeServicesAsync()` entfernt.
- **Alarm/Timer-Notifications (Hintergrund):** AlarmSchedulerService und TimerService nutzen INotificationService, um System-Notifications zu planen (Android: AlarmManager.SetAlarmClock, Desktop: Task.Delay). Dadurch funktionieren Alarme/Timer auch wenn die App minimiert/geschlossen ist. AlarmViewModel nutzt IAlarmSchedulerService statt direkt die DB, damit Notifications konsistent geplant/gecancelt werden.
- **AlarmActivity:** Dedizierte Android Activity (ShowWhenLocked, TurnScreenOn) fuer Fullscreen-Alarm über Lockscreen. Wird von AlarmReceiver gestartet (via AlarmManager). Buttons (Dismiss/Snooze) lokalisiert via `App.Services.GetService<ILocalizationService>()`. Unterstützt benutzerdefinierte Alarm-Töne via `alarm_tone` Intent-Extra, Snooze-Dauer via `snooze_duration` Extra, ansteigende Lautstärke (Volume Ramp).
- **Sound-System:** IAudioService erweitert mit SystemSounds, PlayUriAsync, PickSoundAsync. Android: RingtoneManager für System-Sounds + RingtoneManager.ActionRingtonePicker für Auswahl (ActivityResult via MainActivity). Desktop: Avalonia StorageProvider.OpenFilePickerAsync + Kopie in AppData. SoundItem hat optionale Uri (null für eingebaute Töne).
- **StableHash:** Deterministische Hash-Funktion für Alarm-IDs (statt GetHashCode() der nicht deterministisch ist). Verwendet in AndroidNotificationService, AlarmActivity.
- **Foreground-Check:** `MainActivity.IsAppInForeground` statisches Flag. AlarmReceiver prüft dies um Doppel-Auslösung (AlarmActivity + In-App Overlay) zu vermeiden.
- **UI-Thread:** System.Timers.Timer feuert auf ThreadPool → `Dispatcher.UIThread.Post()` fuer Property-Updates
- **Stopwatch Undo:** TimeSpan _offset Pattern (Stopwatch unterstuetzt keine direkte Elapsed-Zuweisung)
- **Thread-Safety:** TimerService und AlarmSchedulerService nutzen `lock(_lock)` fuer List-Zugriffe, AudioService lock-swap fuer CTS, DesktopNotificationService ConcurrentDictionary
- **AlarmItem:** Erbt ObservableObject, IsEnabled nutzt SetProperty fuer UI-Notification
- **CustomShiftPattern:** ShortName() nutzt LocalizationManager.GetString() fuer lokalisierte Schicht-Kuerzel

## SkiaSharp-Visualisierungen

6 Visualisierungen in `Graphics/`:

| Datei | Beschreibung | Genutzt in |
|-------|-------------|------------|
| `ClockworkBackgroundRenderer.cs` | Animierter "Warm Clockwork"-Hintergrund (5 Layer): 3-Farben-Gradient (#282018/#1A2332/#28200E), konzentrische pulsierende Uhrenringe (Alpha 8-10), driftende Gluehwuermchen-Partikel (Amber, Alpha 15-20, Glow via MaskFilter), 60 Tick-Markierungen, radiale Vignette. Struct-Pool (max 12), gecachte Paints/Shader, ~5fps DispatcherTimer | MainView |
| `StopwatchVisualization.cs` | Stoppuhr-Ring mit Sekundenzeiger + Nachleucht-Trail (6 Ghost-Positionen), Runden-Sektoren (farbige Bögen pro Runde, 8 Farben), Sub-Dial (Minuten-Ring oben rechts), 60 Sekunden-Ticks, Glow, Rundenpunkte | StopwatchView |
| `PomodoroVisualization.cs` | RenderRing: Fortschrittsring mit Pulsier-Effekt (2Hz) auf aktivem Zyklus-Segment + Glow, innerer Session-Ring (Tages-Fortschritt als Segment-Bögen); RenderWeeklyBars: Wochen-Balkendiagramm | PomodoroView |
| `TimerVisualization.cs` | Timer-Ring mit Flüssigkeits-Füllung + Welleneffekt, Tropfen-Partikel (8 Stück, fallen von Oberfläche), Countdown-Ziffern (letzte 5s, Scale-Bounce 1.5→1.0), Ablauf-Burst (20 Confetti-Partikel bei Timer=0) | TimerView (Reserve) |
| `PomodoroStatisticsVisualization.cs` | Monats-Heatmap (GitHub-Contributions-Style): 7x5 Grid, 5 Intensitätsstufen (0→4+ Sessions), Wochentag-Labels, Heute-Highlight, Farb-Legende, HitTest für Tap-Interaktion | PomodoroView (Statistik-Ansicht) |
| `ZeitManagerSplashRenderer.cs` | App-spezifischer Splash: "Die tickende Uhr" - Analoge Uhr mit Snap-Tick Sekundenzeiger, kreisförmiger Progress-Ring, 12 rotierende Zahnrad-Partikel, konzentrische Deko-Ringe | Splash-Screen |

**TimerView:** Nutzt `SkiaGradientRing` aus MeineApps.UI (Shared Control) statt `CircularProgress` pro Timer-Item, mit `GlowEnabled`/`IsPulsing` bei laufendem Timer.

## Abhaengigkeiten

- MeineApps.Core.Ava, MeineApps.UI
- sqlite-net-pcl + SQLitePCLRaw.bundle_green
- SkiaSharp + Avalonia.Labs.Controls (SkiaSharp-Visualisierungen)
- **Kein MeineApps.Core.Premium - komplett werbefrei!**
