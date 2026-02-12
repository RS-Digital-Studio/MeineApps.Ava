# FinanzRechner (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Finanz-App mit Ausgaben-Tracking, Budget-Verwaltung, Dauerauftraegen und 6 Finanz-Rechnern.

**Version:** 2.0.2 | **Package-ID:** com.meineapps.finanzrechner | **Status:** Geschlossener Test

## Features

- **4 Tabs**: Home (Dashboard + Quick-Add), Tracker, Statistics, Settings
- **Expense Tracking**: CRUD mit Filter/Sort, Undo-Delete, Kategorie-Icons
- **Budget Management**: Budget-Limits pro Kategorie, Fortschrittsanzeige, Alert-Levels
- **Recurring Transactions**: Dauerauftraege mit Auto-Processing bei App-Start (verpasste Zeitraeume werden nachgeholt)
- **6 Finanz-Rechner**: CompoundInterest, SavingsPlan, Loan, Amortization, Yield, Inflation
- **Charts**: LiveCharts (ProgressBar, PieChart, LineSeries)
- **Export**: CSV + PDF (PdfSharpCore), plattformspezifisches File-Sharing

## App-spezifische Services

- **IExpenseService / ExpenseService**: SQLite CRUD (Expense, Budget, RecurringTransaction Models)
- **IExportService / ExportService**: CSV + PDF Export mit optionalem targetPath Parameter und Datum-Range-Filterung
- **IFileDialogService / FileDialogService**: Avalonia StorageProvider.SaveFilePickerAsync
- **IFileShareService**: Plattformspezifisch (Desktop: Process.Start, Android: FileProvider + Intent.ActionSend)
- **CategoryLocalizationHelper**: Statische Kategorie-Namen/Icons/Farben pro Sprache

## Premium & Ads

### Ad-Placements (Rewarded)
1. **export_pdf**: PDF-Export (StatisticsView)
2. **export_csv**: CSV-Export (ExpenseTrackerView + StatisticsView)
3. **budget_analysis**: Monatsreport mit Kategorie-Breakdown + Spartipps (HomeView)
4. **extended_stats**: 24h-Zugang zu Quartal/Halbjahr/Jahr Statistiken (StatisticsView)

### Premium-Modell
- **Preis**: 3,99 EUR (`remove_ads`)
- **Vorteile**: Keine Ads, direkter Export, unbegrenzter Budget-Report, permanente erweiterte Statistiken

## Besondere Architektur

### Export-Logik
- **ExportService**: `GetExportDirectory()` gibt Android external-files-path zurueck
- **ShareFileAsync**: Nach Export wird `IFileShareService.ShareFileAsync()` aufgerufen
- **Fallback**: Android-Export faellt zurueck auf hardcodierte Pfade wenn FileDialog nicht verfuegbar

### Budget-Verwaltung
- **BudgetDisplayItem**: ObservableObject mit CategoryName Property (Sprachwechsel-faehig)
- **Auto-Processing**: `MainViewModel.OnAppearingAsync()` verarbeitet faellige Dauerauftraege bei App-Start

### HomeView Dashboard
- Hero-Header (Bilanz + Einnahmen/Ausgaben als Pill-Chips)
- Budget-Status (Gesamt-ProgressBar + Top-3 Kategorien)
- Quick-Add FAB (Overlay mit Betrag, Beschreibung, Kategorie-Chips)
- Recent Transactions (3 neueste mit Kategorie-Icon)
- Calculator-Grid (6 kompakte Karten im 2x3 Grid, farbiger Accent-Balken)

### SettingsView Events
- **BackupCreated**: Datei teilen via IFileShareService
- **RestoreFileRequested**: StorageProvider.OpenFilePickerAsync fuer JSON-Restore → zeigt Merge/Replace-Dialog (ShowRestoreConfirmation Overlay)
- **OpenUrlRequested**: URL im Standardbrowser oeffnen (Process.Start)
- **FeedbackRequested**: mailto-Link fuer Feedback-E-Mail

