#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Hero;
using ArcaneKingdom.Domain.Runes;

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
        public const int MaxTurns = 50;

        /// <summary>
        /// Mana-Kosten pro eingesetzter Karte. Designplan v3 Kap. 7.3: jede Karte kostet 1 Mana-Orb,
        /// UNABHAENGIG von ihrem COST-Wert. COST ist das Deck-Bau-Budget (DeckValidator, Cap 200) und
        /// das Schwere-Karten-Gate, NICHT der pro-Karte-Mana-Preis.
        /// </summary>
        public const int ManaPerCard = 1;

        /// <summary>
        /// COST-Schwelle fuer "schwere" Karten (Designplan v3 Kap. 7.3): Karten mit COST &gt; 30
        /// koennen nur eingesetzt werden, wenn in diesem Zug noch nichts anderes gespielt wurde.
        /// </summary>
        public const int HeavyCardCostThreshold = 30;

        // Plattformneutraler PRNG (Mulberry32) — identisch in der TS-Portierung des Servers,
        // damit der Anti-Cheat-Replay denselben Kampfverlauf erzeugt (kein System.Random!).
        private readonly DeterministicRng _random;
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
            _random = new DeterministicRng(state.Seed);
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

            // K12: Runen-Start-Effekte (Hero-HP + Start-Mana). Kein RNG -> RNG-Verlauf unveraendert.
            ApplyRuneStartEffects(forPlayer: true, State.PlayerRuneLoadout);
            ApplyRuneStartEffects(forPlayer: false, State.EnemyRuneLoadout);

            State.Phase = BattlePhase.PlayerTurn;
        }

        /// <summary>
        /// Wendet die einmaligen Start-Boni einer Deck-Rune an (Spielplan v5 Kap. 7.2):
        /// Hero-Runen erhoehen Helden-HP (+Max), Mana-Runen geben einmalig Start-Mana fuer Runde 1
        /// (NICHT MaxMana -> ab Runde 2 wieder 3 Orbs, Invariante gewahrt).
        /// </summary>
        private void ApplyRuneStartEffects(bool forPlayer, RuneLoadout? loadout)
        {
            if (loadout == null) return;
            if (loadout.HeroHpFlat > 0)
            {
                if (forPlayer) { State.PlayerHeroHp += loadout.HeroHpFlat; State.PlayerHeroMaxHp += loadout.HeroHpFlat; }
                else           { State.EnemyHeroHp += loadout.HeroHpFlat;  State.EnemyHeroMaxHp += loadout.HeroHpFlat; }
            }
            if (loadout.BonusStartMana > 0)
            {
                if (forPlayer) State.PlayerMana += loadout.BonusStartMana;
                else           State.EnemyMana += loadout.BonusStartMana;
            }
        }

        /// <summary>Spezial-Cooldown nach Geschwindigkeits-Runen (Spielplan v5 Kap. 7.2), min. 1 Runde.</summary>
        private static int EffectiveTurnsToSpecial(CardDefinition def, RuneLoadout? loadout)
        {
            var t = def.TurnsToSpecial;
            if (loadout != null && loadout.SpecialTurnReduction > 0)
                t = Math.Max(1, t - loadout.SpecialTurnReduction);
            return t;
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
            float hpMultiplier = 1.0f;
            float atkMultiplier = 1.0f;

            // Mana-Kosten = 1 pro Karte (Designplan v3 Kap. 7.3), NICHT die COST.
            var manaCost = ManaPerCard;

            if (heroPassiv != null)
            {
                // Waldlaeufer: erste Karte jeder Runde kostet 0 Mana
                if (heroPassiv.PassivType == HeroFaehigkeitsTyp.Waldlaeufer && !heroPassiv.FirstCardThisTurnPlayed)
                {
                    manaCost = 0;
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

            // Schwere Karte (COST > 30, i.d.R. Epic/Legendaer/Mythisch): nur einsetzbar, wenn in
            // diesem Zug noch nichts anderes gespielt wurde (Designplan v3 Kap. 7.3).
            var cardsPlayedThisTurn = forPlayer ? State.PlayerCardsPlayedThisTurn : State.EnemyCardsPlayedThisTurn;
            if (def.Cost > HeavyCardCostThreshold && cardsPlayedThisTurn > 0) return false;

            if (forPlayer) State.PlayerMana -= manaCost; else State.EnemyMana -= manaCost;
            if (forPlayer) State.PlayerCardsPlayedThisTurn++; else State.EnemyCardsPlayedThisTurn++;
            if (heroPassiv != null && heroPassiv.PassivType == HeroFaehigkeitsTyp.Waldlaeufer)
                heroPassiv.FirstCardThisTurnPlayed = true;

            hand.Remove(cardInstanceId);
            // Gegner-Karten zusaetzlich mit Schwierigkeits-Multiplier skalieren (Spielplan v5 Kap. 8.3).
            var difficultyMul = forPlayer ? 1.0f : State.EnemyStatMultiplier;
            // Deck-Runen-Boni (K12, Spielplan v5 Kap. 7.2): +X% ATK/HP aller Deck-Karten, plus
            // Kombo-Boni (Daemonen -> alle Allies, Drachen -> nur Drachen). Additiv, kein RNG.
            var loadout = forPlayer ? State.PlayerRuneLoadout : State.EnemyRuneLoadout;
            var runeHpMul = 1f + (loadout?.HealthPercent ?? 0f) / 100f;
            var runeAtkMul = 1f + (loadout?.AttackPercent ?? 0f) / 100f;
            if (loadout != null)
            {
                if (loadout.ComboDaemonActive) runeAtkMul += loadout.ComboDaemonAtkPercent / 100f;
                if (loadout.ComboDracheActive && RuneLoadoutBuilder.IsDrache(def))
                    runeAtkMul += loadout.ComboDracheAtkPercent / 100f;
            }
            var finalHp = (int)(def.BaseHealth * statMultiplier * hpMultiplier * difficultyMul * runeHpMul);
            var finalAtk = (int)(def.BaseAttack * statMultiplier * atkMultiplier * difficultyMul * runeAtkMul);
            field.Add(new CardFieldSlot(cardInstanceId,
                currentAttack: finalAtk,
                currentHealth: finalHp,
                turnsUntilSpecial: EffectiveTurnsToSpecial(def, loadout)));

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
            var attackerLoadout = attackerIsPlayer ? State.PlayerRuneLoadout : State.EnemyRuneLoadout;   // K12

            // Status-Effekte: DoT-Tick + Action-Block-Check VOR der Attack-Phase
            foreach (var slot in attackerField)
            {
                var dot = StatusEffectHelpers.TickDamageOverTime(slot.StatusEffects);
                if (dot > 0) slot.CurrentHealth -= dot;
            }
            // Tote Karten entfernen (durch DoT) — mit GoettlicherSegen-Rettung + OnDeath-Event
            for (var i = attackerField.Count - 1; i >= 0; i--)
                ResolveDeathAt(attackerField, i, attackerIsPlayer);

            for (var i = 0; i < attackerField.Count; i++)
            {
                var attacker = attackerField[i];
                if (attacker.CurrentHealth <= 0) continue;
                var (def, _) = ResolveDefinition(attacker.CardInstanceId);
                if (def == null) continue;

                // Status-Effekt blockt Aktion? (Schlaf/Frozen/Stunned)
                if (StatusEffectHelpers.IsBlocked(attacker.StatusEffects)) continue;

                var damage = attacker.CurrentAttack;
                if (defenderField.Count > 0)
                {
                    var target = defenderField[0];
                    var (targetDef, _) = ResolveDefinition(target.CardInstanceId);
                    var multiplier = targetDef != null ? ElementMatchup.GetMultiplier(def.Element, targetDef.Element) : 1f;
                    // K12: Element-Rune verstaerkt den Schaden des passenden Elements (+X%).
                    var elemRune = 1f + (attackerLoadout?.ElementBonusFor(def.Element) ?? 0f) / 100f;
                    var dealt = (int)(damage * multiplier * elemRune);
                    target.CurrentHealth -= dealt;
                    ApplyLifesteal(attackerPassiv, attackerIsPlayer, dealt);
                    ResolveDeathAt(defenderField, 0, !attackerIsPlayer);
                }
                else
                {
                    var heroDmg = (int)(damage * (1f + (attackerLoadout?.ElementBonusFor(def.Element) ?? 0f) / 100f));
                    if (attackerIsPlayer) State.EnemyHeroHp -= heroDmg; else State.PlayerHeroHp -= heroDmg;
                    ApplyLifesteal(attackerPassiv, attackerIsPlayer, heroDmg);
                }

                attacker.TurnsUntilSpecial = Math.Max(0, attacker.TurnsUntilSpecial - 1);
                if (attacker.TurnsUntilSpecial == 0 && def.BaseAbility != null)
                {
                    TriggerSpecial(attacker, def, attackerField, defenderField, attackerIsPlayer);
                    attacker.TurnsUntilSpecial = EffectiveTurnsToSpecial(def, attackerLoadout);   // K12: Geschwindigkeits-Rune
                }
            }

            // Boss-Phase 2 ab 50 % Helden-HP der Gegnerseite (Spielplan v5 Kap. 9.4):
            // Nur in Boss-Encountern (Mini-Boss/World-Boss) und nur einmal pro Kampf.
            if (State.IsBossEncounter
                && attackerIsPlayer
                && State.EnemyHeroHp > 0
                && State.EnemyHeroMaxHp > 0
                && State.EnemyHeroHp * 2 < State.EnemyHeroMaxHp   // < 50%
                && !_bossPhase2Triggered)
            {
                _bossPhase2Triggered = true;
                State.BossPhase2Active = true;
                ApplyBossPhase2();
            }

            State.CurrentTurn++;

            // Status-Effekt-Dauer reduzieren und abgelaufene entfernen
            foreach (var slot in attackerField) StatusEffectHelpers.TickAndExpire(slot.StatusEffects);
            foreach (var slot in defenderField) StatusEffectHelpers.TickAndExpire(slot.StatusEffects);

            // Waldlaeufer-Reset: jede Runde wieder neu
            if (State.PlayerHeroPassiv != null) State.PlayerHeroPassiv.FirstCardThisTurnPlayed = false;
            if (State.EnemyHeroPassiv != null)  State.EnemyHeroPassiv.FirstCardThisTurnPlayed  = false;

            if (State.Phase == BattlePhase.PlayerTurn)
            {
                State.Phase = BattlePhase.EnemyTurn;
                State.EnemyMana = State.EnemyMaxMana;            // Designplan v3 Kap. 7.3: 3 Orbs/Runde, kein Anstieg
                State.EnemyCardsPlayedThisTurn = 0;
                DrawCard(forPlayer: false);
            }
            else
            {
                State.Phase = BattlePhase.PlayerTurn;
                State.PlayerMana = State.PlayerMaxMana;
                State.PlayerCardsPlayedThisTurn = 0;
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
        /// Verarbeitet den moeglichen Tod der Karte an Position <paramref name="index"/>:
        /// prueft zuerst die GoettlicherSegen-Rettung der besitzenden Seite, emittiert sonst das
        /// OnDeath-Persoenlichkeits-Event und entfernt die Karte. Liefert true, wenn entfernt wurde.
        /// Zentralisiert die Tod-Behandlung fuer normale Angriffe, AoE-/Single-Skills und DoT-Ticks.
        /// </summary>
        private bool ResolveDeathAt(List<CardFieldSlot> field, int index, bool fieldOwnerIsPlayer)
        {
            if (index < 0 || index >= field.Count) return false;
            var slot = field[index];
            if (slot.CurrentHealth > 0) return false;
            if (TryGoettlicherSegenRescue(slot, field, fieldOwnerIsPlayer)) return false;
            var (def, _) = ResolveDefinition(slot.CardInstanceId);
            EmitOnDeathEvent(slot, def, fieldOwnerIsPlayer);
            field.RemoveAt(index);
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
            // Die gerade eingesetzte Karte ist der zuletzt hinzugefuegte Slot.
            var playedSlot = field.Count > 0 ? field[field.Count - 1] : null;
            const int synergyHpBonusPct = 5;   // Designplan v4 Kap. 8.2: +5% HP-Bonus
            foreach (var slot in field)
            {
                if (slot.CardInstanceId == "") continue;
                var (otherDef, _) = ResolveDefinition(slot.CardInstanceId);
                if (otherDef == null) continue;
                if (otherDef.Id == def.Id) continue;
                if (def.SynergyCardIds.Contains(otherDef.Id))
                {
                    // Bonus auf beide Synergie-Partner anwenden (nicht nur als Event melden).
                    if (playedSlot != null) ApplySynergyHpBonus(playedSlot, synergyHpBonusPct);
                    ApplySynergyHpBonus(slot, synergyHpBonusPct);
                    State.Events.Add(new BattleEvent(
                        BattleEventType.SynergyActivated, State.CurrentTurn, forPlayer,
                        cardInstanceId: slot.CardInstanceId,
                        cardDefinitionId: def.Id,
                        partnerCardId: otherDef.Id,
                        magnitude: synergyHpBonusPct));
                }
            }
        }

        /// <summary>Erhoeht Max- und Current-HP eines Slots um den prozentualen Synergie-Bonus.</summary>
        private static void ApplySynergyHpBonus(CardFieldSlot slot, int pct)
        {
            var bonus = Math.Max(1, slot.MaxHealth * pct / 100);
            slot.MaxHealth += bonus;
            slot.CurrentHealth += bonus;
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

        private void TriggerSpecial(CardFieldSlot caster, CardDefinition def, List<CardFieldSlot> allies, List<CardFieldSlot> enemies, bool forPlayer)
        {
            var ability = def.BaseAbility;
            if (ability == null) return;
            // enemies ist das gegnerische Feld des Casters -> dessen Owner ist die Gegenseite.
            var enemyFieldOwnerIsPlayer = !forPlayer;
            switch (ability.Category)
            {
                case AbilityCategory.Damage:
                    if (ability.TargetsAllEnemies)
                    {
                        var aoeDmg = Math.Max(1, caster.CurrentAttack * ability.Magnitude / 100);
                        for (var i = enemies.Count - 1; i >= 0; i--)
                        {
                            enemies[i].CurrentHealth -= aoeDmg;
                            ResolveDeathAt(enemies, i, enemyFieldOwnerIsPlayer);
                        }
                    }
                    else if (enemies.Count > 0)
                    {
                        var dmg = Math.Max(1, caster.CurrentAttack * ability.Magnitude / 100);
                        enemies[0].CurrentHealth -= dmg;
                        ResolveDeathAt(enemies, 0, enemyFieldOwnerIsPlayer);
                    }
                    break;
                case AbilityCategory.Defense:
                    if (ability.Magnitude > 0)
                    {
                        var heal = Math.Max(1, caster.MaxHealth * ability.Magnitude / 100);
                        caster.CurrentHealth = Math.Min(caster.CurrentHealth + heal, caster.MaxHealth);
                    }
                    break;
                case AbilityCategory.Buff:
                    if (ability.TargetsAllAllies)
                    {
                        foreach (var a in allies) a.CurrentAttack += Math.Max(1, a.CurrentAttack * ability.Magnitude / 100);
                    }
                    break;
                case AbilityCategory.Debuff:
                    if (ability.TargetsAllEnemies)
                    {
                        foreach (var e in enemies)
                        {
                            var reduce = Math.Max(1, e.CurrentAttack * ability.Magnitude / 100);
                            e.CurrentAttack = Math.Max(1, e.CurrentAttack - reduce);
                        }
                    }
                    break;
                case AbilityCategory.Control:
                    // Status-Effekt-Typ aus Element/Beschreibung ableiten
                    var effectType = InferStatusEffectFromCardElement(def.Element);
                    var duration = Math.Max(1, ability.DurationTurns > 0 ? ability.DurationTurns : 2);
                    var dotMag = (effectType == StatusEffectType.Poisoned || effectType == StatusEffectType.Burning)
                                ? Math.Max(50, ability.Magnitude)
                                : 0;
                    if (ability.TargetsAllEnemies)
                    {
                        foreach (var e in enemies)
                            StatusEffectHelpers.ApplyOrRefresh(e.StatusEffects, new StatusEffect(effectType, duration, dotMag, def.Id));
                    }
                    else if (enemies.Count > 0)
                    {
                        StatusEffectHelpers.ApplyOrRefresh(enemies[0].StatusEffects, new StatusEffect(effectType, duration, dotMag, def.Id));
                    }
                    break;
                case AbilityCategory.Synergy:
                    // Synergy-Bonus auf Allies anwenden (z.B. +X% ATK), mind. +1 gegen Integer-Truncation.
                    if (ability.TargetsAllAllies)
                    {
                        foreach (var a in allies) a.CurrentAttack += Math.Max(1, a.CurrentAttack * ability.Magnitude / 100);
                    }
                    break;
            }
        }

        /// <summary>
        /// Leitet aus dem Element der Karte den passenden Status-Effekt-Typ fuer Control-Skills ab.
        /// Designplan v4 Kap. 3.4 Element-Spezialeffekte.
        /// </summary>
        private static StatusEffectType InferStatusEffectFromCardElement(Element element) => element switch
        {
            // Designplan v4 Kap. 3.3 Element-Spezialeffekte (thematisch korrigiert):
            Element.Feuer  => StatusEffectType.Burning,     // Verbrennung (DoT)
            Element.Wasser => StatusEffectType.Frozen,      // Einfrierung (blockt Aktion)
            Element.Erde   => StatusEffectType.Stunned,     // Betaeubung (blockt Aktion)
            Element.Dunkel => StatusEffectType.Poisoned,    // Gift/Fluch — v4: Dunkel staerkt/verursacht Gift (DoT)
            Element.Natur  => StatusEffectType.Sleep,       // Wald-Ruhe: einschlaefern (Gift gehoert zu Dunkel)
            Element.Licht  => StatusEffectType.Silence,     // Blendung/Stille (Licht ist kein Gift-/Verlangsamungs-Element)
            _              => StatusEffectType.Stunned
        };

        private void ApplyBossPhase2()
        {
            // Spielplan v5 Kap. 9.4: Boss-Phase 2 — 3 Effekte:
            // 1) Boss-Karten erhalten passive Faehigkeit (+200 ATK fuer alle Karten im Feld)
            // 2) 2-3 starke Verstaerkungs-Karten kommen ins Feld
            // 3) Visuelles Signal via BattleEvent

            // 1) Passive Faehigkeit — Plan-Wert ist +200 absolute ATK; wir nehmen +50% als Skalen-sicheren Fallback
            //    UND zusaetzlich +200 absolute ATK wo verfuegbar.
            foreach (var slot in State.EnemyField)
            {
                slot.CurrentAttack += Math.Max(200, slot.CurrentAttack / 2);
            }

            // 2) Verstaerkungs-Karten ins Feld stellen (max. so viele wie freier Platz, bis zu Plan-Vorgabe von 3)
            const int maxReinforcements = 3;
            var addedCount = 0;
            foreach (var reinforcementDefId in State.BossPhase2ReinforcementCardIds)
            {
                if (addedCount >= maxReinforcements) break;
                if (State.EnemyField.Count >= 5) break;
                if (!_cardDefinitions.TryGetValue(reinforcementDefId, out var def)) continue;

                var instId = $"boss_reinforcement_{State.CurrentTurn}_{addedCount}";
                var mul = State.EnemyStatMultiplier;
                var slot = new CardFieldSlot(
                    cardInstanceId: instId,
                    currentAttack: (int)(def.BaseAttack * mul),
                    currentHealth: (int)(def.BaseHealth * mul),
                    turnsUntilSpecial: def.TurnsToSpecial);
                State.EnemyField.Add(slot);
                addedCount++;
            }

            // 3) Battle-Event so die UI eine Phasen-Animation triggern kann
            State.Events.Add(new BattleEvent(
                eventType: BattleEventType.BossPhaseChange,
                turn: State.CurrentTurn,
                forPlayer: false,
                localizationKey: State.BossPhase2PassiveKey ?? "boss_phase_2_passive",
                magnitude: addedCount));
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
