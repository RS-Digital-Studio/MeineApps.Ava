using FinanzRechner.Models;

namespace FinanzRechner.Services;

/// <summary>
/// Service für Kontoverwaltung (Multi-Konto + Überweisungen).
/// </summary>
public interface IAccountService
{
    Task InitializeAsync();

    // Konto-CRUD
    Task<Account> CreateAccountAsync(Account account);
    Task<bool> UpdateAccountAsync(Account account);
    Task<bool> DeleteAccountAsync(string id);
    Task<Account?> GetAccountAsync(string id);
    Task<IReadOnlyList<Account>> GetAllAccountsAsync();

    // Kontostand-Berechnungen
    Task<double> GetAccountBalanceAsync(string accountId);
    Task<IReadOnlyList<AccountBalance>> GetAllAccountBalancesAsync();
    Task<double> GetNetWorthAsync();

    // Überweisungen
    Task<(Expense from, Expense to)> CreateTransferAsync(
        string fromAccountId, string toAccountId,
        double amount, string description, DateTime date, string? note = null);

    // Daten-Export/Import
    Task<string> ExportToJsonAsync();
    Task<int> ImportFromJsonAsync(string json, bool merge = false);
}
