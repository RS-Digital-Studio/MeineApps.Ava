namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Seltenheitsstufe für Meisterwerkzeuge.
/// </summary>
public enum MasterToolRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public static class MasterToolRarityExtensions
{
    public static string GetLocalizationKey(this MasterToolRarity rarity) => rarity switch
    {
        MasterToolRarity.Common => "RarityCommon",
        MasterToolRarity.Uncommon => "RarityUncommon",
        MasterToolRarity.Rare => "RarityRare",
        MasterToolRarity.Epic => "RarityEpic",
        MasterToolRarity.Legendary => "RarityLegendary",
        _ => "RarityCommon"
    };

    public static string GetColor(this MasterToolRarity rarity) => rarity switch
    {
        MasterToolRarity.Common => "#9CA3AF",     // Grau
        MasterToolRarity.Uncommon => "#22C55E",    // Grün
        MasterToolRarity.Rare => "#3B82F6",        // Blau
        MasterToolRarity.Epic => "#A855F7",        // Lila
        MasterToolRarity.Legendary => "#F59E0B",   // Gold
        _ => "#9CA3AF"
    };
}
