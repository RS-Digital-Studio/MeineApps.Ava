// Deterministische Kampf-Engine — 1:1-Portierung von
// Unity/Assets/_Project/Scripts/Domain/Battle/BattleEngine.cs.
//
// DETERMINISMUS-DOKTRIN (kritisch fuer den Anti-Cheat-Replay):
//   - C#-`(int)x` trunciert Richtung Null -> hier Math.trunc().
//   - C#-Integer-Division `a * b / 100` (alle int) -> Math.trunc(a * b / 100).
//     Da a, b int sind und das Produkt < 2^53 bleibt, ist a*b in JS exakt; Math.trunc bildet
//     die C#-Ganzzahl-Division bit-genau nach.
//   - C#-`(int)(float * float)`: erst die float-Multiplikation (IEEE-754 double in JS, C# nutzt
//     hier `float`! -> siehe Risiko-Hinweis im Abschlussbericht), dann Truncation Richtung Null.
//   - List-Iterationsreihenfolge ist index-basiert und identisch zu C# List<>.
//
// Die Engine ist absichtlich state-mutierend wie das Original (kein funktionaler Rewrite),
// damit jede Reihenfolge-Entscheidung 1:1 nachvollziehbar bleibt.

import { DeterministicRng } from "./deterministicRng";
import {
  AbilityCategory,
  BattleEventType,
  BattlePhase,
  BattleResult,
  Element,
  HeroFaehigkeitsTyp,
  Race,
  StatusEffectType,
  type CardDefinition,
  type CardInstance,
  statBonusMultiplier,
} from "./types";
import { getMultiplier } from "./elementMatchup";
import { StatusEffect, applyOrRefresh, isBlocked, tickAndExpire, tickDamageOverTime } from "./statusEffect";
import { BattleEvent, BattleState, CardFieldSlot, HeroPassivContext } from "./battleState";

/** C#-`(int)`-Cast: Truncation Richtung Null. */
function toInt(value: number): number {
  return Math.trunc(value);
}

export class BattleEngine {
  static readonly MaxFieldSlots = 5;
  static readonly MaxHandSize = 5;
  static readonly MaxTurns = 50;
  /** Jede Karte kostet 1 Mana-Orb (Designplan v3 Kap. 7.3) — UNABHAENGIG vom COST-Wert. */
  static readonly ManaPerCard = 1;
  /** COST-Schwelle fuer "schwere" Karten: nur als erste Aktion der Runde einsetzbar. */
  static readonly HeavyCardCostThreshold = 30;

  readonly state: BattleState;
  private readonly cardDefinitions: ReadonlyMap<string, CardDefinition>;
  private readonly cardInstances: ReadonlyMap<string, CardInstance>;
  private readonly random: DeterministicRng;
  private bossPhase2Triggered = false;

  constructor(
    state: BattleState,
    cardDefinitions: ReadonlyMap<string, CardDefinition>,
    cardInstances?: ReadonlyMap<string, CardInstance>
  ) {
    this.state = state;
    this.cardDefinitions = cardDefinitions;
    this.cardInstances = cardInstances ?? new Map<string, CardInstance>();
    this.random = new DeterministicRng(state.seed);
  }

  setup(playerDeckInstanceIds: string[], enemyDeckInstanceIds: string[]): void {
    const playerDeck = [...playerDeckInstanceIds];
    const enemyDeck = [...enemyDeckInstanceIds];

    this.updateBeastSpiritCount(this.state.playerHeroPassiv, playerDeck);
    this.updateBeastSpiritCount(this.state.enemyHeroPassiv, enemyDeck);

    for (const id of this.shuffleDeterministic(playerDeck)) this.state.playerDeckQueue.enqueue(id);
    for (const id of this.shuffleDeterministic(enemyDeck)) this.state.enemyDeckQueue.enqueue(id);

    for (let i = 0; i < 4; i++) {
      if (this.state.playerDeckQueue.count > 0) this.state.playerHand.push(this.state.playerDeckQueue.dequeue()!);
      if (this.state.enemyDeckQueue.count > 0) this.state.enemyHand.push(this.state.enemyDeckQueue.dequeue()!);
    }
    this.state.phase = BattlePhase.PlayerTurn;
  }

