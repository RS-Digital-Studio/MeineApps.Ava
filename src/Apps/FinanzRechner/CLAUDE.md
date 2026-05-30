# FinanzRechner — Vollwertiger Finanz-Manager

Vollwertiger Finanz-Manager mit Expense-Tracking, Multi-Konto, Budgets, Sparzielen,
Schulden-Tracker, Finanz-Score, Prognosen, Daueraufträgen und 6 Finanz-Rechnern.
Alle Charts vollständig in SkiaSharp (kein LiveCharts).

| Aspekt | Wert |
|--------|------|
| Aktuelle Version | v2.0.7 |
| Package-ID | com.meineapps.finanzrechner |
| Modus | Geschlossener Test |
| Theme | Smaragd `#10B981` — Living Finance |
| Ads | Banner + Rewarded |
| Premium | 3,99 EUR `remove_ads` |

Für generische Build-Befehle, Conventions und Architektur → [Haupt-CLAUDE.md](../../../CLAUDE.md).

---

## Build & Zielframework

| Projekt | Framework | Befehl |
|---------|-----------|--------|
| `FinanzRechner.Shared` | `net10.0` | `dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared` |
| `FinanzRechner.Desktop` | `net10.0` | `dotnet run --project src/Apps/FinanzRechner/FinanzRechner.Desktop` |
| `FinanzRechner.Android` | `net10.0-android` | `dotnet build src/Apps/FinanzRechner/FinanzRechner.Android` |

Release-AAB: `dotnet publish src/Apps/FinanzRechner/FinanzRechner.Android -c Release`

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `FinanzRechner.Shared/ViewModels/` | `FinanzRechner.ViewModels` |
| `FinanzRechner.Shared/Views/` | `FinanzRechner.Views` |
| `FinanzRechner.Shared/Services/` | `FinanzRechner.Services` |
| `FinanzRechner.Shared/Models/` | `FinanzRechner.Models` |
| `FinanzRechner.Shared/Graphics/` | `FinanzRechner.Graphics` |
| `FinanzRechner.Shared/Loading/` | `FinanzRechner.Loading` |

---

## Projekt-Struktur

```
src/Apps/FinanzRechner/
├── FinanzRechner.Shared/
│   ├── ViewModels/
│   │   ├── MainViewModel.cs              # Constructor, Navigation, Tab, Back, Ads
│   │   ├── MainViewModel.Home.cs         # Dashboard-Logik (Budget, Saldo, Quick-Add, Analyse)
│   │   ├── ExpenseTrackerViewModel.cs    # Expense-CRUD, Filter, Sort, Undo
│   │   ├── StatisticsViewModel.cs        # Charts, Monatsvergleich, Trend
│   │   ├── BudgetsViewModel.cs           # Budget-Verwaltung, Alert-Levels
│   │   ├── RecurringTransactionsViewModel.cs
│   │   ├── AccountsViewModel.cs          # Multi-Konto + Überweisungen
│   │   ├── SavingsGoalsViewModel.cs      # Sparziele + Einzahlungen
│   │   ├── DebtTrackerViewModel.cs       # Schulden + Zahlungen
│   │   ├── CustomCategoriesViewModel.cs
│   │   ├── SettingsViewModel.cs          # Backup v2, Restore Merge/Replace, Währung
│   │   └── Calculators/
│   │       ├── CompoundInterestViewModel.cs
│   │       ├── SavingsPlanViewModel.cs
│   │       ├── LoanViewModel.cs
│   │       ├── AmortizationViewModel.cs
│   │       ├── YieldViewModel.cs
│   │       └── InflationViewModel.cs
│   ├── Services/
│   │   ├── IExpenseService / ExpenseService          # JSON-CRUD (Expenses, Budgets, Recurring)
│   │   ├── IAccountService / AccountService          # accounts.json
│   │   ├── ISavingsGoalService / SavingsGoalService  # savings_goals.json
│   │   ├── IDebtService / DebtService                # debts.json
│   │   ├── ICustomCategoryService / CustomCategoryService  # custom_categories.json
│   │   ├── IFinancialAnalysisService / FinancialAnalysisService  # Score, Prognose, Nettovermögen
│   │   ├── IExportService / ExportService            # CSV + PDF (PdfSharpCore)
│   │   ├── IFileDialogService / FileDialogService    # StorageProvider.SaveFilePickerAsync
│   │   └── IFileShareService                         # Plattformspezifisch (Factory-Pattern)
│   ├── Graphics/                                     # App-spezifische SkiaSharp-Renderer
│   ├── Models/                                       # Expense, Account, Budget, SavingsGoal, DebtEntry, ...
│   ├── Helpers/
│   │   └── CategoryLocalizationHelper.cs             # Statische Kategorie-Namen/Icons/Farben pro Sprache
│   └── Loading/
│       └── FinanzRechnerLoadingPipeline.cs           # Alle Services + Shader parallel laden
├── FinanzRechner.Android/
└── FinanzRechner.Desktop/
```

