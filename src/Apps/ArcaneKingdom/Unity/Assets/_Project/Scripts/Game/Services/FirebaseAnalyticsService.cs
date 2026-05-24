#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;

namespace ArcaneKingdom.Game.Services
{
    /// <summary>
    /// Firebase-Analytics-Stub. Loggt aktuell nur in die Konsole. Wird in der MVP-Phase
    /// durch <c>Firebase.Analytics.FirebaseAnalytics</c> ergaenzt.
    /// </summary>
    public sealed class FirebaseAnalyticsService : IAnalyticsService
    {
        public void Track(string eventName, IReadOnlyDictionary<string, object>? properties = null)
        {
            if (properties == null || properties.Count == 0)
            {
                GameLogger.Verbose("Analytics", $"Event: {eventName}");
                return;
            }
            var props = string.Join(", ", FlattenPropertyPairs(properties));
            GameLogger.Verbose("Analytics", $"Event: {eventName} {{ {props} }}");
            // TODO MVP: FirebaseAnalytics.LogEvent(eventName, props.Select(p => new Parameter(p.Key, p.Value.ToString())).ToArray());
        }

        public void SetUserProperty(string key, string value)
        {
            GameLogger.Verbose("Analytics", $"UserProperty: {key} = {value}");
        }

        public void SetUserId(string userId)
        {
            GameLogger.Info("Analytics", $"UserId: {userId}");
        }

        private static IEnumerable<string> FlattenPropertyPairs(IReadOnlyDictionary<string, object> dict)
        {
            foreach (var kv in dict) yield return $"{kv.Key}={kv.Value}";
        }
    }
}
