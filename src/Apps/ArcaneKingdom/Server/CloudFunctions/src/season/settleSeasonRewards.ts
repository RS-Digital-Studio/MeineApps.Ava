import { onSchedule } from "firebase-functions/v2/scheduler";
import * as admin from "firebase-admin";

// Taeglicher Cron: prueft, ob eine Saison heute endet, und verteilt Belohnungen
// als PendingClaim in alle Spieler-Saves. Schreibt anschliessend einen
// History-Eintrag unter /seasons/{seasonId}/settledAt.
export const settleSeasonRewards = onSchedule(
  { schedule: "every day 00:00", timeZone: "UTC", region: "europe-west1" },
  async () => {
    const nowMs = Date.now();
    const seasonsRef = admin.firestore().collection("seasons").where("endsAtMs", "<=", nowMs).where("settledAtMs", "==", 0);
    const snapshot = await seasonsRef.get();
    if (snapshot.empty) {
      console.log("[settleSeasonRewards] Keine Saisons zum Settlen.");
      return;
    }

    for (const seasonDoc of snapshot.docs) {
      const seasonId = seasonDoc.id;
      const rankingsRef = seasonDoc.ref.collection("rankings");
      const rankings = await rankingsRef.get();
      for (const rankDoc of rankings.docs) {
        const { uid, rangPunkte } = rankDoc.data() as { uid: string; rangPunkte: number };
        const tier = tierFromPoints(rangPunkte);
        const rewards = rewardsForTier(tier);
        const claimsRef = admin.database().ref(`players/${uid}/pendingClaims`);
        for (const reward of rewards) {
          await claimsRef.push({
            kind: reward.kind,
            subType: reward.subType,
            amount: reward.amount,
            sourceKey: `season.${seasonId}.${tier}`,
            createdAtMs: nowMs,
          });
        }
      }
      await seasonDoc.ref.update({ settledAtMs: nowMs });
      console.log(`[settleSeasonRewards] Saison ${seasonId} mit ${rankings.size} Spielern abgerechnet.`);
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
