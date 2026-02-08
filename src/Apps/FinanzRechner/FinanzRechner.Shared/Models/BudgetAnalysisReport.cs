namespace FinanzRechner.Models;

/// <summary>
/// Monatsreport: Budget-Analyse mit Kategorie-Aufschluesselung,
/// Sparpotenzial und Vormonatsvergleich.
/// </summary>
public class BudgetAnalysisReport
{
    /// <summary>Anzeige-Zeitraum (z.B. "Februar 2026")</summary>
    public string PeriodDisplay { get; set; } = string.Empty;

    /// <summary>Gesamtausgaben im Zeitraum</summary>
    public double TotalExpenses { get; set; }

    /// <summary>Gesamteinnahmen im Zeitraum</summary>
    public double TotalIncome { get; set; }

    /// <summary>Bilanz (Einnahmen - Ausgaben)</summary>
    public double Balance => TotalIncome - TotalExpenses;

    /// <summary>Ausgaben nach Kategorie, sortiert absteigend</summary>
    public List<CategoryBreakdownItem> CategoryBreakdown { get; set; } = [];

    /// <summary>Top-3 Sparpotenziale</summary>
    public List<SavingTipItem> SavingTips { get; set; } = [];

    /// <summary>Ausgaben des Vormonats (fuer Vergleich)</summary>
    public double PreviousMonthExpenses { get; set; }

    /// <summary>Prozentuale Veraenderung zum Vormonat</summary>
    public double MonthChangePercent { get; set; }

    /// <summary>Ob Ausgaben gestiegen oder gesunken sind</summary>
    public bool IsExpenseIncreased => MonthChangePercent > 0;

    // Display-Properties
    public string TotalExpensesDisplay => $"{TotalExpenses:N2} \u20ac";
    public string TotalIncomeDisplay => $"{TotalIncome:N2} \u20ac";
    public string BalanceDisplay => Balance >= 0 ? $"+{Balance:N2} \u20ac" : $"{Balance:N2} \u20ac";
    public string PreviousMonthDisplay => $"{PreviousMonthExpenses:N2} \u20ac";
    public string MonthChangeDisplay => MonthChangePercent >= 0 ? $"+{MonthChangePercent:F0}%" : $"{MonthChangePercent:F0}%";
    public bool IsBalancePositive => Balance >= 0;
}

/// <summary>
/// Einzelne Kategorie im Budget-Report
/// </summary>
public class CategoryBreakdownItem
{
    public ExpenseCategory Category { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public double Amount { get; set; }
    public double Percentage { get; set; }

    public string AmountDisplay => $"{Amount:N2} \u20ac";
    public string PercentageDisplay => $"{Percentage:F0}%";
}

/// <summary>
/// Spartipp im Budget-Report
/// </summary>
public class SavingTipItem
{
    public string CategoryName { get; set; } = string.Empty;
    public string Tip { get; set; } = string.Empty;
    public double Amount { get; set; }
    public string AmountDisplay => $"{Amount:N2} \u20ac";
}
