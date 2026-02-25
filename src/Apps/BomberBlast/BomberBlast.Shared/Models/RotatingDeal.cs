namespace BomberBlast.Models;

/// <summary>
/// Repräsentiert ein rotierendes Angebot (Tages- oder Wochendeal) im Shop.
/// </summary>
public class RotatingDeal
{
    /// <summary>Eindeutige ID des Deals</summary>
    public string Id { get; set; } = "";

    /// <summary>RESX-Key für den Anzeigenamen</summary>
    public string TitleKey { get; set; } = "";

    /// <summary>Originalpreis ohne Rabatt</summary>
    public int OriginalPrice { get; set; }

    /// <summary>Rabattierter Preis</summary>
    public int DiscountedPrice { get; set; }

    /// <summary>Rabattprozent (20-50)</summary>
    public int DiscountPercent { get; set; }

    /// <summary>Währung: "Coins" oder "Gems"</summary>
    public string Currency { get; set; } = "Coins";

    /// <summary>Art der Belohnung: "Coins", "Gems", "Card", "Upgrade"</summary>
    public string RewardType { get; set; } = "";

    /// <summary>Menge der Belohnung</summary>
    public int RewardAmount { get; set; }

    /// <summary>Ob der Deal bereits eingelöst wurde</summary>
    public bool IsClaimed { get; set; }

    /// <summary>Optionaler Beschreibungs-Key (z.B. für Upgrade-Name)</summary>
    public string DescriptionKey { get; set; } = "";
}
