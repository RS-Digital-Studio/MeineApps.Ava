# FinanzRechner (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Vollwertiger Finanz-Manager mit Multi-Konto, Ausgaben-Tracking, Budget-Verwaltung, Sparzielen, Schulden-Tracker, Finanz-Score, Prognosen, Daueraufträgen und 6 Finanz-Rechnern.

**Version:** 2.0.6 | **Package-ID:** com.meineapps.finanzrechner | **Status:** Geschlossener Test

## Features

- **4 Tabs**: Home (Dashboard + Quick-Add), Tracker, Statistics, Settings
- **Multi-Konto**: Girokonto, Sparkonto, Bargeld, Kreditkarte, Depot + Überweisungen zwischen Konten
- **Expense Tracking**: CRUD mit Filter/Sort, Undo-Delete, Kategorie-Icons, Konto-Zuordnung
- **Budget Management**: Budget-Limits pro Kategorie, Fortschrittsanzeige, Alert-Levels
- **Sparziele**: Zielbetrag, Fortschritt, Deadline, Einzahlungen/Entnahmen, Celebration bei Zielerreichung
- **Schulden-Tracker**: Kredite/Darlehen verfolgen, Zinssatz, monatliche Rate, Restlaufzeit-Berechnung
- **Eigene Kategorien**: Benutzerdefinierte Ausgaben-/Einnahmen-Kategorien mit Icon und Farbe
- **Finanz-Score**: Gesundheitsbewertung 0-100 (Sparquote, Budget-Einhaltung, Schulden, Regelmäßigkeit)
- **Prognose**: Hochrechnung Monatsende-Saldo, Tagesbudget, Durchschn. Tagesausgabe
- **Nettovermögen**: Berechnung aus allen Konten minus Schulden
- **Monatsvergleich**: Ausgaben/Einnahmen-Veränderung zum Vormonat, Kategorie-Breakdown
- **Konfigurierbare Währung**: 16 Presets (EUR, USD, GBP, CHF, JPY, etc.) mit korrekter Formatierung
- **Split-Transaktionen**: Eine Rechnung auf mehrere Kategorien aufteilen (Model vorbereitet)
- **Recurring Transactions**: Daueraufträge mit Auto-Processing bei App-Start
- **6 Finanz-Rechner**: CompoundInterest, SavingsPlan, Loan, Amortization, Yield, Inflation
- **Charts**: Komplett SkiaSharp-basiert (DonutChart, TrendLine, StackedArea, AmortizationBar, Sparkline, MiniRing, LinearProgress, BudgetGauge) - KEIN LiveCharts
- **Export**: CSV + PDF (PdfSharpCore), plattformspezifisches File-Sharing

## App-spezifische Services

### Bestehende Services
- **IExpenseService / ExpenseService**: JSON-CRUD (Expense, Budget, RecurringTransaction Models)
- **IExportService / ExportService**: CSV + PDF Export mit optionalem targetPath Parameter und Datum-Range-Filterung
- **IFileDialogService / FileDialogService**: Avalonia StorageProvider.SaveFilePickerAsync
- **IFileShareService**: Plattformspezifisch (Desktop: Process.Start, Android: FileProvider + Intent.ActionSend)
- **CategoryLocalizationHelper**: Statische Kategorie-Namen/Icons/Farben pro Sprache

### Neue Services (März 2026)
- **IAccountService / AccountService**: Kontoverwaltung, Saldo-Berechnung, Überweisungen. JSON: `accounts.json`
- **ISavingsGoalService / SavingsGoalService**: Sparziel-CRUD, Betrag anpassen, Abschluss. JSON: `savings_goals.json`
- **IDebtService / DebtService**: Schulden-CRUD, Zahlungen buchen, Tilgungsberechnung. JSON: `debts.json`
- **ICustomCategoryService / CustomCategoryService**: Benutzerdefinierte Kategorien. JSON: `custom_categories.json`
- **IFinancialAnalysisService / FinancialAnalysisService**: Score-Berechnung, Monatsvergleich, Prognose, Nettovermögen. Keine eigene Persistenz

