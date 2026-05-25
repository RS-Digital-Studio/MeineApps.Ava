#nullable enable
using System;
using ArcaneKingdom.Domain.Economy;

namespace ArcaneKingdom.Domain.Save
{
    /// <summary>
    /// Persistierter Sternkarten-State (Schema v3).
    /// Kombiniert Karten-Inventar + Login-Tracker + Mythischer-Kern-Stand.
    /// </summary>
    [Serializable]
    public sealed class SternkartenSaveSlice
    {
        public SternkartenInventory Inventory { get; set; } = new();
        public LoginTracker Tracker { get; set; } = new();

        /// <summary>Anzahl Mythischer Kerne im Inventar (1 Kern = 3 Fragmente = fuer 6*-Crafting).</summary>
        public int MythicCoresAvailable { get; set; }
    }
}
