using BingXBot.Backtest.Portfolio;
using BingXBot.Backtest.Simulation;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Backtest;

/// <summary>
/// GAP 2 — Min-Order/Min-Notional-Simulation im Portfolio-Backtest.
/// Spiegelt die Live-Reject-Semantik (BingXRestClient.PlaceOrderAsync) in der SimulatedExchange:
/// zu kleine Orders → Rejected. Mit symbolInfo=null bleibt die Single-Symbol-Engine unveraendert.
/// Zusaetzlich: TP1-50/50-Split faellt bei winziger Teilmenge auf Full-TP (SplitTpQuantity-Spiegel).
/// </summary>
public class SimulatedExchangeMinOrderTests
{
    /// <summary>Manuell befuelltes Fake ohne HTTP — fixe MinQty/MinNotional/Precision pro Symbol.</summary>
    private sealed class FakeSymbolInfoProvider(decimal minQty, decimal minNotional, int qtyPrecision = 3, int pricePrecision = 2)
        : ISymbolInfoProvider
    {
        public bool MeetsMinimumOrder(string symbol, decimal quantity, decimal price)
        {
            if (quantity < minQty) return false;
            if (price > 0 && quantity * price < minNotional) return false;
            return true;
        }

        public decimal TruncateQuantity(string symbol, decimal quantity)
        {
            var factor = (decimal)Math.Pow(10, qtyPrecision);
            return Math.Floor(quantity * factor) / factor;
        }

        public decimal RoundPrice(string symbol, decimal price)
            => Math.Round(price, pricePrecision, MidpointRounding.ToEven);

        public decimal RoundPriceConservative(string symbol, decimal price, Side positionSide)
        {
            var factor = (decimal)Math.Pow(10, pricePrecision);
            return positionSide == Side.Buy
                ? Math.Floor(price * factor) / factor
                : Math.Ceiling(price * factor) / factor;
        }
    }

    private static BacktestSettings Settings() => new()
    {
        InitialBalance = 1000m,
        UseDynamicSlippage = false,
        SlippagePercent = 0m,
        SpreadPercent = 0m,
        Tp1CloseRatio = 0.5m
    };

    private static async Task<Order> PlaceMarketBuyAsync(SimulatedExchange ex, string symbol, decimal price, decimal qty)
    {
        ex.SetCurrentPrice(symbol, price);
        await ex.SetLeverageAsync(symbol, 10, Side.Buy);
        return await ex.PlaceOrderAsync(new OrderRequest(symbol, Side.Buy, OrderType.Market, qty));
    }

