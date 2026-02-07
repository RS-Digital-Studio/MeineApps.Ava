using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Pool of available workers for hire. Rotates every 4 hours.
/// </summary>
public class WorkerMarketPool
{
    /// <summary>
    /// Workers currently available for hire.
    /// </summary>
    [JsonPropertyName("availableWorkers")]
    public List<Worker> AvailableWorkers { get; set; } = [];

    /// <summary>
    /// When the pool will rotate to new workers.
    /// </summary>
    [JsonPropertyName("nextRotation")]
    public DateTime NextRotation { get; set; } = DateTime.UtcNow.AddHours(4);

    /// <summary>
    /// When the pool was last rotated.
    /// </summary>
    [JsonPropertyName("lastRotation")]
    public DateTime LastRotation { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Time remaining until next rotation.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TimeUntilRotation
    {
        get
        {
            var remaining = NextRotation - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Whether the pool needs rotation.
    /// </summary>
    [JsonIgnore]
    public bool NeedsRotation => DateTime.UtcNow >= NextRotation;

    /// <summary>
    /// Generates a new pool of workers based on player progression.
    /// </summary>
    public void GeneratePool(int playerLevel, int prestigeLevel, bool hasHeadhunter = false)
    {
        AvailableWorkers.Clear();
        LastRotation = DateTime.UtcNow;
        NextRotation = DateTime.UtcNow.AddHours(4);

        int poolSize = hasHeadhunter ? 8 : 5;
        var availableTiers = Worker.GetAvailableTiers(playerLevel, prestigeLevel);
        if (availableTiers.Count == 0) return;

        var random = new Random();
        for (int i = 0; i < poolSize; i++)
        {
            // Weighted tier distribution: higher tiers are rarer
            var tier = GetWeightedTier(availableTiers, random);
            AvailableWorkers.Add(Worker.CreateForTier(tier));
        }
    }

    /// <summary>
    /// Removes a worker from the pool (after hiring).
    /// </summary>
    public bool RemoveWorker(string workerId)
    {
        var worker = AvailableWorkers.FirstOrDefault(w => w.Id == workerId);
        if (worker == null) return false;
        AvailableWorkers.Remove(worker);
        return true;
    }

    private static WorkerTier GetWeightedTier(List<WorkerTier> available, Random random)
    {
        // Higher tiers are exponentially rarer
        var weights = new Dictionary<WorkerTier, double>
        {
            [WorkerTier.F] = 30.0,
            [WorkerTier.E] = 25.0,
            [WorkerTier.D] = 18.0,
            [WorkerTier.C] = 12.0,
            [WorkerTier.B] = 8.0,
            [WorkerTier.A] = 5.0,
            [WorkerTier.S] = 2.0
        };

        double totalWeight = 0;
        foreach (var tier in available)
        {
            if (weights.TryGetValue(tier, out var w))
                totalWeight += w;
        }

        double roll = random.NextDouble() * totalWeight;
        double cumulative = 0;
        foreach (var tier in available)
        {
            if (weights.TryGetValue(tier, out var w))
            {
                cumulative += w;
                if (roll <= cumulative) return tier;
            }
        }

        return available[^1];
    }
}
