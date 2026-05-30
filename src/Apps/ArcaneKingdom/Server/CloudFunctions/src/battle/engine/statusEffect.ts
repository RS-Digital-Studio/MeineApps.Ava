// Status-Effekt-System — 1:1-Portierung von
// Unity/Assets/_Project/Scripts/Domain/Battle/StatusEffect.cs.
//
// Bildet StatusEffect (Type/Magnitude/RemainingTurns/SourceCardId) und die statischen Helpers ab.
// Die Helper-Iterationsreihenfolge ist identisch zur C#-List<>-Reihenfolge (Index-basiert),
// damit DoT-Summen und ApplyOrRefresh-Ersetzung deterministisch bleiben.

import { StatusEffectType } from "./types";

export class StatusEffect {
  readonly type: StatusEffectType;
  readonly magnitude: number;
  remainingTurns: number;
  readonly sourceCardId: string | null;

  constructor(
    type: StatusEffectType,
    remainingTurns: number,
    magnitude = 0,
    sourceCardId: string | null = null
  ) {
    this.type = type;
    this.magnitude = magnitude;
    this.remainingTurns = remainingTurns;
    this.sourceCardId = sourceCardId;
  }

  /** True, wenn der Effekt die normale Aktion blockt (Sleep/Frozen/Stunned). */
  get blocksAction(): boolean {
    return (
      this.type === StatusEffectType.Sleep ||
      this.type === StatusEffectType.Frozen ||
      this.type === StatusEffectType.Stunned
    );
  }

  /** True, wenn der Effekt nur Skills blockt (Silence), den normalen Angriff aber erlaubt. */
  get blocksSkills(): boolean {
    return this.type === StatusEffectType.Silence;
  }

  /** True, wenn der Effekt pro Runde Schaden verursacht (Poisoned/Burning). */
  get isDamageOverTime(): boolean {
    return this.type === StatusEffectType.Poisoned || this.type === StatusEffectType.Burning;
  }
}

export function hasEffect(effects: ReadonlyArray<StatusEffect>, type: StatusEffectType): boolean {
  for (let i = 0; i < effects.length; i++) if (effects[i].type === type) return true;
  return false;
}

export function isBlocked(effects: ReadonlyArray<StatusEffect>): boolean {
  for (let i = 0; i < effects.length; i++) if (effects[i].blocksAction) return true;
  return false;
}

export function isSilenced(effects: ReadonlyArray<StatusEffect>): boolean {
  for (let i = 0; i < effects.length; i++) if (effects[i].blocksSkills) return true;
  return false;
}

/** Summiert alle DoT-Effekte (Schaden pro Runde) am Rundenanfang. */
export function tickDamageOverTime(effects: ReadonlyArray<StatusEffect>): number {
  let total = 0;
  for (let i = 0; i < effects.length; i++) if (effects[i].isDamageOverTime) total += effects[i].magnitude;
  return total;
}

/** Reduziert die Dauer aller Effekte um 1 und entfernt abgelaufene (Rueckwaerts-Iteration wie C#). */
export function tickAndExpire(effects: StatusEffect[]): void {
  for (let i = effects.length - 1; i >= 0; i--) {
    effects[i].remainingTurns--;
    if (effects[i].remainingTurns <= 0) effects.splice(i, 1);
  }
}

/**
 * Fuegt einen Effekt hinzu oder ersetzt einen bestehenden gleichen Typs, wenn die neue Dauer
 * STRIKT laenger ist (C#-Semantik: `>`). Bei gleicher/kuerzerer Dauer bleibt der alte Effekt.
 */
export function applyOrRefresh(effects: StatusEffect[], newEffect: StatusEffect): void {
  for (let i = 0; i < effects.length; i++) {
    if (effects[i].type === newEffect.type) {
      if (newEffect.remainingTurns > effects[i].remainingTurns) effects[i] = newEffect;
      return;
    }
  }
  effects.push(newEffect);
}
