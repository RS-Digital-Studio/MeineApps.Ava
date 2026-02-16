# ZeitManager - Timer, Stoppuhr, Wecker

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## Status

**Version:** 2.0.0 | **Package-ID:** com.meineapps.zeitmanager | **Preis:** Kostenlos (werbefrei)

## App-Beschreibung

Multi-Timer, Stoppuhr mit Rundenzeiten, Pomodoro-Timer, Wecker mit Challenges, Schichtplan-Rechner (15/21-Schicht). 5 Tabs: Timer, Stoppuhr, Pomodoro, Wecker/Schichtplan, Settings. Komplett werbefrei, keine Premium-Features.

## Features

- **Timer:** Mehrere gleichzeitig, Quick-Timer (1/5/10/15/30 min), +1/+5 Min Extend, Alle löschen, Snooze, AutoRepeat, Presets (DB-gespeichert), eingebaute + System-/benutzerdefinierte Töne
- **Stoppuhr:** Rundenzeiten mit Best/Worst-Markierung + Delta, Undo-Funktion, Centisecond-Precision, FadeInBehavior auf Runden
- **Wecker:** CRUD, Weekday-Toggles, Challenge-Support (Math + Shake mit UI), Tonauswahl (eingebaut + System-Ringtones + benutzerdefiniert), Snooze mit konfigurierbarer Dauer, ansteigende Lautstärke, Urlaubsmodus (Alarm-Pause mit WheelPicker 1-30 Tage)
- **Schichtplan:** 15-Schicht (3 Gruppen Mo-Fr) + 21-Schicht (5 Gruppen 24/7), Kalender-Ansicht, Ausnahmen (Urlaub/Krank/Schichttausch)
- **Fullscreen Alarm-Overlay:** Pulsier-Animation, Dismiss + Snooze Buttons, Math-Challenge + Shake-Challenge zum Aufwachen
- **Pomodoro:** PomodoroViewModel mit konfigurierbaren Zeiten (Work/ShortBreak/LongBreak), Zyklus-Tracking (Zyklen bis Langpause + CycleDots), Auto-Start nächste Phase, Phasen-Ringfarbe (PhaseBrush), Streak-Anzeige (Tage in Folge), Focus-Statistiken (Heute + Woche mit Balkendiagramm/DayStatistic), FocusSession DB-Persistierung, Celebration + FloatingText bei Session-Abschluss, Config-Dialog (Bottom-Sheet, Preferences-gespeichert)
- **Game Juice:** FloatingTextOverlay (Stoppuhr-Runden, Timer fertig) + CelebrationOverlay (Confetti bei Timer-Ende) + TapScaleBehavior (Quick-Timer) + FadeInBehavior (Stoppuhr-Runden) + StaggerFadeInBehavior (Timer-/Alarm-Listen) + CountUpBehavior (Pomodoro-Statistiken) + SwipeToRevealBehavior (Alarm Swipe-to-Delete) + Onboarding (TooltipBubble, 2 Schritte)

## App-spezifische Services

- `ITimerService` → TimerService (In-Memory Timer Management + Snooze + AutoRepeat + System-Notifications + ExtendTimer + DeleteAll)
- `IAudioService` → AudioService/AndroidAudioService (Eingebaute Töne + System-Ringtones + PlayUriAsync + PickSoundAsync)
- `IAlarmSchedulerService` → AlarmSchedulerService (10s Check-Timer, Weekday-Matching, Double-Trigger-Schutz + System-Notifications via INotificationService + Urlaubsmodus/PauseAll)
- `IShiftScheduleService` → ShiftScheduleService (15/21-Schicht Berechnung + Ausnahmen)
- `IShakeDetectionService` → DesktopShakeDetectionService/AndroidShakeDetectionService (Shake-Challenge: Desktop=Button-Simulation, Android=Accelerometer)
- `INotificationService` → Plattform-spezifisch via `ConfigurePlatformServices` (Android: AndroidNotificationService, Desktop: DesktopNotificationService)

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

3 Visualisierungen in `Graphics/`:

| Datei | Beschreibung | Genutzt in |
|-------|-------------|------------|
| `StopwatchVisualization.cs` | Stoppuhr-Ring mit 60 Sekunden-Ticks, Glow, Minuten-Zeiger, Rundenpunkte, zentrale Zeitanzeige | StopwatchView (ersetzt CSS-Border-Ring) |
| `PomodoroVisualization.cs` | RenderRing: Fortschrittsring mit Phase-Farben + Zyklus-Segmente; RenderWeeklyBars: Wochen-Balkendiagramm | PomodoroView (Ring + Statistik) |
| `TimerVisualization.cs` | Timer-Ring mit Flüssigkeits-Füllung, Welleneffekt, Farbwechsel nach Restzeit | *(Reserve, Render-Methode vorhanden)* |

