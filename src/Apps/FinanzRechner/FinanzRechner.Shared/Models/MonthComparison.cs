using FinanzRechner.Helpers;

namespace FinanzRechner.Models;

/// <summary>
/// Monatsvergleich: Aktueller Monat vs. Vormonat mit Prozent-Änderungen.
/// </summary>
public record MonthComparison(
    MonthSummary CurrentMonth,
    MonthSummary PreviousMonth,
    decimal ExpenseChangePercent,
    decimal IncomeChangePercent,
    decimal BalanceChange,
    IReadOnlyList<CategoryChange> CategoryChanges)
{
    /// <summary>Ob die Ausgaben gestiegen sind (schlecht).</summary>
    public bool ExpensesIncreased => ExpenseChangePercent > 0;

    /// <summary>Ob die Einnahmen gestiegen sind (gut).</summary>
    public bool IncomeIncreased => IncomeChangePercent > 0;

    public string ExpenseChangeDisplay => FormatChangePercent(ExpenseChangePercent);
    public string IncomeChangeDisplay => FormatChangePercent(IncomeChangePercent);
    public string BalanceChangeDisplay => CurrencyHelper.FormatSigned(BalanceChange);

    private static string FormatChangePercent(decimal percent)
        => percent >= 0 ? $"+{percent:F1}%" : $"{percent:F1}%";
}

/// <summary>
/// Änderung einer einzelnen Kategorie im Vergleich zum Vormonat.
/// </summary>
public record CategoryChange(
    ExpenseCategory Category,
    string? CustomCategoryId,
    string CategoryName,
    decimal CurrentAmount,
    decimal PreviousAmount)
{
    public decimal ChangeAmount => CurrentAmount - PreviousAmount;
    public decimal ChangePercent => PreviousAmount > 0
        ? ((CurrentAmount - PreviousAmount) / PreviousAmount) * 100
        : CurrentAmount > 0 ? 100 : 0;

    public bool IsIncrease => ChangeAmount > 0;
    public string ChangeDisplay => CurrencyHelper.FormatSigned(ChangeAmount);
    public string ChangePercentDisplay => ChangePercent >= 0 ? $"+{ChangePercent:F1}%" : $"{ChangePercent:F1}%";
    public string CurrentDisplay => CurrencyHelper.Format(CurrentAmount);
    public string PreviousDisplay => CurrencyHelper.Format(PreviousAmount);
}
