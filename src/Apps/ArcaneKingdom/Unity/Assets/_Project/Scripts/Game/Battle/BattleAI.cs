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
    /// Strategie (Pilot, Designplan v3 Kap. 7.3 Mana-Modell):
    /// 1. Jede Karte kostet 1 Mana-Orb (3/Runde) — COST ist KEIN Mana-Preis
    /// 2. Wahl-Reihenfolge: hoechste absolute Staerke (ATK + HP), Element-Vorteil bevorzugt
    /// 3. Schwere Karten (COST > 30) nur als erste Aktion der Runde einsetzen
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
                // Jede Karte ist grundsaetzlich spielbar — COST ist kein Mana-Preis (Designplan v3 Kap. 7.3).
                playable.Add((instId, def, ScoreCard(def, dominantEnemyElement)));
            }

            playable.Sort((a, b) => b.score.CompareTo(a.score));

            // 1 Mana pro Karte; schwere Karten (COST>30) nur, wenn diese Runde noch nichts gespielt wurde.
            var result = new List<string>();
            var remainingMana = availableMana;
            var cardsPlayed = 0;
            foreach (var p in playable)
            {
                if (remainingMana < BattleEngine.ManaPerCard) break;
                if (p.def.Cost > BattleEngine.HeavyCardCostThreshold && cardsPlayed > 0) continue;
                result.Add(p.id);
                remainingMana -= BattleEngine.ManaPerCard;
                cardsPlayed++;
            }
            return result;
        }

        private static float ScoreCard(CardDefinition def, Element? dominantEnemyElement)
        {
            // Mana ist nicht cost-abhaengig -> absolute Karten-Staerke bewerten, nicht Wert/Cost.
            var statValue = (float)(def.BaseAttack + def.BaseHealth);

            // Element-Bonus
            if (dominantEnemyElement.HasValue)
            {
                var multiplier = Domain.Battle.ElementMatchup.GetMultiplier(def.Element, dominantEnemyElement.Value);
                statValue *= multiplier;
            }

            // Spezial-Cooldown-Bonus: Karten mit kurzer Wartezeit sind im Schnitt stärker
            statValue *= 1f + (1f / Math.Max(1, def.TurnsToSpecial)) * 0.2f;

            // Rarity-Präferenz (leichter Tie-Break-Multiplier)
            statValue *= 1f + (int)def.Rarity * 0.02f;

            return statValue;
        }
    }
}
