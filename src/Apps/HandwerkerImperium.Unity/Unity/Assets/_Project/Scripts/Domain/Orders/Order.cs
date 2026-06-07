#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using HandwerkerImperium.Domain.Economy;

namespace HandwerkerImperium.Domain.Orders
{
    /// <summary>
    /// Repräsentiert einen Auftrag/Vertrag, den Spieler für Belohnungen abschließen.
    /// Aufträge haben Typen, optionale Deadlines und können von Stammkunden kommen.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Order.cs). Reine Spiellogik. Das Original
    /// implementiert <c>INotifyPropertyChanged</c> für einen UI-Live-Countdown — diese UI-Bindung
    /// (samt LiveCountdownText + Display-Feldern) lebt in der Unity-Präsentationsschicht; hier bleibt
    /// die Logik (Reward/XP-Berechnung, Ablauf, Pause). Persistenz: Newtonsoft.Json.
    /// </summary>
    public class Order
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("titleKey")]
        public string TitleKey { get; set; } = string.Empty;

        [JsonProperty("titleFallback")]
        public string TitleFallback { get; set; } = string.Empty;

        [JsonProperty("workshopType")]
        public WorkshopType WorkshopType { get; set; }

        [JsonProperty("difficulty")]
        public OrderDifficulty Difficulty { get; set; } = OrderDifficulty.Medium;

        /// <summary>Auftragstyp (Quick, Standard, Large, Weekly, Cooperation, MaterialOrder).</summary>
        [JsonProperty("orderType")]
        public OrderType OrderType { get; set; } = OrderType.Standard;

        /// <summary>
        /// Spieler-gewählte Strategie (Safe/Standard/Risk). Wirkt auf MiniGame-Schwierigkeit +
        /// Reward-Multiplikator + Miss-Handling. Default Standard.
        /// </summary>
        [JsonProperty("strategy")]
        public OrderStrategy Strategy { get; set; } = OrderStrategy.Standard;

        /// <summary>
        /// Ob der Auftrag durch einen Miss unter <see cref="OrderStrategy.Risk"/> komplett gescheitert ist.
        /// Wenn true: <see cref="FinalReward"/> = 0, Reputation-Penalty wurde angewendet.
        /// </summary>
        [JsonProperty("hasHardFailed")]
        public bool HasHardFailed { get; set; }

        /// <summary>Ob dieser Auftrag live generiert wurde (hat <see cref="ExpiresAt"/>, verschwindet bei Ablauf).</summary>
        [JsonProperty("isLive")]
        public bool IsLive { get; set; }

        /// <summary>Ob dieser Auftrag ein seltener Premium/VIP-Auftrag ist (3x Reward, kürzere Deadlines).</summary>
        [JsonProperty("isPremium")]
        public bool IsPremium { get; set; }

        [JsonProperty("tasks")]
        public List<OrderTask> Tasks { get; set; } = new List<OrderTask>();

        [JsonProperty("baseReward")]
        public decimal BaseReward { get; set; }

        [JsonProperty("baseXp")]
        public int BaseXp { get; set; }

        [JsonProperty("requiredLevel")]
        public int RequiredLevel { get; set; } = 1;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("expiresAt")]
        public DateTime? ExpiresAt { get; set; }

        /// <summary>Deadline für zeitlich begrenzte Aufträge (Weekly).</summary>
        [JsonProperty("deadline")]
        public DateTime? Deadline { get; set; }

        /// <summary>
        /// Zeitpunkt zu dem die Order pausiert wurde (App im Hintergrund). Null = nicht pausiert.
        /// Beim Resume wird die Differenz auf <see cref="AccumulatedPauseDuration"/> addiert.
        /// </summary>
        [JsonProperty("pausedAt")]
        public DateTime? PausedAt { get; set; }

        /// <summary>
        /// Akkumulierte Pause-Dauer. Wird zur ExpiresAt-Prüfung addiert, damit Live-Orders nicht
        /// während Hintergrund-Sessions ablaufen. Cap: 5 Minuten.
        /// </summary>
        [JsonProperty("accumulatedPauseDuration")]
        public TimeSpan AccumulatedPauseDuration { get; set; }

        /// <summary>Kunden-ID falls dieser Auftrag von einem Stammkunden ist.</summary>
        [JsonProperty("customerId")]
        public string? CustomerId { get; set; }

        /// <summary>Generierter Kundenname für die Anzeige.</summary>
        [JsonProperty("customerName")]
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>Seed für die Kunden-Avatar-Generierung.</summary>
        [JsonProperty("customerAvatarSeed")]
        public string CustomerAvatarSeed { get; set; } = Guid.NewGuid().ToString().Substring(0, 8);

        /// <summary>Benötigte Workshop-Typen für Cooperation-Aufträge.</summary>
        [JsonProperty("requiredWorkshops")]
        public List<WorkshopType>? RequiredWorkshops { get; set; }

        /// <summary>
        /// Benötigte Materialien für Lieferaufträge (MaterialOrder). Key = Produkt-ID, Value = Menge.
        /// </summary>
        [JsonProperty("requiredMaterials")]
        public Dictionary<string, int>? RequiredMaterials { get; set; }

        /// <summary>
        /// Optionales Material-Angebot (NICHT Pflicht). Der Spieler kann den Auftrag auch OHNE
        /// Material annehmen (normales Reward) ODER mit Material für einen Bonus-Multiplikator.
        /// Key = Produkt-ID, Value = Menge.
        /// </summary>
        [JsonProperty("materialOffer")]
        public Dictionary<string, int>? MaterialOffer { get; set; }

        /// <summary>
        /// Bonus-Reward-Multiplikator bei akzeptiertem Material-Angebot. 0.0 = kein Offer.
        /// </summary>
        [JsonProperty("materialOfferBonusMultiplier")]
        public double MaterialOfferBonusMultiplier { get; set; }

        /// <summary>True wenn der Spieler beim Annehmen entschieden hat, Materialien zu liefern.</summary>
        [JsonProperty("materialOfferAccepted")]
        public bool MaterialOfferAccepted { get; set; }

        /// <summary>True wenn dieser Auftrag ein Material-Angebot hat.</summary>
        [JsonIgnore]
        public bool HasMaterialOffer => MaterialOffer is { Count: > 0 };

        /// <summary>Reputation-Bonus/-Malus bei Abschluss.</summary>
        [JsonProperty("reputationBonus")]
        public decimal ReputationBonus { get; set; }

        /// <summary>Ob die Belohnung durch Rewarded Ad verdoppelt wurde.</summary>
        [JsonProperty("isScoreDoubled")]
        public bool IsScoreDoubled { get; set; }

        /// <summary>Combo-Multiplikator aus PaintingGame (1.0 = kein Combo).</summary>
        [JsonProperty("comboMultiplier")]
        public decimal ComboMultiplier { get; set; } = 1m;

        [JsonProperty("currentTaskIndex")]
        public int CurrentTaskIndex { get; set; }

        [JsonProperty("taskResults")]
        public List<MiniGameRating> TaskResults { get; set; } = new List<MiniGameRating>();

        [JsonIgnore]
        public bool IsCompleted => CurrentTaskIndex >= Tasks.Count;

        [JsonIgnore]
        public OrderTask? CurrentTask => CurrentTaskIndex < Tasks.Count ? Tasks[CurrentTaskIndex] : null;

        /// <summary>
        /// Ob dieser Auftrag eine Deadline hat und sie überschritten ist.
        /// Berücksichtigt <see cref="ExpiresAt"/> + <see cref="AccumulatedPauseDuration"/>.
        /// </summary>
        [JsonIgnore]
        public bool IsExpired =>
            (Deadline != null && DateTime.UtcNow > Deadline) ||
            (ExpiresAt != null && GetEffectiveNow() > ExpiresAt);

        /// <summary>
        /// "Effektive Jetzt-Zeit" für Live-Order-Ablauf — zieht akkumulierte Pause-Dauer ab,
        /// sodass Hintergrund-Zeit nicht zählt. Cap 5 Minuten.
        /// </summary>
        private DateTime GetEffectiveNow()
        {
            var now = DateTime.UtcNow;
            var pauseTotal = AccumulatedPauseDuration;
            if (PausedAt.HasValue)
            {
                var currentPause = now - PausedAt.Value;
                if (currentPause < TimeSpan.Zero) currentPause = TimeSpan.Zero;
                pauseTotal += currentPause;
            }
            if (pauseTotal > TimeSpan.FromMinutes(5)) pauseTotal = TimeSpan.FromMinutes(5);
            return now - pauseTotal;
        }

        /// <summary>Verbleibende Sekunden bis <see cref="ExpiresAt"/>. Null wenn kein Live-Auftrag.</summary>
        [JsonIgnore]
        public double? LiveCountdownSeconds
        {
            get
            {
                if (!IsLive || !ExpiresAt.HasValue) return null;
                var remaining = (ExpiresAt.Value - GetEffectiveNow()).TotalSeconds;
                return remaining < 0 ? 0 : remaining;
            }
        }

        /// <summary>Ob dieser Auftrag von einem Stammkunden ist.</summary>
        [JsonIgnore]
        public bool IsRegularCustomerOrder => CustomerId != null;

        /// <summary>Finale Belohnung (Durchschnitts-Rating × alle Multiplikatoren). 0 bei Hard-Fail.</summary>
        [JsonIgnore]
        public decimal FinalReward
        {
            get
            {
                if (HasHardFailed) return 0;        // Risk-Strategy Hard-Fail: 0 Reward
                if (TaskResults.Count == 0) return 0;
                decimal avgPercentage = TaskResults.Average(r => r.GetRewardPercentage());
                return BaseReward * avgPercentage * Difficulty.GetRewardMultiplier() * OrderType.GetRewardMultiplier() * Strategy.GetRewardMultiplier();
            }
        }

        /// <summary>Finale XP (Durchschnitts-Rating × alle Multiplikatoren). 0 bei Hard-Fail.</summary>
        [JsonIgnore]
        public int FinalXp
        {
            get
            {
                if (HasHardFailed) return 0;        // Risk-Strategy Hard-Fail: 0 XP
                if (TaskResults.Count == 0) return 0;
                decimal avgPercentage = TaskResults.Average(r => r.GetXpPercentage());
                return (int)(BaseXp * avgPercentage * Difficulty.GetXpMultiplier() * OrderType.GetXpMultiplier() * Strategy.GetXpMultiplier());
            }
        }

        /// <summary>
        /// Geschätzte Belohnung unter Einbeziehung aller Multiplikatoren (Referenz: "Good"-Rating 100%).
        /// </summary>
        public decimal CalculateEstimatedReward()
        {
            return BaseReward * Difficulty.GetRewardMultiplier() * OrderType.GetRewardMultiplier() * Strategy.GetRewardMultiplier();
        }

        /// <summary>
        /// Geschätzte XP unter Einbeziehung aller Multiplikatoren (Referenz: "Good"-Rating 100%).
        /// </summary>
        public int CalculateEstimatedXp()
        {
            return (int)(BaseXp * Difficulty.GetXpMultiplier() * OrderType.GetXpMultiplier() * Strategy.GetXpMultiplier());
        }

        /// <summary>Geschätzte Belohnung inkl. Difficulty + OrderType (für Dashboard-Binding).</summary>
        [JsonIgnore]
        public decimal EstimatedReward => CalculateEstimatedReward();

        /// <summary>Geschätzte XP inkl. Difficulty + OrderType (für Dashboard-Binding).</summary>
        [JsonIgnore]
        public int EstimatedXp => CalculateEstimatedXp();

        public void RecordTaskResult(MiniGameRating rating)
        {
            TaskResults.Add(rating);
            CurrentTaskIndex++;
        }
    }

    /// <summary>Ein einzelner Task innerhalb eines Auftrags.</summary>
    public class OrderTask
    {
        [JsonProperty("gameType")]
        public MiniGameType GameType { get; set; }

        [JsonProperty("descriptionKey")]
        public string DescriptionKey { get; set; } = string.Empty;

        [JsonProperty("descriptionFallback")]
        public string DescriptionFallback { get; set; } = string.Empty;
    }
}
