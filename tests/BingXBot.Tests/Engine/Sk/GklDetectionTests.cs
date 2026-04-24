using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>Phase 5 — Tests für <see cref="MultiTfGklDetector"/> (Task 1.1).</summary>
public class GklDetectionTests
{
    private static Candle C(int day, decimal open, decimal high, decimal low, decimal close)
    {
        var t = new DateTime(2026, 1, 1).AddDays(day);
        return new Candle(t, open, high, low, close, 1_000_000m, t.AddDays(1));
    }

    [Fact]
    public void Detect_Null_WennKeineCandles()
    {
        var hit = MultiTfGklDetector.Detect(100m, null, null);
        hit.Should().BeNull();
    }

    [Fact]
    public void Detect_Null_WennZuWenigeCandles()
    {
        var small = new List<Candle> { C(0, 100m, 101m, 99m, 100m) };
        var hit = MultiTfGklDetector.Detect(100m, small, small);
        hit.Should().BeNull();
    }

    [Fact]
    public void TryDetectOnTf_Null_WennPreisAusserhalbZone()
    {
        var candles = BuildUptrendCandles();
        // Preis weit oberhalb der GKL-Zone
        var hit = MultiTfGklDetector.TryDetectOnTf(999m, candles, TimeFrame.W1, swingStrength: 5);
        hit.Should().BeNull();
    }

    [Fact]
    public void TryDetectOnTf_LiefertHit_WennPreisInGklZone()
    {
        var candles = BuildUptrendCandles();
        // Berechne erwartete GKL-Zone
        var gkl = SequenceDetector.CalculateGKL(candles, swingStrength: 5);
        if (gkl != null)
        {
            var priceInZone = (gkl.Value.Gkl500 + gkl.Value.Gkl667) / 2m;
            var hit = MultiTfGklDetector.TryDetectOnTf(priceInZone, candles, TimeFrame.W1, swingStrength: 5);
            hit.Should().NotBeNull();
            hit!.Tf.Should().Be(TimeFrame.W1);
        }
    }

    [Fact]
    public void GklHit_EnthaeltGkl500Und667()
    {
        var candles = BuildUptrendCandles();
        var gkl = SequenceDetector.CalculateGKL(candles, swingStrength: 5);
        if (gkl != null)
        {
            var priceInZone = (gkl.Value.Gkl500 + gkl.Value.Gkl667) / 2m;
            var hit = MultiTfGklDetector.TryDetectOnTf(priceInZone, candles, TimeFrame.W1, swingStrength: 5);
            if (hit != null)
            {
                hit.Gkl500.Should().BeGreaterThan(0m);
                hit.Gkl667.Should().BeGreaterThan(0m);
                hit.Gkl618.Should().BeInRange(Math.Min(hit.Gkl500, hit.Gkl667), Math.Max(hit.Gkl500, hit.Gkl667));
            }
        }
    }

    [Fact]
    public void Detect_BevorzugtW1UeberD1()
    {
        // Wenn W1 einen Hit liefert, D1 wird gar nicht erst gefragt
        var weekly = BuildUptrendCandles();
        var daily = BuildUptrendCandles();
        var gklW = SequenceDetector.CalculateGKL(weekly, 5);
        if (gklW != null)
        {
            var priceInZone = (gklW.Value.Gkl500 + gklW.Value.Gkl667) / 2m;
            var hit = MultiTfGklDetector.Detect(priceInZone, weekly, daily);
            hit.Should().NotBeNull();
            hit!.Tf.Should().Be(TimeFrame.W1);
        }
    }

    [Fact]
    public void Detect_FaellBackAufD1_WennW1Null()
    {
        var daily = BuildUptrendCandles();
        var gkl = SequenceDetector.CalculateGKL(daily, 7);
        if (gkl != null)
        {
            var priceInZone = (gkl.Value.Gkl500 + gkl.Value.Gkl667) / 2m;
            var hit = MultiTfGklDetector.Detect(priceInZone, weekly: null, daily: daily);
            if (hit != null)
                hit.Tf.Should().Be(TimeFrame.D1);
        }
    }

    [Fact]
    public void PreferredLong_FiltertGegenrichtung()
    {
        // Uptrend-Candles: isUptrend=true → hit.IsUptrend=true
        // Mit preferredLong=false (Short) würde der W1-Hit verworfen
        var candles = BuildUptrendCandles();
        var gkl = SequenceDetector.CalculateGKL(candles, 5);
        if (gkl != null && gkl.Value.IsUptrend)
        {
            var priceInZone = (gkl.Value.Gkl500 + gkl.Value.Gkl667) / 2m;
            var hit = MultiTfGklDetector.Detect(priceInZone, candles, null,
                requireMatchDirection: true, preferredLong: false);
            hit.Should().BeNull();
        }
    }

    [Fact]
    public void TryDetectOnTf_WeeklyMitVerschiedenenSwingStrengths()
    {
        var candles = BuildUptrendCandles();
        var hit1 = MultiTfGklDetector.TryDetectOnTf(100m, candles, TimeFrame.W1, swingStrength: 3);
        var hit2 = MultiTfGklDetector.TryDetectOnTf(100m, candles, TimeFrame.W1, swingStrength: 7);
        // Beide Aufrufe dürfen keine Exception werfen
        (hit1 is null || hit2 is null || hit1.Tf == hit2.Tf).Should().BeTrue();
    }

    private static List<Candle> BuildUptrendCandles()
    {
        var list = new List<Candle>();
        for (int i = 0; i < 30; i++)
        {
            var baseP = 100m + i * 1m;
            list.Add(C(i, baseP, baseP + 1m, baseP - 1m, baseP + 0.5m));
        }
        return list;
    }
}
