using BomberBlast.Services;
using Firebase.Analytics;
using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BomberBlast.Droid;

/// <summary>
/// Android-Implementation für IAnalyticsService (Firebase Analytics).
/// Aktiv ab v2.0.56 — google-services.json + Xamarin.Firebase.Analytics-Binding vorhanden.
///
/// DSGVO-Kompliant: Initialize prueft den AnalyticsConsent-Flag (Schema V3) und
/// setzt SetAnalyticsCollectionEnabled entsprechend. Robert kann das auch zur
/// Laufzeit ueber die Privacy-Sektion der Settings-View togglen.
/// </summary>
public sealed class AndroidAnalyticsService : IAnalyticsService
{
    private readonly FirebaseAnalytics _analytics;

    public AndroidAnalyticsService(Android.Content.Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _analytics = FirebaseAnalytics.GetInstance(context);
    }

    public void Initialize()
    {
        try
        {
            var consent = App.Services?
                .GetService<IPreferencesService>()?
                .Get("AnalyticsConsent", false) ?? false;
            _analytics.SetAnalyticsCollectionEnabled(consent);
        }
        catch
        {
            // Best-Effort: Auf Init-Fehler stillschweigend defaulten auf "kein Tracking"
            try { _analytics.SetAnalyticsCollectionEnabled(false); } catch { /* */ }
        }
    }

    public void LogEvent(string eventName, IReadOnlyDictionary<string, object>? parameters = null)
    {
        if (string.IsNullOrEmpty(eventName)) return;

        // Firebase Analytics-Limits: Event-Name <= 40 chars, Param-Key <= 40 chars,
        // Param-String-Value <= 100 chars. Wir clampen defensiv.
        var name = eventName.Length > 40 ? eventName[..40] : eventName;

        try
        {
            if (parameters == null || parameters.Count == 0)
            {
                _analytics.LogEvent(name, null);
                return;
            }

            using var bundle = new Android.OS.Bundle();
            foreach (var (key, value) in parameters)
            {
                if (string.IsNullOrEmpty(key) || value == null) continue;
                var paramKey = key.Length > 40 ? key[..40] : key;
                switch (value)
                {
                    case string s:
                        var clamped = s.Length > 100 ? s[..100] : s;
                        bundle.PutString(paramKey, clamped);
                        break;
                    case int i:
                        bundle.PutInt(paramKey, i);
                        break;
                    case long l:
                        bundle.PutLong(paramKey, l);
                        break;
                    case double d:
                        bundle.PutDouble(paramKey, d);
                        break;
                    case float f:
                        bundle.PutFloat(paramKey, f);
                        break;
                    case bool b:
                        // Analytics kennt keinen Bool — als 1/0 senden
                        bundle.PutInt(paramKey, b ? 1 : 0);
                        break;
                    default:
                        var stringified = value.ToString() ?? string.Empty;
                        if (stringified.Length > 100) stringified = stringified[..100];
                        bundle.PutString(paramKey, stringified);
                        break;
                }
            }
            _analytics.LogEvent(name, bundle);
        }
        catch { /* Best-Effort */ }
    }

    public void SetUserProperty(string name, string? value)
    {
        if (string.IsNullOrEmpty(name)) return;
        var propName = name.Length > 24 ? name[..24] : name; // Limit: 24 chars
        var propValue = value?.Length > 36 ? value[..36] : value; // Limit: 36 chars
        try { _analytics.SetUserProperty(propName, propValue); }
        catch { /* Best-Effort */ }
    }

    public void SetAnalyticsCollectionEnabled(bool enabled)
    {
        try { _analytics.SetAnalyticsCollectionEnabled(enabled); }
        catch { /* Best-Effort */ }
    }
}
