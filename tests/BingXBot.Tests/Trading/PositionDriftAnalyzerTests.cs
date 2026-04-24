using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Trading.Reconciliation;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Trading;

// Tests fuer den Bot-State-vs-Exchange-Drift-Analyzer (P0-1, 24.04.2026).
// Pure Funktion ohne Seiteneffekte → direkt testbar ohne Exchange-Mock.
// Szenarien: Ein-zu-Eins-Match, Orphan-Signal, Unmanaged-Position, Pending-Ausnahme, Grace-Window.
public class PositionDriftAnalyzerTests
{
    // ────────────────── Baseline: alles konsistent ──────────────────

    [Fact]
    public void KeineDrift_Wenn_PositionUndSignalKonsistent()
    {
        var positions = new List<Position>
        {
            MakePosition("BTC-USDT", Side.Buy, 0.1m, 50000m)
        };
        var botKeys = new HashSet<string> { "BTC-USDT_Buy" };

        var actions = PositionDriftAnalyzer.Analyze(
            positions, botKeys, EmptyPending(), TimeSpan.FromSeconds(90), EmptySignalCreated());

        actions.Should().BeEmpty();
    }

    // ────────────────── OrphanSignalRemove ──────────────────

    [Fact]
    public void OrphanSignal_Wenn_SignalDaAberKeinePosition()
    {
        // Bot glaubt an BTC-Long, aber Exchange hat gar keine Position.
        var positions = new List<Position>(); // leer
        var botKeys = new HashSet<string> { "BTC-USDT_Buy" };

        var actions = PositionDriftAnalyzer.Analyze(
            positions, botKeys, EmptyPending(), TimeSpan.FromSeconds(90), EmptySignalCreated());

        actions.Should().ContainSingle();
        actions[0].Kind.Should().Be(PositionDriftAnalyzer.DriftKind.OrphanSignalRemove);
        actions[0].Symbol.Should().Be("BTC-USDT");
        actions[0].Side.Should().Be(Side.Buy);
    }

    [Fact]
    public void KeinOrphan_Wenn_SignalNochInPendingListe()
    {
        // Limit-Entry wartet auf Fill — "keine Position" ist erwartet, KEIN Drift.
        var positions = new List<Position>();
        var botKeys = new HashSet<string> { "ETH-USDT_Sell" };
        var pending = new HashSet<(string, Side)> { ("ETH-USDT", Side.Sell) };

        var actions = PositionDriftAnalyzer.Analyze(
            positions, botKeys, pending, TimeSpan.FromSeconds(90), EmptySignalCreated());

        actions.Should().BeEmpty();
    }

    [Fact]
    public void KeinOrphan_Wenn_SignalInnerhalbGraceWindow()
    {
        // Signal wurde vor 10 Sekunden angelegt, Grace-Window 90 Sekunden — noch zu jung fuer Orphan-Check.
        var positions = new List<Position>();
        var botKeys = new HashSet<string> { "SOL-USDT_Buy" };
        var created = new Dictionary<string, DateTime>
        {
            ["SOL-USDT_Buy"] = DateTime.UtcNow.AddSeconds(-10)
        };

        var actions = PositionDriftAnalyzer.Analyze(
            positions, botKeys, EmptyPending(), TimeSpan.FromSeconds(90), created);

        actions.Should().BeEmpty();
    }

    [Fact]
    public void OrphanKommt_Wenn_SignalAelterAlsGraceWindow()
    {
        // Signal 120 s alt, Grace 90 s → Orphan-Check greift.
        var positions = new List<Position>();
        var botKeys = new HashSet<string> { "SOL-USDT_Buy" };
        var created = new Dictionary<string, DateTime>
        {
            ["SOL-USDT_Buy"] = DateTime.UtcNow.AddSeconds(-120)
        };

        var actions = PositionDriftAnalyzer.Analyze(
            positions, botKeys, EmptyPending(), TimeSpan.FromSeconds(90), created);

        actions.Should().ContainSingle();
        actions[0].Kind.Should().Be(PositionDriftAnalyzer.DriftKind.OrphanSignalRemove);
    }

    // ────────────────── UnmanagedPositionWarning ──────────────────

