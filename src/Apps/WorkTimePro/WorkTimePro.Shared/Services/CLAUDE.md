# Services — Zeiterfassung, Export, Datenbank & Benachrichtigungen

13 Service-Interfaces + Implementierungen. Alle als Singleton im DI registriert.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Interface | Implementierung | Zweck |
|-----------|----------------|-------|
| `IDatabaseService` | `DatabaseService` | SQLite (WAL-Modus, alle 11 Tabellen, Indizes, Settings-Cache) |
| `ITimeTrackingService` | `TimeTrackingService` | CheckIn/Out, Pausen, `LiveDataSnapshot`, Mitternachts-Übergang |
| `ICalculationService` | `CalculationService` | Netto-Arbeitszeit, Auto-Pause, Saldo, §3 ArbZG Compliance |
| `IExportService` | `ExportService` | PDF (PdfSharpCore), Excel (ClosedXML), CSV |
| `ICalendarExportService` | `CalendarExportService` | ICS (RFC 5545, importierbar in Google/Apple/Outlook) |
| `IVacationService` | `VacationService` | 9 Abwesenheits-Typen (DayStatus-Subset), Resturlaub, Übertrag |
| `IHolidayService` | `HolidayService` | DE (16 BL), AT (9 BL), CH (12 Kantone) |
| `IProjectService` | `ProjectService` | Projekte: CRUD + Stunden-Aggregation aus TimeEntry |
| `IShiftService` | `ShiftService` | Schichtplanung: wiederkehrende Muster + Einzelzuweisungen |
| `IEmployerService` | `EmployerService` | Arbeitgeber: Default-Flag, Stunden-Aggregation |
| `IBackupService` | `BackupService` | JSON-Backup/Restore mit Safety-Backup, BulkRestore |
| `INotificationService` | `DesktopNotificationService` (Shared) / `AndroidNotificationService` (in `.Android`) | Plattform-abstrakt |
| `IReminderService` | `ReminderService` | 5 Reminder-Typen, subscribed auf `StatusChanged` |

## DatabaseService — Architektur

- **WAL-Modus** (`SharedCache + WAL`): Gleichzeitige Reads während Writes, reduziert "SQLite busy"-
  Fehler bei parallelen Pfaden (Auto-Pause-Recalculation + Note-Debounce + Live-Updates).
- **`_settingsCache`** (mit Lock): `WorkSettings` werden selten geändert aber pro Tab/Tick häufig
  gelesen. Cache wird in `SaveSettingsAsync` invalidiert.
- **`GetDatabaseAsync()`**: Alle Aufrufer warten auf denselben init-Task — keine Race-Condition.
  Bei Faulted-Task: Retry möglich (Task wird genullt).
- **Batch-Query für Export**: `GetTimeEntriesForWorkDaysAsync(List<int>)` verhindert N+1 beim Export.
- **LockMonth / SetDefaultEmployer**: Je 1–2 SQL-UPDATEs statt "lade alle, iterate, save each".

## TimeTrackingService — DateTime-Konvention

Arbeitszeiten (CheckIn/Out, Pausen) verwenden **`DateTime.Now`** (Ortszeit), weil alle
Anzeigen lokale Uhrzeiten erwarten. Audit-Timestamps (`CreatedAt`/`ModifiedAt`) verwenden `DateTime.UtcNow`.

`GetLiveDataSnapshotAsync()` liefert WorkTime, PauseTime, TimeUntilEnd + Today-WorkDay in
**einem** Snapshot (3 DB-Queries statt 5+) — verhindert Query-Sturm im 1s-Timer.

`_cachedWorkTimeTicks` als `long` + `Interlocked`-Zugriff: `TimeSpan` (8 Bytes) ist auf 32-Bit
nicht atomar lesbar — daher als Ticks gespeichert.

Mitternachts-Übergang: `LoadStatusAsync` prüft auch den gestrigen WorkDay (Nachtarbeit).

## CalculationService — Überstunden & §3 ArbZG

- **Netto-Arbeitszeit** = Gesamt − Pausen − Auto-Pause-Ergänzung (wenn < gesetzlich).
- **Saldo** = Netto − Soll (aus `WorkSettings.GetHoursForDay()` — gecachtes JSON-Dictionary).
- **Tages-Saldo nach DayStatus (zentral in `WorkDay.CalculateBalance`/`EffectiveTargetMinutes`):**
  Tage mit erfasster Arbeit zählen IMMER Ist−Soll (auch Dienstreise/Schulung mit Stempelung).
  Ohne erfasste Arbeit gilt: bezahlte/unbezahlte **Abwesenheit** (Urlaub/Krank/Feiertag/Sonderurlaub/
  Dienstreise/Schulung/Unbezahlt) = **Saldo 0** (Tag erfüllt, `IsFulfilledAbsence`); **Überstundenabbau/
  Zeitausgleich** (`OvertimeCompensation`/`CompensatoryTime`) + nicht gestempelter Arbeitstag = **−Soll**
  (bauen Plus ab bzw. fehlende Arbeit). Diese reinen statischen Methoden sind die **einzige Wahrheit** —
  `RecalculateWorkDayAsync`, `CalculateWeek/MonthAsync` (effektives Soll im Tag-Loop), `DayDetailViewModel.
  SelectStatusAsync` und `VacationService` nutzen sie, damit `GetTotalOvertimeMinutesAsync`
  (`SUM(BalanceMinutes)`), Monats-/Wochen-Saldo und der Statistics-Overtime-Chart denselben Wert liefern.
  (Vorher zählte ein Urlaubstag fälschlich als −Soll → Urlaubswoche = −40h.)
