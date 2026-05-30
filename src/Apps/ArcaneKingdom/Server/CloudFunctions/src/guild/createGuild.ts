import { onCall, HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import { requireString } from "../common/validation";

interface CreateGuildRequest {
  name: string;
  tag: string;
  slogan: string;
}

// Gilden-Gruendungskosten (server-autoritativ).
const GUILD_COST_GOLD = 50_000;

// Erstellt eine Gilde mit eindeutigem Tag. Reihenfolge ist bewusst gewaehlt, damit
// keine verwaisten Daten entstehen (H21/M36):
//   1) Mitgliedschaft pruefen (kein Mehrfach-Gruenden).
//   2) Gold per Transaction reservieren MIT Deckungspruefung (kann nicht negativ werden).
//   3) Erst danach Firestore-Gilde + Tag atomar anlegen.
//   4) Schlaegt Schritt 3 fehl, Gold kompensierend zurueckbuchen.
export const createGuild = onCall<CreateGuildRequest>(
  { region: "europe-west1", enforceAppCheck: true },
  async (request) => {
    const uid = request.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Login required.");

    // 1) Input-Sanitization (M34).
    const name = requireString(request.data.name, "name", 20, 3);
    const tagRaw = requireString(request.data.tag, "tag", 5, 5);
    const tag = tagRaw.toUpperCase();
    if (!/^[A-Z0-9]{5}$/.test(tag)) {
      throw new HttpsError("invalid-argument", "Tag muss aus genau 5 Buchstaben/Ziffern bestehen.");
    }
    // Slogan optional, aber laengenbegrenzt.
    const slogan = typeof request.data.slogan === "string" ? request.data.slogan.trim().slice(0, 100) : "";

    const db = admin.database();
    const profileRef = db.ref(`players/${uid}/profile/guildId`);

    // 2) Mitgliedschafts-Pruefung: bereits in einer Gilde → keine Zweitgruendung.
    const existingGuild = await profileRef.get();
    if (existingGuild.exists() && existingGuild.val()) {
      throw new HttpsError("already-exists", "Spieler ist bereits in einer Gilde.");
    }

    // 3) Gold reservieren MIT Deckungspruefung. transaction gibt undefined zurueck,
    //    wenn die Deckung nicht reicht → Abbruch ohne Abzug (kein negativer Stand).
    const goldRef = db.ref(`players/${uid}/currencies/gold`);
    const goldResult = await goldRef.transaction((current) => {
      const balance = (current ?? 0) as number;
      if (balance < GUILD_COST_GOLD) return undefined; // Abbruch: zu wenig Gold
      return balance - GUILD_COST_GOLD;
    });
    if (!goldResult.committed) {
      throw new HttpsError("failed-precondition", `Nicht genug Gold (${GUILD_COST_GOLD} benoetigt).`);
    }

    // 4) Firestore-Gilde + eindeutigen Tag atomar anlegen. Bei Fehler Gold zurueckbuchen.
    const tagRef = admin.firestore().collection("guildTags").doc(tag);
    let guildId: string;
    try {
      guildId = await admin.firestore().runTransaction(async (tx) => {
        const existingTag = await tx.get(tagRef);
        if (existingTag.exists) throw new HttpsError("already-exists", "Tag bereits vergeben.");
        const guildRef = admin.firestore().collection("guilds").doc();
        const nowMs = Date.now();
        tx.set(tagRef, { guildId: guildRef.id });
        tx.set(guildRef, {
          name,
          tag,
          slogan,
          level: 1,
          leaderId: uid,
          memberCount: 1,
          createdAtMs: nowMs,
        });
        tx.set(guildRef.collection("members").doc(uid), {
          role: "Leader",
          joinedAtMs: nowMs,
        });
        return guildRef.id;
      });
    } catch (err) {
      // Kompensation: Gold zurueckbuchen, damit der Spieler nicht ohne Gegenwert zahlt.
      await goldRef.transaction((current) => ((current ?? 0) as number) + GUILD_COST_GOLD);
      if (err instanceof HttpsError) throw err;
      console.error("[createGuild] Gilden-Anlage fehlgeschlagen, Gold zurueckgebucht:", err);
      throw new HttpsError("internal", "Gilde konnte nicht angelegt werden.");
    }

    // 5) GuildId im Spielerprofil setzen (nach erfolgreicher Anlage).
    await profileRef.set(guildId);

    return { guildId };
  }
);
