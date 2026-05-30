# WorkTimePro — Arbeitszeiterfassung & Export

Vollständige Arbeitszeiterfassung mit Check-in/out, Pausen, Urlaub, Schichtplanung,
Feiertagen (DE/AT/CH), Statistiken (11 eigene + 3 geteilte SkiaSharp-Visualisierungen)
und Export (PDF/Excel/CSV/ICS).

> Für Build-Befehle, Conventions, Architektur → [Haupt-CLAUDE.md](../../../CLAUDE.md)

| Aspekt | Wert |
|--------|------|
| Package-ID | `com.meineapps.worktimepro` |
| Plattformen | Android + Desktop |

---

## Build & Zielframework

| Projekt | Framework | Befehl |
|---------|-----------|--------|
| `WorkTimePro.Shared` | `net10.0` | `dotnet build src/Apps/WorkTimePro/WorkTimePro.Shared` |
| `WorkTimePro.Desktop` | `net10.0` | `dotnet run --project src/Apps/WorkTimePro/WorkTimePro.Desktop` |
| `WorkTimePro.Android` | `net10.0-android` | `dotnet build src/Apps/WorkTimePro/WorkTimePro.Android` |

Release-AAB: `dotnet publish src/Apps/WorkTimePro/WorkTimePro.Android -c Release`

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `WorkTimePro.Shared/ViewModels/` | `WorkTimePro.ViewModels` |
| `WorkTimePro.Shared/Views/` | `WorkTimePro.Views` |
| `WorkTimePro.Shared/Services/` | `WorkTimePro.Services` |
| `WorkTimePro.Shared/Models/` | `WorkTimePro.Models` |
| `WorkTimePro.Shared/Graphics/` | `WorkTimePro.Graphics` |
| `WorkTimePro.Shared/Helpers/` | `WorkTimePro.Helpers` |
| `WorkTimePro.Shared/Loading/` | `WorkTimePro.Loading` |

---

## Architektur

### Projekt-Struktur

```
src/Apps/WorkTimePro/
├── WorkTimePro.Shared/        # ViewModels, Views, Services, Models, Graphics
├── WorkTimePro.Android/       # MainActivity + Platform-Service-Factories
└── WorkTimePro.Desktop/       # Program.cs (Desktop-Shell)
```

### DI-Registrierung (App.axaml.cs)

Alle Services als Singleton. ViewModels: alle als **Singleton** registriert (MainViewModel
und alle Child-VMs). Platform-Services über Factory-Pattern:

| Factory-Property | Plattform | Zweck |
|-----------------|-----------|-------|
| `FileShareServiceFactory` | Android | `IFileShareService` via FileProvider |
| `RewardedAdServiceFactory` | Android | `AndroidRewardedAdService` |
| `PurchaseServiceFactory` | Android | `AndroidPurchaseService` (Billing v8) |
| `NotificationServiceFactory` | beide | `AndroidNotificationService` / `DesktopNotificationService` |
| `HapticServiceFactory` | Android | `AndroidHapticService` (NoOp auf Desktop) |

Factories werden in `MainActivity.CustomizeAppBuilder` BEVOR DI-Build gesetzt —
dasselbe Pattern wie in allen anderen Apps.

### Loading-Pipeline

`WorkTimeProLoadingPipeline` (in `Loading/`) läuft beim Startup:
1. DB-Init + Shader-Kompilierung (parallel)
2. Reminder-Setup
3. ViewModel-Erstellung

`SkiaLoadingSplash` zeigt Fortschrittsring + Statustext. DataContext wird in
`App.axaml.cs` erst nach Pipeline-Abschluss gesetzt — verhindert Race im `InitializeAsync`.

### ViewModel-Initialisierung

`MainViewModel`-Konstruktor speichert `_initTask = InitializeAsync()`. Alle Commands und
`ToggleTrackingAsync` awaiten `EnsureInitializedAsync()` — verhindert Race bei schnellem
Tap nach App-Start.

Child-VMs werden lazy geladen: Calendar-VM lädt keine Daten im Konstruktor — erst beim
Tab-Wechsel (`MainViewModel.LoadTabDataAsync`).

---

## Services

