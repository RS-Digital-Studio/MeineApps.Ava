namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Worker personalities that affect gameplay.
/// Each personality provides a unique bonus and drawback.
/// </summary>
public enum WorkerPersonality
{
    /// <summary>Balanced, no special bonuses or penalties</summary>
    Steady = 0,

    /// <summary>+20% efficiency, mood decays 50% faster</summary>
    Perfectionist = 1,

    /// <summary>Mood decays 50% slower, -10% efficiency</summary>
    Cheerful = 2,

    /// <summary>+25% XP gain, fatigue 25% faster</summary>
    Ambitious = 3,

    /// <summary>Fatigue 30% slower, -15% efficiency</summary>
    Relaxed = 4,

    /// <summary>+15% specialization bonus (instead of +15%), lower mood impact from transfers</summary>
    Specialist = 5
}

public static class WorkerPersonalityExtensions
{
    /// <summary>
    /// Efficiency multiplier from personality.
    /// </summary>
    public static decimal GetEfficiencyMultiplier(this WorkerPersonality personality) => personality switch
    {
        WorkerPersonality.Steady => 1.0m,
        WorkerPersonality.Perfectionist => 1.20m,
        WorkerPersonality.Cheerful => 0.90m,
        WorkerPersonality.Ambitious => 1.0m,
        WorkerPersonality.Relaxed => 0.85m,
        WorkerPersonality.Specialist => 1.0m,
        _ => 1.0m
    };

    /// <summary>
    /// Mood decay rate multiplier (higher = faster decay).
    /// </summary>
    public static decimal GetMoodDecayMultiplier(this WorkerPersonality personality) => personality switch
    {
        WorkerPersonality.Steady => 1.0m,
        WorkerPersonality.Perfectionist => 1.5m,
        WorkerPersonality.Cheerful => 0.5m,
        WorkerPersonality.Ambitious => 1.0m,
        WorkerPersonality.Relaxed => 1.0m,
        WorkerPersonality.Specialist => 1.0m,
        _ => 1.0m
    };

    /// <summary>
    /// Fatigue rate multiplier (higher = tires faster).
    /// </summary>
    public static decimal GetFatigueMultiplier(this WorkerPersonality personality) => personality switch
    {
        WorkerPersonality.Steady => 1.0m,
        WorkerPersonality.Perfectionist => 1.0m,
        WorkerPersonality.Cheerful => 1.0m,
        WorkerPersonality.Ambitious => 1.25m,
        WorkerPersonality.Relaxed => 0.70m,
        WorkerPersonality.Specialist => 1.0m,
        _ => 1.0m
    };

    /// <summary>
    /// XP gain multiplier.
    /// </summary>
    public static decimal GetXpMultiplier(this WorkerPersonality personality) => personality switch
    {
        WorkerPersonality.Ambitious => 1.25m,
        _ => 1.0m
    };

    /// <summary>
    /// Specialization bonus multiplier (on top of base +15%).
    /// </summary>
    public static decimal GetSpecializationBonus(this WorkerPersonality personality) => personality switch
    {
        WorkerPersonality.Specialist => 0.15m, // Total +30% with base +15%
        _ => 0.0m
    };

    /// <summary>
    /// Localization key for personality name.
    /// </summary>
    public static string GetLocalizationKey(this WorkerPersonality personality) => $"Person{personality}";

    /// <summary>
    /// Icon/emoji for this personality.
    /// </summary>
    public static string GetIcon(this WorkerPersonality personality) => personality switch
    {
        WorkerPersonality.Steady => "\u2696\ufe0f",      // Balance scale
        WorkerPersonality.Perfectionist => "\ud83c\udfaf", // Target
        WorkerPersonality.Cheerful => "\ud83d\ude04",      // Grinning face
        WorkerPersonality.Ambitious => "\ud83d\ude80",     // Rocket
        WorkerPersonality.Relaxed => "\ud83c\udf3f",       // Herb/leaf
        WorkerPersonality.Specialist => "\ud83d\udd27",    // Wrench
        _ => "\u2696\ufe0f"
    };
}
