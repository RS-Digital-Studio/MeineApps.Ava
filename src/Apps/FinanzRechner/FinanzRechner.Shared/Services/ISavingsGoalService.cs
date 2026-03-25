using FinanzRechner.Models;

namespace FinanzRechner.Services;

/// <summary>
/// Service für Sparziel-Verwaltung.
/// </summary>
public interface ISavingsGoalService
{
    Task InitializeAsync();

    Task<SavingsGoal> CreateGoalAsync(SavingsGoal goal);
    Task<bool> UpdateGoalAsync(SavingsGoal goal);
    Task<bool> DeleteGoalAsync(string id);
    Task<SavingsGoal?> GetGoalAsync(string id);
    Task<IReadOnlyList<SavingsGoal>> GetAllGoalsAsync();

    /// <summary>Betrag zum Sparziel hinzufügen oder abziehen.</summary>
    Task<bool> AdjustAmountAsync(string goalId, double amount);

    /// <summary>Sparziel als abgeschlossen markieren.</summary>
    Task<bool> CompleteGoalAsync(string goalId);

    // Daten-Export/Import
    Task<string> ExportToJsonAsync();
    Task<int> ImportFromJsonAsync(string json, bool merge = false);
}