---

## Features

### 4 Tabs

| Tab | Inhalt |
|-----|--------|
| **Home** | Dashboard (Saldo, Einnahmen/Ausgaben, Budget-Status, Prognose, Finanz-Score, Recent Transactions, Calculator-Grid, Quick-Add FAB) |
| **Tracker** | Expense-CRUD mit Filter/Sort, Monatsnavigation, Undo-Delete, Swipe-to-Delete |
| **Statistics** | Donut-Charts, TrendLine 6 Monate, Monatsvergleich, CSV/PDF-Export |
| **Settings** | Währung, Backup v2, Restore Merge/Replace, Sprache, Premium, Feedback |

### Sub-Pages (alle via GoBack `".."`)

| Sub-Page | ViewModel |
|----------|-----------|
| AccountsView | Konten verwalten + Überweisungen zwischen Konten |
| SavingsGoalsView | Sparziele + Einzahlungen/Entnahmen + Celebration bei 100% |
| DebtTrackerView | Schulden verwalten + Zahlungen buchen + Tilgungsberechnung |
| CustomCategoriesView | Benutzerdefinierte Kategorien mit Icon und Farbe |
| BudgetsView | Budget-Limits pro Kategorie + Alert-Levels + ContextFlyout |
| RecurringTransactionsView | Daueraufträge + Toggle aktiv/inaktiv |

### 6 Finanz-Rechner (im Calculator-Grid auf der Home-View)

| Rechner | ViewModel | Chart |
|---------|-----------|-------|
| Zinseszins | CompoundInterestViewModel | StackedArea |
| Sparplan | SavingsPlanViewModel | StackedArea |
| Kredit | LoanViewModel | Donut |
| Tilgungsplan | AmortizationViewModel | Stacked-Bar |
| Rendite | YieldViewModel | Donut |
| Inflation | InflationViewModel | StackedArea |

Alle 6 Rechner: `XxxValue` (double) für CountUpBehavior + `XxxDisplay` (string) für Text.
Gold-Flash (`ResultFlash` CSS-Klasse) nach Berechnung. CalculateCommand öffnet immer neu
(auch wenn IsVisible false — damit PaintSurface nach Sichtbar-Werden feuert).

---

## Architektur-Patterns

### DI-Konfiguration (App.axaml.cs)

```csharp
// Factory-Pattern für plattformspezifische Services
public static Func<IFileShareService>? FileShareServiceFactory { get; set; }
public static Func<IServiceProvider, IRewardedAdService>? RewardedAdServiceFactory { get; set; }
public static Func<IServiceProvider, IPurchaseService>? PurchaseServiceFactory { get; set; }
```

Alle Services als Singleton. MainViewModel als Singleton. Child-VMs per Constructor-Injection.

### Lade-Pipeline (FinanzRechnerLoadingPipeline)

Zwei-Stufen-Modell, damit die UI nicht blockiert:

1. **DB+Shader** (Weight 40): ExpenseService + AccountService + SavingsGoalService +
   DebtService + CustomCategoryService + PurchaseService + ShaderPreloader parallel mit
   `Task.WhenAll`. Danach Währungs-Preset aus Preferences laden + CurrencyHelper konfigurieren.
2. **ViewModel** (Weight 15): MainViewModel aus DI auflösen (bindet an bereits geladene Daten).

### Cache-Invalidierung (Tab-Wechsel)

Das ist der häufigste Punkt wo Bugs entstehen — jede VM hat ein eigenes Stale-Flag:

