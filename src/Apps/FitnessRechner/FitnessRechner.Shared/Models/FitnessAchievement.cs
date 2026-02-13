using Material.Icons;

namespace FitnessRechner.Models;

/// <summary>
/// Achievement-Kategorie für Gruppierung.
/// </summary>
public enum AchievementCategory
{
    Tracking,
    Nutrition,
    Water,
    Body,
    Special
}

/// <summary>
/// Ein einzelnes Achievement/Badge im Gamification-System.
/// </summary>
public class FitnessAchievement
{
    public string Id { get; init; } = "";
    public string TitleKey { get; init; } = "";
    public string DescriptionKey { get; init; } = "";
    public MaterialIconKind Icon { get; init; } = MaterialIconKind.Star;
    public AchievementCategory Category { get; init; }
    public int TargetValue { get; init; } = 1;
    public int CurrentValue { get; set; }
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public int XpReward { get; init; } = 50;

    /// <summary>
    /// Fortschritt als Fraction (0.0-1.0).
    /// </summary>
    public double Progress => TargetValue > 0 ? Math.Min((double)CurrentValue / TargetValue, 1.0) : 0;
}
