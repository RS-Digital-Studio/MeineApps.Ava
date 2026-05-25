#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Game.Battle
{
    /// <summary>
    /// Greedy-AI für Welt-Kaempfe (PvE) und Auto-Kampf. Reine C#-Logik, deterministisch
    /// für Replay-Fähigkeit.
    ///
    /// Strategie (Pilot):
    /// 1. Spiele alle Karten, die ins Mana passen
    /// 2. Wahl-Reihenfolge: hoechster (ATK + HP) / Cost — best value first
    /// 3. Bevorzuge Element-Vorteil gegen aktiven Gegner-Feld
    /// 4. Reserviere Mana für Karten mit aktiver Spezial in <=2 Runden
    /// </summary>
    public sealed class BattleAI
    {
        private readonly IReadOnlyDictionary<string, CardDefinition> _cardDefinitions;
        private readonly IReadOnlyDictionary<string, CardInstance> _cardInstances;

        public BattleAI(IReadOnlyDictionary<string, CardDefinition> defs, IReadOnlyDictionary<string, CardInstance> instances)
        {
            _cardDefinitions = defs;
            _cardInstances = instances;
        }

        /// <summary>
        /// Empfiehlt eine geordnete Liste von Karten zum Ausspielen. Aufrufer wendet
        /// jede Empfehlung der Reihe nach an (Mana-Check passiert intern).
        /// </summary>
        public List<string> ChooseCardsToPlay(IReadOnlyList<string> handInstanceIds, int availableMana, Element? dominantEnemyElement = null)
        {
            var playable = new List<(string id, CardDefinition def, float score)>();
            foreach (var instId in handInstanceIds)
            {
                if (!_cardInstances.TryGetValue(instId, out var inst)) continue;
                if (!_cardDefinitions.TryGetValue(inst.CardDefinitionId, out var def)) continue;
                if (def.Cost > availableMana) continue;
                playable.Add((instId, def, ScoreCard(def, dominantEnemyElement)));
            }

            playable.Sort((a, b) => b.score.CompareTo(a.score));

            var result = new List<string>();
            var remainingMana = availableMana;
            foreach (var p in playable)
            {
                if (p.def.Cost > remainingMana) continue;
                result.Add(p.id);
                remainingMana -= p.def.Cost;
            }
            return result;
        }

        private static float ScoreCard(CardDefinition def, Element? dominantEnemyElement)
        {
            var statValue = (def.BaseAttack + def.BaseHealth) / (float)Math.Max(1, def.Cost);

            // Element-Bonus
            if (dominantEnemyElement.HasValue)
            {
                var multiplier = Domain.Battle.ElementMatchup.GetMultiplier(def.Element, dominantEnemyElement.Value);
                statValue *= multiplier;
            }

            // Spezial-Cooldown-Bonus: Karten mit kurzer Wartezeit sind im Schnitt stärker
            statValue *= 1f + (1f / Math.Max(1, def.TurnsToSpecial)) * 0.2f;

            // Rarity-Präferenz (Tie-Break)
            statValue += (int)def.Rarity * 0.5f;

            return statValue;
        }
    }
}
