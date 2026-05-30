import { onDocumentUpdated } from "firebase-functions/v2/firestore";
import * as admin from "firebase-admin";

// Verteilt Klan-Match-Belohnungen + uebertraegt Gebiets-Kontrolle.
// Trigger: klanMatches/{matchId} 'state' wechselt zu 'completed'.
//
// Idempotenz: Der Uebergangs-Guard (before != completed && after == completed) feuert
// nur beim erstmaligen Abschluss. Zusaetzlich locken wir das Match per Transaction ueber
// das Feld `rewardsSettledAtMs`, damit ein doppelt zugestellter Trigger-Event (Firestore
// garantiert mindestens-einmal-Zustellung) die Belohnungen NICHT doppelt verteilt.
export const onKlanMatchCompleted = onDocumentUpdated(
  { document: "klanMatches/{matchId}", region: "europe-west1" },
  async (event) => {
    const before = event.data?.before.data();
    const after = event.data?.after.data();
    if (!before || !after) return;
    // Nur beim Uebergang in 'completed' reagieren.
    if (before.state === "completed" || after.state !== "completed") return;

    const matchRef = event.data!.after.ref;

    // Settled-Lock per Transaction: nur der erste Lauf gewinnt.
    const acquired = await admin.firestore().runTransaction(async (tx) => {
      const fresh = await tx.get(matchRef);
      if (fresh.get("rewardsSettledAtMs")) return false; // bereits abgerechnet
      tx.update(matchRef, { rewardsSettledAtMs: Date.now() });
      return true;
    });
    if (!acquired) {
      console.log(`[onKlanMatchCompleted] Match ${event.params.matchId} bereits abgerechnet — uebersprungen.`);
      return;
    }

    // TODO Reward-Verteilung: Gewinner-Logik basierend auf Round-Results.
    // TODO Territory-Ownership-Transfer an den Gewinner.
    // TODO Gold-Auszahlung (Verlierer-Gebot - 10% an Gewinner-Treasury).
    // Die Idempotenz-Sperre oben ist bereits gesetzt, sodass diese Logik beim Nachruesten
    // automatisch gegen Doppel-Auszahlung geschuetzt ist.
    console.log(`[onKlanMatchCompleted] Match ${event.params.matchId} completed (Reward-Logik folgt).`);
  }
);
