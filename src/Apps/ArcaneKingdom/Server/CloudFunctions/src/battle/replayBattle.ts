// Server-seitiger deterministischer Battle-Replay (Anti-Cheat).
//
// Nimmt ein vollstaendiges Replay-Eingabeformat (Seed + Deck-Snapshot + Spieler-Aktions-Log +
// Gegner-Deck) und spielt den Kampf mit der portierten BattleEngine NACH. Liefert das Outcome
// (Sieg/Niederlage/Unentschieden) + die berechnete Rewards-Basis (Sterne).
//
// Der Replay reproduziert die Client-Kampf-Schleife (BattleScreen.OnEndTurnAsync + BattleBootstrap):
//   Pro Zug:
//     1. Spieler spielt die im Aktions-Log fuer diesen Zug gelisteten Karten (in Reihenfolge).
//     2. engine.endTurn()  -> wickelt Spieler-Combat ab, Phase wechselt zu EnemyTurn.
//     3. Gegner-AI waehlt Karten (chooseCardsToPlay) und spielt sie.
//     4. engine.endTurn()  -> wickelt Gegner-Combat ab, Phase wechselt zurueck zu PlayerTurn.
//   bis ein Ergebnis feststeht oder der Aktions-Log erschoepft ist.
//
// WICHTIG: Dieser Replay muss gegen die echte C#-Engine cross-getestet werden, bevor er als
// scharfe Ablehnungs-Instanz genutzt wird (siehe validateBattleResult.ts / REPLAY_VALIDATION_ENABLED).

import { BattleEngine } from "./engine/battleEngine";
import { BattleAi } from "./engine/battleAi";
import { BattleResult, BattlePhase, type CardDefinition, type CardInstance } from "./engine/types";
import { BattleState, HeroPassivContext } from "./engine/battleState";
import { HeroFaehigkeitsTyp } from "./engine/types";

/** Maximale Anzahl Zuege, die der Replay verarbeitet (Sicherheits-Limit gegen Endlos-Eingaben). */
const REPLAY_MAX_TURNS = BattleEngine.MaxTurns + 2;

/** Helden-Passiv-Konfiguration einer Seite (optional). */
export interface HeroPassivInput {
  passivType: HeroFaehigkeitsTyp;
  magnitude: number;
}

/** Ein Eintrag im Spieler-Aktions-Log: die in EINEM Zug gespielten Karten in Reihenfolge. */
export interface TurnActionLog {
  /** CardInstanceIds, die der Spieler in diesem Zug ausspielt (Reihenfolge ist relevant). */
  playedCardInstanceIds: string[];
}

/** Vollstaendiges Replay-Eingabeformat. Der Server spielt damit den Kampf nach. */
export interface BattleReplayInput {
  /** PRNG-Seed — MUSS identisch zu dem sein, mit dem der Client gespielt hat. */
  seed: number;

  /** Karten-Stammdaten (alle im Kampf vorkommenden CardDefinitions), per defId indiziert. */
  cardDefinitions: CardDefinition[];

  /**
   * Spieler-Karten-Instanzen mit Level (bestimmt den StatBonusMultiplier).
   * Schluessel ist die instanceId, wie sie im Deck/Aktions-Log referenziert wird.
   */
  playerCardInstances: CardInstance[];

  /** Spieler-Deck als geordnete Liste von CardInstanceIds (vor dem deterministischen Shuffle). */
  playerDeckInstanceIds: string[];

  /** Gegner-Deck als geordnete Liste von CardInstanceIds (synthetische Gegner-Instanzen). */
  enemyDeckInstanceIds: string[];

  /**
   * Gegner-Karten-Instanzen mit Level. Der Client erzeugt diese synthetisch (Level 0) aus den
   * Node-EnemyDeckCardIds; der Server muss dieselben Instanzen kennen, um Stats aufzuloesen.
   */
  enemyCardInstances: CardInstance[];

  /** Spieler-Aktions-Log: pro Zug die gespielten Karten in Reihenfolge. */
  playerActionLog: TurnActionLog[];

  /** Helden-Passiv des Spielers (optional). */
  playerHeroPassiv?: HeroPassivInput | null;
  /** Helden-Passiv des Gegners (optional). */
  enemyHeroPassiv?: HeroPassivInput | null;

  /** Start-HP des Spieler-Helden (Client: 1000). */
  playerHeroHp: number;
  /** Start-HP des Gegner-Helden (Client: 1000 * EnemyStatMultiplier, gerundet). */
  enemyHeroHp: number;

  /** Schwierigkeits-Multiplier fuer Gegner-Karten-Stats (Classic 1.0 / Amateur 1.25 / Profi 1.6 / Gott 2.2). */
  enemyStatMultiplier: number;

