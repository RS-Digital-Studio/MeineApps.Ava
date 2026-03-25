using System.Text.Json;
using FinanzRechner.Models;

namespace FinanzRechner.Services;

/// <summary>
/// Implementierung der Kontoverwaltung mit lokalem JSON-Speicher.
/// </summary>
public sealed class AccountService : IAccountService, IDisposable
{
    private const string AccountsFile = "accounts.json";
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly IExpenseService _expenseService;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private List<Account> _accounts = [];
    private bool _isInitialized;

    public AccountService(IExpenseService expenseService)
    {
        _expenseService = expenseService;
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FinanzRechner");
        Directory.CreateDirectory(appDataDir);
        _filePath = Path.Combine(appDataDir, AccountsFile);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        await _semaphore.WaitAsync();
        try
        {
            if (_isInitialized) return;
            await LoadAsync();

            // Standard-Konto erstellen wenn keines existiert
            if (_accounts.Count == 0)
            {
                _accounts.Add(new Account
                {
                    Name = "Girokonto",
                    Type = AccountType.Checking,
                    Icon = "\U0001F3E6",
                    ColorHex = "#4CAF50",
                    SortOrder = 0
                });
                await SaveAsync();
            }

            _isInitialized = true;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<Account> CreateAccountAsync(Account account)
    {
        await InitializeAsync();
        ValidateAccount(account);
        await _semaphore.WaitAsync();
        try
        {
            account.Id = Guid.NewGuid().ToString();
            account.CreatedAt = DateTime.UtcNow;
            if (account.SortOrder == 0)
                account.SortOrder = _accounts.Count;
            _accounts.Add(account);
            await SaveAsync();
            return account;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<bool> UpdateAccountAsync(Account account)
    {
        await InitializeAsync();
        ValidateAccount(account);
        await _semaphore.WaitAsync();
        try
        {
            var existing = _accounts.FirstOrDefault(a => a.Id == account.Id);
            if (existing == null) return false;

            existing.Name = account.Name;
            existing.Type = account.Type;
            existing.Icon = account.Icon;
            existing.ColorHex = account.ColorHex;
            existing.InitialBalance = account.InitialBalance;
            existing.SortOrder = account.SortOrder;
            existing.IsActive = account.IsActive;
            existing.IncludeInNetWorth = account.IncludeInNetWorth;

            await SaveAsync();
            return true;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<bool> DeleteAccountAsync(string id)
    {
        await InitializeAsync();
        await _semaphore.WaitAsync();
        try
        {
            var account = _accounts.FirstOrDefault(a => a.Id == id);
            if (account == null) return false;
            // Konto nur deaktivieren, nicht löschen (Transaktionen referenzieren es noch)
            account.IsActive = false;
            await SaveAsync();
            return true;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<Account?> GetAccountAsync(string id)
    {
        await InitializeAsync();
        return _accounts.FirstOrDefault(a => a.Id == id);
    }

    public async Task<IReadOnlyList<Account>> GetAllAccountsAsync()
    {
        await InitializeAsync();
        return _accounts.Where(a => a.IsActive).OrderBy(a => a.SortOrder).ToList();
    }

    public async Task<double> GetAccountBalanceAsync(string accountId)
    {
        // Nutzt GetAllAccountBalancesAsync um N+1 Queries zu vermeiden
        var balances = await GetAllAccountBalancesAsync();
        return balances.FirstOrDefault(b => b.Account.Id == accountId)?.CurrentBalance ?? 0;
    }

    public async Task<IReadOnlyList<AccountBalance>> GetAllAccountBalancesAsync()
    {
        await InitializeAsync();
        var allExpenses = await _expenseService.GetAllExpensesAsync();
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        // Einmal GroupBy statt O(N) pro Konto → O(N) total
        var byAccount = allExpenses
            .Where(e => e.AccountId != null)
            .GroupBy(e => e.AccountId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Transfers nach Zielkonto gruppieren (für transfersIn)
        var transfersByTarget = allExpenses
            .Where(e => e.Type == TransactionType.Transfer && e.TransferToAccountId != null)
            .GroupBy(e => e.TransferToAccountId!)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        var balances = new List<AccountBalance>();

        foreach (var account in _accounts.Where(a => a.IsActive).OrderBy(a => a.SortOrder))
        {
            var accountExpenses = byAccount.GetValueOrDefault(account.Id, []);

            var totalIncome = accountExpenses.Where(e => e.Type == TransactionType.Income).Sum(e => e.Amount);
            var totalExpenses = accountExpenses.Where(e => e.Type == TransactionType.Expense).Sum(e => e.Amount);
            var transfersIn = transfersByTarget.GetValueOrDefault(account.Id, 0);
            var transfersOut = accountExpenses.Where(e => e.Type == TransactionType.Transfer).Sum(e => e.Amount);

            var currentBalance = account.InitialBalance + totalIncome - totalExpenses + transfersIn - transfersOut;

            // Monatliche Werte
            var monthExpensesList = accountExpenses.Where(e => e.Date >= monthStart).ToList();
            var monthIncome = monthExpensesList.Where(e => e.Type == TransactionType.Income).Sum(e => e.Amount);
            var monthExpensesSum = monthExpensesList.Where(e => e.Type == TransactionType.Expense).Sum(e => e.Amount);

            balances.Add(new AccountBalance(account, currentBalance, monthIncome, monthExpensesSum, monthIncome - monthExpensesSum));
        }

        return balances;
    }

    public async Task<double> GetNetWorthAsync()
    {
        var balances = await GetAllAccountBalancesAsync();
        return balances.Where(b => b.Account.IncludeInNetWorth).Sum(b => b.CurrentBalance);
    }

    public async Task<(Expense from, Expense to)> CreateTransferAsync(
        string fromAccountId, string toAccountId,
        double amount, string description, DateTime date, string? note = null)
    {
        var transferId = Guid.NewGuid().ToString();

        // Einzelne Transfer-Transaktion: AccountId = Quelle, TransferToAccountId = Ziel.
        // Saldo-Berechnung: Quelle verliert (transfersOut), Ziel gewinnt (transfersIn).
        // Kein Doppel-Record nötig - ein Record reicht für korrekte Buchführung.
        var transferExpense = new Expense
        {
            Date = date,
            Description = description,
            Amount = amount,
            Type = TransactionType.Transfer,
            AccountId = fromAccountId,
            TransferToAccountId = toAccountId,
            TransferId = transferId,
            Note = note,
            Category = ExpenseCategory.Other
        };

        var added = await _expenseService.AddExpenseAsync(transferExpense);

        // Tuple-Rückgabe beibehalten für API-Kompatibilität
        return (added, added);
    }

    public async Task<string> ExportToJsonAsync()
    {
        await InitializeAsync();
        return JsonSerializer.Serialize(_accounts, _jsonOptions);
    }

    public async Task<int> ImportFromJsonAsync(string json, bool merge = false)
    {
        await InitializeAsync();
        var imported = JsonSerializer.Deserialize<List<Account>>(json) ?? [];
        await _semaphore.WaitAsync();
        try
        {
            if (!merge) _accounts.Clear();
            var existingIds = _accounts.Select(a => a.Id).ToHashSet();
            var count = 0;
            foreach (var account in imported)
            {
                if (merge && existingIds.Contains(account.Id)) continue;
                _accounts.Add(account);
                count++;
            }
            await SaveAsync();
            return count;
        }
        finally { _semaphore.Release(); }
    }

    private async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = await File.ReadAllTextAsync(_filePath);
                _accounts = JsonSerializer.Deserialize<List<Account>>(json) ?? [];
            }
        }
        catch { _accounts = []; }
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_accounts, _jsonOptions);
        var tempPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static void ValidateAccount(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (string.IsNullOrWhiteSpace(account.Name))
            throw new ArgumentException("Kontoname darf nicht leer sein.");
        if (account.Name.Length > 50)
            throw new ArgumentException("Kontoname darf maximal 50 Zeichen lang sein.");
    }

    public void Dispose() => _semaphore.Dispose();
}
