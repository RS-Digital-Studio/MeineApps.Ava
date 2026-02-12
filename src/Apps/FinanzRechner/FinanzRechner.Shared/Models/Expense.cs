using FinanzRechner.Helpers;

namespace FinanzRechner.Models;

/// <summary>
/// Single transaction (expense or income)
/// </summary>
public class Expense
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Date { get; set; } = DateTime.Today;
    public string Description { get; set; } = string.Empty;
    public double Amount { get; set; }
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;
    public string? Note { get; set; }
    public TransactionType Type { get; set; } = TransactionType.Expense;
}

/// <summary>
/// Transaction type
/// </summary>
public enum TransactionType
{
    Expense,
    Income
}

/// <summary>
/// Transaction categories (expenses and income)
/// </summary>
public enum ExpenseCategory
{
    // Expenses
    Food,
    Transport,
    Housing,
    Entertainment,
    Shopping,
    Health,
    Education,
    Bills,
    Other,

    // Income
    Salary,
    Freelance,
    Investment,
    Gift,
    OtherIncome
}

/// <summary>
/// Monthly summary
/// </summary>
public record MonthSummary(
    int Year,
    int Month,
    double TotalExpenses,
    double TotalIncome,
    double Balance,
    Dictionary<ExpenseCategory, double> ByCategory)
{
    public double TotalAmount => TotalExpenses;
}

/// <summary>
/// Filter options for expenses
/// </summary>
public class ExpenseFilter
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ExpenseCategory? Category { get; set; }
    public double? MinAmount { get; set; }
    public double? MaxAmount { get; set; }
}

/// <summary>
/// Grouped transactions by date
/// </summary>
public class ExpenseGroup : List<Expense>
{
    public DateTime Date { get; }
    public string DateDisplay { get; }
    public double DayTotal { get; }
    public double DayIncome { get; }
    public double DayExpenses { get; }

    public ExpenseGroup(DateTime date, string dateDisplay, IEnumerable<Expense> expenses) : base(expenses)
    {
        Date = date;
        DateDisplay = dateDisplay;
        DayIncome = this.Where(e => e.Type == TransactionType.Income).Sum(e => e.Amount);
        DayExpenses = this.Where(e => e.Type == TransactionType.Expense).Sum(e => e.Amount);
        DayTotal = DayIncome - DayExpenses;
    }

    public string DayTotalDisplay => CurrencyHelper.FormatSigned(DayTotal);
}