| Interface | Implementierung | Zweck |
|-----------|----------------|-------|
| `IDatabaseService` | `DatabaseService` | SQLite (WAL, alle Tabellen, Indizes) |
| `ICalculationService` | `CalculationService` | Arbeitszeit, Auto-Pause, Saldo, §3 ArbZG |
| `ITimeTrackingService` | `TimeTrackingService` | Check-in/out, Pause, `LiveDataSnapshot` |
| `IExportService` | `ExportService` | PDF (PdfSharpCore), Excel (ClosedXML), CSV |
| `ICalendarExportService` | `CalendarExportService` | ICS (RFC 5545, Google/Apple/Outlook) |
| `IVacationService` | `VacationService` | 9 Status-Typen, Resturlaub, Übertrag |
| `IHolidayService` | `HolidayService` | DE (16 BL), AT (9 BL), CH (12 Kantone) |
| `IProjectService` | `ProjectService` | Projekte (CRUD + Stunden-Aggregation) |
| `IShiftService` | `ShiftService` | Schichtplanung (wiederkehrende Muster) |
| `IEmployerService` | `EmployerService` | Arbeitgeber (Default-Employer, Stunden) |
| `IBackupService` | `BackupService` | JSON-Backup/Restore mit Safety-Backup |
| `INotificationService` | plattform-spezifisch | Toast/notify-send bzw. NotificationChannel |
| `IReminderService` | `ReminderService` | 5 Reminder-Typen, subscribed auf `StatusChanged` |
| `IHapticService` | plattform-spezifisch | Vibration (Android: Click/HeavyClick) |

---

## Models & Datenbank

### Tabellen (SQLite, WAL-Modus)

| Tabelle | Modell | Besonderheit |
|---------|--------|-------------|
| `WorkDays` | `WorkDay` | UNIQUE-Index auf `Date` |
| `TimeEntries` | `TimeEntry` | UNIQUE auf `(WorkDayId, Timestamp, Type)` (Anti-Duplikat) |
| `PauseEntries` | `PauseEntry` | FK auf `WorkDayId`, Index |
| `VacationEntries` | `VacationEntry` | FK auf `Year` |
| `VacationQuotas` | `VacationQuota` | Pro Jahr + optionaler EmployerId |
| `Holidays` | `HolidayEntry` | Region-basiert (gespeichert je Kombination Jahr+Region) |
| `Projects` | `Project` | Soft-Delete (IsActive) |
| `Employers` | `Employer` | Default-Flag, SetDefaultAsync via 2× SQL-UPDATE |
| `ShiftPatterns` | `ShiftPattern` | Wiederkehrendes Muster |
| `ShiftAssignments` | `ShiftAssignment` | Einzelne Tages-Zuweisung, Index auf Date |
| `WorkSettings` | `WorkSettings` | Singleton-Zeile (FirstOrDefault + Insert wenn leer) |

### Enums

| Enum | Werte |
|------|-------|
| `DayStatus` | WorkDay, Weekend, Vacation, Holiday, Sick, UnpaidLeave, HomeOffice, BusinessTrip, OvertimeCompensation, SpecialLeave, Training, CompensatoryTime (12 Typen) |
| `TrackingStatus` | Idle, Working, OnBreak |
| `EntryType` | CheckIn, CheckOut |
| `PauseType` | Manual, Auto (Auto = gesetzlich ergänzt) |
| `ShiftType` | Early, Late, Night, Normal, Flexible, Off |
| `ExportFormat` | PDF, CSV, Excel |
| `StatisticsPeriod` | Week, Month, Quarter, Year, Custom |
| `CloudProvider` | None, GoogleDrive, OneDrive (in WorkSettings, noch nicht aktiv) |

### DateTime-Konvention (WorkTimePro-spezifisch)

- **Arbeitszeiten** (Check-in/out, Pausen): `DateTime.Now` (Ortszeit — menschenlesbar im UI)
- **Audit-Timestamps** (CreatedAt/ModifiedAt): `DateTime.UtcNow`
- **Export-Footer + Backup-Dateinamen**: Ortszeit (menschenlesbar)
- Persistenz-Format: ISO 8601 `"O"` + `DateTimeStyles.RoundtripKind` beim Parse

---

## Features & Patterns

### Zeiterfassung (Core-Loop)