- `StatisticsViewModel.InvalidateCache()` + `_isDataStale` — lädt nur bei Änderungen neu
- `ExpenseTrackerViewModel.InvalidateCache()` + `DataChanged` Event → MainViewModel
- `BudgetsViewModel.DataChanged` + `RecurringTransactionsViewModel.DataChanged` → MainViewModel
- `MainViewModel._isHomeDataStale` lauscht auf alle drei `DataChanged`-Events

### Transfer-Buchung (Kontoüberweisungen)

**EINE** Transfer-Transaktion, KEIN Doppel-Record:

- `AccountId` = Quell-Konto, `TransferToAccountId` = Ziel-Konto, `Type = Transfer`
- Saldo-Berechnung: `transfersOut` wenn AccountId passt, `transfersIn` wenn TransferToAccountId passt
- Doppel-Record würde Saldo verfälschen (beide Seiten als Ausgabe gezählt)

### Backup-Format v2.0

Container-JSON mit Schlüsseln: `version`, `expenses`, `accounts`, `savings_goals`,
`debts`, `custom_categories`. Rückwärtskompatibel: fehlendes `version`-Feld = altes Format,
nur Expenses importieren. SettingsViewModel injiziert alle 5 Services für Backup/Restore.

Restore hat Merge/Replace-Dialog (modale Overlay in SettingsView):
- `RestoreMergeCommand` → `ProcessRestoreFileAsync(path, merge:true)`
- `RestoreReplaceCommand` → `ProcessRestoreFileAsync(path, merge:false)`

### CurrencyHelper

```csharp
// Einmalig beim Start (LoadingPipeline Schritt 1)
CurrencyHelper.Configure(CurrencySettings.Presets.First(p => p.CurrencyCode == currencyCode));
```

16 Währungs-Presets. Symbol-Position und Dezimalformat automatisch korrekt.
Konfiguration ist global — kein Service, kein DI nötig.

**Overloads für decimal + double:** `Format`, `FormatSigned`, `FormatCompactSigned`,
`FormatAxis`, `FormatInvariant` existieren beide Varianten. Datenebene (Models, Services,
Datenebenen-VMs) nutzen `decimal`. Calculator-VMs (Math.Pow-basiert) nutzen `double` für
Eingaben und konvertieren intern wenn nötig. Bei Aufrufen mit Literal `0` ist Disambiguierung
nötig: `CurrencyHelper.Format(0m)` statt `Format(0)`.

### Geldwerte-Typ-Konvention (decimal)

Datenebene: **decimal** für alle Geldwerte. Models (`Expense.Amount`, `Account.InitialBalance`,
`Budget.MonthlyLimit`, `DebtEntry.*`, `SavingsGoal.*`, `RecurringTransaction.Amount`),
Services (`GetTotalExpensesAsync` → `Task<decimal>`, `AccountBalance.CurrentBalance` etc.),
ViewModel-Felder mit Geld-Semantik (`MonthlyIncome/Expenses/Balance`, `TotalBudgetLimit`,
`NetWorth`, `TotalDebt`, `TotalSaved`, `LastMonthExpenses`).

**Calculator-Math bleibt double:** `FinanceEngine.CalculateXxx` nutzt `double` für `Math.Pow`
(Zinseszins-Formel, Tilgungsplan, Inflation). Result-Records (`CompoundInterestResult`,
`LoanResult`, etc.) bleiben `double`. Calculator-ViewModels (`CompoundInterestViewModel.Principal`,
`LoanViewModel.LoanAmount`) bleiben `double` als User-Eingabe — NaN/Infinity-Guards via
`double.IsFinite()` PFLICHT vor `_financeEngine.CalculateXxx`.

**System.Text.Json:** deserialisiert alte JSON-Backups (mit `double`-Werten) automatisch in
`decimal`-Properties — keine Migration nötig.

**ProgressPercent / RemainingAmount:** Computed-Properties die mit `decimal` rechnen müssen
`100m` / `0m` als Literal nutzen (sonst Compiler-Fehler "/ kann nicht auf decimal+double").

### Theme-Tokens für Income/Expense/Transfer

App-eigene Semantic-Brushes in `Themes/AppPalette.axaml`:

