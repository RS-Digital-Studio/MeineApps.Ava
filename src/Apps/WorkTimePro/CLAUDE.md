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

## Changelog Highlights

- **09.02.2026**: MessageRequested Event-Signatur von `Action<string>` zu `Action<string, string>` (Titel, Nachricht) in allen 10 ViewModels korrigiert (Convention-konform). Localization-Key "Info" in 6 .resx + Designer ergaenzt.
- **08.02.2026**: Game Juice (Floating-Text "Feierabend!" + Confetti + Ueberstunden)
- **07.02.2026**: Kalender Status-Overlay, Rewarded Ads (3 Placements), Android Export Fix (FileProvider)
