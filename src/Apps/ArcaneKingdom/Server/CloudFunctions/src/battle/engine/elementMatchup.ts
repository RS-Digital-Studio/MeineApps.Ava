// Element-Effektivitaets-Matrix — 1:1-Portierung von
// Unity/Assets/_Project/Scripts/Domain/Battle/ElementMatchup.cs.
//
// Doppel-Dreieck (Designplan v4 Kap. 3):
//   Physisch: Feuer -> Natur -> Wasser -> Feuer
//   Magisch:  Licht -> Dunkel -> Erde -> Licht
//   Stark = 1.10x, Schwach = 0.90x, Cross-Dreieck = 1.00x (neutral).
//
// Determinismus-Hinweis: Die Multiplier sind exakt dieselben float-Konstanten wie in C#
// (1.0 / 1.10 / 0.90). Sie gehen anschliessend durch `(int)(damage * multiplier)` (Truncation).

import { Element } from "./types";

export const NEUTRAL_MULTIPLIER = 1.0;
export const STRONG_MULTIPLIER = 1.1;
export const WEAK_MULTIPLIER = 0.9;

/** Liefert den Schadens-Multiplikator des Angreifer-Elements gegen das Verteidiger-Element. */
export function getMultiplier(attacker: Element, defender: Element): number {
  // Physisches Dreieck: Feuer -> Natur -> Wasser -> Feuer
  if (attacker === Element.Feuer && defender === Element.Natur) return STRONG_MULTIPLIER;
  if (attacker === Element.Natur && defender === Element.Wasser) return STRONG_MULTIPLIER;
  if (attacker === Element.Wasser && defender === Element.Feuer) return STRONG_MULTIPLIER;
  if (attacker === Element.Natur && defender === Element.Feuer) return WEAK_MULTIPLIER;
  if (attacker === Element.Wasser && defender === Element.Natur) return WEAK_MULTIPLIER;
  if (attacker === Element.Feuer && defender === Element.Wasser) return WEAK_MULTIPLIER;

  // Magisches Dreieck: Licht -> Dunkel -> Erde -> Licht
  if (attacker === Element.Licht && defender === Element.Dunkel) return STRONG_MULTIPLIER;
  if (attacker === Element.Dunkel && defender === Element.Erde) return STRONG_MULTIPLIER;
  if (attacker === Element.Erde && defender === Element.Licht) return STRONG_MULTIPLIER;
  if (attacker === Element.Dunkel && defender === Element.Licht) return WEAK_MULTIPLIER;
  if (attacker === Element.Erde && defender === Element.Dunkel) return WEAK_MULTIPLIER;
  if (attacker === Element.Licht && defender === Element.Erde) return WEAK_MULTIPLIER;

  // Verschiedene Dreiecke: neutral
  return NEUTRAL_MULTIPLIER;
}

/** True, wenn das Element zum physischen Dreieck (Feuer/Wasser/Natur) gehoert. */
export function isPhysical(element: Element): boolean {
  return element === Element.Feuer || element === Element.Wasser || element === Element.Natur;
}

/** True, wenn das Element zum magischen Dreieck (Licht/Dunkel/Erde) gehoert. */
export function isMagical(element: Element): boolean {
  return element === Element.Licht || element === Element.Dunkel || element === Element.Erde;
}