```xml
<SolidColorBrush x:Key="IncomeBrush" Color="#22C55E"/>
<SolidColorBrush x:Key="ExpenseBrush" Color="#EF4444"/>
<SolidColorBrush x:Key="TransferBrush" Color="#06B6D4"/>
<SolidColorBrush x:Key="IncomeBackgroundBrush"  Color="#3322C55E"/>  <!-- 20% Alpha -->
<SolidColorBrush x:Key="ExpenseBackgroundBrush" Color="#33EF4444"/>
<SolidColorBrush x:Key="TransferBackgroundBrush" Color="#3306B6D4"/>
```

NIEMALS Hex-Literale für Income/Expense in Views — IMMER `{DynamicResource IncomeBrush}`
etc. damit Marketing-Änderungen zentral greifen.

### Atomic File-Persistenz

Alle Service-Save-Methoden nutzen `MeineApps.Core.Ava.Services.AtomicFileWriter`:

```csharp
var json = JsonSerializer.Serialize(_data, _jsonOptions);
await AtomicFileWriter.WriteAllTextAsync(_filePath, json);
```

Pattern: Schreibe in `{path}.tmp`, dann `File.Move(.tmp, target, overwrite)`. Atomar gegen
Crash/Power-Loss. Vorher pro-Service dupliziert — jetzt zentraler Helper.

---

## Daten-Modell

### Expense-Erweiterungen (rückwärtskompatibel, alle nullable)

| Property | Zweck |
|----------|-------|
| `AccountId` | Konto-Zuordnung |
| `CustomCategoryId` | Überschreibt Standard-Kategorie-Enum |
| `TransferToAccountId` + `TransferId` | Überweisungen zwischen Konten |
| `SplitItems` | Split-Transaktionen (Liste Kategorie+Betrag, Model vorbereitet) |
| `TransactionType.Transfer` | Dritter Typ neben Expense/Income |

### Persistenz (JSON-Dateien)

| Datei | Service |
|-------|---------|
| `expenses.json` | IExpenseService (inkl. Budgets + Recurring) |
| `accounts.json` | IAccountService |
| `savings_goals.json` | ISavingsGoalService |
| `debts.json` | IDebtService |
| `custom_categories.json` | ICustomCategoryService |

IFinancialAnalysisService hat keine eigene Persistenz — berechnet aus den anderen Services.

---

## SkiaSharp-Visualisierungen

### App-spezifische Renderer (`Graphics/`)

| Datei | Zweck |
|-------|-------|
| `FinanceBackgroundRenderer.cs` | Animierter Background MainView (~5fps: Smaragd-Gradient, Chart-Linien, Mini-Balken, Sparkle, Vignette) |
| `FinanceDashboardRenderer.cs` | Animierter Hero-Header-Hintergrund (Gradient-Mesh, Grid-Linien, Glow-Dots, Floating-Symbole) |
| `CalculatorHeaderRenderer.cs` | 6 individuelle animierte Headers pro Rechner |
| `CardGlowRenderer.cs` | Status-basierter Edge-Glow (Budget-Status, Bilanz, Berechnungs-Flash) |
| `FinanzRechnerSplashRenderer.cs` | Splash "Das wachsende Kapital" (Aktien-Chart, Münz-Stapel, Gold-Partikel) |
| `SparklineVisualization.cs` | Mini-Sparkline mit Gradient-Füllung (30-Tage-Ausgaben-Trend) |
| `BudgetMiniRingVisualization.cs` | Kompakte Mini-Ringe für Budget-Kategorien-Übersicht |
| `TrendLineVisualization.cs` | 2 Spline-Kurven (Einnahmen/Ausgaben) mit Gradient-Füllung |
| `StackedAreaVisualization.cs` | 2 gestapelte Flächen (CompoundInterest, SavingsPlan, Inflation) |
| `AmortizationBarVisualization.cs` | Gestapelte Balken (Tilgung+Zinsen pro Jahr) |
| `BudgetGaugeVisualization.cs` | Halbkreis-Tachometer (Legacy, ersetzt durch SkiaGradientRing) |
| `ChartHelper.cs` | Gemeinsame Y-Achsen-Skalierung und Label-Formatierung |

### Shared-Renderer aus `MeineApps.UI.SkiaSharp`

- **DonutChartVisualization**: HomeView, StatisticsView, ExpenseTrackerView, LoanView, YieldView
- **LinearProgressVisualization**: Budget-Fortschrittsbalken in BudgetsView
- **SkiaGradientRing**: Gesamt-Budget in HomeView + BudgetsView

### View → Renderer Zuordnung

