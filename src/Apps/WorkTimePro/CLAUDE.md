# WorkTimePro (Avalonia)

## Status
**Version:** 2.0.0 | **Portiert von:** MAUI v1.0.8 | **Build:** Erfolgreich (Shared + Desktop + Android)

## Architektur

```
WorkTimePro/
├── WorkTimePro.Shared/          # net10.0 - Shared Code
│   ├── App.axaml(.cs)           # DI + App Startup
│   ├── Models/                  # 12 Models (TimeEntry, WorkDay, PauseEntry, etc.)
│   ├── ViewModels/              # 10 ViewModels
│   ├── Views/                   # 12 Views (axaml + cs)
│   ├── Services/                # 10 Interfaces + 10 Implementierungen
│   ├── Converters/              # AdditionalConverters, StatusToVisibilityConverter
│   ├── Helpers/                 # Icons.cs (MDI glyphs)
│   └── Resources/Strings/      # 6 Sprachen (resx)
├── WorkTimePro.Desktop/         # win-x64, linux-x64
└── WorkTimePro.Android/         # net10.0-android, com.meineapps.worktimepro
```

## Features
- Zeiterfassung (Check-in/out, Pausen, Auto-Pause)
- Tages-/Wochen-/Monats-/Jahresuebersicht
- Kalender-Heatmap
- Statistiken mit LiveCharts
- Export (PDF, Excel, CSV) - PdfSharpCore + ClosedXML
- Urlaubsverwaltung
- Feiertage (16 Bundeslaender)
- Projekte & Arbeitgeber
- Schichtplanung
- 4 Themes, 6 Sprachen
- Premium (Werbefrei + Export)

## Abhaengigkeiten
- MeineApps.Core.Ava + Core.Premium.Ava + MeineApps.UI
- sqlite-net-pcl, SQLitePCLRaw.bundle_green
- LiveChartsCore.SkiaSharpView.Avalonia
- PdfSharpCore, ClosedXML
- Material.Avalonia, Material.Icons.Avalonia, DialogHost.Avalonia

## Services (DI in App.axaml.cs)
| Interface | Implementierung | Scope |
|-----------|----------------|-------|
| IDatabaseService | DatabaseService | Singleton |
| ICalculationService | CalculationService | Singleton |
| ITimeTrackingService | TimeTrackingService | Singleton |
| IExportService | ExportService | Singleton |
| IVacationService | VacationService | Singleton |
| IHolidayService | HolidayService | Singleton |
| IProjectService | ProjectService | Singleton |
| IShiftService | ShiftService | Singleton |
| IEmployerService | EmployerService | Singleton |
| ICalendarSyncService | CalendarSyncService | Singleton |
| IBackupService | BackupService | Singleton |

## ViewModels
| ViewModel | View | Beschreibung |
|-----------|------|-------------|
| MainViewModel | MainView/TodayView | Haupt-Tab + Timer |
| WeekOverviewViewModel | WeekOverviewView | Wochenuebersicht |
| CalendarViewModel | CalendarView | Kalender-Heatmap |
| StatisticsViewModel | StatisticsView | Charts + Tabelle |
| SettingsViewModel | SettingsView | Einstellungen |
| DayDetailViewModel | DayDetailView | Tagesdetail |
| MonthOverviewViewModel | MonthOverviewView | Monatsuebersicht |
| YearOverviewViewModel | YearOverviewView | Jahresuebersicht |
| VacationViewModel | VacationView | Urlaubsverwaltung |
| ShiftPlanViewModel | ShiftPlanView | Schichtplanung |

## Migration Notes
- Shell Navigation -> NavigationRequested Event Pattern
- MainThread -> Avalonia.Threading.Dispatcher.UIThread
- Shell.Current.DisplayAlert -> Debug.WriteLine (Fallback-Logik)
- Browser.Default.OpenAsync -> Process.Start mit UseShellExecute
- ThemeType -> AppTheme
- Syncfusion PDF -> PdfSharpCore
- Android Widget nicht portiert (Avalonia hat kein Widget-System)

