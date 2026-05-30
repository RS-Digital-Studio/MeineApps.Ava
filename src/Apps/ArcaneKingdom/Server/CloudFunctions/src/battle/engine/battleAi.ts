// Greedy-Gegner-AI — Portierung von Unity/Assets/_Project/Scripts/Game/Battle/BattleAI.cs.
//
// Der Server braucht diese AI, weil der GEGNER-Zug NICHT im Spieler-Aktions-Log steht, sondern
// von der AI erzeugt wird. Damit der Replay denselben Kampfverlauf reproduziert, muss die
// Server-AI bit-genau dieselben Karten in derselben Reihenfolge waehlen wie der Client.
//
// ==================== DETERMINISMUS-WARNUNG (Sortierung) ====================
// C# `List<T>.Sort` ist ein INSTABILER Introsort. JS `Array.prototype.sort` ist seit ES2019
// STABIL. Bei GLEICHEN Scores (Ties) koennen C# und JS daher eine ANDERE Reihenfolge liefern.
// Solange die Scores eindeutig sind, ist das irrelevant; bei Ties ist es ein DIVERGENZ-RISIKO.
// Dieser Port repliziert KEINEN der beiden Sort-Algorithmen exakt — er nutzt die Score-Vergleichs-
// funktion 1:1, aber die Tie-Aufloesung kann abweichen. Vor dem Scharfschalten des Replays MUSS
// dies gegen die echte C#-AI cross-getestet werden (siehe Abschlussbericht/REPLAY_VALIDATION).
// ============================================================================

import { BattleEngine } from "./battleEngine";
import { getMultiplier } from "./elementMatchup";
import { Element, type CardDefinition, type CardInstance } from "./types";

export class BattleAi {
  private readonly cardDefinitions: ReadonlyMap<string, CardDefinition>;
  private readonly cardInstances: ReadonlyMap<string, CardInstance>;

  constructor(defs: ReadonlyMap<string, CardDefinition>, instances: ReadonlyMap<string, CardInstance>) {
    this.cardDefinitions = defs;
    this.cardInstances = instances;
  }

  /**
   * Empfiehlt eine geordnete Liste von Karten zum Ausspielen. Der Aufrufer wendet jede
   * Empfehlung der Reihe nach an (Mana-Check passiert in der Engine).
   */
  chooseCardsToPlay(
    handInstanceIds: ReadonlyArray<string>,
    availableMana: number,
    dominantEnemyElement: Element | null = null
  ): string[] {
    const playable: { id: string; def: CardDefinition; score: number }[] = [];
    for (const instId of handInstanceIds) {
      const inst = this.cardInstances.get(instId);
      if (inst == null) continue;
      const def = this.cardDefinitions.get(inst.cardDefinitionId);
      if (def == null) continue;
      playable.push({ id: instId, def, score: BattleAi.scoreCard(def, dominantEnemyElement) });
    }

    // C#: playable.Sort((a, b) => b.score.CompareTo(a.score))  (absteigend nach Score).
    // Siehe DETERMINISMUS-WARNUNG oben bzur Tie-Behandlung.
    playable.sort((a, b) => BattleAi.compareDescending(a.score, b.score));

    const result: string[] = [];
    let remainingMana = availableMana;
    let cardsPlayed = 0;
    for (const p of playable) {
      if (remainingMana < BattleEngine.ManaPerCard) break;
      if (p.def.cost > BattleEngine.HeavyCardCostThreshold && cardsPlayed > 0) continue;
      result.push(p.id);
      remainingMana -= BattleEngine.ManaPerCard;
      cardsPlayed++;
    }
    return result;
  }

  /** Bildet `b.score.CompareTo(a.score)` (absteigend) nach: -1 / 0 / 1, NaN-sicher wie C# double.CompareTo. */
  private static compareDescending(aScore: number, bScore: number): number {
    if (bScore < aScore) return -1;
    if (bScore > aScore) return 1;
    return 0;
  }

  private static scoreCard(def: CardDefinition, dominantEnemyElement: Element | null): number {
    // C# rechnet hier in `float` (single precision)! Siehe Risiko-Hinweis im Abschlussbericht.
    let statValue = def.baseAttack + def.baseHealth;

    if (dominantEnemyElement != null) {
      statValue *= getMultiplier(def.element, dominantEnemyElement);
    }

    // C#: statValue *= 1f + (1f / Math.Max(1, def.TurnsToSpecial)) * 0.2f;
    statValue *= 1 + (1 / Math.max(1, def.turnsToSpecial)) * 0.2;

    // C#: statValue *= 1f + (int)def.Rarity * 0.02f;
    statValue *= 1 + def.rarity * 0.02;

    return statValue;
  }
}
