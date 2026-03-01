# FinanzRechner (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Finanz-App mit Ausgaben-Tracking, Budget-Verwaltung, Dauerauftraegen und 6 Finanz-Rechnern.

**Version:** 2.0.2 | **Package-ID:** com.meineapps.finanzrechner | **Status:** Geschlossener Test

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

## Changelog (Highlights)

- **01.03.2026**: **Immersiver Ladebildschirm**: SplashOverlay durch SkiaLoadingSplash + SplashScreenRenderer ersetzt (Gradient-BG, 24 Glow-Partikel, Fortschrittsbalken). Neue `FinanzRechnerLoadingPipeline` in `Loading/`: Step 1 DB+Shader parallel (weight 40, ExpenseService.InitializeAsync + ShaderPreloader.PreloadAll), Step 2 ViewModel (weight 15). App.axaml.cs: Panel(MainView + Splash) Pattern mit RunLoadingAsync, DataContext erst nach Pipeline-Abschluss. Altes SplashOverlay aus MainView.axaml und PreloadAction aus MainView.axaml.cs entfernt. Build 0 Fehler.
- **28.02.2026 (5)**: **Erweitertes Preloading im SplashOverlay**: PreloadAction von 2 auf 4 Schritte erweitert. Schritt 1: ShaderPreloader.PreloadAll() (12 SkSL-Shader). Schritt 2: ExpenseService.InitializeAsync() (SQLite + JSON-Restore + Auto-Backup). Schritt 3: 6 statische Chart-Visualisierungen vorladen (BudgetGaugeVisualization, SparklineVisualization, BudgetMiniRingVisualization, TrendLineVisualization, StackedAreaVisualization, AmortizationBarVisualization - je WarmUp() triggert CLR-Klassen-Initializer für SKPaint/SKFont/SKMaskFilter). Schritt 4: Home-Dashboard-Daten vorladen via MainViewModel.OnAppearingAsync() (Daueraufträge verarbeiten, Monats-Zusammenfassung, Budget-Status, Sparkline, MiniRings). Double-Call-Safety durch _isHomeDataStale Flag gewährleistet. Build 0 Fehler.
- **28.02.2026 (4)**: **SplashOverlay / Ladebildschirm**: SplashOverlay (MeineApps.UI.Controls) in MainView.axaml integriert (Panel-Wrapper um Root-Grid + SplashOverlay darüber). PreloadAction in MainView.axaml.cs OnAttachedToVisualTree: ShaderPreloader.PreloadAll() + ExpenseService.InitializeAsync() (Datenbank-Initialisierung). Fire-and-Forget in App.axaml.cs entfernt (InitializeAsync wird jetzt sauber im Preloader awaited). Funktioniert auf Desktop UND Android. Build 0 Fehler.
- **28.02.2026 (3)**: 8 Verbesserungen: (FR-P1) Thread-Safety-Kommentar auf allen 6 Visualisierungs-Render()-Methoden (statische Paints nicht thread-safe), (FR-P3) Kommentar bei ObservableCollection-Neuerstellung in ApplyFilterAndSort (effizienter als Clear+AddRange in Avalonia), (FR-P4) AmortizationViewModel UpdateChartData: LINQ-pro-Jahr durch Single-Pass for-each ersetzt, (FR-A1) MainViewModel in Partial Classes aufgeteilt (MainViewModel.cs 483 Zeilen + MainViewModel.Home.cs 530 Zeilen statt 1143), (FR-A3) ChartHelper.cs extrahiert (CalculateGridStep+FormatYLabel aus StackedArea+AmortizationBar dedupliziert), (FR-C2) BudgetStatus.CategoryIcon: Duplizierte Emoji-Map durch Delegation an CategoryLocalizationHelper.GetCategoryIcon() ersetzt + TODO-Kommentar für Material-Icons-Migration, (FR-POT-6) BudgetsViewModel: Redundanter _cleanupLock bei ConcurrentDictionary entfernt, (FR-POT-4) ExpenseService: Debug.WriteLine durch Trace.TraceError/TraceWarning ersetzt (2 Stellen)
- **28.02.2026 (2)**: Code-Qualität: (FR-C1) Englische Validierungs-Fehlermeldungen in ExpenseService ins Deutsche übersetzt (ValidateExpense, ValidateBudget, ValidateRecurringTransaction, ImportFromJsonAsync), (FR-C3) Englische Kommentare in Expense.cs, FinanceEngine.cs und ExpenseService.cs ins Deutsche übersetzt, (FR-BUG-6) Tote Variable isPositive in SparklineVisualization.cs entfernt, FinanceEngine ArgumentException-Texte eingedeutscht (6 Methoden)
- **28.02.2026**: 8 Bugfixes: (FR-3) Fire-and-Forget InitializeAsync mit try-catch + Trace.TraceError (korrupte JSON nicht verschlucken), (FR-1) Race Condition _isHomeDataStale: Flag erst NACH LoadMonthlyDataAsync zurücksetzen (ContinueWith OnlyOnRanToCompletion), (FR-5) Sparkline am Monatsanfang: Vormonat laden wenn Tag<30 (30-Tage-Trend braucht Vormonats-Daten), (FR-4) Doppeltes Laden bei Monatswechsel in SaveExpenseAsync: _suppressLoad Pattern wie PreviousMonth/NextMonth, (FR-7) -0,00€ Bug: MonthlyExpensesDisplay prüft MonthlyExpenses>0, (FR-6) DateTime.Now→DateTime.UtcNow in ExportService (2 Stellen) + StatisticsViewModel (2 Stellen), (FR-2) SavingsPlanResult Validierung: ArgumentException bei negativem Zinssatz + years<=0, (FR-P2) StringComparison.OrdinalIgnoreCase statt ToLowerInvariant in Filter-Logik, (FR-P5) Redundanter ProcessDueRecurringTransactionsAsync Aufruf in LoadExpensesAsync entfernt (MainViewModel verarbeitet bereits bei App-Start)
- **16.02.2026 (3)**: Fix: Calculator-Charts zeigten nur Umrandung aber kein Diagramm. Root Cause: Auto-Calculate (300ms Debounce) setzte Chart-Daten bevor die View sichtbar war, InvalidateSurface() auf unsichtbare SKCanvasView wird ignoriert, OpenXxx() Commands übersprangen Calculate() bei HasResult=true. Fix: HasResult-Check in allen 6 OpenXxx() Commands entfernt → CalculateCommand wird IMMER aufgerufen.
- **16.02.2026 (2)**: DonutChart Premium-Optik (Gradient-Segmente, 3D-Highlight, innerer Schatten, Glow). ExpenseTrackerView als ganzseitige ScrollView umgebaut (Header+Filter+Chart+Monatsnavigation+Transaktionen in einer ScrollView, FAB+Dialog als Overlay).
- **16.02.2026**: LiveCharts komplett durch SkiaSharp ersetzt (Phase 8): 3 neue Renderer (TrendLineVisualization, StackedAreaVisualization, AmortizationBarVisualization), 9 Views migriert (HomeView, StatisticsView, ExpenseTrackerView, CompoundInterestView, SavingsPlanView, InflationView, LoanView, YieldView, AmortizationView), bestehende Renderer aktiviert (Sparkline 30-Tage-Trend + MiniRing Budget-Kategorien in HomeView), BudgetsView ProgressBar→LinearProgressVisualization (shared in MeineApps.UI), LiveCharts-PackageReference entfernt
- **13.02.2026 (11)**: Numerische Tastatur: `TextInputOptions.ContentType="Number"` (Dezimalzahlen: Beträge, Zinssätze) und `TextInputOptions.ContentType="Digits"` (Ganzzahlen: Jahre) auf alle 20 TextBox-Felder in allen 6 Rechner-Views (CompoundInterest, SavingsPlan, Loan, Amortization, Yield, Inflation). Android zeigt jetzt automatisch die Ziffern-Tastatur.
- **13.02.2026 (10)**: UI/UX Verbesserungsplan (10 Batches A-J): (A) Globale Animation-Styles in MainView.axaml (DialogOverlay, BouncingFab, EmptyPulse, PremiumShimmer, SummaryCard, InputError, AnimatedValue, MonthFade, UndoTimer, ThemePreview), (B) Dialog Scale-Up Animation auf 4 Dialogen (QuickAdd, AddExpense, AddBudget, AddRecurring, Restore), (C) Farbige Kategorie-Chips in 3 Dialogen (QuickAdd, AddExpense, AddRecurring) mit CategoryToColorBrushConverter, (D) Gruppierte Transaktionsliste nach Datum mit Date-Headers + Tages-Summe + Notiz-Anzeige, (E) HomeView: EmptyPulse-Icon, SummaryCard-Hover auf Karten, PremiumShimmer auf Premium-Card, (F) Monats-Navigation Fade-Animation (MonthFade auf ScrollViewer), (G) Statistics: SummaryCard-Hover + aktive Period-Buttons mit Scale+Bold, (H) Recurring: Farbiger Seitenstreifen (Kategorie-Farbe), Countdown ("Heute/Morgen/In X Tagen fällig"), farbige Beträge (rot/grün), Inaktiv-Badge+Opacity+Strikethrough, Kategorie-Chips im Dialog, neue RESX-Keys (DaysUntilDue/DueToday/DueTomorrow in 6 Sprachen), RecurringDisplayItem-Wrapper, BoolToDoubleConverter, (I) Undo-Countdown-Balken in 3 Snackbars (5s Scale-Animation), (J) Settings: PremiumShimmer auf Premium-Card, ThemePreview-Hover (Scale 1.03) auf alle 8 Theme-Karten, DialogOverlay auf Restore-Dialog
- **13.02.2026 (9)**: UI-Bugfixes: (1) \u20ac→€ in allen XAML StringFormats (XAML interpretiert C#-Unicode-Escapes nicht, 8 Stellen in 5 Views), (2) ToggleSwitch "On/Off"-Text entfernt (OnContent=""/OffContent=""), (3) Recurring-Dialog Toggle-Buttons mit Selected-State (rot/grün wie in ExpenseTracker, IsExpenseSelected/IsIncomeSelected Properties), (4) Kategoriefeld-Überschneidung behoben (SelectedCategory vor Categories-Notify setzen + IsExpenseSelected/IsIncomeSelected notifyen)
- **13.02.2026 (8)**: Double-Back-to-Exit: Android-Zurücktaste navigiert schrittweise zurück (Overlays→SubPages→Calculator→Home-Tab), App schließt erst bei 2x schnellem Drücken auf Home. HandleBackPressed() in MainViewModel (plattformunabhängig), OnBackPressed()-Override in MainActivity mit Toast-Hinweis. Vollständige Overlay-Kette: BudgetAnalysis→BudgetAd→QuickAdd→RestoreDialog→AddExpense→SubPage-Dialoge→SubPage→Calculator→Tab→Home→Exit. Neuer RESX-Key "PressBackToExit" in 6 Sprachen.
- **13.02.2026 (7)**: Bugfix-Runde 7: (1) Process.Start→UriLauncher.OpenUri in SettingsView (URL+Feedback auf Android crashte mit PlatformNotSupportedException), (2) GetNextDueDate Default-Pattern `_ => baseDate.AddDays(1)` statt `_ => baseDate` (verhindert Endlosschleife bei ungültigem RecurrencePattern-Enum), (3) _sentNotifications.Clear() bei Replace-Restore (Budget-Warnungen werden nach Daten-Replace zurückgesetzt), (4) importedCount zählt jetzt auch Budgets+RecurringTransactions (nicht nur Expenses)
- **13.02.2026 (6)**: Bugfix-Runde 6: (1) CSV Formula-Injection-Schutz (EscapeCsvField: =,+,-,@ Prefix-Escape + CR-Normalisierung), (2) Trend "+∞"→lokalisiertes "Neu"/"New" bei fehlenden Vormonatsdaten (neuer RESX-Key "New" in 6 Sprachen), (3) RESX-Akzente massiv korrigiert: PT 14 Fixes (transações, estatísticas, orçamentos, botão, vídeo, relatório, Também, Análise, Política...), ES 8 Fixes (botón, estadísticas, límites, categorías, automáticamente, También, Análisis, Política, día...), FR 15 Fixes (confidentialité, Thème, dégradé, Lumière, Forêt, dépenses, aperçu, Définissez, catégories, répètent, également, entrée, récurrent, Budgétaire, vidéo, Accès...), DE 2 Fixes (fuer→für), (4) CSV-Header lokalisiert (Date/Type/Category/Description/Amount/Note aus RESX), (5) PdfDocument+XGraphics korrekt disposed (using + Dispose bei Seitenumbruch), (6) Kategorie-Sortierung deterministisch (ThenBy CategoryName bei gleichen Beträgen)
- **12.02.2026 (5)**: Bugfix-Runde 5: (1) Tab-Wechsel schließt QuickAdd-Overlay (ShowQuickAdd=false in OnSelectedTabChanged), (2) Budget-Analysis CancellationToken (verhindert Overlay-Wiederöffnung nach Tab-Wechsel), (3) RESX de Umlaute korrigiert (5 Werte ae/oe/ue→ä/ö/ü), (4) FloatingText+Chart-Labeler über CurrencyHelper.FormatCompactSigned/FormatAxis (konsistente de-DE Formatierung), (5) "p.a." lokalisiert in YieldVM (neuer RESX-Key PerAnnum in 6 Sprachen), (6) PDF-Title+Footer lokalisiert (FinancialStatistics RESX-Key + CurrentUICulture Datumsformat)
- **12.02.2026 (4)**: Bugfix-Runde 4: (1) ErrorMessage-Anzeige in allen 6 Rechner-Views (AlertCircle-Icon + roter Text, sichtbar bei Overflow), (2) ErrorMessage-Reset in allen 6 Reset()-Methoden, (3) Cache-Invalidierung: BudgetsVM+RecurringTransactionsVM feuern DataChanged→Home+Statistics werden bei Budget-/Dauerauftrags-Änderungen aktualisiert, (4) IsExportingPdf→IsExporting umbenannt (Guard für CSV+PDF Export korrekt benannt)
- **12.02.2026 (3)**: Bugfix-Runde 3: (1) CTS-Leak in StatisticsVM (IDisposable+Dispose) + ExpenseTrackerVM (_statusCts in Dispose ergänzt), (2) Fehlende RESX-Keys (LoadError, LoadErrorExpenses/Budgets/Recurring, ErrorEndDateBeforeStart, ErrorOverflow) in allen 6 Sprachen, (3) Hardcodierte deutsche Fehlermeldungen in ExpenseService lokalisiert, (4) EndDate>=StartDate Validierung bei Daueraufträgen (RecurringTransactionsVM+ExpenseTrackerVM), (5) Rechner ErrorMessage Property bei OverflowException statt nur HasResult=false, (6) Negative AnnualRate bei Loan/Amortization abgefangen, (7) CurrencyHelper Format mit fester de-DE CultureInfo statt OS-Locale, (8) CSV-Export sep=; Header für Excel-Kompatibilität
- **12.02.2026 (2)**: Bugfix-Runde 2: (1) Recurring Iterations-Limit (max 365 pro Dauerauftrag, verhindert OOM bei langem Offline), (2) Budget-Prozent über 100% erlaubt + rote Warnanzeige (CSS-Klasse .overLimit), (3) FinanceEngine Infinity/NaN-Schutz (ValidateResult→OverflowException in allen 5 Berechnungen + catch in allen 6 Calculator-VMs), (4) Monatliche Daueraufträge Datums-Drift gefixt (bewahrt StartDate.Day bei Monatsende-Übergängen), (5) Tab-Wechsel Cache (InvalidateCache/DataChanged Pattern für Statistics+Tracker+Home)
- **12.02.2026**: Visual Redesign komplett. Chart-Typen erneuert: SavingsPlan+CompoundInterest→StackedAreaSeries (Einzahlungen/Kapital+Zinsen gestapelt), Amortization→StackedColumnSeries (Tilgung vs Zinsen pro Jahr), Inflation→StackedAreaSeries (Kaufkraft vs Verlust), Yield→Donut-PieChart (Startkapital vs Gewinn), Loan→Donut-PieChart. Alle Charts Background=CardBrush (kein Transparent). StatisticsView: Donut-Charts (InnerRadius=50), Summary-Cards mit Gradient, Kategorie-Breakdown mit farbigen Fortschrittsbalken+Prozent-Badge, Trend-Chart mit Fill+LineSmoothness. HomeView: Mini-Donut (Top-6), farbige Kategorie-Icons. ExpenseTrackerView: farbige Icons. Neuer Converter: CategoryToColorBrushConverter.cs
- **11.02.2026 (4)**: Optimierungs-Durchlauf Batch 4-6: Atomares Schreiben (temp+rename), Auto-Backup (5 Versionen), DateTime.Today/UtcNow konsistent, Budget-Notification-Persistenz, Trend-Abfragen optimiert (6→1), Recurring nur 1x/Tag, stille Fehler loggen, Fire-and-forget try-catch. CurrencyHelper zentral (alle EUR-Formatierungen), CategoryLocalizationHelper erweitert (Icons+Farben), CSV InvariantCulture, Biweekly-Intervall, englische Kommentare→deutsch. Budget-Kategorie im Edit deaktiviert, Gesamt-Monatsbudget-ProgressBar, Amortization-Tabelle ausklappbar, Live-Berechnung Debouncing (300ms) in allen 6 Rechnern, Inflationsrechner (6. Rechner), Restore Merge/Replace Dialog
- **11.02.2026 (3)**: Inflationsrechner als 6. Finanzrechner: FinanceEngine.CalculateInflation + InflationResult, InflationViewModel mit Chart (Kaufkraft-Verlauf als rote LineSeries), InflationView (orange Gradient #F97316/#EA580C, CurrencyUsd Icon), MainViewModel-Integration (ActiveCalculatorIndex=5), HomeView 2x3 Grid-Karte, DI-Registrierung, 8 neue RESX-Keys in allen 6 Sprachen (CalcInflation, CurrentAmount, AnnualInflationRate, FutureValue, PurchasingPower, PurchasingPowerLoss, ChartPurchasingPower, LossPercent)
- **11.02.2026 (2)**: Restore Merge vs Replace Dialog: Nach File-Picker zeigt SettingsView einen Overlay-Dialog mit Merge/Replace/Cancel Buttons. 6 neue RESX-Keys in allen 6 Sprachen. ProcessRestoreFileAsync von public auf private geaendert.
- **11.02.2026**: Bugfix-Review: Dauerauftraege nachholen bei laengerem Nicht-Benutzen, PremiumPrice 3.99 in allen 6 RESX, SettingsView Events verdrahtet (Backup/Restore/URL/Feedback), CSV/PDF-Export mit Datum-Range statt nur einem Monat, Undo-Delete Queue statt Einzelvariable, ClearAllExpensesAsync mit Semaphore, CelebrationRequested implementiert, KW lokalisiert, CategoryStatistic/BudgetStatus lokalisiert, InflationResult entfernt (toter Code), DateTime.UtcNow in Backup-Metadaten, hardcodierte Error-Strings lokalisiert, doppeltes Laden bei PreviousMonth/NextMonth behoben
- **08.02.2026**: FloatingTextOverlay + CelebrationOverlay (Game Juice)
- **07.02.2026**: 4 Rewarded Ad Features, Android FileProvider Export, HomeView Redesign
- **06.02.2026**: Calculator Views Redesign, Export mit File-Dialog + Feedback, vollstaendige Lokalisierung