  playCard(forPlayer: boolean, cardInstanceId: string): boolean {
    const hand = forPlayer ? this.state.playerHand : this.state.enemyHand;
    const field = forPlayer ? this.state.playerField : this.state.enemyField;
    if (!hand.includes(cardInstanceId)) return false;
    if (field.length >= BattleEngine.MaxFieldSlots) return false;

    const { def, statMultiplier } = this.resolveDefinition(cardInstanceId);
    if (def == null) return false;

    const heroPassiv = forPlayer ? this.state.playerHeroPassiv : this.state.enemyHeroPassiv;
    let hpMultiplier = 1.0;
    let atkMultiplier = 1.0;
    let manaCost = BattleEngine.ManaPerCard;

    if (heroPassiv != null) {
      if (heroPassiv.passivType === HeroFaehigkeitsTyp.Waldlaeufer && !heroPassiv.firstCardThisTurnPlayed) {
        manaCost = 0;
      }
      if (heroPassiv.passivType === HeroFaehigkeitsTyp.KoeniglicheAura) {
        hpMultiplier += heroPassiv.magnitude / 100;
      }
      if (heroPassiv.passivType === HeroFaehigkeitsTyp.Rudelbund) {
        atkMultiplier += (heroPassiv.magnitude * heroPassiv.beastSpiritCountInDeck) / 100;
      }
    }

    const availableMana = forPlayer ? this.state.playerMana : this.state.enemyMana;
    if (manaCost > availableMana) return false;

    const cardsPlayedThisTurn = forPlayer
      ? this.state.playerCardsPlayedThisTurn
      : this.state.enemyCardsPlayedThisTurn;
    if (def.cost > BattleEngine.HeavyCardCostThreshold && cardsPlayedThisTurn > 0) return false;

    if (forPlayer) this.state.playerMana -= manaCost;
    else this.state.enemyMana -= manaCost;
    if (forPlayer) this.state.playerCardsPlayedThisTurn++;
    else this.state.enemyCardsPlayedThisTurn++;
    if (heroPassiv != null && heroPassiv.passivType === HeroFaehigkeitsTyp.Waldlaeufer) {
      heroPassiv.firstCardThisTurnPlayed = true;
    }

    // hand.Remove(cardInstanceId): entfernt das ERSTE Vorkommen (C#-List.Remove-Semantik).
    const handIdx = hand.indexOf(cardInstanceId);
    if (handIdx >= 0) hand.splice(handIdx, 1);

    const difficultyMul = forPlayer ? 1.0 : this.state.enemyStatMultiplier;
    const finalHp = toInt(def.baseHealth * statMultiplier * hpMultiplier * difficultyMul);
    const finalAtk = toInt(def.baseAttack * statMultiplier * atkMultiplier * difficultyMul);
    field.push(new CardFieldSlot(cardInstanceId, finalAtk, finalHp, def.turnsToSpecial));

    if (def.onPlayLineKey) {
      this.state.events.push(
        new BattleEvent(BattleEventType.CardPlayed, this.state.currentTurn, forPlayer, {
          cardInstanceId,
          cardDefinitionId: def.id,
          localizationKey: def.onPlayLineKey,
        })
      );
    }

    this.triggerSynergyIfMatches(def, field, forPlayer);
    this.checkRivalryWithOpposingField(def, forPlayer);
    return true;
  }

