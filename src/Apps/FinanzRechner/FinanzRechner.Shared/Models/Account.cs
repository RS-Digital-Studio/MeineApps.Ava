using FinanzRechner.Helpers;

namespace FinanzRechner.Models;

/// <summary>
/// Kontotyp (Girokonto, Sparkonto, Bargeld, Kreditkarte, Depot, Sonstiges).
/// </summary>
public enum AccountType
{
    Checking,       // Girokonto
    Savings,        // Sparkonto
    Cash,           // Bargeld
    CreditCard,     // Kreditkarte
    Investment,     // Depot
    Other           // Sonstiges
}

/// <summary>
/// Finanzkonto (Girokonto, Sparkonto, Bargeld, Kreditkarte etc.).
/// Jede Transaktion wird einem Konto zugeordnet.
/// </summary>
public class Account
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; } = AccountType.Checking;

    /// <summary>Anfangssaldo beim Erstellen des Kontos.</summary>
    public double InitialBalance { get; set; }

    /// <summary>Emoji-Icon für die Darstellung.</summary>
    public string Icon { get; set; } = "\U0001F3E6"; // Bankgebäude

    /// <summary>Farbe als Hex-String (z.B. "#4CAF50").</summary>
    public string ColorHex { get; set; } = "#4CAF50";

    /// <summary>Sortierreihenfolge in der Liste.</summary>
    public int SortOrder { get; set; }

    /// <summary>Ob das Konto aktiv ist (inaktive werden ausgeblendet).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Ob das Konto in die Nettovermögen-Berechnung einfließt.</summary>
    public bool IncludeInNetWorth { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Berechneter Kontostand (Anfangssaldo + Einnahmen - Ausgaben).
/// </summary>
public record AccountBalance(
    Account Account,
    double CurrentBalance,
    double MonthlyIncome,
    double MonthlyExpenses,
    double MonthlyBalance)
{
    public string BalanceDisplay => CurrencyHelper.Format(CurrentBalance);
    public string MonthlyIncomeDisplay => CurrencyHelper.FormatSigned(MonthlyIncome);
    public string MonthlyExpensesDisplay => CurrencyHelper.FormatSigned(-MonthlyExpenses);
}
