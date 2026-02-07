using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Tracks customer reputation which affects order quality and frequency.
/// </summary>
public class CustomerReputation
{
    /// <summary>
    /// Reputation score (0-100). Starts at 50.
    /// </summary>
    [JsonPropertyName("reputationScore")]
    public int ReputationScore { get; set; } = 50;

    /// <summary>
    /// Recent order ratings (last 50).
    /// </summary>
    [JsonPropertyName("recentRatings")]
    public List<int> RecentRatings { get; set; } = [];

    /// <summary>
    /// Regular/loyal customers.
    /// </summary>
    [JsonPropertyName("regularCustomers")]
    public List<RegularCustomer> RegularCustomers { get; set; } = [];

    /// <summary>
    /// Income multiplier based on reputation.
    /// </summary>
    [JsonIgnore]
    public decimal ReputationMultiplier => ReputationScore switch
    {
        < 30 => 0.7m,
        < 60 => 1.0m,
        < 80 => 1.2m,
        _ => 1.5m
    };

    /// <summary>
    /// Reputation level name key for localization.
    /// </summary>
    [JsonIgnore]
    public string ReputationLevelKey => ReputationScore switch
    {
        < 30 => "ReputationPoor",
        < 60 => "ReputationAverage",
        < 80 => "ReputationGood",
        < 90 => "ReputationExcellent",
        _ => "ReputationLegendary"
    };

    /// <summary>
    /// Adds a rating (1-5 stars) from a completed order.
    /// </summary>
    public void AddRating(int stars)
    {
        stars = Math.Clamp(stars, 1, 5);
        RecentRatings.Add(stars);
        if (RecentRatings.Count > 50)
            RecentRatings.RemoveAt(0);

        // Adjust reputation based on rating
        int delta = stars switch
        {
            5 => 3,
            4 => 1,
            3 => 0,
            2 => -2,
            _ => -5
        };

        ReputationScore = Math.Clamp(ReputationScore + delta, 0, 100);
    }

    /// <summary>
    /// Reputation decays slowly when no orders are completed.
    /// Call once per day.
    /// </summary>
    public void DecayReputation()
    {
        if (ReputationScore > 50)
            ReputationScore = Math.Max(50, ReputationScore - 1);
    }
}
