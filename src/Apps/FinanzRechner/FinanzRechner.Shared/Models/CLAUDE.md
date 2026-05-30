# Models — Datenmodelle & Berechnungsengine

Datenebene der App. Alle Geldwerte als `decimal` (außer `FinanceEngine`-Berechnungen, die
`Math.Pow` verwenden und deshalb `double` benötigen). Generische Conventions →
[Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Inhalt |
|-------|--------|
| `Expense.cs` | `Expense`, `TransactionType` (Expense/Income/Transfer), `ExpenseCategory`, `MonthSummary`, `ExpenseFilter`, `ExpenseGroup` |
| `Account.cs` | `Account`, `AccountBalance` |
| `Budget.cs` | `Budget`, `BudgetStatus`, `BudgetAlertLevel` (Safe/Warning/Exceeded) |
| `RecurringTransaction.cs` | `RecurringTransaction`, `RecurringPattern` (Daily/Weekly/Monthly/Yearly) |
| `SavingsGoal.cs` | `SavingsGoal` mit `ProgressPercent` (computed, `decimal`) |
| `DebtEntry.cs` | `DebtEntry` mit Zahlungshistorie |
| `SplitItem.cs` | `SplitItem` — Kategorie + Betrag für Split-Transaktionen |
| `BudgetDisplayItem.cs` | `BudgetDisplayItem : ObservableObject` — VM-nähes Anzeigeobjekt mit `CategoryName` Property |
| `BudgetAnalysisReport.cs` | Report-Datenstruktur für Budget-Analyse-Overlay |
| `FinancialScore.cs` | `FinancialScore` + `FinancialForecast` — Ergebnisse der Analyse-Services |
| `MonthComparison.cs` | Vergleichsdaten aktueller Monat vs. Vormonat |
| `CategoryDisplayItem.cs` | Display-Helper für Kategorie-Picker in UI |
| `CustomCategory.cs` | Benutzerdefinierte Kategorie (Id, Name, Icon, ColorHex) |
| `CurrencySettings.cs` | `CurrencySettings` — 16 Währungs-Presets; wird in `CurrencyHelper.Configure()` einmalig gesetzt |
| `FinanceEngine.cs` | Berechnungsengine für alle 6 Finanzrechner + Result-Records |

---

## Geldwerte-Typ-Konvention

**Datenebene: `decimal`** für alle Geldwerte:
- `Expense.Amount`, `Account.InitialBalance`, `Budget.MonthlyLimit`,
  `DebtEntry.*`, `SavingsGoal.*`, `RecurringTransaction.Amount`.
- Service-Rückgaben: `GetTotalExpensesAsync()` → `Task<decimal>`,
  `AccountBalance.CurrentBalance` etc.
- ViewModel-Felder mit Geld-Semantik: `MonthlyIncome/Expenses/Balance`, `NetWorth`, `TotalDebt`.

**Calculator-Math: `double`** — `FinanceEngine.CalculateXxx()` nutzt `Math.Pow` (Zinseszins,
Tilgungsplan, Inflation). Result-Records (`CompoundInterestResult`, `LoanResult` etc.) bleiben
`double`. Calculator-ViewModels nutzen `double` als Eingabe, prüfen via `double.IsFinite()`.

**Literal-Disambiguierung:** `CurrencyHelper.Format(0m)` statt `Format(0)` — sonst
Compiler-Fehler "Overload zwischen decimal und double nicht eindeutig".

---

## FinanceEngine

Singleton, kein Interface (rein mathematisch, kein Mocking nötig). Alle Methoden werfen bei
ungültigen Ergebnissen `OverflowException` (intern via `ValidateResult()`). Garantiert damit,
dass keine `Infinity`/`NaN`-Werte in die UI durchdringen.

```csharp
CalculateCompoundInterest(principal, annualRate, years, compoundingsPerYear)
CalculateSavingsPlan(monthlyDeposit, annualRate, years, initialDeposit)
CalculateLoan(loanAmount, annualRate, years)
CalculateAmortization(loanAmount, annualRate, years)   // gibt vollständigen Schedule zurück
CalculateEffectiveYield(initialInvestment, finalValue, years)
CalculateInflation(currentAmount, annualInflationRate, years)
```

---

## Expense-Erweiterungen (rückwärtskompatibel, alle nullable)

| Property | Zweck |
|----------|-------|
| `AccountId` | Konto-Zuordnung (null = keinem Konto zugeordnet) |
| `CustomCategoryId` | Überschreibt Standard-`Category`-Enum wenn gesetzt |
| `TransferToAccountId` + `TransferId` | Überweisungen (EINE Transaktion, kein Doppel-Record) |
| `SplitItems` | Split-Transaktionen (vorbereitet, noch nicht in UI) |

---

## Gotchas

**`ProgressPercent` und `RemainingAmount` in SavingsGoal:** Computed-Properties rechnen mit
`decimal` → Literale IMMER als `100m` / `0m` schreiben (sonst Compiler-Fehler).

**System.Text.Json Migration:** Alte Backups mit `double`-Werten in JSON deserialisiert
automatisch korrekt in `decimal`-Properties — keine Migration nötig.
