#nullable enable
using System;
using ArcaneKingdom.Domain.World;

namespace ArcaneKingdom.UI.Modals
{
    /// <summary>
    /// Daten-Transfer-Context fuer den DifficultyPickerModal.
    /// Caller setzt Node + verfuegbare Energie, das Modal liest das beim OnEnter aus
    /// und liefert die gewaehlte Difficulty via Callback (oder via einen weiteren Context).
    /// </summary>
    public sealed class DifficultyPickerContext
    {
        /// <summary>Welt-Node fuer den der Kampf gestartet werden soll.</summary>
        public NodeDefinition? Node { get; set; }
        /// <summary>Welt-ID (fuer Belohnungs- und Sterne-Anzeige).</summary>
        public string? WorldId { get; set; }
        /// <summary>Verfuegbare Energie des Spielers (fuer Disable-Logik).</summary>
        public int AvailableEnergy { get; set; }
        /// <summary>Bisher beste Wertung auf diesem Node (0-4 Sterne).</summary>
        public int BestStarsSoFar { get; set; }
        /// <summary>Callback wenn der Spieler eine Difficulty waehlt — Caller startet den Kampf.</summary>
        public Action<NodeDifficulty>? OnDifficultySelected { get; set; }

        public void Reset()
        {
            Node = null;
            WorldId = null;
            AvailableEnergy = 0;
            BestStarsSoFar = 0;
            OnDifficultySelected = null;
        }
    }
}
