# Services — Persistenz, Export & Analyse

Alle Services als **Singleton** in `App.axaml.cs` registriert. Persistenz via JSON-Dateien
mit `AtomicFileWriter` (Schreibe nach `.tmp`, dann `File.Move`) gegen Crash/Power-Loss.
Generische Service-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `IExpenseService` / `ExpenseService` | Primärer Service: Expenses CRUD, Budget-Verwaltung, Daueraufträge, JSON-Backup/Restore. Persistenz in `expenses.json`. |
| `IAccountService` / `AccountService` | Konten verwalten + Saldo berechnen. Persistenz in `accounts.json`. Nutzt `IExpenseService` für Saldo-Berechnungen. |
| `ISavingsGoalService` / `SavingsGoalService` | Sparziele + Einzahlungen/Entnahmen. Persistenz in `savings_goals.json`. |
| `IDebtService` / `DebtService` | Schulden + Zahlungshistorie. Persistenz in `debts.json`. |
| `ICustomCategoryService` / `CustomCategoryService` | Benutzerdefinierte Kategorien. Persistenz in `custom_categories.json`. |
| `IFinancialAnalysisService` / `FinancialAnalysisService` | Score, Monatsvergleich, Prognose, Nettovermögen. **Keine eigene Persistenz** — berechnet aus den anderen Services. |
| `IExportService` / `ExportService` | CSV + PDF (PdfSharpCore). Delegiert Datei-Teilen an `IFileShareService`. |
| `IFileDialogService` / `FileDialogService` | `StorageProvider.SaveFilePickerAsync` / `OpenFilePickerAsync`. |
| `INotificationService` / `NotificationService` | In-App-Benachrichtigungen für Daueraufträge und Budget-Alerts. |

---

## Persistenz-Dateien

| Datei | Service |
|-------|---------|
| `expenses.json` + `budgets.json` + `recurring_transactions.json` + `notifications.json` | `IExpenseService` (separate Dateien, alle vom ExpenseService verwaltet) |
| `accounts.json` | `IAccountService` |
| `savings_goals.json` | `ISavingsGoalService` |
| `debts.json` | `IDebtService` |
| `custom_categories.json` | `ICustomCategoryService` |

---

## IExpenseService — wichtige Besonderheiten

**Budget-Verwaltung ist im ExpenseService** integriert (nicht separater Service), da Budgets
direkt die Ausgaben-Daten auswerten.

**Daueraufträge:** `ProcessDueRecurringTransactionsAsync()` nutzt `DateTime.UtcNow.Date` als
Idempotenz-Schlüssel (`last_processed.txt`). Buchungs-Datum (`Expense.Date`) bleibt lokal
(User-Erwartung "am 15. des Monats" bezieht sich auf die lokale Uhr).

**Backup-Format v2.0:** Container-JSON mit Schlüsseln `version`, `expenses`, `accounts`,
`savings_goals`, `debts`, `custom_categories`. Fehlendes `version`-Feld = altes Format →
nur Expenses importieren (Rückwärtskompatibilität).

---

## IFinancialAnalysisService — API

```csharp
Task<FinancialScore>        CalculateScoreAsync();
Task<MonthComparison>       GetMonthComparisonAsync(int year, int month);
Task<FinancialForecast>     GetForecastAsync();
Task<decimal>               CalculateNetWorthAsync();
Task<FinancialInsightsBundle> GetAllInsightsAsync(CancellationToken ct = default);
```

`GetAllInsightsAsync` bündelt Score + Forecast + NetWorth in einem Aufruf, um redundante
Daten-Abfragen zu vermeiden. Der CancellationToken wird intern via `Task.WhenAll().WaitAsync(ct)`
weitergereicht — schnelle Tab-Wechsel brechen laufende Berechnungen sofort ab.

---

## Transfer-Buchung (Kontoüberweisungen)

**EINE** Transfer-Transaktion, **KEIN Doppel-Record**:
- `AccountId` = Quell-Konto, `TransferToAccountId` = Ziel-Konto, `Type = Transfer`.
- Saldo: `transfersOut` wenn `AccountId` passt, `transfersIn` wenn `TransferToAccountId` passt.
- Doppel-Record würde Saldo verfälschen (beide Seiten als Ausgabe gezählt).

---

## Export-Pfad

`ExportService` ruft intern `_fileShareService.GetExportDirectory("FinanzRechner")` auf
(`IFileShareService` aus Core.Ava — Android: external-files-path, Desktop: Downloads-Ordner).
Nach dem Schreiben in dieses Verzeichnis **immer** `IFileShareService.ShareFileAsync()` aufrufen —
`ExportService` übernimmt das selbst, ViewModels rufen nur `IExportService.ExportToCsvAsync()`/
`ExportToPdfAsync()` auf.

`IFileShareService` lebt in `MeineApps.Core.Ava` (nicht lokal) — Implementierungen:
`AndroidFileShareService` (Intent.ActionSend) und `DesktopFileShareService` (Downloads-Fallback).
Details → [Core.Ava/CLAUDE.md](../../../../../../Libraries/MeineApps.Core.Ava/CLAUDE.md).
