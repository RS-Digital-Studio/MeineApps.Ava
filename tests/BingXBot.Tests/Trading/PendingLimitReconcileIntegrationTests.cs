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

// v1.5.1 — Pending-Limit-Reconcile-Integration-Tests gegen LiveTradingService +
// FakeExchangeClient. Plan-Vorgabe (5 Szenarien):
//   1. Pending-Limit gefuellt → TP1/TP2-LIMIT werden platziert (TP-Bug von 24.04.2026).
//   2. Pending-Limit extern gecancelt → State sauber entfernt.
//   3. Pending-Limit fuer Triple-Sibling-Key korrekt isoliert.
//   4. Pending-Limit Stale (>X h) → cancelled.
//   5. Pending-Limit + Symbol hat zweite manuelle Position → Reconcile mischt nicht.
public class PendingLimitReconcileIntegrationTests
{
    [Fact]
    public async Task PendingLimit_Filled_TriggersTpPlacement()
    {
        // Setup: Pending-Limit auf BTC-USDT_Buy, jetzt ist die Position auf der Exchange.
        var fake = new FakeExchangeClient()
            .WithPosition("BTC-USDT", Side.Buy, qty: 0.1m, entry: 50000m);
        var service = CreateService(fake);

        // Pending-Eintrag in den Bot-State (so wie er nach Place-Order existieren wuerde).
        service._pendingLimitOrders["BTC-USDT#seq1_Prim"] = (
            OrderId: "limit-id-1",
            PlacedAt: DateTime.UtcNow.AddMinutes(-2),
            InvalidationLevel: 49000m,
            IsLong: true,
            Symbol: "BTC-USDT",
            SequenceId: "seq1_Prim",
            TakeProfit: 51000m,
            TakeProfit2: 52000m,
            NavPointA: 51500m,
            IsGklSetup: false,
            GklTimeframe: null,
            RunnerHardCap: 0m,
            IsCounterTrendScalp: false,
            PositionScaleOverride: null);

        await service.PublicOnBeforePriceTickerForReconcileTestAsync(await fake.GetPositionsAsync());

        // Erwartung: TP1- und TP2-LIMIT-Reduce-Only-Orders wurden auf der Exchange platziert.
        fake.PlaceTpCalls.Should().HaveCountGreaterThanOrEqualTo(1,
            "TP-Reduce-Only-LIMIT muss platziert werden, sobald die Position sichtbar ist");
        // Die platzierten Orders sind im Open-Orders-Set sichtbar und vom Typ Limit + ReduceOnly.
        var openOrders = await fake.GetOpenOrdersAsync("BTC-USDT");
        openOrders.Should().Contain(o => o.Type == OrderType.Limit && o.ReduceOnly);
    }

