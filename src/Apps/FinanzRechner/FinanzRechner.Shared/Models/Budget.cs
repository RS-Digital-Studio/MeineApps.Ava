using FinanzRechner.Helpers;

namespace FinanzRechner.Models;

/// <summary>
/// Budget-Limit für eine Kategorie
/// </summary>
public class Budget
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ExpenseCategory Category { get; set; }
    public decimal MonthlyLimit { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Warnschwelle in Prozent (z.B. 80 = Warnung bei 80% des Limits)
    /// </summary>
    public decimal WarningThreshold { get; set; } = 80;
}

/// <summary>
/// Budget-Status für eine Kategorie
/// </summary>
public record BudgetStatus(
    ExpenseCategory Category,
    decimal Limit,
    decimal Spent,
    decimal Remaining,
    decimal PercentageUsed,
    BudgetAlertLevel AlertLevel,
    string? LocalizedCategoryName = null)
{
    public bool IsExceeded => AlertLevel == BudgetAlertLevel.Exceeded;
    public bool IsWarning => AlertLevel == BudgetAlertLevel.Warning;
    /// <summary>
    /// Lokalisierter Kategorie-Name. Faellt auf Enum-Name zurueck wenn nicht gesetzt.
    /// </summary>
    public string CategoryName => LocalizedCategoryName ?? Category.ToString();
    // Kategorie-Icon kommt aus CategoryLocalizationHelper (zentral, sprachunabhaengig).
    // Aktuell Emoji-basiert — eine Migration auf MaterialIcons betraefe auch User-Daten
    // (Account.Icon / CustomCategory.Icon sind frei waehlbare Emoji-Strings).
    public string CategoryIcon => CategoryLocalizationHelper.GetCategoryIcon(Category);
    public string SpentDisplay => CurrencyHelper.Format(Spent);
    public string LimitDisplay => CurrencyHelper.Format(Limit);
    public string PercentageDisplay => $"{PercentageUsed:F0}%";
};

/// <summary>
/// Budget-Warnstufe
/// </summary>
public enum BudgetAlertLevel
{
    Safe,
    Warning,
    Exceeded
}
