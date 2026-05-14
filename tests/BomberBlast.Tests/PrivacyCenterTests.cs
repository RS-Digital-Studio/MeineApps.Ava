using BomberBlast.Services;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für PrivacyCenter (Phase 25b — Compliance).
/// </summary>
public class PrivacyCenterTests
{
    [Fact]
    public void NeueInstanz_AlleConsents_DefaultFalse_AusserPush()
    {
        var prefs = new InMemoryPreferences();
        var pc = new PrivacyCenter(prefs);

        pc.CrashlyticsConsent.Should().BeFalse();
        pc.AnalyticsConsent.Should().BeFalse();
        pc.PersonalizedAdsConsent.Should().BeFalse();
        pc.PushNotificationsConsent.Should().BeTrue("Push ist Default-True (UI-Hint, System-Permission ist die echte Hürde)");
        pc.ChildSafeMode.Should().BeFalse();
    }

    [Fact]
    public void Setter_PersistierenKorrekt()
    {
        var prefs = new InMemoryPreferences();
        var pc = new PrivacyCenter(prefs);
        pc.CrashlyticsConsent = true;
        pc.AnalyticsConsent = true;

        var pc2 = new PrivacyCenter(prefs);
        pc2.CrashlyticsConsent.Should().BeTrue();
        pc2.AnalyticsConsent.Should().BeTrue();
    }

    [Fact]
    public void ConsentChanged_FeuertBeiAenderung()
    {
        var prefs = new InMemoryPreferences();
        var pc = new PrivacyCenter(prefs);
        var fireCount = 0;
        pc.ConsentChanged += () => fireCount++;

        pc.CrashlyticsConsent = true;
        pc.AnalyticsConsent = true;
        pc.PersonalizedAdsConsent = true;

        fireCount.Should().Be(3);
    }

    [Fact]
    public void ConsentChanged_FeuertNichtBeiGleichemWert()
    {
        var prefs = new InMemoryPreferences();
        var pc = new PrivacyCenter(prefs);
        pc.CrashlyticsConsent = true;
        var fireCount = 0;
        pc.ConsentChanged += () => fireCount++;

        pc.CrashlyticsConsent = true; // gleicher Wert
        fireCount.Should().Be(0);
    }

    [Fact]
    public void RejectAll_LoeschtAlleConsents()
    {
        var prefs = new InMemoryPreferences();
        var pc = new PrivacyCenter(prefs);
        pc.AcceptAll();
        pc.RejectAll();

        pc.CrashlyticsConsent.Should().BeFalse();
        pc.AnalyticsConsent.Should().BeFalse();
        pc.PersonalizedAdsConsent.Should().BeFalse();
        pc.PushNotificationsConsent.Should().BeFalse();
    }

    [Fact]
    public void AcceptAll_AktiviertAlleAusserChildSafeMode()
    {
        var prefs = new InMemoryPreferences();
        var pc = new PrivacyCenter(prefs);
        pc.AcceptAll();

        pc.CrashlyticsConsent.Should().BeTrue();
        pc.AnalyticsConsent.Should().BeTrue();
        pc.PersonalizedAdsConsent.Should().BeTrue();
        pc.PushNotificationsConsent.Should().BeTrue();
        pc.ChildSafeMode.Should().BeFalse("ChildSafeMode ist eine Eltern-Einstellung, kein Consent");
    }

    [Fact]
    public void ChildSafeMode_AktiviertDeaktiviertPersonalizedAds()
    {
        var prefs = new InMemoryPreferences();
        var pc = new PrivacyCenter(prefs);
        pc.PersonalizedAdsConsent = true;

        pc.ChildSafeMode = true;
        pc.PersonalizedAdsConsent.Should().BeFalse("ChildSafe-Mode → kein Behavioral-Targeting");
    }

    [Fact]
    public void AcceptAll_BeiChildSafeMode_LaesstPersonalizedAdsAus()
    {
        var prefs = new InMemoryPreferences();
        var pc = new PrivacyCenter(prefs);
        pc.ChildSafeMode = true;

        pc.AcceptAll();

        pc.PersonalizedAdsConsent.Should().BeFalse("AcceptAll respektiert ChildSafeMode");
        pc.CrashlyticsConsent.Should().BeTrue("Andere Consents werden trotzdem aktiviert");
    }

    [Fact]
    public void GetActiveDataFlows_LiefertSnapshot()
    {
        var prefs = new InMemoryPreferences();
        var pc = new PrivacyCenter(prefs);
        pc.AcceptAll();

        var flows = pc.GetActiveDataFlows();
        flows.Should().NotBeEmpty();
        flows.Should().Contain(f => f.NameKey == "Privacy_DataFlow_Crashlytics" && f.Active);
        flows.Should().Contain(f => f.NameKey == "Privacy_DataFlow_Leaderboard" && f.Active);
    }

    [Fact]
    public void GetActiveDataFlows_NachReject_KernFlowsBleibenAktiv()
    {
        var prefs = new InMemoryPreferences();
        var pc = new PrivacyCenter(prefs);
        pc.RejectAll();

        var flows = pc.GetActiveDataFlows();
        flows.Should().Contain(f => f.NameKey == "Privacy_DataFlow_Crashlytics" && !f.Active);
        flows.Should().Contain(f => f.NameKey == "Privacy_DataFlow_Leaderboard" && f.Active,
            "Liga ist Kern-Funktion ohne Opt-out");
    }

    [Fact]
    public void DataFlowDescriptor_HasReceiverString()
    {
        var prefs = new InMemoryPreferences();
        var pc = new PrivacyCenter(prefs);
        var flows = pc.GetActiveDataFlows();
        foreach (var f in flows)
        {
            f.DataReceiver.Should().NotBeNullOrEmpty();
            f.NameKey.Should().StartWith("Privacy_DataFlow_");
        }
    }
}
