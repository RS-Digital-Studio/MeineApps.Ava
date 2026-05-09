using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Default-Implementation von <see cref="IPrivacyCenter"/> (Phase 25b — AAA-Audit Compliance).
///
/// <para>Bestehende Consent-Keys (CrashlyticsConsent, AnalyticsConsent) bleiben kompatibel —
/// PrivacyCenter ist eine Façade über IPreferencesService mit zusätzlichen DSGVO/COPPA-Toggles
/// und einer Audit-Liste.</para>
/// </summary>
public sealed class PrivacyCenter : IPrivacyCenter
{
    private const string KeyCrashlytics = "CrashlyticsConsent";   // Bestand
    private const string KeyAnalytics = "AnalyticsConsent";       // Bestand
    private const string KeyPersonalizedAds = "Privacy_PersonalizedAds";
    private const string KeyPushNotifications = "Privacy_PushNotifications";
    private const string KeyChildSafeMode = "Privacy_ChildSafeMode";

    private readonly IPreferencesService _prefs;

    public event Action? ConsentChanged;

    public PrivacyCenter(IPreferencesService prefs)
    {
        _prefs = prefs;
    }

    public bool CrashlyticsConsent
    {
        get => _prefs.Get(KeyCrashlytics, false);
        set { if (CrashlyticsConsent == value) return; _prefs.Set(KeyCrashlytics, value); ConsentChanged?.Invoke(); }
    }

    public bool AnalyticsConsent
    {
        get => _prefs.Get(KeyAnalytics, false);
        set { if (AnalyticsConsent == value) return; _prefs.Set(KeyAnalytics, value); ConsentChanged?.Invoke(); }
    }

    public bool PersonalizedAdsConsent
    {
        get => _prefs.Get(KeyPersonalizedAds, false);
        set { if (PersonalizedAdsConsent == value) return; _prefs.Set(KeyPersonalizedAds, value); ConsentChanged?.Invoke(); }
    }

    public bool PushNotificationsConsent
    {
        // Default true (Push macht Sinn als Standard, aber DSGVO-Schutz: User kann opt-out).
        // Auf Android 13+ ist das System-Permission-Prompt die ECHTE Hürde — dieser Toggle ist UI-Hint.
        get => _prefs.Get(KeyPushNotifications, true);
        set { if (PushNotificationsConsent == value) return; _prefs.Set(KeyPushNotifications, value); ConsentChanged?.Invoke(); }
    }

    public bool ChildSafeMode
    {
        get => _prefs.Get(KeyChildSafeMode, false);
        set
        {
            if (ChildSafeMode == value) return;
            _prefs.Set(KeyChildSafeMode, value);
            // ChildSafeMode aktiviert → automatisch Personalized-Ads aus
            if (value && PersonalizedAdsConsent)
            {
                _prefs.Set(KeyPersonalizedAds, false);
            }
            ConsentChanged?.Invoke();
        }
    }

    public IReadOnlyList<DataFlowDescriptor> GetActiveDataFlows()
    {
        var list = new List<DataFlowDescriptor>(8);
        list.Add(new DataFlowDescriptor(
            "Privacy_DataFlow_Crashlytics", "Privacy_DataFlow_Crashlytics_Purpose",
            "Google Firebase", CrashlyticsConsent));
        list.Add(new DataFlowDescriptor(
            "Privacy_DataFlow_Analytics", "Privacy_DataFlow_Analytics_Purpose",
            "Google Firebase", AnalyticsConsent));
        list.Add(new DataFlowDescriptor(
            "Privacy_DataFlow_PersonalizedAds", "Privacy_DataFlow_PersonalizedAds_Purpose",
            "Google AdMob", PersonalizedAdsConsent && !ChildSafeMode));
        list.Add(new DataFlowDescriptor(
            "Privacy_DataFlow_PushNotifications", "Privacy_DataFlow_PushNotifications_Purpose",
            "Firebase Cloud Messaging", PushNotificationsConsent));
        list.Add(new DataFlowDescriptor(
            "Privacy_DataFlow_Leaderboard", "Privacy_DataFlow_Leaderboard_Purpose",
            "Firebase Realtime Database", true)); // Liga ist Kern-Funktion, kein Opt-out
        list.Add(new DataFlowDescriptor(
            "Privacy_DataFlow_CloudSave", "Privacy_DataFlow_CloudSave_Purpose",
            "Firebase Realtime Database", true)); // Cloud-Save ist Kern-Funktion
        return list;
    }

    public void RejectAll()
    {
        CrashlyticsConsent = false;
        AnalyticsConsent = false;
        PersonalizedAdsConsent = false;
        PushNotificationsConsent = false;
        // ChildSafeMode bleibt unangetastet (das ist eine Eltern-Einstellung, kein Consent)
    }

    public void AcceptAll()
    {
        // ChildSafeMode aktiv → kein Behavioral-Targeting auch bei Accept-All
        if (!ChildSafeMode)
        {
            PersonalizedAdsConsent = true;
        }
        CrashlyticsConsent = true;
        AnalyticsConsent = true;
        PushNotificationsConsent = true;
    }
}
