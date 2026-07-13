using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Strategies;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Verhaltens-Tests für die Level-Strategie. Verifiziert die Level-Cluster-Erkennung (2 Pivots →
/// ein Level), den Bounce-Entry (Test + Ablehnung am Level) mit SL hinter dem LEVEL, den
/// Retest-Entry (frischer Breakout + Role-Reversal-Test) und dass ein gebrochenes Level weder
/// Long noch Bounce-Short liefert.
/// </summary>
public class LevelStrategyTests
{
    private static MarketContext Ctx(IReadOnlyList<Candle> candles, IReadOnlyList<Position>? open = null)
    {
        var last = candles[^1];
        var ticker = new Ticker("TEST-USDT", last.Close, last.Close, last.Close, 1000m, 0m, last.CloseTime);
        return new MarketContext("TEST-USDT", candles, ticker, open ?? [], new AccountInfo(1000m, 1000m, 0m, 0m),
            NavigatorTimeframe: TimeFrame.H4);
    }

    /// <summary>Serie mit fortlaufender H4-Zeit: Padding-Seitwärtsphase, dann explizite Kerzen.</summary>
    private sealed class Series
    {
        private readonly List<Candle> _list = [];
        private readonly DateTime _t0 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public Series Add(decimal o, decimal h, decimal l, decimal c)
        {
            int i = _list.Count;
            _list.Add(new Candle(_t0.AddHours(4 * i), o, h, l, c, 1200m, _t0.AddHours(4 * (i + 1))));
            return this;
        }

        /// <summary>Seitwärts-Padding ohne Fractal-Pivots (konstante Highs/Lows sind nie strikt extremer).</summary>
        public Series Pad(decimal around, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var c = around + (i % 2 == 0 ? 0.2m : -0.2m);
                Add(around, around + 0.6m, around - 0.6m, c);
            }
            return this;
        }

        public List<Candle> Build() => _list;
    }

    /// <summary>
    /// Doppel-Boden bei ~100 (zwei bestätigte Pivot-Lows, Cluster → EIN Level), Erholung,
    /// dann Rückkehr: Signal-Kerze dippt auf das Level und schließt in der oberen Hälfte.
    /// </summary>
    private static List<Candle> DoubleBottomBounceSetup() => new Series()
        .Pad(103m, 50)
        // Dip 1 → Pivot-Low 100.0 (3 Kerzen Erholung bestätigen den Pivot).
        .Add(103.0m, 103.2m, 101.5m, 101.7m)
        .Add(101.7m, 101.9m, 100.0m, 100.4m)
        .Add(100.4m, 101.6m, 100.5m, 101.4m)
        .Add(101.4m, 102.4m, 101.2m, 102.2m)
        .Add(102.2m, 102.8m, 101.9m, 102.6m)
        // Dip 2 → Pivot-Low 100.1 (clustert mit 100.0 → Level ~100.05, 2 Touches).
        .Add(102.6m, 102.8m, 101.3m, 101.5m)
        .Add(101.5m, 101.7m, 100.1m, 100.5m)
        .Add(100.5m, 101.8m, 100.4m, 101.6m)
        .Add(101.6m, 102.6m, 101.4m, 102.4m)
        .Add(102.4m, 103.0m, 102.0m, 102.8m)
        // Rückkehr zum Level.
        .Add(102.8m, 102.9m, 101.6m, 101.8m)
        .Add(101.8m, 102.0m, 100.8m, 101.0m)
        // Signal-Kerze: Test (Low 100.1 am Level) + Ablehnung (Close in der oberen Hälfte).
        .Add(101.0m, 101.6m, 100.1m, 101.5m)
        .Build();

    /// <summary>Doppel-Top bei ~106 (zwei bestätigte Pivot-Highs), dann Rückkehr an das Level.</summary>
    private static Series DoubleTopBase() => new Series()
        .Pad(103m, 50)
        // Anstieg 1 → Pivot-High 106.0.
        .Add(103.0m, 104.5m, 102.8m, 104.3m)
        .Add(104.3m, 106.0m, 104.1m, 105.6m)
        .Add(105.6m, 105.7m, 104.2m, 104.4m)
        .Add(104.4m, 104.5m, 103.4m, 103.6m)
        .Add(103.6m, 103.8m, 103.0m, 103.2m)
        // Anstieg 2 → Pivot-High 105.9 (clustert mit 106.0 → Level ~105.95, 2 Touches).
        .Add(103.2m, 104.7m, 103.1m, 104.6m)
        .Add(104.6m, 105.9m, 104.4m, 105.5m)
        .Add(105.5m, 105.6m, 104.3m, 104.5m)
        .Add(104.5m, 104.6m, 103.6m, 103.8m)
        .Add(103.8m, 104.0m, 103.0m, 103.2m);

    [Fact]
    public void Evaluate_Bounce_SupportHolds_ShouldEmitLongWithSlBehindLevel()
    {
        var strategy = new LevelStrategy();
        var signal = strategy.Evaluate(Ctx(DoubleBottomBounceSetup()));

        signal.Signal.Should().Be(Signal.Long, signal.Reason);
        signal.EntryPrice.Should().Be(101.5m, "Market-Entry zum Close (keine Limit-Entries — SK-Falle)");
        // SL liegt hinter dem LEVEL (~100.05), nicht nur hinter dem Entry.
        signal.StopLoss.Should().BeLessThan(100.0m);
        signal.TakeProfit.Should().BeGreaterThan(signal.EntryPrice!.Value);
        signal.TakeProfit2.Should().BeGreaterThan(signal.TakeProfit!.Value);
        signal.EntryAtr.Should().NotBeNull();
        signal.DisableSmartBreakeven.Should().BeTrue("BE-Block muss aktiv sein (invertiert benannt)");
    }

    [Fact]
    public void Evaluate_Bounce_ResistanceHolds_ShouldEmitShortWithSlBehindLevel()
    {
        var candles = DoubleTopBase()
            // Rückkehr nach oben an das Level.
            .Add(103.2m, 104.1m, 103.1m, 104.0m)
            .Add(104.0m, 104.9m, 103.9m, 104.8m)
            // Signal-Kerze: Test (High 106.1 am Level ~105.95) + Ablehnung (Close in der unteren Hälfte).
            .Add(104.8m, 106.1m, 104.6m, 105.0m)
            .Build();

        var signal = new LevelStrategy().Evaluate(Ctx(candles));

        signal.Signal.Should().Be(Signal.Short, signal.Reason);
        signal.EntryPrice.Should().Be(105.0m);
        signal.StopLoss.Should().BeGreaterThan(106.0m, "SL hinter dem Level (~105.95) + ATR-Puffer");
        signal.TakeProfit.Should().BeLessThan(signal.EntryPrice!.Value);
        signal.TakeProfit2.Should().BeLessThan(signal.TakeProfit!.Value);
    }

    [Fact]
    public void Evaluate_Retest_AfterFreshBreakout_ShouldEmitLong()
    {
        var candles = DoubleTopBase()
            // Anstieg + frischer Breakout-Close ÜBER das Level (Vorkerze noch darunter).
            .Add(103.8m, 105.3m, 103.7m, 105.2m)
            .Add(105.2m, 106.9m, 105.1m, 106.8m)
            .Add(106.8m, 107.4m, 106.6m, 107.2m)
            // Signal-Kerze: Pullback testet das gebrochene Level von oben und hält (Role-Reversal).
            .Add(106.9m, 107.0m, 105.9m, 106.5m)
            .Build();

        var signal = new LevelStrategy(mode: LevelEntryMode.Retest).Evaluate(Ctx(candles));

        signal.Signal.Should().Be(Signal.Long, signal.Reason);
        signal.EntryPrice.Should().Be(106.5m);
        signal.StopLoss.Should().BeLessThan(105.9m, "SL hinter dem zurückeroberten Level (~105.95)");
        signal.TakeProfit.Should().BeGreaterThan(signal.EntryPrice!.Value);
    }

    [Fact]
    public void Evaluate_Retest_WithoutFreshBreakout_ShouldNotEmit()
    {
        // Identisches Bounce-Setup, aber Retest-Modus: das Level wurde nie gebrochen → kein Signal.
        var signal = new LevelStrategy(mode: LevelEntryMode.Retest).Evaluate(Ctx(DoubleBottomBounceSetup()));
        signal.Signal.Should().Be(Signal.None);
    }

    [Fact]
    public void Evaluate_LevelBroken_ShouldNotEmit()
    {
        var candles = new Series()
            .Pad(103m, 50)
            .Add(103.0m, 103.2m, 101.5m, 101.7m)
            .Add(101.7m, 101.9m, 100.0m, 100.4m)
            .Add(100.4m, 101.6m, 100.5m, 101.4m)
            .Add(101.4m, 102.4m, 101.2m, 102.2m)
            .Add(102.2m, 102.8m, 101.9m, 102.6m)
            .Add(102.6m, 102.8m, 101.3m, 101.5m)
            .Add(101.5m, 101.7m, 100.1m, 100.5m)
            .Add(100.5m, 101.8m, 100.4m, 101.6m)
            .Add(101.6m, 102.6m, 101.4m, 102.4m)
            .Add(102.4m, 103.0m, 102.0m, 102.8m)
            .Add(102.8m, 102.9m, 101.6m, 101.8m)
            .Add(101.8m, 102.0m, 100.8m, 101.0m)
            // Bruch-Kerze: schließt UNTER dem Level — weder Long (Level hält nicht) noch
            // Bounce-Short (der Preis kam von oben, das Level war nie Resistance).
            .Add(101.0m, 101.2m, 99.5m, 99.8m)
            .Build();

        var signal = new LevelStrategy().Evaluate(Ctx(candles));
        signal.Signal.Should().Be(Signal.None);
    }

    [Fact]
    public void Evaluate_OpenPositionOnSymbol_ShouldNotEmit()
    {
        var candles = DoubleBottomBounceSetup();
        var last = candles[^1];
        var openPos = new Position("TEST-USDT", Side.Buy, last.Close, last.Close, 1m, 0m, 5m, MarginType.Isolated, last.CloseTime);
        new LevelStrategy().Evaluate(Ctx(candles, [openPos])).Signal.Should().Be(Signal.None);
    }

    [Fact]
    public void Evaluate_InsufficientData_ShouldNotEmit()
    {
        var candles = new Series().Pad(103m, 20).Build();
        new LevelStrategy().Evaluate(Ctx(candles)).Signal.Should().Be(Signal.None);
    }
}