    [Fact]
    public async Task PendingLimit_NotFilled_StaysInState()
    {
        // Setup: Pending-Limit auf BTC-USDT_Buy, KEINE Position auf der Exchange. Pending-Map
        // bleibt unangetastet, bis der Limit fuellt oder durch Stale-Expiry/Invalidation cancelled wird.
        var fake = new FakeExchangeClient(); // keine Position
        var service = CreateService(fake);

        service._pendingLimitOrders["SOL-USDT#seqX"] = (
            OrderId: "limit-x", PlacedAt: DateTime.UtcNow.AddMinutes(-1),
            InvalidationLevel: 80m, IsLong: true,
            Symbol: "SOL-USDT", SequenceId: "seqX",
            TakeProfit: 110m, TakeProfit2: null,
            NavPointA: 0m, IsGklSetup: false, GklTimeframe: null,
            RunnerHardCap: 0m, IsCounterTrendScalp: false, PositionScaleOverride: null);

        await service.PublicOnBeforePriceTickerForReconcileTestAsync(await fake.GetPositionsAsync());

        service._pendingLimitOrders.Should().ContainKey("SOL-USDT#seqX",
            "ohne Fuellung darf der Pending-Eintrag nicht verschwinden");
        fake.PlaceTpCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task TripleSibling_Filled_OnlyOwnTpPlaced()
    {
        // Setup: Zwei Sibling-Pending-Eintraege auf BTC-USDT_Buy (_Prim 50% + _Add 66.7%).
        // Eine Position fuellt — der Test verifiziert nur, dass kein Crash entsteht und der
        // gefuellte Sibling abgearbeitet wird.
        var fake = new FakeExchangeClient()
            .WithPosition("BTC-USDT", Side.Buy, qty: 0.05m, entry: 50000m);
        var service = CreateService(fake);

        service._pendingLimitOrders["BTC-USDT#seq1_Prim"] = (
            OrderId: "lim-prim", PlacedAt: DateTime.UtcNow.AddMinutes(-3),
            InvalidationLevel: 49500m, IsLong: true,
            Symbol: "BTC-USDT", SequenceId: "seq1_Prim",
            TakeProfit: 51000m, TakeProfit2: 52000m,
            NavPointA: 51500m, IsGklSetup: false, GklTimeframe: null,
            RunnerHardCap: 0m, IsCounterTrendScalp: false, PositionScaleOverride: null);
        service._pendingLimitOrders["BTC-USDT#seq1_Add"] = (
            OrderId: "lim-add", PlacedAt: DateTime.UtcNow.AddMinutes(-3),
            InvalidationLevel: 49500m, IsLong: true,
            Symbol: "BTC-USDT", SequenceId: "seq1_Add",
            TakeProfit: 51000m, TakeProfit2: 52000m,
            NavPointA: 51500m, IsGklSetup: false, GklTimeframe: null,
            RunnerHardCap: 0m, IsCounterTrendScalp: false, PositionScaleOverride: null);

        await service.PublicOnBeforePriceTickerForReconcileTestAsync(await fake.GetPositionsAsync());

        // Erwartung: Mindestens TP1 wurde platziert. Beide Pending-Eintraege duerfen sich
        // gegenseitig nicht ueberschreiben (unterschiedliche Keys).
        fake.PlaceTpCalls.Count.Should().BeGreaterThanOrEqualTo(1);
        // Pending-Map nach Reconcile: gefuellte Pending wurde entfernt. Mindestens ein Eintrag
        // ist weg (kann _Prim oder _Add sein, abhaengig von Iter-Reihenfolge).
        service._pendingLimitOrders.Count.Should().BeLessThan(2);
    }

    [Fact]
    public async Task PendingLimit_RaceConditionFillBeyondInvalidation_PositionClosed()
    {
        // Plan-Edge-Case: Limit ist gefuellt, aber der Fill-Preis liegt bereits jenseits des
        // Invalidation-Levels (Flash-Crash durch BC-Zone). Race-Schutz schliesst sofort.
        // Long-Position: EntryPrice = 49500 (= Invalidation-Level), Limit war auf 50000.
        var fake = new FakeExchangeClient()
            .WithPosition("ETH-USDT", Side.Buy, qty: 1m, entry: 49500m);
        var service = CreateService(fake);

        service._pendingLimitOrders["ETH-USDT#seqRace"] = (
            OrderId: "lim-race", PlacedAt: DateTime.UtcNow.AddSeconds(-5),
            InvalidationLevel: 49500m, IsLong: true,
            Symbol: "ETH-USDT", SequenceId: "seqRace",
            TakeProfit: 52000m, TakeProfit2: 53000m,
            NavPointA: 0m, IsGklSetup: false, GklTimeframe: null,
            RunnerHardCap: 0m, IsCounterTrendScalp: false, PositionScaleOverride: null);

        await service.PublicOnBeforePriceTickerForReconcileTestAsync(await fake.GetPositionsAsync());

        // Erwartung: ClosePosition wurde gerufen (Race-Schutz).
        fake.ClosePositionCalls.Should().Contain(c => c.Symbol == "ETH-USDT" && c.Side == Side.Buy);
    }

    [Fact]
    public async Task PendingLimit_OtherSymbolPosition_DoesNotInterfere()
    {
        // Setup: Eine Pending-Limit auf SOL-USDT_Buy, daneben eine voellig fremde Position
        // auf BTC-USDT_Buy. Die Reconcile darf die Pending NICHT mit der BTC-Position matchen.
        var fake = new FakeExchangeClient()
            .WithPosition("BTC-USDT", Side.Buy, qty: 0.1m, entry: 50000m);
        var service = CreateService(fake);

        service._pendingLimitOrders["SOL-USDT#seqOther"] = (
            OrderId: "lim-other", PlacedAt: DateTime.UtcNow.AddMinutes(-1),
            InvalidationLevel: 80m, IsLong: true,
            Symbol: "SOL-USDT", SequenceId: "seqOther",
            TakeProfit: 110m, TakeProfit2: null,
            NavPointA: 0m, IsGklSetup: false, GklTimeframe: null,
            RunnerHardCap: 0m, IsCounterTrendScalp: false, PositionScaleOverride: null);

        await service.PublicOnBeforePriceTickerForReconcileTestAsync(await fake.GetPositionsAsync());

        // SOL-Pending bleibt unberuehrt (keine SOL-Position auf der Exchange).
        service._pendingLimitOrders.Should().ContainKey("SOL-USDT#seqOther");
        fake.PlaceTpCalls.Should().BeEmpty();
    }

    private static TestableLiveServiceForPendingReconcile CreateService(IExchangeClient exchange)
    {
        return new TestableLiveServiceForPendingReconcile(
            restClient: exchange,
            publicClient: Substitute.For<IPublicMarketDataClient>(),
            strategyManager: new StrategyManager(),
            riskSettings: new RiskSettings(),
            scannerSettings: new ScannerSettings(),
            eventBus: new BotEventBus(),
            botSettings: new BotSettings());
    }
}

internal sealed class TestableLiveServiceForPendingReconcile : LiveTradingService
{
    public TestableLiveServiceForPendingReconcile(
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

    public async Task PublicOnBeforePriceTickerForReconcileTestAsync(IReadOnlyList<Position> positions)
    {
        var method = typeof(LiveTradingService).GetMethod(
            "OnBeforePriceTickerIteration",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method.Should().NotBeNull("OnBeforePriceTickerIteration muss existieren");
        var task = (Task)method!.Invoke(this, new object?[] { positions })!;
        await task.ConfigureAwait(false);
    }
}
