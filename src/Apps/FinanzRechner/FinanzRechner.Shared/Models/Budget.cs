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
    // TODO: Emojis durch Material Icons ersetzen (erfordert AXAML-Umbau in BudgetsView.axaml:
    // TextBlock→MaterialIcon, Binding von string auf MaterialIconKind ändern).
    // Aktuell wird CategoryIcon als TextBlock.Text in BudgetsView angezeigt.
    // Delegiert an zentralen Helper um Duplikation zu vermeiden.
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
