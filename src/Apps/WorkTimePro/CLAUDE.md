# WorkTimePro (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Zeiterfassung & Arbeitszeitmanagement mit Pausen, Kalender-Heatmap, Statistiken, Export, Urlaubsverwaltung, Feiertage (16 Bundeslaender), Projekten, Arbeitgebern und Schichtplanung.

**Version:** 2.0.6 | **Package-ID:** com.meineapps.worktimepro | **Status:** Geschlossener Test

## Premium-Modell

- **Preis**: 3,99 EUR/Monat oder 19,99 EUR Lifetime
- **Features**: Werbefrei + Export (PDF, Excel, CSV)
- **Trial**: 7 Tage (TrialService)
- **Rewarded Ads**: Soft Paywall (Video ODER Premium)

## Features

### Kern-Features
- **Zeiterfassung**: Check-in/out mit Pausen-Management, Auto-Pause
- **Kalender-Heatmap**: Monatsuebersicht mit Status-Overlay (Urlaub, Krank, HomeOffice etc.)
- **Statistiken**: Charts (SkiaSharp) + Tabelle, Taeglich/Woechentlich/Monatlich/Quartal/Jahr
- **Export**: PDF, Excel (XLSX), CSV via PdfSharpCore + ClosedXML
- **Urlaubsverwaltung**: 9 Status-Typen, Resturlaub, Uebertrag, Urlaubsanspruch
- **Feiertage**: 16 deutsche Bundeslaender
- **Schichtplanung**: Wiederkehrende Muster mit Tagesnamen-Lokalisierung
- **Projekte + Arbeitgeber**: CRUD mit Zuweisung zu Zeiteintraegen
- **Smart Notifications**: 5 Reminder-Typen (Morgen/Abend/Pause/Überstunden/Wochenzusammenfassung), plattformübergreifend
- **Zeitrundung**: 5/10/15/30 Minuten-Rundung der Arbeitszeit (Settings)
- **Stundenlohn**: Verdienst-Berechnung mit Anzeige auf TodayView
- **Fortschrittsring**: Kreisförmiger Tages-Fortschritt um den Start/Stop-Button mit Puls-Animation (IsPulsing)
- **Haptic Feedback**: Vibration bei CheckIn/CheckOut/Pause (Android)
- **Streak-Anzeige**: Feuer-Icon + aufeinanderfolgende Arbeitstage (>=2) auf TodayView
- **Wochenziel-Celebration**: Confetti + FloatingText wenn WeekProgress >= 100%
- **Tab-Indikator**: Animierter farbiger Balken unter aktivem Tab (TransformOperationsTransition)
- **Tab-Highlighting**: Aktiver Tab-Icon+Label in PrimaryBrush, Rest TextSecondaryBrush
- **Achievement/Badge-System**: 10 Achievements (Stunden, Streaks, Perfekte Woche, Frühaufsteher, Überstundenkönig), DB-persistiert, Celebration bei Unlock
- **Predictive Insights**: Wochenziel-Restzeit und Monatstrend auf WeekOverviewView (Insight-Card)
- **Earnings-Ticker**: CountUp-Animation auf TodayView (800ms EaseOut) bei Earnings-Änderung
- **Balance-Glow**: Pulsierende Opacity-Animation auf Balance-TextBlock bei negativer Balance (2s Zyklus)

### ViewModels & Views (12 VMs, 14 Views)
MainViewModel, WeekOverview, Calendar, Statistics, Settings, DayDetail, MonthOverview, YearOverview, Vacation, ShiftPlan, Achievement

## App-spezifische Services

| Service | Zweck |
|---------|-------|
| IDatabaseService | SQLite (TimeEntry, WorkDay, PauseEntry, VacationEntry etc.) |
| ICalculationService | Arbeitszeit-Berechnung, Auto-Pause, Saldo |
| ITimeTrackingService | Check-in/out Logik, Pausen-Management |
| IExportService | PDF/Excel/CSV Export + FileShare |
| IVacationService | Urlaubsverwaltung + 9 Status-Typen |
| IHolidayService | Feiertagsberechnung (16 Bundeslaender) |
| IProjectService | Projekt-Verwaltung |
| IShiftService | Schichtplanung |
| IEmployerService | Arbeitgeber-Verwaltung |
| IAchievementService | Achievement/Badge-System (10 Achievements, DB-Persistenz, Unlock-Events) |
| ICalendarSyncService | Kalender-Export (ICS) |
| IBackupService | Backup/Restore |
| INotificationService | Plattform-Notifications (Desktop: Toast/notify-send, Android: NotificationChannel + AlarmManager) |
| IReminderService | 5 Reminder-Typen: Morgen, Abend, Pause, Überstunden, Wochenzusammenfassung |
| IHapticService | Haptisches Feedback (Desktop: NoOp, Android: Vibrator API Click/HeavyClick) |

