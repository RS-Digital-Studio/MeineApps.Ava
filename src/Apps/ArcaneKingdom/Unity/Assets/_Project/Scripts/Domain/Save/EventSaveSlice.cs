#nullable enable
using System;
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.Save
{
    /// <summary>
    /// Persistierter Event-State (Schema v3, Designplan v4 Oeko Kap. 2).
    /// Saison-Events mit Event-Punkten + Notfall-Kauf-Tracking.
    /// </summary>
    [Serializable]
    public sealed class EventSaveSlice
    {
        /// <summary>
        /// Event-Punkte pro Event-ID (z.B. "event_yule_fest" -> 7500).
        /// </summary>
        public Dictionary<string, int> EventPointsByEventId { get; } = new();

        /// <summary>
        /// Welche Event-Belohnungen (Punkte-Schwellen) wurden bereits abgeholt?
        /// Format: "{eventId}:{threshold}" (z.B. "event_yule_fest:5000").
        /// </summary>
        public HashSet<string> ClaimedThresholds { get; } = new();

        /// <summary>
        /// Hat der Spieler den Notfall-Kauf einer Event-Karte schon getaetigt?
        /// Format: "{eventId}:{cardId}".
        /// </summary>
        public HashSet<string> EmergencyPurchases { get; } = new();

        /// <summary>
        /// Notfall-Kauf-Slot wurde freigeschaltet (= letzter Tag des Events erreicht)?
        /// </summary>
        public HashSet<string> EmergencyUnlockedEventIds { get; } = new();

        /// <summary>Helper: Punkte fuer ein Event abrufen.</summary>
        public int GetPoints(string eventId)
            => EventPointsByEventId.TryGetValue(eventId, out var p) ? p : 0;

        /// <summary>Helper: Punkte zu einem Event hinzufuegen.</summary>
        public void AddPoints(string eventId, int delta)
        {
            if (delta <= 0) return;
            EventPointsByEventId[eventId] = GetPoints(eventId) + delta;
        }
    }
}
