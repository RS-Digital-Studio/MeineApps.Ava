using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Trading;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BingXBot.Tests.Trading;

// v1.4.0 .1 (Finding 0.1) — Ghost-TP-Orders nach Position-Close
//
// Vorher cancelte der Cancel-Filter nur StopMarket / TakeProfitMarket / TakeProfitLimit.
// Bot-platzierte TP1/TP2 werden aber als plain LIMIT mit reduceOnly=true platziert
// (PlaceTpReduceOnlyLimitAsync) — sie blieben als Ghost-Orders im BingX-Orderbuch nach
// regulaerem Close. Konsequenz: Open-Order-Limit wird aufgefressen, Reconcile zeigt falsche
// Stats, im Worst-Case Cross-Match mit neuer Position auf demselben Symbol+Side.
//
// Fix verifiziert hier:
// - Test 1: Reduce-Only-Limits werden gecancelt (war NICHT der Fall vor v1.4.0).
// - Test 2: Entry-Limits (reduceOnly=false) bleiben unberuehrt — sonst wuerden Pending-Entries
//   anderer Sequenzen kaputt gehen.
// - Test 3: Native SL/TP-Orders werden weiterhin gecancelt (Regression-Schutz).
public class CancelNativeSlTpGhostOrderTests
{
    [Fact]
    public async Task CancelsReduceOnlyLimitOrders()
    {
        var fake = new FakeExchangeClient()
            .WithOpenOrder("BTC-USDT", Side.Sell, OrderType.Limit, qty: 0.1m, price: 70000m, reduceOnly: true);

        await fake.GetOpenOrdersAsync("BTC-USDT"); // Smoke-Check
        // Direkter Aufruf des Cancel-Filters via Service.
        var service = CreateService(fake);
        await service.PublicCancelNativeSlTpForTestAsync("BTC-USDT", Side.Buy);

        fake.CancelOrderCalls.Should().HaveCount(1);
        fake.CancelOrderCalls[0].Symbol.Should().Be("BTC-USDT");
    }

    [Fact]
    public async Task KeepsNonReduceOnlyLimitOrders()
    {
        // Entry-Limit auf BUY-Seite (nicht Reduce-Only) — darf NICHT gecancelt werden, sonst
        // wuerde ein Pending-Entry einer parallelen Sequenz unbeabsichtigt verschwinden.
        var fake = new FakeExchangeClient()
            .WithOpenOrder("BTC-USDT", Side.Buy, OrderType.Limit, qty: 0.05m, price: 65000m, reduceOnly: false);

        var service = CreateService(fake);
        await service.PublicCancelNativeSlTpForTestAsync("BTC-USDT", Side.Buy);

        fake.CancelOrderCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelsStopMarketAndTakeProfitTogether()
    {
        // Klassische Native-Cancellation + neue Reduce-Only-Cancellation in einem Aufruf.
        var fake = new FakeExchangeClient()
            .WithOpenOrder("ETH-USDT", Side.Sell, OrderType.StopMarket, qty: 1m, price: 2900m, reduceOnly: false)
            .WithOpenOrder("ETH-USDT", Side.Sell, OrderType.TakeProfitMarket, qty: 1m, price: 3200m, reduceOnly: false)
            .WithOpenOrder("ETH-USDT", Side.Sell, OrderType.Limit, qty: 0.5m, price: 3300m, reduceOnly: true);

        var service = CreateService(fake);
        await service.PublicCancelNativeSlTpForTestAsync("ETH-USDT", Side.Buy);

        fake.CancelOrderCalls.Should().HaveCount(3);
    }

    [Fact]
    public async Task RespectsHedgeModeSideFilter_KeepsOppositeSideReduceOnly()
    {
        // Hedge-Mode: Long+Short auf BTC parallel. Long-Close cancelt nur die SELL-Reduce-Onlys
        // (= TP-Orders der Long-Position). BUY-Reduce-Onlys gehoeren zur parallelen Short und
        // muessen ueberleben.
        var fake = new FakeExchangeClient()
            .WithOpenOrder("BTC-USDT", Side.Sell, OrderType.Limit, qty: 0.1m, price: 70000m, reduceOnly: true) // TP fuer Long
            .WithOpenOrder("BTC-USDT", Side.Buy,  OrderType.Limit, qty: 0.1m, price: 60000m, reduceOnly: true); // TP fuer Short

        var service = CreateService(fake);
        await service.PublicCancelNativeSlTpForTestAsync("BTC-USDT", Side.Buy); // Long-Close

        fake.CancelOrderCalls.Should().HaveCount(1);
        // Verifizieren: gecancelte Order war SELL (Schliess-Seite Long).
        // Cancel-Calls speichern nur OrderId+Symbol; daher pruefen ob die Short-TP noch offen ist.
        var remaining = await fake.GetOpenOrdersAsync("BTC-USDT");
        remaining.Should().ContainSingle(o => o.Side == Side.Buy && o.ReduceOnly);
    }

    // ────────────────── Helpers ──────────────────

    private static TestableLiveTradingService CreateService(IExchangeClient exchange)
    {
        return new TestableLiveTradingService(
            restClient: exchange,
            publicClient: Substitute.For<IPublicMarketDataClient>(),
            strategyManager: new StrategyManager(),
            riskSettings: new RiskSettings(),
            scannerSettings: new ScannerSettings(),
            eventBus: new BotEventBus(),
            botSettings: new BotSettings());
    }
}

// Test-Subclass damit der private Cancel-Helper aufrufbar ist (kein Reflection-Voodoo).
internal sealed class TestableLiveTradingService : LiveTradingService
{
    public TestableLiveTradingService(
        IExchangeClient restClient,
        IPublicMarketDataClient publicClient,
        StrategyManager strategyManager,
        RiskSettings riskSettings,
        ScannerSettings scannerSettings,
        BotEventBus eventBus,
        BotSettings botSettings)
        : base(restClient, publicClient, strategyManager, riskSettings, scannerSettings, eventBus, botSettings)
    {
    }

    /// <summary>
    /// Aufruf des privaten Cancel-Helpers ueber Reflection — die Methode ist private und nicht
    /// sinnvoll fuer Test-Doubles oeffentlich zu machen, ohne die Public-API aufzublaehen.
    /// </summary>
    public async Task PublicCancelNativeSlTpForTestAsync(string symbol, Side originalPositionSide)
    {
        var method = typeof(LiveTradingService).GetMethod(
            "CancelNativeSlTpOrdersAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method.Should().NotBeNull("CancelNativeSlTpOrdersAsync muss als private Method existieren");
        var task = (Task)method!.Invoke(this, new object?[] { symbol, originalPositionSide })!;
        await task.ConfigureAwait(false);
    }
}
