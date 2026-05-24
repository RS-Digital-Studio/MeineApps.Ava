import { onCall, HttpsError } from "firebase-functions/v2/https";

interface ValidateBattleResultRequest {
  worldId: string;
  nodeId: string;
  stars: number;
  seed: number;
  deckCardIds: string[];
  claimedGold: number;
  claimedExp: number;
}

interface ValidateBattleResultResponse {
  valid: boolean;
  awardedGold: number;
  awardedExp: number;
  rejectedReason?: string;
}

// Anti-Cheat-Validierung fuer Welt-Kaempfe.
// Server fuehrt denselben deterministischen BattleEngine aus und vergleicht.
//
// STUB: Echte Implementierung folgt sobald die TS-Portierung der C#-BattleEngine
// in einem separaten Modul vorliegt. Hier nur Schema/Permission-Checks.
export const validateBattleResult = onCall<ValidateBattleResultRequest, Promise<ValidateBattleResultResponse>>(
  { region: "europe-west1", timeoutSeconds: 30, memory: "256MiB" },
  async (request) => {
    const uid = request.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Login required.");
    const { worldId, nodeId, stars, seed, deckCardIds, claimedGold, claimedExp } = request.data;
    if (stars < 1 || stars > 4) throw new HttpsError("invalid-argument", "Invalid stars value.");
    if (!Array.isArray(deckCardIds) || deckCardIds.length === 0) {
      throw new HttpsError("invalid-argument", "Deck must contain cards.");
    }
    // TODO: Owner-Pruefung gegen RTDB-Snapshot, Seed-Nonce-Check, BattleEngine-Replay.
    // TODO: Reward-Berechnung aus WorldDefinition.
    return {
      valid: true,
      awardedGold: claimedGold,
      awardedExp: claimedExp,
    };
  }
);