  /** Boss-Encounter-Flag (aktiviert Boss-Phase-2-Mechanik). */
  isBossEncounter?: boolean;
  /** Boss-Phase-2 Verstaerkungs-Karten-Definition-IDs. */
  bossPhase2ReinforcementCardIds?: string[];
  /** Boss-Phase-2 Passiv-Lokalisierungs-Key. */
  bossPhase2PassiveKey?: string | null;
}

/** Ergebnis des Replays. */
export interface BattleReplayResult {
  outcome: BattleResult;
  /** Anzahl gespielter Zuege bis zum Ende (currentTurn der Engine). */
  turnsPlayed: number;
  /** Verbleibendes Spieler-Helden-HP (fuer Plausibilitaet/Reporting). */
  playerHeroHpRemaining: number;
  /** Verbleibendes Gegner-Helden-HP. */
  enemyHeroHpRemaining: number;
  /** True, wenn der Aktions-Log vor einem Ergebnis erschoepft war (unvollstaendiger Replay). */
  actionLogExhausted: boolean;
}

function toDefinitionMap(defs: CardDefinition[]): Map<string, CardDefinition> {
  const map = new Map<string, CardDefinition>();
  for (const def of defs) map.set(def.id, def);
  return map;
}

function toInstanceMap(...instanceLists: CardInstance[][]): Map<string, CardInstance> {
  const map = new Map<string, CardInstance>();
  for (const list of instanceLists) for (const inst of list) map.set(inst.instanceId, inst);
  return map;
}

function buildPassiv(input: HeroPassivInput | null | undefined): HeroPassivContext | null {
  if (input == null) return null;
  return new HeroPassivContext(input.passivType, input.magnitude);
}

/**
 * Spielt den Kampf deterministisch nach. Wirft NICHT bei inkonsistenten Eingaben, sondern
 * liefert das Engine-Ergebnis (inkl. actionLogExhausted-Flag) — die Validierung entscheidet,
 * wie damit umzugehen ist.
 */
export function replayBattle(input: BattleReplayInput): BattleReplayResult {
  const definitions = toDefinitionMap(input.cardDefinitions);
  const instances = toInstanceMap(input.playerCardInstances, input.enemyCardInstances);

  const state = new BattleState(input.seed, input.playerHeroHp, input.enemyHeroHp);
  state.enemyStatMultiplier = input.enemyStatMultiplier;
  state.playerHeroPassiv = buildPassiv(input.playerHeroPassiv);
  state.enemyHeroPassiv = buildPassiv(input.enemyHeroPassiv);
  state.isBossEncounter = input.isBossEncounter ?? false;
  state.bossPhase2PassiveKey = input.bossPhase2PassiveKey ?? null;
  if (input.bossPhase2ReinforcementCardIds) {
    for (const id of input.bossPhase2ReinforcementCardIds) state.bossPhase2ReinforcementCardIds.push(id);
  }

  const engine = new BattleEngine(state, definitions, instances);
  const ai = new BattleAi(definitions, instances);

  engine.setup(input.playerDeckInstanceIds, input.enemyDeckInstanceIds);

  let actionLogExhausted = false;
  for (let turnIndex = 0; turnIndex < REPLAY_MAX_TURNS; turnIndex++) {
    if (state.result !== BattleResult.Undecided) break;

    // Phase MUSS PlayerTurn sein (Engine wechselt nach jedem Enemy-EndTurn zurueck).
    if (state.phase !== BattlePhase.PlayerTurn) break;

    // 1) Spieler-Aktionen dieses Zuges aus dem Log.
    const log = turnIndex < input.playerActionLog.length ? input.playerActionLog[turnIndex] : null;
    if (log == null) {
      actionLogExhausted = true;
      break;
    }
    for (const cardId of log.playedCardInstanceIds) {
      engine.playCard(true, cardId);
    }

    // 2) Spieler-EndTurn -> Combat + Phasenwechsel zu EnemyTurn.
    engine.endTurn();
    if (state.result !== BattleResult.Undecided) break;

    // 3) Gegner-AI waehlt + spielt Karten.
    const enemyHandSnapshot = [...state.enemyHand];
    const picks = ai.chooseCardsToPlay(enemyHandSnapshot, state.enemyMana);
    for (const instId of picks) {
      engine.playCard(false, instId);
    }

    // 4) Gegner-EndTurn -> Combat + Phasenwechsel zurueck zu PlayerTurn.
    engine.endTurn();
  }

  return {
    outcome: state.result,
    turnsPlayed: state.currentTurn,
    playerHeroHpRemaining: state.playerHeroHp,
    enemyHeroHpRemaining: state.enemyHeroHp,
    actionLogExhausted,
  };
}
