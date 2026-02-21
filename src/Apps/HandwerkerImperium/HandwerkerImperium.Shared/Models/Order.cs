using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Represents a contract/order that players can complete for rewards.
/// Orders have types, optional deadlines, and can come from regular customers.
/// </summary>
public class Order
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("titleKey")]
    public string TitleKey { get; set; } = string.Empty;

    [JsonPropertyName("titleFallback")]
    public string TitleFallback { get; set; } = string.Empty;

    [JsonPropertyName("workshopType")]
    public WorkshopType WorkshopType { get; set; }

    [JsonPropertyName("difficulty")]
    public OrderDifficulty Difficulty { get; set; } = OrderDifficulty.Medium;

    /// <summary>
    /// Type of order (Quick, Standard, Large, Weekly, Cooperation).
    /// </summary>
    [JsonPropertyName("orderType")]
    public OrderType OrderType { get; set; } = OrderType.Standard;

    [JsonPropertyName("tasks")]
    public List<OrderTask> Tasks { get; set; } = [];

    [JsonPropertyName("baseReward")]
    public decimal BaseReward { get; set; }

    [JsonPropertyName("baseXp")]
    public int BaseXp { get; set; }

    [JsonPropertyName("requiredLevel")]
    public int RequiredLevel { get; set; } = 1;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Deadline for timed orders (Weekly).
    /// </summary>
    [JsonPropertyName("deadline")]
    public DateTime? Deadline { get; set; }

    /// <summary>
    /// Customer ID if this order is from a regular customer.
    /// </summary>
    [JsonPropertyName("customerId")]
    public string? CustomerId { get; set; }

    /// <summary>
    /// Generated customer name for display.
    /// </summary>
    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Seed for customer avatar generation.
    /// </summary>
    [JsonPropertyName("customerAvatarSeed")]
    public string CustomerAvatarSeed { get; set; } = Guid.NewGuid().ToString()[..8];

    /// <summary>
    /// Required workshop types for Cooperation orders.
    /// </summary>
    [JsonPropertyName("requiredWorkshops")]
    public List<WorkshopType>? RequiredWorkshops { get; set; }

    /// <summary>
    /// Reputation bonus/penalty on completion.
    /// </summary>
    [JsonPropertyName("reputationBonus")]
    public decimal ReputationBonus { get; set; }

    [JsonPropertyName("currentTaskIndex")]
    public int CurrentTaskIndex { get; set; }

    [JsonPropertyName("taskResults")]
    public List<MiniGameRating> TaskResults { get; set; } = [];

    [JsonIgnore]
    public string DisplayTitle { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayWorkshopName { get; set; } = string.Empty;

    /// <summary>
    /// Lokalisierter Auftragstyp-Name (z.B. "Großauftrag", "Wochenauftrag").
    /// </summary>
    [JsonIgnore]
    public string DisplayOrderType { get; set; } = string.Empty;

    /// <summary>
    /// Icon für den Auftragstyp (z.B. ⚡, 📦, 📅, 🤝).
    /// </summary>
    [JsonIgnore]
    public string OrderTypeIcon { get; set; } = string.Empty;

    /// <summary>
    /// Badge-Farbe für den Auftragstyp (Large=Orange, Weekly=Gold, Cooperation=Teal, Standard/Quick=leer).
    /// </summary>
    [JsonIgnore]
    public string OrderTypeBadgeColor { get; set; } = string.Empty;

    /// <summary>
    /// Ob das Auftragstyp-Badge angezeigt werden soll (nur bei Large, Weekly, Cooperation).
    /// </summary>
    [JsonIgnore]
    public bool ShowOrderTypeBadge { get; set; }

    [JsonIgnore]
    public bool IsCompleted => CurrentTaskIndex >= Tasks.Count;

    [JsonIgnore]
    public OrderTask? CurrentTask => CurrentTaskIndex < Tasks.Count ? Tasks[CurrentTaskIndex] : null;

    /// <summary>
    /// Whether this order has a deadline and it has passed.
    /// </summary>
    [JsonIgnore]
    public bool IsExpired => Deadline != null && DateTime.UtcNow > Deadline;

    /// <summary>
    /// Whether this is from a regular customer.
    /// </summary>
    [JsonIgnore]
    public bool IsRegularCustomerOrder => CustomerId != null;

    [JsonIgnore]
    public decimal FinalReward
    {
        get
        {
            if (TaskResults.Count == 0) return 0;
            decimal avgPercentage = TaskResults.Average(r => r.GetRewardPercentage());
            return BaseReward * avgPercentage * Difficulty.GetRewardMultiplier() * OrderType.GetRewardMultiplier();
        }
    }

    [JsonIgnore]
    public int FinalXp
    {
        get
        {
            if (TaskResults.Count == 0) return 0;
            decimal avgPercentage = TaskResults.Average(r => r.GetXpPercentage());
            return (int)(BaseXp * avgPercentage * Difficulty.GetXpMultiplier() * OrderType.GetXpMultiplier());
        }
    }

    /// <summary>
    /// Berechnet die geschätzte Belohnung unter Einbeziehung aller Multiplikatoren.
    /// Geht von "Good"-Rating (100%) aus als Referenzwert.
    /// </summary>
    public decimal CalculateEstimatedReward()
    {
        return BaseReward * Difficulty.GetRewardMultiplier() * OrderType.GetRewardMultiplier();
    }

    /// <summary>
    /// Berechnet die geschätzte XP-Belohnung unter Einbeziehung aller Multiplikatoren.
    /// Geht von "Good"-Rating (100%) aus als Referenzwert.
    /// </summary>
    public int CalculateEstimatedXp()
    {
        return (int)(BaseXp * Difficulty.GetXpMultiplier() * OrderType.GetXpMultiplier());
    }

    public void RecordTaskResult(MiniGameRating rating)
    {
        TaskResults.Add(rating);
        CurrentTaskIndex++;
    }
}

/// <summary>
/// A single task within an order.
/// </summary>
public class OrderTask
{
    [JsonPropertyName("gameType")]
    public MiniGameType GameType { get; set; }

    [JsonPropertyName("descriptionKey")]
    public string DescriptionKey { get; set; } = string.Empty;

    [JsonPropertyName("descriptionFallback")]
    public string DescriptionFallback { get; set; } = string.Empty;
}
