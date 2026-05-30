// Battle-State + CardFieldSlot + HeroPassivContext + BattleEvent — 1:1-Portierung von
// Unity/Assets/_Project/Scripts/Domain/Battle/{BattleState,HeroPassivContext,BattleEvent}.cs.
//
// Determinismus-relevant:
//   - PlayerDeckQueue/EnemyDeckQueue sind FIFO-Queues (C# Queue<string>): enqueue() haengt an,
//     dequeue() nimmt das vorderste Element. Wir bilden das mit einem Array + Kopf-Index nach,
//     damit shift() (O(n)) nicht doch eine andere Reihenfolge erzeugt — Reihenfolge ist hier kritisch.
//   - MaxHealth wird im Ctor von CardFieldSlot auf CurrentHealth gesetzt (wie C#).

import {
  BattlePhase,
  BattleResult,
  BattleEventType,
  HeroFaehigkeitsTyp,
} from "./types";
import { StatusEffect } from "./statusEffect";

/** Karten-Feld-Slot mit aktuellen Kampf-Werten. */
export class CardFieldSlot {
  readonly cardInstanceId: string;
  currentAttack: number;
  currentHealth: number;
  maxHealth: number;
  turnsUntilSpecial: number;
  readonly statusEffects: StatusEffect[] = [];

  constructor(cardInstanceId: string, currentAttack: number, currentHealth: number, turnsUntilSpecial: number) {
    this.cardInstanceId = cardInstanceId;
    this.currentAttack = currentAttack;
    this.currentHealth = currentHealth;
    this.maxHealth = currentHealth; // C#-Ctor: MaxHealth = currentHealth
    this.turnsUntilSpecial = turnsUntilSpecial;
  }
}

/** Vorberechnete Helden-Passiv-Parameter pro Kampfseite. */
export class HeroPassivContext {
  readonly passivType: HeroFaehigkeitsTyp;
  readonly magnitude: number;
  beastSpiritCountInDeck: number;
  firstCardThisTurnPlayed: boolean;
  divineBlessingsRemaining: number;

  constructor(passivType: HeroFaehigkeitsTyp, magnitude: number, beastSpiritCountInDeck = 0) {
    this.passivType = passivType;
    this.magnitude = magnitude;
    this.beastSpiritCountInDeck = beastSpiritCountInDeck;
    this.firstCardThisTurnPlayed = false;
    // Wie C#: nur GoettlicherSegen startet mit Rettungen, sonst 0.
    this.divineBlessingsRemaining = passivType === HeroFaehigkeitsTyp.GoettlicherSegen ? magnitude : 0;
  }
}

/** Aufzeichnung eines Battle-Events fuer Replay/Reporting. */
export class BattleEvent {
  readonly eventType: BattleEventType;
  readonly turn: number;
  readonly forPlayer: boolean;
  readonly cardInstanceId: string | null;
  readonly cardDefinitionId: string | null;
  readonly localizationKey: string | null;
  readonly partnerCardId: string | null;
  readonly magnitude: number;

  constructor(
    eventType: BattleEventType,
    turn: number,
    forPlayer: boolean,
    opts: {
      cardInstanceId?: string | null;
      cardDefinitionId?: string | null;
      localizationKey?: string | null;
      partnerCardId?: string | null;
      magnitude?: number;
    } = {}
  ) {
    this.eventType = eventType;
    this.turn = turn;
    this.forPlayer = forPlayer;
    this.cardInstanceId = opts.cardInstanceId ?? null;
    this.cardDefinitionId = opts.cardDefinitionId ?? null;
    this.localizationKey = opts.localizationKey ?? null;
    this.partnerCardId = opts.partnerCardId ?? null;
    this.magnitude = opts.magnitude ?? 0;
  }
}

/**
 * FIFO-Queue ueber einem Array mit wanderndem Kopf-Index — bildet C# Queue<string> exakt nach
 * (enqueue haengt an, dequeue liefert das vorderste Element).
 */
export class StringQueue {
  private readonly items: string[] = [];
  private head = 0;

  enqueue(value: string): void {
    this.items.push(value);
  }

  dequeue(): string | undefined {
    if (this.head >= this.items.length) return undefined;
    return this.items[this.head++];
  }

  get count(): number {
    return this.items.length - this.head;
  }
}

/** Deterministischer Battle-State. Wird von der BattleEngine fortgeschritten. */
export class BattleState {
  readonly seed: number;
  currentTurn: number;
  playerHeroHp: number;
  enemyHeroHp: number;
  playerHeroMaxHp: number;
  enemyHeroMaxHp: number;
  playerMana: number;
  enemyMana: number;
  playerMaxMana: number;
  enemyMaxMana: number;
  playerCardsPlayedThisTurn: number;
  enemyCardsPlayedThisTurn: number;
  enemyStatMultiplier: number;
  isBossEncounter: boolean;
  readonly bossPhase2ReinforcementCardIds: string[] = [];
  bossPhase2PassiveKey: string | null;
  bossPhase2Active: boolean;
  readonly playerField: CardFieldSlot[] = [];
  readonly enemyField: CardFieldSlot[] = [];
  readonly playerHand: string[] = [];
  readonly enemyHand: string[] = [];
  readonly playerDeckQueue = new StringQueue();
  readonly enemyDeckQueue = new StringQueue();
  phase: BattlePhase;
  result: BattleResult;
  playerHeroPassiv: HeroPassivContext | null;
  enemyHeroPassiv: HeroPassivContext | null;
  readonly events: BattleEvent[] = [];

  constructor(seed: number, playerHeroHp: number, enemyHeroHp: number) {
    this.seed = seed;
    this.playerHeroHp = playerHeroHp;
    this.enemyHeroHp = enemyHeroHp;
    this.playerHeroMaxHp = playerHeroHp;
    this.enemyHeroMaxHp = enemyHeroHp;
    this.playerMana = 3;
    this.enemyMana = 3;
    this.playerMaxMana = 3;
    this.enemyMaxMana = 3;
    this.playerCardsPlayedThisTurn = 0;
    this.enemyCardsPlayedThisTurn = 0;
    this.enemyStatMultiplier = 1.0;
    this.isBossEncounter = false;
    this.bossPhase2PassiveKey = null;
    this.bossPhase2Active = false;
    this.phase = BattlePhase.Setup;
    this.result = BattleResult.Undecided;
    this.currentTurn = 1;
    this.playerHeroPassiv = null;
    this.enemyHeroPassiv = null;
  }
}
