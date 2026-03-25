using FinanzRechner.Helpers;

namespace FinanzRechner.Models;

/// <summary>
/// Einzelne Transaktion (Ausgabe, Einnahme oder Überweisung).
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

    /// <summary>Zugeordnetes Konto (null = Standard-Konto / nicht zugeordnet).</summary>
    public string? AccountId { get; set; }

    /// <summary>Benutzerdefinierte Kategorie (überschreibt Category-Enum wenn gesetzt).</summary>
    public string? CustomCategoryId { get; set; }

    /// <summary>Zielkonto bei Überweisungen (nur wenn Type == Transfer).</summary>
    public string? TransferToAccountId { get; set; }

    /// <summary>Verknüpfungs-ID bei Überweisungen (beide Seiten haben dieselbe TransferId).</summary>
    public string? TransferId { get; set; }

    /// <summary>Split-Positionen wenn die Transaktion auf mehrere Kategorien aufgeteilt ist.</summary>
    public List<SplitItem>? SplitItems { get; set; }

    /// <summary>Ob die Transaktion gesplittet ist.</summary>
    public bool IsSplit => SplitItems is { Count: > 0 };
}

/// <summary>
/// Transaktionstyp.
/// </summary>
public enum TransactionType
{
    Expense,
    Income,
    Transfer
}

/// <summary>
/// Transaktionskategorien (Ausgaben und Einnahmen).
/// </summary>
public enum ExpenseCategory
{
    // Ausgaben
    Food,
    Transport,
    Housing,
    Entertainment,
    Shopping,
    Health,
    Education,
    Bills,
    Other,

    // Einnahmen
    Salary,
    Freelance,
    Investment,
    Gift,
    OtherIncome
}

/// <summary>
/// Monatliche Zusammenfassung.
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
/// Filteroptionen für Transaktionen.
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
/// Nach Datum gruppierte Transaktionen.
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
