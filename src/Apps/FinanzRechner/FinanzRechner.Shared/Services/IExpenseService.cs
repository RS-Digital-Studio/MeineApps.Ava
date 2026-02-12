using FinanzRechner.Models;

namespace FinanzRechner.Services;

/// <summary>
/// Service für Ausgabenverwaltung (CRUD + Statistiken)
/// </summary>
public interface IExpenseService
{
    /// <summary>Wird ausgelöst wenn beim Laden Fehler auftreten (Datei korrupt etc.)</summary>
    event Action<string>? OnDataLoadError;

    Task InitializeAsync();
    Task<IReadOnlyList<Expense>> GetAllExpensesAsync();
    Task<IReadOnlyList<Expense>> GetExpensesByMonthAsync(int year, int month);
    Task<IReadOnlyList<Expense>> GetExpensesAsync(ExpenseFilter filter);
    Task<Expense?> GetExpenseAsync(string id);
    Task<Expense> AddExpenseAsync(Expense expense);
    Task<bool> UpdateExpenseAsync(Expense expense);
    Task<bool> DeleteExpenseAsync(string id);
    Task<MonthSummary> GetMonthSummaryAsync(int year, int month);
    Task<double> GetTotalExpensesAsync(DateTime startDate, DateTime endDate);
    Task ClearAllExpensesAsync();

    // Budget-Verwaltung
    Task<Budget> SetBudgetAsync(Budget budget);
    Task<Budget?> GetBudgetAsync(ExpenseCategory category);
    Task<IReadOnlyList<Budget>> GetAllBudgetsAsync();
    Task<bool> DeleteBudgetAsync(ExpenseCategory category);
    Task<BudgetStatus?> GetBudgetStatusAsync(ExpenseCategory category);
    Task<IReadOnlyList<BudgetStatus>> GetAllBudgetStatusAsync();

    // Daueraufträge
    Task<RecurringTransaction> CreateRecurringTransactionAsync(RecurringTransaction transaction);
    Task<bool> UpdateRecurringTransactionAsync(RecurringTransaction transaction);
    Task<bool> DeleteRecurringTransactionAsync(Guid id);
    Task<RecurringTransaction?> GetRecurringTransactionAsync(Guid id);
    Task<IReadOnlyList<RecurringTransaction>> GetAllRecurringTransactionsAsync();
    Task<int> ProcessDueRecurringTransactionsAsync();

    // Sicherung & Wiederherstellung
    Task<string> ExportToJsonAsync();
    Task<int> ImportFromJsonAsync(string json, bool mergeData = false);
}
