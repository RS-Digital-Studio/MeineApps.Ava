using System.Text.Json;
using FinanzRechner.Models;

namespace FinanzRechner.Services;

/// <summary>
/// Implementierung des Schulden-Trackers mit lokalem JSON-Speicher.
/// </summary>
public sealed class DebtService : IDebtService, IDisposable
{
    private const string DebtsFile = "debts.json";
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private List<DebtEntry> _debts = [];
    private bool _isInitialized;

    public DebtService()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FinanzRechner");
        Directory.CreateDirectory(appDataDir);
        _filePath = Path.Combine(appDataDir, DebtsFile);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        await _semaphore.WaitAsync();
        try
        {
            if (_isInitialized) return;
            await LoadAsync();
            _isInitialized = true;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<DebtEntry> CreateDebtAsync(DebtEntry debt)
    {
        await InitializeAsync();
        ValidateDebt(debt);
        await _semaphore.WaitAsync();
        try
        {
            debt.Id = Guid.NewGuid().ToString();
            debt.CreatedAt = DateTime.UtcNow;
            _debts.Add(debt);
            await SaveAsync();
            return debt;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<bool> UpdateDebtAsync(DebtEntry debt)
    {
        await InitializeAsync();
        ValidateDebt(debt);
        await _semaphore.WaitAsync();
        try
        {
            var existing = _debts.FirstOrDefault(d => d.Id == debt.Id);
            if (existing == null) return false;

            existing.Name = debt.Name;
            existing.OriginalAmount = debt.OriginalAmount;
            existing.RemainingAmount = debt.RemainingAmount;
            existing.InterestRate = debt.InterestRate;
            existing.MonthlyPayment = debt.MonthlyPayment;
            existing.TargetPayoffDate = debt.TargetPayoffDate;
            existing.Note = debt.Note;
            existing.Icon = debt.Icon;
            existing.ColorHex = debt.ColorHex;

            await SaveAsync();
            return true;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<bool> DeleteDebtAsync(string id)
    {
        await InitializeAsync();
        await _semaphore.WaitAsync();
        try
        {
            var debt = _debts.FirstOrDefault(d => d.Id == id);
            if (debt == null) return false;
            _debts.Remove(debt);
            await SaveAsync();
            return true;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<DebtEntry?> GetDebtAsync(string id)
    {
        await InitializeAsync();
        return _debts.FirstOrDefault(d => d.Id == id);
    }

    public async Task<IReadOnlyList<DebtEntry>> GetAllDebtsAsync()
    {
        await InitializeAsync();
        return _debts.OrderByDescending(d => d.IsActive).ThenByDescending(d => d.RemainingAmount).ToList();
    }

    public async Task<bool> MakePaymentAsync(string debtId, double amount)
    {
        await InitializeAsync();
        if (amount <= 0)
            throw new ArgumentException("Zahlungsbetrag muss größer als Null sein.");

        await _semaphore.WaitAsync();
        try
        {
            var debt = _debts.FirstOrDefault(d => d.Id == debtId);
            if (debt == null) return false;

            debt.RemainingAmount = Math.Max(0, debt.RemainingAmount - amount);
            if (debt.RemainingAmount <= 0)
            {
                debt.RemainingAmount = 0;
                debt.IsActive = false;
            }

            await SaveAsync();
            return true;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<double> GetTotalDebtAsync()
    {
        await InitializeAsync();
        return _debts.Where(d => d.IsActive).Sum(d => d.RemainingAmount);
    }

    public async Task<bool> PayOffDebtAsync(string debtId)
    {
        await InitializeAsync();
        await _semaphore.WaitAsync();
        try
        {
            var debt = _debts.FirstOrDefault(d => d.Id == debtId);
            if (debt == null) return false;
            debt.RemainingAmount = 0;
            debt.IsActive = false;
            await SaveAsync();
            return true;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<string> ExportToJsonAsync()
    {
        await InitializeAsync();
        return JsonSerializer.Serialize(_debts, _jsonOptions);
    }

    public async Task<int> ImportFromJsonAsync(string json, bool merge = false)
    {
        await InitializeAsync();
        var imported = JsonSerializer.Deserialize<List<DebtEntry>>(json) ?? [];
        await _semaphore.WaitAsync();
        try
        {
            if (!merge) _debts.Clear();
            var existingIds = _debts.Select(d => d.Id).ToHashSet();
            var count = 0;
            foreach (var debt in imported)
            {
                if (merge && existingIds.Contains(debt.Id)) continue;
                _debts.Add(debt);
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
                _debts = JsonSerializer.Deserialize<List<DebtEntry>>(json) ?? [];
            }
        }
        catch { _debts = []; }
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_debts, _jsonOptions);
        var tempPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static void ValidateDebt(DebtEntry debt)
    {
        ArgumentNullException.ThrowIfNull(debt);
        if (string.IsNullOrWhiteSpace(debt.Name))
            throw new ArgumentException("Name darf nicht leer sein.");
        if (debt.OriginalAmount <= 0)
            throw new ArgumentException("Ursprungsbetrag muss größer als Null sein.");
        if (debt.MonthlyPayment < 0)
            throw new ArgumentException("Monatliche Rate darf nicht negativ sein.");
        if (debt.InterestRate < 0)
            throw new ArgumentException("Zinssatz darf nicht negativ sein.");
    }

    public void Dispose() => _semaphore.Dispose();
}
