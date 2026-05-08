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

// v1.4.0 Phase 0.4 (Finding 0.4) — OrphanSignal-Cleanup beachtet jetzt Side.
//
// Vor v1.4.0: _pendingLimitOrders.Values.Any(v => v.Symbol == symbol) verhinderte das
// Entfernen jedes Orphan-Signals fuer das Symbol — egal ob die Pending-Order in dieselbe
// oder die Gegenrichtung lief. Im Hedge-Mode mit Long+Short parallel fuehrte das zu
// Zombie-Long-Signalen, die DailyRisk-Berechnungen + Recovery-TP verzerrten.
//
// Fix: Side wird aus dem posKey ({symbol}_{Buy|Sell}) extrahiert, Pending-Schutz greift
// nur bei Side-Match (IsLong → Buy, sonst Sell).
public class OrphanSignalSideFilterTests
{
    [Fact]
    public async Task LongOrphan_ShortPendingExists_LongStillRemoved()
    {
        // Setup: Long-Signal orphan (alt), Pending-Order auf BTC ist SHORT.
        // Vor v1.4.0 blieb das Long-Signal stehen, weil "irgendeine Pending fuer das Symbol".
        var fake = new FakeExchangeClient(); // keine Position
        var service = CreateService(fake);

        service._positionSignals["BTC-USDT_Buy"] = MakeSignal(Signal.Long, 50000m, 49000m, 52000m);
        service._signalCreatedAt["BTC-USDT_Buy"] = DateTime.UtcNow.AddSeconds(-120);

        // Pending in die GEGEN-Richtung (Short)
        service._pendingLimitOrders["BTC-USDT#shortSeq"] = (
            OrderId: "x1",
            PlacedAt: DateTime.UtcNow.AddMinutes(-2),
            InvalidationLevel: 51000m,
            IsLong: false,
            Symbol: "BTC-USDT",
            SequenceId: "shortSeq",
            TakeProfit: 48000m,
            TakeProfit2: null,
            NavPointA: 0m,
            IsGklSetup: false,
            GklTimeframe: null,
            RunnerHardCap: 0m,
            IsCounterTrendScalp: false,
            PositionScaleOverride: null);

        await service.PublicOnBeforePriceTickerIterationForTestAsync(Array.Empty<Position>());

        service._positionSignals.ContainsKey("BTC-USDT_Buy").Should().BeFalse(
            "Long-Orphan muss entfernt werden, Short-Pending schuetzt es nicht (Phase 0.4)");
    }

    [Fact]
    public async Task LongOrphan_LongPendingExists_LongPreserved()
    {
        // Backwards-Compat: Pending derselben Side muss das Signal weiterhin schuetzen.
        var fake = new FakeExchangeClient();
        var service = CreateService(fake);

        service._positionSignals["BTC-USDT_Buy"] = MakeSignal(Signal.Long, 50000m, 49000m, 52000m);
        service._signalCreatedAt["BTC-USDT_Buy"] = DateTime.UtcNow.AddSeconds(-120);

        // Pending in die GLEICHE Richtung (Long)
        service._pendingLimitOrders["BTC-USDT#longSeq"] = (
            OrderId: "x1",
            PlacedAt: DateTime.UtcNow.AddMinutes(-2),
            InvalidationLevel: 49000m,
            IsLong: true,
            Symbol: "BTC-USDT",
            SequenceId: "longSeq",
            TakeProfit: 52000m,
            TakeProfit2: null,
            NavPointA: 0m,
            IsGklSetup: false,
            GklTimeframe: null,
            RunnerHardCap: 0m,
            IsCounterTrendScalp: false,
            PositionScaleOverride: null);

        await service.PublicOnBeforePriceTickerIterationForTestAsync(Array.Empty<Position>());

        service._positionSignals.ContainsKey("BTC-USDT_Buy").Should().BeTrue(
            "Long-Pending derselben Side muss das Signal weiterhin schuetzen (Backwards-Compat)");
    }

    private static TestableLiveServiceForOrphan CreateService(IExchangeClient exchange)
    {
        return new TestableLiveServiceForOrphan(
            restClient: exchange,
            publicClient: Substitute.For<IPublicMarketDataClient>(),
            strategyManager: new StrategyManager(),
            riskSettings: new RiskSettings(),
            scannerSettings: new ScannerSettings(),
            eventBus: new BotEventBus(),
            botSettings: new BotSettings());
    }

    private static SignalResult MakeSignal(Signal signal, decimal entry, decimal sl, decimal tp) =>
        new(Signal: signal, Confidence: 0.8m, EntryPrice: entry, StopLoss: sl, TakeProfit: tp, Reason: "Test");
}

internal sealed class TestableLiveServiceForOrphan : LiveTradingService
{
    public TestableLiveServiceForOrphan(
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

    public async Task PublicOnBeforePriceTickerIterationForTestAsync(IReadOnlyList<Position> positions)
    {
        var method = typeof(LiveTradingService).GetMethod(
            "OnBeforePriceTickerIteration",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method.Should().NotBeNull("OnBeforePriceTickerIteration muss existieren");
        var task = (Task)method!.Invoke(this, new object?[] { positions })!;
        await task.ConfigureAwait(false);
    }
}
