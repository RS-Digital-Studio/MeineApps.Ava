#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Runes;

namespace ArcaneKingdom.Domain.Player
{
    /// <summary>
    /// Zentrale Save-Datenstruktur (cloud-synced). Wird vom ISaveService geladen und gespeichert.
    /// Aggregiert Profil, Waehrungen, Inventar, Decks, Welt-Fortschritt.
    /// </summary>
    [Serializable]
    public sealed class PlayerSave
    {
        public int SchemaVersion { get; set; } = 1;
        public PlayerProfile Profile { get; set; }
        public PlayerCurrencies Currencies { get; set; }
        public Dictionary<string, CardInstance> CardInventory { get; set; }   // Key: InstanceId
        public Dictionary<string, RuneInstance> RuneInventory { get; set; }   // Key: InstanceId
        public List<Deck> Decks { get; set; }
        public int ActiveDeckSlot { get; set; }
        public Dictionary<string, WorldProgress> WorldProgress { get; set; }
        public Dictionary<string, int> AchievementProgress { get; set; }      // Key: AchievementId, Value: Step
        public DateTime LastEnergyRegenAtUtc { get; set; }
        public DateTime LastSavedAtUtc { get; set; }

        public PlayerSave(PlayerProfile profile)
        {
            Profile = profile;
            Currencies = new PlayerCurrencies();
            CardInventory = new Dictionary<string, CardInstance>();
            RuneInventory = new Dictionary<string, RuneInstance>();
            Decks = new List<Deck> { new Deck(0, "Deck 1") };
            ActiveDeckSlot = 0;
            WorldProgress = new Dictionary<string, WorldProgress>();
            AchievementProgress = new Dictionary<string, int>();
            LastEnergyRegenAtUtc = DateTime.UtcNow;
            LastSavedAtUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Pro-Welt-Fortschritt: Sterne pro Node (0-4).
    /// </summary>
    [Serializable]
    public sealed class WorldProgress
    {
        public string WorldId { get; }
        public Dictionary<string, int> StarsByNodeId { get; }
        public DateTime LastPlayedAtUtc { get; set; }

        public WorldProgress(string worldId)
        {
            WorldId = worldId;
            StarsByNodeId = new Dictionary<string, int>();
            LastPlayedAtUtc = DateTime.UtcNow;
        }

        public int TotalStars
        {
            get
            {
                var sum = 0;
                foreach (var kv in StarsByNodeId) sum += kv.Value;
                return sum;
            }
        }
    }
}
