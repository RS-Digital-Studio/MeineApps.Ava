using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Default-Implementation von <see cref="IPrivacyCenter"/>.
///
/// <para>Façade über <see cref="IPreferencesService"/> für alle DSGVO/COPPA-relevanten Toggles.
/// Crashlytics + Analytics sind permanent deaktiviert (Firebase-SDKs raus aus dem Build).
/// Der AnalyticsConsent-Toggle bleibt fuer kuenftige Provider als persistierter User-Wert
/// erhalten.</para>
/// </summary>
public sealed class PrivacyCenter : IPrivacyCenter
{
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
        var list = new List<DataFlowDescriptor>(5);
        list.Add(new DataFlowDescriptor(
            "Privacy_DataFlow_Analytics", "Privacy_DataFlow_Analytics_Purpose",
            "Lokal (kein Backend aktiv)", AnalyticsConsent));
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
        AnalyticsConsent = true;
        PushNotificationsConsent = true;
    }
}