  endTurn(): void {
    const attackerIsPlayer = this.state.phase === BattlePhase.PlayerTurn;
    const attackerField = attackerIsPlayer ? this.state.playerField : this.state.enemyField;
    const defenderField = attackerIsPlayer ? this.state.enemyField : this.state.playerField;
    const attackerPassiv = attackerIsPlayer ? this.state.playerHeroPassiv : this.state.enemyHeroPassiv;

    // DoT-Tick auf Attacker-Field VOR der Attack-Phase.
    for (const slot of attackerField) {
      const dot = tickDamageOverTime(slot.statusEffects);
      if (dot > 0) slot.currentHealth -= dot;
    }
    for (let i = attackerField.length - 1; i >= 0; i--) this.resolveDeathAt(attackerField, i, attackerIsPlayer);

    for (let i = 0; i < attackerField.length; i++) {
      const attacker = attackerField[i];
      if (attacker.currentHealth <= 0) continue;
      const { def } = this.resolveDefinition(attacker.cardInstanceId);
      if (def == null) continue;

      if (isBlocked(attacker.statusEffects)) continue;

      const damage = attacker.currentAttack;
      if (defenderField.length > 0) {
        const target = defenderField[0];
        const { def: targetDef } = this.resolveDefinition(target.cardInstanceId);
        const multiplier = targetDef != null ? getMultiplier(def.element, targetDef.element) : 1.0;
        const dealt = toInt(damage * multiplier);
        target.currentHealth -= dealt;
        this.applyLifesteal(attackerPassiv, attackerIsPlayer, dealt);
        this.resolveDeathAt(defenderField, 0, !attackerIsPlayer);
      } else {
        if (attackerIsPlayer) this.state.enemyHeroHp -= damage;
        else this.state.playerHeroHp -= damage;
        this.applyLifesteal(attackerPassiv, attackerIsPlayer, damage);
      }

      attacker.turnsUntilSpecial = Math.max(0, attacker.turnsUntilSpecial - 1);
      if (attacker.turnsUntilSpecial === 0 && def.baseAbility != null) {
        this.triggerSpecial(attacker, def, attackerField, defenderField, attackerIsPlayer);
        attacker.turnsUntilSpecial = def.turnsToSpecial;
      }
    }

    // Boss-Phase 2 ab < 50% Gegner-Helden-HP (nur in Boss-Encountern, einmal pro Kampf).
    if (
      this.state.isBossEncounter &&
      attackerIsPlayer &&
      this.state.enemyHeroHp > 0 &&
      this.state.enemyHeroMaxHp > 0 &&
      this.state.enemyHeroHp * 2 < this.state.enemyHeroMaxHp &&
      !this.bossPhase2Triggered
    ) {
      this.bossPhase2Triggered = true;
      this.state.bossPhase2Active = true;
      this.applyBossPhase2();
    }

    this.state.currentTurn++;

    for (const slot of attackerField) tickAndExpire(slot.statusEffects);
    for (const slot of defenderField) tickAndExpire(slot.statusEffects);

    if (this.state.playerHeroPassiv != null) this.state.playerHeroPassiv.firstCardThisTurnPlayed = false;
    if (this.state.enemyHeroPassiv != null) this.state.enemyHeroPassiv.firstCardThisTurnPlayed = false;

    if (this.state.phase === BattlePhase.PlayerTurn) {
      this.state.phase = BattlePhase.EnemyTurn;
      this.state.enemyMana = this.state.enemyMaxMana;
      this.state.enemyCardsPlayedThisTurn = 0;
      this.drawCard(false);
    } else {
      this.state.phase = BattlePhase.PlayerTurn;
      this.state.playerMana = this.state.playerMaxMana;
      this.state.playerCardsPlayedThisTurn = 0;
      this.drawCard(true);
    }

    const result = this.checkVictoryCondition();
    if (result !== BattleResult.Undecided) {
      this.state.result = result;
      this.state.phase = BattlePhase.Settlement;
      this.emitVictoryEvents(result);
    }
  }

  checkVictoryCondition(): BattleResult {
    if (this.state.playerHeroHp <= 0 && this.state.enemyHeroHp <= 0) return BattleResult.Draw;
    if (this.state.playerHeroHp <= 0) return BattleResult.EnemyWins;
    if (this.state.enemyHeroHp <= 0) return BattleResult.PlayerWins;
    if (this.state.currentTurn >= BattleEngine.MaxTurns) {
      return this.state.playerHeroHp > this.state.enemyHeroHp ? BattleResult.PlayerWins : BattleResult.EnemyWins;
    }
    return BattleResult.Undecided;
  }

  // ============================================================ Helden-Passiv

