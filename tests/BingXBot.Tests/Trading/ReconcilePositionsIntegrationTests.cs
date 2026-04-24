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

// Integration-Tests fuer LiveTradingService.ReconcilePositionsAsync().
// Verifiziert die End-to-End-Interaktion zwischen FakeExchangeClient, PositionDriftAnalyzer,
// und der State-Manipulation (RemoveSignalByKey) im echten Code-Pfad — nicht nur den Analyzer.
//
// Reconcile-Verhalten:
// 1. Orphan-Signal (Bot-Signal ohne Position) → nach Grace-Window entfernt
// 2. Unmanaged-Position (Position ohne Signal) → nur Warning-Log, kein State-Change
// 3. Pending-Entry (Limit-Order noch nicht gefuellt) → Grace-Ausnahme, kein Entfernen
// 4. Alles konsistent → kein Log, kein State-Change
public class ReconcilePositionsIntegrationTests
{
    [Fact]
    public async Task ReconcilePositionsAsync_OrphanSignal_EntferntSignal()
    {
        // Setup: Bot glaubt BTC-Long ist offen, Exchange hat keine Position
        var fake = new FakeExchangeClient(); // leer
        var service = CreateService(fake);

        var signal = MakeSignal(Signal.Long, entry: 50000m, sl: 49000m, tp: 52000m);
        service._positionSignals["BTC-USDT_Buy"] = signal;
        // Signal 120 s alt — ueber Grace-Window (90 s)
        service._signalCreatedAt["BTC-USDT_Buy"] = DateTime.UtcNow.AddSeconds(-120);

        // Act
        await service.ReconcilePositionsAsync(CancellationToken.None);

        // Assert: Signal ist weg
        service._positionSignals.ContainsKey("BTC-USDT_Buy").Should().BeFalse();
    }

    [Fact]
    public async Task ReconcilePositionsAsync_GraceWindow_BehaeltFrischesSignal()
    {
        var fake = new FakeExchangeClient();
        var service = CreateService(fake);

        service._positionSignals["ETH-USDT_Sell"] = MakeSignal(Signal.Short, 3000m, 3100m, 2800m);
        // Signal erst 10 s alt — innerhalb Grace-Window (90 s) → soll behalten werden.
        service._signalCreatedAt["ETH-USDT_Sell"] = DateTime.UtcNow.AddSeconds(-10);

        await service.ReconcilePositionsAsync(CancellationToken.None);

        service._positionSignals.ContainsKey("ETH-USDT_Sell").Should().BeTrue();
    }

    [Fact]
    public async Task ReconcilePositionsAsync_PendingEntry_BehaeltSignal()
    {
        // Pending-Limit-Entry ist noch nicht gefuellt — Exchange hat deshalb keine Position.
        // Das ist erwartet, kein Drift.
        var fake = new FakeExchangeClient();
        var service = CreateService(fake);

        service._positionSignals["SOL-USDT_Buy"] = MakeSignal(Signal.Long, 100m, 95m, 110m);
        service._signalCreatedAt["SOL-USDT_Buy"] = DateTime.UtcNow.AddSeconds(-120); // alt genug fuer Orphan

        // Aber: Pending-Entry vorhanden → Ausnahme greift.
        service._pendingLimitOrders["SOL-USDT#seq1"] = (
            OrderId: "x1",
            PlacedAt: DateTime.UtcNow.AddMinutes(-5),
            InvalidationLevel: 95m,
            IsLong: true,
            Symbol: "SOL-USDT",
            SequenceId: "seq1",
            TakeProfit: 110m,
            TakeProfit2: null);

        await service.ReconcilePositionsAsync(CancellationToken.None);

        service._positionSignals.ContainsKey("SOL-USDT_Buy").Should().BeTrue();
    }

    [Fact]
    public async Task ReconcilePositionsAsync_UnmanagedPosition_AendetStateNichtAberLoggt()
    {
        // Exchange hat BTC-Long-Position, Bot kennt sie nicht → nur Warning, keine Modifikation.
        var fake = new FakeExchangeClient().WithPosition("BTC-USDT", Side.Buy, qty: 0.1m, entry: 50000m);
        var service = CreateService(fake);

        // Kein Signal im Bot → sollte Warning geben aber nichts aendern.
        service._positionSignals.Count.Should().Be(0);

        await service.ReconcilePositionsAsync(CancellationToken.None);

        // Keine State-Aenderung (keine Signal-Registrierung, kein ClosePosition-Call)
        service._positionSignals.Count.Should().Be(0);
        fake.ClosePositionCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task ReconcilePositionsAsync_AllesKonsistent_KeineAenderung()
    {
        // Bot hat BTC-Long-Signal UND Exchange hat BTC-Long-Position.
        var fake = new FakeExchangeClient().WithPosition("BTC-USDT", Side.Buy, 0.1m, 50000m);
        var service = CreateService(fake);

        service._positionSignals["BTC-USDT_Buy"] = MakeSignal(Signal.Long, 50000m, 49000m, 52000m);
        service._signalCreatedAt["BTC-USDT_Buy"] = DateTime.UtcNow.AddSeconds(-120);

        await service.ReconcilePositionsAsync(CancellationToken.None);

        service._positionSignals.ContainsKey("BTC-USDT_Buy").Should().BeTrue();
        fake.ClosePositionCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task ReconcilePositionsAsync_MehrereDrift_AlleKorrektBehandelt()
    {
        // Exchange: DOGE-Short (unmanaged)
        // Bot: BTC-Long (orphan, alt genug), ETH-Sell (fresh, behalten)
        var fake = new FakeExchangeClient().WithPosition("DOGE-USDT", Side.Sell, 100m, 0.08m);
        var service = CreateService(fake);

        service._positionSignals["BTC-USDT_Buy"] = MakeSignal(Signal.Long, 50000m, 49000m, 52000m);
        service._signalCreatedAt["BTC-USDT_Buy"] = DateTime.UtcNow.AddSeconds(-120);

        service._positionSignals["ETH-USDT_Sell"] = MakeSignal(Signal.Short, 3000m, 3100m, 2800m);
        service._signalCreatedAt["ETH-USDT_Sell"] = DateTime.UtcNow.AddSeconds(-10);

        await service.ReconcilePositionsAsync(CancellationToken.None);

        // BTC orphan → weg
        service._positionSignals.ContainsKey("BTC-USDT_Buy").Should().BeFalse();
        // ETH fresh → bleibt
        service._positionSignals.ContainsKey("ETH-USDT_Sell").Should().BeTrue();
        // DOGE unmanaged → keine Aktion, kein Close
        fake.ClosePositionCalls.Should().BeEmpty();
    }

    // ────────────────── Helpers ──────────────────

    private static LiveTradingService CreateService(IExchangeClient exchange)
    {
        return new LiveTradingService(
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
