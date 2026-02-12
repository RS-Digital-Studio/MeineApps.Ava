using FinanzRechner.Helpers;

namespace FinanzRechner.Models;

/// <summary>
/// Budget-Limit für eine Kategorie
/// </summary>
public class Budget
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ExpenseCategory Category { get; set; }
    public double MonthlyLimit { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Warnschwelle in Prozent (z.B. 80 = Warnung bei 80% des Limits)
    /// </summary>
    public double WarningThreshold { get; set; } = 80;
}

/// <summary>
/// Budget-Status für eine Kategorie
/// </summary>
public record BudgetStatus(
    ExpenseCategory Category,
    double Limit,
    double Spent,
    double Remaining,
    double PercentageUsed,
    BudgetAlertLevel AlertLevel,
    string? LocalizedCategoryName = null)
{
    public bool IsExceeded => AlertLevel == BudgetAlertLevel.Exceeded;
    public bool IsWarning => AlertLevel == BudgetAlertLevel.Warning;
    /// <summary>
    /// Lokalisierter Kategorie-Name. Faellt auf Enum-Name zurueck wenn nicht gesetzt.
    /// </summary>
    public string CategoryName => LocalizedCategoryName ?? Category.ToString();
    public string CategoryIcon => Category switch
    {
        ExpenseCategory.Food => "\U0001F354",
        ExpenseCategory.Transport => "\U0001F697",
        ExpenseCategory.Housing => "\U0001F3E0",
        ExpenseCategory.Entertainment => "\U0001F3AC",
        ExpenseCategory.Shopping => "\U0001F6D2",
        ExpenseCategory.Health => "\U0001F48A",
        ExpenseCategory.Education => "\U0001F4DA",
        ExpenseCategory.Bills => "\U0001F4C4",
        _ => "\U0001F4E6"
    };
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