```
CheckIn → TimeEntry(CheckIn) → WorkDay.Status=Working
    ↓
PauseStart → PauseEntry(Start) → WorkDay.Status=OnBreak
PauseEnd   → PauseEntry(End)  → WorkDay.Status=Working
    ↓
CheckOut → TimeEntry(CheckOut) → CalculationService.RecalculateWorkDay()
```

`ITimeTrackingService.GetLiveDataSnapshotAsync()` liefert WorkTime, PauseTime,
TimeUntilEnd in einem Aufruf (3 DB-Queries statt 5+) — verhindert Query-Sturm im 1s-Timer.

### Überstunden-Berechnung

`CalculationService` berechnet:
- **Netto-Arbeitszeit** = Gesamt − Pausen − Auto-Pause-Ergänzung (wenn < Gesetzlich)
- **Saldo** = Netto − Soll (aus `WorkSettings.GetHoursForDay()` — Caching mit String-Vergleich)
- **§3 ArbZG Compliance**: 6-Monats-Durchschnitt ≤ 8h/Tag über Mo–Sa (Sonntage ausgeschlossen), Vacation/Sick zählen als 0h, Mindest-Schwelle 60 Werktage

### Settings-Pattern

`SettingsViewModel` speichert per Debounce-Timer (800ms, `ScheduleAutoSave`). Kein
Speichern-Button. `_isInitializing` Flag verhindert Speichern während `LoadDataAsync`.

`SettingsChanged` ist `EventHandler<bool>` — Bool signalisiert ob Tab-Reload nötig ist.
Kosmetische Änderungen (Reminder-Zeiten, Stundenlohn) reloaden den Tab nicht mehr.

`_cachedSettings` in `MainViewModel`: wird in `LoadDataAsync` und `OnSettingsChanged`
aktualisiert. `UpdateLiveDataAsync` (1s-Timer) nutzt Cache statt DB-Query.

### Overlay-Pattern (Kalender, DayDetail, Statistics)

```csharp
public bool IsAnyOverlayVisible => IsEditOverlayVisible || IsConfirmDeleteVisible;
partial void OnIsEditOverlayVisibleChanged(bool value) => OnPropertyChanged(nameof(IsAnyOverlayVisible));
```

ScrollViewer mit `IsHitTestVisible="{Binding !IsAnyOverlayVisible}"` — verhindert Touch-
Durch-Fall bei ZIndex-Overlays. Betrifft: `DayDetailView`, `StatisticsView`,
`VacationView`, `YearOverviewView`.

### Undo CheckIn/CheckOut

5 Sekunden nach CheckIn/CheckOut: Undo-Button + Ctrl+Z Shortcut. `_lastUndoEntry`
speichert den Eintrag. `UndoLastActionAsync` löscht, berechnet WorkDay neu, lädt Status.

### Export-Logik

- **PDF**: PdfSharpCore
- **Excel**: ClosedXML (XLSX)
- **CSV**: Eigene Implementierung
- **ICS**: RFC 5545 — Arbeitstage als zeitgebundene Events, Urlaub/etc. als ganztägige Events, importierbar in Google/Apple/Outlook Calendar
- `GetTimeEntriesForWorkDaysAsync(List<int>)`: Batch-Query für Export (verhindert N+1)
- Android-Share: FileProvider `com.meineapps.worktimepro.fileprovider` via `IFileShareService`

### Backup-Service

- `CreateLocalBackupAsync()` → JSON in `Backups/`-Ordner (kein Cloud-Auth)
- `ExportBackupAsync()` → Backup + Share-Sheet via `IFileShareService`
- `ImportBackupFromFileAsync()` → Safety-Backup VOR Restore, JSON-Roundtrip für Deep-Clone
- Bei Fehler: automatischer Rollback auf Safety-Backup
- `BulkRestoreAsync()` → Batch-Insert in einer Transaction (5–10× schneller als einzelne Saves)
- `DateTime.TryParse` mit `CultureInfo.InvariantCulture` (nicht `null` = CurrentCulture)

### Smart Notifications (2-Schichten)

- **`INotificationService`**: Plattform-abstrakt
  - Desktop: PowerShell-Toast (Injection-sicher via Base64-EncodedCommand) / `notify-send`
  - Android: `worktimepro_reminder` NotificationChannel + `ReminderReceiver` BroadcastReceiver + `SetExactAndAllowWhileIdle`
