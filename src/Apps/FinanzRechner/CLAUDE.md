# FinanzRechner (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Finanz-App mit Ausgaben-Tracking, Budget-Verwaltung, Dauerauftraegen und 6 Finanz-Rechnern.

**Version:** 2.0.6 | **Package-ID:** com.meineapps.finanzrechner | **Status:** Geschlossener Test

## Features

- **4 Tabs**: Home (Dashboard + Quick-Add), Tracker, Statistics, Settings
- **Expense Tracking**: CRUD mit Filter/Sort, Undo-Delete, Kategorie-Icons
- **Budget Management**: Budget-Limits pro Kategorie, Fortschrittsanzeige, Alert-Levels
- **Recurring Transactions**: Dauerauftraege mit Auto-Processing bei App-Start (verpasste Zeitraeume werden nachgeholt, max 365 Iterationen pro Dauerauftrag)
- **6 Finanz-Rechner**: CompoundInterest, SavingsPlan, Loan, Amortization, Yield, Inflation
- **Charts**: Komplett SkiaSharp-basiert (DonutChart, TrendLine, StackedArea, AmortizationBar, Sparkline, MiniRing, LinearProgress, BudgetGauge) - KEIN LiveCharts
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
- **Über-Budget-Anzeige**: Prozent >100% erlaubt, ProgressBar+Text werden rot (CSS-Klasse `.overLimit`)

### Cache-Invalidierung (Tab-Wechsel)
- **StatisticsViewModel**: `InvalidateCache()` + `_isDataStale` Flag → lädt nur bei Änderungen neu
- **ExpenseTrackerViewModel**: `InvalidateCache()` + `DataChanged` Event → benachrichtigt MainViewModel
- **BudgetsViewModel**: `DataChanged` Event nach Save/Delete → benachrichtigt MainViewModel
- **RecurringTransactionsViewModel**: `DataChanged` Event nach Save/Delete → benachrichtigt MainViewModel
- **MainViewModel**: `_isHomeDataStale` Flag, lauscht auf `DataChanged` von ExpenseTrackerVM, BudgetsVM, RecurringTransactionsVM

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

### SkiaSharp-Visualisierungen (LiveCharts komplett ersetzt)

| Datei | Zweck |
|-------|-------|
| `Graphics/BudgetGaugeVisualization.cs` | Halbkreis-Tachometer (Grün→Gelb→Rot) für Gesamt-Budget |
| `Graphics/SparklineVisualization.cs` | Mini-Sparkline mit Gradient-Füllung für 30-Tage-Ausgaben-Trend |
| `Graphics/BudgetMiniRingVisualization.cs` | Kompakte Mini-Ringe für Budget-Kategorien-Übersicht |
| `Graphics/TrendLineVisualization.cs` | 2 Spline-Kurven (Einnahmen/Ausgaben) mit Gradient-Füllung |
| `Graphics/StackedAreaVisualization.cs` | 2 gestapelte Flächen (CompoundInterest, SavingsPlan, Inflation) |
| `Graphics/AmortizationBarVisualization.cs` | Gestapelte Balken (Tilgung+Zinsen pro Jahr) |
| `Graphics/FinanzRechnerSplashRenderer.cs` | Splash-Screen "Das wachsende Kapital" (Aktien-Chart, Münz-Stapel, Gold-Partikel) |

Shared-Renderer aus `MeineApps.UI.SkiaSharp`:
- **DonutChartVisualization**: Donut-Charts für HomeView, StatisticsView, ExpenseTrackerView, LoanView, YieldView
- **LinearProgressVisualization**: Budget-Fortschrittsbalken in BudgetsView (ersetzt ProgressBar)

View-Zuordnung:
- **HomeView**: Budget-Gauge + Sparkline (30-Tage-Trend) + MiniRing (Budget-Kategorien) + Expense-Donut
- **StatisticsView**: 2x Donut (Einnahmen/Ausgaben) + TrendLine (6-Monats-Trend)
- **ExpenseTrackerView**: Kategorie-Donut
- **CompoundInterestView/SavingsPlanView/InflationView**: StackedArea-Chart
- **AmortizationView**: Stacked-Bar-Chart
- **LoanView/YieldView**: Donut-Chart
- **BudgetsView**: Budget-Gauge + LinearProgress pro Kategorie

