using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Pool verfügbarer Worker zum Anheuern. Rotiert alle 4 Stunden.
    /// 1:1-Port aus dem Avalonia-Original (Models/WorkerMarketPool.cs). GeneratePool nimmt jetzt eine
    /// System.Random-Instanz statt Random.Shared. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class WorkerMarketPool
    {
        /// <summary>Aktuell zum Anheuern verfügbare Worker.</summary>
        [JsonProperty("availableWorkers")]
        public List<Worker> AvailableWorkers { get; set; } = new List<Worker>();

        /// <summary>Wann der Pool zu neuen Workern rotiert.</summary>
        [JsonProperty("nextRotation")]
        public DateTime NextRotation { get; set; } = DateTime.UtcNow.AddHours(4);

        /// <summary>Wann der Pool zuletzt rotiert wurde.</summary>
        [JsonProperty("lastRotation")]
        public DateTime LastRotation { get; set; } = DateTime.UtcNow;

        /// <summary>Ob der Gratis-Refresh für die aktuelle Rotation bereits verwendet wurde.</summary>
        [JsonProperty("freeRefreshUsedThisRotation")]
        public bool FreeRefreshUsedThisRotation { get; set; }

        /// <summary>Zeitpunkt der letzten Legendary-Sichtung im Markt (7 Tage Cooldown gegen Farming).</summary>
        [JsonProperty("lastLegendarySpawn")]
        public DateTime LastLegendarySpawn { get; set; } = DateTime.MinValue;

        /// <summary>Verbleibende Zeit bis zur nächsten Rotation.</summary>
        [JsonIgnore]
        public TimeSpan TimeUntilRotation
        {
            get
            {
                var remaining = NextRotation - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        /// <summary>Ob der Pool eine Rotation braucht.</summary>
        [JsonIgnore]
        public bool NeedsRotation => DateTime.UtcNow >= NextRotation;

        /// <summary>
        /// Generiert einen neuen Worker-Pool basierend auf dem Spieler-Fortschritt.
        /// <paramref name="rng"/> liefert die Tier-Verteilung (ersetzt Random.Shared des Originals).
        /// </summary>
        public void GeneratePool(int playerLevel, int prestigeLevel, Random rng, bool hasHeadhunter = false, bool hasSTierResearch = false)
        {
            AvailableWorkers.Clear();
            LastRotation = DateTime.UtcNow;
            NextRotation = DateTime.UtcNow.AddHours(4);
            FreeRefreshUsedThisRotation = false;

            int poolSize = hasHeadhunter ? 8 : 5;
            var availableTiers = Worker.GetAvailableTiers(playerLevel, prestigeLevel, hasSTierResearch);
            if (availableTiers.Count == 0) return;

            // Legendary-Cooldown — 7 Tage nach letzter Sichtung kein Legendary mehr im Pool.
            var legendaryOnCooldown = LastLegendarySpawn != DateTime.MinValue
                && DateTime.UtcNow - LastLegendarySpawn < TimeSpan.FromDays(7);
            var effectiveTiers = legendaryOnCooldown
                ? availableTiers.Where(t => t != WorkerTier.Legendary).ToList()
                : availableTiers;
            if (effectiveTiers.Count == 0) effectiveTiers = availableTiers;

            for (int i = 0; i < poolSize; i++)
            {
                // Weighted tier distribution: higher tiers are rarer
                var tier = GetWeightedTier(effectiveTiers, rng);
                AvailableWorkers.Add(Worker.CreateForTier(tier));
                if (tier == WorkerTier.Legendary)
                    LastLegendarySpawn = DateTime.UtcNow;
            }
        }

        /// <summary>Entfernt einen Worker aus dem Pool (nach dem Anheuern).</summary>
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
            // Höhere Tiers sind exponentiell seltener.
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

            return available[available.Count - 1];
        }
    }
}
