using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>
/// Phase 5 — Tests für Docht-basierte Fibonacci-Messung (Task 4.1).
/// Buch-Regel: Point0/PointA/PointB immer aus candle.Low (Long) bzw. candle.High (Short).
/// </summary>
public class WickBasedFibMeasurementTests
{
    private static Candle C(int hour, decimal open, decimal high, decimal low, decimal close)
    {
        var t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(hour);
        return new Candle(t, open, high, low, close, 1_000_000m, t.AddHours(1));
    }

    [Fact]
    public void Long_Point0_NimmtCandleLow_NichtClose()
    {
        var candles = new List<Candle>
        {
            C(0, 100m, 100.5m, 90m, 100m),   // Langer Docht nach unten, Close bei 100
            C(1, 100m, 102m, 99m, 101m),
            C(2, 101m, 110m, 100m, 109m),     // Impuls nach oben
            C(3, 109m, 111m, 108m, 110m),
            C(4, 110m, 112m, 109m, 111m),
        };
        for (int i = 5; i < 30; i++)
            candles.Add(C(i, 111m, 113m, 110m, 112m));

        var (_, longMachine, _) = SequenceStateMachine.FromCandlesBoth(candles, 0.3m, 0.2m, minPoint0Candles: 1);
        longMachine.Point0.Should().Be(90m, "Point 0 muss aus candle.Low stammen, nicht aus Close (100)");
    }

    [Fact]
    public void Short_Point0_NimmtCandleHigh_NichtClose()
    {
        var candles = new List<Candle>
        {
            C(0, 100m, 110m, 99.5m, 100m),   // Langer Docht nach oben
            C(1, 100m, 101m, 98m, 99m),
            C(2, 99m, 100m, 90m, 91m),       // Impuls nach unten
            C(3, 91m, 92m, 89m, 90m),
            C(4, 90m, 91m, 88m, 89m),
        };
        for (int i = 5; i < 30; i++)
            candles.Add(C(i, 89m, 90m, 87m, 88m));

        var (_, _, shortMachine) = SequenceStateMachine.FromCandlesBoth(candles, 0.3m, 0.2m, minPoint0Candles: 1);
        shortMachine.Point0.Should().Be(110m, "Point 0 muss aus candle.High stammen, nicht aus Close (100)");
    }

    [Fact]
    public void Long_PointA_NimmtCandleHigh()
    {
        // Point0 bei 100, Impuls-High bei 110 (mit Docht bis 111), Korrektur
        var candles = new List<Candle>
        {
            C(0, 100m, 101m, 99m, 100m),
            C(1, 100m, 105m, 99m, 104m),
            C(2, 104m, 111m, 103m, 110m),    // Impulsgipfel — PointA sollte 111 (High) sein
            C(3, 110m, 110.5m, 106m, 107m),  // Korrektur
            C(4, 107m, 108m, 104m, 105m),
        };
        for (int i = 5; i < 30; i++)
            candles.Add(C(i, 105m, 106m, 104m, 105m));

        var (_, longMachine, _) = SequenceStateMachine.FromCandlesBoth(candles, 0.3m, 0.2m, minPoint0Candles: 1);
        longMachine.PointA.Should().BeGreaterThanOrEqualTo(111m, "PointA muss aus candle.High (111) kommen");
    }

    [Fact]
    public void Doji_DochtExtremWirdGenommen()
    {
        // Doji = sehr kleiner Body, große Wicks. Point 0 muss das Wick-Low sein.
        var candles = new List<Candle>
        {
            C(0, 100m, 100.5m, 85m, 100.1m), // Kleiner Body, riesiger unterer Wick
            C(1, 100m, 101m, 99m, 100m),
            C(2, 100m, 110m, 99m, 109m),     // Impuls
        };
        for (int i = 3; i < 30; i++)
            candles.Add(C(i, 109m, 112m, 107m, 110m));

        var (_, longMachine, _) = SequenceStateMachine.FromCandlesBoth(candles, 0.3m, 0.2m, minPoint0Candles: 1);
        longMachine.Point0.Should().Be(85m, "Doji-Wick-Extrem (85) muss Point 0 sein, nicht Close (100.1)");
    }
}
