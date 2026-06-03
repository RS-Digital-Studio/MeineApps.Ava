# Models — SQLite-Entitäten & Enums

SQLite-Modelle (sqlite-net-pcl Attribute), Enums und Hilfsklassen.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `WorkDay.cs` | Zentrales Modell: Datum, Status, Soll/Ist-Minuten, Pausen, Saldo, Lock-Flag, Notiz. `[Ignore]`-Properties für berechnete Anzeige-Werte (TimeSpan, BalanceDisplay, BalanceColor, StatusIconKind u.a.). |
| `TimeEntry.cs` | CheckIn/CheckOut-Eintrag: `Type` (CheckIn/CheckOut), `Timestamp` (Ortszeit), optionale `ProjectId`/`EmployerId`. `[Indexed]` auf `WorkDayId`. |
| `PauseEntry.cs` | Pausen-Eintrag: `Start`/`End` (nullable), `Type` (Manual/Auto). FK auf `WorkDayId`. |
| `VacationEntry.cs` | Urlaubs-Eintrag: `StartDate`/`EndDate`, `Days`, `Type` (DayStatus: Vacation/Sick/SpecialLeave/UnpaidLeave), `IsApproved`, optionaler Text. |
| `VacationQuota.cs` | Urlaubs-Kontingent pro Jahr + optionaler `EmployerId`. Berechnete Properties: `AvailableDays`, `RemainingDays`, `UsedPercent`. |
| `HolidayEntry.cs` | Feiertag: `Date`, `Name`, `Region` (DE-BY etc.), `IsNational`, gespeichert je Jahr+Region-Kombination. |
| `WorkSettings.cs` | Singleton-Zeile (FirstOrDefault + Insert wenn leer). Enthält Soll-Stunden, Stundenlohn, Reminder-Zeiten, `WorkDays` (kommagetrennte Wochentage), `DailyHoursPerDay` (JSON: individuelle Tagesstunden). Gecachte `WorkDaysArray`-Property und `GetHoursForDay()`-Methode. |
| `WorkWeek.cs` | Aggregation einer Woche: Liste von WorkDays, berechnete Saldo-/Fortschritts-Properties. |
| `WorkMonth.cs` | Aggregation eines Monats: Liste von WorkWeeks + Liste von WorkDays, kumulierter Saldo. |
| `Project.cs` | Projekt: `Name`, `Color`, `IsActive` (Soft-Delete). |
| `Employer.cs` | Arbeitgeber: `Name`, `IsDefault`-Flag, `WeeklyHours`, `Color`. Berechnete Properties: `Initials` (Avatar), `DailyHours`. |
| `ShiftPattern.cs` | Wiederkehrendes Schichtmuster: `Name`, `ShiftType`, Start-/Endzeit (Ticks), `BreakMinutes`, `Color`. Enthält auch `ShiftAssignment` (DB-Tabelle `ShiftAssignments`): ordnet ein `ShiftPattern` einem Datum zu. |
| `ShiftDayItem.cs` | UI-Hilfsmodell für Schichtplan-Kalender (kein DB-Mapping, `ObservableObject`). |
| `Enums.cs` | Alle App-Enums (siehe unten). |
| `../AppColors.cs` | Statische Farbkonstanten für ViewModels (Namespace `WorkTimePro`, ersetzt Magic-Strings). |

## Enums

| Enum | Werte (Anzahl) |
|------|---------------|
| `DayStatus` | WorkDay, Weekend, Vacation, Holiday, Sick, UnpaidLeave, HomeOffice, BusinessTrip, OvertimeCompensation, SpecialLeave, Training, CompensatoryTime (12) |
| `TrackingStatus` | Idle, Working, OnBreak (3) |
| `EntryType` | CheckIn, CheckOut (2) |
| `PauseType` | Manual, Auto (2) |
| `ShiftType` | Early, Late, Night, Normal, Flexible, Off (6) |
| `ExportFormat` | PDF, CSV, Excel (3) |
| `StatisticsPeriod` | Week, Month, Quarter, Year, Custom (5) |
| `CloudProvider` | None, GoogleDrive, OneDrive (3 — noch nicht aktiv) |

## DateTime-Konvention (WorkTimePro-spezifisch)

| Verwendung | DateTime-Typ | Begründung |
|------------|-------------|------------|
| CheckIn/Out, Pausen | `DateTime.Now` (Ortszeit) | Alle Anzeigen sind lokale Uhrzeiten |
| Audit-Timestamps (CreatedAt/ModifiedAt) | `DateTime.UtcNow` | Eindeutig über Zeitzonen |
| Export-Footer, Backup-Dateinamen | Ortszeit | Menschenlesbar |
| Persistenz-Format | ISO 8601 `"O"` + `DateTimeStyles.RoundtripKind` beim Parse | |

## AppColors

Statische Klasse (`../AppColors.cs`, Namespace `WorkTimePro`) mit Hex-Strings für ViewModels
(kein XAML-Zugriff aus VMs). `SolidColorBrush.Parse(AppColors.X)` in `MainViewModel` als
static readonly Fields gecacht (verhindert Parse im 1s-Timer).

**Wichtige Gruppen:**
- Status: `StatusIdle` (#9E9E9E), `StatusActive` (#4CAF50), `StatusPaused` (#FF9800)
- Gradient-Varianten: `StatusIdleLight`, `StatusActiveLight`, `StatusPausedLight` (für SkiaGradientRing)
- Balance: `BalancePositive` (#4CAF50), `BalanceNegative` (#F44336)
- Kalender-Heatmap: `HeatmapLight` .. `HeatmapOvertime` (5 Abstufungen)
- Kalender-Hintergrund/Text: `CalendarDark*` / `CalendarLight*` (8 Konstanten)
- Premium-Status: `PremiumActive` .. `PremiumFree` (4)
- Chart-Farben: `ChartColors[]` (10 Einträge)

## StatusIconKind

`WorkDay.StatusIconKind` liefert `MaterialIconKind` (kein MDI-Font-String):
`DayStatus.WorkDay → Briefcase`, `Vacation → Beach`, `Sick → Thermometer`, etc.
Alle Views binden auf diese Property — kein direktes Enum-zu-Icon-Mapping im XAML.
`TimeEntry` und `VacationEntry` haben ebenfalls `TypeIconKind` (MaterialIconKind).

## Gotcha — Kein DB-seitiger UNIQUE auf TimeEntry

`TimeEntry` hat nur `[Indexed]` auf `WorkDayId`, **keinen** UNIQUE-Index auf
`(WorkDayId, Timestamp, Type)`. Duplikat-Schutz bei Doppel-Taps muss im Service
(`ITimeTrackingService`) implementiert werden. `InsertAsync()` wirft bei versehentlichen
Duplikaten keine DB-Exception — daher vor dem Insert prüfen.
