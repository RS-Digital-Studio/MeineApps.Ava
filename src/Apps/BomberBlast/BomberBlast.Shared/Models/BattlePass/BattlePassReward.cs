namespace BomberBlast.Models.BattlePass;

/// <summary>
/// Typ einer Battle-Pass-Belohnung
/// </summary>
public enum BattlePassRewardType
{
    Coins,
    Gems,
    CardPack,
    Cosmetic
}

/// <summary>
/// Einzelne Belohnung auf einem Battle-Pass-Tier
/// </summary>
public class BattlePassReward
{
    public BattlePassRewardType Type { get; init; }
    public int Amount { get; init; }
    /// <summary>Item-ID für Cosmetic-Rewards (z.B. Skin-ID)</summary>
    public string ItemId { get; init; } = "";
    /// <summary>Lokalisierungs-Key für die Beschreibung</summary>
    public string DescriptionKey { get; init; } = "";
    /// <summary>Icon-Name (Material.Icons.MaterialIconKind)</summary>
    public string IconName { get; init; } = "";
}