- **`IReminderService`**: Orchestriert 5 Typen — Morgen, Abend, Pause, Überstunden, Wochenzusammenfassung. Subscribed auf `ITimeTrackingService.StatusChanged`. `RescheduleAsync()` bei Settings-Änderungen.
- Permissions Android: `POST_NOTIFICATIONS`, `SCHEDULE_EXACT_ALARM`

---

## Premium-Modell & Rewarded Ads

**Preise**: 3,99 EUR/Monat oder 19,99 EUR Lifetime
**Trial**: 7 Tage (TrialService, danach Soft Paywall)
**Features**: Werbefrei + Export (PDF, Excel, CSV)
**Rewarded Ads**: Video ODER Premium-Kauf als Gate

| Placement | Placement-ID | Zugang |
|-----------|-------------|--------|
| Urlaubseintrag, Quota, Übertrag | `vacation_entry` | Einmalig |
| PDF-Export | `export` | Einmalig |
| Statistik-Export | `monthly_stats` | Einmalig |
| Quartal/Jahr-Statistik | `monthly_stats` | 24h (Preference-Key `"ExtendedStatsExpiry"`, UTC + RoundtripKind) |

---

## ViewModels & Views

| ViewModel | View | Daten-Laden |
|-----------|------|-------------|
| `MainViewModel` | `MainView` | Sofort + 1s-Timer (LiveData) |
| `WeekOverviewViewModel` | `WeekOverviewView` | Tab-Wechsel |
| `CalendarViewModel` | `CalendarView` | Tab-Wechsel (Lazy, kein Ctor-Load) |
| `StatisticsViewModel` | `StatisticsView` | Tab-Wechsel |
| `SettingsViewModel` | `SettingsView` | Tab-Wechsel |
| `DayDetailViewModel` | `DayDetailView` | `NavigateCommand(date)` |
| `MonthOverviewViewModel` | `MonthOverviewView` | `NavigateToMonthCommand` |
| `YearOverviewViewModel` | `YearOverviewView` | `NavigateToYearCommand` |
| `VacationViewModel` | `VacationView` | `NavigateToVacationCommand` |
| `ShiftPlanViewModel` | `ShiftPlanView` | `NavigateToShiftPlanCommand` |

Navigation: Event-basiert (`NavigationRequested`), kein Avalonia Shell.

---

## SkiaSharp-Visualisierungen

| Datei | Zweck |
|-------|-------|
| `DayTimelineVisualization` | 24h-Timeline: Arbeits-/Pausen-Blöcke, Stundenticks, Jetzt-Markierung |
| `WeekBarVisualization` | 7-Tage-Balken, Ist/Soll-Vergleich |
| `OvertimeSplineVisualization` | Überstunden-Trend: Tagesbalken (grün/rot) + kumulative Spline-Kurve |
| `WeekdayRadialVisualization` | Radiales Balkendiagramm Mo–So, gestrichelte Soll-Linie |
| `WeeklyWorkChartVisualization` | Wöchentliche Arbeitsstunden + Soll-Linie |
| `MonthlyBarChartVisualization` | Monatsbalken + kumulative Saldo-Kurve |
| `VacationQuotaGaugeVisualization` | 3 konzentrische Ringe, Farbe grün→gelb→rot nach Verbrauch |
| `StatsSummaryGaugeVisualization` | 4 Halbkreis-Gauges (Arbeitszeit/Überstunden/Schnitt/Quote) |
| `MonthWeekProgressVisualization` | Alle Wochen eines Monats als Gradient-Balken in einem Canvas |
| `WorkTimeProSplashRenderer` | "Die Stechuhr": Stempelzyklus (3s) + 10 Business-Partikel |
| `WorkspaceBackgroundRenderer` | "Professional Dashboard": 5-Layer animierter Hintergrund (~5fps, 0 GC/Frame) |

Geteilte Controls aus `MeineApps.UI`:
- `LinearProgressVisualization` (Gradient-Fortschrittsbalken mit Glow + Prozent, in `WeekOverviewView`)
- `DonutChartVisualization` (Pausen, Projekte, Arbeitgeber)
- `SkiaGradientRing` (Tagesfortschritt-Ring auf TodayView)