## Rewarded Ads (Soft Paywall)

| Feature | Placement-ID | Dauer |
|---------|--------------|-------|
| Urlaubseintrag/Quota/Uebertrag | `vacation_entry` | Einmalig |
| PDF-Export | `export` | Einmalig |
| Statistik-Export | `monthly_stats` | Einmalig |
| Erweiterte Zeitraeume (Quartal/Jahr) | `monthly_stats` | 24h Zugang |

**Erweiterte Stats**: `HasExtendedStatsAccess()` + Preference-Key `"ExtendedStatsExpiry"` (UTC + RoundtripKind)

## Besondere Architektur

### Trial-System
- 7 Tage kostenloser Zugang zu Premium-Features
- Nach Trial: Soft Paywall mit Rewarded Ads oder Premium-Kauf

### Export-Logik
- PdfSharpCore + ClosedXML
- Android: IFileShareService (FileProvider `com.meineapps.worktimepro.fileprovider`)
- Desktop: Process.Start

### Kalender-Overlay
- Status-Eintrag direkt im Kalender via Overlay (statt NavigationRequested)
- CalendarDay: StatusIconKind (MaterialIconKind) fuer visuelle Darstellung

### Vacation-Typen (9)
Vacation, Sick, HomeOffice, BusinessTrip, SpecialLeave, UnpaidLeave, OvertimeCompensation, Training, CompensatoryTime

### Smart Notifications (2 Schichten)
- **INotificationService**: Plattform-abstrakt (Desktop: PowerShell Toast / notify-send + Task.Delay, Android: NotificationChannel + AlarmManager + ReminderReceiver)
- **IReminderService → ReminderService**: Orchestriert 5 Typen (Morgen, Abend, Pause, Überstunden, Wochenzusammenfassung). Subscribed auf `ITimeTrackingService.StatusChanged`. SettingsViewModel ruft `RescheduleAsync()` bei Reminder-Änderungen auf.
- **Android**: `worktimepro_reminder` NotificationChannel, `ReminderReceiver` BroadcastReceiver, `SetExactAndAllowWhileIdle` für Hintergrund-Notifications. Permissions: POST_NOTIFICATIONS, SCHEDULE_EXACT_ALARM.

## SkiaSharp-Visualisierungen