  private applyLifesteal(passiv: HeroPassivContext | null, attackerIsPlayer: boolean, damage: number): void {
    if (passiv == null) return;
    if (passiv.passivType !== HeroFaehigkeitsTyp.LebensraubAura) return;
    // C#: damage * Magnitude / 100  (Integer-Division).
    const heal = toInt((damage * passiv.magnitude) / 100);
    if (heal <= 0) return;
    if (attackerIsPlayer) this.state.playerHeroHp = Math.min(this.state.playerHeroHp + heal, 10_000);
    else this.state.enemyHeroHp = Math.min(this.state.enemyHeroHp + heal, 10_000);

    this.state.events.push(
      new BattleEvent(BattleEventType.HeroPassivTriggered, this.state.currentTurn, attackerIsPlayer, {
        localizationKey: "hero.daemonen.skill.name",
        magnitude: heal,
      })
    );
  }

  private tryGoettlicherSegenRescue(target: CardFieldSlot, _field: CardFieldSlot[], forPlayer: boolean): boolean {
    const passiv = forPlayer ? this.state.playerHeroPassiv : this.state.enemyHeroPassiv;
    if (passiv == null) return false;
    if (passiv.passivType !== HeroFaehigkeitsTyp.GoettlicherSegen) return false;
    if (passiv.divineBlessingsRemaining <= 0) return false;

    passiv.divineBlessingsRemaining--;
    target.currentHealth = 1;
    this.state.events.push(
      new BattleEvent(BattleEventType.HeroPassivTriggered, this.state.currentTurn, forPlayer, {
        cardInstanceId: target.cardInstanceId,
        localizationKey: "hero.goetter.skill.name",
        magnitude: 1,
      })
    );
    return true;
  }

  private resolveDeathAt(field: CardFieldSlot[], index: number, fieldOwnerIsPlayer: boolean): boolean {
    if (index < 0 || index >= field.length) return false;
    const slot = field[index];
    if (slot.currentHealth > 0) return false;
    if (this.tryGoettlicherSegenRescue(slot, field, fieldOwnerIsPlayer)) return false;
    const { def } = this.resolveDefinition(slot.cardInstanceId);
    this.emitOnDeathEvent(slot, def, fieldOwnerIsPlayer);
    field.splice(index, 1);
    return true;
  }

  private updateBeastSpiritCount(passiv: HeroPassivContext | null, deck: string[]): void {
    if (passiv == null) return;
    if (passiv.passivType !== HeroFaehigkeitsTyp.Rudelbund) return;
    let count = 0;
    for (const instanceId of deck) {
      const { def } = this.resolveDefinition(instanceId);
      if (def != null && def.race === Race.Tiergeister) count++;
    }
    passiv.beastSpiritCountInDeck = count;
  }

  // ============================================================ Personality-Events

  private triggerSynergyIfMatches(def: CardDefinition, field: CardFieldSlot[], forPlayer: boolean): void {
    if (def.synergyCardIds == null || def.synergyCardIds.length === 0) return;
    const playedSlot = field.length > 0 ? field[field.length - 1] : null;
    const synergyHpBonusPct = 5;
    for (const slot of field) {
      if (slot.cardInstanceId === "") continue;
      const { def: otherDef } = this.resolveDefinition(slot.cardInstanceId);
      if (otherDef == null) continue;
      if (otherDef.id === def.id) continue;
      if (def.synergyCardIds.includes(otherDef.id)) {
        if (playedSlot != null) BattleEngine.applySynergyHpBonus(playedSlot, synergyHpBonusPct);
        BattleEngine.applySynergyHpBonus(slot, synergyHpBonusPct);
        this.state.events.push(
          new BattleEvent(BattleEventType.SynergyActivated, this.state.currentTurn, forPlayer, {
            cardInstanceId: slot.cardInstanceId,
            cardDefinitionId: def.id,
            partnerCardId: otherDef.id,
            magnitude: synergyHpBonusPct,
          })
        );
      }
    }
  }

  private static applySynergyHpBonus(slot: CardFieldSlot, pct: number): void {
    // C#: Math.Max(1, slot.MaxHealth * pct / 100)  (Integer-Division).
    const bonus = Math.max(1, toInt((slot.maxHealth * pct) / 100));
    slot.maxHealth += bonus;
    slot.currentHealth += bonus;
  }

