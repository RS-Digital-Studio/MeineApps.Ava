#nullable enable
using System;
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.Save
{
    /// <summary>
    /// Chat-Moderations-State pro Spieler (Schema v2).
    /// </summary>
    [Serializable]
    public sealed class ChatSaveSlice
    {
        public HashSet<string> MutedPlayerIds { get; } = new();
        public List<ChatReportEntry> ReportsSent { get; } = new();
        public DateTime? MutedUntilUtc { get; set; }       // Bei Auto-Mute durch andere Reports
    }

    [Serializable]
    public sealed class ChatReportEntry
    {
        public string ReportedPlayerId { get; init; } = string.Empty;
        public string MessageId { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public DateTime ReportedAtUtc { get; init; }
    }
}
