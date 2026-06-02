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
// 2. Unmanaged-Position (Position ohne Signal) → adoptiert: Signal (SL+TP+BE) registriert,
//    Notfall-SL gesetzt wenn nativ keiner liegt (02.06.2026 — vorher nur Warning-Log)
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
            TakeProfit2: null,
            NavPointA: 0m,
            IsGklSetup: false,
            GklTimeframe: null,
            RunnerHardCap: 0m,
            IsCounterTrendScalp: false,
            PositionScaleOverride: null);

        await service.ReconcilePositionsAsync(CancellationToken.None);

        service._positionSignals.ContainsKey("SOL-USDT_Buy").Should().BeTrue();
    }

    [Fact]
    public async Task ReconcilePositionsAsync_UnmanagedPosition_WirdAdoptiert()
    {
        // Exchange hat BTC-Long-Position, Bot kennt sie nicht (z.B. nach Crash ohne State-Persist).
        // NEUES Verhalten (02.06.2026): Der Bot ADOPTIERT die Position statt sie nur zu loggen —
        // registriert ein vollstaendiges Signal (SL+TP+BE) und setzt einen Notfall-SL, da kein nativer
        // SL vorhanden ist. Eine ungeschuetzte Echtgeld-Position ist das groesste Risiko.
        var fake = new FakeExchangeClient().WithPosition("BTC-USDT", Side.Buy, qty: 0.1m, entry: 50000m);
        var service = CreateService(fake);

        service._positionSignals.Count.Should().Be(0);

        await service.ReconcilePositionsAsync(CancellationToken.None);

        // Adoptiert: Signal registriert, Position NICHT geschlossen.
        service._positionSignals.ContainsKey("BTC-USDT_Buy").Should().BeTrue();
        fake.ClosePositionCalls.Should().BeEmpty();

        // Notfall-SL via SetPositionSlTp gesetzt (Long → unter Entry), TP=null (TP laeuft ueber Limit-Pfad).
        fake.SetSlTpCalls.Should().Contain(c =>
            c.Symbol == "BTC-USDT" && c.Sl.HasValue && c.Sl.Value < 50000m && c.Tp == null);

        // Registriertes Signal: SL unter Entry, TP1 aus RRR (1.5R) ueber Entry, Break-Even aktiv.
        var sig = service._positionSignals["BTC-USDT_Buy"];
        sig.StopLoss.Should().NotBeNull();
        sig.StopLoss!.Value.Should().BeLessThan(50000m);
        sig.TakeProfit.Should().NotBeNull();
        sig.TakeProfit!.Value.Should().BeGreaterThan(50000m);
        sig.DisableSmartBreakeven.Should().BeTrue();
    }

    [Fact]
    public async Task ReconcilePositionsAsync_UnmanagedPositionMitNativemSchutz_AdoptiertOhneNeueOrders()
    {
        // Unmanaged Long-Position die bereits nativen SL (StopMarket) + zwei TP-Limits hat
        // (NASDAQ/CRCL-Szenario nach Crash). Adoption uebernimmt die ECHTEN SL/TP-Werte ins Signal
        // und setzt KEINE neuen Orders — kein Notfall-SL noetig, keine TP-Duplikate.
        var fake = new FakeExchangeClient()
            .WithPosition("BTC-USDT", Side.Buy, qty: 0.1m, entry: 50000m)
            .WithOpenOrderInstance(new Order(
                OrderId: "sl1", Symbol: "BTC-USDT", Side: Side.Sell, Type: OrderType.StopMarket,
                Price: 0m, Quantity: 0.1m, StopPrice: 49000m, CreateTime: DateTime.UtcNow,
                Status: OrderStatus.New, ReduceOnly: false))
            .WithOpenOrder("BTC-USDT", Side.Sell, OrderType.Limit, qty: 0.05m, price: 51000m)
            .WithOpenOrder("BTC-USDT", Side.Sell, OrderType.Limit, qty: 0.05m, price: 52000m);
        var service = CreateService(fake);

        await service.ReconcilePositionsAsync(CancellationToken.None);

        // Adoptiert mit echten Werten — KEIN neuer SL-Call (nativer SL existiert), kein Close.
        service._positionSignals.ContainsKey("BTC-USDT_Buy").Should().BeTrue();
        fake.SetSlTpCalls.Should().BeEmpty();
        fake.ClosePositionCalls.Should().BeEmpty();

        var sig = service._positionSignals["BTC-USDT_Buy"];
        sig.StopLoss.Should().Be(49000m);    // nativer SL uebernommen
        sig.TakeProfit.Should().Be(51000m);  // TP1 = naeher am Entry (Long → niedriger)
        sig.TakeProfit2.Should().Be(52000m); // TP2 = weiter
        sig.DisableSmartBreakeven.Should().BeTrue();
    }

    [Fact]
    public async Task ReconcilePositionsAsync_UnvollstaendigesRecoverySignal_WirdVervollstaendigt()
    {
        // Der RecoverOpenPositions-Start-Pfad registriert Recovery-Signale mit SL aber TakeProfit=null
        // und DisableSmartBreakeven=false (Live-Befund 02.06.: SP500/ETH hatten SL, aber kein TP/BE).
        // Die Adoption vervollstaendigt solche Signale jeden Durchgang: TP1/TP2 (aus RRR) + BE — ohne
        // einen neuen SL-Call (der SL existiert bereits).
        var fake = new FakeExchangeClient()
            .WithPosition("BTC-USDT", Side.Buy, qty: 0.1m, entry: 50000m)
            .WithOpenOrderInstance(new Order(
                OrderId: "sl1", Symbol: "BTC-USDT", Side: Side.Sell, Type: OrderType.StopMarket,
                Price: 0m, Quantity: 0.1m, StopPrice: 49000m, CreateTime: DateTime.UtcNow,
                Status: OrderStatus.New, ReduceOnly: false));
        var service = CreateService(fake);

        // Recovery-Signal: SL gesetzt, KEIN TP, BE aus.
        service._positionSignals["BTC-USDT_Buy"] = new SignalResult(
            Signal.Long, 0.5m, 50000m, 49000m, null, "Recovery", DisableSmartBreakeven: false);

        await service.ReconcilePositionsAsync(CancellationToken.None);

        var sig = service._positionSignals["BTC-USDT_Buy"];
        sig.StopLoss.Should().Be(49000m);                  // SL unveraendert (kein neuer SL-Call noetig)
        sig.TakeProfit.Should().Be(50000m + 1.5m * 1000m); // TP1 = 1.5R (SL-Distanz 1000) = 51500
        sig.TakeProfit2.Should().Be(50000m + 3.0m * 1000m);// TP2 = 3R = 53000
        sig.DisableSmartBreakeven.Should().BeTrue();       // BE aktiviert
        fake.SetSlTpCalls.Should().BeEmpty();              // kein SL-Re-Place noetig
    }

    [Fact]
    public async Task ReconcilePositionsAsync_WinzigePosition_TpOhneSplit_FullTp1()
    {
        // ETH-Short 0.01 (= Min-Qty), nativer SL, KEINE TP-Limits, managed mit TP1+TP2.
        // Der 50/50-Split (0.005) faellt unter die Min-Order-Qty 0.01 → KEIN Split: ein Full-TP bei
        // TP1, und TP2 wird aus dem Signal entfernt (verhindert Endlos-Re-Place; Live-Befund 02.06.).
        var fake = new FakeExchangeClient { MinOrderQty = 0.01m }
            .WithPosition("ETH-USDT", Side.Sell, qty: 0.01m, entry: 1925m)
            .WithOpenOrderInstance(new Order(
                OrderId: "sl1", Symbol: "ETH-USDT", Side: Side.Buy, Type: OrderType.StopMarket,
                Price: 0m, Quantity: 0.01m, StopPrice: 1995m, CreateTime: DateTime.UtcNow,
                Status: OrderStatus.New, ReduceOnly: false));
        var service = CreateService(fake);
        service._positionSignals["ETH-USDT_Sell"] = new SignalResult(
            Signal.Short, 0.5m, 1925m, 1995m, 1820m, "Test", TakeProfit2: 1716m, DisableSmartBreakeven: true);

        await service.ReconcilePositionsAsync(CancellationToken.None);

        // Genau EIN TP-Limit platziert, mit voller Qty 0.01 (kein 0.005-Split).
        fake.PlaceTpCalls.Should().ContainSingle();
        fake.PlaceTpCalls[0].Qty.Should().Be(0.01m);
        fake.PlaceTpCalls[0].Price.Should().Be(1820m);
        // TP2 aus dem Signal entfernt → kuenftige Durchgaenge jagen keinen ungueltigen TP2 nach.
        service._positionSignals["ETH-USDT_Sell"].TakeProfit2.Should().BeNull();
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
