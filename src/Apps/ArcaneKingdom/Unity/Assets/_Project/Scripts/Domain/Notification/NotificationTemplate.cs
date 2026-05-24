#nullable enable
using System;

namespace ArcaneKingdom.Domain.Notification
{
    public enum NotificationKind
    {
        EnergyFull = 0,
        ThiefSpawned = 1,
        KlanMatch = 2,
        DailyReward = 3,
        SeasonEnd = 4
    }

    [Serializable]
    public sealed class NotificationTemplate
    {
        public string Id { get; init; } = string.Empty;
        public NotificationKind Kind { get; init; }
        public string TitleKey { get; init; } = string.Empty;
        public string BodyKey { get; init; } = string.Empty;
        public int DefaultDelaySeconds { get; init; }            // Wenn relativ scheduled
        public bool RequiresOptIn { get; init; } = true;
    }

    [Serializable]
    public sealed class ScheduledNotification
    {
        public string Id { get; init; } = string.Empty;
        public NotificationKind Kind { get; init; }
        public DateTime FireAtUtc { get; init; }
        public string TitleKey { get; init; } = string.Empty;
        public string BodyKey { get; init; } = string.Empty;
        public string? BodyParam0 { get; init; }                 // z.B. Dieb-Typ, Klan-Match-Zeit
    }
}
