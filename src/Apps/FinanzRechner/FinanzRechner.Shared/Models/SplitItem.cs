namespace FinanzRechner.Models;

/// <summary>
/// Teil einer Split-Transaktion. Erlaubt eine Rechnung auf mehrere Kategorien aufzuteilen.
/// Beispiel: Supermarkt-Rechnung → 80% Lebensmittel, 20% Haushalt.
/// </summary>
public class SplitItem
{
    /// <summary>Kategorie dieses Teils (Built-in).</summary>
    public ExpenseCategory Category { get; set; }

    /// <summary>Optionale Custom-Kategorie (überschreibt Category wenn gesetzt).</summary>
    public string? CustomCategoryId { get; set; }

    /// <summary>Betrag dieses Teils.</summary>
    public double Amount { get; set; }

    /// <summary>Optionale Notiz für diesen Teil.</summary>
    public string? Note { get; set; }
}
