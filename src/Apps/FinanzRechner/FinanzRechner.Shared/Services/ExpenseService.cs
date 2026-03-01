using System.Text.Json;
using FinanzRechner.Helpers;
using FinanzRechner.Models;
using MeineApps.Core.Ava.Localization;

namespace FinanzRechner.Services;

/// <summary>
/// Implementierung des ExpenseService mit lokalem JSON-Speicher.
/// </summary>
public class ExpenseService : IExpenseService, IDisposable
{
    private const string ExpensesFile = "expenses.json";
    private const string BudgetsFile = "budgets.json";
    private const string RecurringFile = "recurring_transactions.json";
    private const string NotificationsFile = "notifications.json";
    private const string LastProcessedFile = "last_processed.txt";
    private const double MaxAmount = 999_999_999.99;
    private const int MaxDescriptionLength = 200;
    private const int MaxNoteLength = 500;
    private const int MaxBackupVersions = 5;
    private static readonly JsonSerializerOptions _jsonWriteOptions = new() { WriteIndented = true };

    private readonly string _expensesFilePath;
    private readonly string _budgetsFilePath;
    private readonly string _recurringFilePath;
    private readonly string _notificationsFilePath;
    private readonly string _lastProcessedFilePath;
    private readonly string _backupDir;
    private readonly INotificationService? _notificationService;
    private readonly ILocalizationService? _localizationService;
    private List<Expense> _expenses = [];
    private List<Budget> _budgets = [];
    private List<RecurringTransaction> _recurringTransactions = [];
    private Dictionary<string, DateTime> _sentNotifications = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _isInitialized;

    /// <summary>Wird ausgelöst wenn beim Laden Fehler auftreten</summary>
    public event Action<string>? OnDataLoadError;

