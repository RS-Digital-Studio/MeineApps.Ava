import { onCall, HttpsError } from "firebase-functions/v2/https";
import { defineSecret } from "firebase-functions/params";
import * as admin from "firebase-admin";
import { google } from "googleapis";
import { requirePathSafeId, requireString } from "../common/validation";

// Service-Account-JSON fuer die Google Play Developer API (androidpublisher).
// Wird NICHT im Repo gehalten, sondern via `firebase functions:secrets:set
// GOOGLE_PLAY_SERVICE_ACCOUNT` gesetzt. Inhalt: der komplette JSON-Key des
// Service-Accounts (Rolle "Finanzdaten anzeigen" + API-Zugriff in der Play Console).
const GOOGLE_PLAY_SERVICE_ACCOUNT = defineSecret("GOOGLE_PLAY_SERVICE_ACCOUNT");

interface ValidateIapReceiptRequest {
  productId: string;
  purchaseToken: string;
  packageName: string;
}

interface ValidateIapReceiptResponse {
  valid: boolean;
  diamondsGranted: number;
  transactionId: string;
}

// Diamanten-Mengen pro Produkt (server-autoritativ, NICHT vom Client steuerbar).
const DIAMONDS_BY_PRODUCT: Record<string, number> = {
  diamonds_starter: 60,
  diamonds_small: 330,
  diamonds_medium: 1130,
  diamonds_large: 2380,
  diamonds_huge: 4080,
  diamonds_mega: 8480,
};

// Erlaubter App-Package-Name. Schuetzt davor, dass Tokens einer fremden App
// akzeptiert werden. Muss zur produktiven Application-Id passen.
const ALLOWED_PACKAGE_NAME = "com.rsdigital.arcanekingdom";

// purchaseState der Google-Antwort: 0 = purchased, 1 = canceled, 2 = pending.
const PURCHASE_STATE_PURCHASED = 0;

// acknowledgementState der Google-Antwort: 0 = noch nicht bestaetigt, 1 = bestaetigt.
const ACK_STATE_ACKNOWLEDGED = 1;

// Validiert einen Google-Play-Kauf serverseitig gegen die Developer-API und
// schreibt Diamanten NUR nach erfolgreicher Verifikation. Idempotent ueber die
// von Google vergebene orderId (NICHT den purchaseToken), abgesichert durch ein
// RTDB-Ledger gegen Doppel-Gutschrift (Replay-Schutz, C7/H25/H26).
export const validateIapReceipt = onCall<ValidateIapReceiptRequest, Promise<ValidateIapReceiptResponse>>(
  {
    region: "europe-west1",
    timeoutSeconds: 30,
    memory: "256MiB",
    enforceAppCheck: true,
    secrets: [GOOGLE_PLAY_SERVICE_ACCOUNT],
  },
  async (request) => {
    const uid = request.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Login required.");

    // 1) Input-Sanitization (M34) — productId fliesst nur als Map-Key, Token an die API.
    const productId = requirePathSafeId(request.data.productId, "productId", 64);
    const purchaseToken = requireString(request.data.purchaseToken, "purchaseToken", 1024);
    const packageName = requireString(request.data.packageName, "packageName", 128);

    if (packageName !== ALLOWED_PACKAGE_NAME) {
      throw new HttpsError("invalid-argument", "Ungueltiger packageName.");
    }
    const diamonds = DIAMONDS_BY_PRODUCT[productId];
    if (!diamonds) throw new HttpsError("invalid-argument", `Unbekanntes Produkt: ${productId}`);

    // 2) Echte Verifikation gegen die Google Play Developer API.
    const purchase = await fetchPlayPurchase(packageName, productId, purchaseToken);

    if (purchase.purchaseState !== PURCHASE_STATE_PURCHASED) {
      throw new HttpsError("failed-precondition", "Kauf nicht im Status 'purchased'.");
    }
    // orderId ist der stabile, von Google vergebene Idempotenz-Schluessel.
    const orderId = purchase.orderId;
    if (!orderId) {
      throw new HttpsError("failed-precondition", "Google-Antwort ohne orderId.");
    }
    // orderId in pfad-sicheren Ledger-Key normalisieren (orderIds enthalten '.', '..').
    const ledgerKey = sanitizeOrderId(orderId);

    const db = admin.database();
    const ledgerRef = db.ref(`ledger/${ledgerKey}`);

    // 3) Idempotenz-Ledger: per Transaction pruefen, ob die orderId schon eingeloest
    //    wurde. Nur der erste Aufruf reserviert den Ledger-Eintrag; alle weiteren
    //    brechen ab (committed === false) und schreiben KEINE Diamanten.
    const ledgerResult = await ledgerRef.transaction((current) => {
      if (current !== null) return undefined; // bereits eingeloest → Transaction abbrechen
      return {
        uid,
        productId,
        diamonds,
        orderId,
        purchaseToken,
        grantedAtMs: admin.database.ServerValue.TIMESTAMP,
      };
    });

    if (!ledgerResult.committed) {
      // Bereits gutgeschrieben → idempotenter Erfolg ohne Doppel-Gutschrift.
      return { valid: true, diamondsGranted: 0, transactionId: orderId };
    }

    // 4) Erst NACH erfolgreicher Ledger-Reservierung gutschreiben.
    await db.ref(`players/${uid}/currencies/diamond`).transaction((c) => (c ?? 0) + diamonds);

    // 5) Kauf bei Google bestaetigen (acknowledge) — sonst storniert Google nach 3 Tagen.
    //    Schon bestaetigte Kaeufe (acknowledgementState === 1) nicht erneut acken.
    if (purchase.acknowledgementState !== ACK_STATE_ACKNOWLEDGED) {
      await acknowledgePlayPurchase(packageName, productId, purchaseToken);
    }

    return { valid: true, diamondsGranted: diamonds, transactionId: orderId };
  }
);