| Datei | Zweck |
|-------|-------|
| `Graphics/DayTimelineVisualization.cs` | 24h-Timeline mit Arbeits-/Pausen-Blöcken, Stundenticks, Jetzt-Markierung |
| `Graphics/WeekBarVisualization.cs` | Wochen-Balkendiagramm (7 Tage), Ist/Soll-Vergleich, farbige Balken |
| `Graphics/OvertimeSplineVisualization.cs` | Überstunden-Trend: Tagesbalken (grün/rot) + kumulative Spline-Kurve mit Flächenfüllung |
| `Graphics/WeekdayRadialVisualization.cs` | Radiales Balkendiagramm (Mo-So), gestrichelte Soll-Linie, Ø-Wert in Mitte |
| `Graphics/WeeklyWorkChartVisualization.cs` | Wöchentliche Arbeitsstunden als Balkendiagramm mit Soll-Linie |
| `Graphics/MonthlyBarChartVisualization.cs` | Monatliche Arbeitsstunden-Balken + optionale kumulative Saldo-Kurve |
| `Graphics/LinearProgressVisualization.cs` | Linearer Fortschrittsbalken mit Gradient, Glow, Prozent-Text (ersetzt ProgressBar) |
| `Graphics/CalendarHeatmapVisualization.cs` | GitHub-Contribution-Style Heatmap (7×5/6 Grid), 5 Stufen-Gradient, Status-Punkte, Heute-Ring (pulsierend), Touch-HitTest |
| `Graphics/VacationQuotaGaugeVisualization.cs` | 3 konzentrische Ringe (Genommen/Geplant/Rest), Endpunkt-Dots, Glow, Legende, Farbe grün→gelb→rot nach Verbrauch |
| `Graphics/StatsSummaryGaugeVisualization.cs` | 4 kompakte Halbkreis-Gauges (Arbeitszeit, Überstunden, Schnitt/Tag, Quote), Überfluss-Markierung >100% |
| `Graphics/MonthWeekProgressVisualization.cs` | Alle Wochen eines Monats als Gradient-Balken in einem Canvas, Labels + Saldo, Glow-Effekt |
| `Graphics/WorkTimeProSplashRenderer.cs` | "Die Stechuhr": Dunkelgrau-Hintergrund mit Kalender-Grid, animierte Stechuhr mit Karten-Stempelzyklus (3s), 10 Business-Partikel. Erbt von SplashRendererBase |
| `Graphics/WorkspaceBackgroundRenderer.cs` | "Professional Dashboard": 5-Layer animierter Hintergrund (3-Farben Gradient, Dot-Matrix-Grid mit Drift, Calendar-Block-Partikel, gestrichelte Stunden-Linien, Vignette). ~5fps Render-Loop, 0 GC pro Frame, max 10 Calendar-Blocks |

- **MainView**: WorkspaceBackgroundRenderer (animierter Hintergrund, alle Rows, hinter Content, ~5fps DispatcherTimer)
- **TodayView**: SkiaGradientRing (Tagesfortschritt, 24 Ticks, Glow+Pulsation bei Tracking) + DayTimeline (Arbeitsblöcke grün, Pausen orange schraffiert)
- **WeekOverviewView**: WeekBarVisualization (Balken pro Tag) + LinearProgressVisualization (Wochenfortschritt, ersetzt ProgressBar)
- **StatisticsView**: 6 SkiaSharp-Charts (PauseDonut, WeeklyChart, OvertimeSpline, WeekdayRadial, ProjectDonut, EmployerDonut) - LiveCharts vollständig ersetzt
- **YearOverviewView**: MonthlyBarChartVisualization (Monatsbalken + kumulative Saldo-Kurve) - LiveCharts vollständig ersetzt
- Shared: `DonutChartVisualization` aus MeineApps.UI/SkiaSharp/ (wiederverwendbar für alle Apps)

## Game Juice

- **FloatingText**: "Feierabend!" bei CheckOut + optionale Ueberstunden-Anzeige ("+X.Xh")
- **Celebration**: Confetti bei Feierabend (MainViewModel.ToggleTrackingAsync)

## Architektur-Hinweise

- **DateTime-Konvention**: Arbeitszeiten (Check-in/out, Pausen) nutzen `DateTime.Now` (Ortszeit). Audit-Timestamps (CreatedAt/ModifiedAt) nutzen `DateTime.UtcNow`. Export-Footer und Backup-Dateinamen bleiben Ortszeit (menschenlesbar).
- **TimeEntry.TypeText**: Lokalisiert via `AppStrings.CheckIn`/`AppStrings.CheckOut` (nicht hardcoded)

## Architektur-Details

