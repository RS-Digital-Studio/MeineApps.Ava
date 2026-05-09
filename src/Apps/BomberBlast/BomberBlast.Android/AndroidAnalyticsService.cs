using BomberBlast.Services;

namespace BomberBlast.Droid;

/// <summary>
/// Android-Implementation für IAnalyticsService (Firebase Analytics).
///
/// AKTUELLER STAND: Stub. Wird zur funktionalen Implementation sobald Console-Setup + NuGet erledigt sind.
///
/// SETUP-VORAUSSETZUNGEN (Robert):
/// 1. Firebase Analytics in der Firebase-Console aktivieren (kostenlos)
/// 2. NuGet: `Plugin.Firebase.Analytics` zu Directory.Packages.props
/// 3. MainActivity.cs: `App.AnalyticsServiceFactory = sp =&gt; new AndroidAnalyticsService();` in OnCreate
///
/// DSGVO-Kompliant: Initialize prüft den User-Consent-Flag (Schema V3 Key `AnalyticsConsent`).
/// Wenn Consent nicht erteilt: SetAnalyticsCollectionEnabled(false).
///
/// IMPLEMENTATION-CODE (auskommentiert):
/// <code>
/// using Plugin.Firebase.Analytics;
/// CrossFirebaseAnalytics.Current.LogEvent(eventName, parameters);
/// CrossFirebaseAnalytics.Current.SetUserProperty(name, value);
/// CrossFirebaseAnalytics.Current.SetAnalyticsCollectionEnabled(enabled);
/// </code>
/// </summary>
public sealed class AndroidAnalyticsService : IAnalyticsService
{
    public void Initialize()
    {
        // TODO nach Firebase-Setup:
        // var consent = App.Services?.GetService<MeineApps.Core.Ava.Services.IPreferencesService>()?.Get("AnalyticsConsent", false) ?? false;
        // CrossFirebaseAnalytics.Current.SetAnalyticsCollectionEnabled(consent);
    }

    public void LogEvent(string eventName, IReadOnlyDictionary<string, object>? parameters = null)
    {
        if (string.IsNullOrEmpty(eventName)) return;

        // TODO nach Firebase-Setup:
        // var firebaseParams = new Dictionary<string, object>();
        // if (parameters != null)
        // {
        //     foreach (var (k, v) in parameters)
        //     {
        //         var clamped = v?.ToString() is { } s && s.Length > 100 ? s[..100] : v;
        //         firebaseParams[k] = clamped ?? string.Empty;
        //     }
        // }
        // CrossFirebaseAnalytics.Current.LogEvent(eventName, firebaseParams);
    }

    public void SetUserProperty(string name, string? value)
    {
        if (string.IsNullOrEmpty(name)) return;
        // TODO: CrossFirebaseAnalytics.Current.SetUserProperty(name, value);
    }

    public void SetAnalyticsCollectionEnabled(bool enabled)
    {
        // TODO: CrossFirebaseAnalytics.Current.SetAnalyticsCollectionEnabled(enabled);
    }
}
