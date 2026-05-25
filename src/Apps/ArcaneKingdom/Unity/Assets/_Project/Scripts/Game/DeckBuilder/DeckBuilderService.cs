#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Game.DeckBuilder
{
    /// <summary>
    /// Schlägt ein optimales Deck aus der Spieler-Sammlung vor (DESIGN.md Kap. 4.4
    /// "Suggest-Button"). Greedy-Heuristik: maximiert ATK + HP innerhalb des COST-Budgets,
    /// respektiert Deck-Limits (1/deck, MaxTwo, Unlimited).
    ///
    /// Pure C# — deterministisch für gleiche Eingabe (Ordnung der Sammlung egal).
    /// </summary>
    public sealed class DeckBuilderService
    {
        public sealed class BuildContext
        {
            public IReadOnlyDictionary<string, CardInstance> Sammlung { get; init; } = default!;
            public IReadOnlyDictionary<string, CardDefinition> Definitions { get; init; } = default!;
            public int CostBudget { get; init; } = int.MaxValue;
            /// <summary>Optionales Element-Thema für Synergie-Präferenz.</summary>
            public Element? PreferredElement { get; init; }
            /// <summary>Optionale Rassen-Präferenz für Synergie-Boni.</summary>
            public ArcaneKingdom.Domain.Cards.Race? PreferredRace { get; init; }
        }

        public sealed class BuildResult
        {
            public List<string> CardInstanceIds { get; } = new();
            public int TotalCost { get; set; }
            public long TotalAttack { get; set; }
            public long TotalHealth { get; set; }
            public bool Truncated { get; set; }   // true wenn das Budget knapp wurde
        }

        public BuildResult Build(BuildContext ctx)
        {
            if (ctx.Sammlung == null || ctx.Definitions == null)
                throw new ArgumentException("Sammlung + Definitions sind Pflicht.", nameof(ctx));

            // Vorbereitung: jede Karten-Instanz mit Score versehen
            var scored = new List<(string instId, CardDefinition def, CardInstance inst, float score)>();
            foreach (var kv in ctx.Sammlung)
            {
                if (!ctx.Definitions.TryGetValue(kv.Value.CardDefinitionId, out var def)) continue;
                scored.Add((kv.Key, def, kv.Value, ScoreCard(def, kv.Value, ctx.PreferredElement, ctx.PreferredRace)));
            }
            // Hohe Werte zuerst, deterministische Sortierung
            scored.Sort((a, b) =>
            {
                var cmp = b.score.CompareTo(a.score);
                return cmp != 0 ? cmp : string.CompareOrdinal(a.instId, b.instId);
            });

            var result = new BuildResult();
            var perDefCount = new Dictionary<string, int>();
            var remainingCost = ctx.CostBudget;

            foreach (var s in scored)
            {
                if (result.CardInstanceIds.Count >= Deck.MaxCards) break;
                if (s.def.Cost > remainingCost) { result.Truncated = true; continue; }

                var currentCount = perDefCount.TryGetValue(s.def.Id, out var c) ? c : 0;
                var limit = s.def.DeckLimit switch
                {
                    DeckLimit.OneOnly => 1,
                    DeckLimit.MaxTwo => 2,
                    DeckLimit.Unlimited => DeckValidator.MaxCopiesFarmable,
                    _ => DeckValidator.MaxCopiesFarmable
                };
                if (currentCount >= limit) continue;

                result.CardInstanceIds.Add(s.instId);
                perDefCount[s.def.Id] = currentCount + 1;
                remainingCost -= s.def.Cost;
                result.TotalCost += s.def.Cost;
                var statMultiplier = s.inst.StatBonusMultiplier;
                result.TotalAttack += (long)(s.def.BaseAttack * statMultiplier);
                result.TotalHealth += (long)(s.def.BaseHealth * statMultiplier);
            }

            return result;
        }

        private static float ScoreCard(CardDefinition def, CardInstance inst, Element? prefElement, ArcaneKingdom.Domain.Cards.Race? prefRace)
        {
            var statValue = (def.BaseAttack + def.BaseHealth) * inst.StatBonusMultiplier;
            var perCost = statValue / Math.Max(1, def.Cost);
            if (prefElement.HasValue && def.Element == prefElement.Value) perCost *= 1.15f;
            if (prefRace.HasValue && def.Race == prefRace.Value) perCost *= 1.10f;
            perCost += (int)def.Rarity * 0.5f;                  // sanfte Rarity-Präferenz
            perCost += (1f / Math.Max(1, def.TurnsToSpecial));  // Speed-Bonus
            return perCost;
        }
    }
}
