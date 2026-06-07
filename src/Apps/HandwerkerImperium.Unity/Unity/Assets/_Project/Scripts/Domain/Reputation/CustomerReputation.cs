using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Reputation
{
    /// <summary>
    /// Verfolgt die Kunden-Reputation, die Auftragsqualität und -häufigkeit beeinflusst.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/CustomerReputation.cs). Datenklasse + reine
    /// Logik (Rating, Decay, Tier-Hysterese). Die UI-Property ReputationLevelKey (Lokalisierungs-Key)
    /// wandert in die Präsentationsschicht. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class CustomerReputation
    {
        /// <summary>Reputation-Score (0-100). Start bei 50.</summary>
        [JsonProperty("reputationScore")]
        public int ReputationScore { get; set; } = 50;

        /// <summary>Letzte Auftrags-Bewertungen (max 50).</summary>
        [JsonProperty("recentRatings")]
        public List<int> RecentRatings { get; set; } = new List<int>();

        /// <summary>Stammkunden / treue Kunden.</summary>
        [JsonProperty("regularCustomers")]
        public List<RegularCustomer> RegularCustomers { get; set; } = new List<RegularCustomer>();

        /// <summary>Einkommens-Multiplikator basierend auf Reputation.</summary>
        [JsonIgnore]
        public decimal ReputationMultiplier => ReputationScore switch
        {
            < 30 => 0.7m,
            < 60 => 1.0m,
            < 80 => 1.2m,
            _ => 1.5m
        };

        /// <summary>
        /// Letzter berechneter Tier-Stand. Persistiert damit Hysterese über App-Restarts
        /// konsistent bleibt. Default Beginner — wird beim ersten RecomputeTier-Call gesetzt.
        /// </summary>
        [JsonProperty("currentTier")]
        public CustomerReputationTier CurrentTier { get; set; } = CustomerReputationTier.Beginner;

        /// <summary>
        /// Berechnet den Tier mit Hysterese und aktualisiert <see cref="CurrentTier"/> bei Änderung.
        /// Liefert true wenn ein Tier-Wechsel stattgefunden hat (Caller kann TierChanged-Event auslösen).
        /// </summary>
        public bool RecomputeTier(out CustomerReputationTier oldTier)
        {
            oldTier = CurrentTier;
            var newTier = CustomerReputationTierExtensions.FromScoreWithHysteresis(ReputationScore, CurrentTier);
            if (newTier != oldTier)
            {
                CurrentTier = newTier;
                return true;
            }
            return false;
        }

        /// <summary>Fügt eine Bewertung (1-5 Sterne) aus einem abgeschlossenen Auftrag hinzu.</summary>
        public void AddRating(int stars, decimal researchReputationBonus = 0m)
        {
            stars = Math.Clamp(stars, 1, 5);
            RecentRatings.Add(stars);
            if (RecentRatings.Count > 50)
                RecentRatings.RemoveAt(0);

            // Reputation anhand der Bewertung anpassen
            int delta = stars switch
            {
                5 => 3,
                4 => 1,
                3 => 0,
                2 => -2,
                _ => -5
            };

            // Research ReputationBonus: Positive Änderungen verstärken
            if (delta > 0 && researchReputationBonus > 0)
                delta = (int)Math.Ceiling(delta * (1m + researchReputationBonus));

            ReputationScore = Math.Clamp(ReputationScore + delta, 0, 100);
        }

        /// <summary>Extra Order-Slots basierend auf Reputation (gute Reputation = mehr Aufträge).</summary>
        [JsonIgnore]
        public int ExtraOrderSlots => ReputationScore switch
        {
            >= 90 => 2,
            >= 70 => 1,
            _ => 0
        };

        /// <summary>
        /// Order-Qualitäts-Bonus: Höhere Reputation senkt Standard-Wahrscheinlichkeit.
        /// Negativ bei schlechter Reputation → mehr Standard-Orders.
        /// </summary>
        [JsonIgnore]
        public decimal OrderQualityBonus => ReputationScore switch
        {
            < 30 => -0.10m,
            < 60 => 0m,
            < 80 => 0.10m,
            _ => 0.20m
        };

        /// <summary>Reputation verfällt langsam wenn keine Aufträge abgeschlossen werden. Einmal pro Tag aufrufen.</summary>
        public void DecayReputation()
        {
            if (ReputationScore > 50)
                ReputationScore = Math.Max(50, ReputationScore - 1);
        }
    }
}
