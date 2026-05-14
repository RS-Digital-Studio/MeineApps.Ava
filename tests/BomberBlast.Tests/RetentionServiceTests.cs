using BomberBlast.Services;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für RetentionService (Phase 24 — O3-O5).
/// Validiert FirstWin-Idempotenz, FTUE-Skin-Tracking, Inactive-Detection und D1/D7-Windows.
/// </summary>
public class RetentionServiceTests
{
    [Fact]
    public void NeueInstanz_HatKeineFlags()
    {
        var prefs = new InMemoryPreferences();
        var svc = new RetentionService(prefs);
        svc.HasFirstWin.Should().BeFalse();
        svc.HasFtueSkinClaimed.Should().BeFalse();
        svc.DaysSinceLastSession.Should().Be(0);
    }

    [Fact]
    public void RegisterFirstWin_BeimErstenAufruf_TriggertTrue()
    {
        var prefs = new InMemoryPreferences();
        var svc = new RetentionService(prefs);
        svc.RegisterFirstWin().Should().BeTrue("Erster Aufruf signalisiert Trigger-Cinematic");
        svc.HasFirstWin.Should().BeTrue();
    }

    [Fact]
    public void RegisterFirstWin_BeimZweitenAufruf_GibtFalseZurueck()
    {
        var prefs = new InMemoryPreferences();
        var svc = new RetentionService(prefs);
        svc.RegisterFirstWin();
        svc.RegisterFirstWin().Should().BeFalse("Idempotent — kein zweiter Trigger");
    }

    [Fact]
    public void RegisterFirstWin_Persistiert()
    {
        var prefs = new InMemoryPreferences();
        var svc1 = new RetentionService(prefs);
        svc1.RegisterFirstWin();

        var svc2 = new RetentionService(prefs);
        svc2.HasFirstWin.Should().BeTrue("Über App-Restart hinweg erhalten");
        svc2.RegisterFirstWin().Should().BeFalse();
    }

    [Fact]
    public void MarkFtueSkinClaimed_PersistiertFlag()
    {
        var prefs = new InMemoryPreferences();
        var svc = new RetentionService(prefs);
        svc.MarkFtueSkinClaimed();

        svc.HasFtueSkinClaimed.Should().BeTrue();
        new RetentionService(prefs).HasFtueSkinClaimed.Should().BeTrue();
    }

    [Fact]
    public void TouchSession_SetztLastSessionDate()
    {
        var prefs = new InMemoryPreferences();
        var svc = new RetentionService(prefs);
        svc.TouchSession();

        // Direkt nach Touch sollte DaysSinceLastSession = 0 sein
        svc.DaysSinceLastSession.Should().Be(0);
    }

    [Fact]
    public void TouchSession_FirstSessionDate_NurEinmalGesetzt()
    {
        var prefs = new InMemoryPreferences();
        var svc = new RetentionService(prefs);

        svc.TouchSession();
        var firstRaw1 = prefs.Get("Retention_FirstSessionUtc", "");

        // Zweiter Touch — FirstSession darf nicht überschrieben werden
        svc.TouchSession();
        var firstRaw2 = prefs.Get("Retention_FirstSessionUtc", "");

        firstRaw1.Should().Be(firstRaw2, "FirstSession ist der D1/D7-Anker und darf nicht wandern");
    }

    [Fact]
    public void IsComebackEligible_OhneInaktivitaet_False()
    {
        var prefs = new InMemoryPreferences();
        var svc = new RetentionService(prefs);
        svc.TouchSession();
        svc.IsComebackEligible.Should().BeFalse("0 Tage inaktiv → kein Comeback");
    }

    [Fact]
    public void IsComebackEligible_BeiInaktivitaet_KannClaimedWerden()
    {
        var prefs = new InMemoryPreferences();
        // Faken: Letzte Session vor 5 Tagen
        prefs.Set("Retention_LastSessionUtc",
            DateTime.UtcNow.AddDays(-5).ToString("O"));
        var svc = new RetentionService(prefs);

        svc.IsComebackEligible.Should().BeTrue();

        svc.MarkComebackClaimed();
        svc.IsComebackEligible.Should().BeFalse("Direkt nach Claim wieder false");
    }

    [Fact]
    public void IsComebackEligible_NachClaim_BrauchtErneutInaktivitaet()
    {
        var prefs = new InMemoryPreferences();
        prefs.Set("Retention_LastSessionUtc", DateTime.UtcNow.AddDays(-5).ToString("O"));
        var svc = new RetentionService(prefs);
        svc.MarkComebackClaimed();

        // Comeback-LastClaim ist heute → braucht 3 Tage bevor wieder eligible
        prefs.Set("Retention_LastSessionUtc", DateTime.UtcNow.AddDays(-7).ToString("O"));
        svc.IsComebackEligible.Should().BeFalse("Comeback-Cooldown 3 Tage");
    }

    [Fact]
    public void DaysSinceLastSession_RechnetTagdiffenz()
    {
        var prefs = new InMemoryPreferences();
        prefs.Set("Retention_LastSessionUtc", DateTime.UtcNow.AddDays(-4).ToString("O"));
        var svc = new RetentionService(prefs);
        svc.DaysSinceLastSession.Should().BeGreaterThanOrEqualTo(3);
        svc.DaysSinceLastSession.Should().BeLessThanOrEqualTo(5); // Toleranz für UTC-Date-Boundary
    }
}