| View | Renderer |
|------|---------|
| MainView | FinanceBackgroundRenderer (Grid.RowSpan=3) |
| HomeView | FinanceDashboardRenderer + SkiaGradientRing + Sparkline + MiniRing + Expense-Donut |
| StatisticsView | 2× Donut + TrendLine |
| ExpenseTrackerView | Kategorie-Donut |
| BudgetsView | SkiaGradientRing + LinearProgress pro Kategorie |
| CompoundInterest/SavingsPlan/InflationView | StackedArea |
| AmortizationView | Stacked-Bar |
| LoanView/YieldView | Donut |

---

## Behaviors & Game Juice

### Behaviors pro View

| View | Behaviors |
|------|-----------|
| **HomeView** | CountUpBehavior (Saldo, Einnahmen, Ausgaben, 800ms CubicEaseOut), StaggerFadeInBehavior (Recent-Items 40ms, Calculator-Karten 60ms FixedIndex 0-5), TapScaleBehavior (0.92–0.97) |
| **ExpenseTrackerView** | FadeInBehavior (250ms SlideFromBottom), StaggerFadeInBehavior (Transaction-Items 40ms), SwipeToRevealBehavior (80px → roter Delete-Layer), TapScaleBehavior |
| **BudgetsView** | FadeInBehavior, StaggerFadeInBehavior (50ms), CountUpBehavior (Spent/Remaining/Limit), TapScaleBehavior, AlertLevelToBoxShadowConverter (Safe=grün, Warning=gelb, Exceeded=rot), ContextFlyout |
| **RecurringTransactionsView** | FadeInBehavior, StaggerFadeInBehavior (40ms), SwipeToRevealBehavior, TapScaleBehavior |
| **Calculator-Views (alle 6)** | CountUpBehavior (Geldbeträge, 600ms), TapScaleBehavior (Berechnen-Button 0.95), Gold-Flash (ResultFlash CSS) |

### Weitere Juice-Elemente

- **FloatingText**: Quick-Add (+/- Betrag, income=grün, expense=rot)
- **Celebration**: Confetti bei Budget-Analyse (CelebrationRequested Event)
- **Kombination StaggerFadeIn + TapScale** auf Calculator-Karten: Panel-Wrapper (Stagger) + Button-Kind (TapScale) — weil beide verschiedene RenderTransform-Typen setzen
- **Undo-Countdown**: ScaleX 1→0 über 5s als visueller Balken in Undo-Snackbars
- **Farbige Kategorie-Chips**: QuickAdd, AddExpense, AddRecurring (CategoryToColorBrushConverter)
- **Gruppierte Transaktionen**: Date-Headers mit Tages-Summe + Notiz-Anzeige

---

## Ad-Placements (Rewarded)

| Placement-ID | Auslöser | View |
|-------------|----------|------|
| `export_pdf` | PDF-Export | StatisticsView |
| `export_csv` | CSV-Export | ExpenseTrackerView + StatisticsView |
| `budget_analysis` | Monatsreport mit Kategorie-Breakdown + Spartipps | HomeView |
| `extended_stats` | 24h-Zugang zu Quartal/Halbjahr/Jahr Statistiken | StatisticsView |

Premium-Nutzer (remove_ads 3,99 EUR): Keine Ads, direkter Export, unbegrenzter Budget-Report,
permanente erweiterte Statistiken.

---

## Back-Navigation (Double-Back-to-Exit)

Reihenfolge in `HandleBackPressed()`:

1. BudgetAnalysis-Overlay schließen
2. BudgetAd-Overlay schließen
3. QuickAdd-Overlay schließen
4. RestoreDialog (Settings) schließen
5. AddExpense-Overlay (Tracker) schließen
6. SubPage-Dialoge (AddBudget/AddRecurring) schließen
7. SubPage schließen (GoBack `".."`)
8. Calculator schließen
9. Tab → Home
10. Double-Back-Exit (2s Fenster, RESX-Key `PressBackToExit`, BackPressHelper)

---

## Bekannte Gotchas

### SKCanvasView in unsichtbaren Containern

