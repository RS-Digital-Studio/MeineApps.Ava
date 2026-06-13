namespace HandwerkerRechner.Models;

/// <summary>
/// Angebot/Rechnung für einen Kunden mit Positionen, MwSt und Marge.
/// </summary>
public class Quote
{
    /// <summary>Eindeutige ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Angebotsnummer (z.B. "A-2026-001")</summary>
    public string QuoteNumber { get; set; } = string.Empty;

    /// <summary>Kundenname</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Kundenadresse</summary>
    public string CustomerAddress { get; set; } = string.Empty;

    /// <summary>Projektbeschreibung</summary>
    public string ProjectDescription { get; set; } = string.Empty;

    /// <summary>Angebots-Positionen</summary>
    public List<QuoteItem> Items { get; set; } = [];

    /// <summary>Mehrwertsteuer in %</summary>
    public decimal VatPercent { get; set; } = 19.0m;

    /// <summary>Marge/Aufschlag in %</summary>
    public decimal MarginPercent { get; set; } = 15.0m;

    /// <summary>Erstellungsdatum</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Gültig bis</summary>
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow.AddDays(30);

    /// <summary>Status des Angebots</summary>
    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;

    // Berechnete Properties — Geldbeträge in decimal, Endbeträge definiert kaufmännisch gerundet
    public decimal SubtotalNet => Math.Round(Items.Sum(i => i.Total), 2, MidpointRounding.AwayFromZero);
    public decimal MarginAmount => Math.Round(SubtotalNet * MarginPercent / 100m, 2, MidpointRounding.AwayFromZero);
    public decimal TotalNet => SubtotalNet + MarginAmount;
    public decimal VatAmount => Math.Round(TotalNet * VatPercent / 100m, 2, MidpointRounding.AwayFromZero);
    public decimal TotalGross => TotalNet + VatAmount;

    /// <summary>Formatierte Anzeige für UI</summary>
    public string DisplayTitle => string.IsNullOrEmpty(CustomerName)
        ? QuoteNumber
        : $"{QuoteNumber} - {CustomerName}";
}

/// <summary>
/// Einzelne Position in einem Angebot
/// </summary>
public class QuoteItem
{
    /// <summary>Bezeichnung (z.B. "Bodenfliesen 60x60cm")</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Einheit (z.B. "m²", "h", "Stück")</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>Menge</summary>
    public double Quantity { get; set; }

    /// <summary>Einzelpreis</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Gesamtpreis (Menge × Einzelpreis), kaufmännisch auf 2 Nachkommastellen gerundet</summary>
    public decimal Total => Math.Round((decimal)Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

    /// <summary>Art der Position</summary>
    public QuoteItemType ItemType { get; set; } = QuoteItemType.Material;
}

/// <summary>Art einer Angebots-Position</summary>
public enum QuoteItemType
{
    Material,
    Labor,
    Other
}

/// <summary>Status eines Angebots</summary>
public enum QuoteStatus
{
    Draft,
    Sent,
    Accepted,
    Rejected
}
