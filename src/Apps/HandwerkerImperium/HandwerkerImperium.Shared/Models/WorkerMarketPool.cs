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
    /// Ob der Gratis-Refresh für die aktuelle Rotation bereits verwendet wurde.
    /// Wird bei jeder Rotation zurückgesetzt.
    /// </summary>
    [JsonPropertyName("freeRefreshUsedThisRotation")]
    public bool FreeRefreshUsedThisRotation { get; set; }

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
    public void GeneratePool(int playerLevel, int prestigeLevel, bool hasHeadhunter = false, bool hasSTierResearch = false)
    {
        AvailableWorkers.Clear();
        LastRotation = DateTime.UtcNow;
        NextRotation = DateTime.UtcNow.AddHours(4);
        FreeRefreshUsedThisRotation = false;

        int poolSize = hasHeadhunter ? 8 : 5;
        var availableTiers = Worker.GetAvailableTiers(playerLevel, prestigeLevel, hasSTierResearch);
        if (availableTiers.Count == 0) return;

        var random = Random.Shared;
        for (int i = 0; i < poolSize; i++)
        {
            // Weighted tier distribution: higher tiers are rarer
            var tier = GetWeightedTier(availableTiers, random);
            AvailableWorkers.Add(Worker.CreateForTier(tier));
        }
    }

    /// <summary>
    /// Removes a worker from the pool (after hiring). For-Schleife mit RemoveAt (keine doppelte Iteration).
    /// </summary>
    public bool RemoveWorker(string workerId)
    {
        for (int i = 0; i < AvailableWorkers.Count; i++)
        {
            if (AvailableWorkers[i].Id == workerId)
            {
                AvailableWorkers.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    private static WorkerTier GetWeightedTier(List<WorkerTier> available, Random random)
    {
        // Higher tiers are exponentially rarer
        // F/E von 55% auf 42% gesenkt, D von 18% auf 22% erhöht
        // Spieler sieht öfter D-Tier Worker als erstes spürbares Upgrade
        var weights = new Dictionary<WorkerTier, double>
        {
            [WorkerTier.F] = 20.0,
            [WorkerTier.E] = 22.0,
            [WorkerTier.D] = 22.0,
            [WorkerTier.C] = 14.0,
            [WorkerTier.B] = 10.0,
            [WorkerTier.A] = 6.0,
            [WorkerTier.S] = 3.0,
            [WorkerTier.SS] = 1.5,
            [WorkerTier.SSS] = 0.5,
            [WorkerTier.Legendary] = 0.1
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
