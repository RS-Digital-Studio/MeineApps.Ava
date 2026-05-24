import { onSchedule } from "firebase-functions/v2/scheduler";
import * as admin from "firebase-admin";

// Taeglicher Cron: schreibt Gold/Diamanten/Scraps aus kontrollierten Gebieten
// in die Gilden-Treasury (Common 1k Gold/Mitglied, Legendaer 20k + 100 Diamanten/Mitglied).
export const dailyTerritoryTick = onSchedule(
  { schedule: "every day 00:00", timeZone: "UTC", region: "europe-west1" },
  async () => {
    const guildsSnap = await admin.firestore().collection("guilds").get();
    for (const guildDoc of guildsSnap.docs) {
      const guild = guildDoc.data();
      const territories = (guild.territoryIds ?? []) as string[];
      const memberCount = (guild.memberCount ?? 1) as number;
      if (territories.length === 0) continue;

      let gold = 0;
      let diamonds = 0;
      for (const territoryId of territories) {
        const territorySnap = await admin.firestore().collection("territories").doc(territoryId).get();
        const rarity = (territorySnap.get("rarity") ?? "Common") as string;
        const { goldPerMember, diamondsPerMember } = bonusForRarity(rarity);
        gold += goldPerMember * memberCount;
        diamonds += diamondsPerMember * memberCount;
      }
      await guildDoc.ref.update({
        "treasury.gold": admin.firestore.FieldValue.increment(gold),
        "treasury.diamonds": admin.firestore.FieldValue.increment(diamonds),
      });
      console.log(`[dailyTerritoryTick] Gilde ${guildDoc.id}: +${gold} Gold, +${diamonds} Diamanten`);
    }
  }
);

function bonusForRarity(rarity: string): { goldPerMember: number; diamondsPerMember: number } {
  switch (rarity) {
    case "Rare":      return { goldPerMember: 3_000, diamondsPerMember: 0 };
    case "Epic":      return { goldPerMember: 8_000, diamondsPerMember: 50 };
    case "Legendaer": return { goldPerMember: 20_000, diamondsPerMember: 100 };
    default:          return { goldPerMember: 1_000, diamondsPerMember: 0 };
  }
}
