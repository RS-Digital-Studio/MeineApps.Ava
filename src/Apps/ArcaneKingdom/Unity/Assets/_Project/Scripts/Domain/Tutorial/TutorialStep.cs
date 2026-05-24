#nullable enable
using System;

namespace ArcaneKingdom.Domain.Tutorial
{
    /// <summary>
    /// Tutorial-Schritt-Definition (First-Time-User-Experience).
    /// </summary>
    [Serializable]
    public sealed class TutorialStep
    {
        public string Id { get; init; } = string.Empty;
        public int Order { get; init; }
        public string TitleKey { get; init; } = string.Empty;
        public string BodyKey { get; init; } = string.Empty;
        public string TriggerEvent { get; init; } = string.Empty;   // z.B. "hub_entered", "first_battle_won"
        public string? HighlightTargetId { get; init; }              // optional fuer UI-Pulse-Highlight
        public bool Skippable { get; init; } = true;
    }

    /// <summary>
    /// Persistierte Spieler-Tutorial-Progress (gehoert in PlayerSave-Schema v2).
    /// </summary>
    [Serializable]
    public sealed class TutorialProgress
    {
        public string CurrentStepId { get; set; } = string.Empty;
        public bool TutorialCompleted { get; set; }
        public bool TutorialSkipped { get; set; }
    }
}
