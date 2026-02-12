using FinanzRechner.Helpers;

namespace FinanzRechner.Models;

/// <summary>
/// Wiederholungsmuster für Daueraufträge.
/// </summary>
public enum RecurrencePattern
{
    Daily,
    Weekly,
    Biweekly,
    Monthly,
    Yearly
}

/// <summary>
/// Dauerauftrag (Vorlage für automatische Buchungen).
/// </summary>
public class RecurringTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public double Amount { get; set; }
    public ExpenseCategory Category { get; set; }
    public TransactionType Type { get; set; }
    public string? Note { get; set; }

    public RecurrencePattern Pattern { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? LastExecuted { get; set; }
    public bool IsActive { get; set; } = true;

    public string CategoryName => Category.ToString();
    public string AmountDisplay => Type == TransactionType.Expense
        ? $"-{CurrencyHelper.Format(Amount)}"
        : $"+{CurrencyHelper.Format(Amount)}";

    public DateTime GetNextDueDate()
    {
        var baseDate = LastExecuted ?? StartDate;

        return Pattern switch
        {
            RecurrencePattern.Daily => baseDate.AddDays(1),
            RecurrencePattern.Weekly => baseDate.AddDays(7),
            RecurrencePattern.Biweekly => baseDate.AddDays(14),
            RecurrencePattern.Monthly => baseDate.AddMonths(1),
            RecurrencePattern.Yearly => baseDate.AddYears(1),
            _ => baseDate
        };
    }

    public bool IsDue(DateTime currentDate)
    {
        if (!IsActive) return false;
        if (EndDate.HasValue && currentDate > EndDate.Value) return false;
        if (currentDate < StartDate) return false;

        var nextDue = GetNextDueDate();
        return currentDate >= nextDue;
    }

    public Expense CreateExpense(DateTime date)
    {
        return new Expense
        {
            Date = date,
            Description = Description,
            Amount = Amount,
            Category = Category,
            Type = Type,
            Note = Note
        };
    }
}
