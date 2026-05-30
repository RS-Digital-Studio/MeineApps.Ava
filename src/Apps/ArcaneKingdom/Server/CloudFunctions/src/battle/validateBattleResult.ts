import { onCall, HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import { requirePathSafeId, requirePathSafeIdArray, requireInt } from "../common/validation";
import { replayBattle, type BattleReplayInput } from "./replayBattle";
import { BattleResult } from "./engine/types";

interface ValidateBattleResultRequest {
  worldId: string;
  nodeId: string;
  stars: number;
  seed: number;
  deckCardIds: string[];
  claimedGold: number;
  claimedExp: number;
  /**
   * Optionales Replay-Paket (Seed + Deck-Snapshot + Spieler-Aktions-Log + Gegner-Deck).
   * Wird vom Client (BattleScreen) mitgeschickt, sobald das Aktions-Logging implementiert ist.
   * Solange es fehlt, faellt die Validierung auf reine Plausibilitaet zurueck.
   */
  replay?: BattleReplayInput;
}

// ============================================================================
// REPLAY-SCHARFSCHALTUNG
//
// Der deterministische TS-Replay (replayBattle.ts) ist eine 1:1-Portierung der C#-BattleEngine,
// wurde aber NOCH NICHT gegen die echte C#-Engine cross-getestet. Solange dieser Cross-Test
// (gleiche Seeds/Eingaben -> bit-identische Outputs) nicht bestaetigt ist, darf der Replay
// einen vom Client gemeldeten Sieg NICHT blind ablehnen — sonst wuerden bei jeder noch so
// kleinen Determinismus-Abweichung (float-Rundung, Sort-Tie, Truncation) legitime Spieler
// faelschlich als Cheater behandelt.
//
// Verhalten:
//   REPLAY_VALIDATION_ENABLED = false (DEFAULT):
//     Replay laeuft nur als SCHATTEN-Lauf — Divergenzen werden geloggt, aber NICHT abgelehnt.
//     Es greift weiterhin die Plausibilitaets-Pruefung (Owner/Nonce/Reward-Caps).
//   REPLAY_VALIDATION_ENABLED = true (erst nach bestaetigtem Cross-Test setzen):
//     Replay ist autoritativ — meldet der Client einen Sieg, den der Replay nicht reproduziert,
//     wird der Kampf abgelehnt (Rewards = 0).
//
// Umschaltbar per Umgebungsvariable REPLAY_VALIDATION_ENABLED=true.
// ============================================================================
const REPLAY_VALIDATION_ENABLED = process.env.REPLAY_VALIDATION_ENABLED === "true";

interface ValidateBattleResultResponse {
  valid: boolean;
  awardedGold: number;
  awardedExp: number;
  rejectedReason?: string;
}

// Maximale Karten pro Deck (Cost-Cap 200 erlaubt praktisch nie mehr als das).
const MAX_DECK_SIZE = 30;
// Nonce-TTL gegen Seed-Replay: 7 Tage (siehe SERVEROPS.md 2.1).
const NONCE_TTL_MS = 7 * 24 * 60 * 60 * 1000;
// Plausibilitaets-Schranken fuer die claimed-Werte (nur fuer Logging, nicht autoritativ).
const MAX_PLAUSIBLE_GOLD = 1_000_000;
const MAX_PLAUSIBLE_EXP = 1_000_000;

// Anti-Cheat-Validierung fuer Welt-Kaempfe (C8/H27).
//
// Rewards werden SERVER-AUTORITATIV aus der WorldDefinition/NodeDefinition + Sternen
// berechnet — NICHT aus claimedGold/claimedExp (Trust-the-Client wird eliminiert).
// Die claimed-Werte dienen ausschliesslich als Plausibilitaets-Vergleich im Log.
//
// Der vollstaendige deterministische BattleEngine-Replay (Seed + Deck → Ergebnis)
// wird SEPARAT ergaenzt, sobald die C#-Engine nach TypeScript portiert ist. Bis dahin
// sichern wir ueber Owner-Pruefung, Seed-Nonce und server-autoritative Rewards ab.
export const validateBattleResult = onCall<ValidateBattleResultRequest, Promise<ValidateBattleResultResponse>>(
  { region: "europe-west1", timeoutSeconds: 30, memory: "256MiB", enforceAppCheck: true },
  async (request) => {
    const uid = request.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Login required.");

    // 1) Vollstaendige Input-Validierung (M34).
    const worldId = requirePathSafeId(request.data.worldId, "worldId", 64);
    const nodeId = requirePathSafeId(request.data.nodeId, "nodeId", 64);
    const stars = requireInt(request.data.stars, "stars", 1, 4);
    // Seed wird als Nonce-Key genutzt → pfad-sicher als String normalisieren.
    const seed = requireInt(request.data.seed, "seed", 0, Number.MAX_SAFE_INTEGER);
    const deckCardIds = requirePathSafeIdArray(request.data.deckCardIds, "deckCardIds", MAX_DECK_SIZE, 64);
    const claimedGold = requireInt(request.data.claimedGold, "claimedGold", 0, Number.MAX_SAFE_INTEGER);
    const claimedExp = requireInt(request.data.claimedExp, "claimedExp", 0, Number.MAX_SAFE_INTEGER);

    const db = admin.database();

    // 2) Owner-Pruefung: Alle Deck-Karten muessen dem Spieler gehoeren.
    const inventorySnap = await db.ref(`players/${uid}/cardInventory`).get();
    const inventory = (inventorySnap.val() ?? {}) as Record<string, unknown>;
    const foreignCard = deckCardIds.find((cardId) => !(cardId in inventory));
    if (foreignCard) {
      throw new HttpsError("permission-denied", `Karte gehoert dem Spieler nicht: ${foreignCard}`);
    }

    // 3) Seed-Nonce gegen Replay: pro (uid, seed) nur einmal akzeptieren.
    //    TTL ~7 Tage; abgelaufene Eintraege werden ueberschrieben.
    const nonceRef = db.ref(`battleNonces/${uid}/${seed}`);
    const nowMs = Date.now();
    const nonceResult = await nonceRef.transaction((current) => {
      const prev = current as { createdAtMs?: number } | null;
      if (prev && typeof prev.createdAtMs === "number" && nowMs - prev.createdAtMs < NONCE_TTL_MS) {
        return undefined; // Seed innerhalb der TTL bereits genutzt → Replay, abbrechen.
      }
      return { createdAtMs: nowMs, worldId, nodeId };
    });
    if (!nonceResult.committed) {
      throw new HttpsError("failed-precondition", "Seed wurde bereits verwendet (Replay-Schutz).");
    }

    // 4) Deterministischer BattleEngine-Replay (Anti-Cheat-Kern).
    //    Der Server spielt den Kampf mit (seed, Deck-Snapshot, Aktions-Log, Gegner-Deck) nach und
    //    leitet daraus das AUTORITATIVE Ergebnis ab. Das vom Client gemeldete Resultat (stars > 0
    //    impliziert Sieg) wird gegen den nachgespielten Outcome geprueft.
    const claimedVictory = stars > 0;
    const replayCheck = runReplayCheck(request.data.replay, claimedVictory, seed, uid, worldId, nodeId);

    // Wenn der Replay scharf ist und einen vom Client behaupteten Sieg NICHT reproduziert,
    // wird der Kampf abgelehnt — Rewards werden NICHT ausgezahlt.
    if (REPLAY_VALIDATION_ENABLED && replayCheck.divergent) {
      return {
        valid: false,
        awardedGold: 0,
        awardedExp: 0,
        rejectedReason: replayCheck.reason,
      };
    }

    // 5) Server-autoritative Reward-Berechnung (Quelle der Wahrheit).
    //    Rewards kommen IMMER aus dem nachgespielten Outcome + WorldDefinition, NIE aus
    //    claimed-Werten. Bei nicht reproduziertem Sieg (Replay sagt: kein Sieg) zahlen wir
    //    auch im Schatten-Modus 0 aus, sobald ein eindeutiges Replay-Ergebnis vorliegt.
    const replayConfirmsVictory = replayCheck.replayOutcome === BattleResult.PlayerWins;
    const payOut = replayCheck.hasReplay ? replayConfirmsVictory : claimedVictory;
    const { gold, exp } = payOut
      ? await computeAuthoritativeRewards(worldId, nodeId, stars)
      : { gold: 0, exp: 0 };

    // 6) Plausibilitaets-Vergleich der claimed-Werte (nur Logging, nicht autoritativ).
    if (
      claimedGold > gold * 2 ||
      claimedExp > exp * 2 ||
      claimedGold > MAX_PLAUSIBLE_GOLD ||
      claimedExp > MAX_PLAUSIBLE_EXP
    ) {
      console.warn(
        `[validateBattleResult] Verdaechtige claimed-Werte uid=${uid} world=${worldId} node=${nodeId} ` +
          `stars=${stars}: claimedGold=${claimedGold} (server=${gold}), claimedExp=${claimedExp} (server=${exp})`
      );
    }

    return { valid: true, awardedGold: gold, awardedExp: exp };
  }
);

interface ReplayCheckResult {
  /** True, wenn ein Replay-Paket vorhanden und ausgewertet wurde. */
  hasReplay: boolean;
  /** Nachgespieltes Ergebnis (undefined, wenn kein Replay vorlag). */
  replayOutcome?: BattleResult;
  /** True, wenn der Client einen Sieg behauptet, den der Replay NICHT reproduziert. */
  divergent: boolean;
  /** Ablehnungs-Grund (nur bei divergent). */
  reason?: string;
}

// Fuehrt den deterministischen Replay aus und vergleicht ihn mit dem Client-Resultat.
// Wirft NIE — Replay-Fehler degradieren zu "kein Replay" (Schatten-Modus, kein False-Positive).
function runReplayCheck(
  replay: BattleReplayInput | undefined,
  claimedVictory: boolean,
  seed: number,
  uid: string,
  worldId: string,
  nodeId: string
): ReplayCheckResult {
  if (!replay) {
    // Client hat (noch) kein Aktions-Log mitgeschickt -> reine Plausibilitaet (Owner/Nonce/Caps).
    return { hasReplay: false, divergent: false };
  }

  // Seed-Konsistenz: das Replay-Paket MUSS denselben Seed wie der Nonce-geschuetzte Request nutzen.
  if (replay.seed !== seed) {
    console.warn(
      `[validateBattleResult] Replay-Seed (${replay.seed}) != Request-Seed (${seed}) ` +
        `uid=${uid} world=${worldId} node=${nodeId} — Replay verworfen.`
    );
    return { hasReplay: false, divergent: false };
  }

  let result;
  try {
    result = replayBattle(replay);
  } catch (err) {
    console.error(`[validateBattleResult] Replay-Ausfuehrung fehlgeschlagen uid=${uid}:`, err);
    return { hasReplay: false, divergent: false };
  }

  const replayVictory = result.outcome === BattleResult.PlayerWins;
  // Divergenz = Client behauptet Sieg, Replay reproduziert ihn nicht (oder Log war unvollstaendig).
  const divergent = claimedVictory && (!replayVictory || result.actionLogExhausted);

  if (divergent) {
    console.warn(
      `[validateBattleResult] REPLAY-DIVERGENZ uid=${uid} world=${worldId} node=${nodeId}: ` +
        `claimedVictory=${claimedVictory}, replayOutcome=${BattleResult[result.outcome]}, ` +
        `actionLogExhausted=${result.actionLogExhausted}, turns=${result.turnsPlayed}. ` +
        `${REPLAY_VALIDATION_ENABLED ? "ABGELEHNT (scharf)." : "NUR GELOGGT (Schatten-Modus)."}`
    );
  }

  return {
    hasReplay: true,
    replayOutcome: result.outcome,
    divergent,
    reason: divergent ? "Kampf-Replay reproduziert den gemeldeten Sieg nicht." : undefined,
  };
}

interface NodeReward {
  gold: number;
  exp: number;
}

// Server-seitige Reward-Berechnung. Bevorzugt die in Firestore gepflegten
// WorldDefinition-Daten (Collection `remoteConfig/worlds/{worldId}/nodes/{nodeId}`);
// fehlen diese, greift eine klar dokumentierte deterministische Fallback-Formel.
//
// Fallback-Formel (dokumentiert, deterministisch):
//   Node-Index n wird aus dem nodeId-Suffix extrahiert (z.B. "node_07" → 7), sonst 1.
//   Welt-Index w aus dem worldId-Suffix (z.B. "world_3" → 3), sonst 1.
//   basisGold = 500 * w * n
//   basisExp  = 50  * w * n
//   sterneFaktor = 1.0 / 2 / 3 / 4 Sterne → 0.5 / 0.75 / 1.0 / 1.25
//   → gold = round(basisGold * sterneFaktor), exp = round(basisExp * sterneFaktor)
async function computeAuthoritativeRewards(
  worldId: string,
  nodeId: string,
  stars: number
): Promise<NodeReward> {
  const starFactor = starRewardFactor(stars);

  // Bevorzugt: gepflegte Node-Definition aus Firestore (remoteConfig).
  try {
    const nodeDoc = await admin
      .firestore()
      .collection("remoteConfig")
      .doc("worlds")
      .collection(worldId)
      .doc(nodeId)
      .get();
    if (nodeDoc.exists) {
      const data = nodeDoc.data() ?? {};
      const baseGold = typeof data.baseGold === "number" ? data.baseGold : 0;
      const baseExp = typeof data.baseExp === "number" ? data.baseExp : 0;
      if (baseGold > 0 || baseExp > 0) {
        return {
          gold: Math.round(baseGold * starFactor),
          exp: Math.round(baseExp * starFactor),
        };
      }
    }
  } catch (err) {
    // Firestore-Lesefehler nicht fatal — Fallback-Formel greift.
    console.warn(`[validateBattleResult] NodeDefinition-Read fehlgeschlagen (${worldId}/${nodeId}):`, err);
  }

  // Fallback: deterministische Formel aus Welt-/Node-Index.
  const w = extractIndex(worldId, 1);
  const n = extractIndex(nodeId, 1);
  return {
    gold: Math.round(500 * w * n * starFactor),
    exp: Math.round(50 * w * n * starFactor),
  };
}

// Sterne-Faktor: mehr Sterne = mehr Belohnung (1★ minimal, 4★ maximal).
function starRewardFactor(stars: number): number {
  switch (stars) {
    case 4: return 1.25;
    case 3: return 1.0;
    case 2: return 0.75;
    default: return 0.5;
  }
}

// Extrahiert die letzte Zahlengruppe aus einer Id ("world_3", "node_07") als Index.
function extractIndex(id: string, fallback: number): number {
  const match = id.match(/(\d+)\D*$/);
  if (!match) return fallback;
  const value = parseInt(match[1], 10);
  return Number.isFinite(value) && value > 0 ? value : fallback;
}