### TimeBlock-Cache (TodayView)

Geschlossene Segmente werden nur bei strukturellen Änderungen (Entries/Pauses/Status)
gecacht. Offene Segmente (laufender CheckIn, aktive Pause) werden pro Frame aus den
gespeicherten Start-Timestamps + `DateTime.Now` zusammengesetzt — verhindert LINQ-
Aufruf pro Sekunde.

---

## UI-Conventions

### CircularProgressControl

Custom Avalonia Control (`Controls/CircularProgressControl.cs`): kreisförmiger Fortschrittsring
via StreamGeometry. Properties: `Progress` (0–100), `TrackBrush`, `ProgressBrush`,
`StrokeWidth`, `IsPulsing`.

**WICHTIG**: Property heißt `IsPulsing` (NICHT `IsAnimating`) — `AvaloniaObject.IsAnimating()`
ist eine Methode und würde kollidieren. Jede Umbenennung hier bricht alle XAML-Bindings.

### AppColors

Statische Klasse (`AppColors.cs`) mit Farbkonstanten:
`StatusIdle`, `StatusActive`, `StatusPaused`, `BalancePositive`, `BalanceNegative`.
Ersetzt Magic-Strings in allen ViewModels und Models (WorkDay, WorkMonth, WorkWeek).

### StatusIconKind

Alle Views nutzen `StatusIconKind` (MaterialIconKind). Die veraltete `StatusIcon`-Property
(MDI-Glyph-String) ist entfernt. Kalender-`CalendarDay` exponiert `StatusIconKind` für
visuelle Darstellung.

### Compiled Bindings

`x:CompileBindings="True"` + `x:DataType="vm:XxxViewModel"` auf jeder View-Root.
`VacationView`-ComboBox DataTemplate: `x:DataType="vm:VacationTypeItem"` (sonst Reflection).

### Keyboard Shortcuts (Desktop)

`MainView.axaml.cs` `OnKeyDown`: F5 = Refresh, 1–5 = Tabs, Escape = Sub-Page schließen,
Ctrl+Z = Undo. Shortcuts NICHT im ViewModel — keine Commands, nur Key-Down-Handler im
Code-Behind (ist akzeptabel für globale Keyboard-Navigation).

### Back-Button (Android)

`HandleBackPressed()` in MainViewModel:
1. Offene Overlays / Dialoge schließen
2. Sub-Navigation zurück
3. Double-Back-to-Exit via `BackPressHelper` → `FloatingTextRequested` (statt `ExitHintRequested`)

---

## Bekannte Gotchas

### OvertimeWarningHours vs. MaxDailyHours

`ReminderService.StartOvertimeTimer` nutzt `settings.OvertimeWarningHours` (double),
NICHT `settings.MaxDailyHours` (int). Das RESX-Label war "Stunden/Woche" aber der Timer
ist tagesbasiert — RESX-Labels in allen 6 Sprachen korrigiert ("Stunden/Tag").

### SaveSettings fire-and-forget

`ScheduleAutoSave`-Lambda muss `async` sein damit `SaveSettingsAsync` wirklich awaited
wird. Ohne `async` wird der Task verworfen, Exceptions verschwinden stillschweigend.

### ProjectService: GetProjectHoursAsync

Aggregiert aus `TimeEntry.ProjectId` (CheckIn/CheckOut-Paare). Die alte
`ProjectTimeEntry`-Tabelle ist entfernt. NIEMALS wieder die Tabelle einführen —
Stunden-Tracking läuft über TimeEntry.

### BackupService Deep-Clone

`CreateBackupDataAsync` klont per JSON-Roundtrip (entkoppelt vom DB-Tracking).
`DateTime.TryParse` mit `CultureInfo.InvariantCulture` — NICHT `null` (= CurrentCulture
je nach Gerät unterschiedlich).

### SelectLanguage CommandParameter

CommandParameter ist Sprachcode (String: "de"/"en"/...), KEIN Integer-Index.
`RelayCommand<string>`, NICHT `RelayCommand<int>` — XAML `CommandParameter="de"` ist
immer String (siehe Core.Ava-CLAUDE.md, Framework-Fallstricke).

