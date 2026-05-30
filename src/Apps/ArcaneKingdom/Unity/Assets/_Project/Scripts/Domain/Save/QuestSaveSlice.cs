#nullable enable
using System;
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.Save
{
    /// <summary>
    /// Persistierter Quest-Zustand (Schema v4). Ohne diese Slice ging der Quest-Fortschritt bei
    /// jedem Neustart verloren und bereits eingeloeste Quests konnten nach einem Reset erneut
    /// geclaimed werden (Belohnungs-Exploit).
    /// </summary>
    [Serializable]
    public sealed class QuestSaveSlice
    {
        /// <summary>Aktueller Zaehlerstand pro Quest-Id.</summary>
        public Dictionary<string, int> CountByQuestId { get; set; } = new();

        /// <summary>Quests, deren Belohnung bereits abgeholt wurde (Re-Claim-Schutz ueber Neustart/Reset hinweg).</summary>
        public HashSet<string> ClaimedQuestIds { get; set; } = new();

        /// <summary>Zeitpunkt des letzten Daily-Resets (UTC) — datumsbasiert, unabhaengig von der Energie-Uhr.</summary>
        public DateTime LastDailyResetUtc { get; set; }

        /// <summary>Zeitpunkt des letzten Weekly-Resets (UTC).</summary>
        public DateTime LastWeeklyResetUtc { get; set; }
    }
}
