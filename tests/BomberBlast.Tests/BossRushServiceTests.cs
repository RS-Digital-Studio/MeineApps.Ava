using BomberBlast.Services;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für BossRushService (v2.0.41 + v2.0.45 — ).
/// Validiert Wochen-Reset, Best-Score-Tiebreaker (Time bei gleichem Score),
/// Total-Completions-Lifetime, ISO-8601-Year-Week-Logic.
/// </summary>
public class BossRushServiceTests
{
    [Fact]
    public void BossSequence_HatGenau5Bosse()
    {
        var prefs = new InMemoryPreferences();
        var service = new BossRushService(prefs);
        service.BossSequence.Should().HaveCount(5);
    }

    [Fact]
    public void Initial_KeinRun_HasRunThisWeekFalse()
    {
        var prefs = new InMemoryPreferences();
        var service = new BossRushService(prefs);
        service.HasRunThisWeek.Should().BeFalse();
        service.WeeklyBestScore.Should().Be(0);
        service.HasEverCompleted.Should().BeFalse();
    }

    [Fact]
    public void SubmitRun_ErsterRun_GibtTrueZurueck()
    {
        var prefs = new InMemoryPreferences();
        var service = new BossRushService(prefs);

        var isNewBest = service.SubmitRun(finalScore: 5000, totalTimeSeconds: 120f, completedAllBosses: false);

        isNewBest.Should().BeTrue();
        service.WeeklyBestScore.Should().Be(5000);
        service.HasRunThisWeek.Should().BeTrue();
    }

    [Fact]
    public void SubmitRun_HoehererScore_NeuerBestEberscheint()
    {
        var prefs = new InMemoryPreferences();
        var service = new BossRushService(prefs);
        service.SubmitRun(5000, 120f, false);

        var isNewBest = service.SubmitRun(7000, 130f, false);

        isNewBest.Should().BeTrue();
        service.WeeklyBestScore.Should().Be(7000);
    }

    [Fact]
    public void SubmitRun_NiedrigererScore_NichtAlsBest()
    {
        var prefs = new InMemoryPreferences();
        var service = new BossRushService(prefs);
        service.SubmitRun(7000, 120f, false);

        var isNewBest = service.SubmitRun(5000, 100f, false);

        isNewBest.Should().BeFalse();
        service.WeeklyBestScore.Should().Be(7000);
    }

    [Fact]
    public void SubmitRun_GleicherScoreSchnellerZeit_GibtNeuenBest()
    {
        var prefs = new InMemoryPreferences();
        var service = new BossRushService(prefs);
        service.SubmitRun(5000, 120f, false);

        var isNewBest = service.SubmitRun(5000, 100f, false);

        isNewBest.Should().BeTrue("Tiebreaker: kürzere Zeit gewinnt");
        service.WeeklyBestTime.Should().Be(100f);
    }

    [Fact]
    public void SubmitRun_CompletedAllBosses_ErhoehtTotalCompletions()
    {
        var prefs = new InMemoryPreferences();
        var service = new BossRushService(prefs);

        service.SubmitRun(10000, 300f, completedAllBosses: true);
        service.SubmitRun(8000, 250f, completedAllBosses: false);
        service.SubmitRun(12000, 280f, completedAllBosses: true);

        service.TotalCompletions.Should().Be(2);
        service.HasEverCompleted.Should().BeTrue();
    }

    [Fact]
    public void CurrentWeekId_HatFormat_yyyy_Www()
    {
        var prefs = new InMemoryPreferences();
        var service = new BossRushService(prefs);

        service.CurrentWeekId.Should().MatchRegex(@"^\d{4}-W\d{2}$");
    }

    [Fact]
    public void Persistenz_ZweiteInstanzLiestVorherigeWerte()
    {
        var prefs = new InMemoryPreferences();
        var service1 = new BossRushService(prefs);
        service1.SubmitRun(5000, 120f, completedAllBosses: true);

        var service2 = new BossRushService(prefs);

        service2.WeeklyBestScore.Should().Be(5000);
        service2.TotalCompletions.Should().Be(1);
    }
}
