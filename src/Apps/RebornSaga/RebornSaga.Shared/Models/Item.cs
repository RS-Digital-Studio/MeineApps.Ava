namespace RebornSaga.Models;

using RebornSaga.Models.Enums;
using System.Text.Json.Serialization;

/// <summary>
/// Item-Definition aus JSON. Waffen, Rüstungen, Accessoires, Verbrauchsgegenstände, Key-Items.
/// </summary>
public class Item
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("nameKey")]
    public string NameKey { get; set; } = "";

    [JsonPropertyName("descriptionKey")]
    public string DescriptionKey { get; set; } = "";

    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ItemType Type { get; set; }

    /// <summary>
    /// Welche Klasse das Equipment tragen darf (null = alle).
    /// </summary>
    [JsonPropertyName("classRestriction")]
    public string? ClassRestriction { get; set; }

    // --- Stat-Boni (Equipment) ---

    [JsonPropertyName("atkBonus")]
    public int AtkBonus { get; set; }

    [JsonPropertyName("defBonus")]
    public int DefBonus { get; set; }

    [JsonPropertyName("intBonus")]
    public int IntBonus { get; set; }

    [JsonPropertyName("spdBonus")]
    public int SpdBonus { get; set; }

    [JsonPropertyName("lukBonus")]
    public int LukBonus { get; set; }

    [JsonPropertyName("hpBonus")]
    public int HpBonus { get; set; }

    [JsonPropertyName("mpBonus")]
    public int MpBonus { get; set; }

    /// <summary>
    /// Spezial-Effekt als String (z.B. "crit_5pct", "fire_element", "fire_resist_30pct").
    /// </summary>
    [JsonPropertyName("effect")]
    public string? Effect { get; set; }

    // --- Verbrauchsgegenstände ---

    /// <summary>
    /// HP-Heilung (Consumable).
    /// </summary>
    [JsonPropertyName("healHp")]
    public int HealHp { get; set; }

    /// <summary>
    /// MP-Heilung (Consumable).
    /// </summary>
    [JsonPropertyName("healMp")]
    public int HealMp { get; set; }

    /// <summary>
    /// Prozentuale Heilung (z.B. 100 = volle HP+MP, Elixier).
    /// </summary>
    [JsonPropertyName("healPercent")]
    public int HealPercent { get; set; }

    // --- Wirtschaft ---

    [JsonPropertyName("buyPrice")]
    public int BuyPrice { get; set; }

    [JsonPropertyName("sellPrice")]
    public int SellPrice { get; set; }

    /// <summary>
    /// Wo das Item verfügbar ist (z.B. "K1 Shop", "K3 Boss-Drop").
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    // --- Equipment-Slot ---

    /// <summary>
    /// Equipment-Slot (nur für Equipment-Typen relevant).
    /// </summary>
    [JsonIgnore]
    public EquipSlot Slot => Type switch
    {
        ItemType.Weapon => EquipSlot.Weapon,
        ItemType.Armor => EquipSlot.Armor,
        ItemType.Accessory => EquipSlot.Accessory,
        _ => EquipSlot.Weapon // Fallback, wird für Consumable/KeyItem nicht genutzt
    };

    /// <summary>
    /// Ist das Item ausrüstbar (Waffe, Rüstung, Accessoire)?
    /// </summary>
    [JsonIgnore]
    public bool IsEquippable => Type is ItemType.Weapon or ItemType.Armor or ItemType.Accessory;

    /// <summary>
    /// Ist das Item im Kampf benutzbar (Consumable)?
    /// </summary>
    [JsonIgnore]
    public bool IsUsable => Type == ItemType.Consumable;
}
