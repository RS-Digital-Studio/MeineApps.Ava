using BomberBlast.Models;
using BomberBlast.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für LoadoutService (v2.0.41 + v2.0.45 — AAA-Audit).
/// Validiert Cost-Tabellen, Max-2-Boosts-Limit, Coin-vs-Gem-Pfade, atomare Buchung.
/// </summary>
public class LoadoutServiceTests
{
    private static (LoadoutService Service, ICoinService Coins, IGemService Gems, IPreferencesServiceMock Prefs) CreateService(
        int coinBalance = 100_000, int gemBalance = 1000)
    {
        var prefs = new IPreferencesServiceMock();
        var coins = Substitute.For<ICoinService>();
        var gems = Substitute.For<IGemService>();

        coins.CanAfford(Arg.Any<int>()).Returns(call => coinBalance >= (int)call[0]);
        coins.TrySpendCoins(Arg.Any<int>()).Returns(call =>
        {
            int amount = (int)call[0];
            if (coinBalance < amount) return false;
            coinBalance -= amount;
            return true;
        });
        gems.CanAfford(Arg.Any<int>()).Returns(call => gemBalance >= (int)call[0]);
        gems.TrySpendGems(Arg.Any<int>()).Returns(call =>
        {
            int amount = (int)call[0];
            if (gemBalance < amount) return false;
            gemBalance -= amount;
            return true;
        });

        var service = new LoadoutService(prefs, coins, gems);
        return (service, coins, gems, prefs);
    }

    [Fact]
    public void GetCoinCost_AlleBoostTypen_HabenPositivePreise()
    {
        var (service, _, _, _) = CreateService();
        foreach (LoadoutBoostType type in Enum.GetValues<LoadoutBoostType>())
        {
            service.GetCoinCost(type).Should().BeGreaterThan(0, $"{type} sollte Coin-Kosten haben");
            service.GetGemCost(type).Should().BeGreaterThan(0, $"{type} sollte Gem-Kosten haben");
        }
    }

    [Fact]
    public void GetCoinCost_Invincibility_TeurerAlsExtraBomb()
    {
        var (service, _, _, _) = CreateService();
        service.GetCoinCost(LoadoutBoostType.Invincibility)
            .Should().BeGreaterThan(service.GetCoinCost(LoadoutBoostType.ExtraBomb));
    }

    [Fact]
    public void Purchase_LeereListe_LiefertNull()
    {
        var (service, _, _, _) = CreateService();
        service.Purchase(level: 5, boosts: Array.Empty<LoadoutBoostType>(), useGems: false).Should().BeNull();
    }

    [Fact]
    public void Purchase_DreiBoostsZuVielKickedZurueck()
    {
        var (service, _, _, _) = CreateService();
        var boosts = new[]
        {
            LoadoutBoostType.ExtraBomb,
            LoadoutBoostType.ExtraFire,
            LoadoutBoostType.SpeedBoost
        };
        service.Purchase(level: 5, boosts: boosts, useGems: false).Should().BeNull("Max 2 Boosts pro Level");
    }

    [Fact]
    public void Purchase_NichtGenugCoins_LiefertNull()
    {
        var (service, _, _, _) = CreateService(coinBalance: 50);
        var boosts = new[] { LoadoutBoostType.ExtraBomb }; // 300 Coins
        service.Purchase(level: 5, boosts: boosts, useGems: false).Should().BeNull();
    }

    [Fact]
    public void Purchase_GenugCoins_SpeichertLoadout()
    {
        var (service, coins, _, _) = CreateService();
        var boosts = new[] { LoadoutBoostType.ExtraBomb, LoadoutBoostType.SpeedBoost };
        var result = service.Purchase(level: 5, boosts: boosts, useGems: false);

        result.Should().NotBeNull();
        result!.Should().HaveCount(2);
        coins.Received().TrySpendCoins(500); // 300 + 200
    }

    [Fact]
    public void Purchase_NutztGems_SpendetGemsNichtCoins()
    {
        var (service, coins, gems, _) = CreateService();
        var boosts = new[] { LoadoutBoostType.Invincibility }; // 8 Gems
        var result = service.Purchase(level: 5, boosts: boosts, useGems: true);

        result.Should().NotBeNull();
        gems.Received().TrySpendGems(8);
        coins.DidNotReceive().TrySpendCoins(Arg.Any<int>());
    }

    [Fact]
    public void GetSavedLoadout_NichtVorhanden_LiefertLeereListe()
    {
        var (service, _, _, _) = CreateService();
        service.GetSavedLoadout(99).Should().BeEmpty();
    }

    [Fact]
    public void ClearLoadout_EntferntPersistierteEinträge()
    {
        var (service, _, _, _) = CreateService();
        service.Purchase(level: 7, boosts: new[] { LoadoutBoostType.ExtraBomb }, useGems: false);
        service.GetSavedLoadout(7).Should().NotBeEmpty();

        service.ClearLoadout(7);
        service.GetSavedLoadout(7).Should().BeEmpty();
    }
}

/// <summary>
/// Test-Helper: konkreter Subtyp für expliziteren Typnamen in Test-Setups.
/// </summary>
public class IPreferencesServiceMock : InMemoryPreferences { }
