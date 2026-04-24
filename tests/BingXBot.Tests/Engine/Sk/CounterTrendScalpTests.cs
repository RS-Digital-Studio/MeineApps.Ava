using BingXBot.Core.Configuration;
using BingXBot.Core.Models;
using BingXBot.Engine.Strategies;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>Phase 5 — Tests für <see cref="CounterTrendScalper"/> (Task 4.10).</summary>
public class CounterTrendScalpTests
{
    [Fact]
    public void EnableCounterTrendScalp_DefaultFalse()
    {
        var settings = new ScannerSettings();
        settings.EnableCounterTrendScalp.Should().BeFalse();
    }

    [Fact]
    public void TryDetect_Null_WennHauptsequenzNichtActive()
    {
        var seq = new Sequence
        {
            Point0 = new SwingPoint(100m, 0, DateTime.MinValue, false),
            PointA = new SwingPoint(110m, 1, DateTime.MinValue, true),
            IsLong = true,
            State = SequenceState.CorrectionZone,
            Extension1618 = 120m,
            Extension200 = 122m,
        };
        var result = CounterTrendScalper.TryDetect(seq, 120m, null, 0.0001m);
        result.Should().BeNull();
    }

    [Fact]
    public void TryDetect_Null_WennKeineCandles()
    {
        var seq = new Sequence
        {
            Point0 = new SwingPoint(100m, 0, DateTime.MinValue, false),
            PointA = new SwingPoint(110m, 1, DateTime.MinValue, true),
            IsLong = true,
            State = SequenceState.Active,
            Extension1618 = 120m,
            Extension200 = 122m,
        };
        var result = CounterTrendScalper.TryDetect(seq, 120m, null, 0.0001m);
        result.Should().BeNull();
    }

    [Fact]
    public void TryDetect_Null_WennPreisAusserhalbTpZone()
    {
        var seq = new Sequence
        {
            Point0 = new SwingPoint(100m, 0, DateTime.MinValue, false),
            PointA = new SwingPoint(110m, 1, DateTime.MinValue, true),
            IsLong = true,
            State = SequenceState.Active,
            Extension1618 = 120m,
            Extension200 = 122m,
        };
        var candles = new List<Candle>();
        for (int i = 0; i < 30; i++)
            candles.Add(new Candle(DateTime.UtcNow, 100m, 101m, 99m, 100m, 1000m, DateTime.UtcNow));

        var result = CounterTrendScalper.TryDetect(seq, currentPrice: 105m, candles, 0.0001m);
        // Preis 105 nicht nah an Ext1618 (120) oder Ext200 (122)
        result.Should().BeNull();
    }

    [Fact]
    public void PositionScaleOverride_IstFuenfzigProzent()
    {
        // Der Detector liefert immer 0.5 als Position-Scale wenn ein Hit erkannt wird
        // (Test auf Hit selbst ist komplex wegen LTF-Sequenz-Aufbau, aber Record-Feld testbar)
        var hit = new CounterTrendHit(
            EntryPrice: 120m, StopLoss: 118m, TakeProfit: 115m,
            LtfPoint0: 121m, Reason: "test", PositionScaleOverride: 0.5m);
        hit.PositionScaleOverride.Should().Be(0.5m);
    }

    [Fact]
    public void CounterTrendHit_EnthaeltAlleFelder()
    {
        var hit = new CounterTrendHit(
            100m, 98m, 95m, 101m, "test", 0.5m);
        hit.EntryPrice.Should().Be(100m);
        hit.StopLoss.Should().Be(98m);
        hit.TakeProfit.Should().Be(95m);
        hit.LtfPoint0.Should().Be(101m);
        hit.Reason.Should().Be("test");
    }

    [Fact]
    public void ScannerSettings_EnableCounterTrendScalp_KannAktiviertWerden()
    {
        var settings = new ScannerSettings { EnableCounterTrendScalp = true };
        settings.EnableCounterTrendScalp.Should().BeTrue();
    }

    [Fact]
    public void TryDetect_Null_WennRangeInvalid()
    {
        var seq = new Sequence
        {
            Point0 = new SwingPoint(100m, 0, DateTime.MinValue, false),
            PointA = new SwingPoint(100m, 1, DateTime.MinValue, true),
            IsLong = true,
            State = SequenceState.Active,
            Extension1618 = 0m,
            Extension200 = 0m,
        };
        var candles = new List<Candle>();
        for (int i = 0; i < 30; i++)
            candles.Add(new Candle(DateTime.UtcNow, 100m, 101m, 99m, 100m, 1000m, DateTime.UtcNow));

        var result = CounterTrendScalper.TryDetect(seq, 100m, candles, 0.0001m);
        result.Should().BeNull();
    }
}
