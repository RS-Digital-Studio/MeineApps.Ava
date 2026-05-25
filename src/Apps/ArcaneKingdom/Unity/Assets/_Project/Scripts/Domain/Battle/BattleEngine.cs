#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Hero;

namespace ArcaneKingdom.Domain.Battle
{
    /// <summary>
    /// Deterministische Kampf-Engine. Reine C#-Logik (kein UnityEngine), damit unit-testbar
    /// und replay-faehig (Seed-basiert). v6.0: Erweitert um Helden-Passivs (5 Rassen) und
    /// Karten-Persoenlichkeit-Events (Designplan v4 Kap. 2.1 + Kap. 8).
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
            var playerDeck = new List<string>(playerDeckInstanceIds);
            var enemyDeck = new List<string>(enemyDeckInstanceIds);

            // Rudelbund: Tiergeister im Deck vorzaehlen fuer Stack-Bonus
            UpdateBeastSpiritCount(State.PlayerHeroPassiv, playerDeck);
            UpdateBeastSpiritCount(State.EnemyHeroPassiv, enemyDeck);

            foreach (var id in ShuffleDeterministic(playerDeck)) State.PlayerDeckQueue.Enqueue(id);
            foreach (var id in ShuffleDeterministic(enemyDeck)) State.EnemyDeckQueue.Enqueue(id);

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

            // Helden-Passivs anwenden (Designplan v4 Kap. 2.1)
            var heroPassiv = forPlayer ? State.PlayerHeroPassiv : State.EnemyHeroPassiv;
            var manaCost = def.Cost;
            float hpMultiplier = 1.0f;
            float atkMultiplier = 1.0f;

            if (heroPassiv != null)
            {
                // Waldlaeufer: erste Karte jeder Runde kostet 0 COST
                if (heroPassiv.PassivType == HeroFaehigkeitsTyp.Waldlaeufer && !heroPassiv.FirstCardThisTurnPlayed)
                {
                    manaCost = heroPassiv.Magnitude;  // i.d.R. 0
                }
                // Koenigliche Aura: +X% HP auf eigene Karten
                if (heroPassiv.PassivType == HeroFaehigkeitsTyp.KoeniglicheAura)
                {
                    hpMultiplier += heroPassiv.Magnitude / 100f;
                }
                // Rudelbund: +X% ATK pro Tiergeist im Deck (stapelbar)
                if (heroPassiv.PassivType == HeroFaehigkeitsTyp.Rudelbund)
                {
                    atkMultiplier += (heroPassiv.Magnitude * heroPassiv.BeastSpiritCountInDeck) / 100f;
                }
            }

            var availableMana = forPlayer ? State.PlayerMana : State.EnemyMana;
            if (manaCost > availableMana) return false;

            if (forPlayer) State.PlayerMana -= manaCost; else State.EnemyMana -= manaCost;
            if (heroPassiv != null && heroPassiv.PassivType == HeroFaehigkeitsTyp.Waldlaeufer)
                heroPassiv.FirstCardThisTurnPlayed = true;

            hand.Remove(cardInstanceId);
            var finalHp = (int)(def.BaseHealth * statMultiplier * hpMultiplier);
            var finalAtk = (int)(def.BaseAttack * statMultiplier * atkMultiplier);
            field.Add(new CardFieldSlot(cardInstanceId,
                currentAttack: finalAtk,
                currentHealth: finalHp,
                turnsUntilSpecial: def.TurnsToSpecial));

            // Karten-Persoenlichkeit OnPlay (ab 3 Sternen mit Dialog-Lines)
            if (!string.IsNullOrEmpty(def.OnPlayLineKey))
            {
                State.Events.Add(new BattleEvent(
                    BattleEventType.CardPlayed, State.CurrentTurn, forPlayer,
                    cardInstanceId: cardInstanceId,
                    cardDefinitionId: def.Id,
                    localizationKey: def.OnPlayLineKey));
            }

            // Synergy-Bonus: Karte mit identifizierten Synergy-Partner im selben Field?
            TriggerSynergyIfMatches(def, field, forPlayer);

            // Rivalen-Dialog: gegnerisches Field enthaelt eine Rival-Karte?
            CheckRivalryWithOpposingField(def, forPlayer);

