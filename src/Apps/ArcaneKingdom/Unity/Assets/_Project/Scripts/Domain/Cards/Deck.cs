#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArcaneKingdom.Domain.Cards
{
    /// <summary>
    /// Spieler-Deck mit max. 10 Karten + bis zu 4 Runen-Slot-Belegungen.
    /// Validierung der Deck-Limits (1/deck, Begrenzt:2) erfolgt beim Speichern in DeckValidator.
    /// </summary>
    [Serializable]
    public sealed class Deck
    {
        public const int MaxCards = 10;
        // Spielplan v5 Kap. 7.1: genau 4 Runen-Slots (Lvl 1/20/30/40), konsistent mit RuneSlotUnlock.MaxSlots.
        public const int MaxRuneSlots = 4;

        public int SlotIndex { get; }
        public string Name { get; set; }
        public List<string> CardInstanceIds { get; }      // bis zu MaxCards
        public List<string?> RuneInstanceIds { get; }      // genau MaxRuneSlots Einträge (null wenn leer)
        public DateTime LastModifiedUtc { get; set; }

        public Deck(int slotIndex, string name)
        {
            SlotIndex = slotIndex;
            Name = name;
            CardInstanceIds = new List<string>(MaxCards);
            RuneInstanceIds = new List<string?>(MaxRuneSlots);
            for (var i = 0; i < MaxRuneSlots; i++) RuneInstanceIds.Add(null);
            LastModifiedUtc = DateTime.UtcNow;
        }

        public bool IsValid => CardInstanceIds.Count > 0 && CardInstanceIds.Count <= MaxCards;
        public int CardCount => CardInstanceIds.Count;
        public int TotalCost(IReadOnlyDictionary<string, CardDefinition> definitions, IReadOnlyDictionary<string, CardInstance> instances)
        {
            var sum = 0;
            foreach (var instanceId in CardInstanceIds)
            {
                if (!instances.TryGetValue(instanceId, out var inst)) continue;
                if (!definitions.TryGetValue(inst.CardDefinitionId, out var def)) continue;
                sum += def.Cost;
            }
            return sum;
        }
    }
}