- **Settings Auto-Save**: SettingsViewModel speichert automatisch per Debounce-Timer (800ms). Kein Speichern-Button. `ScheduleAutoSave()` wird von allen `OnXxxChanged` partial-Methods aufgerufen. `_isInitializing` Flag verhindert Speichern während `LoadDataAsync`.
- **Tab-Reload**: MainViewModel.OnCurrentTabChanged lädt Daten für den jeweiligen Tab automatisch neu (LoadTabDataAsync). Stellt sicher, dass z.B. die Wochenansicht aktuelle Settings berücksichtigt.
- **Loading-Pipeline**: `WorkTimeProLoadingPipeline` (in `Loading/`) führt echtes Preloading aus: DB-Init + Shader-Kompilierung parallel, dann Achievement, Reminder, ViewModel-Erstellung. `SkiaLoadingSplash` zeigt Fortschrittsring + Statustext. App.axaml.cs setzt DataContext erst nach Pipeline-Abschluss. MainViewModel exponiert `WaitForInitializationAsync()` für die Pipeline.
- **Initiale Datenladung**: MainViewModel-Konstruktor speichert `_initTask = InitializeAsync()`. `WaitForInitializationAsync()` / `EnsureInitializedAsync()` wird in ToggleTracking/TogglePause und von der Loading-Pipeline aufgerufen um Race Conditions zu vermeiden.
- **Sub-Seiten-Datenladung**: Alle Navigate-Commands (DayDetail, Month, Year, Vacation, ShiftPlan) sind async und awaiten `LoadDataAsync()` auf dem Ziel-VM.
- **Settings-Propagation**: `SettingsViewModel.SettingsChanged` Event wird nach jedem `SaveSettingsAsync` gefeuert. MainViewModel subscribed darauf und lädt den aktiven Tab neu. Bei Arbeitszeit-relevanten Änderungen (DailyHours, WeeklyHours, Arbeitstage) wird eine Warnung angezeigt wenn bestehende WorkDays betroffen sind (`SettingsChangedWarning` RESX-Key).
- **MessageRequested-Handler**: MainView.axaml.cs verdrahtet `MessageRequested` Event → Fehler werden als roter FloatingText angezeigt + Debug.WriteLine geloggt.
- **Kalender-Overlay**: Schließt automatisch nach Speichern/Entfernen ohne Bestätigungsmeldung.
- **SelectLanguage Bug-Fix**: CommandParameter ist Sprachcode ("de"/"en"/...), kein Integer-Index.
- **Lösch-Bestätigung**: DayDetailViewModel nutzt Confirm-Overlay-Pattern (`IsConfirmDeleteVisible`, `_pendingDeleteAction`) für TimeEntry- und Pause-Löschung. RESX-Keys: `ConfirmDelete`, `DeleteEntryConfirm`, `DeletePauseConfirm`, `Yes`, `No`.
- **Export Batch-Query**: `GetTimeEntriesForWorkDaysAsync(List<int>)` in IDatabaseService/DatabaseService lädt alle TimeEntries für mehrere WorkDays in einer Query. Vermeidet N+1 im ExportService.
- **Event-Handler Cleanup**: MainViewModel speichert `_wiredEvents` Liste für sauberes Dispose der Reflection-basierten Event-Handler aus `WireSubPageNavigation()`.
- **Undo CheckIn/CheckOut**: MainViewModel zeigt nach CheckIn/CheckOut 5 Sekunden lang einen Undo-Button. `_lastUndoEntry` speichert den zu löschenden Eintrag. `UndoLastActionAsync` löscht den Eintrag, berechnet WorkDay neu und lädt Status. Ctrl+Z Shortcut. 3 RESX-Keys (Undo, UndoCheckIn, UndoCheckOut).
- **Keyboard Shortcuts (Desktop)**: MainView.axaml.cs OnKeyDown: F5=Refresh, 1-5=Tabs, Escape=Sub-Page schließen, Ctrl+Z=Undo.
- **CalendarVM Lazy-Load**: Konstruktor lädt keine Daten mehr (`_ = LoadDataAsync()` entfernt). Daten werden erst bei Tab-Wechsel geladen (MainViewModel.LoadTabDataAsync).
- **TimeFormatter**: Zentraler Helper (`Helpers/TimeFormatter.cs`) für `FormatMinutes()`, `FormatBalance()`, `GetStatusName()` - eliminiert alle lokalen `FormatTimeSpan()`-Kopien (WorkDay, WorkMonth, WorkWeek, MainViewModel).
- **DatabaseService Indizes**: UNIQUE auf WorkDay.Date, Indizes auf FK-Spalten (TimeEntry.WorkDayId, PauseEntry.WorkDayId, VacationEntry.Year, ShiftAssignment.Date).
- **BackupService Sicherheits-Backup**: Vor Restore wird Sicherheits-Backup erstellt. Bei Fehler automatischer Rollback auf vorherigen Stand.
- **BackupService Lokaler Export**: `CreateLocalBackupAsync()` erstellt JSON in `Backups/`-Ordner (kein Cloud-Auth nötig). `ExportBackupAsync()` = Backup + Share-Sheet via IFileShareService. `ImportBackupFromFileAsync()` importiert aus beliebiger Datei mit Safety-Backup. `GetLocalBackupsAsync()` listet lokale Backups.
- **VacationVM Quota-Edit**: Overlay-Bearbeitung von Urlaubstagen pro Jahr + Resturlaub (`IsEditingQuota`, `EditTotalDays`, `EditCarryOverDays`).
- **DesktopNotificationService**: PowerShell-Injection-sicher via Single-Quoted Here-String + `-EncodedCommand` (Base64).
- **CircularProgressControl**: Custom Avalonia Control (`Controls/CircularProgressControl.cs`) für kreisförmigen Fortschrittsring. Zeichnet Track-Kreis + Progress-Arc via StreamGeometry. Properties: Progress (0-100), TrackBrush, ProgressBrush, StrokeWidth, IsPulsing. WICHTIG: Property heißt `IsPulsing` (nicht `IsAnimating`), da `AvaloniaObject.IsAnimating()` Methode kollidiert.
- **Zeitrundung**: `WorkSettings.RoundingMinutes` (0/5/10/15/30), `CalculationService` rundet Netto-Arbeitszeit. SettingsView: ComboBox mit `RoundingDisplayConverter`.
- **Stundenlohn**: `WorkSettings.HourlyRate`, MainViewModel berechnet `TodayEarnings` in UpdateLiveDataAsync, TodayView zeigt Earnings-Card mit CurrencyEur-Icon.
- **Haptic Feedback**: `IHapticService` (Click/HeavyClick), Desktop: `NoOpHapticService`, Android: `AndroidHapticService` (Vibrator API). MainViewModel: CheckIn=Click, CheckOut=HeavyClick, Pause=Click.
- **WorkDaysArray Caching**: `WorkSettings.WorkDaysArray` nutzt jetzt Cache mit String-Vergleich statt bei jedem Zugriff neu zu parsen.
- **DailyHoursPerDay Caching**: `WorkSettings.GetHoursForDay()` cached deserialisiertes JSON-Dictionary mit String-Vergleich. Cache wird in `SetHoursForDay()` mit-aktualisiert.
- **Settings-Cache in MainViewModel**: `_cachedSettings` wird in `LoadDataAsync` und `OnSettingsChanged` aktualisiert. `UpdateLiveDataAsync` (1s Timer) nutzt Cache statt DB-Query.
- **ClearAllDataAsync**: `IDatabaseService.ClearAllDataAsync()` löscht alle Tabellen-Inhalte (FK-Reihenfolge). Wird von `BackupService.RestoreDataAsync` vor dem Import aufgerufen.
- **AppColors**: Statische Klasse (`AppColors.cs`) mit Farbkonstanten (StatusIdle, StatusActive, StatusPaused, BalancePositive, BalanceNegative). Ersetzt Magic Strings in allen ViewModels und Models (WorkDay, WorkMonth, WorkWeek).
- **Predictive Insights TodayView**: `EstimatedEndTime` ("~17:23"), `RemainingTodayText` ("Noch 2:15"), `HasInsight` (nur bei aktivem Tracking + Soll nicht erfüllt). Berechnung in `UpdateLiveDataAsync`.
- **Kulturspezifische Formate**: `ToString("D")` statt hardcoded "dddd, dd. MMMM". `WeekNumberFormat` RESX-Key statt "KW". `ToString("d")` für DateRange.
- **LiveDataSnapshot**: `ITimeTrackingService.GetLiveDataSnapshotAsync()` liefert WorkTime, PauseTime, TimeUntilEnd, Today in einem Aufruf (3 DB-Queries statt 5+). `LiveDataSnapshot` Record in ITimeTrackingService.cs definiert. Eliminiert DB-Query-Sturm bei 1s-Timer in MainViewModel.
- **N+1 Query Fixes in DatabaseService**: `SetDefaultEmployerAsync` nutzt 2 SQL-UPDATEs statt alle Employer laden + einzeln updaten. `LockMonthAsync`/`UnlockMonthAsync` nutzen je 1 SQL-UPDATE statt N WorkDays laden + einzeln updaten. `SaveHolidaysAsync` nutzt `RunInTransactionAsync` statt einzelner Inserts/Updates.
