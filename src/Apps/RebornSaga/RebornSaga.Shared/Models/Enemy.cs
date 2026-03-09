namespace RebornSaga.Models;

using RebornSaga.Models.Enums;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Gegner-Definition aus JSON. Enthält Stats, Element, Drops.
/// </summary>
public class Enemy
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("nameKey")]
    public string NameKey { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("hp")]
    public int Hp { get; set; }

    [JsonPropertyName("atk")]
    public int Atk { get; set; }

    [JsonPropertyName("def")]
    public int Def { get; set; }

    [JsonPropertyName("spd")]
    public int Spd { get; set; } = 5;

    [JsonPropertyName("element")]
    public string? ElementStr { get; set; }

    [JsonPropertyName("weakness")]
    public string? WeaknessStr { get; set; }

    [JsonPropertyName("exp")]
    public int Exp { get; set; }

    [JsonPropertyName("gold")]
    public int Gold { get; set; }

    [JsonPropertyName("phases")]
    public int Phases { get; set; } = 1;

    [JsonPropertyName("drops")]
    public List<EnemyDrop>? Drops { get; set; }

    /// <summary>Boss ist Teil des Prologs (unbesiegbar oder skriptiert).</summary>
    [JsonPropertyName("isProlog")]
    public bool IsProlog { get; set; }

    /// <summary>Skriptierter Kampf — feste Rundenzahl, dann Cutscene.</summary>
    [JsonPropertyName("isScripted")]
    public bool IsScripted { get; set; }

    /// <summary>Anzahl Runden bei skriptiertem Kampf.</summary>
    [JsonPropertyName("scriptedRounds")]
    public int ScriptedRounds { get; set; }

    /// <summary>Reiner Cutscene-Boss, kein echter Kampf.</summary>
    [JsonPropertyName("isCutsceneOnly")]
    public bool IsCutsceneOnly { get; set; }

    // Lazy-gecachte Elemente (vermeidet ToLowerInvariant() pro Zugriff)
    private Element? _cachedElement;
    private Element? _cachedWeakness;
    private bool _elementParsed;
    private bool _weaknessParsed;

    /// <summary>Parsed das Element aus dem String (lazy-cached).</summary>
    [JsonIgnore]
    public Element? Element
    {
        get
        {
            if (!_elementParsed) { _cachedElement = ParseElement(ElementStr); _elementParsed = true; }
            return _cachedElement;
        }
    }

    /// <summary>Parsed die Schwäche aus dem String (lazy-cached).</summary>
    [JsonIgnore]
    public Element? Weakness
    {
        get
        {
            if (!_weaknessParsed) { _cachedWeakness = ParseElement(WeaknessStr); _weaknessParsed = true; }
            return _cachedWeakness;
        }
    }

    private static Element? ParseElement(string? str) => str?.ToLowerInvariant() switch
    {
        "fire" => Enums.Element.Fire,
        "ice" => Enums.Element.Ice,
        "lightning" => Enums.Element.Lightning,
        "wind" => Enums.Element.Wind,
        "light" => Enums.Element.Light,
        "dark" => Enums.Element.Dark,
        _ => null
    };
}

/// <summary>
/// Ein möglicher Item-Drop eines Gegners.
/// </summary>
public class EnemyDrop
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = "";

    [JsonPropertyName("chance")]
    public float Chance { get; set; }
}