    public ExpenseService(INotificationService? notificationService = null,
        ILocalizationService? localizationService = null)
    {
        _notificationService = notificationService;
        _localizationService = localizationService;

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FinanzRechner");
        Directory.CreateDirectory(appDataDir);

        _expensesFilePath = Path.Combine(appDataDir, ExpensesFile);
        _budgetsFilePath = Path.Combine(appDataDir, BudgetsFile);
        _recurringFilePath = Path.Combine(appDataDir, RecurringFile);
        _notificationsFilePath = Path.Combine(appDataDir, NotificationsFile);
        _lastProcessedFilePath = Path.Combine(appDataDir, LastProcessedFile);
        _backupDir = Path.Combine(appDataDir, "backups");
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        await _semaphore.WaitAsync();
        try
        {
            if (_isInitialized) return;
            await LoadExpensesAsync();
            await LoadBudgetsAsync();
            await LoadRecurringTransactionsAsync();
            await LoadNotificationsAsync();

            // Auto-Backup nach erfolgreichem Laden (4.2)
            await CreateAutoBackupAsync();

            _isInitialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<Expense>> GetAllExpensesAsync()
    {
        await InitializeAsync();
        return _expenses.OrderByDescending(e => e.Date).ToList();
    }

    public async Task<IReadOnlyList<Expense>> GetExpensesByMonthAsync(int year, int month)
    {
        await InitializeAsync();
        return _expenses
            .Where(e => e.Date.Year == year && e.Date.Month == month)
            .OrderByDescending(e => e.Date)
            .ToList();
    }

    public async Task<IReadOnlyList<Expense>> GetExpensesAsync(ExpenseFilter filter)
    {
        await InitializeAsync();
        var query = _expenses.AsEnumerable();

        if (filter.StartDate.HasValue)
            query = query.Where(e => e.Date >= filter.StartDate.Value);
        if (filter.EndDate.HasValue)
            query = query.Where(e => e.Date <= filter.EndDate.Value);
        if (filter.Category.HasValue)
            query = query.Where(e => e.Category == filter.Category.Value);
        if (filter.MinAmount.HasValue)
            query = query.Where(e => e.Amount >= filter.MinAmount.Value);
        if (filter.MaxAmount.HasValue)
            query = query.Where(e => e.Amount <= filter.MaxAmount.Value);

        return query.OrderByDescending(e => e.Date).ToList();
    }

    public async Task<Expense?> GetExpenseAsync(string id)
    {
        await InitializeAsync();
        return _expenses.FirstOrDefault(e => e.Id == id);
    }

    public async Task<Expense> AddExpenseAsync(Expense expense)
    {
        await InitializeAsync();
        ValidateExpense(expense);

        await _semaphore.WaitAsync();
        try
        {
            expense.Id = Guid.NewGuid().ToString();
            _expenses.Add(expense);
            await SaveExpensesAsync();
        }
        finally
        {
            _semaphore.Release();
        }

        if (expense.Type == TransactionType.Expense)
        {
            // Fire-and-forget mit try-catch (4.8)
            _ = Task.Run(async () =>
            {
                try { await CheckBudgetWarningAsync(expense.Category, expense.Date); }
                catch (Exception ex) { System.Diagnostics.Trace.TraceError($"Budget-Warnung fehlgeschlagen: {ex.Message}"); }
            });
        }

        return expense;
    }

    public async Task<bool> UpdateExpenseAsync(Expense expense)
    {
        await InitializeAsync();
        ValidateExpense(expense);

        await _semaphore.WaitAsync();
        try
        {
            var existing = _expenses.FirstOrDefault(e => e.Id == expense.Id);
            if (existing == null) return false;

            existing.Date = expense.Date;
            existing.Description = expense.Description;
            existing.Amount = expense.Amount;
            existing.Category = expense.Category;
            existing.Note = expense.Note;
            existing.Type = expense.Type;

            await SaveExpensesAsync();
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> DeleteExpenseAsync(string id)
    {
        await InitializeAsync();

        await _semaphore.WaitAsync();
        try
        {
            var expense = _expenses.FirstOrDefault(e => e.Id == id);
            if (expense == null) return false;

            _expenses.Remove(expense);
            await SaveExpensesAsync();
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<MonthSummary> GetMonthSummaryAsync(int year, int month)
    {
        var transactions = await GetExpensesByMonthAsync(year, month);

        var totalExpenses = transactions
            .Where(e => e.Type == TransactionType.Expense).Sum(e => e.Amount);
        var totalIncome = transactions
            .Where(e => e.Type == TransactionType.Income).Sum(e => e.Amount);
        var balance = totalIncome - totalExpenses;
        var byCategory = transactions
            .GroupBy(e => e.Category)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        return new MonthSummary(year, month, totalExpenses, totalIncome, balance, byCategory);
    }

    public async Task<double> GetTotalExpensesAsync(DateTime startDate, DateTime endDate)
    {
        var filter = new ExpenseFilter { StartDate = startDate, EndDate = endDate };
        var expenses = await GetExpensesAsync(filter);
        return expenses.Sum(e => e.Amount);
    }

    public async Task ClearAllExpensesAsync()
    {
        await InitializeAsync();

        await _semaphore.WaitAsync();
        try
        {
            _expenses.Clear();
            await SaveExpensesAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    #region Budget Management

    public async Task<Budget> SetBudgetAsync(Budget budget)
    {
        await InitializeAsync();
        ValidateBudget(budget);

        await _semaphore.WaitAsync();
        try
        {
            var existing = _budgets.FirstOrDefault(b => b.Category == budget.Category);
            if (existing != null)
            {
                existing.MonthlyLimit = budget.MonthlyLimit;
                existing.IsEnabled = budget.IsEnabled;
                existing.WarningThreshold = budget.WarningThreshold;
            }
            else
            {
                budget.Id = Guid.NewGuid().ToString();
                _budgets.Add(budget);
            }

            await SaveBudgetsAsync();
            return budget;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Budget?> GetBudgetAsync(ExpenseCategory category)
    {
        await InitializeAsync();
        return _budgets.FirstOrDefault(b => b.Category == category);
    }

    public async Task<IReadOnlyList<Budget>> GetAllBudgetsAsync()
    {
        await InitializeAsync();
        return _budgets.Where(b => b.IsEnabled).ToList();
    }

    public async Task<bool> DeleteBudgetAsync(ExpenseCategory category)
    {
        await InitializeAsync();
        await _semaphore.WaitAsync();
        try
        {
            var budget = _budgets.FirstOrDefault(b => b.Category == category);
            if (budget == null) return false;
            _budgets.Remove(budget);
            await SaveBudgetsAsync();
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<BudgetStatus?> GetBudgetStatusAsync(ExpenseCategory category)
    {
        var budget = await GetBudgetAsync(category);
        if (budget == null || !budget.IsEnabled) return null;

        var today = DateTime.Today;
        var expenses = await GetExpensesByMonthAsync(today.Year, today.Month);
        var spent = expenses
            .Where(e => e.Category == category && e.Type == TransactionType.Expense)
            .Sum(e => e.Amount);

        var remaining = budget.MonthlyLimit - spent;
        var percentageUsed = budget.MonthlyLimit > 0 ? (spent / budget.MonthlyLimit) * 100 : 0;
        var alertLevel = percentageUsed >= 100 ? BudgetAlertLevel.Exceeded :
                        percentageUsed >= budget.WarningThreshold ? BudgetAlertLevel.Warning :
                        BudgetAlertLevel.Safe;

        var localizedName = CategoryLocalizationHelper.GetLocalizedName(category, _localizationService);
        return new BudgetStatus(category, budget.MonthlyLimit, spent, remaining, percentageUsed, alertLevel, localizedName);
    }

    public async Task<IReadOnlyList<BudgetStatus>> GetAllBudgetStatusAsync()
    {
        await InitializeAsync();
        var today = DateTime.Today;
        // Alle Monatsausgaben einmal laden statt pro Budget (N+1 vermeiden)
        var monthExpenses = _expenses
            .Where(e => e.Date.Year == today.Year && e.Date.Month == today.Month && e.Type == TransactionType.Expense)
            .ToList();

        var statusList = new List<BudgetStatus>();
        foreach (var budget in _budgets.Where(b => b.IsEnabled))
        {
            var spent = monthExpenses.Where(e => e.Category == budget.Category).Sum(e => e.Amount);
            var remaining = budget.MonthlyLimit - spent;
            var percentageUsed = budget.MonthlyLimit > 0 ? (spent / budget.MonthlyLimit) * 100 : 0;
            var alertLevel = percentageUsed >= 100 ? BudgetAlertLevel.Exceeded :
                            percentageUsed >= budget.WarningThreshold ? BudgetAlertLevel.Warning :
                            BudgetAlertLevel.Safe;
            var localizedName = CategoryLocalizationHelper.GetLocalizedName(budget.Category, _localizationService);
            statusList.Add(new BudgetStatus(budget.Category, budget.MonthlyLimit, spent, remaining, percentageUsed, alertLevel, localizedName));
        }
        return statusList;
    }

    #endregion

    #region Recurring Transactions

    public async Task<RecurringTransaction> CreateRecurringTransactionAsync(RecurringTransaction transaction)
    {
        ValidateRecurringTransaction(transaction);
        await _semaphore.WaitAsync();
        try
        {
            if (transaction.Id == Guid.Empty) transaction.Id = Guid.NewGuid();
            _recurringTransactions.Add(transaction);
            await SaveRecurringTransactionsAsync();
            return transaction;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> UpdateRecurringTransactionAsync(RecurringTransaction transaction)
    {
        ValidateRecurringTransaction(transaction);
        await _semaphore.WaitAsync();
        try
        {
            var existing = _recurringTransactions.FirstOrDefault(r => r.Id == transaction.Id);
            if (existing == null) return false;
            var index = _recurringTransactions.IndexOf(existing);
            _recurringTransactions[index] = transaction;
            await SaveRecurringTransactionsAsync();
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> DeleteRecurringTransactionAsync(Guid id)
    {
        await _semaphore.WaitAsync();
        try
        {
            var transaction = _recurringTransactions.FirstOrDefault(r => r.Id == id);
            if (transaction == null) return false;
            _recurringTransactions.Remove(transaction);
            await SaveRecurringTransactionsAsync();
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task<RecurringTransaction?> GetRecurringTransactionAsync(Guid id) =>
        Task.FromResult(_recurringTransactions.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<RecurringTransaction>> GetAllRecurringTransactionsAsync() =>
        Task.FromResult<IReadOnlyList<RecurringTransaction>>(
            _recurringTransactions.OrderBy(r => r.Description).ToList());

    public async Task<int> ProcessDueRecurringTransactionsAsync()
    {
        await InitializeAsync();
        var today = DateTime.Today;

        // Nur 1x pro Tag verarbeiten (4.6)
        if (await WasProcessedTodayAsync(today))
            return 0;

        await _semaphore.WaitAsync();
        try
        {
            var count = 0;
            const int maxIterationsPerRecurring = 365; // Schutz vor Endlosschleife (z.B. beschädigte lastProcessed-Datei)
            foreach (var recurring in _recurringTransactions.Where(r => r.IsActive))
            {
                var iterations = 0;
                // Alle verpassten Zeiträume nachholen (z.B. wenn App länger nicht geöffnet wurde)
                while (recurring.IsDue(today) && iterations < maxIterationsPerRecurring)
                {
                    var dueDate = recurring.GetNextDueDate();
                    // Fälligkeitsdatum nicht in der Zukunft
                    if (dueDate > today) dueDate = today;
                    // Enddatum prüfen
                    if (recurring.EndDate.HasValue && dueDate > recurring.EndDate.Value) break;

                    var expense = recurring.CreateExpense(dueDate);
                    expense.Id = Guid.NewGuid().ToString();
                    _expenses.Add(expense);
                    recurring.LastExecuted = dueDate;
                    count++;
                    iterations++;
                }
                if (iterations >= maxIterationsPerRecurring)
                    System.Diagnostics.Trace.TraceWarning($"Dauerauftrag '{recurring.Description}' (ID: {recurring.Id}) hat Iterations-Limit ({maxIterationsPerRecurring}) erreicht.");
            }
            // Batch-Save: Alles auf einmal statt pro Expense einzeln
            if (count > 0)
            {
                await SaveExpensesAsync();
                await SaveRecurringTransactionsAsync();
            }

            // Verarbeitungsdatum speichern
            await MarkProcessedTodayAsync(today);
            return count;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    #endregion

    #region Backup & Restore

    private record BackupData(
        string Version, DateTime CreatedAt,
        List<Expense> Expenses, List<Budget> Budgets,
        List<RecurringTransaction> RecurringTransactions);

    public async Task<string> ExportToJsonAsync()
    {
        await InitializeAsync();
        await _semaphore.WaitAsync();
        try
        {
            var backup = new BackupData("1.0", DateTime.UtcNow,
                _expenses.ToList(), _budgets.ToList(), _recurringTransactions.ToList());
            return JsonSerializer.Serialize(backup, _jsonWriteOptions);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<int> ImportFromJsonAsync(string json, bool mergeData = false)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON darf nicht leer sein.", nameof(json));

        await InitializeAsync();
        await _semaphore.WaitAsync();
        try
        {
            var backup = JsonSerializer.Deserialize<BackupData>(json);
            if (backup == null) throw new InvalidOperationException("Ungültiges Backup-Datenformat.");

            int importedCount = 0;
            if (!mergeData)
            {
                _expenses.Clear();
                _budgets.Clear();
                _recurringTransactions.Clear();
                _sentNotifications.Clear();
            }

            if (backup.Expenses != null)
            {
                var existingIds = mergeData ? _expenses.Select(e => e.Id).ToHashSet() : null;
                foreach (var expense in backup.Expenses)
                {
                    if (mergeData && existingIds!.Contains(expense.Id)) continue;
                    _expenses.Add(expense);
                    importedCount++;
                }
            }

            if (backup.Budgets != null)
            {
                foreach (var budget in backup.Budgets)
                {
                    if (mergeData)
                    {
                        var existing = _budgets.FirstOrDefault(b => b.Category == budget.Category);
                        if (existing != null) _budgets.Remove(existing);
                    }
                    _budgets.Add(budget);
                    importedCount++;
                }
            }

            if (backup.RecurringTransactions != null)
            {
                var existingRecurringIds = mergeData ? _recurringTransactions.Select(r => r.Id).ToHashSet() : null;
                foreach (var recurring in backup.RecurringTransactions)
                {
                    if (mergeData && existingRecurringIds!.Contains(recurring.Id)) continue;
                    _recurringTransactions.Add(recurring);
                    importedCount++;
                }
            }

            await SaveExpensesAsync();
            await SaveBudgetsAsync();
            await SaveRecurringTransactionsAsync();

            return importedCount;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    #endregion

    #region Private Helpers

    private async Task LoadExpensesAsync()
    {
        try
        {
            if (File.Exists(_expensesFilePath))
            {
                var json = await File.ReadAllTextAsync(_expensesFilePath);
                _expenses = JsonSerializer.Deserialize<List<Expense>>(json) ?? [];
            }
        }
        catch (Exception ex)
        {
            _expenses = [];
            OnDataLoadError?.Invoke($"{_localizationService?.GetString("LoadErrorExpenses") ?? "Expenses"}: {ex.Message}");
        }
    }

    private async Task LoadBudgetsAsync()
    {
        try
        {
            if (File.Exists(_budgetsFilePath))
            {
                var json = await File.ReadAllTextAsync(_budgetsFilePath);
                _budgets = JsonSerializer.Deserialize<List<Budget>>(json) ?? [];
            }
        }
        catch (Exception ex)
        {
            _budgets = [];
            OnDataLoadError?.Invoke($"{_localizationService?.GetString("LoadErrorBudgets") ?? "Budgets"}: {ex.Message}");
        }
    }

    private async Task LoadRecurringTransactionsAsync()
    {
        try
        {
            if (File.Exists(_recurringFilePath))
            {
                var json = await File.ReadAllTextAsync(_recurringFilePath);
                _recurringTransactions = JsonSerializer.Deserialize<List<RecurringTransaction>>(json) ?? [];
            }
        }
        catch (Exception ex)
        {
            _recurringTransactions = [];
            OnDataLoadError?.Invoke($"{_localizationService?.GetString("LoadErrorRecurring") ?? "Recurring transactions"}: {ex.Message}");
        }
    }

    /// <summary>Gesendete Benachrichtigungen laden (4.4)</summary>
    private async Task LoadNotificationsAsync()
    {
        try
        {
            if (File.Exists(_notificationsFilePath))
            {
                var json = await File.ReadAllTextAsync(_notificationsFilePath);
                _sentNotifications = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json) ?? new();
            }
        }
        catch (Exception)
        {
            _sentNotifications = new();
        }
    }

    /// <summary>Gesendete Benachrichtigungen persistieren (4.4)</summary>
    private async Task SaveNotificationsAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_sentNotifications, _jsonWriteOptions);
            await WriteAtomicAsync(_notificationsFilePath, json);
        }
        catch (Exception) { /* Nicht-kritisch */ }
    }

    /// <summary>Atomares Schreiben: temp-Datei + rename (4.1)</summary>
    private static async Task WriteAtomicAsync(string targetPath, string content)
    {
        var tempPath = targetPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, content);
        File.Move(tempPath, targetPath, overwrite: true);
    }

    private async Task SaveExpensesAsync()
    {
        var json = JsonSerializer.Serialize(_expenses, _jsonWriteOptions);
        await WriteAtomicAsync(_expensesFilePath, json);
    }

    private async Task SaveBudgetsAsync()
    {
        var json = JsonSerializer.Serialize(_budgets, _jsonWriteOptions);
        await WriteAtomicAsync(_budgetsFilePath, json);
    }

    private async Task SaveRecurringTransactionsAsync()
    {
        var json = JsonSerializer.Serialize(_recurringTransactions, _jsonWriteOptions);
        await WriteAtomicAsync(_recurringFilePath, json);
    }

    /// <summary>Auto-Backup erstellen, max. 5 Versionen rotieren (4.2)</summary>
    private async Task CreateAutoBackupAsync()
    {
        try
        {
            if (_expenses.Count == 0 && _budgets.Count == 0 && _recurringTransactions.Count == 0)
                return;

            Directory.CreateDirectory(_backupDir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(_backupDir, $"backup_{timestamp}.json");

            var backup = new BackupData("1.0", DateTime.UtcNow,
                _expenses.ToList(), _budgets.ToList(), _recurringTransactions.ToList());
            var json = JsonSerializer.Serialize(backup, _jsonWriteOptions);
            await File.WriteAllTextAsync(backupPath, json);

            // Alte Backups löschen (nur die ältesten, max. 5 behalten)
            var backups = Directory.GetFiles(_backupDir, "backup_*.json")
                .OrderByDescending(f => f)
                .Skip(MaxBackupVersions)
                .ToList();
            foreach (var old in backups)
            {
                try { File.Delete(old); } catch { /* Löschen nicht kritisch */ }
            }
        }
        catch (Exception)
        {
            // Backup-Fehler sind nicht kritisch
        }
    }

    /// <summary>Prüft ob heute bereits verarbeitet wurde (4.6)</summary>
    private async Task<bool> WasProcessedTodayAsync(DateTime today)
    {
        try
        {
            if (!File.Exists(_lastProcessedFilePath)) return false;
            var dateStr = await File.ReadAllTextAsync(_lastProcessedFilePath);
            return dateStr.Trim() == today.ToString("yyyy-MM-dd");
        }
        catch { return false; }
    }

    /// <summary>Heutiges Datum als verarbeitet markieren (4.6)</summary>
    private async Task MarkProcessedTodayAsync(DateTime today)
    {
        try
        {
            await File.WriteAllTextAsync(_lastProcessedFilePath, today.ToString("yyyy-MM-dd"));
        }
        catch { /* Nicht-kritisch */ }
    }

    private static void ValidateExpense(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);
        if (string.IsNullOrWhiteSpace(expense.Description))
            throw new ArgumentException("Beschreibung darf nicht leer sein.");
        if (expense.Description.Length > MaxDescriptionLength)
            throw new ArgumentException($"Beschreibung darf maximal {MaxDescriptionLength} Zeichen lang sein.");
        if (expense.Amount <= 0)
            throw new ArgumentException("Betrag muss größer als Null sein.");
        if (expense.Amount > MaxAmount)
            throw new ArgumentException($"Betrag darf {MaxAmount:N2} nicht überschreiten.");
        if (!string.IsNullOrEmpty(expense.Note) && expense.Note.Length > MaxNoteLength)
            throw new ArgumentException($"Notiz darf maximal {MaxNoteLength} Zeichen lang sein.");
    }

    private static void ValidateBudget(Budget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        if (budget.MonthlyLimit <= 0)
            throw new ArgumentException("Monatliches Limit muss größer als Null sein.");
        if (budget.MonthlyLimit > MaxAmount)
            throw new ArgumentException($"Monatliches Limit darf {MaxAmount:N2} nicht überschreiten.");
        if (budget.WarningThreshold is < 0 or > 100)
            throw new ArgumentException("Warnschwelle muss zwischen 0 und 100 liegen.");
    }

    private static void ValidateRecurringTransaction(RecurringTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (string.IsNullOrWhiteSpace(transaction.Description))
            throw new ArgumentException("Beschreibung darf nicht leer sein.");
        if (transaction.Amount <= 0)
            throw new ArgumentException("Betrag muss größer als Null sein.");
        if (transaction.EndDate.HasValue && transaction.EndDate.Value <= transaction.StartDate)
            throw new ArgumentException("Enddatum muss nach dem Startdatum liegen.");
    }

    private async Task CheckBudgetWarningAsync(ExpenseCategory category, DateTime date)
    {
        if (_notificationService == null) return;
        try
        {
            var budget = _budgets.FirstOrDefault(b => b.Category == category && b.IsEnabled);
            if (budget == null) return;

            var startOfMonth = new DateTime(date.Year, date.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
            var spent = _expenses
                .Where(e => e.Category == category && e.Type == TransactionType.Expense
                    && e.Date >= startOfMonth && e.Date <= endOfMonth)
                .Sum(e => e.Amount);
            var percentageUsed = budget.MonthlyLimit > 0 ? (spent / budget.MonthlyLimit * 100) : 0;
            var notificationKey = $"{category}_{date:yyyy-MM}";

            if (percentageUsed >= 80 && !_sentNotifications.ContainsKey($"{notificationKey}_80"))
            {
                var categoryName = CategoryLocalizationHelper.GetLocalizedName(category, _localizationService);
                await _notificationService.SendBudgetAlertAsync(categoryName, percentageUsed, spent, budget.MonthlyLimit);
                _sentNotifications[$"{notificationKey}_80"] = DateTime.UtcNow;
                await SaveNotificationsAsync();
            }
        }
        catch (Exception)
        {
            // Budget-Warnungsprüfung stillschweigend ignorieren
        }
    }

    #endregion

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
