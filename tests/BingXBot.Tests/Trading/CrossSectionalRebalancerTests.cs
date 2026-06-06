using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Trading.CrossSectional;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Trading;

/// <summary>
/// Real-Money-Safety-Tests fuer den <see cref="CrossSectionalRebalancer"/>: Close-vor-Open,
/// Seiten-Flip, Min-Order-Skip, kein Hedge bei fehlgeschlagenem Close, Leverage-Cap, Equity-Sizing.
/// </summary>
public class CrossSectionalRebalancerTests
{
    private static CrossSectionalSettings Cfg(int longK = 1, int shortK = 1, int levCap = 1) => new()
    {
        LongK = longK, ShortK = shortK, LeverageCap = levCap, MarginUtilization = 0.75m, RebalanceDays = 21,
    };

    private static RiskSettings Risk() => new() { MaxOpenPositions = 10 };

    private static Dictionary<string, MarketCategory> Crypto(params string[] symbols) =>
        symbols.ToDictionary(s => s, _ => MarketCategory.Crypto);

    [Fact]
    public async Task Reconcile_SchliesstAbgewaehlte_OeffnetNeue_BehaeltKorrekte()
    {
        var ex = new FakeExchangeClient()
            .WithPosition("AAA", Side.Buy, 1m, 100m)   // im Ziel → behalten
            .WithPosition("BBB", Side.Buy, 1m, 100m);  // nicht im Ziel → schliessen
        var target = new Dictionary<string, Side> { ["AAA"] = Side.Buy, ["CCC"] = Side.Sell };
        var prices = new Dictionary<string, decimal> { ["AAA"] = 100m, ["BBB"] = 100m, ["CCC"] = 50m };

        var r = await CrossSectionalRebalancer.ReconcileAsync(ex, target, prices, Crypto("AAA", "BBB", "CCC"), Cfg(), Risk());

        ex.ClosePositionCalls.Should().Contain(("BBB", Side.Buy));
        ex.ClosePositionCalls.Should().NotContain(("AAA", Side.Buy));      // bereits korrekt → kein Close
        ex.PlaceOrderCalls.Select(p => (p.Symbol, p.Side)).Should().Contain(("CCC", Side.Sell));
        ex.PlaceOrderCalls.Select(p => p.Symbol).Should().NotContain("AAA"); // kein Re-Open
        r.Closed.Should().Be(1);
        r.Opened.Should().Be(1);
    }

    [Fact]
    public async Task Reconcile_SeitenFlip_SchliesstAltOeffnetNeu()
    {
        var ex = new FakeExchangeClient().WithPosition("AAA", Side.Buy, 1m, 100m);
        var target = new Dictionary<string, Side> { ["AAA"] = Side.Sell };
        var prices = new Dictionary<string, decimal> { ["AAA"] = 100m };

        var r = await CrossSectionalRebalancer.ReconcileAsync(ex, target, prices, Crypto("AAA"), Cfg(), Risk());

        ex.ClosePositionCalls.Should().Contain(("AAA", Side.Buy));
        ex.PlaceOrderCalls.Select(p => (p.Symbol, p.Side)).Should().Contain(("AAA", Side.Sell));
        r.Closed.Should().Be(1);
        r.Opened.Should().Be(1);
    }

    [Fact]
    public async Task Reconcile_UnterMinOrder_OeffnetNicht()
    {
        var ex = new FakeExchangeClient { MinOrderQty = 1_000_000m };
        var target = new Dictionary<string, Side> { ["AAA"] = Side.Buy };
        var prices = new Dictionary<string, decimal> { ["AAA"] = 100m };

        var r = await CrossSectionalRebalancer.ReconcileAsync(ex, target, prices, Crypto("AAA"), Cfg(), Risk());

        ex.PlaceOrderCalls.Should().BeEmpty();
        r.Opened.Should().Be(0);
        r.SkippedMinOrder.Should().Be(1);
    }

    [Fact]
    public async Task Reconcile_FehlgeschlagenerClose_OeffnetGegenseiteNicht()
    {
        var ex = new FakeExchangeClient { FailCloses = true }.WithPosition("AAA", Side.Buy, 1m, 100m);
        var target = new Dictionary<string, Side> { ["AAA"] = Side.Sell };
        var prices = new Dictionary<string, decimal> { ["AAA"] = 100m };

        var r = await CrossSectionalRebalancer.ReconcileAsync(ex, target, prices, Crypto("AAA"), Cfg(), Risk());

        r.FailedClose.Should().Be(1);
        r.Opened.Should().Be(0);
        ex.PlaceOrderCalls.Should().BeEmpty();   // KEIN ungewollter Hedge (Long+Short auf AAA)
    }

    [Fact]
    public async Task Reconcile_KapptLeverage()
    {
        var ex = new FakeExchangeClient();
        var target = new Dictionary<string, Side> { ["AAA"] = Side.Buy };
        var prices = new Dictionary<string, decimal> { ["AAA"] = 100m };

        await CrossSectionalRebalancer.ReconcileAsync(ex, target, prices, Crypto("AAA"), Cfg(levCap: 1), Risk());

        ex.SetLeverageCalls.Should().Contain(("AAA", 1, Side.Buy));   // min(KategorieLev, 1) = 1
    }

    [Fact]
    public async Task Reconcile_SiztEquityGleichgewichtet()
    {
        // equity 10000, slots = LongK1+ShortK1 = 2, util 0.75 → perSlotMargin 3750; lev1, price 150 → qty 25.
        var ex = new FakeExchangeClient { AccountEquity = 10000m };
        var target = new Dictionary<string, Side> { ["AAA"] = Side.Buy };
        var prices = new Dictionary<string, decimal> { ["AAA"] = 150m };

        await CrossSectionalRebalancer.ReconcileAsync(ex, target, prices, Crypto("AAA"), Cfg(longK: 1, shortK: 1, levCap: 1), Risk());

        ex.PlaceOrderCalls.Should().ContainSingle();
        ex.PlaceOrderCalls[0].Qty.Should().BeApproximately(25m, 0.0001m);
    }
}
