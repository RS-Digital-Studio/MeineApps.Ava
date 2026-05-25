#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.World;

namespace ArcaneKingdom.Domain.Save
{
    /// <summary>
    /// Persistierter Prestige-State pro Spieler (Schema v3).
    /// Pro Welt eine Prestige-Stufe (Normal/I/II/III/IV).
    /// </summary>
    [Serializable]
    public sealed class PrestigeSaveSlice
    {
        /// <summary>Map worldId -> aktuelle Prestige-Stufe.</summary>
        public Dictionary<string, PrestigeStufe> StufenByWorldId { get; } = new();

        /// <summary>Welche Prestige-IV-Karten der Spieler bereits freigeschaltet hat (worldId -> wurde gezogen).</summary>
        public HashSet<string> Prestige4CardsUnlocked { get; } = new();

        /// <summary>Letzte Berechnung des passiven Daily-Income (UTC) — fuer Idle-Tick.</summary>
        public DateTime LastDailyIncomeAtUtc { get; set; }

        public PrestigeStufe Get(string worldId)
            => StufenByWorldId.TryGetValue(worldId, out var s) ? s : PrestigeStufe.Normal;

        public void Set(string worldId, PrestigeStufe stufe)
            => StufenByWorldId[worldId] = stufe;
    }
}
