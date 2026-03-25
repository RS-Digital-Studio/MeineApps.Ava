using System.Text.Json;
using FinanzRechner.Models;

namespace FinanzRechner.Services;

/// <summary>
/// Implementierung der Sparziel-Verwaltung mit lokalem JSON-Speicher.
/// </summary>
public sealed class SavingsGoalService : ISavingsGoalService, IDisposable
{
    private const string GoalsFile = "savings_goals.json";
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private List<SavingsGoal> _goals = [];
    private bool _isInitialized;

    public SavingsGoalService()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FinanzRechner");
        Directory.CreateDirectory(appDataDir);
        _filePath = Path.Combine(appDataDir, GoalsFile);
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

    public async Task<SavingsGoal> CreateGoalAsync(SavingsGoal goal)
    {
        await InitializeAsync();
        ValidateGoal(goal);
        await _semaphore.WaitAsync();
        try
        {
            goal.Id = Guid.NewGuid().ToString();
            goal.CreatedAt = DateTime.UtcNow;
            _goals.Add(goal);
            await SaveAsync();
            return goal;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<bool> UpdateGoalAsync(SavingsGoal goal)
    {
        await InitializeAsync();
        ValidateGoal(goal);
        await _semaphore.WaitAsync();
        try
        {
            var existing = _goals.FirstOrDefault(g => g.Id == goal.Id);
            if (existing == null) return false;

            existing.Name = goal.Name;
            existing.TargetAmount = goal.TargetAmount;
            existing.CurrentAmount = goal.CurrentAmount;
            existing.Deadline = goal.Deadline;
            existing.Icon = goal.Icon;
            existing.ColorHex = goal.ColorHex;
            existing.Note = goal.Note;
            existing.LinkedAccountId = goal.LinkedAccountId;

            await SaveAsync();
            return true;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<bool> DeleteGoalAsync(string id)
    {
        await InitializeAsync();
        await _semaphore.WaitAsync();
        try
        {
            var goal = _goals.FirstOrDefault(g => g.Id == id);
            if (goal == null) return false;
            _goals.Remove(goal);
            await SaveAsync();
            return true;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<SavingsGoal?> GetGoalAsync(string id)
    {
        await InitializeAsync();
        return _goals.FirstOrDefault(g => g.Id == id);
    }

    public async Task<IReadOnlyList<SavingsGoal>> GetAllGoalsAsync()
    {
        await InitializeAsync();
        return _goals.OrderBy(g => g.IsCompleted).ThenBy(g => g.Deadline ?? DateTime.MaxValue).ToList();
    }

    public async Task<bool> AdjustAmountAsync(string goalId, double amount)
    {
        await InitializeAsync();
        await _semaphore.WaitAsync();
        try
        {
            var goal = _goals.FirstOrDefault(g => g.Id == goalId);
            if (goal == null) return false;

            goal.CurrentAmount = Math.Max(0, goal.CurrentAmount + amount);
            if (goal.CurrentAmount >= goal.TargetAmount)
                goal.IsCompleted = true;

            await SaveAsync();
            return true;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<bool> CompleteGoalAsync(string goalId)
    {
        await InitializeAsync();
        await _semaphore.WaitAsync();
        try
        {
            var goal = _goals.FirstOrDefault(g => g.Id == goalId);
            if (goal == null) return false;
            goal.IsCompleted = true;
            await SaveAsync();
            return true;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<string> ExportToJsonAsync()
    {
        await InitializeAsync();
        return JsonSerializer.Serialize(_goals, _jsonOptions);
    }

    public async Task<int> ImportFromJsonAsync(string json, bool merge = false)
    {
        await InitializeAsync();
        var imported = JsonSerializer.Deserialize<List<SavingsGoal>>(json) ?? [];
        await _semaphore.WaitAsync();
        try
        {
            if (!merge) _goals.Clear();
            var existingIds = _goals.Select(g => g.Id).ToHashSet();
            var count = 0;
            foreach (var goal in imported)
            {
                if (merge && existingIds.Contains(goal.Id)) continue;
                _goals.Add(goal);
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
                _goals = JsonSerializer.Deserialize<List<SavingsGoal>>(json) ?? [];
            }
        }
        catch { _goals = []; }
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_goals, _jsonOptions);
        var tempPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static void ValidateGoal(SavingsGoal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);
        if (string.IsNullOrWhiteSpace(goal.Name))
            throw new ArgumentException("Name darf nicht leer sein.");
        if (goal.TargetAmount <= 0)
            throw new ArgumentException("Zielbetrag muss größer als Null sein.");
    }

    public void Dispose() => _semaphore.Dispose();
}
