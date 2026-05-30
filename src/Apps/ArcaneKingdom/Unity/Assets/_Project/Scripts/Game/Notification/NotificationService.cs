#nullable enable
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Notification;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcaneKingdom.Game.Notification
{
    /// <summary>
    /// Lokale Notification-Implementierung (STUB). Wird in der MVP-Phase mit
    /// com.unity.mobile.notifications für Android verdrahtet.
    /// </summary>
    public sealed class NotificationService : INotificationService
    {
        private const string OptInPrefsKey = "arcanekingdom.notifications.opted_in";

        private readonly IAnalyticsService _analytics;
        private readonly List<NotificationTemplate> _templates = new();
        private readonly List<ScheduledNotification> _scheduled = new();

        public NotificationService(IAnalyticsService analytics)
        {
            _analytics = analytics;
            LoadTemplates();
            _optedIn = PlayerPrefs.GetInt(OptInPrefsKey, 0) == 1;
        }

        public IReadOnlyList<NotificationTemplate> AvailableTemplates => _templates;
        public IReadOnlyList<ScheduledNotification> ScheduledNotifications => _scheduled;

        private bool _optedIn;
        public bool OptedIn
        {
            get => _optedIn;
            set
            {
                _optedIn = value;
                PlayerPrefs.SetInt(OptInPrefsKey, value ? 1 : 0);
                _analytics.Track("notifications_opt_changed", new Dictionary<string, object> { ["opted_in"] = value });
                // M19: Opt-Out muss auch die bereits am OS geplanten Notifications abbestellen —
                // sonst feuern sie weiter, obwohl der User abgelehnt hat. Nicht nur die In-Memory-Liste leeren.
                if (!value) CancelAllScheduled();
            }
        }

        public void Schedule(ScheduledNotification notification)
        {
            var template = _templates.FirstOrDefault(t => t.Kind == notification.Kind);
            if (template != null && template.RequiresOptIn && !_optedIn)
            {
                GameLogger.Verbose("Notification", $"Skip {notification.Kind} — User nicht opt-in.");
                return;
            }
            CancelById(notification.Id);
            _scheduled.Add(notification);
            GameLogger.Info("Notification", $"Scheduled {notification.Kind} fuer {notification.FireAtUtc:O}");
            // TODO MVP: AndroidNotificationCenter.SendNotification + NotificationChannel.
        }

        public void CancelById(string scheduledId)
        {
            var idx = _scheduled.FindIndex(n => n.Id == scheduledId);
            if (idx >= 0)
            {
                CancelAtOs(_scheduled[idx]);
                _scheduled.RemoveAt(idx);
            }
        }

        public void CancelByKind(NotificationKind kind)
        {
            for (var i = _scheduled.Count - 1; i >= 0; i--)
            {
                if (_scheduled[i].Kind != kind) continue;
                CancelAtOs(_scheduled[i]);
                _scheduled.RemoveAt(i);
            }
        }

        /// <summary>
        /// M19: Bestellt ALLE geplanten Notifications ab (OS + In-Memory). Wird beim Opt-Out
        /// aufgerufen, damit keine bereits geplante OS-Notification mehr feuert.
        /// </summary>
        private void CancelAllScheduled()
        {
            for (var i = _scheduled.Count - 1; i >= 0; i--)
                CancelAtOs(_scheduled[i]);
            _scheduled.Clear();
        }

        /// <summary>
        /// Bestellt eine einzelne geplante Notification am Betriebssystem ab. Zentraler Hook
        /// fuer die OS-Integration (Gegenstueck zum Schedule-OS-Hook).
        /// </summary>
        private void CancelAtOs(ScheduledNotification notification)
        {
            GameLogger.Verbose("Notification", $"Cancel {notification.Kind} ({notification.Id}) am OS.");
            // TODO MVP: AndroidNotificationCenter.CancelScheduledNotification(int) anhand der
            // OS-Notification-ID (gemappt aus notification.Id). Symmetrisch zum Schedule-Hook.
        }

        private void LoadTemplates()
        {
            var asset = Resources.Load<TextAsset>("Data/notifications");
            if (asset == null)
            {
                GameLogger.Warning("Notification", "Resources/Data/notifications.json fehlt.");
                return;
            }
            try
            {
                var list = JsonConvert.DeserializeObject<List<NotificationTemplate>>(asset.text);
                if (list != null) _templates.AddRange(list);
                GameLogger.Info("Notification", $"{_templates.Count} Notification-Templates geladen.");
            }
            catch (System.Exception ex)
            {
                GameLogger.Error("Notification", "Templates-Deserialisierung fehlgeschlagen", ex);
            }
        }
    }
}
