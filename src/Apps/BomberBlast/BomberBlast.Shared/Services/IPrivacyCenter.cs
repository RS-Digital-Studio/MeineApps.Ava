namespace BomberBlast.Services;

/// <summary>
/// Privacy-Center (Phase 25b — AAA-Audit Compliance).
///
/// <para>Zentralisiert ALLE DSGVO-relevanten Toggles in einem Service. Vorher waren die
/// Settings auf SettingsViewModel + IAccessibilityService verteilt (CrashlyticsConsent,
/// AnalyticsConsent direkt via Preferences). Hier:</para>
///
/// <para>EU DSA Art. 15-22 + DSGVO Art. 6/7: Spieler hat Recht auf:</para>
/// <list type="bullet">
///   <item>Information über Verarbeitung (Privacy-Center zeigt aktive Datenflüsse).</item>
///   <item>Widerruf der Einwilligung jederzeit (alle Toggles).</item>
///   <item>Löschung der Daten (Account-Deletion via <see cref="IAccountDeletionService"/>).</item>
///   <item>Export der Daten (via <see cref="IDataExportService"/>).</item>
/// </list>
///
/// <para>Pflicht-Disclosure (Lootbox-Compliance UK/China): Drop-Rates für LuckySpin sind über
/// <see cref="ILuckySpinService.GetDropRates"/> öffentlich abrufbar.</para>
/// </summary>
public interface IPrivacyCenter
{
    // === Consent-Toggles ===

    /// <summary>Crashlytics-Berichte (Crash-Daten an Firebase senden).</summary>
    bool CrashlyticsConsent { get; set; }

    /// <summary>Analytics-Events (Funnel-Tracking an Firebase senden).</summary>
    bool AnalyticsConsent { get; set; }

    /// <summary>Personalisierte Werbung (Behavioral-Targeting via AdMob).</summary>
    bool PersonalizedAdsConsent { get; set; }

    /// <summary>Push-Notifications (Re-Engagement, Daily-Reminder).</summary>
    bool PushNotificationsConsent { get; set; }

    /// <summary>
    /// COPPA-Toggle: Spieler ist <13 → kontextuelle Ads only, kein Behavioral-Targeting,
    /// kein Personal-Data-Sharing. Default false.
    /// </summary>
    bool ChildSafeMode { get; set; }

    // === Lookup-API für Audit/Anzeige ===

    /// <summary>Liefert eine snapshot-Liste aller aktiven Daten-Flüsse für UI-Anzeige.</summary>
    IReadOnlyList<DataFlowDescriptor> GetActiveDataFlows();

    /// <summary>Setzt ALLE Consent-Toggles auf false (Reject-All-Pattern).</summary>
    void RejectAll();

    /// <summary>Setzt alle Consent-Toggles auf true (Accept-All-Pattern, DSGVO-konform nur wenn aktiv gewählt).</summary>
    void AcceptAll();

    /// <summary>Wird gefeuert wenn sich ein Consent-Wert ändert (für UI-Refresh + Service-Re-Init).</summary>
    event Action? ConsentChanged;
}

/// <summary>Beschreibung eines aktiven Daten-Flusses für Audit-Anzeige.</summary>
public sealed class DataFlowDescriptor
{
    public DataFlowDescriptor(string nameKey, string purposeKey, string dataReceiver, bool active)
    {
        NameKey = nameKey;
        PurposeKey = purposeKey;
        DataReceiver = dataReceiver;
        Active = active;
    }

    /// <summary>RESX-Key für lokalisierten Daten-Fluss-Namen.</summary>
    public string NameKey { get; }

    /// <summary>RESX-Key für Zweck-Beschreibung (z.B. "Crash-Berichte zur Stabilitätsanalyse").</summary>
    public string PurposeKey { get; }

    /// <summary>Daten-Empfänger (z.B. "Google Firebase", "Google AdMob").</summary>
    public string DataReceiver { get; }

    /// <summary>True wenn Spieler-Consent aktiv ist und Daten tatsächlich gesendet werden.</summary>
    public bool Active { get; }
}
