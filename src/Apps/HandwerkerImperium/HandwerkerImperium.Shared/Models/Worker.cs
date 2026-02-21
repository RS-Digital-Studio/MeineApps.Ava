using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Represents a worker with tier, mood, fatigue, experience, and personality.
/// Workers generate passive income but require management (rest, training, mood).
/// </summary>
public class Worker
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("tier")]
    public WorkerTier Tier { get; set; } = WorkerTier.E;

    /// <summary>
    /// Talent rating (1-5 stars). Determines max efficiency ceiling.
    /// </summary>
    [JsonPropertyName("talent")]
    public int Talent { get; set; } = 3;

    [JsonPropertyName("personality")]
    public WorkerPersonality Personality { get; set; } = WorkerPersonality.Steady;

    /// <summary>
    /// Preferred workshop type. +15% efficiency when working in this workshop.
    /// </summary>
    [JsonPropertyName("specialization")]
    public WorkshopType? Specialization { get; set; }

    /// <summary>
    /// Worker assigned to this workshop. Can be transferred between workshops.
    /// </summary>
    [JsonPropertyName("assignedWorkshop")]
    public WorkshopType? AssignedWorkshop { get; set; }

    /// <summary>
    /// Mood level (0-100). Below 50 = unhappy, below 20 = will quit within 24h.
    /// </summary>
    [JsonPropertyName("mood")]
    public decimal Mood { get; set; } = 80m;

    /// <summary>
    /// Fatigue level (0-100). At 100 = exhausted, must rest.
    /// Increases ~12.5/h while working, resets during rest.
    /// </summary>
    [JsonPropertyName("fatigue")]
    public decimal Fatigue { get; set; }

    /// <summary>
    /// Experience level (1-10). Increases max efficiency.
    /// </summary>
    [JsonPropertyName("experienceLevel")]
    public int ExperienceLevel { get; set; } = 1;

    /// <summary>
    /// Current XP towards next experience level.
    /// </summary>
    [JsonPropertyName("experienceXp")]
    public int ExperienceXp { get; set; }

    /// <summary>
    /// Akkumulator für fraktionale XP-Gewinne beim Arbeiten.
    /// Wird nicht persistiert - nur innerhalb einer Session relevant.
    /// </summary>
    [JsonIgnore]
    public decimal WorkingXpAccumulator { get; set; }

    /// <summary>
    /// Akkumulator für fraktionale XP-Gewinne beim Training.
    /// Wird nicht persistiert - nur innerhalb einer Session relevant.
    /// </summary>
    [JsonIgnore]
    public decimal TrainingXpAccumulator { get; set; }

    /// <summary>
    /// Hourly wage based on tier.
    /// </summary>
    [JsonPropertyName("wagePerHour")]
    public decimal WagePerHour { get; set; } = 10m;

    [JsonPropertyName("isResting")]
    public bool IsResting { get; set; }

    [JsonPropertyName("isTraining")]
    public bool IsTraining { get; set; }

    [JsonPropertyName("restStartedAt")]
    public DateTime? RestStartedAt { get; set; }

    [JsonPropertyName("activeTrainingType")]
    public TrainingType ActiveTrainingType { get; set; } = TrainingType.Efficiency;

    /// <summary>
    /// Ausdauer-Bonus (0-0.5): Reduziert FatiguePerHour permanent um bis zu 50%.
    /// </summary>
    [JsonPropertyName("enduranceBonus")]
    public decimal EnduranceBonus { get; set; }

    /// <summary>
    /// Stimmungs-Bonus (0-0.5): Reduziert MoodDecayPerHour permanent um bis zu 50%.
    /// </summary>
    [JsonPropertyName("moraleBonus")]
    public decimal MoraleBonus { get; set; }

    [JsonPropertyName("trainingStartedAt")]
    public DateTime? TrainingStartedAt { get; set; }

    /// <summary>
    /// Total money this worker has earned (lifetime).
    /// </summary>
    [JsonPropertyName("totalEarned")]
    public decimal TotalEarned { get; set; }

    /// <summary>
    /// Number of orders this worker has contributed to.
    /// </summary>
    [JsonPropertyName("ordersCompleted")]
    public int OrdersCompleted { get; set; }

    /// <summary>
    /// Geschlecht des Workers (deterministisch aus Id abgeleitet).
    /// </summary>
    [JsonPropertyName("isFemale")]
    public bool IsFemale { get; set; }

    /// <summary>
    /// Ausgerüstetes Equipment (null = nichts ausgerüstet).
    /// </summary>
    [JsonPropertyName("equippedItem")]
    public Equipment? EquippedItem { get; set; }

    [JsonPropertyName("hiredAt")]
    public DateTime HiredAt { get; set; }

    /// <summary>
    /// When the worker will quit if mood stays below 20.
    /// </summary>
    [JsonPropertyName("quitDeadline")]
    public DateTime? QuitDeadline { get; set; }

    // Legacy property for save compatibility
    [JsonPropertyName("efficiency")]
    public decimal Efficiency { get; set; } = 1.0m;

    [JsonPropertyName("skillLevel")]
    public int SkillLevel { get; set; } = 1;

    // ═══════════════════════════════════════════════════════════════════════
    // CALCULATED PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Effective efficiency considering all factors.
    /// Formula: BaseEfficiency * MoodFactor * FatigueFactor * Specialization * Personality
    /// </summary>
    [JsonIgnore]
    public decimal EffectiveEfficiency
    {
        get
        {
            if (IsResting || IsTraining) return 0m;

            decimal baseEff = Efficiency;
            decimal moodFactor = GetMoodFactor();
            decimal fatigueFactor = GetFatigueFactor();
            decimal specBonus = GetSpecializationBonus();
            decimal personalityMult = Personality.GetEfficiencyMultiplier();
            decimal equipBonus = EquippedItem?.EfficiencyBonus ?? 0m;

            return Math.Max(0m, baseEff * moodFactor * fatigueFactor * (1m + specBonus + equipBonus) * personalityMult);
        }
    }

    /// <summary>
    /// Maximum efficiency this worker can reach (talent + experience bonus).
    /// </summary>
    [JsonIgnore]
    public decimal MaxEfficiency
    {
        get
        {
            decimal tierMax = Tier.GetMaxEfficiency();
            decimal xpBonus = 1m + ExperienceLevel * 0.03m;
            decimal talentBonus = 1m + (Talent - 1) * 0.05m;
            return tierMax * xpBonus * talentBonus;
        }
    }

    /// <summary>
    /// Mood decay rate per hour (base 3%, modified by personality, buildings und MoraleBonus).
    /// </summary>
    [JsonIgnore]
    public decimal MoodDecayPerHour => 3m * Personality.GetMoodDecayMultiplier() * (1m - MoraleBonus);

    /// <summary>
    /// Fatigue increase per hour of work (base 12.5, 8h to exhaust, reduziert durch EnduranceBonus).
    /// </summary>
    [JsonIgnore]
    public decimal FatiguePerHour => 12.5m * Personality.GetFatigueMultiplier() * (1m - EnduranceBonus);

    /// <summary>
    /// Hours needed to fully rest (base 4h).
    /// </summary>
    [JsonIgnore]
    public decimal RestHoursNeeded => 4m;

    /// <summary>
    /// Cost to train per hour (2x hourly wage).
    /// </summary>
    [JsonIgnore]
    public decimal TrainingCostPerHour => 2m * WagePerHour;

    /// <summary>
    /// XP gained per hour of training (base 50).
    /// </summary>
    [JsonIgnore]
    public int TrainingXpPerHour => 50;

    /// <summary>
    /// XP required for next experience level.
    /// </summary>
    [JsonIgnore]
    public int XpForNextLevel => ExperienceLevel * 200;

    [JsonIgnore]
    public bool IsTired => Fatigue >= 100m;

    [JsonIgnore]
    public bool IsUnhappy => Mood < 50m;

    [JsonIgnore]
    public bool WillQuit => Mood < 20m;

    [JsonIgnore]
    public bool IsWorking => !IsResting && !IsTraining && AssignedWorkshop != null;

    /// <summary>
    /// Personality icon emoji for display (avoids extension method in bindings).
    /// </summary>
    [JsonIgnore]
    public string PersonalityIcon => Personality.GetIcon();

    /// <summary>
    /// Anstellungskosten (level-skaliert). Wird beim Laden des Markts gesetzt.
    /// Fallback auf Basis-Kosten wenn nicht gesetzt.
    /// </summary>
    [JsonIgnore]
    public decimal HiringCost { get; set; }

    /// <summary>
    /// Zusaetzliche Goldschrauben-Kosten (nur Tier A und S).
    /// </summary>
    [JsonIgnore]
    public int HiringScrewCost => Tier.GetHiringScrewCost();

    [JsonIgnore]
    public string MoodEmoji => Mood switch
    {
        >= 80m => "\ud83d\ude0a",  // Smiling
        >= 50m => "\ud83d\ude10",  // Neutral
        >= 20m => "\ud83d\ude1f",  // Worried
        _ => "\ud83d\ude21"         // Angry
    };

    /// <summary>
    /// Lokalisierter Persönlichkeitsname (wird vom ViewModel gesetzt).
    /// </summary>
    [JsonIgnore]
    public string PersonalityDisplay { get; set; } = string.Empty;

    /// <summary>
    /// Lokalisierter Spezialisierungsname (wird vom ViewModel gesetzt).
    /// </summary>
    [JsonIgnore]
    public string SpecializationDisplay { get; set; } = string.Empty;

    /// <summary>
    /// Einkommensbeitrag pro Sekunde (wird vom ViewModel gesetzt, nicht persistiert).
    /// Zeigt dem Spieler den konkreten Mehrwert dieses Arbeiters.
    /// </summary>
    [JsonIgnore]
    public decimal IncomeContribution { get; set; }

    /// <summary>
    /// Formatierte Anzeige des Einkommensbeitrags.
    /// </summary>
    [JsonIgnore]
    public string IncomeContributionDisplay => IncomeContribution > 0
        ? $"+{IncomeContribution:N2} €/s"
        : "-";

    [JsonIgnore]
    public string StatusEmoji
    {
        get
        {
            if (IsResting) return "\ud83d\udca4";     // Sleeping
            if (IsTraining) return "\ud83d\udcda";    // Books
            if (IsTired) return "\ud83d\ude29";        // Weary
            return "\ud83d\udee0\ufe0f";              // Hammer and wrench (working)
        }
    }

    /// <summary>
    /// Tier-Farbe als Hex-String fuer UI-Binding (z.B. "#4CAF50").
    /// </summary>
    [JsonIgnore]
    public string TierColorHex => Tier.GetColorKey();

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    private decimal GetMoodFactor()
    {
        // Mood 100 = 1.1x, Mood 80 = 1.0x, Mood 50 = 0.8x, Mood 0 = 0.5x
        if (Mood >= 80m) return 1.0m + (Mood - 80m) / 200m;
        if (Mood >= 50m) return 0.8m + (Mood - 50m) / 150m;
        return 0.5m + Mood / 100m;
    }

    private decimal GetFatigueFactor()
    {
        // Fatigue 0 = 1.0x, 50 = 0.85x, 100 = 0.5x
        if (Fatigue <= 0m) return 1.0m;
        if (Fatigue >= 100m) return 0.5m;
        return 1.0m - (Fatigue / 200m);
    }

    private decimal GetSpecializationBonus()
    {
        if (Specialization == null || AssignedWorkshop == null) return 0m;
        if (Specialization != AssignedWorkshop) return 0m;
        return 0.15m + Personality.GetSpecializationBonus();
    }

    /// <summary>
    /// Berechnet den individuellen Marktpreis basierend auf Tier, Level, Talent,
    /// Persönlichkeit, Spezialisierung und Basis-Effizienz innerhalb des Tiers.
    /// Höherwertiger Worker = höherer Preis.
    /// </summary>
    public decimal CalculateMarketPrice(int playerLevel)
    {
        var baseCost = Tier.GetHiringCost(playerLevel);

        // Talent-Multiplikator: 1★=0.70x, 2★=0.85x, 3★=1.00x, 4★=1.15x, 5★=1.30x
        var talentMult = 0.70m + (Talent - 1) * 0.15m;

        // Persönlichkeits-Multiplikator (wertvollere Traits kosten mehr)
        var personalityMult = Personality switch
        {
            WorkerPersonality.Perfectionist => 1.20m,
            WorkerPersonality.Specialist => 1.15m,
            WorkerPersonality.Ambitious => 1.10m,
            WorkerPersonality.Cheerful => 1.05m,
            WorkerPersonality.Relaxed => 0.90m,
            _ => 1.0m // Steady
        };

        // Spezialisierung macht Worker wertvoller
        var specMult = Specialization.HasValue ? 1.15m : 1.0m;

        // Effizienz-Position innerhalb des Tiers (höhere Basis = teurer)
        var minEff = Tier.GetMinEfficiency();
        var maxEff = Tier.GetMaxEfficiency();
        var effRange = maxEff - minEff;
        var effPosition = effRange > 0 ? Math.Clamp((Efficiency - minEff) / effRange, 0m, 1m) : 0.5m;
        var effMult = 0.85m + effPosition * 0.30m; // 0.85x (Min) bis 1.15x (Max)

        return Math.Round(baseCost * talentMult * personalityMult * specMult * effMult);
    }

    /// <summary>
    /// Creates a worker for a specific tier with random attributes.
    /// </summary>
    public static Worker CreateForTier(WorkerTier tier)
    {
        var random = Random.Shared;
        var personality = (WorkerPersonality)random.Next(0, 6);
        var talent = tier switch
        {
            WorkerTier.F => random.Next(1, 3),        // 1-2
            WorkerTier.E => random.Next(1, 4),        // 1-3
            WorkerTier.D => random.Next(2, 4),        // 2-3
            WorkerTier.C => random.Next(2, 5),        // 2-4
            WorkerTier.B => random.Next(3, 5),        // 3-4
            WorkerTier.A => random.Next(3, 6),        // 3-5
            WorkerTier.S => random.Next(4, 6),        // 4-5
            WorkerTier.SS => random.Next(4, 6),       // 4-5
            WorkerTier.SSS => 5,                      // immer 5
            WorkerTier.Legendary => 5,                // immer 5
            _ => 3
        };

        // Random specialization (50% chance of having one)
        WorkshopType? spec = null;
        if (random.NextDouble() > 0.5)
        {
            var types = Enum.GetValues<WorkshopType>();
            spec = types[random.Next(types.Length)];
        }

        // Base efficiency within tier range
        var minEff = tier.GetMinEfficiency();
        var maxEff = tier.GetMaxEfficiency();
        var efficiency = minEff + (maxEff - minEff) * (decimal)random.NextDouble();

        var id = Guid.NewGuid().ToString();

        var worker = new Worker
        {
            Id = id,
            Name = GenerateRandomName(),
            Tier = tier,
            Talent = talent,
            Personality = personality,
            Specialization = spec,
            Mood = 80m,
            Fatigue = 0m,
            ExperienceLevel = 1,
            ExperienceXp = 0,
            WagePerHour = tier.GetWagePerHour(),
            Efficiency = Math.Round(efficiency, 3),
            SkillLevel = 1,
            HiredAt = DateTime.UtcNow,
            IsFemale = (id.GetHashCode() % 2 == 0)
        };

        return worker;
    }

    /// <summary>
    /// Legacy factory method (creates a Tier E worker).
    /// </summary>
    public static Worker CreateRandom()
    {
        return CreateForTier(WorkerTier.E);
    }

    /// <summary>
    /// Returns available tiers based on player level and prestige.
    /// </summary>
    public static List<WorkerTier> GetAvailableTiers(int playerLevel, int prestigeLevel, bool hasSTierResearch = false)
    {
        var tiers = new List<WorkerTier>();
        foreach (var tier in Enum.GetValues<WorkerTier>())
        {
            // S, SS, SSS, Legendary brauchen alle S-Tier Research-Unlock
            if (tier >= WorkerTier.S && !hasSTierResearch) continue;
            if (playerLevel >= tier.GetUnlockLevel())
                tiers.Add(tier);
        }
        return tiers;
    }

    private static string GenerateRandomName()
    {
        var firstNames = new[]
        {
            "Hans", "Klaus", "Peter", "Michael", "Thomas",
            "Stefan", "Andreas", "Markus", "Frank", "Erik",
            "Carlos", "Marco", "Pierre", "James", "Oliver",
            "Lucas", "Matteo", "Hugo", "Leo", "Noah",
            "Finn", "Liam", "Emil", "Anton", "Felix",
            "Sofia", "Anna", "Maria", "Elena", "Laura"
        };

        var surnames = new[]
        {
            "M\u00fcller", "Schmidt", "Schneider", "Fischer", "Weber",
            "Meyer", "Wagner", "Becker", "Schulz", "Hoffmann",
            "Martin", "Garcia", "Santos", "Silva", "Rossi",
            "Dupont", "Brown", "Wilson", "Anderson", "Taylor"
        };

        var random = Random.Shared;
        return $"{firstNames[random.Next(firstNames.Length)]} {surnames[random.Next(surnames.Length)]}";
    }
}
