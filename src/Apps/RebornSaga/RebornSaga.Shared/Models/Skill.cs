namespace RebornSaga.Models;

using RebornSaga.Models.Enums;
using System.Text.Json.Serialization;

/// <summary>
/// Ein Kampf-Skill mit Stufen-basierter Evolution.
/// Skills werden durch Nutzung gemeistert und entwickeln sich weiter.
/// </summary>
public class Skill
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("nameKey")]
    public string NameKey { get; set; } = "";

    [JsonPropertyName("descriptionKey")]
    public string DescriptionKey { get; set; } = "";

    [JsonPropertyName("class")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ClassName Class { get; set; }

    [JsonPropertyName("mpCost")]
    public int MpCost { get; set; }

    /// <summary>
    /// Schadens-Multiplikator (0 = kein Schaden, z.B. Buff/Heal).
    /// </summary>
    [JsonPropertyName("multiplier")]
    public float Multiplier { get; set; }

    /// <summary>
    /// Element des Skills (null = neutral).
    /// </summary>
    [JsonPropertyName("element")]
    public string? ElementStr { get; set; }

    /// <summary>
    /// Zusatz-Effekt (z.B. "heal_10pct", "burn_3", "stun_20pct", "atk_buff_30").
    /// </summary>
    [JsonPropertyName("effect")]
    public string? Effect { get; set; }

    /// <summary>
    /// Stufe innerhalb der Evolution (1-5, 5 = Ultimate).
    /// </summary>
    [JsonPropertyName("tier")]
    public int Tier { get; set; } = 1;

    /// <summary>
    /// Wie oft der Skill benutzt werden muss um die nächste Stufe freizuschalten.
    /// 0 = keine weitere Evolution (Ultimate oder Basis).
    /// </summary>
    [JsonPropertyName("masteryRequired")]
    public int MasteryRequired { get; set; }

    /// <summary>
    /// ID der nächsten Stufe (null = höchste Stufe).
    /// </summary>
    [JsonPropertyName("nextTierId")]
    public string? NextTierId { get; set; }

    /// <summary>
    /// Ob der Skill ein Ultimate ist (Stufe 5, besonders stark).
    /// </summary>
    [JsonPropertyName("isUltimate")]
    public bool IsUltimate { get; set; }

    /// <summary>
    /// Ob der Skill AoE (alle Gegner) betrifft.
    /// </summary>
    [JsonPropertyName("isAoe")]
    public bool IsAoe { get; set; }

    // Lazy-gecachtes Element
    private Element? _cachedElement;
    private bool _elementParsed;

    [JsonIgnore]
    public Element? Element
    {
        get
        {
            if (!_elementParsed)
            {
                _cachedElement = ElementStr?.ToLowerInvariant() switch
                {
                    "fire" => Enums.Element.Fire,
                    "ice" => Enums.Element.Ice,
                    "lightning" => Enums.Element.Lightning,
                    "wind" => Enums.Element.Wind,
                    "light" => Enums.Element.Light,
                    "dark" => Enums.Element.Dark,
                    _ => null
                };
                _elementParsed = true;
            }
            return _cachedElement;
        }
    }
}

/// <summary>
/// Spieler-Skill-Instanz mit Mastery-Fortschritt.
/// </summary>
public class PlayerSkill
{
    /// <summary>Skill-Definition.</summary>
    public Skill Definition { get; set; } = null!;

    /// <summary>Aktuelle Mastery (Anzahl Benutzungen).</summary>
    public int Mastery { get; set; }

    /// <summary>Ist der Skill freigeschaltet?</summary>
    public bool IsUnlocked { get; set; }

    /// <summary>Kann der Skill zur nächsten Stufe evolvieren?</summary>
    [JsonIgnore]
    public bool CanEvolve =>
        Definition.MasteryRequired > 0 &&
        Mastery >= Definition.MasteryRequired &&
        !string.IsNullOrEmpty(Definition.NextTierId);
}