- **§3 ArbZG**: 6-Monats-Durchschnitt ≤ 8h/Tag über Mo–Sa (Sonntage ausgeschlossen),
  Vacation/Sick zählen als 0h, Mindest-Schwelle 60 Werktage.
- **Rundungs-Konvention (bewusste Entscheidung):** Der kumulative Saldo
  (`GetCumulativeBalanceAsync` → Summe `BalanceMinutes`) läuft auf der **gerundeten
  Abrechnungsbasis** (`ActualWorkMinutes` nach `RoundingMinutes`) — das ist die Zahl,
  die der Nutzer pro Tag sieht und abrechnet. NUR die gesetzlichen Prüfungen (§3/§4 ArbZG)
  nutzen `UnroundedWorkMinutes`. Zeitwerte runden kaufmännisch
  (`MidpointRounding.AwayFromZero`), nicht Banker's Rounding.

## HolidayCalculator — reine Feiertagsberechnung

`static class` (kein Interface, kein DI). Berechnet Feiertage in-memory für DE (16 BL),
AT (9 BL), CH (12 Kantone) und setzt `Region`/`Year` auf jedem Eintrag. Bewusst
abhängigkeitsfrei, damit sowohl `HolidayService` (mit Cache) als auch
`DatabaseService.IsHolidayAsync` sie nutzen können **ohne DI-Zyklus**.

## ExportService — Anti-N+1

`GetTimeEntriesForWorkDaysAsync(List<int>)`: Batch-Query für alle WorkDay-IDs auf einmal.
NIEMALS N einzelne `GetTimeEntriesAsync(id)` im Export-Loop.

Android-Share via `IFileShareService` (FileProvider `com.meineapps.worktimepro.fileprovider`).

## BackupService — Deep-Clone + Safety

- `CreateBackupDataAsync()` klont per JSON-Roundtrip (entkoppelt vom DB-Tracking).
- `ImportBackupFromFileAsync()`: Safety-Backup VOR Restore, Rollback bei Fehler.
- `BulkRestoreAsync()`: Batch-Insert in einer Transaction (5–10× schneller als einzelne Saves).
- `DateTime.TryParse` mit `CultureInfo.InvariantCulture` — NICHT `null` (= CurrentCulture je nach Gerät).

## ReminderService — 5 Typen

1. **Morgen-Erinnerung** (Zeit einzustempeln)
2. **Abend-Erinnerung** (Noch am Arbeiten?)
3. **Pausen-Erinnerung** (Nach X Stunden ohne Pause)
4. **Überstunden-Warnung** (Über `OvertimeWarningHours` pro **Tag** — NICHT pro Woche!)
5. **Wochenzusammenfassung** (Montag Morgen)

Subscribed auf `ITimeTrackingService.StatusChanged`. `RescheduleAsync()` bei Settings-Änderungen.

**Prozessbindung (bewusste Design-Wahl):** Morgen/Abend/Weekly laufen über `AlarmManager`
(überleben Prozess-Tod und Reboot via BootReceiver). **Pausen- und Überstunden-Reminder**
laufen dagegen über In-Memory-Timer (`Task.Delay`) — sie feuern nur, solange der Prozess
lebt ("best effort"). Bei App-Kill durch das System entfallen sie bis zum nächsten Start.

**Exact-Alarm-Permission (Android 13+):** `SCHEDULE_EXACT_ALARM` wird ab API 33 nicht mehr
automatisch gewährt → ohne Nutzer-Aktion läuft die Planung im inexakten Fallback
(`SetAndAllowWhileIdle`, Verzögerung im Doze möglich). Beim Aktivieren eines Reminders prüft
`SettingsViewModel.WarnIfNotificationsDisabled` daher `CanScheduleExactAlarms()` und führt
den Nutzer per `RequestExactAlarmPermission()` (Settings-Intent
`ACTION_REQUEST_SCHEDULE_EXACT_ALARM`) zur System-Einstellung "Wecker und Erinnerungen".

## DesktopNotificationService

- Windows: PowerShell-Toast via `Base64-EncodedCommand` (Injection-sicher).
- Linux: `notify-send`.

## Gotchas

- **OvertimeWarningHours vs. MaxDailyHours:** `ReminderService.StartOvertimeTimer` nutzt
  `settings.OvertimeWarningHours` (double) — NICHT `settings.MaxDailyHours` (int). RESX-Labels
  in allen 6 Sprachen lauten "Stunden/Tag" (nicht "Woche"). Label nicht verwechseln.
- **ProjectService: GetProjectHoursAsync:** Aggregiert aus `TimeEntry.ProjectId` (CheckIn/CheckOut-
  Paare). Die alte `ProjectTimeEntry`-Tabelle ist entfernt. NIEMALS wieder einführen.
- **BackupService: InvariantCulture:** `DateTime.TryParse` mit `CultureInfo.InvariantCulture` —
  NICHT `null` (= CurrentCulture kann je nach Gerät variieren).
- **LockMonth / UnlockMonth N+1:** Nutzt je 1 SQL-UPDATE. NIEMALS auf "lade alle, iterate, save each" zurückfallen.
- **WorkSettings Caching:** `WorkSettings.WorkDaysArray` ist gecacht (String-Vergleich).
  `GetHoursForDay()` cached deserialisiertes JSON-Dictionary. Direktes Parsen bei jedem 1s-Timer-Tick wäre messbar.
