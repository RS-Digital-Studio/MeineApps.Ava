import { onDocumentUpdated } from "firebase-functions/v2/firestore";

// Verteilt Klan-Match-Belohnungen + uebertraegt Gebiets-Kontrolle.
// Trigger: klanMatches/{matchId} 'state' wechselt zu 'completed'.
export const onKlanMatchCompleted = onDocumentUpdated(
  { document: "klanMatches/{matchId}", region: "europe-west1" },
  async (event) => {
    const before = event.data?.before.data();
    const after = event.data?.after.data();
    if (!before || !after) return;
    if (before.state === "completed" || after.state !== "completed") return;

    // TODO: Gewinner-Logik basierend auf Round-Results
    // TODO: Territory-Ownership transfer
    // TODO: Gold-Auszahlung (Verlierer-Gebot - 10% an Gewinner-Treasury)
    console.log(`[onKlanMatchCompleted] Match ${event.params.matchId} completed.`);
  }
);