    [Fact]
    public async Task PlaceOrder_BelowMinQuantity_IsRejected()
    {
        // MinQty 0.01, Preis 100 → Notional 0.5 USDT bei qty 0.005, vor allem aber qty < MinQty.
        var info = new FakeSymbolInfoProvider(minQty: 0.01m, minNotional: 0m, qtyPrecision: 3);
        using var ex = new SimulatedExchange(Settings(), info);

        var order = await PlaceMarketBuyAsync(ex, "ETH-USDT", price: 100m, qty: 0.005m);

        order.Status.Should().Be(OrderStatus.Rejected);
        (await ex.GetPositionsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task PlaceOrder_BelowMinNotional_IsRejected()
    {
        // qty 0.02 >= MinQty 0.01, aber qty*price = 0.02*100 = 2 USDT < MinNotional 5.
        var info = new FakeSymbolInfoProvider(minQty: 0.01m, minNotional: 5m, qtyPrecision: 3);
        using var ex = new SimulatedExchange(Settings(), info);

        var order = await PlaceMarketBuyAsync(ex, "ETH-USDT", price: 100m, qty: 0.02m);

        order.Status.Should().Be(OrderStatus.Rejected);
        (await ex.GetPositionsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task PlaceOrder_MeetsMinimum_IsFilled()
    {
        // qty 0.10 >= MinQty 0.01, Notional 10 >= MinNotional 5 → Fill.
        var info = new FakeSymbolInfoProvider(minQty: 0.01m, minNotional: 5m, qtyPrecision: 3);
        using var ex = new SimulatedExchange(Settings(), info);

        var order = await PlaceMarketBuyAsync(ex, "ETH-USDT", price: 100m, qty: 0.10m);

        order.Status.Should().Be(OrderStatus.Filled);
        (await ex.GetPositionsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task PlaceOrder_WithoutProvider_NoReject_BackwardCompat()
    {
        // symbolInfo == null → kein Min-Order-Check, winzige Order wird gefuellt (Single-Symbol-Engine).
        using var ex = new SimulatedExchange(Settings(), symbolInfo: null);

        var order = await PlaceMarketBuyAsync(ex, "ETH-USDT", price: 100m, qty: 0.0001m);

        order.Status.Should().Be(OrderStatus.Filled);
        order.Quantity.Should().Be(0.0001m, "ohne Provider bleibt die Quantity unveraendert (kein Truncate)");
        (await ex.GetPositionsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Tp1Split_TinyQuantity_FoldsToFullTp()
    {
        // Winzige Position: OriginalQuantity 0.02, MinQty 0.01. TP1-Teilmenge = 0.02*0.5 = 0.01,
        // Rest = 0.01. Beide GENAU an der Grenze → MeetsMinimumOrder true → KEIN Fold (Kontroll-Pfad).
        // Daher MinQty knapp ueber die Haelfte setzen: MinQty 0.015 → Teilmenge 0.01 < 0.015 → Fold.
        var info = new FakeSymbolInfoProvider(minQty: 0.015m, minNotional: 0m, qtyPrecision: 3);
        var settings = Settings();
        using var ex = new SimulatedExchange(settings, info);

        const string symbol = "ETH-USDT";
        const decimal entry = 100m;
        const decimal qty = 0.02m;
        var openOrder = await PlaceMarketBuyAsync(ex, symbol, entry, qty);
        openOrder.Status.Should().Be(OrderStatus.Filled);

        var positions = await ex.GetPositionsAsync();
        var pos = positions.Single();

        // ExitState + Signal aufsetzen: TP1 wird in dieser Candle getroffen (High >= TP).
        const decimal tp1 = 110m;
        const decimal tp2 = 120m;
        var key = $"{symbol}_{Side.Buy}";
        var signal = new SignalResult(Signal.Long, 1m, entry, StopLoss: 95m, TakeProfit: tp1,
            Reason: "test", TakeProfit2: tp2);
        var positionSignals = new Dictionary<string, SignalResult> { [key] = signal };
        var exitTracking = new Dictionary<string, BacktestExitState>
        {
            [key] = new BacktestExitState
            {
                EntryPrice = entry,
                OriginalQuantity = qty,
                EntryTime = DateTime.UtcNow,
                Tp2 = tp2
            }
        };

        // Candle deren High den TP1 trifft.
        var candle = new Candle(DateTime.UtcNow, entry, High: tp1 + 1m, Low: entry - 1m, Close: tp1 + 0.5m,
            Volume: 1000m, DateTime.UtcNow.AddHours(4));

        await BacktestExitProcessor.ProcessExitsAsync(
            ex, positions, positionSignals, exitTracking, settings,
            riskSettings: null, symbol, candle, info);

        // Full-TP: Position vollstaendig geschlossen, KEIN Rest fuer TP2, Tracking entfernt.
        (await ex.GetPositionsAsync()).Should().BeEmpty("Fold-to-Full-TP schliesst die ganze Position bei TP1");
        positionSignals.Should().NotContainKey(key);
        exitTracking.Should().NotContainKey(key);

        var trades = ex.GetCompletedTrades();
        trades.Should().ContainSingle("Full-TP erzeugt genau einen CompletedTrade (kein Split)");
        trades[0].Quantity.Should().Be(qty, "die gesamte Original-Menge wird in einem Schritt geschlossen");
    }

    [Fact]
    public async Task Tp1Split_NormalQuantity_SplitsAsUsual()
    {
        // Kontroll-Test: ausreichend grosse Position → normaler 50/50-Split (TP1 partial, Rest auf TP2).
        var info = new FakeSymbolInfoProvider(minQty: 0.01m, minNotional: 0m, qtyPrecision: 3);
        var settings = Settings();
        using var ex = new SimulatedExchange(settings, info);

        const string symbol = "ETH-USDT";
        const decimal entry = 100m;
        const decimal qty = 1.0m;
        await PlaceMarketBuyAsync(ex, symbol, entry, qty);
        var positions = await ex.GetPositionsAsync();

        const decimal tp1 = 110m;
        const decimal tp2 = 120m;
        var key = $"{symbol}_{Side.Buy}";
        var signal = new SignalResult(Signal.Long, 1m, entry, StopLoss: 95m, TakeProfit: tp1,
            Reason: "test", TakeProfit2: tp2);
        var positionSignals = new Dictionary<string, SignalResult> { [key] = signal };
        var exitTracking = new Dictionary<string, BacktestExitState>
        {
            [key] = new BacktestExitState
            {
                EntryPrice = entry, OriginalQuantity = qty, EntryTime = DateTime.UtcNow, Tp2 = tp2
            }
        };
        var candle = new Candle(DateTime.UtcNow, entry, tp1 + 1m, entry - 1m, tp1 + 0.5m, 1000m, DateTime.UtcNow.AddHours(4));

        await BacktestExitProcessor.ProcessExitsAsync(
            ex, positions, positionSignals, exitTracking, settings,
            riskSettings: null, symbol, candle, info);

        // Split: Rest-Position (0.5) bleibt offen, Ziel auf TP2 verschoben.
        var remaining = await ex.GetPositionsAsync();
        remaining.Should().ContainSingle();
        remaining[0].Quantity.Should().Be(0.5m);
        positionSignals[key].TakeProfit.Should().Be(tp2);
        exitTracking[key].PartialClosed.Should().BeTrue();
    }
}