### Game Juice
- **FloatingText**: Quick-Add (+/- Betrag, income=gruen, expense=rot)
- **Celebration**: Confetti bei Budget-Analyse (CelebrationRequested Event in MainViewModel)
- **Animationen (MainView.axaml Styles)**: DialogOverlay (Scale+Opacity 200ms), BouncingFab (Pulse 2s infinite), EmptyPulse (Opacity 2.5s), PremiumShimmer (Opacity 3s), SummaryCard (Hover translateY+BoxShadow), InputError (Shake 0.4s), AnimatedValue (Opacity-Fade 0.3s), MonthFade (Opacity 0.15s), UndoTimer (ScaleX 5s Countdown), ThemePreview (Hover Scale 1.03)
- **Farbige Kategorie-Chips**: In QuickAdd, AddExpense, AddRecurring Dialogen (CategoryToColorBrushConverter mit Opacity)
- **Gruppierte Transaktionen**: Date-Headers mit Tages-Summe, Notiz-Anzeige
- **Recurring Display**: Farbiger Seitenstreifen, Countdown-Text, farbige Beträge, Inaktiv-Styling (Opacity+Strikethrough)
- **Undo-Countdown**: Visueller Balken in Undo-Snackbars (Scale 1→0 über 5s)

### Neue Converter/Models
- **BoolToDoubleConverter**: `bool→double` für Opacity-Binding (Parameter: "TrueValue,FalseValue")
- **RecurringDisplayItem**: Wrapper mit DueDateDisplay, CategoryColor, CategoryColorHex
- **CategoryDisplayItem.CategoryColorHex**: Hex-Farbe aus CategoryLocalizationHelper

### Back-Navigation (Double-Back-to-Exit)
- **MainViewModel.HandleBackPressed()**: Plattformunabhängige Logik, gibt bool zurück (true=behandelt, false=App schließen)
- **MainActivity.OnBackPressed()**: Android-Override, ruft HandleBackPressed(), bei false → base.OnBackPressed()
- **ExitHintRequested Event**: Feuert bei erstem Back auf Home → Toast auf Android
- **Overlay-Reihenfolge**: BudgetAnalysis → BudgetAd → QuickAdd → RestoreDialog (Settings) → AddExpense (Tracker) → SubPage-Dialoge (AddBudget/AddRecurring) → SubPage → Calculator → Tab→Home → Double-Back-Exit (2s)
- **RESX-Key**: PressBackToExit (6 Sprachen)

### Bekanntes Pattern: SKCanvasView in unsichtbaren Containern

Calculator-Views (CompoundInterest, SavingsPlan, Loan, etc.) liegen in `Border IsVisible="{Binding IsXxxActive}"` im MainView. Wenn `InvalidateSurface()` auf einer unsichtbaren SKCanvasView aufgerufen wird, wird PaintSurface NICHT gefeuert. Deshalb: Die `OpenXxx()` Commands im MainViewModel rufen IMMER `CalculateCommand.Execute(null)` auf (ohne HasResult-Check), damit nach dem Sichtbar-Werden ein frisches PropertyChanged → InvalidateSurface() → PaintSurface ausgelöst wird.

### Architektur

| Datei | Zweck |
|-------|-------|
| `ViewModels/MainViewModel.cs` | Constructor, Navigation, Tab-Switching, Back-Button, Calculator-Nav, Sub-Pages, HomeView-Texte |
| `ViewModels/MainViewModel.Home.cs` | Home-Dashboard (Daten laden, Budget-Status, Recent Transactions, Expense-Chart, Sparkline, Mini-Ringe, Quick-Add, Budget-Analyse) |
| `Graphics/ChartHelper.cs` | Gemeinsame Y-Achsen-Skalierung und Label-Formatierung für Charts |
