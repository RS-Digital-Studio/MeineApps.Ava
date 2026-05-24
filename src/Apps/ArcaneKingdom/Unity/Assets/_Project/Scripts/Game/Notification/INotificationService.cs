#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Domain.Notification;

namespace ArcaneKingdom.Game.Notification
{
    /// <summary>
    /// Push-Notification-Abstraktion. In Phase 1 lokal via com.unity.mobile.notifications,
    /// in Phase 2 remote via Firebase Cloud Messaging (FCM).
    /// </summary>
    public interface INotificationService
    {
        IReadOnlyList<NotificationTemplate> AvailableTemplates { get; }
        bool OptedIn { get; set; }

        void Schedule(ScheduledNotification notification);
        void CancelById(string scheduledId);
        void CancelByKind(NotificationKind kind);
        IReadOnlyList<ScheduledNotification> ScheduledNotifications { get; }
    }
}