### CurrencyHelper (konfigurierbar)
- `CurrencyHelper.Configure(CurrencySettings)` beim App-Start
- 16 Währungs-Presets in `CurrencySettings.Presets`
- Formatierung passt sich automatisch an (Symbol vor/nach Betrag, Dezimalformat)

### Expense Model Erweiterungen
- `AccountId`: Konto-Zuordnung (nullable, rückwärtskompatibel)
- `CustomCategoryId`: Benutzerdefinierte Kategorie (überschreibt Enum)
- `TransferToAccountId` + `TransferId`: Für Überweisungen zwischen Konten
- `SplitItems`: Split-Transaktionen (Liste von Kategorie+Betrag)
- `TransactionType.Transfer`: Neuer Typ neben Expense/Income

### SubPage-Navigation (erweitert)
- **AccountsPage**: Konten verwalten + Überweisungen
- **SavingsGoalsPage**: Sparziele verwalten + Einzahlungen
- **DebtTrackerPage**: Schulden verwalten + Zahlungen buchen
- **CustomCategoriesPage**: Eigene Kategorien erstellen/bearbeiten
- Alle SubPages: GoBack via NavigationRequested → ".."
- Alle SubPages: Dialoge (Add/Edit/Payment) als modale Overlays

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
- Hero-Header (Bilanz + Einnahmen/Ausgaben als Pill-Chips) mit animiertem SkiaSharp-Hintergrund (FinanceDashboardRenderer)
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

### Backup-Format v2.0 (Maerz 2026)
- Container-JSON mit Schlüsseln: `version`, `expenses`, `accounts`, `savings_goals`, `debts`, `custom_categories`
- Jeder Schlüssel enthält den JSON-Export des jeweiligen Services
- Rückwärtskompatibel: Restore erkennt altes Format (kein `version` Key) und importiert nur Expenses
- SettingsViewModel injiziert alle 5 Services (IExpenseService, IAccountService, ISavingsGoalService, IDebtService, ICustomCategoryService)

### Transfer-Buchung (Kontoüberweisungen)
- EINE Transfer-Transaktion pro Überweisung: `AccountId` = Quelle, `TransferToAccountId` = Ziel
- Saldo-Berechnung: `transfersOut` = AccountId passt + Type==Transfer, `transfersIn` = TransferToAccountId passt + Type==Transfer
- KEIN Doppel-Record (zweite Buchung würde Saldo verfälschen)

### SkiaSharp-Visualisierungen (LiveCharts komplett ersetzt)

| Datei | Zweck |
|-------|-------|
| `Graphics/BudgetGaugeVisualization.cs` | Halbkreis-Tachometer (Legacy, ersetzt durch SkiaGradientRing) |
| `Graphics/SparklineVisualization.cs` | Mini-Sparkline mit Gradient-Füllung für 30-Tage-Ausgaben-Trend |
| `Graphics/BudgetMiniRingVisualization.cs` | Kompakte Mini-Ringe für Budget-Kategorien-Übersicht |
| `Graphics/TrendLineVisualization.cs` | 2 Spline-Kurven (Einnahmen/Ausgaben) mit Gradient-Füllung |
| `Graphics/StackedAreaVisualization.cs` | 2 gestapelte Flächen (CompoundInterest, SavingsPlan, Inflation) |
| `Graphics/AmortizationBarVisualization.cs` | Gestapelte Balken (Tilgung+Zinsen pro Jahr) |
| `Graphics/FinanzRechnerSplashRenderer.cs` | Splash-Screen "Das wachsende Kapital" (Aktien-Chart, Münz-Stapel, Gold-Partikel) |
| `Graphics/FinanceDashboardRenderer.cs` | Animierter Hero-Header-Hintergrund (Gradient-Mesh, Grid-Linien, Glow-Dots, Floating-Symbole) |
| `Graphics/CalculatorHeaderRenderer.cs` | 6 individuelle animierte Header pro Rechner (Exponentialkurve, Stufen, etc.) |
| `Graphics/CardGlowRenderer.cs` | Status-basierter Edge-Glow (Budget-Status, Bilanz, Berechnungs-Flash) |
| `Graphics/FinanceBackgroundRenderer.cs` | Animierter "Financial Data Stream" Hintergrund (5 Layer: Smaragd-Gradient, Chart-Linien, Mini-Balken-Partikel, Sparkle-Punkte, Vignette). ~5fps Render-Loop in MainView |