## AVLN2000 Compiled Binding Fixes (06.02.2026)
Alle fehlenden Properties/Commands fuer compiled bindings in AXAML Views hinzugefuegt:
- **MainViewModel**: Tab-Navigation (IsTodayActive etc., SelectXxxTabCommand), StatusIconKind (MaterialIconKind), IsWorking, HasCheckedIn
- **SettingsViewModel**: Theme-Selection (IsMidnightSelected etc., SelectThemeCommand), Language-Selection (IsGermanSelected etc., SelectLanguageCommand), MorningReminderTimeDisplay, EveningReminderTimeDisplay, dynamische AppVersion
- **CalendarViewModel**: CalendarWeeks (Gruppierte Kalender-Tage fuer nested ItemsControl)
- **DayDetailViewModel**: StatusIconKind, HasWarnings, HasNoTimeEntries, HasNoPauseEntries
- **WeekOverviewViewModel**: HasVacationDays, HasSickDays
- **MonthOverviewViewModel**: GoBackCommand
- **YearOverviewViewModel**: GoBackCommand, AverageHoursPerDayDisplay, VacationDaysTakenDisplay, SickDaysDisplay
- **StatisticsViewModel**: IsWeekSelected/IsMonthSelected/IsQuarterSelected/IsYearSelected, HasPauseChartData, HasNoTableData
- **ShiftPlanViewModel**: GoBackCommand, HasNoPatterns, PatternStartTimeDisplay, PatternEndTimeDisplay, SelectPatternCommand
- **ShiftDayItem**: TodayBorderThickness (Avalonia.Thickness), DayOpacity
- **VacationViewModel**: GoBackCommand, CalculatedDaysDisplay, HasNoVacations, VacationTypeItem Klasse (statt ValueTuple)
- **TimeEntry Model**: TypeIconKind (MaterialIconKind), HasNote
- **VacationEntry Model**: TypeIconKind (MaterialIconKind), HasNote
- **WorkDay Model**: DayName, DateShortDisplay

## Code Review Cleanup (06.02.2026)
Alle Verbesserungen aus anderen Apps (RechnerPlus, ZeitManager, etc.) angewendet:

### Debug.WriteLine -> MessageRequested Events
- Alle 10 ViewModels: `Debug.WriteLine` durch `MessageRequested?.Invoke()` ersetzt
- `public event Action<string>? MessageRequested` in allen VMs hinzugefuegt

### Hardcodierte deutsche Strings lokalisiert
- **StatisticsView.axaml**: Tabellen-Header (Datum, Status, Komm, Geh, Arbeit, Pause, Saldo) -> loc:Translate
- **StatisticsViewModel**: Chart-Labels (Ist-Stunden, Soll, Stunden, Taeglich, Kumuliert, Manuell, Avg Stunden, Wochentage) -> AppStrings
- **YearOverviewViewModel**: Monatsnamen (CultureInfo), Chart-Labels (Arbeitsstunden, Kum. Saldo, Stunden) -> AppStrings
- **ShiftPlanViewModel**: "Frei" -> AppStrings.ShiftOff, Tagesnamen (Mo-So) -> AppStrings.Mon-Sun
- 17 neue Lokalisierungs-Keys in 6 Sprachen (Table*, Chart*, ShiftOff)

### DateTime-Parsing Fix
- **BackupService**: `DateTime.TryParse` mit `DateTimeStyles.RoundtripKind` (2 Stellen)
- **CalendarSyncService**: `DateTime.TryParse` mit `DateTimeStyles.RoundtripKind` (1 Stelle)

### Sonstige Fixes
- **VacationView.axaml**: UnlockPremium Button Command-Binding hinzugefuegt (GoBackCommand)
- **MainViewModel**: NavigationRequested entfernt (CS0067 Warning behoben)
- **SettingsViewModel**: OpenArbZG/OpenHolidaysSource als synchrone void Methoden (korrekt)

## Deep Code Review Cleanup (06.02.2026)
Tiefgehender Review aller Services + ViewModels, alle verbliebenen Probleme behoben:

### Debug.WriteLine aus Services entfernt
- **BackupService**: 10 Debug.WriteLine entfernt, `using System.Diagnostics` entfernt
- **CalendarSyncService**: 9 Debug.WriteLine entfernt, `using System.Diagnostics` entfernt
- **ExportService**: 6 Debug.WriteLine entfernt (behaelt `using System.Diagnostics` wegen Process.Start)
- Alle unbenutzten `catch (Exception ex)` zu `catch (Exception)` geaendert (CS0168 Warnungen behoben)

### Hardcodierte Strings in Services lokalisiert
- **ExportService.GetStatusText()**: 12 deutsche DayStatus-Strings -> AppStrings (DayStatus_WorkDay, DayStatus_Weekend, etc.)
- **CalendarSyncService.FormatEventTitle()**: "Work:" -> AppStrings.WorkTime
- **CalendarSyncService.FormatEventDescription()**: "Work time:", "Target:", "Pause:", "thereof auto:", "Balance:", "Created by WorkTime Pro" -> AppStrings
- **CalendarSyncService.GetStatusName()**: 11 englische DayStatus-Strings -> AppStrings
- **CalendarSyncService.ExportVacationAsync()**: "Vacation" -> AppStrings.Vacation
- **CalculationService**: "Automatically added (legal requirement)" -> AppStrings.AutoPauseLegal