### Tab-Reload vs. Tab-Lazy-Load

`MainViewModel.OnCurrentTabChanged` lädt Daten für den neuen Tab automatisch
(`LoadTabDataAsync`). CalendarVM hat KEINEN `_ = LoadDataAsync()` im Konstruktor mehr —
wurde entfernt weil doppelt geladen wurde. Alle Navigate-Commands zu Sub-Seiten
(DayDetail, Month, Year, Vacation, ShiftPlan) sind `async` und awaiten `LoadDataAsync`.

### LockMonth / UnlockMonth N+1

`LockMonthAsync` / `UnlockMonthAsync` nutzen je 1 SQL-UPDATE statt N WorkDays laden +
einzeln updaten. `SetDefaultEmployerAsync` nutzt 2 SQL-UPDATEs. Nie wieder auf
"lade alle, iterate, save each" zurückfallen.

### WorkSettings Caching

`WorkSettings.WorkDaysArray` ist gecacht (String-Vergleich, NICHT auf Property-Zugriff
neu parsen). `WorkSettings.GetHoursForDay()` cached deserialisiertes JSON-Dictionary —
`SetHoursForDay()` aktualisiert den Cache mit. Direktes Parsen bei jedem 1s-Timer-Tick
würde messbar CPU verbrauchen.

---

## Game Juice

- **FloatingText**: "Feierabend!" bei CheckOut + Überstunden-Betrag ("+X.Xh")
- **Celebration**: Confetti bei CheckOut wenn Wochenziel erreicht (`WeekProgress >= 100%`)
- **Earnings-Ticker**: CountUp-Animation (800ms EaseOut) bei Änderung ≥ 0,10 EUR
- **Balance-Glow**: Pulsierende Opacity auf Balance-TextBlock bei negativer Balance (2s Zyklus)
- **Fortschrittsring**: Puls-Animation (IsPulsing) während aktivem Tracking

---

## Build / Test / Deploy

```bash
# Shared + Desktop bauen
dotnet build "F:\Meine_Apps_Ava\src\Apps\WorkTimePro\WorkTimePro.Shared"
dotnet run --project "F:\Meine_Apps_Ava\src\Apps\WorkTimePro\WorkTimePro.Desktop"

# Android AAB (nur auf explizite Anfrage!)
dotnet publish "F:\Meine_Apps_Ava\src\Apps\WorkTimePro\WorkTimePro.Android" -c Release

# AppChecker
dotnet run --project "F:\Meine_Apps_Ava\tools\AppChecker" WorkTimePro
```

**Vor Android-Release**: `ApplicationVersion` + `ApplicationDisplayVersion` in
`WorkTimePro.Android.csproj` hochsetzen.

---

## Wichtige Dateien

| Datei | Zweck |
|-------|-------|
| `WorkTimePro.Shared/Models/Enums.cs` | Alle App-Enums (DayStatus, TrackingStatus, ...) |
| `WorkTimePro.Shared/AppColors.cs` | Farbkonstanten (ersetzt Magic-Strings) |
| `WorkTimePro.Shared/Helpers/TimeFormatter.cs` | `FormatMinutes()`, `FormatBalance()`, `GetStatusName()` |
| `WorkTimePro.Shared/Controls/CircularProgressControl.cs` | Fortschrittsring-Control |
| `WorkTimePro.Shared/Loading/WorkTimeProLoadingPipeline.cs` | Startup-Sequenz |
| `WorkTimePro.Android/Services/ReminderReceiver.cs` | Android BroadcastReceiver für Notifications |
| `Releases/WorkTimePro/CHANGELOG_*.md` | Release-Notes |

---

## Verweise

- [Haupt-CLAUDE.md](../../../CLAUDE.md) — Build, Conventions, Architektur
- [MeineApps.Core.Ava/CLAUDE.md](../../Libraries/MeineApps.Core.Ava/CLAUDE.md) — Preferences, BackPressHelper, ViewLocator
- [MeineApps.Core.Premium.Ava/CLAUDE.md](../../Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md) — AdMob + Google Play Billing
- [MeineApps.UI/CLAUDE.md](../../UI/MeineApps.UI/CLAUDE.md) — Custom Controls, SkiaSharp-Helpers
