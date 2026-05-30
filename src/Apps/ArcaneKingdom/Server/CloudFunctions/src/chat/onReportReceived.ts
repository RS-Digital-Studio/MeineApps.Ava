import { onDocumentCreated } from "firebase-functions/v2/firestore";
import * as admin from "firebase-admin";

// Schwelle fuer AutoMute: ab so vielen Reports innerhalb des Zeitfensters.
const AUTO_MUTE_THRESHOLD = 3;
// Beobachtungs- und Mute-Fenster: 24 Stunden.
const WINDOW_MS = 24 * 60 * 60 * 1000;

// Wird ausgeloest, wenn ein Chat-Report angelegt wird. Aggregiert die Reports auf
// den gemeldeten Spieler und triggert AutoMute bei >= 3 Reports in 24 Stunden.
//
// M35-Fix: Trigger-Pfad und Lese-Pfad MUESSEN auf dieselbe Collection zeigen.
// Vorher schrieb/triggerte die Funktion auf `chats/reports/{reportId}`, las aber aus
// der Subcollection `chats/reports/entries` — diese existierte nie, also griff AutoMute
// nie. Jetzt liest die Aggregation aus derselben `reports`-Subcollection, in der der
// Trigger feuert: `chats/reports/{reportId}` → CollectionGroup-konsistent ueber
// `event.data.ref.parent` (die Collection, in der das ausloesende Dokument liegt).
export const onReportReceived = onDocumentCreated(
  { document: "chats/reports/{reportId}", region: "europe-west1" },
  async (event) => {
    const snap = event.data;
    if (!snap) return;
    const data = snap.data() as { reportedPlayerId?: string; reportedAtMs?: number };
    const reportedPlayerId = data.reportedPlayerId;
    if (!reportedPlayerId || typeof reportedPlayerId !== "string") return;

    const sinceMs = Date.now() - WINDOW_MS;

    // Aus DERSELBEN Collection lesen, in der das ausloesende Dokument liegt
    // (snap.ref.parent === die `reports`-Subcollection). Damit sind Trigger-Pfad
    // und Lese-Pfad garantiert identisch.
    const reportsCollection = snap.ref.parent;
    const recent = await reportsCollection
      .where("reportedPlayerId", "==", reportedPlayerId)
      .where("reportedAtMs", ">=", sinceMs)
      .get();

    if (recent.size >= AUTO_MUTE_THRESHOLD) {
      const muteUntil = Date.now() + WINDOW_MS;
      await admin
        .database()
        .ref(`players/${reportedPlayerId}/chatSlice/mutedUntilMs`)
        .set(muteUntil);
      console.log(`[onReportReceived] AutoMute fuer ${reportedPlayerId} (${recent.size} Reports/24h)`);
    }
  }
);