  private checkRivalryWithOpposingField(def: CardDefinition, forPlayer: boolean): void {
    if (def.rivalCardIds == null || def.rivalCardIds.length === 0) return;
    const enemyField = forPlayer ? this.state.enemyField : this.state.playerField;
    for (const slot of enemyField) {
      const { def: otherDef } = this.resolveDefinition(slot.cardInstanceId);
      if (otherDef == null) continue;
      if (def.rivalCardIds.includes(otherDef.id)) {
        this.state.events.push(
          new BattleEvent(BattleEventType.RivalryClashed, this.state.currentTurn, forPlayer, {
            cardDefinitionId: def.id,
            partnerCardId: otherDef.id,
          })
        );
      }
    }
  }

  private emitOnDeathEvent(slot: CardFieldSlot, def: CardDefinition | null, forPlayer: boolean): void {
    if (def == null) return;
    if (!def.onDeathLineKey) return;
    this.state.events.push(
      new BattleEvent(BattleEventType.CardDied, this.state.currentTurn, forPlayer, {
        cardInstanceId: slot.cardInstanceId,
        cardDefinitionId: def.id,
        localizationKey: def.onDeathLineKey,
      })
    );
  }

  private emitVictoryEvents(result: BattleResult): void {
    const winnerField = result === BattleResult.PlayerWins ? this.state.playerField : this.state.enemyField;
    const winnerIsPlayer = result === BattleResult.PlayerWins;
    for (const slot of winnerField) {
      if (slot.currentHealth <= 0) continue;
      const { def } = this.resolveDefinition(slot.cardInstanceId);
      if (def == null || !def.onVictoryLineKey) continue;
      this.state.events.push(
        new BattleEvent(BattleEventType.CardVictory, this.state.currentTurn, winnerIsPlayer, {
          cardInstanceId: slot.cardInstanceId,
          cardDefinitionId: def.id,
          localizationKey: def.onVictoryLineKey,
        })
      );
    }
  }

  // ============================================================ Spezial-Skills

  private drawCard(forPlayer: boolean): void {
    const hand = forPlayer ? this.state.playerHand : this.state.enemyHand;
    const deck = forPlayer ? this.state.playerDeckQueue : this.state.enemyDeckQueue;
    if (hand.length >= BattleEngine.MaxHandSize) return;
    if (deck.count > 0) hand.push(deck.dequeue()!);
  }

  private triggerSpecial(
    caster: CardFieldSlot,
    def: CardDefinition,
    allies: CardFieldSlot[],
    enemies: CardFieldSlot[],
    forPlayer: boolean
  ): void {
    const ability = def.baseAbility;
    if (ability == null) return;
    const enemyFieldOwnerIsPlayer = !forPlayer;
    switch (ability.category) {
      case AbilityCategory.Damage:
        if (ability.targetsAllEnemies) {
          const aoeDmg = Math.max(1, toInt((caster.currentAttack * ability.magnitude) / 100));
          for (let i = enemies.length - 1; i >= 0; i--) {
            enemies[i].currentHealth -= aoeDmg;
            this.resolveDeathAt(enemies, i, enemyFieldOwnerIsPlayer);
          }
        } else if (enemies.length > 0) {
          const dmg = Math.max(1, toInt((caster.currentAttack * ability.magnitude) / 100));
          enemies[0].currentHealth -= dmg;
          this.resolveDeathAt(enemies, 0, enemyFieldOwnerIsPlayer);
        }
        break;
      case AbilityCategory.Defense:
        if (ability.magnitude > 0) {
          const heal = Math.max(1, toInt((caster.maxHealth * ability.magnitude) / 100));
          caster.currentHealth = Math.min(caster.currentHealth + heal, caster.maxHealth);
        }
        break;
      case AbilityCategory.Buff:
        if (ability.targetsAllAllies) {
          for (const a of allies) a.currentAttack += Math.max(1, toInt((a.currentAttack * ability.magnitude) / 100));
        }
        break;
      case AbilityCategory.Debuff:
        if (ability.targetsAllEnemies) {
          for (const e of enemies) {
            const reduce = Math.max(1, toInt((e.currentAttack * ability.magnitude) / 100));
            e.currentAttack = Math.max(1, e.currentAttack - reduce);
          }
        }
        break;
      case AbilityCategory.Control: {
        const effectType = BattleEngine.inferStatusEffectFromCardElement(def.element);
        const duration = Math.max(1, ability.durationTurns > 0 ? ability.durationTurns : 2);
        const dotMag =
          effectType === StatusEffectType.Poisoned || effectType === StatusEffectType.Burning
            ? Math.max(50, ability.magnitude)
            : 0;
        if (ability.targetsAllEnemies) {
          for (const e of enemies)
            applyOrRefresh(e.statusEffects, new StatusEffect(effectType, duration, dotMag, def.id));
        } else if (enemies.length > 0) {
          applyOrRefresh(enemies[0].statusEffects, new StatusEffect(effectType, duration, dotMag, def.id));
        }
        break;
      }
      case AbilityCategory.Synergy:
        if (ability.targetsAllAllies) {
          for (const a of allies) a.currentAttack += Math.max(1, toInt((a.currentAttack * ability.magnitude) / 100));
        }
        break;
    }
  }