### Restore Merge/Replace Dialog
- Nach File-Picker wird `OnRestoreFileSelected(filePath)` aufgerufen → setzt ShowRestoreConfirmation=true
- Dialog-Overlay in SettingsView.axaml mit Merge-Button (Primary) und Replace-Button (Secondary)
- RestoreMergeCommand → ProcessRestoreFileAsync(path, merge:true)
- RestoreReplaceCommand → ProcessRestoreFileAsync(path, merge:false)
- CancelRestoreCommand → Dialog schliessen, IsBackupInProgress zuruecksetzen
- RESX-Keys: RestoreQuestion, RestoreMerge, RestoreReplace, RestoreMergeDesc, RestoreReplaceDesc, TotalBudget

### Game Juice
- **FloatingText**: Quick-Add (+/- Betrag, income=gruen, expense=rot)
- **Celebration**: Confetti bei Budget-Analyse (CelebrationRequested Event in MainViewModel)

## Changelog (Highlights)

- **11.02.2026 (4)**: Optimierungs-Durchlauf Batch 4-6: Atomares Schreiben (temp+rename), Auto-Backup (5 Versionen), DateTime.Today/UtcNow konsistent, Budget-Notification-Persistenz, Trend-Abfragen optimiert (6→1), Recurring nur 1x/Tag, stille Fehler loggen, Fire-and-forget try-catch. CurrencyHelper zentral (alle EUR-Formatierungen), CategoryLocalizationHelper erweitert (Icons+Farben), CSV InvariantCulture, Biweekly-Intervall, englische Kommentare→deutsch. Budget-Kategorie im Edit deaktiviert, Gesamt-Monatsbudget-ProgressBar, Amortization-Tabelle ausklappbar, Live-Berechnung Debouncing (300ms) in allen 6 Rechnern, Inflationsrechner (6. Rechner), Restore Merge/Replace Dialog
- **11.02.2026 (3)**: Inflationsrechner als 6. Finanzrechner: FinanceEngine.CalculateInflation + InflationResult, InflationViewModel mit Chart (Kaufkraft-Verlauf als rote LineSeries), InflationView (orange Gradient #F97316/#EA580C, CurrencyUsd Icon), MainViewModel-Integration (ActiveCalculatorIndex=5), HomeView 2x3 Grid-Karte, DI-Registrierung, 8 neue RESX-Keys in allen 6 Sprachen (CalcInflation, CurrentAmount, AnnualInflationRate, FutureValue, PurchasingPower, PurchasingPowerLoss, ChartPurchasingPower, LossPercent)
- **11.02.2026 (2)**: Restore Merge vs Replace Dialog: Nach File-Picker zeigt SettingsView einen Overlay-Dialog mit Merge/Replace/Cancel Buttons. 6 neue RESX-Keys in allen 6 Sprachen. ProcessRestoreFileAsync von public auf private geaendert.
- **11.02.2026**: Bugfix-Review: Dauerauftraege nachholen bei laengerem Nicht-Benutzen, PremiumPrice 3.99 in allen 6 RESX, SettingsView Events verdrahtet (Backup/Restore/URL/Feedback), CSV/PDF-Export mit Datum-Range statt nur einem Monat, Undo-Delete Queue statt Einzelvariable, ClearAllExpensesAsync mit Semaphore, CelebrationRequested implementiert, KW lokalisiert, CategoryStatistic/BudgetStatus lokalisiert, InflationResult entfernt (toter Code), DateTime.UtcNow in Backup-Metadaten, hardcodierte Error-Strings lokalisiert, doppeltes Laden bei PreviousMonth/NextMonth behoben
- **08.02.2026**: FloatingTextOverlay + CelebrationOverlay (Game Juice)
- **07.02.2026**: 4 Rewarded Ad Features, Android FileProvider Export, HomeView Redesign
- **06.02.2026**: Calculator Views Redesign, Export mit File-Dialog + Feedback, vollstaendige Lokalisierung
