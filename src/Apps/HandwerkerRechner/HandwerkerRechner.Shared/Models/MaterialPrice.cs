namespace HandwerkerRechner.Models;

/// <summary>
/// Materialpreis mit Standardwert und optionalem benutzerdefiniertem Preis.
/// Wird für Kostenschätzungen in allen Rechnern verwendet.
/// </summary>
public class MaterialPrice
{
    /// <summary>Eindeutiger Schlüssel (z.B. "tile_standard", "cable_1_5mm")</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>RESX-Key für den lokalisierten Anzeigenamen</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Einheit (z.B. "€/m²", "€/l", "€/Stück", "€/m")</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>Regionaler Durchschnittspreis (Deutschland)</summary>
    public double DefaultPrice { get; set; }

    /// <summary>Benutzerdefinierter Preis (-1 = nicht überschrieben)</summary>
    public double CustomPrice { get; set; } = -1;

    /// <summary>Effektiver Preis: CustomPrice wenn gesetzt, sonst DefaultPrice</summary>
    public double EffectivePrice => CustomPrice >= 0 ? CustomPrice : DefaultPrice;

    /// <summary>Kategorie für Gruppierung (z.B. "flooring", "electrical", "wall")</summary>
    public string Category { get; set; } = string.Empty;
}
