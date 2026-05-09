# ZeitManager — Timer, Stoppuhr, Wecker

Fünf-Tab-Zeitverwaltungs-App: Multi-Timer, Stoppuhr mit Rundenzeiten, Pomodoro, Wecker mit
Challenges und Schichtplan-Rechner. Komplett werbefrei, kein Premium, kein MeineApps.Core.Premium.

| Aspekt | Wert |
|--------|------|
| Version | v2.0.7 |
| Package-ID | com.meineapps.zeitmanager |
| Modus | Geschlossener Test |
| Preis | Kostenlos (werbefrei) |
| Tabs | Timer, Stoppuhr, Pomodoro, Wecker/Schichtplan, Settings |

> Für Build-Befehle, Conventions, Troubleshooting und Packages → [Haupt-CLAUDE.md](../../../CLAUDE.md)

---

## Architektur

### Projekt-Struktur

```
src/Apps/ZeitManager/
├── ZeitManager.Shared/
│   ├── Audio/
│   │   ├── WavGenerator.cs          # WAV-Daten generieren (Frequenz + Dauer)
│   │   ├── SoundDefinitions.cs      # 6 eingebaute Töne mit Frequenz/Dauer
│   │   └── TimeFormatHelper.cs      # HH:MM:SS.cs Formatierung
│   ├── Graphics/                    # 6 SkiaSharp-Visualisierungen (siehe unten)
│   ├── Loading/
│   │   └── ZeitManagerLoadingPipeline.cs  # DB-Init + Shader-Kompilierung parallel
│   ├── Models/                      # AlarmItem, TimerItem, FocusSession, ShiftException
│   └── ViewModels/                  # TimerVM, StopwatchVM, AlarmVM, PomodoroVM, ShiftVM
├── ZeitManager.Android/
│   └── Services/                    # 7 Android-Services (siehe unten)
└── ZeitManager.Desktop/
```

### Loading-Pipeline

`ZeitManagerLoadingPipeline` führt echtes Preloading aus: DB-Init + Shader-Kompilierung parallel,
dann AlarmScheduler, dann ViewModel-Erstellung. `SkiaLoadingSplash` zeigt Fortschrittsring +
Statustext. `App.axaml.cs` setzt DataContext erst nach Pipeline-Abschluss — kein fire-and-forget
`_ = InitializeServicesAsync()`.

### Android-Services

```
ZeitManager.Android/Services/
├── TimerForegroundService.cs       # Foreground Service mit Notification (Timer-Countdown)
├── AlarmReceiver.cs                # BroadcastReceiver für Wecker-Auslösung
├── BootReceiver.cs                 # BOOT_COMPLETED → Wecker neu planen
├── AlarmActivity.cs                # Fullscreen Lockscreen-Alarm (Dismiss/Snooze, Gradual Volume)
├── AndroidAudioService.cs          # System-Ringtones via RingtoneManager + PlayUri + PickSound
├── AndroidNotificationService.cs   # NotificationChannels + AlarmManager + StableHash
└── AndroidShakeDetectionService.cs # Accelerometer-basierte Shake-Erkennung
```

**AndroidManifest Permissions:** FOREGROUND_SERVICE, SCHEDULE_EXACT_ALARM, RECEIVE_BOOT_COMPLETED,
POST_NOTIFICATIONS, VIBRATE, USE_FULL_SCREEN_INTENT, WAKE_LOCK

---

## Services

| Interface | Implementierung | Zweck |
|-----------|----------------|-------|
| `ITimerService` | `TimerService` | In-Memory Timer-Management + Snooze + AutoRepeat + Notifications + ExtendTimer + DeleteAll |
| `IAudioService` | `AudioService` / `AndroidAudioService` | Eingebaute Töne + System-Ringtones + PlayUriAsync + PickSoundAsync |
| `IAlarmSchedulerService` | `AlarmSchedulerService` | 60s Check-Timer, Weekday-Matching, Double-Trigger-Schutz + Notifications + Urlaubsmodus/PauseAll |
| `IShiftScheduleService` | `ShiftScheduleService` | 15/21-Schicht-Berechnung + Ausnahmen |
| `IShakeDetectionService` | `Desktop-` / `AndroidShakeDetectionService` | Shake-Challenge: Desktop = Button-Simulation, Android = Accelerometer |
| `INotificationService` | Plattform-spezifisch via `ConfigurePlatformServices` | Android: `AndroidNotificationService`, Desktop: `DesktopNotificationService` |
| `IHapticService` | `NoOpHapticService` / `AndroidHapticService` | HeavyClick bei Alarm-Dismiss + Timer-Ende, Click bei Snooze |

