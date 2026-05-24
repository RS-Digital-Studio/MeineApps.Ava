using CommunityToolkit.Mvvm.ComponentModel;

namespace FinanzRechner.Models;

/// <summary>
/// Anzeige-Item fuer Budget-Status auf der HomeView.
/// ObservableObject damit CategoryName bei Sprachwechsel aktualisiert werden kann.
/// </summary>
public partial class BudgetDisplayItem : ObservableObject
{
    public ExpenseCategory Category { get; set; }

    [ObservableProperty]
    private string _categoryName = string.Empty;

    public decimal Percentage { get; set; }
    public BudgetAlertLevel AlertLevel { get; set; }

    public string PercentageDisplay => $"{Percentage:F0}%";
    public bool IsExceeded => AlertLevel == BudgetAlertLevel.Exceeded;
}
