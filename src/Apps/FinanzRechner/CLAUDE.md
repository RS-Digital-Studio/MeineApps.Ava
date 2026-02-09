# FinanzRechner (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Finanz-App mit Ausgaben-Tracking, Budget-Verwaltung, Dauerauftraegen und 5 Finanz-Rechnern.

**Version:** 2.0.0 | **Package-ID:** com.meineapps.finanzrechner | **Status:** Geschlossener Test

## Features

- **4 Tabs**: Home (Dashboard + Quick-Add), Tracker, Statistics, Settings
- **Expense Tracking**: CRUD mit Filter/Sort, Undo-Delete, Kategorie-Icons
- **Budget Management**: Budget-Limits pro Kategorie, Fortschrittsanzeige, Alert-Levels
- **Recurring Transactions**: Dauerauftraege mit Auto-Processing bei App-Start
- **5 Finanz-Rechner**: CompoundInterest, SavingsPlan, Loan, Amortization, Yield
- **Charts**: LiveCharts (ProgressBar, PieChart, LineSeries)
- **Export**: CSV + PDF (PdfSharpCore), plattformspezifisches File-Sharing

## App-spezifische Services

- **IExpenseService / ExpenseService**: SQLite CRUD (Expense, Budget, RecurringTransaction Models)
- **IExportService / ExportService**: CSV + PDF Export mit optionalem targetPath Parameter
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
- Calculator-ScrollView (5 kompakte Karten, farbiger Accent-Balken)

### Game Juice
- **FloatingText**: Quick-Add (+/- Betrag, income=gruen, expense=rot)
- **Celebration**: Confetti bei Zielerreichung (Budget-Analyse abgeschlossen)

## Changelog (Highlights)

- **08.02.2026**: FloatingTextOverlay + CelebrationOverlay (Game Juice)
- **07.02.2026**: 4 Rewarded Ad Features, Android FileProvider Export, HomeView Redesign
- **06.02.2026**: Calculator Views Redesign, Export mit File-Dialog + Feedback, vollstaendige Lokalisierung