**TimerView:** Nutzt `SkiaGradientRing` aus MeineApps.UI (Shared Control) statt `CircularProgress` pro Timer-Item, mit `GlowEnabled`/`IsPulsing` bei laufendem Timer.

## Abhaengigkeiten

- MeineApps.Core.Ava, MeineApps.UI
- sqlite-net-pcl + SQLitePCLRaw.bundle_green
- SkiaSharp + Avalonia.Labs.Controls (SkiaSharp-Visualisierungen)
- **Kein MeineApps.Core.Premium - komplett werbefrei!**

## Changelog (Highlights)

- **13.02.2026 (10)**: Verbleibende UI/UX Features: (1) StaggerFadeInBehavior auf Timer-Liste und Alarm-Liste (gestaffeltes Einblenden der Items). (2) CountUpBehavior auf Pomodoro CompletedSessions + CurrentStreak (animiertes Hochzählen). (3) SwipeToRevealBehavior (neues Behavior in MeineApps.UI) + Alarm Swipe-to-Delete (Panel mit Delete-Layer dahinter, Content verschiebbar, SwipeDeleteAlarmCommand ohne Bestätigungsdialog). (4) Onboarding mit TooltipBubble (2 Schritte: Quick-Timer Tipp + Custom-Timer Tipp, onboarding_completed in Preferences). 2 neue RESX-Keys (OnboardingQuickTimer, OnboardingCreateTimer) in allen 6 Sprachen.
- **13.02.2026 (9)**: UI/UX Komplett-Überarbeitung (5 Phasen): Phase 1 (Timer + Stoppuhr): EmptyStateView für leere Timer-/Runden-Liste, TapScaleBehavior auf Quick-Timer-Chips, Farbwechsel-Ring nach Restzeit (grün >30%, amber 10-30%, rot <10%), Card.Interactive auf Timer-Kacheln, Stoppuhr-Ring 280→340px, Beste/Schlechteste Runde markiert (grün/rot) + Delta-Spalte, FadeInBehavior auf Runden. Phase 2 (Pomodoro + Alarm): Zyklus-Punkte (CycleDots), dynamische Ring-Farbe nach Phase (PhaseBrush), Streak-Anzeige (Tage in Folge), verbesserte Wochenstatistik-Balken (200px, Gradient, SemiBold), Nächster-Alarm-Countdown, runde Wochentag-Toggles (36x36, CSS-Classes), Card.Interactive + EmptyStateView für Alarm. Phase 3 (Übergreifend): Tab-Slide-Animation (translate + TransformOperationsTransition), alle Dialoge auf Bottom-Sheet umgebaut (VerticalAlignment=Bottom, CornerRadius=20,20,0,0, Drag-Handle). Phase 4 (Settings): Feedback+Privacy in Support-Card mit HeartOutline-Icon. Phase 5 (RESX): 8 neue Keys (TimerEmptyHint, CreateTimerButton, StopwatchEmptyHint, StreakDays, NextAlarmIn, NextAlarmCountdownFormat, NewAlarmButton, Support) in allen 6 Sprachen. Neue shared UI-Behaviors: TapScaleBehavior.cs + FadeInBehavior.cs in MeineApps.UI/Behaviors/.
- **13.02.2026 (8)**: RESX-Encoding-Fix + Pomodoro-Lokalisierung: Mojibake-Encoding in allen 5 nicht-englischen RESX-Dateien behoben (UTF-8 Bytes als Latin-1 interpretiert → ä/ö/ü/ß etc. korrigiert). ~30 fehlende RESX-Keys in allen 6 Sprachen ergänzt (Pomodoro: PomodoroTitle, Work, ShortBreak, LongBreak, StartWork, SkipPhase, SessionsCompleted, PomodoroConfig, WorkDuration, ShortBreakDuration, LongBreakDuration, CyclesBeforeLongBreak, AutoStartNext, CycleFormat, Statistics, NoSessionsYet, ThisWeek, TodaySessions, TodayMinutes; Timer: DeleteAllTimers, DeleteAllTimersConfirm, ExtendTimer, AutoRepeat, Presets, SaveAsPreset, PresetSaved, TimerDurationTooLong; Allgemein: On, Off). PomodoroView Config-Overlay: ScrollViewer + MaxHeight=500 für scrollbare Einstellungen, ToggleSwitch OnContent/OffContent Bindings. PomodoroViewModel: OnText/OffText Properties.
- **13.02.2026 (7)**: Android Zurück-Taste: Double-Back-Press zum Beenden (2s Fenster, Toast-Hinweis). Zurück-Navigation schließt Overlays (Timer-Create/Delete, Alarm-Edit/Delete/Pause, Pomodoro-Config/Statistics, Schichtplan) und navigiert zurück zum ersten Tab. Alarm-Overlay bewusst ausgenommen (Dismiss/Snooze erforderlich). MainViewModel.HandleBackPressed() + ExitHintRequested Event + MainActivity.OnBackPressed(). RESX-Key `PressBackAgainToExit` in 6 Sprachen.
- **13.02.2026 (6)**: Pomodoro-Tab Integration: MainViewModel um PomodoroViewModel Property + NavigateToPomodoro Command erweitert, Tab-Reihenfolge: Timer=0, Stoppuhr=1, Pomodoro=2, Alarm=3, Settings=4. MainView.axaml: 5-Spalten Tab-Bar mit PomodoroAccentColor (Rot), PomodoroView Content-Border. App.axaml.cs: PomodoroViewModel DI-Registrierung (Transient). DayStatistic.DayFontWeight Property (Bold für heute). FloatingText + Celebration Events von PomodoroVM in MainVM verdrahtet. UpdateLocalizedTexts() in OnLanguageChanged.
- **13.02.2026 (5)**: PomodoroViewModel: Vollständiges ViewModel mit konfigurierbaren Work/ShortBreak/LongBreak-Zeiten (Preferences-gespeichert), Zyklus-Tracking (CurrentCycle/CyclesBeforeLongBreak), Auto-Start nächste Phase, Phasenfarben (Rot/Grün/Blau), Focus-Statistiken (TodaySessions/TodayMinutes/WeekSessions + DayStatistic-Balkendiagramm mit HeightFraction), FocusSession DB-Speicherung bei Work-Abschluss, FloatingText + Celebration Events, UpdateLocalizedTexts(), IDisposable.
- **13.02.2026 (4)**: Shake-Challenge: IShakeDetectionService Interface + DesktopShakeDetectionService (Button-Simulation) + AndroidShakeDetectionService (Accelerometer, Schwellwert 12.0f, Cooldown 500ms). AlarmOverlayViewModel um Shake-Properties (IsShakeChallenge, ShakeProgress, ShakeTarget, ShakeProgressFraction) + SimulateShake-Command erweitert. AlarmOverlayView: Shake-Challenge-Block mit Fortschrittsanzeige + ProgressBar + Desktop-Fallback-Button. Math-Challenge IsVisible auf IsMathChallenge geändert (statt HasChallengeActive). DI-Registrierung in App.axaml.cs (Desktop) + MainActivity.cs (Android).
- **13.02.2026 (3)**: Alarm-Pause (Urlaubsmodus): IAlarmSchedulerService um PauseAllAlarmsAsync/ResumeAllAlarmsAsync/PausedUntil/IsAllPaused erweitert. AlarmSchedulerService speichert Pause in IPreferencesService, cancelt alle Notifications bei Pause, plant sie bei Resume neu. ShouldTrigger prüft IsAllPaused. OnCheckTimerTick hebt abgelaufene Pause automatisch auf. AlarmViewModel: Pause-Dialog mit WheelPicker (1-30 Tage), Status-Banner, Beach-FAB. AlarmView: Urlaubsmodus-Banner, Pause-Button, Pause-Dialog-Overlay.
- **13.02.2026 (2)**: Pomodoro-Grundlagen: PomodoroAccentColor/Brush in allen 4 Themes (Rot-Töne), FocusSession Model (SQLite-Tabelle für Statistiken), PomodoroPhase Enum (Work/ShortBreak/LongBreak), DB-Erweiterung (GetFocusSessionsAsync, SaveFocusSessionAsync).
- **13.02.2026**: Timer-Autorepeat + Presets: TimerItem.AutoRepeat (bool, DB-Feld), AutoRepeat-Logik in TimerService.OnUiTimerTick (Timer startet nach Ablauf automatisch neu, Overlay wird trotzdem angezeigt). TimerPreset Model (DB-Tabelle TimerPresets) mit Name/Duration/AutoRepeat. Preset-CRUD in IDatabaseService/DatabaseService. TimerViewModel: Preset-Commands (StartFromPreset, SaveAsPreset, DeletePreset), AutoRepeat-Toggle im Create-Dialog. TimerView: Preset-Chips zwischen Quick-Timer und Timer-Liste, Repeat-Icon neben Timer-Name bei aktivem AutoRepeat, "Als Vorlage speichern" Button im Create-Dialog.
- **12.02.2026 (4)**: Phase 4 - System-Ringtones & Sound-Picker: IAudioService erweitert (SystemSounds, PlayUriAsync, PickSoundAsync). Android: RingtoneManager lädt System-Sounds, RingtonePicker via ActivityResult in MainActivity. Desktop: FilePicker für .wav/.mp3/.ogg, Kopie in AppData. SoundItem mit optionaler Uri. Sound-Picker Button in AlarmView + SettingsView. AlarmActivity unterstützt benutzerdefinierte Sounds via Intent-Extra.
- **12.02.2026 (3)**: Phase 3 - UX-Optimierungen: (1) +1/+5 Min Extend-Buttons bei laufendem Timer, (2) Alle Timer löschen Button + Bestätigungsdialog, (3) Alarm-Challenge UI (Math-Aufgabe zum Aufwachen, Dismiss blockiert bis gelöst), (4) ShiftException-UI (Urlaub/Krank/Schichttausch pro Kalendertag), (5) Ansteigende Lautstärke bei Alarm (Volume Ramp). ~15 neue RESX-Keys in 6 Sprachen.
- **12.02.2026 (2)**: Phase 1+2 Bugfixes: (1) UriLauncher statt Process.Start, (2) StableHash statt GetHashCode, (3) Foreground-Check gegen Doppel-Alarm, (4) Snooze-Dauer aus Intent, (5) MessageRequested für TimerVM, (6) daysSinceStart Fix (36400), (7) SelectedGroup Validierung, (8) Hours-Label Fix, (9) CTS-Dispose Fixes, (10) WavGenerator/SoundDefinitions/TimeFormatHelper Code-Deduplizierung, (11) DayOfWeek-Mapping Helper.
- **12.02.2026**: Visual Redesign: Feature-Akzentfarben (Timer=Amber, Stoppuhr=Cyan, Alarm=Violet) in allen 4 Themes. TimerView mit CircularProgress-Ring + Pulsier-Animation + Status-Icons (Running/Paused/Finished). StopwatchView mit Glow-Ring-Animation bei laufender Messung. AlarmView Icons in Violet. MainView Tab-Bar mit feature-spezifischen Farben. FABs in Feature-Farben. TimerItem: ProgressFraction/IsPaused/IsFinished Properties.
- **11.02.2026 (3)**: Timer startet automatisch nach Erstellung (wie Quick-Timer)
- **11.02.2026 (2)**: Optimierungen: TimerService Recovery nach App-Kill (Running-Timer Ablauf prüfen, Paused-Timer validieren, Finished melden), TimerViewModel Validierung (Duration <= 0 → MessageRequested mit TimerDurationInvalid), AlarmSchedulerService Intervall 30s→10s (verhindert verpasste Alarme), AlarmViewModel SnoozeDuration-Validierung (min. 1 Min.) + ToggleAlarm Double-Tap-Guard, AlarmOverlayViewModel "min"-Text lokalisiert, 1 neuer RESX-Key (TimerDurationInvalid) in allen 6 Sprachen
- **11.02.2026**: Bugfix-Review: AlarmSchedulerService Thread-Safety (lock um _activeAlarms), TimerService Notification-Text lokalisiert, AlarmActivity Buttons lokalisiert (Dismiss/Snooze), AlarmOverlayViewModel von Transient zu Singleton
- **v2.0.0-notifications**: AlarmSchedulerService + TimerService mit INotificationService verbunden → Alarme/Timer funktionieren jetzt auch bei minimierter App (Android AlarmManager + AlarmReceiver + AlarmActivity). AlarmViewModel nutzt AlarmSchedulerService statt direkt DB.
- **v2.0.0-review**: DatabaseService SemaphoreSlim, StopwatchVM Dispatcher, AlarmScheduler Double-Trigger-Schutz, Delete-Bestaetigungen, Android Runtime Permissions
- **v2.0.0-gamejuice**: FloatingTextOverlay + CelebrationOverlay eingebaut
