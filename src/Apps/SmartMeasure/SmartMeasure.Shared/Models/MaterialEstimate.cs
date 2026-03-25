namespace SmartMeasure.Shared.Models;

/// <summary>Geschaetzter Materialbedarf fuer ein Gartenelement</summary>
public class MaterialEstimate
{
    /// <summary>Material-Bezeichnung</summary>
    public string Material { get; set; } = string.Empty;

    /// <summary>Menge (Zahlenwert)</summary>
    public double Quantity { get; set; }

    /// <summary>Einheit (m², m³, lfm)</summary>
    public string Unit { get; set; } = "m²";

    /// <summary>Menge inkl. 15% Sicherheitsfaktor</summary>
    public double QuantityWithSafety => Quantity * 1.15;

    /// <summary>Zugehoeriges Gartenelement</summary>
    public string ElementName { get; set; } = string.Empty;
}