  private static inferStatusEffectFromCardElement(element: Element): StatusEffectType {
    switch (element) {
      case Element.Feuer:
        return StatusEffectType.Burning;
      case Element.Wasser:
        return StatusEffectType.Frozen;
      case Element.Natur:
        return StatusEffectType.Poisoned;
      case Element.Erde:
        return StatusEffectType.Stunned;
      case Element.Dunkel:
        return StatusEffectType.Silence;
      case Element.Licht:
        return StatusEffectType.Slowed;
      default:
        return StatusEffectType.Slowed;
    }
  }

  private applyBossPhase2(): void {
    // 1) Passive: +Max(200, ATK/2) ATK fuer alle Karten im Gegner-Feld.
    for (const slot of this.state.enemyField) {
      slot.currentAttack += Math.max(200, toInt(slot.currentAttack / 2));
    }

    // 2) Verstaerkungs-Karten ins Feld (max. 3, bis 5 Slots voll).
    const maxReinforcements = 3;
    let addedCount = 0;
    for (const reinforcementDefId of this.state.bossPhase2ReinforcementCardIds) {
      if (addedCount >= maxReinforcements) break;
      if (this.state.enemyField.length >= 5) break;
      const def = this.cardDefinitions.get(reinforcementDefId);
      if (def == null) continue;

      const instId = `boss_reinforcement_${this.state.currentTurn}_${addedCount}`;
      const mul = this.state.enemyStatMultiplier;
      const slot = new CardFieldSlot(instId, toInt(def.baseAttack * mul), toInt(def.baseHealth * mul), def.turnsToSpecial);
      this.state.enemyField.push(slot);
      addedCount++;
    }

    // 3) Battle-Event fuer die UI.
    this.state.events.push(
      new BattleEvent(BattleEventType.BossPhaseChange, this.state.currentTurn, false, {
        localizationKey: this.state.bossPhase2PassiveKey ?? "boss_phase_2_passive",
        magnitude: addedCount,
      })
    );
  }

  /**
   * Loest die CardDefinition + den StatBonusMultiplier (aus dem Karten-Level) auf — wie der
   * C#-`ResolveDefinition`. Zuerst direkter Treffer (instanceId == defId, z.B. Gegner-Synthese
   * mit defId-Schluessel), sonst ueber die CardInstance -> CardDefinitionId.
   */
  private resolveDefinition(cardInstanceId: string): { def: CardDefinition | null; statMultiplier: number } {
    const inst = this.cardInstances.get(cardInstanceId);
    const multiplier = inst != null ? statBonusMultiplier(inst.level) : 1.0;
    const defDirect = this.cardDefinitions.get(cardInstanceId);
    if (defDirect != null) return { def: defDirect, statMultiplier: multiplier };
    if (inst != null) {
      const defViaInst = this.cardDefinitions.get(inst.cardDefinitionId);
      if (defViaInst != null) return { def: defViaInst, statMultiplier: multiplier };
    }
    return { def: null, statMultiplier: multiplier };
  }

  private shuffleDeterministic(ids: string[]): string[] {
    const list = [...ids];
    // Fisher-Yates ABWAERTS — exakt wie C#: for (i = n-1; i > 0; i--) { j = rng.Next(i+1); swap(i,j); }
    for (let i = list.length - 1; i > 0; i--) {
      const j = this.random.next(i + 1);
      const tmp = list[i];
      list[i] = list[j];
      list[j] = tmp;
    }
    return list;
  }
}
