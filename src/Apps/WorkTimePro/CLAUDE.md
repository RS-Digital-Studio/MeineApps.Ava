# WorkTimePro (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Zeiterfassung & Arbeitszeitmanagement mit Pausen, Kalender-Heatmap, Statistiken, Export, Urlaubsverwaltung, Feiertage (16 Bundeslaender), Projekten, Arbeitgebern und Schichtplanung.

**Version:** 2.0.0 | **Package-ID:** com.meineapps.worktimepro | **Status:** Geschlossener Test

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

## Changelog Highlights

- **01.03.2026**: **Immersiver Ladebildschirm**: SkiaLoadingSplash von Ring-basiert auf SplashScreenRenderer umgestellt (Gradient-BG, 24 Glow-Partikel, Fortschrittsbalken). MainWindow.axaml vereinfacht (selbstschließendes Window-Tag, Content wird in App.axaml.cs gesetzt). App.axaml.cs: Panel(MainView + Splash) Pattern einheitlich mit allen 8 Apps. IconSource-Property entfernt, AppVersion-Property hinzugefügt. Build 0 Fehler.
- **28.02.2026 (8)**: Code-Review Fixes Loading-Pipeline: (1) **BUG-3**: Stream-Leak bei Icon-Laden (Android) behoben (`using var stream`). (2) **POT-3**: SKShader + SKMaskFilter in SkiaLoadingSplash.OnPaintSurface separat disposed. (3) **POT-1**: try/catch um `RunLoadingAsync` - bei Fehler wird Splash trotzdem ausgeblendet.
- **28.02.2026 (7)**: Echter Ladebildschirm mit Preloading: (1) `WorkTimeProLoadingPipeline` in `Loading/` - führt DB-Init + Shader-Kompilierung parallel aus (statt sequentiell fire-and-forget), dann Achievement, Reminder, ViewModel. (2) `SkiaLoadingSplash` aus MeineApps.UI ersetzt `SplashOverlay` in MainWindow.axaml - SkiaSharp-Fortschrittsring mit Glow, smooth-interpolierter Progress, Statustext. (3) App.axaml.cs: `InitializeAndStartAsync()` entfernt, Pipeline-gesteuert. DataContext wird erst nach Pipeline-Abschluss gesetzt. (4) MainViewModel: `WaitForInitializationAsync()` als public API exponiert (für Pipeline). (5) Android: Panel-Wrapper mit SkiaLoadingSplash für SingleViewLifetime.
- **28.02.2026 (6)**: Performance-Optimierung + Code-Cleanup: (1) LiveDataSnapshot-Pattern: `GetLiveDataSnapshotAsync()` in ITimeTrackingService/TimeTrackingService konsolidiert GetCurrentWorkTimeAsync + GetCurrentPauseTimeAsync + GetTimeUntilEndAsync in einen einzigen Aufruf (3 DB-Queries statt 8+/Sekunde). MainViewModel.UpdateLiveDataAsync nutzt Snapshot. (2) CalculationService: RecalculateWorkDayAsync lädt Entries/Pauses/Settings einmal und reicht sie durch (RecalculatePauseTime static helper, ApplyAutoPauseAsync Overload mit Pre-loaded Data - 7 redundante DB-Queries eliminiert). (3) ReminderService: IDisposable-Interface deklariert + N+1 Query in ScheduleWeeklyReminderAsync durch Batch-Query GetWorkDaysAsync ersetzt. (4) SkiaSharp MaskFilter/Shader Lifecycle: 4 MaskFilter-Leaks (OvertimeSpline, StatsSummaryGauge, VacationQuotaGauge, MonthWeekProgress) + 3 Shader-Leaks (OvertimeSpline, DayTimeline, MonthWeekProgress) mit using-Blöcken gefixt.
- **28.02.2026 (5)**: Code-Cleanup: (1) WorkDay/WorkMonth hardcoded Farben (#4CAF50/#F44336) durch AppColors.BalancePositive/BalanceNegative ersetzt. (2) FormatTimeSpan-Duplikate in WorkDay, WorkMonth, WorkWeek, MainViewModel durch TimeFormatter.FormatMinutes/FormatBalance ersetzt (4 lokale Kopien entfernt). (3) CalendarViewModel.AvailableStatusTypes gecacht (lazy-initialisiert, invalidiert bei LoadDataAsync). (4) StringNotNullConverter delegiert intern an StringToBoolConverter (identische Logik, kein Duplikat mehr). (5) VacationQuotaGaugeVisualization: sweepAngle-Variable aus using-Block nach oben verschoben (Kompilierfehler behoben).
- **28.02.2026 (4)**: Bugfix-Session (Phase 3): (1) 5 neue Achievements (night_owl, marathon, half_year, full_year, pause_master) mit SharedProgressData-Erweiterung + RESX in 6 Sprachen. (2) Lokaler Backup-Export ohne Cloud-Auth: `CreateLocalBackupAsync()`, `ExportBackupAsync()` (Share-Sheet), `ImportBackupFromFileAsync()`, `GetLocalBackupsAsync()`. BackupService erhält IFileShareService-Dependency. (3) SettingsVM: ExportBackup/ImportBackup Commands. (4) Hardcoded Farben in AppColors zentralisiert (CalendarVM, StatisticsVM, SettingsVM, ShiftPlanVM). (5) Datumsformate lokalisiert (ToString("D") statt "dddd, dd. MMMM yyyy"). (6) "KW" lokalisiert (WeekNumberFormat/WeekNumberShort RESX-Keys). (7) AchievementService: TotalPauseMinutes-Bug gefixt (ManualPauseMinutes + AutoPauseMinutes). 8 neue Backup-RESX-Keys + 10 Achievement-RESX-Keys in 6 Sprachen + Designer.
- **28.02.2026 (3)**: Verbesserungen (Phase 2): (1) Settings-Cache in MainViewModel (DB-Query jede Sekunde eliminiert). (2) Backup-Restore: ClearAllDataAsync vor Import (kein Misch-Zustand mehr). (3) Tote Navigationsrouten gefixt (month?date= und WeekOverviewPage). (4) WorkSettings.DailyHoursPerDay JSON-Cache (keine Deserialisierung pro Aufruf). (5) CalendarView doppelte GenerateCalendarDaysAsync eliminiert. (6) Datumsformate lokalisiert (ToString("D") statt hardcoded "dd. MMMM"). (7) KW-Anzeige lokalisiert (RESX-Key WeekNumber). (8) Hardcoded Farben in AppColors-Klasse zentralisiert. (9) Predictive Insights auf TodayView (geschätzte Feierabendzeit + Restzeit). (10) MonthOverview Wochen-Berechnung parallelisiert (Task.WhenAll).
- **28.02.2026 (2)**: Bugfix-Session (8 Fixes):
  - FIX: AchievementView Gold-Tint funktionierte nicht (CSS-Klasse wurde nie gesetzt → Panel-Overlay-Pattern mit IsVisible-Binding)
  - FIX: BigAction-Button fehlte `RenderTransform="scale(1)"` → Android-Crash bei TransformOperationsTransition
  - FIX: Hardcoded "Tage" in Streak-Anzeige → lokalisierte `StreakDisplayText` Property (nutzt AppStrings.DayStreak)
  - FIX: CalendarSyncService negatives Balance-Vorzeichen fehlte (`""` statt `"-"` bei negativer Balance)
  - FIX: Duplizierte Streak-Berechnung in MainViewModel entfernt → nutzt jetzt `IAchievementService.GetCurrentStreakAsync()`
  - FIX: Achievements fehlten im Backup/Restore → `BackupData.Achievements` Property + Backup/Restore-Logik
  - FIX: Unbenutzte Variable `prefix` in WeekOverviewViewModel entfernt
  - FIX: TodayView PropertyChanged Memory Leak → anonymes Lambda durch benannten Handler mit sauberem Lifecycle ersetzt
  - PERF: AchievementService komplett ueberarbeitet: SharedProgressData-Pattern (gemeinsame Daten einmal laden), nur geaenderte Achievements speichern, HasPerfectWeekBulkAsync (1 Query statt 52), CalculateStreakFromDays (Batch statt N+1)
- **28.02.2026**: Achievement-System + Predictive Insights + UI-Verbesserungen:
  - NEU: Achievement/Badge-System (10 Achievements: Stunden-Meilensteine, Streak-Badges, Perfekte Woche, Volle Anwesenheit, Frühaufsteher, Überstundenkönig)
  - NEU: `IAchievementService` + `AchievementService` mit DB-Persistenz (`Achievement`-Model, SQLite-Tabelle)
  - NEU: `AchievementViewModel` + `AchievementView` (2-Spalten-Grid, Gold-Tint bei Unlock, Progress-Bars, Unlock-Datum)
  - NEU: Achievement-Check nach CheckIn/CheckOut, Celebration + FloatingText bei Unlock
  - NEU: Predictive Insights in WeekOverviewView (verbleibende Stunden bis Wochenziel, Monatstrend)
  - NEU: Earnings-Ticker CountUp-Animation (800ms EaseOut, DispatcherTimer 30fps)
  - NEU: Balance-Glow Pulse bei negativer Balance (Opacity 0.6→1.0, 2s Infinite)
  - NEU: Achievements-Button in TodayView Quick-Nav (Gold-Hintergrund, Trophy-Icon)
  - 24 RESX-Keys in 6 Sprachen (DE, EN, ES, FR, IT, PT) + Designer
- **16.02.2026**: Phase 13 SkiaSharp-Erweiterungen:
  - NEU: CalendarHeatmapVisualization (GitHub-Style Heatmap, 5 Stufen-Gradient, Status-Punkte, pulsierender Heute-Ring, HitTest)
  - NEU: VacationQuotaGaugeVisualization (3 konzentrische Ringe: Genommen/Geplant/Rest, Endpunkt-Dots, Glow, grün→gelb→rot Legende)
  - NEU: StatsSummaryGaugeVisualization (4 Halbkreis-Gauges: Arbeitszeit, Überstunden, Schnitt/Tag, Arbeitstage-Quote, Überfluss >100%)
  - NEU: MonthWeekProgressVisualization (Wochen-Fortschrittsbalken mit Gradient, Glow, Labels, Saldo-Farbe)
- **13.02.2026 (6)**: KRITISCH: InsertAsync ID-Bug in DatabaseService + Navigation-Fixes: (1) **KRITISCH**: `InsertAsync()` (sqlite-net-pcl) gibt Anzahl betroffener Zeilen zurück (immer 1), NICHT die Auto-Increment-ID. sqlite-net setzt die ID direkt auf dem Objekt. Code überschrieb korrekte ID mit `entity.Id = newId` (=1) → ALLE 8 Insert-Methoden gefixt (WorkDay, TimeEntry, PauseEntry, VacationEntry, Project, ProjectTimeEntry, Employer, ShiftPattern, ShiftAssignment). (2) MainView.axaml.cs OnMessage zeigte nur Titel ("Fehler") statt eigentliche Fehlermeldung → zeigt jetzt `message` Inhalt. (3) MainViewModel.HandleNavigation verarbeitet jetzt DayDetail-Routen (`DayDetailPage?date=...`), nicht nur `..`/back. (4) WeekVm + CalendarVm NavigationRequested Events jetzt verdrahtet (WireSubPageNavigation). (5) NavigateToDayDetailAsync setzt SelectedDate auf DateTime.Today. **HINWEIS**: Bestehende DB-Daten können durch den alten Bug korrupt sein → ggf. DB löschen für sauberen Neustart.
- **13.02.2026 (5)**: Bugfix Race Conditions + Settings-Propagation: (1) Fire-and-Forget beseitigt: `_initTask = InitializeAsync()` statt `_ = LoadDataAsync()`, `EnsureInitializedAsync()` in ToggleTracking/TogglePause. (2) Navigate-Commands async: DayDetail/Month/Year/Vacation/ShiftPlan awaiten jetzt `LoadDataAsync()`. (3) Settings-Propagation: `SettingsChanged` Event in SettingsViewModel, MainViewModel subscribed und lädt aktiven Tab neu. (4) Warnung bei Arbeitszeit-Settings-Änderung wenn bestehende WorkDays betroffen (`_workTimeSettingsChanged` Flag). 1 neuer RESX-Key (SettingsChangedWarning) in 6 Sprachen + Designer.
- **13.02.2026 (4)**: UI/UX Overhaul: (1) MainView: Animierter Tab-Indikator (TransformOperationsTransition, translateX), aktives Tab-Highlighting (PrimaryBrush), Tab-Fade-Transition (Opacity 0.18s), Sub-Page Slide-In (translateX 0.25s). (2) TodayView: Pulsierender Glow-Ring (IsPulsing + Opacity-Animation 2s INFINITE), BrushTransition auf Status-Badge (400ms), DayProgressPercent im Ring, BigAction Button Press-Effekt (scale 0.92/1.04), Quick-Nav Cards mit Hover-Scale, Earnings-Card grüner Border, Streak-Anzeige (Fire-Icon). (3) CalendarView: Heute pulsierender Border (PrimaryBrush, Opacity 0.5-1.0 INFINITE). (4) WeekOverviewView: Farbige Seitenleiste pro Tag (BalanceColor), ProgressBar CornerRadius. (5) StatisticsView: SegmentedControl Period-Selection, Summary CardElevated mit 32px Zahlen, View-Toggle+Export kompakte Row. (6) SettingsView: Crown goldfarben (#FFD700), Section-Header-Linien (PrimaryBrush 0.3), Spacing 12. (7) DayDetailView: Timeline-Cards mit vertikaler Linie + Kreis-Marker. (8) MonthOverviewView: CardElevated für Summary, größere Zahlen (26px). (9) YearOverviewView: Kompakteres Layout. (10) MainViewModel: StreakCount + HasStreak + CalculateStreakAsync, Wochenziel-Celebration, DayProgressPercent Property. 3 RESX-Keys (WeekGoalReached, DayStreak, PerHour) in 6 Sprachen + Designer.
- **13.02.2026 (3)**: Kritische Bugfixes: (1) KRITISCH: Kein initialer LoadDataAsync-Aufruf → MainViewModel-Konstruktor ruft jetzt `_ = LoadDataAsync()` auf (Status aus DB laden, Today-Ansicht aktualisieren). (2) KRITISCH: Sub-Seiten-Navigation (DayDetail, Month, Year, Vacation, ShiftPlan) rief LoadDataAsync nicht auf → WorkDay war null → AddEntry/AddPause/etc. taten nichts. (3) MessageRequested Event in MainView.axaml.cs verdrahtet (vorher: alle Fehlermeldungen stumm geschluckt). Fehler werden jetzt als roter FloatingText angezeigt + Debug.WriteLine.
- **13.02.2026 (2)**: Feature-Session: (1) Zeitrundung (5/10/15/30 min) in WorkSettings + CalculationService + SettingsView ComboBox + RoundingDisplayConverter. (2) Stundenlohn in WorkSettings + SettingsView NumericUpDown + MainViewModel TodayEarnings-Berechnung + TodayView Earnings-Card. (3) CircularProgressControl (Custom Avalonia Control) für Tages-Fortschrittsring um den Start/Stop-Button. (4) IHapticService + NoOpHapticService (Desktop) + AndroidHapticService (Vibrator API) - Click bei CheckIn/Pause, HeavyClick bei CheckOut. (5) WorkDaysArray-Caching in WorkSettings. 7 neue RESX-Keys (MinutesShortFormat, TimeRounding, NoRounding, HourlyRate, TodayEarnings, TotalEarnings, Earnings) in 6 Sprachen + Designer.
- **13.02.2026**: Tiefgründige Bugfix+Refactoring-Session (Konkurrenz-Vergleich): (1) KRITISCH: BackupService Sicherheits-Backup vor Restore + Rollback bei Fehler. (2) KRITISCH: DatabaseService UNIQUE-Index auf WorkDay.Date + Performance-Indizes + SQLiteException-Catch für Race-Conditions. (3) DayDetailVM FormatMinutes Math.Abs-Fix für negative Zeiten. (4) MainViewModel Timer-Handler Named-Method statt Anonymous-Lambda + Dispose. (5) SettingsVM CancellationTokenSource für ReminderReschedule. (6) YearOverviewVM Task.WhenAll für parallele Monatsberechnung. (7) ExportService CSV UTF-8 BOM für Excel-Kompatibilität. (8+9) Code-Duplikation: TimeFormatter.cs (FormatMinutes/FormatBalance/GetStatusName) ersetzt 6 lokale Kopien in DayDetailVM, YearOverviewVM, StatisticsVM, ExportService, CalendarVM, CalendarSyncService. (10) MonthOverviewVM HashSet statt List.Any(). (12) ExportService PDF-Konstanten (PdfRowBottomBuffer, PdfSummaryBottomBuffer). (13) VacationVM EditQuotaAsync implementiert (Overlay für Urlaubstage + Resturlaub bearbeiten). (14) YearOverview Chart-Achsen i18n-reaktiv. (15) DesktopNotificationService PowerShell-Injection-Fix (Single-Quoted Here-String + EncodedCommand).
- **12.02.2026 (5)**: Bugfix+Optimierung: 20 Bugs gefixt (Kritisch: hardcoded Startdatum für Saldo, Integer-Division in WorkMonth, ReminderService KW-Berechnung; Hoch: CTS-Leak, PDF Seitenumbruch, PauseEntry Mitternacht; Mittel: 3-Tage Midnight-Search, HolidayCache-Invalidierung, VacationEntry EndDate-Validierung, DayStatus.Work Alias entfernt; Niedrig: leere Catches, HolidayEntry Datumsformat). Optimierungen: Undo CheckIn/CheckOut (5s Fenster, Ctrl+Z), Keyboard Shortcuts (F5/1-5/Escape), CalendarVM Lazy-Load. 3 RESX-Keys (Undo, UndoCheckIn, UndoCheckOut) in 6 Sprachen.
- **12.02.2026 (4)**: Smart Notifications: INotificationService (Desktop: Toast/notify-send, Android: NotificationChannel + AlarmManager) + IReminderService mit 5 Typen (Morgen/Abend/Pause/Überstunden/Wochenzusammenfassung). ReminderService subscribed auf StatusChanged (kein MainViewModel-Umbau). SettingsViewModel ruft RescheduleAsync() bei Reminder-Änderungen. Android: ReminderReceiver (BroadcastReceiver), POST_NOTIFICATIONS + SCHEDULE_EXACT_ALARM Permissions. 10 neue RESX-Keys in 6 Sprachen + Designer.
- **12.02.2026 (3)**: Tiefgreifende Bugfix-Session (20 Bugs): (1) Trial-Fortschrittsbalken zeigte /14 statt /7 Tage. (2-4) Individuelle Tagesarbeitszeiten wurden in Wochen-/Monatsberechnung ignoriert → CalculationService nutzt jetzt `GetDailyMinutesForDay()`. (5) Arbeitszeit tickte während Pause weiter (TrackingService Status-Check). (6) Statistik-Chart hardcoded 40h statt Settings.WeeklyHours. (7) Laufender Urlaub wurde nicht proportional gezählt (VacationService Split). (8) WorkDaysArray Crash bei leerem String + Caching. (9) SelectedRegionIndex ArrayIndexOutOfBounds → Math.Clamp. (10) Status-Cycle unvollständig → alle 9 DayStatus-Typen. (11) Ad Event-Handler Memory Leak → Named Methods + Dispose. (14) UpdatePauseEntry suchte nur heute statt richtigen Tag. (15) VacationEntry.DaysDisplay hardcoded "Tage" → AppStrings.DaysFormat. (16) WorkMonth.LockStatusDisplay hardcoded → AppStrings.MonthLocked/MonthOpen. (17) Kalender-Farben nur für Light-Theme → IThemeService + isDarkTheme. (19) BackupService AppVersion hardcoded → Reflection. (20) Leere catch-Blöcke → Debug.WriteLine. (23) WorkDaysArray dupliziert in VacationService → settings.WorkDaysArray. 2 neue RESX-Keys (DaysFormat, MonthOpen) in 6 Sprachen.
- **12.02.2026 (2)**: Bugfix-Session (6 Bugs): (1) N+1 Query in ExportService → Batch-Query `GetTimeEntriesForWorkDaysAsync()` statt Loop-Query in PDF/Excel/CSV. (2) Doppelte `RecalculatePauseTimeAsync` in 4 Stellen entfernt (wird bereits von `RecalculateWorkDayAsync` aufgerufen). (3) Lösch-Bestätigung für TimeEntries und Pausen via Confirm-Overlay in DayDetailView. (4) `Process.Start` → `UriLauncher.OpenUri()` in SettingsViewModel (Android-Kompatibilität). (5) Auto-Pause berücksichtigt jetzt laufenden CheckIn (ArbZG-Compliance). (6) Negative Arbeitszeit abgesichert + Memory Leak in WireSubPageNavigation gefixt. 3 RESX-Keys in Designer ergänzt (ConfirmDelete, Yes, No).
- **12.02.2026**: Settings Auto-Save (Debounce 800ms, kein Speichern-Button), Tab-Wechsel lädt Daten neu (WeekOverview/Calendar/Statistics/Settings), Kalender-Overlay schließt automatisch, SelectLanguage Bug-Fix (langCode statt int)
- **11.02.2026 (4)**: Zeiteinträge & Pausen bearbeiten/hinzufügen: DayDetailView Overlay-Pattern (WheelPicker) für TimeEntry-Edit (Stunde/Minute/Typ-Toggle/Notiz) und PauseEntry-Edit (Start+Ende/Notiz). "Pause hinzufügen"-Button, Edit-Button bei manuellen Pausen. Validierung (CheckIn/CheckOut-Reihenfolge, Pausen-Überlappung, Endzeit>Startzeit). OriginalTimestamp bei Bearbeitung. 10 neue RESX-Keys (HoursShort, MinutesShort, StartTime, EndTime, AddBreak, EntryType, EditEntry, 3x Validation) in 6 Sprachen + Designer.
- **11.02.2026 (3)**: Optimierungen: ExportService vollständig lokalisiert (PDF/Excel/CSV - alle Titel, Header, Zusammenfassungen via AppStrings statt hardcoded Deutsch, Excel-Datum CultureInfo.CurrentCulture), Project.cs BudgetHours/HourlyRate Negativwert-Validierung (Math.Max(0)), 3 neue RESX-Keys (ExportWorkTimeReport, ExportTotal, ExportYearOverviewTitle) in allen 6 Sprachen
- **11.02.2026 (2)**: Härtung: TimeTrackingService Midnight-Crossing-Fix (CheckOut nach Mitternacht berechnet korrekt über Tagesgrenze), Validierung (negative Pausen, CheckOut vor CheckIn), Double-Tap-Guard (_isToggling), Thread-Safety (SemaphoreSlim). DatabaseService GetTimeEntriesForDate UTC→Local korrekt. CalculationService Warning-Strings lokalisiert (CalculationLongPause, CalculationNightShift, CalculationOvertime RESX-Keys)
- **11.02.2026**: Bugfix-Review: DateTime.UtcNow für alle Audit-Timestamps (Models + DatabaseService + BackupService + CalendarSyncService), TimeEntry.TypeText lokalisiert (AppStrings), redundante DayStatus.Work Checks in CalendarViewModel entfernt
- **09.02.2026**: MessageRequested Event-Signatur von `Action<string>` zu `Action<string, string>` (Titel, Nachricht) in allen 10 ViewModels korrigiert (Convention-konform). Localization-Key "Info" in 6 .resx + Designer ergaenzt.
- **08.02.2026**: Game Juice (Floating-Text "Feierabend!" + Confetti + Ueberstunden)
- **07.02.2026**: Kalender Status-Overlay, Rewarded Ads (3 Placements), Android Export Fix (FileProvider)
