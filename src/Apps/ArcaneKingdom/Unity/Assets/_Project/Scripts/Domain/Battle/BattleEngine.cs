#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Domain.Battle
{
    /// <summary>
    /// Deterministische Kampf-Engine. Reine C#-Logik (kein UnityEngine), damit unit-testbar
    /// und replay-faehig (Seed-basiert).
    /// </summary>
    public sealed class BattleEngine
    {
        public const int MaxFieldSlots = 5;
        public const int MaxHandSize = 5;
        public const int MaxMana = 10;
        public const int MaxTurns = 50;

        private readonly Random _random;
        private readonly IReadOnlyDictionary<string, CardDefinition> _cardDefinitions;
        private readonly IReadOnlyDictionary<string, CardInstance> _cardInstances;

        public BattleState State { get; }

        public BattleEngine(BattleState state,
                            IReadOnlyDictionary<string, CardDefinition> cardDefinitions,
                            IReadOnlyDictionary<string, CardInstance>? cardInstances = null)
        {
            State = state;
            _cardDefinitions = cardDefinitions;
            _cardInstances = cardInstances ?? new Dictionary<string, CardInstance>();
            _random = new Random(state.Seed);
        }

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

        public bool PlayCard(bool forPlayer, string cardInstanceId)
        {
            var hand = forPlayer ? State.PlayerHand : State.EnemyHand;
            var field = forPlayer ? State.PlayerField : State.EnemyField;
            if (!hand.Contains(cardInstanceId)) return false;
            if (field.Count >= MaxFieldSlots) return false;

            var (def, statMultiplier) = ResolveDefinition(cardInstanceId);
            if (def == null) return false;

            var availableMana = forPlayer ? State.PlayerMana : State.EnemyMana;
            if (def.Cost > availableMana) return false;

            if (forPlayer) State.PlayerMana -= def.Cost; else State.EnemyMana -= def.Cost;
            hand.Remove(cardInstanceId);
            field.Add(new CardFieldSlot(cardInstanceId,
                currentAttack: (int)(def.BaseAttack * statMultiplier),
                currentHealth: (int)(def.BaseHealth * statMultiplier),
                turnsUntilSpecial: def.TurnsToSpecial));
            return true;
        }

        public void EndTurn()
        {
            var attackerField = State.Phase == BattlePhase.PlayerTurn ? State.PlayerField : State.EnemyField;
            var defenderField = State.Phase == BattlePhase.PlayerTurn ? State.EnemyField : State.PlayerField;
            var attackerIsPlayer = State.Phase == BattlePhase.PlayerTurn;

            for (var i = 0; i < attackerField.Count; i++)
            {
                var attacker = attackerField[i];
                if (attacker.CurrentHealth <= 0) continue;
                var (def, _) = ResolveDefinition(attacker.CardInstanceId);
                if (def == null) continue;

                var damage = attacker.CurrentAttack;
                if (defenderField.Count > 0)
                {
                    var target = defenderField[0];
                    var (targetDef, _) = ResolveDefinition(target.CardInstanceId);
                    var multiplier = targetDef != null ? ElementMatchup.GetMultiplier(def.Element, targetDef.Element) : 1f;
                    var dealt = (int)(damage * multiplier);
                    target.CurrentHealth -= dealt;
                    if (target.CurrentHealth <= 0) defenderField.RemoveAt(0);
                }
                else
                {
                    if (attackerIsPlayer) State.EnemyHeroHp -= damage; else State.PlayerHeroHp -= damage;
                }

                attacker.TurnsUntilSpecial = Math.Max(0, attacker.TurnsUntilSpecial - 1);
                if (attacker.TurnsUntilSpecial == 0 && def.BaseAbility != null)
                {
                    TriggerSpecial(attacker, def, attackerField, defenderField);
                    attacker.TurnsUntilSpecial = def.TurnsToSpecial;
                }
            }

            // Boss-Phase 2 ab 50 % Helden-HP der Gegnerseite (nur einmal pro Kampf).
            if (attackerIsPlayer && State.EnemyHeroHp > 0 && State.EnemyHeroHp < 500 && !_bossPhase2Triggered)
            {
                _bossPhase2Triggered = true;
                ApplyBossPhase2();
            }

            State.CurrentTurn++;
            if (State.Phase == BattlePhase.PlayerTurn)
            {
                State.Phase = BattlePhase.EnemyTurn;
                State.EnemyMaxMana = Math.Min(State.EnemyMaxMana + 1, MaxMana);
                State.EnemyMana = State.EnemyMaxMana;
                DrawCard(forPlayer: false);
            }
            else
            {
                State.Phase = BattlePhase.PlayerTurn;
                State.PlayerMaxMana = Math.Min(State.PlayerMaxMana + 1, MaxMana);
                State.PlayerMana = State.PlayerMaxMana;
                DrawCard(forPlayer: true);
            }

            var result = CheckVictoryCondition();
            if (result != BattleResult.Undecided)
            {
                State.Result = result;
                State.Phase = BattlePhase.Settlement;
            }
        }

        public BattleResult CheckVictoryCondition()
        {
            if (State.PlayerHeroHp <= 0 && State.EnemyHeroHp <= 0) return BattleResult.Draw;
            if (State.PlayerHeroHp <= 0) return BattleResult.EnemyWins;
            if (State.EnemyHeroHp <= 0) return BattleResult.PlayerWins;
            if (State.CurrentTurn >= MaxTurns)
                return State.PlayerHeroHp > State.EnemyHeroHp ? BattleResult.PlayerWins : BattleResult.EnemyWins;
            return BattleResult.Undecided;
        }

        // ----------------------------------------------------------------- Intern

        private bool _bossPhase2Triggered;

        private void DrawCard(bool forPlayer)
        {
            var hand = forPlayer ? State.PlayerHand : State.EnemyHand;
            var deck = forPlayer ? State.PlayerDeckQueue : State.EnemyDeckQueue;
            if (hand.Count >= MaxHandSize) return;
            if (deck.Count > 0) hand.Add(deck.Dequeue());
        }

        private static void TriggerSpecial(CardFieldSlot caster, CardDefinition def, List<CardFieldSlot> allies, List<CardFieldSlot> enemies)
        {
            var ability = def.BaseAbility;
            if (ability == null) return;
            switch (ability.Category)
            {
                case AbilityCategory.Damage:
                    if (ability.TargetsAllEnemies)
                    {
                        var aoeDmg = Math.Max(1, caster.CurrentAttack * ability.Magnitude / 100);
                        for (var i = enemies.Count - 1; i >= 0; i--)
                        {
                            enemies[i].CurrentHealth -= aoeDmg;
                            if (enemies[i].CurrentHealth <= 0) enemies.RemoveAt(i);
                        }
                    }
                    else if (enemies.Count > 0)
                    {
                        var dmg = Math.Max(1, caster.CurrentAttack * ability.Magnitude / 100);
                        enemies[0].CurrentHealth -= dmg;
                        if (enemies[0].CurrentHealth <= 0) enemies.RemoveAt(0);
                    }
                    break;
                case AbilityCategory.Defense:
                    if (ability.Magnitude > 0) caster.CurrentHealth += caster.CurrentHealth * ability.Magnitude / 100;
                    break;
                case AbilityCategory.Buff:
                    if (ability.TargetsAllAllies)
                    {
                        foreach (var a in allies) a.CurrentAttack += a.CurrentAttack * ability.Magnitude / 100;
                    }
                    break;
                case AbilityCategory.Debuff:
                    if (ability.TargetsAllEnemies)
                    {
                        foreach (var e in enemies) e.CurrentAttack = Math.Max(1, e.CurrentAttack - e.CurrentAttack * ability.Magnitude / 100);
                    }
                    break;
                // Control & Synergy: Game-spezifische Effekte folgen mit Status-Effekt-System.
            }
        }

        private void ApplyBossPhase2()
        {
            // Welt-Boss aktiviert Spezialfaehigkeit + ATK-Buff fuer alle Field-Karten (DESIGN 9.4).
            foreach (var slot in State.EnemyField) slot.CurrentAttack += slot.CurrentAttack / 2;
        }

        private (CardDefinition? def, float statMultiplier) ResolveDefinition(string cardInstanceId)
        {
            var multiplier = _cardInstances.TryGetValue(cardInstanceId, out var inst) ? inst.StatBonusMultiplier : 1f;
            if (_cardDefinitions.TryGetValue(cardInstanceId, out var defDirect)) return (defDirect, multiplier);
            if (inst != null && _cardDefinitions.TryGetValue(inst.CardDefinitionId, out var defViaInst)) return (defViaInst, multiplier);
            return (null, multiplier);
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
