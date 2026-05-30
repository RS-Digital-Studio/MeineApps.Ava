# Models — SQLite-Entitäten & Enums

SQLite-Modelle (sqlite-net-pcl Attribute), Enums und Hilfsklassen.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `WorkDay.cs` | Zentrales Modell: Datum, Status, Soll/Ist-Minuten, Pausen, Saldo, Lock-Flag, Notiz. `[Ignore]`-Properties für berechnete Anzeige-Werte. |
| `TimeEntry.cs` | CheckIn/CheckOut-Eintrag: `Type` (CheckIn/CheckOut), `Timestamp` (Ortszeit!), optionale `ProjectId`. UNIQUE-Index auf `(WorkDayId, Timestamp, Type)`. |
| `PauseEntry.cs` | Pausen-Eintrag: `Start`/`End` (nullable), `Type` (Manual/Auto). FK auf `WorkDayId`. |
| `VacationEntry.cs` | Urlaubs-Eintrag: `Date`, `Status` (VacationStatus), `EmployerId`, optionaler Text. |
| `VacationQuota.cs` | Urlaubs-Kontingent pro Jahr + optionaler `EmployerId`. |
| `HolidayEntry.cs` | Feiertag: `Date`, `Name`, `Region` (DE-BY etc.), gespeichert je Jahr+Region-Kombination. |
| `WorkSettings.cs` | Singleton-Zeile (FirstOrDefault + Insert wenn leer). Enthält Soll-Stunden, Stundenlohn, Reminder-Zeiten, WorkDays-JSON, CloudProvider. Gecachtes `GetHoursForDay()`. |
| `WorkDay.cs` (computed) | `[Ignore]`-Properties: `TargetWorkTime`, `ActualWorkTime`, `Balance`, `BalanceDisplay`, `BalanceColor` (via `AppColors`), `StatusIconKind` (MaterialIconKind). |
| `WorkWeek.cs` | Aggregation einer Woche: Liste von WorkDays, berechnete Saldo-Summe. |
| `WorkMonth.cs` | Aggregation eines Monats: Liste von WorkWeeks + Feiertagsliste. |
| `Project.cs` | Projekt: `Name`, `Color`, `IsActive` (Soft-Delete). |
| `Employer.cs` | Arbeitgeber: `Name`, `IsDefault`-Flag. `SetDefaultAsync` via 2 SQL-UPDATEs. |
| `ShiftPattern.cs` | Wiederkehrendes Schichtmuster: `Name`, `ShiftType`, Wiederholungsregel. |
| `ShiftDayItem.cs` | UI-Hilfsmodell für Schichtplan-Kalender (kein DB-Mapping). |
| `Enums.cs` | Alle App-Enums (siehe unten). |
| `../AppColors.cs` | Statische Farbkonstanten für ViewModels (ersetzt Magic-Strings). |

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
- Balance: `BalancePositive` (#4CAF50), `BalanceNegative` (#F44336)
- Kalender-Heatmap: `HeatmapLight` .. `HeatmapOvertime` (5 Abstufungen)
- Premium-Status: `PremiumActive` .. `PremiumFree` (4)
- Chart-Farben: `ChartColors[]` (10 Einträge)

## StatusIconKind

`WorkDay.StatusIconKind` liefert `MaterialIconKind` (kein MDI-Font-String mehr):
`DayStatus.WorkDay → Briefcase`, `Vacation → Beach`, `Sick → Thermometer`, etc.
Alle Views binden auf diese Property — kein direktes Enum-zu-Icon-Mapping im XAML.

## Gotcha — TimeEntry UNIQUE-Index

`TimeEntry` hat UNIQUE auf `(WorkDayId, Timestamp, Type)` — Anti-Duplikat-Schutz bei
Doppel-Taps. `InsertAsync()` kann bei Violation fehlschlagen. NIEMALS `InsertOrReplaceAsync`
für TimeEntries verwenden (würde Daten überschreiben).
