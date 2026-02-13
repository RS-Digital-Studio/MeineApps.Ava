using Material.Icons;

namespace FitnessRechner.Models;

/// <summary>
/// Tägliche Herausforderung im Gamification-System.
/// Wechselt täglich basierend auf DayOfYear.
/// </summary>
public class DailyChallenge
{
    public string Id { get; init; } = "";
    public string TitleKey { get; init; } = "";
    public string DescriptionKey { get; init; } = "";
    public MaterialIconKind Icon { get; init; } = MaterialIconKind.Star;
    public int TargetValue { get; init; } = 1;
    public int CurrentValue { get; set; }
    public bool IsCompleted => CurrentValue >= TargetValue;
    public int XpReward { get; init; } = 25;

    /// <summary>
    /// Fortschritt als Fraction (0.0-1.0).
    /// </summary>
    public double Progress => TargetValue > 0 ? Math.Min((double)CurrentValue / TargetValue, 1.0) : 0;
}
