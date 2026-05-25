#nullable enable
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.Progression
{
    /// <summary>
    /// Spielfeatures die durch Spieler-Level freigeschaltet werden
    /// (Arcane_Legends_Designplan Kap. 2.2 + Spielplan v5 Kap. 7.1).
    /// </summary>
    public enum AccountUnlock
    {
        /// <summary>Welt 1 + Tutorial + 5 Starter-Karten + Welt 1 Missionen 1-1 bis 1-3.</summary>
        SpielstartTutorial = 0,
        /// <summary>Welt 1 komplett + Zauberschmiede (Craften) + Deck-Slot 2 + Runen-Slot 1.</summary>
        ZauberschmiedeDeckSlot2 = 1,
        /// <summary>Welt 2 + Arena freigeschaltet (Trainings-Liga) + Runen koennen aufgewertet werden.</summary>
        ArenaTrainingsLiga = 2,
        /// <summary>Gilde beitreten/gruenden moeglich + Welt 3 + World-Chat freigeschaltet.</summary>
        GildenWorldChat = 3,
        /// <summary>Deck-Slot 3 + Uncommon-Crafting + Runen-Slot 2.</summary>
        DeckSlot3RunenSlot2 = 4,
        /// <summary>Welt 4 + Arena Bronze-Liga + Saison-Events Teilnahme.</summary>
        BronzeLigaSaisonEvents = 5,
        /// <summary>Rare-Crafting + Karten-Verschmelzung + Gilde-Tech Tier 1.</summary>
        RareCraftingVerschmelzung = 6,
        /// <summary>Welt 5-6 + Runen-Slot 3.</summary>
        RunenSlot3 = 7,
        /// <summary>Arena Silber-Liga + Dieb-Events aktiv + Klan-Karte Zugang bei Gilden-LV 3+.</summary>
        SilberLigaDiebEvents = 8,
        /// <summary>Welt 7-9 + Epic-Crafting + Gilde-Tech Tier 2 + Runen-Slot 4.</summary>
        EpicCraftingRunenSlot4 = 9,
        /// <summary>Arena Gold-Liga + Klan-Match Teilnahme.</summary>
        GoldLigaKlanMatches = 10,
        /// <summary>Legendary-Crafting + Gilde-Tech Tier 3.</summary>
        LegendaryCrafting = 11,
        /// <summary>Arena Platin-Liga + Weltboss-Events.</summary>
        PlatinLigaWeltboss = 12,
        /// <summary>Arena Meister-Liga + Saison-Legendarys + alle Welten.</summary>
        MeisterLigaSaisonLegendarys = 13,
        /// <summary>Account-Prestige aktiv: Runen-Rang-Aufstieg + Clan-Beitrags-Maximum erhoeht.</summary>
        AccountPrestige = 14,
        /// <summary>Endgame: Ewiges-Reich-Modus + unbegrenzte Arena-Saison + Weltboss-Phase-3.</summary>
        EndgameEwigesReich = 15,
        /// <summary>Maximum-Level + Prestige-Rune V + alle versteckten Karten zugaenglich.</summary>
        Prestige5RuneAllHiddenCards = 16
    }

    /// <summary>
    /// Mapping Spieler-Level -> freigeschaltete Features.
    /// </summary>
    public static class AccountUnlocks
    {
        /// <summary>Spieler-Level ab dem ein Feature freigeschaltet ist.</summary>
        public static int LevelFor(AccountUnlock unlock) => unlock switch
        {
            AccountUnlock.SpielstartTutorial          => 1,
            AccountUnlock.ZauberschmiedeDeckSlot2     => 5,
            AccountUnlock.ArenaTrainingsLiga          => 10,
            AccountUnlock.GildenWorldChat             => 15,
            AccountUnlock.DeckSlot3RunenSlot2         => 20,
            AccountUnlock.BronzeLigaSaisonEvents      => 25,
            AccountUnlock.RareCraftingVerschmelzung   => 30,
            AccountUnlock.RunenSlot3                  => 35,
            AccountUnlock.SilberLigaDiebEvents        => 40,
            AccountUnlock.EpicCraftingRunenSlot4      => 50,
            AccountUnlock.GoldLigaKlanMatches         => 60,
            AccountUnlock.LegendaryCrafting           => 70,
            AccountUnlock.PlatinLigaWeltboss          => 80,
            AccountUnlock.MeisterLigaSaisonLegendarys => 90,
            AccountUnlock.AccountPrestige             => 100,
            AccountUnlock.EndgameEwigesReich          => 120,
            AccountUnlock.Prestige5RuneAllHiddenCards => 150,
            _ => 1
        };

        /// <summary>Liefert true, wenn der Spieler bei <paramref name="playerLevel"/> das Feature hat.</summary>
        public static bool IsUnlocked(this AccountUnlock unlock, int playerLevel) =>
            playerLevel >= LevelFor(unlock);

        /// <summary>Alle Unlocks die bei <paramref name="playerLevel"/> aktiv sind, in Reihenfolge.</summary>
        public static IEnumerable<AccountUnlock> ActiveUnlocks(int playerLevel)
        {
            foreach (AccountUnlock u in System.Enum.GetValues(typeof(AccountUnlock)))
            {
                if (u.IsUnlocked(playerLevel)) yield return u;
            }
        }
    }
}
