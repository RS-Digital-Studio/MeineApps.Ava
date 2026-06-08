#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Onboarding
{
    /// <summary>
    /// Persistenter FTUE-State. 1:1-Port aus dem Avalonia-Original (Models/FtueStep.cs).
    /// Die FtueStep-Definitionen + FtueExpectedAction (Spotlight/UI-Interaktion) bleiben für die
    /// Unity-Präsentationsschicht; hier nur der reine Fortschritt. Persistenz: Newtonsoft.Json.
    /// </summary>
    public sealed class FtueState
    {
        /// <summary>Aktuell aktiver Step-Index (0-basiert). -1 wenn FTUE noch nicht gestartet.</summary>
        public int CurrentStepIndex { get; set; } = -1;

        /// <summary>True wenn FTUE komplett abgeschlossen.</summary>
        public bool IsCompleted { get; set; }

        /// <summary>True wenn der Spieler das gesamte FTUE manuell abgebrochen hat.</summary>
        public bool WasSkipped { get; set; }

        /// <summary>IDs der bereits absolvierten Steps (idempotent für Replay-Safety).</summary>
        public HashSet<string> CompletedStepIds { get; set; } = new HashSet<string>();

        /// <summary>Wann FTUE gestartet wurde (UTC, ISO 8601).</summary>
        public string? StartedAtIso { get; set; }

        /// <summary>Wann FTUE abgeschlossen wurde (UTC, ISO 8601).</summary>
        public string? CompletedAtIso { get; set; }
    }
}
