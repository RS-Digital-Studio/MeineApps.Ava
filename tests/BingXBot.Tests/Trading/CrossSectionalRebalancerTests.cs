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

    // Bewusst der echte Live-Default (3): MaxOpenPositions darf seit dem Oversizing-Fix
    // (11.08.2026) NICHT mehr in den Xsec-Sizing-Divisor eingehen.
    private static RiskSettings Risk() => new() { MaxOpenPositions = 3 };

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

        // closeSettleDelay Zero: der Fail-Close bleibt in jedem Read sichtbar — ohne Zero wuerde
        // der Settle-Retry hier real 3×2 s warten, bevor er den Close als gescheitert wertet.
        var r = await CrossSectionalRebalancer.ReconcileAsync(
            ex, target, prices, Crypto("AAA"), Cfg(), Risk(), closeSettleDelay: TimeSpan.Zero);

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
        // equity 10000, Divisor = Korbgroesse (target.Count = 1), util 0.75 → perSlotMargin 7500;
        // lev1, price 150 → qty 50. (Vor dem Oversizing-Fix war der Divisor LongK+ShortK.)
        var ex = new FakeExchangeClient { AccountEquity = 10000m };
        var target = new Dictionary<string, Side> { ["AAA"] = Side.Buy };
        var prices = new Dictionary<string, decimal> { ["AAA"] = 150m };

        await CrossSectionalRebalancer.ReconcileAsync(ex, target, prices, Crypto("AAA"), Cfg(longK: 1, shortK: 1, levCap: 1), Risk());

        ex.PlaceOrderCalls.Should().ContainSingle();
        ex.PlaceOrderCalls[0].Qty.Should().BeApproximately(50m, 0.0001m);
    }

    [Fact]
    public async Task Reconcile_SizingDivisor_IstKorbgroesse_NichtMaxOpenPositions()
    {
        // Regressionstest Oversizing-Befund 11.08.2026: 6 Ziel-Slots + MaxOpenPositions=3 (Live-Default)
        // → Divisor MUSS 6 sein (equity 12000 × 0.75 / 6 = 1500 Margin/Slot; lev1, price 100 → qty 15),
        // und ALLE 6 Slots werden eroeffnet. Der alte Code teilte durch min(6,3)=3 → qty 30 (doppelt).
        var ex = new FakeExchangeClient { AccountEquity = 12000m };
        var target = new Dictionary<string, Side>
        {
            ["L1"] = Side.Buy, ["L2"] = Side.Buy, ["L3"] = Side.Buy,
            ["S1"] = Side.Sell, ["S2"] = Side.Sell, ["S3"] = Side.Sell,
        };
        var prices = target.Keys.ToDictionary(s => s, _ => 100m);

        await CrossSectionalRebalancer.ReconcileAsync(
            ex, target, prices, Crypto([.. target.Keys]), Cfg(longK: 3, shortK: 3, levCap: 1), Risk());

        ex.PlaceOrderCalls.Should().HaveCount(6);
        ex.PlaceOrderCalls.Should().OnlyContain(p => Math.Abs(p.Qty - 15m) < 0.0001m);
    }

    [Fact]
    public async Task Reconcile_SettleLatenz_KeinFalsePositiveFailedClose()
    {
        // BingX-Settle-Latenz: der Positions-Snapshot zeigt die eben geschlossene Position noch
        // 2 Reads lang. Ohne Settle-Retry: failedClose=1 und der Hedge-Guard blockt den
        // Seiten-Flip fuer den ganzen Rebalance-Zyklus.
        var ex = new FakeExchangeClient { AccountEquity = 12000m, CloseSettleReads = 2 }
            .WithPosition("AAA", Side.Buy, 1m, 100m);
        var target = new Dictionary<string, Side> { ["AAA"] = Side.Sell };
        var prices = new Dictionary<string, decimal> { ["AAA"] = 100m };

        var r = await CrossSectionalRebalancer.ReconcileAsync(
            ex, target, prices, Crypto("AAA"), Cfg(), Risk(), closeSettleDelay: TimeSpan.Zero);

        r.FailedClose.Should().Be(0);
        r.Opened.Should().Be(1);
        ex.PlaceOrderCalls.Should().ContainSingle(p => p.Symbol == "AAA" && p.Side == Side.Sell);
    }

    [Fact]
    public async Task Reconcile_OeffnetAlternierendShortLong()
    {
        // Insertion-Order waere L1,L2,L3,S1,S2,S3 (Longs zuerst, wie der MomentumBasketCalculator
        // den Korb baut) — bei knapper Margin scheiterten so systematisch die zuletzt platzierten
        // Shorts (netto-long im Abverkauf). Erwartet: alternierend S,L,S,L,S,L.
        var ex = new FakeExchangeClient { AccountEquity = 12000m };
        var target = new Dictionary<string, Side>
        {
            ["L1"] = Side.Buy, ["L2"] = Side.Buy, ["L3"] = Side.Buy,
            ["S1"] = Side.Sell, ["S2"] = Side.Sell, ["S3"] = Side.Sell,
        };
        var prices = target.Keys.ToDictionary(s => s, _ => 100m);

        await CrossSectionalRebalancer.ReconcileAsync(
            ex, target, prices, Crypto([.. target.Keys]), Cfg(longK: 3, shortK: 3, levCap: 1), Risk());

        ex.PlaceOrderCalls.Select(p => p.Side).Should().ContainInOrder(
            Side.Sell, Side.Buy, Side.Sell, Side.Buy, Side.Sell, Side.Buy);
    }

    [Fact]
    public async Task Reconcile_FehlCloses_LandenImResult()
    {
        // Ein werfender Close (TradFi-Wochenende, Rate-Limit) muss als Position im Result stehen,
        // damit der Aufrufer ihn in der Fehl-Close-Retry-Liste fuehren kann — sonst wird der Rest
        // beim naechsten Drift-Tick als Fremd-Position geschuetzt statt geschlossen.
        var ex = new FakeExchangeClient { CloseThrows = true }.WithPosition("AAA", Side.Buy, 1m, 100m);
        var target = new Dictionary<string, Side> { ["BBB"] = Side.Buy };
        var prices = new Dictionary<string, decimal> { ["AAA"] = 100m, ["BBB"] = 100m };

        var r = await CrossSectionalRebalancer.ReconcileAsync(
            ex, target, prices, Crypto("AAA", "BBB"), Cfg(), Risk(), closeSettleDelay: TimeSpan.Zero);

        r.FailedClose.Should().Be(1);
        r.FailedClosePositions.Should().ContainSingle(p => p.Symbol == "AAA" && p.Side == Side.Buy);
    }

    [Fact]
    public async Task Reconcile_BasketSlotsOverride_UebersteuertTargetCount()
    {
        // Drift-Refill-Fall: target enthaelt einen Fremd-Schutz-Eintrag (bereits offen → held, kein
        // Sizing) + 1 Refill-Slot. Der Divisor kommt aus basketSlots (Soll-Korbgroesse 6), nicht aus
        // target.Count (2): equity 12000 × 0.75 / 6 = 1500; lev1, price 100 → qty 15.
        var ex = new FakeExchangeClient { AccountEquity = 12000m }
            .WithPosition("FREMD", Side.Buy, 1m, 100m);
        var target = new Dictionary<string, Side> { ["FREMD"] = Side.Buy, ["NEU"] = Side.Sell };
        var prices = new Dictionary<string, decimal> { ["FREMD"] = 100m, ["NEU"] = 100m };

        await CrossSectionalRebalancer.ReconcileAsync(
            ex, target, prices, Crypto("FREMD", "NEU"), Cfg(longK: 3, shortK: 3, levCap: 1), Risk(),
            basketSlots: 6);

        ex.PlaceOrderCalls.Should().ContainSingle(p => p.Symbol == "NEU");
        ex.PlaceOrderCalls[0].Qty.Should().BeApproximately(15m, 0.0001m);
    }
}
