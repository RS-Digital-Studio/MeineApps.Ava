#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Runes;
using ArcaneKingdom.Domain.Save;
using ArcaneKingdom.Domain.Tutorial;

namespace ArcaneKingdom.Domain.Player
{
    /// <summary>
    /// Zentrale Save-Datenstruktur (cloud-synced). Wird vom ISaveService geladen und gespeichert.
    /// Aggregiert Profil, Währungen, Inventar, Decks, Welt-Fortschritt + Schema-v2-Slices + v3-Slices.
    /// </summary>
    [Serializable]
    public sealed class PlayerSave
    {
        public int SchemaVersion { get; set; } = SaveMigrator.CurrentSchemaVersion;
        public PlayerProfile Profile { get; set; }
        public PlayerCurrencies Currencies { get; set; }
        public Dictionary<string, CardInstance> CardInventory { get; set; }   // Key: InstanceId
        public Dictionary<string, RuneInstance> RuneInventory { get; set; }   // Key: InstanceId
        public List<Deck> Decks { get; set; }
        public int ActiveDeckSlot { get; set; }
        public Dictionary<string, WorldProgress> WorldProgress { get; set; }

        // v2: Schema-Erweiterungen
        public TutorialProgress Tutorial { get; set; }
        public AchievementSaveSlice Achievements { get; set; }
        public FriendsSaveSlice FriendsSlice { get; set; }
        public ChatSaveSlice ChatSlice { get; set; }
        public List<PendingClaim> PendingClaims { get; set; }
        public Dictionary<string, int> PackPityCounters { get; set; }          // Key: packId, Value: Packs ohne Legendary
        public HashSet<string> UnlockedFeatureKeys { get; set; }
        public Dictionary<string, int> SaisonPassXp { get; set; }              // Key: seasonId, Value: SaisonXp

        // v3: Designplan v4-Erweiterungen
        /// <summary>Prestige-Stufen pro Welt (I/II/III/IV) + Prestige-IV-Karten-Unlocks.</summary>
        public PrestigeSaveSlice Prestige { get; set; }
        /// <summary>Sternkarten-Inventar + Login-Tracker + Mythische-Kern-Fragmente.</summary>
        public SternkartenSaveSlice Sternkarten { get; set; }
        /// <summary>Story-Status: Rasse, Erinnerungs-Fragmente, Karten-Persoenlichkeit-Tracking, Endkampf-Wahl.</summary>
        public StorySaveSlice Story { get; set; }
        /// <summary>Saison-Event-Status: Punkte + abgeholte Schwellen + Notfall-Kaeufe.</summary>
        public EventSaveSlice Events { get; set; }
        /// <summary>Favoriten-Karten (Schutz vor versehentlicher Fusion). Map InstanceId -> markiert.</summary>
        public HashSet<string> FavoritedCardInstanceIds { get; set; }
        /// <summary>Bereits eingeloeste Sammlungs-Sets (Spielplan v5 Kap. 5.6) — verhindert Doppel-Eintausch.</summary>
        public HashSet<string> ClaimedCollectionSetIds { get; set; }

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
            Tutorial = new TutorialProgress();
            Achievements = new AchievementSaveSlice();
            FriendsSlice = new FriendsSaveSlice();
            ChatSlice = new ChatSaveSlice();
            PendingClaims = new List<PendingClaim>();
            PackPityCounters = new Dictionary<string, int>();
            UnlockedFeatureKeys = new HashSet<string>();
            SaisonPassXp = new Dictionary<string, int>();
            // v3
            Prestige = new PrestigeSaveSlice();
            Sternkarten = new SternkartenSaveSlice();
            Story = new StorySaveSlice();
            Events = new EventSaveSlice();
            FavoritedCardInstanceIds = new HashSet<string>();
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
