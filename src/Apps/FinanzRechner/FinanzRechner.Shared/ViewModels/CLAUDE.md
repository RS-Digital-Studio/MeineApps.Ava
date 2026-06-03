# ViewModels — Finanz-Logik & Navigation

Alle ViewModels sind **Singleton** (in `App.axaml.cs` registriert) und werden vom
`MainViewModel` per Constructor-Injection gehalten. Nur UI-Logik — Domänen-Berechnungen
delegieren an `FinanceEngine` (Zinseszins-Formeln etc.) und die Service-Schicht.
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainViewModel.cs` | Konstruktor, Tab-Navigation (4 Tabs), Back-Press-Flow, AdBanner, Events, Sub-Page-Routing |
| `MainViewModel.Home.cs` | Dashboard-Logik: Saldo/Einnahmen/Ausgaben, Quick-Add FAB, Budget-Status, Financial-Insights-Abfrage |
| `ExpenseTrackerViewModel.cs` | Expense-CRUD, Filter/Sort, Monatsnavigation, Undo-Delete, DataChanged-Event |
| `StatisticsViewModel.cs` | Charts, Monatsvergleich, 6-Monats-Trend, `_isDataStale`-Cache, CSV/PDF-Export via Rewarded |
| `BudgetsViewModel.cs` | Budget-Limits pro Kategorie, Alert-Levels, ContextFlyout, DataChanged-Event |
| `RecurringTransactionsViewModel.cs` | Daueraufträge CRUD, Toggle aktiv/inaktiv, DataChanged-Event |
| `AccountsViewModel.cs` | Multi-Konto + Überweisungen zwischen Konten |
| `SavingsGoalsViewModel.cs` | Sparziele + Einzahlungen/Entnahmen + CelebrationRequested bei 100 % |
| `DebtTrackerViewModel.cs` | Schulden verwalten + Zahlungen buchen + Tilgungsberechnung |
| `CustomCategoriesViewModel.cs` | Benutzerdefinierte Kategorien mit Icon und Farbe |
| `SettingsViewModel.cs` | Währung, Backup v2, Restore Merge/Replace, Sprache, Premium |
| `Calculators/CompoundInterestViewModel.cs` | Zinseszins-Rechner (CalculateCompoundInterest) |
| `Calculators/SavingsPlanViewModel.cs` | Sparplan-Rechner |
| `Calculators/LoanViewModel.cs` | Kredit-Rechner (CalculateLoan) |
| `Calculators/AmortizationViewModel.cs` | Tilgungsplan-Rechner, Jahres-Aggregate für Chart |
| `Calculators/YieldViewModel.cs` | Rendite-Rechner (CalculateEffectiveYield) |
| `Calculators/InflationViewModel.cs` | Inflations-Rechner (CalculateInflation) |

---

## Cache-Invalidierung (Tab-Wechsel)

Jede VM hat ein eigenes Stale-Flag — lädt nur bei tatsächlichen Änderungen neu:

- `StatisticsViewModel.InvalidateCache()` + `_isDataStale` — lädt nur bei Änderungen neu.
- `MainViewModel._isHomeDataStale` wird von **allen** `DataChanged`-Events gesetzt:
  `ExpenseTrackerViewModel`, `BudgetsViewModel`, `RecurringTransactionsViewModel`,
  `AccountsViewModel`, `SavingsGoalsViewModel`, `DebtTrackerViewModel`, `CustomCategoriesViewModel`.

---

## Financial-Insights-Bundle

`MainViewModel.LoadFinancialInsightsAsync()` ruft `IFinancialAnalysisService.GetAllInsightsAsync(ct)`
auf. Der Token wird via `_insightsCts` verwaltet — schnelle Tab-Wechsel canceln laufende
Berechnungen sofort statt nur das UI-Update zu überspringen.

---

## Calculator-VMs — gemeinsames Pattern

Alle 6 Calculator-ViewModels folgen dem gleichen Muster:

```
XxxValue (double)    → CountUpBehavior (sichtbare Animation)
XxxDisplay (string)  → TextBlock-Binding (formatierter Wert)
CalculateCommand     → öffnet/berechnet immer neu (kein HasResult-Guard!)
```

`double.IsFinite()` PFLICHT vor `_financeEngine.CalculateXxx` — ungültige Eingaben (NaN,
Infinity) erzeugen sonst OverflowException aus der Engine.

Alle Calculator-VMs haben einen **Debounce-Timer** (300 ms): Property-Änderungen triggern
`ScheduleAutoCalculate()`, das nach 300 ms `Calculate()` auf dem UI-Thread aufruft. Der Timer
wird in `Dispose()` freigegeben.

Nach Berechnung: CSS-Klasse `ResultFlash` auf dem Ergebnis-Panel (Gold-Aufleuchten).

---

## Back-Navigation (Reihenfolge in `HandleBackPressed`)

1. BudgetAnalysis-Overlay schließen
2. BudgetAd-Overlay schließen
3. QuickAdd-Overlay schließen
4. RestoreDialog (Settings, nur wenn Tab == 3) schließen
5. AddExpense-Overlay (Tracker, nur wenn Tab == 1) schließen
6. SubPage-interne Dialoge schließen (AddBudget, AddRecurring, Accounts-Dialog/Transfer,
   SavingsGoals-Dialog/AdjustDialog, DebtTracker-Dialog/PaymentDialog, CustomCategories-Dialog),
   danach SubPage selbst schließen
7. Calculator schließen
8. Tab → Home
9. Double-Back-Exit (2 s Fenster, `BackPressHelper`, RESX-Key `PressBackToExit`,
   Event `ExitHintRequested`)

---

## Gotchas

**BudgetDisplayItem Sprachwechsel:** `BudgetDisplayItem.CategoryName` ist eine ObservableObject-Property.
Bei `UpdateLocalizedTexts()` muss die Property explizit neu gesetzt werden — direktes Enum-Binding
gibt immer den englischen Enum-Namen zurück.

**SKCanvasView unsichtbar:** Calculator-Views liegen in `Border IsVisible="{Binding IsXxxActive}"`.
`InvalidateSurface()` auf unsichtbare Canvas → PaintSurface feuert **nicht**. Deshalb rufen alle
`OpenXxx()`-Commands `CalculateCommand.Execute(null)` auf (kein HasResult-Guard), damit nach
Sichtbar-Werden sofort ein frischer PaintSurface ausgelöst wird.
