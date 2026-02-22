using BomberBlast.Models.Entities;

namespace BomberBlast.Models.Cards;

/// <summary>
/// Definition einer Bomben-Karte (statisch, unveränderlich).
/// Beschreibt Typ, Rarität und Gameplay-Werte pro Kartenlevel.
/// </summary>
public class BombCard
{
    /// <summary>Welcher Bombentyp</summary>
    public BombType BombType { get; init; }

    /// <summary>Rarität bestimmt Drop-Chancen und Upgrade-Kosten</summary>
    public Rarity Rarity { get; init; }

    /// <summary>RESX-Key für den Kartennamen</summary>
    public string NameKey { get; init; } = string.Empty;

    /// <summary>RESX-Key für die Kartenbeschreibung</summary>
    public string DescriptionKey { get; init; } = string.Empty;

    /// <summary>Einsätze pro Level auf Bronze-Stufe (Level 1)</summary>
    public int BaseBronzeUses { get; init; }

    /// <summary>
    /// Berechnet die verfügbaren Einsätze für ein bestimmtes Kartenlevel.
    /// Bronze (1) = Basis, Silber (2) = +1, Gold (3) = +2
    /// </summary>
    public int GetUsesForLevel(int cardLevel) => BaseBronzeUses + Math.Max(0, cardLevel - 1);

    /// <summary>
    /// Upgrade-Kosten: Benötigte Duplikate für das nächste Level
    /// </summary>
    public int GetDuplicatesForUpgrade(int currentLevel) => (Rarity, currentLevel) switch
    {
        (Rarity.Common, 1) => 3,    // Bronze → Silber
        (Rarity.Common, 2) => 5,    // Silber → Gold
        (Rarity.Rare, 1) => 3,
        (Rarity.Rare, 2) => 5,
        (Rarity.Epic, 1) => 2,
        (Rarity.Epic, 2) => 4,
        (Rarity.Legendary, 1) => 2,
        (Rarity.Legendary, 2) => 3,
        _ => int.MaxValue // Kein Upgrade möglich (schon Gold)
    };

    /// <summary>
    /// Upgrade-Kosten in Coins für das nächste Level
    /// </summary>
    public int GetCoinCostForUpgrade(int currentLevel) => (Rarity, currentLevel) switch
    {
        (Rarity.Common, 1) => 500,
        (Rarity.Common, 2) => 2_000,
        (Rarity.Rare, 1) => 1_500,
        (Rarity.Rare, 2) => 5_000,
        (Rarity.Epic, 1) => 3_000,
        (Rarity.Epic, 2) => 10_000,
        (Rarity.Legendary, 1) => 5_000,
        (Rarity.Legendary, 2) => 20_000,
        _ => int.MaxValue
    };

    /// <summary>
    /// Direktkauf-Preis in Coins (Legendary nicht für Coins kaufbar)
    /// </summary>
    public int GetDirectBuyCoinPrice() => Rarity switch
    {
        Rarity.Common => 2_000,
        Rarity.Rare => 5_000,
        Rarity.Epic => 15_000,
        _ => 0 // Legendary nicht für Coins kaufbar
    };

    /// <summary>
    /// Direktkauf-Preis in Gems
    /// </summary>
    public int GetDirectBuyGemPrice() => Rarity switch
    {
        Rarity.Common => 20,
        Rarity.Rare => 50,
        Rarity.Epic => 120,
        Rarity.Legendary => 300,
        _ => 0
    };
}
