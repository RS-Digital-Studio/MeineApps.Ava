// ArcaneKingdom Cloud Functions Entry-Point.
// Alle exportierten Funktionen werden via `firebase deploy --only functions` deployed.

import * as admin from "firebase-admin";

admin.initializeApp();

export { validateBattleResult } from "./battle/validateBattleResult";
export { validateIapReceipt } from "./iap/validateIapReceipt";
export { settleSeasonRewards } from "./season/settleSeasonRewards";
export { onReportReceived } from "./chat/onReportReceived";
export { onKlanMatchCompleted } from "./guild/onKlanMatchCompleted";
export { createGuild } from "./guild/createGuild";
export { dailyTerritoryTick } from "./guild/dailyTerritoryTick";
export { updateThiefMultiplier } from "./thief/updateThiefMultiplier";
