#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Domain.Battle
{
    /// <summary>
    /// Deterministische Kampf-Engine. Reine C#-Logik (kein UnityEngine), damit unit-testbar
    /// und replay-faehig (Seed-basiert).
    ///
    /// SKELETT: Die volle Implementierung folgt in der MVP-Phase. Aktuell nur das API-Schema,
    /// damit andere Module dagegen entwickeln koennen.
    /// </summary>
    public sealed class BattleEngine
    {
        private readonly Random _random;
        private readonly IReadOnlyDictionary<string, CardDefinition> _cardDefinitions;

        public BattleState State { get; }

        public BattleEngine(BattleState state, IReadOnlyDictionary<string, CardDefinition> cardDefinitions)
        {
            State = state;
            _cardDefinitions = cardDefinitions;
            _random = new Random(state.Seed);
        }

        /// <summary>
        /// Schritt 1: Initialer Setup — Deck shuffeln, Start-Hand ziehen, Mana setzen.
        /// </summary>
        public void Setup(IEnumerable<string> playerDeckInstanceIds, IEnumerable<string> enemyDeckInstanceIds)
        {
            foreach (var id in ShuffleDeterministic(playerDeckInstanceIds)) State.PlayerDeckQueue.Enqueue(id);
            foreach (var id in ShuffleDeterministic(enemyDeckInstanceIds)) State.EnemyDeckQueue.Enqueue(id);

            for (var i = 0; i < 4; i++)
            {
                if (State.PlayerDeckQueue.Count > 0) State.PlayerHand.Add(State.PlayerDeckQueue.Dequeue());
                if (State.EnemyDeckQueue.Count > 0) State.EnemyHand.Add(State.EnemyDeckQueue.Dequeue());
            }

            State.Phase = BattlePhase.PlayerTurn;
        }

        /// <summary>
        /// Schritt 2: Karte spielen. Validiert Mana, fuegt zu Field hinzu.
        /// </summary>
        public bool PlayCard(bool forPlayer, string cardInstanceId)
        {
            // TODO: Mana-Check, Field-Slot-Check, Faehigkeits-Trigger
            throw new NotImplementedException("Wird in MVP-Phase implementiert.");
        }

        /// <summary>
        /// Schritt 3: Runde beenden. Cards greifen an, Rundenwarten dekrementieren, Spezialattacken triggern.
        /// </summary>
        public void EndTurn()
        {
            // TODO: Attack-Resolution, Special-Triggers, Damage-Calculation mit Element-Multiplikatoren
            throw new NotImplementedException("Wird in MVP-Phase implementiert.");
        }

        /// <summary>
        /// Sieg-Bedingung pruefen.
        /// </summary>
        public BattleResult CheckVictoryCondition()
        {
            if (State.PlayerHeroHp <= 0 && State.EnemyHeroHp <= 0) return BattleResult.Draw;
            if (State.PlayerHeroHp <= 0) return BattleResult.EnemyWins;
            if (State.EnemyHeroHp <= 0) return BattleResult.PlayerWins;
            if (State.CurrentTurn >= 50)
                return State.PlayerHeroHp > State.EnemyHeroHp ? BattleResult.PlayerWins : BattleResult.EnemyWins;
            return BattleResult.Undecided;
        }

        private List<string> ShuffleDeterministic(IEnumerable<string> ids)
        {
            var list = new List<string>(ids);
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }
    }
}