---

## Feature-Patterns

### Timer

- Mehrere Timer gleichzeitig, Quick-Timer (1/5/10/15/30 min), +1/+5 Min Extend, Alle löschen
- Snooze, AutoRepeat, Presets (DB-gespeichert), eingebaute + System-/benutzerdefinierte Töne
- `TimerView` nutzt `SkiaGradientRing` aus `MeineApps.UI` (Shared Control) mit `GlowEnabled` /
  `IsPulsing` bei laufendem Timer — kein `CircularProgress` pro Timer-Item

### Stoppuhr

- Rundenzeiten mit Best/Worst-Markierung + Delta, Undo-Funktion, Centisecond-Precision
- **Undo-Pattern:** `TimeSpan _offset` — `Stopwatch` unterstützt keine direkte Elapsed-Zuweisung,
  deshalb Offset-Akkumulation beim Undo statt direkter Zuweisung

### Wecker + Alarm-Overlay

- CRUD, Weekday-Toggles, Challenge-Support (Math + Shake mit UI), Tonauswahl
- Snooze mit konfigurierbarer Dauer, ansteigende Lautstärke, Urlaubsmodus (WheelPicker 1-30 Tage)
- **Fullscreen Alarm-Overlay:** Content-Swap statt ZIndex-Overlay. Normaler Content + Tab-Bar
  werden per `IsVisible="{Binding !IsAlarmOverlayVisible}"` versteckt, Alarm-Content als Ersatz
  angezeigt. Grund: Avalonia ZIndex Hit-Testing funktioniert nicht auf Android
- Pulsier-Animation: nur Opacity in KeyFrames — kein ScaleTransform (crasht auf Android-GPU)
- `AlarmActivity`: Dedizierte Android Activity (`ShowWhenLocked`, `TurnScreenOn`) für
  Fullscreen-Alarm über Lockscreen. Buttons (Dismiss/Snooze) lokalisiert via
  `App.Services.GetService<ILocalizationService>()`. Unterstützt `alarm_tone` + `snooze_duration`
  als Intent-Extras
- **StableHash:** Deterministische Hash-Funktion für Alarm-IDs statt `GetHashCode()` (nicht
  deterministisch). Verwendet in `AndroidNotificationService`, `AlarmActivity`
- **Foreground-Check:** `MainActivity.IsAppInForeground` statisches Flag. `AlarmReceiver` prüft
  dies um Doppel-Auslösung (AlarmActivity + In-App Overlay) zu vermeiden

### Pomodoro

- Konfigurierbare Zeiten (Work/ShortBreak/LongBreak), Zyklus-Tracking (CycleDots), Auto-Start
- Phasen-Ringfarbe (PhaseBrush), Streak-Anzeige (Tage in Folge mit Gold-Shimmer bei Streak > 3)
- Focus-Statistiken: Heute + Woche als Balkendiagramm (`DayStatistic`), Monats-Heatmap
- `FocusSession` DB-Persistierung, Celebration + FloatingText bei Session-Abschluss
- Config-Dialog (Bottom-Sheet, Preferences-gespeichert): Aufgabenname pro Session,
  konfigurierbares Tagesziel (1-20 Sessions)

### Schichtplan

- 15-Schicht (3 Gruppen Mo-Fr) + 21-Schicht (5 Gruppen 24/7)
- Kalender-Ansicht, Ausnahmen (Urlaub/Krank/Schichttausch)
- `CustomShiftPattern.ShortName()` nutzt `LocalizationManager.GetString()` für lokalisierte Kürzel

### Sound-System

- `IAudioService` mit `SystemSounds`, `PlayUriAsync`, `PickSoundAsync`
- Android: RingtoneManager für System-Sounds + `RingtoneManager.ActionRingtonePicker` für Auswahl
  (ActivityResult via MainActivity)
