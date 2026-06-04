using BingXBot.Backtest.Portfolio;
using BingXBot.Core.Models;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Backtest;

/// <summary>
/// Tests fuer <see cref="MergedTimeline"/> + <see cref="PortfolioSymbolState"/> — die zeitliche
/// Merge-Achse des Portfolio-Backtests. Kern: Dedup, Sortierung, kein Look-Ahead.
/// </summary>
public class MergedTimelineTests
{
    private static Candle C(DateTime close, decimal price = 100m)
        => new(close.AddHours(-4), price, price + 1m, price - 1m, price, 1000m, close);

    private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_DeduplicatesAndSorts()
    {
        // Zwei Symbole, ueberlappende + verschobene CloseTimes, absichtlich unsortiert geliefert.
        var a = new List<Candle> { C(T0.AddHours(8)), C(T0.AddHours(4)), C(T0.AddHours(12)) };
        var b = new List<Candle> { C(T0.AddHours(4)), C(T0.AddHours(16)), C(T0.AddHours(8)) };

        var timeline = MergedTimeline.Build([a, b]);

        // Distinct: 4 eindeutige Zeitpunkte (4,8,12,16), aufsteigend sortiert.
        timeline.Should().Equal(
            T0.AddHours(4), T0.AddHours(8), T0.AddHours(12), T0.AddHours(16));
    }

    [Fact]
    public void Build_Empty_ReturnsEmpty()
    {
        MergedTimeline.Build([]).Should().BeEmpty();
        MergedTimeline.Build([new List<Candle>()]).Should().BeEmpty();
    }

    [Fact]
    public void AdvanceTo_StopsAtLastCandleWithCloseTimeLessOrEqualT()
    {
        var nav = new List<Candle> { C(T0.AddHours(4)), C(T0.AddHours(8)), C(T0.AddHours(12)) };
        var state = new PortfolioSymbolState("X-USDT", nav, new NoopStrategy());

        state.NavIdx.Should().Be(-1, "vor jedem Advance ist noch keine Kerze sichtbar");

        // Genau zwischen Kerze 1 und 2: navIdx muss auf Kerze 0 stehen bleiben.
        state.AdvanceTo(T0.AddHours(6));
        state.NavIdx.Should().Be(0);
        state.CurrentCandle.CloseTime.Should().Be(T0.AddHours(4));

        // Auf Kerze 2 (CloseTime == t).
        state.AdvanceTo(T0.AddHours(8));
        state.NavIdx.Should().Be(1);

        // Weit nach hinten: bis zur letzten Kerze.
        state.AdvanceTo(T0.AddHours(100));
        state.NavIdx.Should().Be(2);
    }

    [Fact]
    public void AdvanceTo_NoLookAhead_FutureCandleNeverVisible()
    {
        var nav = new List<Candle> { C(T0.AddHours(4)), C(T0.AddHours(8)), C(T0.AddHours(12)) };
        var state = new PortfolioSymbolState("X-USDT", nav, new NoopStrategy());

        // Zeit GENAU vor der ersten CloseTime: keine Kerze sichtbar (strikt CloseTime <= t).
        state.AdvanceTo(T0.AddHours(4).AddMinutes(-1));
        state.NavIdx.Should().Be(-1);
        state.HasCandleAt(T0.AddHours(4)).Should().BeFalse("noch nicht avanciert");

        // Genau auf die erste CloseTime.
        state.AdvanceTo(T0.AddHours(4));
        state.HasCandleAt(T0.AddHours(4)).Should().BeTrue();
        // Zukunfts-Kerze (CloseTime 8 > t=4) ist NICHT sichtbar — kein Look-Ahead.
        state.CurrentCandle.CloseTime.Should().Be(T0.AddHours(4));
        state.ContextSlice().Should().HaveCount(1, "nur die bis t geschlossene Kerze ist im Slice");
    }

    [Fact]
    public void HasCandleAt_TrueOnlyOnExactCloseTime()
    {
        var nav = new List<Candle> { C(T0.AddHours(4)), C(T0.AddHours(8)) };
        var state = new PortfolioSymbolState("X-USDT", nav, new NoopStrategy());

        state.AdvanceTo(T0.AddHours(8));
        state.HasCandleAt(T0.AddHours(8)).Should().BeTrue();
        state.HasCandleAt(T0.AddHours(6)).Should().BeFalse("kein Kerzen-Abschluss bei t=6");
        state.HasCandleAt(T0.AddHours(4)).Should().BeFalse("aktuelle Kerze schliesst bei 8, nicht 4");
    }

    [Fact]
    public void ContextSlice_RespectsMaxLenAndStaysPrefix()
    {
        var nav = Enumerable.Range(0, 300).Select(i => C(T0.AddHours(4 * (i + 1)))).ToList();
        var state = new PortfolioSymbolState("X-USDT", nav, new NoopStrategy());
        state.AdvanceTo(T0.AddHours(4 * 300)); // letzte Kerze
        state.NavIdx.Should().Be(299);

        var slice = state.ContextSlice(200);
        slice.Should().HaveCount(200, "Slice auf maxLen begrenzt");
        // Letztes Element des Slice == aktuelle Kerze (Prefix bis navIdx).
        slice[^1].CloseTime.Should().Be(nav[299].CloseTime);
        slice[0].CloseTime.Should().Be(nav[100].CloseTime, "Slice ist die letzten 200 Kerzen");
    }
}
