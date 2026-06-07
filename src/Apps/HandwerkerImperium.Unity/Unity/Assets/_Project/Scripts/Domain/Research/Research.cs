using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Research
{
    /// <summary>
    /// Ein einzelner Forschungs-Knoten im Skill-Tree.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Research.cs). Datenklasse + reine
    /// Zeit-/Fortschritts-Logik (RemainingTime/Progress/InstantFinishScrewCost). Persistenz: Newtonsoft.Json.
    /// </summary>
    public class Research
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("branch")]
        public ResearchBranch Branch { get; set; }

        /// <summary>Level innerhalb des Branch (1-20).</summary>
        [JsonProperty("level")]
        public int Level { get; set; }

        [JsonProperty("nameKey")]
        public string NameKey { get; set; } = string.Empty;

        [JsonProperty("descriptionKey")]
        public string DescriptionKey { get; set; } = string.Empty;

        [JsonProperty("cost")]
        public decimal Cost { get; set; }

        /// <summary>Echtzeit-Dauer bis zum Abschluss der Forschung.</summary>
        [JsonProperty("durationTicks")]
        public long DurationTicks { get; set; }

        [JsonIgnore]
        public TimeSpan Duration => TimeSpan.FromTicks(DurationTicks);

        [JsonProperty("isResearched")]
        public bool IsResearched { get; set; }

        [JsonProperty("isActive")]
        public bool IsActive { get; set; }

        [JsonProperty("startedAt")]
        public DateTime? StartedAt { get; set; }

        [JsonProperty("completedAt")]
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Kumulierte Bonus-Sekunden durch InnovationLab (wird in UpdateTimer berücksichtigt).
        /// Vermeidet direkte Manipulation von StartedAt.
        /// </summary>
        [JsonProperty("bonusSeconds")]
        public double BonusSeconds { get; set; }

        /// <summary>
        /// Effektive Dauer inkl. Gilden-Forschungs-Bonus (transient, vom ResearchService gesetzt).
        /// Wird für RemainingTime/Progress verwendet statt Duration.
        /// </summary>
        [JsonIgnore]
        public TimeSpan? EffectiveDuration { get; set; }

        [JsonProperty("effect")]
        public ResearchEffect Effect { get; set; } = new ResearchEffect();

        /// <summary>IDs der Voraussetzungs-Knoten.</summary>
        [JsonProperty("prerequisites")]
        public List<string> Prerequisites { get; set; } = new List<string>();

        /// <summary>
        /// Verbleibende Zeit bis Abschluss (berücksichtigt BonusSeconds + GuildSpeedBonus).
        /// Verwendet EffectiveDuration wenn vom ResearchService gesetzt, sonst Basis-Duration.
        /// </summary>
        [JsonIgnore]
        public TimeSpan? RemainingTime
        {
            get
            {
                if (!IsActive || StartedAt == null) return null;
                var elapsed = DateTime.UtcNow - StartedAt.Value + TimeSpan.FromSeconds(BonusSeconds);
                var duration = EffectiveDuration ?? Duration;
                var remaining = duration - elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        /// <summary>Goldschrauben-Kosten für Sofortfertigstellung (ab Level 8).</summary>
        [JsonIgnore]
        public int InstantFinishScrewCost => Level switch
        {
            >= 20 => 500,
            >= 19 => 400,
            >= 18 => 320,
            >= 17 => 260,
            >= 16 => 220,
            >= 15 => 180,
            >= 14 => 150,
            >= 13 => 120,
            >= 12 => 90,
            >= 11 => 60,
            >= 10 => 40,
            >= 9 => 25,
            >= 8 => 15,
            _ => 0
        };

        /// <summary>Kann mit Goldschrauben sofort abgeschlossen werden (nur ab Level 8).</summary>
        [JsonIgnore]
        public bool CanInstantFinish => IsActive && InstantFinishScrewCost > 0;

        /// <summary>
        /// Fortschritt in Prozent (0-100, berücksichtigt BonusSeconds + GuildSpeedBonus).
        /// </summary>
        [JsonIgnore]
        public double Progress
        {
            get
            {
                if (IsResearched) return 100.0;
                if (!IsActive || StartedAt == null) return 0.0;
                var elapsed = DateTime.UtcNow - StartedAt.Value + TimeSpan.FromSeconds(BonusSeconds);
                var duration = EffectiveDuration ?? Duration;
                return Math.Clamp(elapsed / duration * 100.0, 0.0, 100.0);
            }
        }
    }
}
