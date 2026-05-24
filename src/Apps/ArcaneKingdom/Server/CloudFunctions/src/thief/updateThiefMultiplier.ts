import { onSchedule } from "firebase-functions/v2/scheduler";
import * as admin from "firebase-admin";

// Aktualisiert den DAU-basierten Dieb-Multiplikator (DESIGN 10.2)
// alle 4 Stunden. Schreibt in Remote Config.
export const updateThiefMultiplier = onSchedule(
  { schedule: "every 4 hours", region: "europe-west1" },
  async () => {
    const since = Date.now() - 24 * 60 * 60 * 1000;
    const dauSnap = await admin
      .firestore()
      .collection("sessionLogs")
      .where("startedAtMs", ">=", since)
      .count()
      .get();
    const dau = dauSnap.data().count;
    const multiplier = calculateMultiplier(dau);
    await admin.firestore().collection("remoteConfig").doc("global").set(
      { thiefDauMultiplier: multiplier, dau, updatedAtMs: Date.now() },
      { merge: true }
    );
    console.log(`[updateThiefMultiplier] DAU=${dau} → mult=${multiplier}`);
  }
);

function calculateMultiplier(dau: number): number {
  if (dau < 1_000) return 0.4;
  if (dau < 5_000) return 0.6 + (dau - 1_000) / 10_000;
  if (dau < 20_000) return 1.0 + (dau - 5_000) / 30_000;
  return 1.5;
}
