// Domaenen-Enums und Definitions-Typen — 1:1-Portierung der fuer die BattleEngine relevanten
// C#-Felder aus Unity/Assets/_Project/Scripts/Domain/Cards/* und /Hero/*.
//
// WICHTIG: Die numerischen Enum-Werte MUESSEN exakt den C#-Werten entsprechen, weil die Engine
// teilweise auf dem Integer-Wert rechnet (z.B. Rarity-Score in der AI: `(int)def.Rarity * 0.02`).

/** Element.cs — sechs Elemente des Doppel-Dreieck-Systems. */
export enum Element {
  Feuer = 0,
  Wasser = 1,
  Natur = 2,
  Erde = 3,
  Dunkel = 4,
  Licht = 5,
}

/** Race.cs — fuenf Rassen. */
export enum Race {
  Ritter = 0,
  Goetter = 1,
  Elfen = 2,
  Tiergeister = 3,
  Daemonen = 4,
}

/** Rarity.cs — sechs Seltenheitsstufen. */
export enum Rarity {
  Gewoehnlich = 0,
  Ungewoehnlich = 1,
  Selten = 2,
  Epic = 3,
  Legendaer = 4,
  Mythisch = 5,
}

/** AbilityDefinition.cs — AbilityType. */
export enum AbilityType {
  Passive = 0,
  ActiveOnSpecial = 1,
}

/** AbilityDefinition.cs — AbilityCategory. */
export enum AbilityCategory {
  Damage = 0,
  Defense = 1,
  Control = 2,
  Buff = 3,
  Debuff = 4,
  Synergy = 5,
}

/** HeroFaehigkeitsTyp.cs — eine Passiv pro Rasse. */
export enum HeroFaehigkeitsTyp {
  KoeniglicheAura = 0, // Ritter: +X% HP
  GoettlicherSegen = 1, // Goetter: 1x Tod verhindern
  Waldlaeufer = 2, // Elfen: erste Karte/Runde gratis
  Rudelbund = 3, // Tiergeister: +X% ATK je Tiergeist
  LebensraubAura = 4, // Daemonen: X% Schaden heilt Held
}

/** StatusEffect.cs — StatusEffectType. */
export enum StatusEffectType {
  Sleep = 0,
  Silence = 1,
  Frozen = 2,
  Stunned = 3,
  Poisoned = 4,
  Burning = 5,
  Slowed = 6,
  Rooted = 7,
}

/** BattleState.cs — BattlePhase. */
export enum BattlePhase {
  Setup = 0,
  PlayerTurn = 1,
  EnemyTurn = 2,
  TurnEnd = 3,
  Settlement = 4,
}

/** BattleState.cs — BattleResult. */
export enum BattleResult {
  Undecided = 0,
  PlayerWins = 1,
  EnemyWins = 2,
  Draw = 3,
}

/** BattleEvent.cs — BattleEventType. */
export enum BattleEventType {
  CardPlayed = 0,
  CardVictory = 1,
  CardDied = 2,
  SynergyActivated = 3,
  RivalryClashed = 4,
  HeroPassivTriggered = 5,
  BossPhaseChange = 6,
}

/**
 * AbilityDefinition.cs — nur die fuer die Engine relevanten Felder.
 * Wird vom Replay-Eingabeformat als reines Daten-Objekt geliefert.
 */
export interface AbilityDefinition {
  id: string;
  category: AbilityCategory;
  /** Schaden/Heilung/Buff in Prozent (Engine rechnet `* magnitude / 100`). */
  magnitude: number;
  /** Status-Effekt-Dauer fuer Control-Skills. */
  durationTurns: number;
  targetsAllAllies: boolean;
  targetsAllEnemies: boolean;
}

/**
 * CardDefinition.cs — nur die fuer die Engine relevanten Stammdaten.
 * Identitaet, Klassifikation, Basis-Stats, Faehigkeit und die Persoenlichkeits-/Synergie-Felder.
 */
export interface CardDefinition {
  id: string;
  element: Element;
  rarity: Rarity;
  race: Race;
  cost: number;
  baseAttack: number;
  baseHealth: number;
  turnsToSpecial: number;
  /** Skill 1 (Awakening) — die einzige von der Engine ausgewertete Faehigkeit. */
  baseAbility: AbilityDefinition | null;
  /** Persoenlichkeits-Dialog-Keys (nur fuer Events, beeinflussen das Kampf-Ergebnis nicht). */
  onPlayLineKey?: string | null;
  onVictoryLineKey?: string | null;
  onDeathLineKey?: string | null;
  /** Rivalen/Synergie-Karten-IDs (Synergie wendet HP-Bonus an, ist also ergebnisrelevant!). */
  rivalCardIds?: string[];
  synergyCardIds?: string[];
}

/**
 * CardInstance.cs — nur Level (woraus der StatBonusMultiplier folgt) und CardDefinitionId.
 * Der Server bekommt das Level pro Instanz aus dem Deck-Snapshot.
 */
export interface CardInstance {
  instanceId: string;
  cardDefinitionId: string;
  /** 0..15. Bestimmt den StatBonusMultiplier (siehe statBonusMultiplier()). */
  level: number;
}

/**
 * CardInstance.StatBonusMultiplier (C#) — EXAKTE Tabelle (DESIGN.md 5.3).
 * Diese float-Werte gehen 1:1 in die int-Truncation der Engine ein und sind damit ergebnisrelevant.
 */
export function statBonusMultiplier(level: number): number {
  switch (level) {
    case 0:
      return 1.0;
    case 1:
      return 1.05;
    case 2:
      return 1.1;
    case 3:
      return 1.15;
    case 4:
      return 1.2;
    case 5:
      return 1.25;
    case 6:
      return 1.3;
    case 7:
      return 1.35;
    case 8:
      return 1.4;
    case 9:
      return 1.5;
    case 10:
      return 1.55;
    case 11:
      return 1.58;
    case 12:
      return 1.63;
    case 13:
      return 1.68;
    case 14:
      return 1.75;
    case 15:
      return 1.8;
    default:
      return 1.0;
  }
}
