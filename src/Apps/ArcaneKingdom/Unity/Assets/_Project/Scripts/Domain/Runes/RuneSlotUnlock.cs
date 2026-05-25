#nullable enable
namespace ArcaneKingdom.Domain.Runes
{
    /// <summary>
    /// Freischaltungs-Logik der 4 Runen-Slots (Spielplan v5 Kap. 7.1).
    /// Slot 1 ab LV1 (immer), Slot 2 ab LV20, Slot 3 ab LV30, Slot 4 ab LV40.
    /// </summary>
    public static class RuneSlotUnlock
    {
        public const int MaxSlots = 4;
        public const int Slot1MinLevel = 1;
        public const int Slot2MinLevel = 20;
        public const int Slot3MinLevel = 30;
        public const int Slot4MinLevel = 40;

        /// <summary>
        /// Mindest-Level fuer einen Slot-Index (1-basiert: Slot 1 = Index 1).
        /// </summary>
        public static int MinLevelForSlot(int slotIndex) => slotIndex switch
        {
            1 => Slot1MinLevel,
            2 => Slot2MinLevel,
            3 => Slot3MinLevel,
            4 => Slot4MinLevel,
            _ => int.MaxValue
        };

        /// <summary>
        /// Liefert true, wenn der Spieler diesen Slot benutzen darf.
        /// </summary>
        public static bool IsUnlocked(int slotIndex, int playerLevel) =>
            slotIndex >= 1 && slotIndex <= MaxSlots && playerLevel >= MinLevelForSlot(slotIndex);

        /// <summary>
        /// Anzahl der bei diesem Spieler-Level offenen Runen-Slots (0-4).
        /// </summary>
        public static int UnlockedSlotCount(int playerLevel)
        {
            var count = 0;
            for (var s = 1; s <= MaxSlots; s++)
                if (IsUnlocked(s, playerLevel)) count++;
            return count;
        }
    }

    /// <summary>
    /// Analog: Deck-Slot-Freischaltung. Deck 1 immer, Deck 2 ab LV5, Deck 3 ab LV20.
    /// </summary>
    public static class DeckSlotUnlock
    {
        public const int MaxDecks = 3;
        public const int Deck1MinLevel = 1;
        public const int Deck2MinLevel = 5;
        public const int Deck3MinLevel = 20;

        public static int MinLevelForDeck(int deckIndex) => deckIndex switch
        {
            1 => Deck1MinLevel,
            2 => Deck2MinLevel,
            3 => Deck3MinLevel,
            _ => int.MaxValue
        };

        public static bool IsUnlocked(int deckIndex, int playerLevel) =>
            deckIndex >= 1 && deckIndex <= MaxDecks && playerLevel >= MinLevelForDeck(deckIndex);

        public static int UnlockedDeckCount(int playerLevel)
        {
            var count = 0;
            for (var d = 1; d <= MaxDecks; d++)
                if (IsUnlocked(d, playerLevel)) count++;
            return count;
        }
    }
}
