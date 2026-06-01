using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Trading.Reconciliation;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Trading;

// v1.5.1 (Finding "Missing-Stop-Detektion") — neue Drift-Kategorie MissingStopLoss.
//
// Ein offener Trade auf BingX MUSS eine STOP_MARKET-Reduce-Only-Order in der Schliess-Seite
// haben, sonst ist die Position bei plötzlicher Bewegung ungeschuetzt. Der Reconcile-Loop
// erkennt fehlende Stop-Orders und re-platziert sie aus dem Signal-SL (oder loggt Error
// wenn kein Signal-SL bekannt ist).
public class MissingStopLossDetectionTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private static readonly IReadOnlySet<(string, Side)> NoPending = new HashSet<(string, Side)>();
    private static readonly IReadOnlyDictionary<string, DateTime> NoSignalCreatedAt = new Dictionary<string, DateTime>();

    [Fact]
    public void MissingStopLoss_PositionOhneStopOrder_Erkannt()
    {
        var positions = new List<Position>
        {
            MakePos("BTC-USDT", Side.Buy, qty: 0.1m, entry: 50000m),
        };
        var openOrders = new List<Order>(); // KEIN STOP_MARKET → Drift erwartet
        var positionOpenedAt = new Dictionary<string, DateTime>
        {
            ["BTC-USDT_Buy"] = DateTime.UtcNow.AddMinutes(-5), // alt genug, Grace passe nicht
        };

        var actions = PositionDriftAnalyzer.Analyze(
            positions,
            new[] { "BTC-USDT_Buy" },
            NoPending,
            graceWindow: TimeSpan.FromSeconds(90),
            signalCreatedAt: NoSignalCreatedAt,
            openOrders: openOrders,
            positionOpenedAt: positionOpenedAt,
            missingStopGraceWindow: Grace);

        actions.Should().ContainSingle(a => a.Kind == PositionDriftAnalyzer.DriftKind.MissingStopLoss);
        actions[0].Symbol.Should().Be("BTC-USDT");
        actions[0].Side.Should().Be(Side.Buy);
    }

    [Fact]
    public void MissingStopLoss_StopOrderVorhanden_KeinDrift()
    {
        var positions = new List<Position>
        {
            MakePos("BTC-USDT", Side.Buy, qty: 0.1m, entry: 50000m),
        };
        // STOP_MARKET-Reduce-Only auf SELL-Seite (= Schliess-Seite einer Long-Position).
        var openOrders = new List<Order>
        {
            new Order(
                OrderId: "sl1", Symbol: "BTC-USDT", Side: Side.Sell, Type: OrderType.StopMarket,
                Price: 0m, Quantity: 0.1m, StopPrice: 49000m, CreateTime: DateTime.UtcNow,
                Status: OrderStatus.New, ReduceOnly: true),
        };
        var positionOpenedAt = new Dictionary<string, DateTime>
        {
            ["BTC-USDT_Buy"] = DateTime.UtcNow.AddMinutes(-5),
        };

        var actions = PositionDriftAnalyzer.Analyze(
            positions,
            new[] { "BTC-USDT_Buy" },
            NoPending,
            graceWindow: TimeSpan.FromSeconds(90),
            signalCreatedAt: NoSignalCreatedAt,
            openOrders: openOrders,
            positionOpenedAt: positionOpenedAt,
            missingStopGraceWindow: Grace);

        actions.Should().NotContain(a => a.Kind == PositionDriftAnalyzer.DriftKind.MissingStopLoss);
    }

    [Fact]
    public void MissingStopLoss_FrischePosition_GraceFiltertAus()
    {
        var positions = new List<Position>
        {
            MakePos("ETH-USDT", Side.Sell, qty: 1m, entry: 3000m),
        };
        var openOrders = new List<Order>(); // KEIN STOP — aber Position ist frisch
        var positionOpenedAt = new Dictionary<string, DateTime>
        {
            ["ETH-USDT_Sell"] = DateTime.UtcNow.AddSeconds(-10), // innerhalb 30 s Grace
        };

        var actions = PositionDriftAnalyzer.Analyze(
            positions,
            new[] { "ETH-USDT_Sell" },
            NoPending,
            graceWindow: TimeSpan.FromSeconds(90),
            signalCreatedAt: NoSignalCreatedAt,
            openOrders: openOrders,
            positionOpenedAt: positionOpenedAt,
            missingStopGraceWindow: Grace);

        actions.Should().NotContain(a => a.Kind == PositionDriftAnalyzer.DriftKind.MissingStopLoss);
    }

    [Fact]
    public void MissingStopLoss_NichtReduceOnly_GiltAlsVorhanden_HedgeMode()
    {
        // HEDGE-MODE-FIX: BingX liefert im Hedge-Mode reduceOnly=false fuer ALLE Orders. Ein
        // STOP_MARKET auf der Schliess-Seite (Sell fuer Long) IST der native SL — unabhaengig vom
        // reduceOnly-Flag. Frueher galt er faelschlich als fehlend → Dauer-Re-Place des lebenden SL
        // alle 60 s (schutzlose Cancel/Place-Fenster + hohe Cancel-Rate).
        var positions = new List<Position>
        {
            MakePos("SOL-USDT", Side.Buy, qty: 5m, entry: 100m),
        };
        var openOrders = new List<Order>
        {
            new Order(
                OrderId: "x1", Symbol: "SOL-USDT", Side: Side.Sell, Type: OrderType.StopMarket,
                Price: 0m, Quantity: 5m, StopPrice: 95m, CreateTime: DateTime.UtcNow,
                Status: OrderStatus.New, ReduceOnly: false),  // Hedge-Mode: BingX liefert immer false
        };
        var positionOpenedAt = new Dictionary<string, DateTime>
        {
            ["SOL-USDT_Buy"] = DateTime.UtcNow.AddMinutes(-2),
        };

        var actions = PositionDriftAnalyzer.Analyze(
            positions,
            new[] { "SOL-USDT_Buy" },
            NoPending,
            graceWindow: TimeSpan.FromSeconds(90),
            signalCreatedAt: NoSignalCreatedAt,
            openOrders: openOrders,
            positionOpenedAt: positionOpenedAt,
            missingStopGraceWindow: Grace);

        actions.Should().NotContain(a => a.Kind == PositionDriftAnalyzer.DriftKind.MissingStopLoss);
    }

    [Fact]
    public void MissingStopLoss_OpenOrdersNull_KeinCheck()
    {
        // Wenn der OpenOrders-Abruf bei BingX fehlschlaegt, soll der Reconcile-Loop NICHT
        // faelschlich Missing-SL melden — kein Lookup moeglich, also no-op.
        var positions = new List<Position>
        {
            MakePos("BTC-USDT", Side.Buy, qty: 0.1m, entry: 50000m),
        };

        var actions = PositionDriftAnalyzer.Analyze(
            positions,
            new[] { "BTC-USDT_Buy" },
            NoPending,
            graceWindow: TimeSpan.FromSeconds(90),
            signalCreatedAt: NoSignalCreatedAt,
            openOrders: null,
            positionOpenedAt: null,
            missingStopGraceWindow: Grace);

        actions.Should().NotContain(a => a.Kind == PositionDriftAnalyzer.DriftKind.MissingStopLoss);
    }

    [Fact]
    public void MissingStopLoss_HedgeMode_KorrektFiltert()
    {
        // Hedge-Mode: Long-Position auf BTC + parallel Short-Position auf BTC. Long hat
        // STOP-Order auf SELL-Seite, Short hat KEINE STOP-Order auf BUY-Seite.
        // Nur die Short-Position soll als MissingStopLoss erkannt werden.
        var positions = new List<Position>
        {
            MakePos("BTC-USDT", Side.Buy, qty: 0.1m, entry: 50000m),
            MakePos("BTC-USDT", Side.Sell, qty: 0.05m, entry: 51000m),
        };
        var openOrders = new List<Order>
        {
            new Order(
                OrderId: "sl-long", Symbol: "BTC-USDT", Side: Side.Sell, Type: OrderType.StopMarket,
                Price: 0m, Quantity: 0.1m, StopPrice: 49000m, CreateTime: DateTime.UtcNow,
                Status: OrderStatus.New, ReduceOnly: true),
            // KEIN STOP fuer Short
        };
        var positionOpenedAt = new Dictionary<string, DateTime>
        {
            ["BTC-USDT_Buy"] = DateTime.UtcNow.AddMinutes(-5),
            ["BTC-USDT_Sell"] = DateTime.UtcNow.AddMinutes(-5),
        };

        var actions = PositionDriftAnalyzer.Analyze(
            positions,
            new[] { "BTC-USDT_Buy", "BTC-USDT_Sell" },
            NoPending,
            graceWindow: TimeSpan.FromSeconds(90),
            signalCreatedAt: NoSignalCreatedAt,
            openOrders: openOrders,
            positionOpenedAt: positionOpenedAt,
            missingStopGraceWindow: Grace);

        actions.Should().ContainSingle(a => a.Kind == PositionDriftAnalyzer.DriftKind.MissingStopLoss);
        actions.Single(a => a.Kind == PositionDriftAnalyzer.DriftKind.MissingStopLoss).Side
            .Should().Be(Side.Sell);
    }

    [Fact]
    public void MissingStopLoss_UnmanagedPosition_KeineMissingSlAction()
    {
        // Position auf Exchange ohne Bot-Signal → UnmanagedPositionWarning, KEIN MissingSL.
        // Bot greift dort nicht ein.
        var positions = new List<Position>
        {
            MakePos("DOGE-USDT", Side.Buy, qty: 100m, entry: 0.08m),
        };
        var openOrders = new List<Order>(); // kein STOP

        var actions = PositionDriftAnalyzer.Analyze(
            positions,
            botSignalKeys: Array.Empty<string>(),
            NoPending,
            graceWindow: TimeSpan.FromSeconds(90),
            signalCreatedAt: NoSignalCreatedAt,
            openOrders: openOrders,
            positionOpenedAt: new Dictionary<string, DateTime>(),
            missingStopGraceWindow: Grace);

        actions.Should().Contain(a => a.Kind == PositionDriftAnalyzer.DriftKind.UnmanagedPositionWarning);
        actions.Should().NotContain(a => a.Kind == PositionDriftAnalyzer.DriftKind.MissingStopLoss);
    }

    private static Position MakePos(string sym, Side side, decimal qty, decimal entry) =>
        new(sym, side, entry, entry, qty, 0m, 10, MarginType.Isolated, DateTime.UtcNow);

    // === Phase 18 / B2 — Missing-Take-Profit-Detection ===

    [Fact]
    public void MissingTakeProfit_PositionMitErwartetemTpOhneTpOrder_Erkannt()
    {
        var positions = new List<Position>
        {
            MakePos("ETH-USDT", Side.Buy, qty: 1m, entry: 3000m),
        };
        // SL ist da, aber kein TP-Limit-Reduce-Only.
        var openOrders = new List<Order>
        {
            new Order("sl", "ETH-USDT", Side.Sell, OrderType.StopMarket, 0m, 1m, 2900m,
                DateTime.UtcNow, OrderStatus.New, ReduceOnly: true),
        };
        var positionOpenedAt = new Dictionary<string, DateTime>
        {
            ["ETH-USDT_Buy"] = DateTime.UtcNow.AddMinutes(-2), // alt genug
        };

        var actions = PositionDriftAnalyzer.Analyze(
            positions,
            new[] { "ETH-USDT_Buy" },
            NoPending,
            graceWindow: TimeSpan.FromSeconds(90),
            signalCreatedAt: NoSignalCreatedAt,
            openOrders: openOrders,
            positionOpenedAt: positionOpenedAt,
            missingStopGraceWindow: Grace,
            signalsExpectingTakeProfit: new HashSet<string> { "ETH-USDT_Buy" });

        actions.Should().ContainSingle(a => a.Kind == PositionDriftAnalyzer.DriftKind.MissingTakeProfit);
    }

    [Fact]
    public void MissingTakeProfit_TpOrderVorhanden_KeinDrift()
    {
        var positions = new List<Position>
        {
            MakePos("ETH-USDT", Side.Buy, qty: 1m, entry: 3000m),
        };
        var openOrders = new List<Order>
        {
            new Order("sl", "ETH-USDT", Side.Sell, OrderType.StopMarket, 0m, 1m, 2900m,
                DateTime.UtcNow, OrderStatus.New, ReduceOnly: true),
            new Order("tp1", "ETH-USDT", Side.Sell, OrderType.Limit, 3300m, 0.5m, null,
                DateTime.UtcNow, OrderStatus.New, ReduceOnly: true),
        };
        var positionOpenedAt = new Dictionary<string, DateTime>
        {
            ["ETH-USDT_Buy"] = DateTime.UtcNow.AddMinutes(-2),
        };

        var actions = PositionDriftAnalyzer.Analyze(
            positions,
            new[] { "ETH-USDT_Buy" },
            NoPending,
            graceWindow: TimeSpan.FromSeconds(90),
            signalCreatedAt: NoSignalCreatedAt,
            openOrders: openOrders,
            positionOpenedAt: positionOpenedAt,
            missingStopGraceWindow: Grace,
            signalsExpectingTakeProfit: new HashSet<string> { "ETH-USDT_Buy" });

        actions.Should().NotContain(a => a.Kind == PositionDriftAnalyzer.DriftKind.MissingTakeProfit);
    }

    [Fact]
    public void MissingTakeProfit_GraceWindowAktiv_KeinDrift()
    {
        var positions = new List<Position>
        {
            MakePos("ETH-USDT", Side.Buy, qty: 1m, entry: 3000m),
        };
        var openOrders = new List<Order>
        {
            new Order("sl", "ETH-USDT", Side.Sell, OrderType.StopMarket, 0m, 1m, 2900m,
                DateTime.UtcNow, OrderStatus.New, ReduceOnly: true),
        };
        var positionOpenedAt = new Dictionary<string, DateTime>
        {
            ["ETH-USDT_Buy"] = DateTime.UtcNow.AddSeconds(-5), // FRISCH offen
        };

        var actions = PositionDriftAnalyzer.Analyze(
            positions,
            new[] { "ETH-USDT_Buy" },
            NoPending,
            graceWindow: TimeSpan.FromSeconds(90),
            signalCreatedAt: NoSignalCreatedAt,
            openOrders: openOrders,
            positionOpenedAt: positionOpenedAt,
            missingStopGraceWindow: Grace,
            signalsExpectingTakeProfit: new HashSet<string> { "ETH-USDT_Buy" });

        actions.Should().NotContain(a => a.Kind == PositionDriftAnalyzer.DriftKind.MissingTakeProfit);
    }

    [Fact]
    public void MissingTakeProfit_SignalErwartetKeinenTp_KeinDrift()
    {
        var positions = new List<Position>
        {
            MakePos("ETH-USDT", Side.Buy, qty: 1m, entry: 3000m),
        };
        var openOrders = new List<Order>
        {
            new Order("sl", "ETH-USDT", Side.Sell, OrderType.StopMarket, 0m, 1m, 2900m,
                DateTime.UtcNow, OrderStatus.New, ReduceOnly: true),
        };
        var positionOpenedAt = new Dictionary<string, DateTime>
        {
            ["ETH-USDT_Buy"] = DateTime.UtcNow.AddMinutes(-2),
        };

        var actions = PositionDriftAnalyzer.Analyze(
            positions,
            new[] { "ETH-USDT_Buy" },
            NoPending,
            graceWindow: TimeSpan.FromSeconds(90),
            signalCreatedAt: NoSignalCreatedAt,
            openOrders: openOrders,
            positionOpenedAt: positionOpenedAt,
            missingStopGraceWindow: Grace,
            // ETH-USDT_Buy ist NICHT in signalsExpectingTakeProfit → kein Drift erwartet.
            signalsExpectingTakeProfit: new HashSet<string>());

        actions.Should().NotContain(a => a.Kind == PositionDriftAnalyzer.DriftKind.MissingTakeProfit);
    }
}
