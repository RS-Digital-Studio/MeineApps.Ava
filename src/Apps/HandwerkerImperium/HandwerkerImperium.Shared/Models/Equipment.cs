using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Ausrüstungstyp für Arbeiter.
/// </summary>
public enum EquipmentType
{
    Helmet,
    Gloves,
    Boots,
    Belt
}

/// <summary>
/// Seltenheitsstufe der Ausrüstung.
/// </summary>
public enum EquipmentRarity
{
    Common,
    Uncommon,
    Rare,
    Epic
}

/// <summary>
/// Ein Ausrüstungsgegenstand der einem Arbeiter zugewiesen werden kann.
/// </summary>
public class Equipment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    [JsonPropertyName("type")]
    public EquipmentType Type { get; set; }

    [JsonPropertyName("rarity")]
    public EquipmentRarity Rarity { get; set; }

    [JsonPropertyName("nameKey")]
    public string NameKey { get; set; } = "";

    [JsonPropertyName("efficiencyBonus")]
    public decimal EfficiencyBonus { get; set; }

    [JsonPropertyName("fatigueReduction")]
    public decimal FatigueReduction { get; set; }

    [JsonPropertyName("moodBonus")]
    public decimal MoodBonus { get; set; }

    /// <summary>
    /// Farbe basierend auf Seltenheit.
    /// </summary>
    [JsonIgnore]
    public string RarityColor => Rarity switch
    {
        EquipmentRarity.Common => "#9E9E9E",
        EquipmentRarity.Uncommon => "#4CAF50",
        EquipmentRarity.Rare => "#2196F3",
        EquipmentRarity.Epic => "#9C27B0",
        _ => "#9E9E9E"
    };

    [JsonIgnore]
    public string RarityIcon => Rarity switch
    {
        EquipmentRarity.Common => "Circle",
        EquipmentRarity.Uncommon => "DiamondOutline",
        EquipmentRarity.Rare => "Diamond",
        EquipmentRarity.Epic => "Star",
        _ => "Circle"
    };

    /// <summary>
    /// Erzeugt ein zufälliges Equipment basierend auf Schwierigkeit.
    /// </summary>
    public static Equipment GenerateRandom(int difficultyLevel)
    {
        var rng = Random.Shared;
        var type = (EquipmentType)rng.Next(4);

        // Seltenheit gewichtet nach Schwierigkeit
        int roll = rng.Next(100);
        EquipmentRarity rarity;
        if (difficultyLevel >= 3 && roll < 5) rarity = EquipmentRarity.Epic;
        else if (difficultyLevel >= 2 && roll < 20) rarity = EquipmentRarity.Rare;
        else if (roll < 45) rarity = EquipmentRarity.Uncommon;
        else rarity = EquipmentRarity.Common;

        // Bonuswerte nach Seltenheit
        decimal effBonus = rarity switch
        {
            EquipmentRarity.Common => rng.Next(5, 8) / 100m,
            EquipmentRarity.Uncommon => rng.Next(8, 11) / 100m,
            EquipmentRarity.Rare => rng.Next(11, 14) / 100m,
            EquipmentRarity.Epic => rng.Next(13, 16) / 100m,
            _ => 0.05m
        };

        decimal fatReduction = rarity switch
        {
            EquipmentRarity.Common => rng.Next(3, 6) / 100m,
            EquipmentRarity.Uncommon => rng.Next(6, 9) / 100m,
            EquipmentRarity.Rare => rng.Next(9, 12) / 100m,
            EquipmentRarity.Epic => rng.Next(11, 15) / 100m,
            _ => 0.03m
        };

        decimal moodBonus = rarity switch
        {
            EquipmentRarity.Common => rng.Next(3, 6) / 100m,
            EquipmentRarity.Uncommon => rng.Next(5, 8) / 100m,
            EquipmentRarity.Rare => rng.Next(7, 10) / 100m,
            EquipmentRarity.Epic => rng.Next(8, 11) / 100m,
            _ => 0.03m
        };

        string nameKey = $"Equipment_{type}_{rarity}";

        return new Equipment
        {
            Type = type,
            Rarity = rarity,
            NameKey = nameKey,
            EfficiencyBonus = effBonus,
            FatigueReduction = fatReduction,
            MoodBonus = moodBonus
        };
    }

    /// <summary>
    /// Goldschrauben-Preis im Shop.
    /// ECON-1 (20.04.2026): Alte Preise 5/15/30/60 GS waren bei F2P-GS-Einkommen (~35 GS/Tag)
    /// tot — Epic = 2 Tage reines Farming. Rebalancing auf 3/8/18/40 macht den Shop fuer F2P nutzbar.
    /// </summary>
    [JsonIgnore]
    public int ShopPrice => Rarity switch
    {
        EquipmentRarity.Common => 3,
        EquipmentRarity.Uncommon => 8,
        EquipmentRarity.Rare => 18,
        EquipmentRarity.Epic => 40,
        _ => 3
    };
}
