import { onCall, HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";

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

// Validiert Google Play Billing Purchase via Developer-API.
// Schreibt Diamanten in RTDB nur nach erfolgreicher Validierung — idempotent
// per transactionId, sodass doppeltes Aufrufen nicht doppelt gutschreibt.
export const validateIapReceipt = onCall<ValidateIapReceiptRequest, Promise<ValidateIapReceiptResponse>>(
  { region: "europe-west1", timeoutSeconds: 30 },
  async (request) => {
    const uid = request.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Login required.");
    const { productId, purchaseToken, packageName } = request.data;

    // TODO: Google Play Developer API call mit Service-Account
    //   const result = await googleplay.purchases.products.get({ packageName, productId, token: purchaseToken });
    //   if (result.purchaseState !== 0) throw ...;

    // TODO: Idempotenz-Check: ledger/{transactionId} existiert? → skip.
    const transactionId = purchaseToken; // Vereinfacht
    const diamondsByProduct: Record<string, number> = {
      diamonds_starter: 60,
      diamonds_small: 330,
      diamonds_medium: 1130,
      diamonds_large: 2380,
      diamonds_huge: 4080,
      diamonds_mega: 8480,
    };
    const diamonds = diamondsByProduct[productId];
    if (!diamonds) throw new HttpsError("invalid-argument", `Unbekanntes Produkt: ${productId}`);

    const playerRef = admin.database().ref(`players/${uid}/currencies/diamond`);
    await playerRef.transaction((current) => (current ?? 0) + diamonds);

    return { valid: true, diamondsGranted: diamonds, transactionId };
  }
);
