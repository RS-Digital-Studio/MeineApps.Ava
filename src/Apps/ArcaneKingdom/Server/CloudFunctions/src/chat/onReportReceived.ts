import { onDocumentCreated } from "firebase-functions/v2/firestore";
import * as admin from "firebase-admin";

// Wird ausgeloest wenn ein Chat-Report in chats/reports/{reportId} angelegt wird.
// Aggregiert die Reports auf den gemeldeten Spieler und triggert AutoMute bei
// >= 3 Reports in 24 Stunden.
export const onReportReceived = onDocumentCreated(
  { document: "chats/reports/{reportId}", region: "europe-west1" },
  async (event) => {
    const data = event.data?.data();
    if (!data) return;
    const { reportedPlayerId } = data as { reportedPlayerId: string };
    if (!reportedPlayerId) return;

    const sinceMs = Date.now() - 24 * 60 * 60 * 1000;
    const recent = await admin
      .firestore()
      .collection("chats")
      .doc("reports")
      .collection("entries")
      .where("reportedPlayerId", "==", reportedPlayerId)
      .where("reportedAtMs", ">=", sinceMs)
      .get();

    if (recent.size >= 3) {
      const muteUntil = Date.now() + 24 * 60 * 60 * 1000;
      await admin
        .database()
        .ref(`players/${reportedPlayerId}/chatSlice/mutedUntilMs`)
        .set(muteUntil);
      console.log(`[onReportReceived] AutoMute fuer ${reportedPlayerId} (${recent.size} Reports/24h)`);
    }
  }
);