interface PlayProductPurchase {
  purchaseState?: number | null;
  acknowledgementState?: number | null;
  orderId?: string | null;
}

// Baut einen authentifizierten androidpublisher-Client aus dem Secret-Service-Account.
function createAndroidPublisher() {
  const credentials = JSON.parse(GOOGLE_PLAY_SERVICE_ACCOUNT.value());
  const auth = new google.auth.GoogleAuth({
    credentials,
    scopes: ["https://www.googleapis.com/auth/androidpublisher"],
  });
  return google.androidpublisher({ version: "v3", auth });
}

// Holt den Kauf-Status eines verbrauchbaren Produkts von Google Play.
async function fetchPlayPurchase(
  packageName: string,
  productId: string,
  token: string
): Promise<PlayProductPurchase> {
  try {
    const publisher = createAndroidPublisher();
    const res = await publisher.purchases.products.get({ packageName, productId, token });
    return res.data as PlayProductPurchase;
  } catch (err) {
    const status = (err as { code?: number }).code;
    // 404/410: Token unbekannt/abgelaufen → ungueltiger Kauf, kein Server-Fehler.
    if (status === 404 || status === 410) {
      throw new HttpsError("failed-precondition", "Purchase-Token unbekannt oder abgelaufen.");
    }
    console.error("[validateIapReceipt] Google-Play-API-Fehler:", err);
    throw new HttpsError("internal", "Verifikation gegen Google Play fehlgeschlagen.");
  }
}

// Bestaetigt den Kauf bei Google Play (Pflicht innerhalb 3 Tagen).
async function acknowledgePlayPurchase(packageName: string, productId: string, token: string): Promise<void> {
  try {
    const publisher = createAndroidPublisher();
    await publisher.purchases.products.acknowledge({ packageName, productId, token });
  } catch (err) {
    // Acknowledge-Fehler nicht fatal fuer den Spieler — Diamanten sind bereits
    // gutgeschrieben. Loggen, damit ein Retry-Job nachziehen kann.
    console.error("[validateIapReceipt] acknowledge fehlgeschlagen:", err);
  }
}

// Normalisiert die Google-orderId zu einem RTDB-pfad-sicheren Key
// (orderIds wie "GPA.1234-5678-9012-34567" sind sicher, koennen aber theoretisch
// verbotene Zeichen enthalten — daher defensiv ersetzen).
function sanitizeOrderId(orderId: string): string {
  return orderId.replace(/[.#$/[\]]/g, "_");
}