- Desktop: Avalonia `StorageProvider.OpenFilePickerAsync` + Kopie in AppData
- `SoundItem.Uri` ist nullable — null = eingebauter Ton

---

## SkiaSharp-Visualisierungen

6 Visualisierungen in `ZeitManager.Shared/Graphics/`:

| Datei | Beschreibung | Genutzt in |
|-------|-------------|------------|
| `ClockworkBackgroundRenderer.cs` | Animierter "Warm Clockwork"-Hintergrund (5 Layer): 3-Farben-Gradient, konzentrische Uhrenringe, Glühwürmchen-Partikel (Amber, Glow via MaskFilter), 60 Tick-Markierungen, radiale Vignette. Struct-Pool (max 12), gecachte Paints/Shader, ~5 fps | MainView |
| `StopwatchVisualization.cs` | Stoppuhr-Ring mit Sekundenzeiger + Nachleucht-Trail (6 Ghost-Positionen), Runden-Sektoren (farbige Bögen, 8 Farben), Sub-Dial (Minuten-Ring oben rechts), 60 Sekunden-Ticks, Rundenpunkte | StopwatchView |
| `PomodoroVisualization.cs` | Fortschrittsring mit Pulsier-Effekt (2 Hz) auf aktivem Zyklus-Segment + Glow, innerer Session-Ring (Tages-Fortschritt als Segment-Bögen); Wochen-Balkendiagramm | PomodoroView |
| `TimerVisualization.cs` | Flüssigkeits-Füllung + Welleneffekt, Tropfen-Partikel (8 Stück), Countdown-Ziffern (letzte 5 s, Scale-Bounce 1,5→1,0), Ablauf-Burst (20 Confetti-Partikel bei Timer=0) | TimerView (Reserve) |
| `PomodoroStatisticsVisualization.cs` | Monats-Heatmap (GitHub-Contributions-Stil): 7×5 Grid, 5 Intensitätsstufen, Wochentag-Labels, Heute-Highlight, HitTest für Tap-Interaktion | PomodoroView (Statistik) |
| `ZeitManagerSplashRenderer.cs` | Analoge Uhr mit Snap-Tick Sekundenzeiger, kreisförmiger Progress-Ring, 12 rotierende Zahnrad-Partikel, konzentrische Deko-Ringe | Splash-Screen |

---

## Game Juice

- `FloatingTextOverlay`: Stoppuhr-Runden, Timer fertig
- `CelebrationOverlay`: Confetti bei Timer-Ende + Pomodoro-Session-Abschluss
- `TapScaleBehavior`: Quick-Timer-Buttons
- `FadeInBehavior`: Stoppuhr-Runden
- `StaggerFadeInBehavior`: Timer-/Alarm-Listen
- `CountUpBehavior`: Pomodoro-Statistiken
- `SwipeToRevealBehavior`: Alarm Swipe-to-Delete
- `BounceEaseOut`: Dialog-Spring-Animations (Timer/Alarm/Pomodoro Bottom-Sheets)
- `EmptyPulse`: Empty-State-Icons
- Onboarding: `TooltipBubble`, 2 Schritte
- Streak Gold-Shimmer: `#FFD700` + `StreakGoldPulse` bei Streak > 3
- Wochen-Balken-Einfahranimation: CubicEaseOut, ~500 ms

---

## Kritische Architektur-Entscheidungen

### Thread-Safety

`System.Timers.Timer` feuert auf ThreadPool → `Dispatcher.UIThread.Post()` für Property-Updates.
`TimerService` und `AlarmSchedulerService` nutzen `lock(_lock)` für List-Zugriffe. `AudioService`
lock-swap für CTS. `DesktopNotificationService` nutzt `ConcurrentDictionary`.

### AlarmItem-Reaktivität

`AlarmItem` erbt `ObservableObject`. `IsEnabled` nutzt `SetProperty()` für UI-Notification —
kein manuelles `PropertyChanged?.Invoke()`.

---

## Abhängigkeiten

- `MeineApps.Core.Ava`, `MeineApps.UI`
- `sqlite-net-pcl` + `SQLitePCLRaw.bundle_green`
- `SkiaSharp` + `Avalonia.Labs.Controls`
- **Kein `MeineApps.Core.Premium` — komplett werbefrei**
