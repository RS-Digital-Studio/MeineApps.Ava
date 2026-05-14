using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Spezialisierungstyp für Workshops (ab Level 50, vorher Doc-Bug "Level 100").
/// Korrekter Unlock-Level ist 50.
/// Quality + Economy haben jetzt sichtbare Eigen-Vorteile (Aura-Verdopplung
/// bzw. Order-Reward-Bonus) — vorher gewann Efficiency immer und Re-Spec war toter Code.
/// </summary>
public enum SpecializationType
{
    /// <summary>+30% Einkommen, -1 Worker-Slot.</summary>
    Efficiency,
    /// <summary>+20% Worker-Effizienz, +15% Kosten, +100% Aura-Bonus. (B-H05: Aura-Verdopplung neu)</summary>
    Quality,
    /// <summary>-25% Kosten, -5% Einkommen, +15% Auftragsbelohnung. (B-H05: Reward-Bonus neu)</summary>
    Economy
}

/// <summary>
/// Eine gewählte Workshop-Spezialisierung.
/// </summary>
public class WorkshopSpecialization
{
    [JsonPropertyName("type")]
    public SpecializationType Type { get; set; }

    [JsonIgnore]
    public decimal IncomeModifier => Type switch
    {
        SpecializationType.Efficiency => 0.30m,
        SpecializationType.Quality => 0m,
        SpecializationType.Economy => -0.05m,
        _ => 0m
    };

    [JsonIgnore]
    public decimal CostModifier => Type switch
    {
        SpecializationType.Efficiency => 0m,
        SpecializationType.Quality => 0.15m,
        SpecializationType.Economy => -0.25m,
        _ => 0m
    };

    [JsonIgnore]
    public decimal EfficiencyModifier => Type switch
    {
        SpecializationType.Efficiency => 0m,
        SpecializationType.Quality => 0.20m,
        SpecializationType.Economy => 0m,
        _ => 0m
    };

    [JsonIgnore]
    public int WorkerCapacityModifier => Type switch
    {
        SpecializationType.Efficiency => -1,  // -10% → ca. 1 weniger
        _ => 0
    };

    /// <summary>
    /// Multiplikator auf den Aura-Bonus von S+-Tier-Workern (siehe GameBalanceConstants.MaxAuraBonus).
    /// Quality verdoppelt die effektive Aura ueber den Standard-Cap hinweg → echter Eigen-Vorteil
    /// neben "20% Effizienz". Ohne Spezialisierung: 1.0m (kein Modifier).
    /// </summary>
    [JsonIgnore]
    public decimal AuraBonusMultiplier => Type switch
    {
        SpecializationType.Quality => 2.0m,
        _ => 1.0m
    };

    /// <summary>
    /// Bonus auf Auftrags-Belohnungen aus diesem Workshop. Economy bekommt jetzt einen
    /// sichtbaren positiven Effekt jenseits "Kosten -25%" — kompensiert die -5% Einkommen.
    /// </summary>
    [JsonIgnore]
    public decimal OrderRewardBonus => Type switch
    {
        SpecializationType.Economy => 0.15m,
        _ => 0m
    };

    [JsonIgnore]
    public string NameKey => $"Specialization_{Type}";

    [JsonIgnore]
    public string DescriptionKey => $"Specialization_{Type}_Desc";

    [JsonIgnore]
    public string Color => Type switch
    {
        SpecializationType.Efficiency => "#FF9800",
        SpecializationType.Quality => "#2196F3",
        SpecializationType.Economy => "#4CAF50",
        _ => "#808080"
    };
}
