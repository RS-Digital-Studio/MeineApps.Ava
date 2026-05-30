using System;
using System.Text.Json;
using BomberBlast.Models.League;
using BomberBlast.Services;
using FluentAssertions;
using MeineApps.Core.Ava.Localization;
using NSubstitute;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Regression fuer den Saison-Wechsel im LeagueService:
/// (#2) Beim Saisonende MUESSEN Points + SeasonRewardClaimed zurueckgesetzt werden — frueher
/// hat der Ctor (SyncSeasonNumber) die SeasonNumber hochgezogen, wodurch CheckAndProcessSeasonEnd
/// sofort returnte und den Reset nie erreichte.
/// (#11) Auf-/Abstieg basiert auf dem zuletzt ONLINE ermittelten Perzentil; ohne Online-Rang
/// (LastOnlinePercentile = -1) gibt es keinen unverdienten Auf-/Abstieg.
/// </summary>
public class LeagueSeasonEndTests
{
    // SeasonEpoch im Service ist 2026-02-24; "heute" liegt mehrere Saisons danach → eine
    // gespeicherte SeasonNumber=1 erzwingt im Ctor einen Saison-Uebergang.
    private const int OldSeason = 1;

    // Muss zur Serialisierung im LeagueService passen (CamelCase), sonst landen Properties
    // beim Deserialisieren auf ihren Defaults.
    private static readonly JsonSerializerOptions LeagueJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static LeagueService CreateWithStoredData(LeagueData stored)
    {
        var prefs = new InMemoryPreferences();
        prefs.Set("LeagueData", JsonSerializer.Serialize(stored, LeagueJson));

        return new LeagueService(
            prefs,
            Substitute.For<ICoinService>(),
            Substitute.For<IGemService>(),
            Substitute.For<ILocalizationService>(),
            Substitute.For<IFirebaseService>(),
            new Lazy<IAchievementService>(() => Substitute.For<IAchievementService>()));
    }

    [Fact]
    public void Ctor_SeasonChanged_ResetsPointsAndReward()
    {
        var svc = CreateWithStoredData(new LeagueData
        {
            SeasonNumber = OldSeason,
            Points = 5000,
            SeasonRewardClaimed = true,
            CurrentTier = LeagueTier.Gold,
        });

        svc.CurrentPoints.Should().Be(0, "Saisonpunkte muessen beim Saisonwechsel zurueckgesetzt werden");
        svc.IsSeasonRewardClaimed.Should().BeFalse("der Reward-Status der alten Saison darf nicht in die neue uebernommen werden");
    }

    [Fact]
    public void Ctor_SeasonChanged_NoOnlineRank_KeepsTier()
    {
        var svc = CreateWithStoredData(new LeagueData
        {
            SeasonNumber = OldSeason,
            Points = 5000,
            CurrentTier = LeagueTier.Gold,
            LastOnlinePercentile = -1f, // nie online ermittelt
        });

        svc.CurrentTier.Should().Be(LeagueTier.Gold, "ohne echten Online-Rang darf es keinen Auf-/Abstieg geben");
    }

    [Fact]
    public void Ctor_SeasonChanged_TopOnlineRank_Promotes()
    {
        var svc = CreateWithStoredData(new LeagueData
        {
            SeasonNumber = OldSeason,
            Points = 9000,
            CurrentTier = LeagueTier.Gold,
            LastOnlinePercentile = 0.01f, // Top 1% der letzten Online-Rangliste
        });

        svc.CurrentTier.Should().Be(LeagueTier.Platinum, "ein Top-Perzentil muss zum Aufstieg fuehren");
    }
}
