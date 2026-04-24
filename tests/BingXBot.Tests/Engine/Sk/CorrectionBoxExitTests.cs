using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>Phase 5 — Tests für <see cref="CorrectionBoxExitClassifier"/> (Task 4.2).</summary>
public class CorrectionBoxExitTests
{
    private static Candle C(decimal open, decimal high, decimal low, decimal close)
    {
        var t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new Candle(t, open, high, low, close, 1_000_000m, t.AddHours(4));
    }

    [Fact]
    public void Long_WickOnly_DochtUnterBox_BodyInBox()
    {
        var candle = C(open: 105m, high: 106m, low: 92m, close: 104m);
        var result = CorrectionBoxExitClassifier.Classify(
            isLong: true, candle, boxUpper: 105m, boxLower: 95m, point0: 90m);
        result.Should().Be(CorrectionBoxExit.WickOnly);
    }

    [Fact]
    public void Long_FullInvalidation_BodyUnterPoint0()
    {
        var candle = C(open: 95m, high: 96m, low: 88m, close: 89m);
        var result = CorrectionBoxExitClassifier.Classify(
            isLong: true, candle, boxUpper: 105m, boxLower: 95m, point0: 90m);
        result.Should().Be(CorrectionBoxExit.FullInvalidation);
    }

    [Fact]
    public void Long_StrongClose_BodyWeitUnterBox()
    {
        var candle = C(open: 94m, high: 96m, low: 91m, close: 92m);
        var result = CorrectionBoxExitClassifier.Classify(
            isLong: true, candle, boxUpper: 105m, boxLower: 95m, point0: 80m,
            strongCloseThreshold: 0.002m);
        result.Should().Be(CorrectionBoxExit.StrongClose);
    }

    [Fact]
    public void Long_InBox_BodyInnerhalb()
    {
        var candle = C(open: 99m, high: 101m, low: 98m, close: 100m);
        var result = CorrectionBoxExitClassifier.Classify(
            isLong: true, candle, boxUpper: 105m, boxLower: 95m, point0: 90m);
        result.Should().Be(CorrectionBoxExit.InBox);
    }

    [Fact]
    public void Short_WickOnly_DochtUeberBox_BodyInBox()
    {
        var candle = C(open: 95m, high: 108m, low: 94m, close: 96m);
        var result = CorrectionBoxExitClassifier.Classify(
            isLong: false, candle, boxUpper: 95m, boxLower: 105m, point0: 110m);
        result.Should().Be(CorrectionBoxExit.WickOnly);
    }

    [Fact]
    public void Short_FullInvalidation_BodyUeberPoint0()
    {
        var candle = C(open: 105m, high: 113m, low: 104m, close: 112m);
        var result = CorrectionBoxExitClassifier.Classify(
            isLong: false, candle, boxUpper: 95m, boxLower: 105m, point0: 110m);
        result.Should().Be(CorrectionBoxExit.FullInvalidation);
    }

    [Fact]
    public void Short_StrongClose_BodyWeitUeberBox()
    {
        var candle = C(open: 106m, high: 109m, low: 104m, close: 108m);
        var result = CorrectionBoxExitClassifier.Classify(
            isLong: false, candle, boxUpper: 95m, boxLower: 105m, point0: 120m,
            strongCloseThreshold: 0.002m);
        result.Should().Be(CorrectionBoxExit.StrongClose);
    }

    [Fact]
    public void Long_WickPikeUnterPoint0_BodyInBox_NichtInvalidiert()
    {
        // Kritisch: Docht bricht sogar Point 0, aber Body schließt in Box → WickOnly (nicht Full)
        var candle = C(open: 100m, high: 102m, low: 88m, close: 99m);
        var result = CorrectionBoxExitClassifier.Classify(
            isLong: true, candle, boxUpper: 105m, boxLower: 95m, point0: 90m);
        result.Should().Be(CorrectionBoxExit.WickOnly);
    }
}