    [Fact]
    public void UnmanagedPosition_Wenn_PositionAufExchangeAberKeinSignal()
    {
        // Exchange hat eine Position die der Bot nicht kennt (User-Trade oder anderer Bot).
        var positions = new List<Position>
        {
            MakePosition("DOGE-USDT", Side.Sell, 100m, 0.08m)
        };
        var botKeys = new HashSet<string>(); // leer

        var actions = PositionDriftAnalyzer.Analyze(
            positions, botKeys, EmptyPending(), TimeSpan.FromSeconds(90), EmptySignalCreated());

        actions.Should().ContainSingle();
        actions[0].Kind.Should().Be(PositionDriftAnalyzer.DriftKind.UnmanagedPositionWarning);
        actions[0].Symbol.Should().Be("DOGE-USDT");
        actions[0].Side.Should().Be(Side.Sell);
        actions[0].Reason.Should().Contain("Qty=100");
    }

    [Fact]
    public void PositionenMitQty0_WerdenIgnoriert()
    {
        // BingX liefert manchmal Position-Objekte mit Qty=0 (gerade geschlossen, noch im Snapshot).
        // Die duerfen keinen Unmanaged-Warning ausloesen.
        var positions = new List<Position>
        {
            MakePosition("BTC-USDT", Side.Buy, 0m, 50000m)
        };
        var botKeys = new HashSet<string>();

        var actions = PositionDriftAnalyzer.Analyze(
            positions, botKeys, EmptyPending(), TimeSpan.FromSeconds(90), EmptySignalCreated());

        actions.Should().BeEmpty();
    }

    // ────────────────── Kombinationen + Bindestrich-Symbole ──────────────────

    [Fact]
    public void MehrereBefunde_GleichzeitigMoeglich()
    {
        // 1x Orphan (Bot hat BTC-Buy, Exchange nicht), 1x Unmanaged (Exchange hat ETH-Sell, Bot nicht).
        var positions = new List<Position>
        {
            MakePosition("ETH-USDT", Side.Sell, 1m, 3000m)
        };
        var botKeys = new HashSet<string> { "BTC-USDT_Buy" };

        var actions = PositionDriftAnalyzer.Analyze(
            positions, botKeys, EmptyPending(), TimeSpan.FromSeconds(90), EmptySignalCreated());

        actions.Should().HaveCount(2);
        actions.Should().Contain(a => a.Kind == PositionDriftAnalyzer.DriftKind.OrphanSignalRemove && a.Symbol == "BTC-USDT");
        actions.Should().Contain(a => a.Kind == PositionDriftAnalyzer.DriftKind.UnmanagedPositionWarning && a.Symbol == "ETH-USDT");
    }

    [Fact]
    public void SymbolMitBindestrich_WirdKorrektGeparst()
    {
        // BingX-Symbole wie "1000PEPE-USDT" haben Bindestriche. Key-Format "1000PEPE-USDT_Buy"
        // muss am LETZTEN Underscore gespalten werden (nicht am ersten).
        var positions = new List<Position>();
        var botKeys = new HashSet<string> { "1000PEPE-USDT_Buy" };
        var created = new Dictionary<string, DateTime>(); // leer → gilt als alt genug

        var actions = PositionDriftAnalyzer.Analyze(
            positions, botKeys, EmptyPending(), TimeSpan.FromSeconds(90), created);

        actions.Should().ContainSingle();
        actions[0].Symbol.Should().Be("1000PEPE-USDT");
        actions[0].Side.Should().Be(Side.Buy);
    }

    // ────────────────── Helpers ──────────────────

    private static Position MakePosition(string symbol, Side side, decimal qty, decimal entry) =>
        new(
            Symbol: symbol,
            Side: side,
            EntryPrice: entry,
            MarkPrice: entry,
            Quantity: qty,
            UnrealizedPnl: 0m,
            Leverage: 10,
            MarginType: MarginType.Isolated,
            OpenTime: DateTime.UtcNow);

    private static HashSet<(string, Side)> EmptyPending() => new();
    private static Dictionary<string, DateTime> EmptySignalCreated() => new();
}
