using SkiaSharp;

namespace FinanzRechner.Models;

/// <summary>
/// Benutzerdefinierte Kategorie (ergänzt die festen ExpenseCategory-Enums).
/// Wenn eine Transaktion ein CustomCategoryId hat, wird diese statt der Enum-Kategorie angezeigt.
/// </summary>
public class CustomCategory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    /// <summary>Ob die Kategorie für Ausgaben oder Einnahmen gilt.</summary>
    public TransactionType Type { get; set; } = TransactionType.Expense;

    /// <summary>Emoji-Icon für die Darstellung.</summary>
    public string Icon { get; set; } = "\U0001F4E6"; // Paket

    /// <summary>Farbe als Hex-String für Charts und UI.</summary>
    public string ColorHex { get; set; } = "#9E9E9E";

    /// <summary>Sortierreihenfolge in der Auswahlliste.</summary>
    public int SortOrder { get; set; }

    /// <summary>Ob die Kategorie aktiv ist (inaktive werden in der Auswahl ausgeblendet).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Konvertiert den Hex-String in eine SKColor für SkiaSharp-Charts.</summary>
    public SKColor GetSkColor()
    {
        if (string.IsNullOrEmpty(ColorHex) || ColorHex.Length < 7)
            return new SKColor(0x9E, 0x9E, 0x9E);

        return SKColor.Parse(ColorHex);
    }
}