Shared-Renderer aus `MeineApps.UI.SkiaSharp`:
- **DonutChartVisualization**: Donut-Charts für HomeView, StatisticsView, ExpenseTrackerView, LoanView, YieldView
- **LinearProgressVisualization**: Budget-Fortschrittsbalken in BudgetsView (ersetzt ProgressBar)
- **SkiaGradientRing**: Gradient-Fortschrittsring für Gesamt-Budget in HomeView und BudgetsView (ersetzt BudgetGaugeVisualization)

View-Zuordnung:
- **MainView**: FinanceBackgroundRenderer (animierter Hintergrund, ~5fps DispatcherTimer, Grid.RowSpan=3)
- **HomeView**: FinanceDashboardRenderer (Hero-Header-BG) + SkiaGradientRing (Budget) + Sparkline (30-Tage-Trend) + MiniRing (Budget-Kategorien) + Expense-Donut
- **StatisticsView**: 2x Donut (Einnahmen/Ausgaben) + TrendLine (6-Monats-Trend)
- **ExpenseTrackerView**: Kategorie-Donut
- **CompoundInterestView/SavingsPlanView/InflationView**: StackedArea-Chart
- **AmortizationView**: Stacked-Bar-Chart
- **LoanView/YieldView**: Donut-Chart
- **BudgetsView**: SkiaGradientRing (Gesamt-Budget) + LinearProgress pro Kategorie

### Game Juice
- **FloatingText**: Quick-Add (+/- Betrag, income=gruen, expense=rot)
- **Celebration**: Confetti bei Budget-Analyse (CelebrationRequested Event in MainViewModel)
- **Animationen (MainView.axaml Styles)**: DialogOverlay (Scale+Opacity 200ms), BouncingFab (Pulse 2s infinite), EmptyPulse (Opacity 2.5s), PremiumShimmer (Opacity 3s), SummaryCard (Hover translateY+BoxShadow), InputError (Shake 0.4s), AnimatedValue (Opacity-Fade 0.3s), MonthFade (Opacity 0.15s), UndoTimer (ScaleX 5s Countdown), ThemePreview (Hover Scale 1.03)
- **Farbige Kategorie-Chips**: In QuickAdd, AddExpense, AddRecurring Dialogen (CategoryToColorBrushConverter mit Opacity)
- **Gruppierte Transaktionen**: Date-Headers mit Tages-Summe, Notiz-Anzeige
- **Recurring Display**: Farbiger Seitenstreifen, Countdown-Text, farbige Beträge, Inaktiv-Styling (Opacity+Strikethrough)
- **Undo-Countdown**: Visueller Balken in Undo-Snackbars (Scale 1→0 über 5s)

### Behaviors (HomeView)
- **CountUpBehavior**: Hero-Header Geldbeträge (Balance, Income, Expenses) zählen animiert von 0 hoch (800ms, CubicEaseOut, de-DE Formatierung)
- **StaggerFadeInBehavior**: Recent Transactions Items (40ms Stagger) + Calculator-Grid Karten (60ms Stagger, FixedIndex 0-5)
- **TapScaleBehavior**: Monatsreport-Button (0.97), Calculator-Buttons (0.95), ViewAll-Button (0.92), Premium-Button (0.97), Overlay-Buttons (0.95)
- **Kombination StaggerFadeIn + TapScale**: Calculator-Karten nutzen Panel-Wrapper (StaggerFadeIn auf Panel, TapScale auf Button) weil beide Behaviors unterschiedliche RenderTransform-Typen setzen

