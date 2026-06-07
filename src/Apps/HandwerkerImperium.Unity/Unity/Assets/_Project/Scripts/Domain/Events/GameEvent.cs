using System;
using Newtonsoft.Json;
using HandwerkerImperium.Domain.Economy;

namespace HandwerkerImperium.Domain.Events
{
    /// <summary>
    /// Ein zufälliges oder saisonales Event, das Spielparameter temporär modifiziert.
    /// 1:1-Port aus dem Avalonia-Original (Models/GameEvent.cs). GameEventType-Enum + Effekt sind in
    /// Schicht 10/11. NameKey/DescriptionKey/Icon (Lokalisierung) wandern in die Präsentationsschicht.
    /// Persistenz: Newtonsoft.Json.
    /// </summary>
    public class GameEvent
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("type")]
        public GameEventType Type { get; set; }

        [JsonProperty("startedAt")]
        public DateTime StartedAt { get; set; }

        [JsonProperty("durationTicks")]
        public long DurationTicks { get; set; }

        [JsonIgnore]
        public TimeSpan Duration => TimeSpan.FromTicks(DurationTicks);

        [JsonIgnore]
        public bool IsActive => DateTime.UtcNow < StartedAt + Duration;

        [JsonIgnore]
        public TimeSpan RemainingTime
        {
            get
            {
                var remaining = StartedAt + Duration - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        [JsonProperty("effect")]
        public GameEventEffect Effect { get; set; } = new GameEventEffect();

        /// <summary>
        /// Erzeugt ein Event mit Default-Effekt. <paramref name="rng"/> wählt den betroffenen Workshop
        /// bei HighDemand/MaterialShortage (ersetzt Random.Shared des Originals; deterministisch je Tick).
        /// </summary>
        public static GameEvent Create(GameEventType type, Random rng)
        {
            var effect = GetDefaultEffect(type);

            // HighDemand + MaterialShortage betreffen einen zufälligen Workshop-Typ
            if (type is GameEventType.HighDemand or GameEventType.MaterialShortage)
            {
                var workshopTypes = (WorkshopType[])Enum.GetValues(typeof(WorkshopType));
                effect.AffectedWorkshop = workshopTypes[rng.Next(workshopTypes.Length)];
            }

            // WorkerStrike: MarketRestriction auf Tier C (höhere Tiers streiken)
            if (type == GameEventType.WorkerStrike)
            {
                effect.MarketRestriction = WorkerTier.C;
            }

            return new GameEvent
            {
                Type = type,
                StartedAt = DateTime.UtcNow,
                DurationTicks = type.GetDefaultDuration().Ticks,
                Effect = effect
            };
        }

        private static GameEventEffect GetDefaultEffect(GameEventType type) => type switch
        {
            GameEventType.MaterialSale => new GameEventEffect { CostMultiplier = 0.7m },
            GameEventType.MaterialShortage => new GameEventEffect { CostMultiplier = 1.5m },
            GameEventType.HighDemand => new GameEventEffect { RewardMultiplier = 1.5m },
            GameEventType.EconomicDownturn => new GameEventEffect { RewardMultiplier = 0.7m, ReputationChange = 2m },
            GameEventType.TaxAudit => new GameEventEffect { SpecialEffect = "tax_10_percent" },
            GameEventType.WorkerStrike => new GameEventEffect { SpecialEffect = "mood_drop_all_20" },
            GameEventType.InnovationFair => new GameEventEffect { IncomeMultiplier = 1.3m },
            GameEventType.CelebrityEndorsement => new GameEventEffect { IncomeMultiplier = 1.2m, ReputationChange = 5m },
            GameEventType.SpringSeason => new GameEventEffect { IncomeMultiplier = 1.15m },
            GameEventType.SummerBoom => new GameEventEffect { RewardMultiplier = 1.2m },
            GameEventType.AutumnSurge => new GameEventEffect { IncomeMultiplier = 1.1m, RewardMultiplier = 1.1m },
            GameEventType.WinterSlowdown => new GameEventEffect { IncomeMultiplier = 0.9m },
            _ => new GameEventEffect()
        };
    }
}