Calculator-Views liegen in `Border IsVisible="{Binding IsXxxActive}"`. Wenn
`InvalidateSurface()` auf einer unsichtbaren Canvas aufgerufen wird, feuert PaintSurface
**nicht**. Deshalb rufen alle `OpenXxx()` Commands im MainViewModel immer
`CalculateCommand.Execute(null)` auf (kein HasResult-Guard), damit nach dem Sichtbar-Werden
ein frisches PropertyChanged → InvalidateSurface() → PaintSurface ausgelöst wird.

### BudgetDisplayItem Sprachwechsel

`BudgetDisplayItem` ist ObservableObject mit `CategoryName` Property. Bei Sprachwechsel
muss `UpdateLocalizedTexts()` die Property neu setzen — direktes Enum-Binding gibt immer
Englisch.

### Export-Pfad Android

`ExportService.GetExportDirectory()` gibt Android external-files-path zurück. Nach Export
immer `IFileShareService.ShareFileAsync()` aufrufen. FileDialog-Fallback auf hardcodierte
Pfade wenn StorageProvider nicht verfügbar.

### Smaragd-Theme (#10B981)

Alle DynamicResource-Keys identisch zu anderen Apps. App-spezifisches Palette in
`Themes/AppPalette.axaml` (per `<StyleInclude />` in App.axaml). Design-Tokens aus
`MeineApps.Core.Ava/Themes/ThemeColors.axaml`. Kein dynamischer Theme-Wechsel.

### HomeView 60-fps-Timer

Der Dashboard-Hintergrund läuft auf einem 60-fps DispatcherTimer. Avalonia 12 detacht
`IsVisible="False"`-Tabs NICHT aus dem Visual Tree → ohne Schutz würde der Timer beim
Wechsel auf Tracker/Stats/Settings weitertick'ten. `HomeView.UpdateTimerState()`
abonniert `MainViewModel.IsHomeActive`/`SelectedTab` und stoppt/startet den Timer
synchron — Akku/CPU bleiben bei Tab-Wechsel auf 0.

### DateTime — Idempotenz vs. User-Anzeige

`ProcessDueRecurringTransactionsAsync` nutzt `DateTime.UtcNow.Date` als Idempotenz-Schlüssel
(`last_processed.txt`). Reisen durch Zeitzonen erzeugen keine Doppel-Verarbeitung. Das
Buchungs-Datum (`Expense.Date`) bleibt lokal — User-Erwartung "am 15. des Monats" bezieht
sich auf die lokale Uhr.

### CancellationToken-Race bei Insights

`MainViewModel.LoadFinancialInsightsAsync` cancelt parallele Aufrufe via
`_insightsCts.Token`. Der Token wird an `IFinancialAnalysisService.GetAllInsightsAsync(ct)`
weitergereicht (intern via `Task.WhenAll().WaitAsync(ct)`) — schnelle Tab-Wechsel brechen
laufende Berechnungen ab, statt nur die UI-Update-Phase zu überspringen.

### Compiled Bindings + ItemsRepeater

Alle 18 Views haben `x:CompileBindings="True"` — falsche Property-Bindings fliegen beim
Build auf. ItemsControl wird NICHT auf ItemsRepeater migriert: F-09 (Clear+Add statt
`new ObservableCollection(...)`) eliminiert das Re-Mount-Problem; bei realistischen
User-Datenmengen (< 100 Tagesgruppen, < 30 Transaktionen pro Tag) ist Virtualisierung
overkill und würde StaggerFadeIn/SwipeToReveal-Behaviors brechen.

---

## Build / Test / Deploy

```bash
# Build
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
dotnet run --project src/Apps/FinanzRechner/FinanzRechner.Desktop

# Android Release (AAB)
dotnet publish src/Apps/FinanzRechner/FinanzRechner.Android -c Release

# AppChecker
dotnet run --project tools/AppChecker FinanzRechner
```

---

## Verweise

| Datei | Zweck |
|-------|-------|
| `F:\Meine_Apps_Ava\CLAUDE.md` | Build, Conventions, DI-Patterns, Architektur |
| `src/Libraries/MeineApps.Core.Ava/CLAUDE.md` | Themes, Services (BackPressHelper, UriLauncher, CurrencyHelper) |
| `src/Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md` | AdMob, IAP, RewardedAdHelper |
| `src/UI/MeineApps.UI/CLAUDE.md` | Shared Behaviors, DonutChart, SkiaGradientRing, LinearProgress |
| `Releases/FinanzRechner/CHANGELOG_*.md` | Release-Notes |
