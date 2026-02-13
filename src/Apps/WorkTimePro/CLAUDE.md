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
- **Statistiken**: Charts (LiveCharts) + Tabelle, Taeglich/Woechentlich/Monatlich/Quartal/Jahr
- **Export**: PDF, Excel (XLSX), CSV via PdfSharpCore + ClosedXML
- **Urlaubsverwaltung**: 9 Status-Typen, Resturlaub, Uebertrag, Urlaubsanspruch
- **Feiertage**: 16 deutsche Bundeslaender
- **Schichtplanung**: Wiederkehrende Muster mit Tagesnamen-Lokalisierung
- **Projekte + Arbeitgeber**: CRUD mit Zuweisung zu Zeiteintraegen
- **Smart Notifications**: 5 Reminder-Typen (Morgen/Abend/Pause/Überstunden/Wochenzusammenfassung), plattformübergreifend
- **Zeitrundung**: 5/10/15/30 Minuten-Rundung der Arbeitszeit (Settings)
- **Stundenlohn**: Verdienst-Berechnung mit Anzeige auf TodayView
- **Fortschrittsring**: Kreisförmiger Tages-Fortschritt um den Start/Stop-Button
- **Haptic Feedback**: Vibration bei CheckIn/CheckOut/Pause (Android)

### ViewModels & Views (10 VMs, 12 Views)
MainViewModel, WeekOverview, Calendar, Statistics, Settings, DayDetail, MonthOverview, YearOverview, Vacation, ShiftPlan

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

## Game Juice

- **FloatingText**: "Feierabend!" bei CheckOut + optionale Ueberstunden-Anzeige ("+X.Xh")
- **Celebration**: Confetti bei Feierabend (MainViewModel.ToggleTrackingAsync)

## Architektur-Hinweise

- **DateTime-Konvention**: Arbeitszeiten (Check-in/out, Pausen) nutzen `DateTime.Now` (Ortszeit). Audit-Timestamps (CreatedAt/ModifiedAt) nutzen `DateTime.UtcNow`. Export-Footer und Backup-Dateinamen bleiben Ortszeit (menschenlesbar).
- **TimeEntry.TypeText**: Lokalisiert via `AppStrings.CheckIn`/`AppStrings.CheckOut` (nicht hardcoded)

## Architektur-Details

- **Settings Auto-Save**: SettingsViewModel speichert automatisch per Debounce-Timer (800ms). Kein Speichern-Button. `ScheduleAutoSave()` wird von allen `OnXxxChanged` partial-Methods aufgerufen. `_isInitializing` Flag verhindert Speichern während `LoadDataAsync`.
- **Tab-Reload**: MainViewModel.OnCurrentTabChanged lädt Daten für den jeweiligen Tab automatisch neu (LoadTabDataAsync). Stellt sicher, dass z.B. die Wochenansicht aktuelle Settings berücksichtigt.
- **Kalender-Overlay**: Schließt automatisch nach Speichern/Entfernen ohne Bestätigungsmeldung.
- **SelectLanguage Bug-Fix**: CommandParameter ist Sprachcode ("de"/"en"/...), kein Integer-Index.
- **Lösch-Bestätigung**: DayDetailViewModel nutzt Confirm-Overlay-Pattern (`IsConfirmDeleteVisible`, `_pendingDeleteAction`) für TimeEntry- und Pause-Löschung. RESX-Keys: `ConfirmDelete`, `DeleteEntryConfirm`, `DeletePauseConfirm`, `Yes`, `No`.
- **Export Batch-Query**: `GetTimeEntriesForWorkDaysAsync(List<int>)` in IDatabaseService/DatabaseService lädt alle TimeEntries für mehrere WorkDays in einer Query. Vermeidet N+1 im ExportService.
- **Event-Handler Cleanup**: MainViewModel speichert `_wiredEvents` Liste für sauberes Dispose der Reflection-basierten Event-Handler aus `WireSubPageNavigation()`.
- **Undo CheckIn/CheckOut**: MainViewModel zeigt nach CheckIn/CheckOut 5 Sekunden lang einen Undo-Button. `_lastUndoEntry` speichert den zu löschenden Eintrag. `UndoLastActionAsync` löscht den Eintrag, berechnet WorkDay neu und lädt Status. Ctrl+Z Shortcut. 3 RESX-Keys (Undo, UndoCheckIn, UndoCheckOut).
- **Keyboard Shortcuts (Desktop)**: MainView.axaml.cs OnKeyDown: F5=Refresh, 1-5=Tabs, Escape=Sub-Page schließen, Ctrl+Z=Undo.
- **CalendarVM Lazy-Load**: Konstruktor lädt keine Daten mehr (`_ = LoadDataAsync()` entfernt). Daten werden erst bei Tab-Wechsel geladen (MainViewModel.LoadTabDataAsync).
- **TimeFormatter**: Zentraler Helper (`Helpers/TimeFormatter.cs`) für `FormatMinutes()`, `FormatBalance()`, `GetStatusName()` - eliminiert Code-Duplikation in 6 Dateien.
- **DatabaseService Indizes**: UNIQUE auf WorkDay.Date, Indizes auf FK-Spalten (TimeEntry.WorkDayId, PauseEntry.WorkDayId, VacationEntry.Year, ShiftAssignment.Date).
- **BackupService Sicherheits-Backup**: Vor Restore wird Sicherheits-Backup erstellt. Bei Fehler automatischer Rollback auf vorherigen Stand.
- **VacationVM Quota-Edit**: Overlay-Bearbeitung von Urlaubstagen pro Jahr + Resturlaub (`IsEditingQuota`, `EditTotalDays`, `EditCarryOverDays`).
- **DesktopNotificationService**: PowerShell-Injection-sicher via Single-Quoted Here-String + `-EncodedCommand` (Base64).
- **CircularProgressControl**: Custom Avalonia Control (`Controls/CircularProgressControl.cs`) für kreisförmigen Fortschrittsring. Zeichnet Track-Kreis + Progress-Arc via StreamGeometry. Properties: Progress (0-100), TrackBrush, ProgressBrush, StrokeWidth.
- **Zeitrundung**: `WorkSettings.RoundingMinutes` (0/5/10/15/30), `CalculationService` rundet Netto-Arbeitszeit. SettingsView: ComboBox mit `RoundingDisplayConverter`.
- **Stundenlohn**: `WorkSettings.HourlyRate`, MainViewModel berechnet `TodayEarnings` in UpdateLiveDataAsync, TodayView zeigt Earnings-Card mit CurrencyEur-Icon.
- **Haptic Feedback**: `IHapticService` (Click/HeavyClick), Desktop: `NoOpHapticService`, Android: `AndroidHapticService` (Vibrator API). MainViewModel: CheckIn=Click, CheckOut=HeavyClick, Pause=Click.
- **WorkDaysArray Caching**: `WorkSettings.WorkDaysArray` nutzt jetzt Cache mit String-Vergleich statt bei jedem Zugriff neu zu parsen.

## Changelog Highlights

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
