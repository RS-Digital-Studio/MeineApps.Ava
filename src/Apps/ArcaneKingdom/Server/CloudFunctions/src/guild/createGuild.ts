import { onCall, HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";

interface CreateGuildRequest {
  name: string;
  tag: string;
  slogan: string;
}

// Erstellt eine Gilde mit eindeutigem Tag (Firestore-Transaction).
// Server zieht 50 000 Gold ab und legt das Mitglieds-Subdokument an.
export const createGuild = onCall<CreateGuildRequest>({ region: "europe-west1" }, async (request) => {
  const uid = request.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Login required.");
  const { name, tag, slogan } = request.data;
  if (!tag || tag.length !== 5) throw new HttpsError("invalid-argument", "Tag must be exactly 5 chars.");
  if (!name || name.length < 3 || name.length > 20) {
    throw new HttpsError("invalid-argument", "Name must be 3-20 chars.");
  }

  const tagRef = admin.firestore().collection("guildTags").doc(tag);
  const result = await admin.firestore().runTransaction(async (tx) => {
    const existing = await tx.get(tagRef);
    if (existing.exists) throw new HttpsError("already-exists", "Tag already in use.");
    const guildRef = admin.firestore().collection("guilds").doc();
    const guildId = guildRef.id;
    tx.set(tagRef, { guildId });
    tx.set(guildRef, {
      name, tag, slogan,
      level: 1,
      leaderId: uid,
      memberCount: 1,
      createdAtMs: Date.now(),
    });
    tx.set(guildRef.collection("members").doc(uid), {
      role: "Leader",
      joinedAtMs: Date.now(),
    });
    return { guildId };
  });

  // Gold abziehen + GuildId im Player setzen
  const playerRef = admin.database().ref(`players/${uid}`);
  await playerRef.child("currencies/gold").transaction((g) => (g ?? 0) - 50_000);
  await playerRef.child("profile/guildId").set(result.guildId);

  return { guildId: result.guildId };
});
