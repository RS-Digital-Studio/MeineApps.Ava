#nullable enable
using System.Collections.Generic;

namespace ArcaneKingdom.Core.Services
{
    /// <summary>
    /// Telemetrie-Abstraktion. Sammelt Events lokal bei Offline und sendet beim nächsten Connect.
    /// </summary>
    public interface IAnalyticsService
    {
        void Track(string eventName, IReadOnlyDictionary<string, object>? properties = null);
        void SetUserProperty(string key, string value);
        void SetUserId(string userId);
    }
}
