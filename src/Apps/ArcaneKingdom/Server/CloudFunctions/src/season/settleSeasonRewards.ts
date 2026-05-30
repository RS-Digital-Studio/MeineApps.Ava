import { onSchedule } from "firebase-functions/v2/scheduler";
import * as admin from "firebase-admin";

// Anzahl Spieler-Rankings, die pro Batch verarbeitet werden (begrenzt Last/Speicher).
const PLAYER_BATCH_SIZE = 200;

// Taeglicher Cron: prueft, ob eine Saison endet, und verteilt Belohnungen als
// PendingClaim in alle Spieler-Saves.
//
// Idempotent (H22):
//   - Die Saison wird FRUEH per Firestore-Transaction gelockt (settledAtMs von 0 auf
//     jetzt gesetzt). Ein paralleler/erneuter Lauf sieht settledAtMs != 0 und ueberspringt.
//   - Pro Spieler dient ein deterministischer RTDB-Eintrag
//     `players/{uid}/settledSeasons/{seasonId}` als Set-if-not-exists-Sperre. Ein Re-Run
//     erzeugt KEINE neuen push()-Claims, weil bereits abgerechnete Spieler uebersprungen
//     werden. Die einzelnen Claims tragen deterministische Keys
//     (`season_{seasonId}_{index}`) statt push(), sodass auch ein Teil-Re-Run
//     dieselben Claims nur ueberschreibt statt zu duplizieren.
export const settleSeasonRewards = onSchedule(
  { schedule: "every day 00:00", timeZone: "UTC", region: "europe-west1" },
  async () => {
    const nowMs = Date.now();
    const seasonsRef = admin
      .firestore()
      .collection("seasons")
      .where("endsAtMs", "<=", nowMs)
      .where("settledAtMs", "==", 0);
    const snapshot = await seasonsRef.get();
    if (snapshot.empty) {
      console.log("[settleSeasonRewards] Keine Saisons zum Settlen.");
      return;
    }

    for (const seasonDoc of snapshot.docs) {
      const seasonId = seasonDoc.id;

      // Saison FRUEH locken: settledAtMs atomar von 0 auf jetzt setzen. Gewinnt nur
      // genau ein Lauf; alle anderen brechen ab und ueberspringen die Saison.
      const locked = await admin.firestore().runTransaction(async (tx) => {
        const fresh = await tx.get(seasonDoc.ref);
        const current = (fresh.get("settledAtMs") ?? 0) as number;
        if (current !== 0) return false; // bereits gelockt/abgerechnet
        tx.update(seasonDoc.ref, { settledAtMs: nowMs });
        return true;
      });
      if (!locked) {
        console.log(`[settleSeasonRewards] Saison ${seasonId} bereits gelockt — uebersprungen.`);
        continue;
      }

      let settledPlayers = 0;
      let lastDoc: admin.firestore.QueryDocumentSnapshot | null = null;

      // Spieler-Rankings in Batches verarbeiten.
      // eslint-disable-next-line no-constant-condition
      while (true) {
        let query = seasonDoc.ref.collection("rankings").orderBy("__name__").limit(PLAYER_BATCH_SIZE);
        if (lastDoc) query = query.startAfter(lastDoc);
        const rankings = await query.get();
        if (rankings.empty) break;

        for (const rankDoc of rankings.docs) {
          const { uid, rangPunkte } = rankDoc.data() as { uid: string; rangPunkte: number };
          if (typeof uid !== "string" || !uid || typeof rangPunkte !== "number") continue;

          // Pro Spieler Set-if-not-exists ueber RTDB-Transaction: bereits abgerechnet → skip.
          const settledRef = admin.database().ref(`players/${uid}/settledSeasons/${seasonId}`);
          const settledResult = await settledRef.transaction((current) => {
            if (current !== null) return undefined; // schon abgerechnet → Transaction abbrechen
            return { settledAtMs: nowMs };
          });
          if (!settledResult.committed) continue;

          // Deterministische Claim-Keys statt push() → kein Doppel-Reward bei Re-Run.
          const tier = tierFromPoints(rangPunkte);
          const rewards = rewardsForTier(tier);
          const claimsRef = admin.database().ref(`players/${uid}/pendingClaims`);
          const updates: Record<string, unknown> = {};
          rewards.forEach((reward, index) => {
            updates[`season_${seasonId}_${index}`] = {
              kind: reward.kind,
              subType: reward.subType,
              amount: reward.amount,
              sourceKey: `season.${seasonId}.${tier}`,
              createdAtMs: nowMs,
            };
          });
          await claimsRef.update(updates);
          settledPlayers++;
        }

        lastDoc = rankings.docs[rankings.docs.length - 1];
        if (rankings.size < PLAYER_BATCH_SIZE) break;
      }

      console.log(`[settleSeasonRewards] Saison ${seasonId} mit ${settledPlayers} Spielern abgerechnet.`);
    }
  }
);

function tierFromPoints(rp: number): string {
  if (rp >= 2500) return "Diamant";
  if (rp >= 1500) return "Platin";
  if (rp >= 1000) return "Gold";
  if (rp >= 500) return "Silber";
  return "Bronze";
}

function rewardsForTier(tier: string): Array<{ kind: string; subType: string; amount: number }> {
  switch (tier) {
    case "Diamant": return [{ kind: "Currency", subType: "Gold", amount: 500000 }, { kind: "Scrap", subType: "Legendary", amount: 1 }];
    case "Platin":  return [{ kind: "Currency", subType: "Gold", amount: 200000 }, { kind: "Scrap", subType: "Epic", amount: 5 }];
    case "Gold":    return [{ kind: "Currency", subType: "Gold", amount: 50000 },  { kind: "Scrap", subType: "Rare", amount: 10 }];
    case "Silber":  return [{ kind: "Currency", subType: "Gold", amount: 15000 },  { kind: "Scrap", subType: "Common", amount: 10 }];
    default:        return [{ kind: "Currency", subType: "Gold", amount: 5000 }];
  }
}