            return true;
        }

        public void EndTurn()
        {
            var attackerIsPlayer = State.Phase == BattlePhase.PlayerTurn;
            var attackerField = attackerIsPlayer ? State.PlayerField : State.EnemyField;
            var defenderField = attackerIsPlayer ? State.EnemyField : State.PlayerField;
            var attackerPassiv = attackerIsPlayer ? State.PlayerHeroPassiv : State.EnemyHeroPassiv;

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
                    ApplyLifesteal(attackerPassiv, attackerIsPlayer, dealt);

                    if (target.CurrentHealth <= 0)
                    {
                        if (!TryGoettlicherSegenRescue(target, defenderField, !attackerIsPlayer))
                        {
                            EmitOnDeathEvent(target, targetDef, !attackerIsPlayer);
                            defenderField.RemoveAt(0);
                        }
                    }
                }
                else
                {
                    if (attackerIsPlayer) State.EnemyHeroHp -= damage; else State.PlayerHeroHp -= damage;
                    ApplyLifesteal(attackerPassiv, attackerIsPlayer, damage);
                }

                attacker.TurnsUntilSpecial = Math.Max(0, attacker.TurnsUntilSpecial - 1);
                if (attacker.TurnsUntilSpecial == 0 && def.BaseAbility != null)
                {
                    TriggerSpecial(attacker, def, attackerField, defenderField, attackerIsPlayer);
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

            // Waldlaeufer-Reset: jede Runde wieder neu
            if (State.PlayerHeroPassiv != null) State.PlayerHeroPassiv.FirstCardThisTurnPlayed = false;
            if (State.EnemyHeroPassiv != null)  State.EnemyHeroPassiv.FirstCardThisTurnPlayed  = false;

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
                EmitVictoryEvents(result);
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

        // ============================================================================
        // Helden-Passiv-Logik
        // ============================================================================

        /// <summary>
        /// LebensraubAura: 20% (default) aller Karten-Schaeden heilen Helden-HP.
        /// </summary>
        private void ApplyLifesteal(HeroPassivContext? passiv, bool attackerIsPlayer, int damage)
        {
            if (passiv == null) return;
            if (passiv.PassivType != HeroFaehigkeitsTyp.LebensraubAura) return;
            var heal = damage * passiv.Magnitude / 100;
            if (heal <= 0) return;
            if (attackerIsPlayer) State.PlayerHeroHp = Math.Min(State.PlayerHeroHp + heal, 10_000);
            else                  State.EnemyHeroHp  = Math.Min(State.EnemyHeroHp  + heal, 10_000);

            State.Events.Add(new BattleEvent(
                BattleEventType.HeroPassivTriggered, State.CurrentTurn, attackerIsPlayer,
                localizationKey: "hero.daemonen.skill.name",
                magnitude: heal));
        }

        /// <summary>
        /// GoettlicherSegen: einmal pro Kampf wird der Tod einer Karte verhindert (1 HP).
        /// </summary>
        private bool TryGoettlicherSegenRescue(CardFieldSlot target, List<CardFieldSlot> field, bool forPlayer)
        {
            var passiv = forPlayer ? State.PlayerHeroPassiv : State.EnemyHeroPassiv;
            if (passiv == null) return false;
            if (passiv.PassivType != HeroFaehigkeitsTyp.GoettlicherSegen) return false;
            if (passiv.DivineBlessingsRemaining <= 0) return false;

            passiv.DivineBlessingsRemaining--;
            target.CurrentHealth = 1;
            State.Events.Add(new BattleEvent(
                BattleEventType.HeroPassivTriggered, State.CurrentTurn, forPlayer,
                cardInstanceId: target.CardInstanceId,
                localizationKey: "hero.goetter.skill.name",
                magnitude: 1));
            return true;
        }

        /// <summary>
        /// Pre-compute Tiergeist-Anzahl im Deck fuer Rudelbund-Stack-Bonus.
        /// </summary>
        private void UpdateBeastSpiritCount(HeroPassivContext? passiv, IEnumerable<string> deck)
        {
            if (passiv == null) return;
            if (passiv.PassivType != HeroFaehigkeitsTyp.Rudelbund) return;
            var count = 0;
            foreach (var instanceId in deck)
            {
                var (def, _) = ResolveDefinition(instanceId);
                if (def != null && def.Race == Race.Tiergeister) count++;
            }
            passiv.BeastSpiritCountInDeck = count;
        }

        // ============================================================================
        // Personality-Events
        // ============================================================================

        private void TriggerSynergyIfMatches(CardDefinition def, List<CardFieldSlot> field, bool forPlayer)
        {
            if (def.SynergyCardIds == null || def.SynergyCardIds.Count == 0) return;
            foreach (var slot in field)
            {
                if (slot.CardInstanceId == "") continue;
                var (otherDef, _) = ResolveDefinition(slot.CardInstanceId);
                if (otherDef == null) continue;
                if (otherDef.Id == def.Id) continue;
                if (def.SynergyCardIds.Contains(otherDef.Id))
                {
                    State.Events.Add(new BattleEvent(
                        BattleEventType.SynergyActivated, State.CurrentTurn, forPlayer,
                        cardInstanceId: slot.CardInstanceId,
                        cardDefinitionId: def.Id,
                        partnerCardId: otherDef.Id,
                        magnitude: 5));   // Designplan v4 Kap. 8.2: +5% HP-Bonus
                }
            }
        }

        private void CheckRivalryWithOpposingField(CardDefinition def, bool forPlayer)
        {
            if (def.RivalCardIds == null || def.RivalCardIds.Count == 0) return;
            var enemyField = forPlayer ? State.EnemyField : State.PlayerField;
            foreach (var slot in enemyField)
            {
                var (otherDef, _) = ResolveDefinition(slot.CardInstanceId);
                if (otherDef == null) continue;
                if (def.RivalCardIds.Contains(otherDef.Id))
                {
                    State.Events.Add(new BattleEvent(
                        BattleEventType.RivalryClashed, State.CurrentTurn, forPlayer,
                        cardDefinitionId: def.Id,
                        partnerCardId: otherDef.Id));
                }
            }
        }

        private void EmitOnDeathEvent(CardFieldSlot slot, CardDefinition? def, bool forPlayer)
        {
            if (def == null) return;
            if (string.IsNullOrEmpty(def.OnDeathLineKey)) return;
            State.Events.Add(new BattleEvent(
                BattleEventType.CardDied, State.CurrentTurn, forPlayer,
                cardInstanceId: slot.CardInstanceId,
                cardDefinitionId: def.Id,
                localizationKey: def.OnDeathLineKey));
        }

        private void EmitVictoryEvents(BattleResult result)
        {
            // Alle ueberlebenden eigenen Karten mit OnVictoryLineKey
            var winnerField = result == BattleResult.PlayerWins ? State.PlayerField : State.EnemyField;
            var winnerIsPlayer = result == BattleResult.PlayerWins;
            foreach (var slot in winnerField)
            {
                if (slot.CurrentHealth <= 0) continue;
                var (def, _) = ResolveDefinition(slot.CardInstanceId);
                if (def == null || string.IsNullOrEmpty(def.OnVictoryLineKey)) continue;
                State.Events.Add(new BattleEvent(
                    BattleEventType.CardVictory, State.CurrentTurn, winnerIsPlayer,
                    cardInstanceId: slot.CardInstanceId,
                    cardDefinitionId: def.Id,
                    localizationKey: def.OnVictoryLineKey));
            }
        }

        // ============================================================================
        // Spezial-Skills + Helpers
        // ============================================================================

        private bool _bossPhase2Triggered;

        private void DrawCard(bool forPlayer)
        {
            var hand = forPlayer ? State.PlayerHand : State.EnemyHand;
            var deck = forPlayer ? State.PlayerDeckQueue : State.EnemyDeckQueue;
            if (hand.Count >= MaxHandSize) return;
            if (deck.Count > 0) hand.Add(deck.Dequeue());
        }

        private static void TriggerSpecial(CardFieldSlot caster, CardDefinition def, List<CardFieldSlot> allies, List<CardFieldSlot> enemies, bool forPlayer)
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
                    if (ability.Magnitude > 0)
                    {
                        var heal = caster.MaxHealth * ability.Magnitude / 100;
                        caster.CurrentHealth = Math.Min(caster.CurrentHealth + heal, caster.MaxHealth);
                    }
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
                // Control & Synergy: Status-Effekt-System wird in Phase 2 nachgereicht.
            }
        }

        private void ApplyBossPhase2()
        {
            // Welt-Boss aktiviert Spezialfähigkeit + ATK-Buff für alle Field-Karten (DESIGN 9.4).
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
