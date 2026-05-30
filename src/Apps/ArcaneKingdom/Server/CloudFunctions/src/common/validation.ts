// Gemeinsame Input-Sanitization fuer alle Callables (M34).
// Validiert Typen, Ranges und String-Laengen, bevor Werte in DB-Pfade oder
// Berechnungen fliessen. Wirft bei Verstoessen einen `invalid-argument`-Fehler.

import { HttpsError } from "firebase-functions/v2/https";

// Erlaubte Zeichen fuer Werte, die als RTDB-/Firestore-Pfad-Segment dienen.
// Firebase verbietet in Keys: . $ # [ ] / sowie Steuerzeichen. Wir sind strenger
// und lassen nur alphanumerisch plus - _ zu, um Pfad-Injektion auszuschliessen.
const SAFE_ID_REGEX = /^[A-Za-z0-9_-]+$/;

/** Stellt sicher, dass `value` ein nicht-leerer String mit erlaubter Maximallaenge ist. */
export function requireString(value: unknown, field: string, maxLength: number, minLength = 1): string {
  if (typeof value !== "string") {
    throw new HttpsError("invalid-argument", `${field} muss ein String sein.`);
  }
  const trimmed = value.trim();
  if (trimmed.length < minLength) {
    throw new HttpsError("invalid-argument", `${field} ist zu kurz (min. ${minLength}).`);
  }
  if (trimmed.length > maxLength) {
    throw new HttpsError("invalid-argument", `${field} ist zu lang (max. ${maxLength}).`);
  }
  return trimmed;
}

/**
 * Wie {@link requireString}, erlaubt aber zusaetzlich nur pfad-sichere Zeichen.
 * Pflicht fuer alle Werte, die anschliessend in einen DB-Pfad interpoliert werden
 * (worldId, nodeId, productId, Card-Ids, …) — verhindert Pfad-Injektion.
 */
export function requirePathSafeId(value: unknown, field: string, maxLength = 128): string {
  const str = requireString(value, field, maxLength);
  if (!SAFE_ID_REGEX.test(str)) {
    throw new HttpsError("invalid-argument", `${field} enthaelt unerlaubte Zeichen.`);
  }
  return str;
}

/** Stellt sicher, dass `value` eine endliche Ganzzahl im Bereich [min, max] ist. */
export function requireInt(value: unknown, field: string, min: number, max: number): number {
  if (typeof value !== "number" || !Number.isFinite(value) || !Number.isInteger(value)) {
    throw new HttpsError("invalid-argument", `${field} muss eine Ganzzahl sein.`);
  }
  if (value < min || value > max) {
    throw new HttpsError("invalid-argument", `${field} ausserhalb des erlaubten Bereichs (${min}..${max}).`);
  }
  return value;
}

/**
 * Validiert ein String-Array (z.B. Deck-Karten-Ids): nicht leer, max. `maxItems`
 * Eintraege, jeder Eintrag pfad-sicher und max. `maxItemLength` Zeichen lang.
 */
export function requirePathSafeIdArray(
  value: unknown,
  field: string,
  maxItems: number,
  maxItemLength = 64
): string[] {
  if (!Array.isArray(value) || value.length === 0) {
    throw new HttpsError("invalid-argument", `${field} muss ein nicht-leeres Array sein.`);
  }
  if (value.length > maxItems) {
    throw new HttpsError("invalid-argument", `${field} hat zu viele Eintraege (max. ${maxItems}).`);
  }
  return value.map((item, i) => requirePathSafeId(item, `${field}[${i}]`, maxItemLength));
}