### Hardcodierte Strings in ViewModels lokalisiert
- **MainViewModel**: "Fehler bei/beim..." -> string.Format(AppStrings.ErrorLoading/ErrorGeneric), Status-Strings lokalisiert
- **SettingsViewModel**: "Settings saved", "Trial started", "Purchase successful", "Failed to open URL" etc. -> AppStrings
- **DayDetailViewModel**: "Fehler beim Laden", "Auto-pause" Meldungen -> AppStrings
- **CalendarViewModel**: "Fehler beim Laden", "Vacation entered" -> AppStrings
- **WeekOverviewViewModel**: "Fehler beim Laden" -> AppStrings.ErrorLoading
- **MonthOverviewViewModel**: "Fehler beim Laden" -> AppStrings.ErrorLoading
- **YearOverviewViewModel**: "LoadData error", "Premium feature", "Export error" -> AppStrings
- **StatisticsViewModel**: "Fehler beim Laden", "Export-Fehler", "Fehler bei Projekt/Arbeitgeber-Chart" -> AppStrings
- **VacationViewModel**: "LoadData error", "Premium feature", "No work days", "CarryOver", "No days to carry over" -> AppStrings
- **ShiftPlanViewModel**: 6 hardcodierte deutsche Fehlerstrings -> AppStrings.ErrorLoading/ErrorSaving/ErrorGeneric

### Placeholder-Texte entfernt
- **DayDetailViewModel**: 4 "implement platform-specific dialog" Meldungen -> TODO-Kommentare (waren Debug-Texte als user-facing MessageRequested)
- **DayDetailViewModel**: "DeleteEntry"/"DeletePause" Debug-Meldungen entfernt
- **MonthOverviewViewModel**: "LockMonth"/"UnlockMonth" Debug-Meldungen entfernt
- **VacationViewModel**: "EditQuota"/"DeleteVacation" Debug-Meldungen entfernt
- **StatisticsViewModel**: "Export requested" Debug-Meldung entfernt
- **DayDetailViewModel/YearOverviewViewModel/VacationViewModel**: "failed:" ContinueWith-Errors -> AppStrings.ErrorLoading

### Neue Lokalisierungs-Keys (8 neue in 6 Sprachen)
- ErrorLoading, ErrorSaving, ErrorGeneric, ErrorOpenUrl
- DayStatus_Training, DayStatus_CompensatoryTime
- CalendarCreatedBy, CalendarAutoBreak
- PdfExportPremium + ExportFailedMessage + RemainingDays in Designer.cs ergaenzt

### Views Cleanup
- **StatisticsView.axaml**: Hardcodierte Farbe `#7B1FA2` -> `DynamicResource PrimaryBrush` (theme-aware)

### Lokalisierungs-Key Fix
- **AppStrings.resx** (EN base): Fehlender `RemainingDays` Key ergaenzt (existierte in DE/ES/FR/IT/PT aber nicht in EN)

## Kalender-Tag-Auswahl mit Status-Eintrag (07.02.2026)
- **CalendarViewModel**: Overlay-System statt NavigationRequested fuer Tag-Auswahl
  - IsOverlayVisible, OverlayStartDate/EndDate, OverlaySelectedType, OverlayNote, OverlayCalculatedDays
  - SelectDayAsync oeffnet Overlay statt NavigationRequested zu feuern
  - SaveStatusCommand: Erstellt VacationEntry via VacationService
  - RemoveStatusCommand: Loescht VacationEntry oder resettet WorkDay-Status
  - CancelOverlayCommand: Schliesst Overlay
  - AvailableStatusTypes: 9 Status-Typen (Vacation, Sick, HomeOffice, BusinessTrip, SpecialLeave, UnpaidLeave, OvertimeCompensation, Training, CompensatoryTime)
  - IVacationService als neue Constructor-Dependency
- **CalendarDay**: StatusIconKind (MaterialIconKind) + HasStatusIcon Properties
  - Icons fuer alle 10 speziellen DayStatus-Werte (Beach, Thermometer, HomeAccount, Airplane, etc.)
- **CalendarView.axaml**: Status-Overlay + MaterialIcon statt Text-Emoji
  - StatusIcon von FontSize=8 TextBlock auf mi:MaterialIcon (12x12, PrimaryBrush)
  - Overlay mit Datum-Anzeige, Status-ComboBox, Von/Bis-DatePicker, Notiz, Speichern/Abbrechen/Entfernen
  - Vorhandener Status wird erkannt und vorausgewaehlt
- **Lokalisierung**: 2 neue Keys (SetStatus, DateRange) + WorkDaysFormat in Designer.cs
- Build: 0 Fehler

## Build
```bash
dotnet build src/Apps/WorkTimePro/WorkTimePro.Shared/WorkTimePro.Shared.csproj
dotnet build src/Apps/WorkTimePro/WorkTimePro.Desktop/WorkTimePro.Desktop.csproj
dotnet build src/Apps/WorkTimePro/WorkTimePro.Android/WorkTimePro.Android.csproj
```

## AppChecker Fixes (07.02.2026)
- **MainViewModel**: `_localization.LanguageChanged += OnLanguageChanged` abonniert (StatusDisplay, PauseButtonText, ShowDayDetailsText, TodayDateDisplay aktualisieren)
- **MainViewModel.Dispose()**: `_localization.LanguageChanged -= OnLanguageChanged` Unsubscribe ergaenzt
