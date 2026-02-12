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

## Changelog Highlights

- **12.02.2026**: Settings Auto-Save (Debounce 800ms, kein Speichern-Button), Tab-Wechsel lädt Daten neu (WeekOverview/Calendar/Statistics/Settings), Kalender-Overlay schließt automatisch, SelectLanguage Bug-Fix (langCode statt int)
- **11.02.2026 (4)**: Zeiteinträge & Pausen bearbeiten/hinzufügen: DayDetailView Overlay-Pattern (WheelPicker) für TimeEntry-Edit (Stunde/Minute/Typ-Toggle/Notiz) und PauseEntry-Edit (Start+Ende/Notiz). "Pause hinzufügen"-Button, Edit-Button bei manuellen Pausen. Validierung (CheckIn/CheckOut-Reihenfolge, Pausen-Überlappung, Endzeit>Startzeit). OriginalTimestamp bei Bearbeitung. 10 neue RESX-Keys (HoursShort, MinutesShort, StartTime, EndTime, AddBreak, EntryType, EditEntry, 3x Validation) in 6 Sprachen + Designer.
- **11.02.2026 (3)**: Optimierungen: ExportService vollständig lokalisiert (PDF/Excel/CSV - alle Titel, Header, Zusammenfassungen via AppStrings statt hardcoded Deutsch, Excel-Datum CultureInfo.CurrentCulture), Project.cs BudgetHours/HourlyRate Negativwert-Validierung (Math.Max(0)), 3 neue RESX-Keys (ExportWorkTimeReport, ExportTotal, ExportYearOverviewTitle) in allen 6 Sprachen
- **11.02.2026 (2)**: Härtung: TimeTrackingService Midnight-Crossing-Fix (CheckOut nach Mitternacht berechnet korrekt über Tagesgrenze), Validierung (negative Pausen, CheckOut vor CheckIn), Double-Tap-Guard (_isToggling), Thread-Safety (SemaphoreSlim). DatabaseService GetTimeEntriesForDate UTC→Local korrekt. CalculationService Warning-Strings lokalisiert (CalculationLongPause, CalculationNightShift, CalculationOvertime RESX-Keys)
- **11.02.2026**: Bugfix-Review: DateTime.UtcNow für alle Audit-Timestamps (Models + DatabaseService + BackupService + CalendarSyncService), TimeEntry.TypeText lokalisiert (AppStrings), redundante DayStatus.Work Checks in CalendarViewModel entfernt
- **09.02.2026**: MessageRequested Event-Signatur von `Action<string>` zu `Action<string, string>` (Titel, Nachricht) in allen 10 ViewModels korrigiert (Convention-konform). Localization-Key "Info" in 6 .resx + Designer ergaenzt.
- **08.02.2026**: Game Juice (Floating-Text "Feierabend!" + Confetti + Ueberstunden)
- **07.02.2026**: Kalender Status-Overlay, Rewarded Ads (3 Placements), Android Export Fix (FileProvider)