### Behaviors (ExpenseTrackerView)
- **FadeInBehavior**: Haupt-Content StackPanel (250ms, SlideFromBottom 12px)
- **TapScaleBehavior**: Header-Buttons Recurring/Budgets/Export (0.92), Month-Nav Prev/Next (0.88), CurrentMonth-Button (0.95), ResetFilters-Button (0.95)
- **StaggerFadeInBehavior**: Transaction-Items (40ms Stagger, 300ms Dauer) auf Panel-Wrapper
- **SwipeToRevealBehavior**: Transaction-Items (Swipe links offenbart roten Delete-Layer, 80px). Nutzt bestehendes DeleteExpenseCommand. Inline-Delete-Button bleibt als Fallback erhalten

### Behaviors (BudgetsView)
- **FadeInBehavior**: Haupt-Content StackPanel (250ms, SlideFromBottom 12px)
- **StaggerFadeInBehavior**: Budget-Items (50ms Stagger, 300ms Dauer) auf Panel-Wrapper
- **TapScaleBehavior**: Budget-Cards (0.97), FAB (0.92), Dialog-Buttons Save/Cancel (0.95), Undo-Button (0.95), Back-Button (0.88), Dismiss-Button (0.88)
- **CountUpBehavior**: Spent/Remaining/Limit Geldbeträge pro Budget-Card (800ms, N2, de-DE, EUR-Suffix)
- **AlertLevelToBoxShadowConverter**: Status-basierter BoxShadow auf Budget-Cards (Safe: grüner Glow #3022C55E, Warning: gelber Glow #30F59E0B, Exceeded: roter Glow #40EF4444)
- **ContextFlyout**: Edit/Delete Menü auf Budget-Cards (Rechtsklick/Long-Press)

### Behaviors (RecurringTransactionsView)
- **FadeInBehavior**: Empty-State StackPanel + Listen-Content StackPanel + Dialog-Content StackPanel (250ms, SlideFromBottom 12px)
- **StaggerFadeInBehavior**: Transaction-Items (40ms Stagger, 300ms Dauer) auf Panel-Wrapper
- **SwipeToRevealBehavior**: Transaction-Items (Swipe links offenbart roten Delete-Layer, 80px). Nutzt DeleteTransactionCommand. Inline-Delete-Button bleibt als Fallback erhalten
- **TapScaleBehavior**: Back-Button (0.88), FAB (0.92), Edit/Delete/Toggle Buttons pro Item (0.88), Dialog-Buttons Save/Cancel (0.95), Undo-Button (0.95), Dismiss-Button (0.88)

### Behaviors (Calculator-Views, alle 6)
- **CountUpBehavior**: Ergebnis-Geldbeträge zählen animiert von 0 hoch (600ms, N2, de-DE, EUR-Suffix). Nur auf Geldbeträge, nicht auf Prozentwerte
  - CompoundInterest: FinalAmountValue, InterestEarnedValue
  - SavingsPlan: FinalAmountValue, TotalDepositsValue, InterestEarnedValue
  - Loan: MonthlyPaymentValue, TotalPaymentValue, TotalInterestValue
  - Amortization: MonthlyPaymentValue, TotalInterestValue
  - Yield: TotalReturnValue (EffectiveAnnualRate/TotalReturnPercent bleiben Text-Binding)
  - Inflation: PurchasingPowerValue, PurchasingPowerLossValue, FutureValueValue (LossPercent bleibt Text-Binding)
- **Gold-Flash Animation**: ResultFlash CSS-Klasse auf Result Card Border (0.8s, BorderBrush Transparent→#FFD700→Transparent)
- **TapScaleBehavior**: Berechnen-Button (0.95)
- **Numerische Value-Properties**: Jedes ViewModel exponiert `XxxValue` (double) neben `XxxDisplay` (string) für CountUpBehavior-Binding

### Neue Converter/Models
- **AlertLevelToBoxShadowConverter**: `BudgetAlertLevel→BoxShadows` für status-basiertes Glow auf Budget-Cards
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
