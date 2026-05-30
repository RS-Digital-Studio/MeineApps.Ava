import { onSchedule } from "firebase-functions/v2/scheduler";
import * as admin from "firebase-admin";

// Firestore getAll() verarbeitet bis zu 100 Refs pro Aufruf — wir chunken Territorien
// in dieser Groesse, um N+1-Reads (ein get() pro Territorium) zu vermeiden (H23).
const TERRITORY_READ_CHUNK = 100;

// Taeglicher Cron: schreibt Gold/Diamanten aus kontrollierten Gebieten in die
// Gilden-Treasury.
//
// Doppellauf-Schutz (H23): Pro Gilde wird in EINER Firestore-Transaction geprueft,
// ob `lastTerritoryTickDay` (YYYY-MM-DD UTC) bereits dem heutigen Tag entspricht.
// Nur wenn der Marker != heute ist, wird increment angewendet UND der Marker gesetzt.
// Ein erneuter Lauf am selben Tag findet den Marker und ueberspringt den increment.
export const dailyTerritoryTick = onSchedule(
  { schedule: "every day 00:00", timeZone: "UTC", region: "europe-west1" },
  async () => {
    const today = utcDayKey(new Date());
    const guildsSnap = await admin.firestore().collection("guilds").get();

    for (const guildDoc of guildsSnap.docs) {
      const guild = guildDoc.data();
      const territories = (guild.territoryIds ?? []) as string[];
      const memberCount = (guild.memberCount ?? 1) as number;
      if (!Array.isArray(territories) || territories.length === 0) continue;

      // Territorien gebuendelt laden (getAll statt N einzelner get()).
      const rarities = await loadTerritoryRarities(territories);

      let gold = 0;
      let diamonds = 0;
      for (const rarity of rarities) {
        const { goldPerMember, diamondsPerMember } = bonusForRarity(rarity);
        gold += goldPerMember * memberCount;
        diamonds += diamondsPerMember * memberCount;
      }

      // Transaktionaler Doppellauf-Schutz: increment nur, wenn heute noch nicht getickt.
      const applied = await admin.firestore().runTransaction(async (tx) => {
        const fresh = await tx.get(guildDoc.ref);
        const lastDay = (fresh.get("lastTerritoryTickDay") ?? "") as string;
        if (lastDay === today) return false; // heute bereits abgerechnet
        tx.update(guildDoc.ref, {
          "treasury.gold": admin.firestore.FieldValue.increment(gold),
          "treasury.diamonds": admin.firestore.FieldValue.increment(diamonds),
          lastTerritoryTickDay: today,
        });
        return true;
      });

      if (applied) {
        console.log(`[dailyTerritoryTick] Gilde ${guildDoc.id}: +${gold} Gold, +${diamonds} Diamanten`);
      } else {
        console.log(`[dailyTerritoryTick] Gilde ${guildDoc.id}: heute bereits getickt — uebersprungen.`);
      }
    }
  }
);

// Laedt die rarity-Werte aller Territorien gebuendelt via getAll() (chunked).
async function loadTerritoryRarities(territoryIds: string[]): Promise<string[]> {
  const db = admin.firestore();
  const result: string[] = [];
  for (let i = 0; i < territoryIds.length; i += TERRITORY_READ_CHUNK) {
    const chunk = territoryIds.slice(i, i + TERRITORY_READ_CHUNK);
    const refs = chunk.map((id) => db.collection("territories").doc(id));
    const snaps = await db.getAll(...refs);
    for (const snap of snaps) {
      result.push((snap.get("rarity") ?? "Common") as string);
    }
  }
  return result;
}

// Erzeugt den UTC-Tagesschluessel YYYY-MM-DD.
function utcDayKey(date: Date): string {
  const y = date.getUTCFullYear();
  const m = String(date.getUTCMonth() + 1).padStart(2, "0");
  const d = String(date.getUTCDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

function bonusForRarity(rarity: string): { goldPerMember: number; diamondsPerMember: number } {
  switch (rarity) {
    case "Rare":      return { goldPerMember: 3_000, diamondsPerMember: 0 };
    case "Epic":      return { goldPerMember: 8_000, diamondsPerMember: 50 };
    case "Legendaer": return { goldPerMember: 20_000, diamondsPerMember: 100 };
    default:          return { goldPerMember: 1_000, diamondsPerMember: 0 };
  }
}
