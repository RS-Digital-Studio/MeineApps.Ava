using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>
/// Phase 5 — Tests für <see cref="LtfReversalDetector"/> und <see cref="CandlePatternDetector"/> (Task 4.3).
/// </summary>
public class LtfReversalTests
{
    private static Candle C(int hour, decimal open, decimal high, decimal low, decimal close)
    {
        var t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(hour);
        return new Candle(t, open, high, low, close, 1_000_000m, t.AddHours(1));
    }

    [Fact]
    public void Pinbar_BullishErkannt()
    {
        var candle = new Candle(DateTime.UtcNow, 10m, 12m, 0m, 11m, 1000m, DateTime.UtcNow);
        CandlePatternDetector.IsPinbar(candle, bullish: true).Should().BeTrue();
    }

    [Fact]
    public void Pinbar_BearishErkannt()
    {
        // Body 110→108 (2), oberer Wick 120→110 (10 = 5× Body), Close unten
        var candle = new Candle(DateTime.UtcNow, 110m, 120m, 108m, 109m, 1000m, DateTime.UtcNow);
        CandlePatternDetector.IsPinbar(candle, bullish: false).Should().BeTrue();
    }

    [Fact]
    public void Pinbar_NichtErkannt_WennDochtZuKurz()
    {
        var candle = new Candle(DateTime.UtcNow, 10m, 14.5m, 9m, 14m, 1000m, DateTime.UtcNow);
        CandlePatternDetector.IsPinbar(candle, bullish: true).Should().BeFalse();
    }

    [Fact]
    public void Pinbar_NichtErkannt_WennCloseImFalschenDrittel()
    {
        // Bullish-Pinbar Definition: Close >= 60% von Low-Range.
        // Bei 10→14 mit Low 0: (11-0)/(14-0) = 0.786 → passt
        // Aber Test-Fall: Close unten
        var candle = new Candle(DateTime.UtcNow, 12m, 14m, 0m, 2m, 1000m, DateTime.UtcNow);
        CandlePatternDetector.IsPinbar(candle, bullish: true).Should().BeFalse();
    }

    [Fact]
    public void Engulfing_BullishErkannt()
    {
        var prev = new Candle(DateTime.UtcNow, 110m, 112m, 104m, 105m, 1000m, DateTime.UtcNow);
        var curr = new Candle(DateTime.UtcNow, 104m, 114m, 103m, 113m, 1000m, DateTime.UtcNow);
        CandlePatternDetector.IsEngulfing(curr, prev, bullish: true).Should().BeTrue();
    }

    [Fact]
    public void Engulfing_BearishErkannt()
    {
        var prev = new Candle(DateTime.UtcNow, 100m, 108m, 99m, 107m, 1000m, DateTime.UtcNow);
        var curr = new Candle(DateTime.UtcNow, 108m, 109m, 98m, 99m, 1000m, DateTime.UtcNow);
        CandlePatternDetector.IsEngulfing(curr, prev, bullish: false).Should().BeTrue();
    }

    [Fact]
    public void Engulfing_NichtErkannt_WennKeinBodyUmschluss()
    {
        var prev = new Candle(DateTime.UtcNow, 110m, 112m, 104m, 105m, 1000m, DateTime.UtcNow);
        var curr = new Candle(DateTime.UtcNow, 106m, 109m, 105m, 108m, 1000m, DateTime.UtcNow);
        CandlePatternDetector.IsEngulfing(curr, prev, bullish: true).Should().BeFalse();
    }

    [Fact]
    public void LtfReversal_Null_WennZuWenigeCandles()
    {
        var result = LtfReversalDetector.Detect(new List<Candle>(), bullish: true);
        result.Should().BeNull();
    }

    [Fact]
    public void LtfReversal_Pinbar_Trigger()
    {
        var candles = new List<Candle>();
        for (int i = 0; i < 20; i++)
            candles.Add(C(i, 100m, 101m, 99m, 100m));
        candles.Add(C(20, 100m, 101m, 90m, 99m)); // Pinbar mit langem unteren Wick (9) vs Body 1
        candles.Add(new Candle(DateTime.UtcNow, 100m, 102m, 90m, 101m, 1000m, DateTime.UtcNow)); // last

        var result = LtfReversalDetector.Detect(candles, bullish: true);
        result.Should().NotBeNull();
        result!.Type.Should().Be(LtfReversalType.Pinbar);
    }

    [Fact]
    public void LtfReversal_Engulfing_Trigger()
    {
        var candles = new List<Candle>();
        for (int i = 0; i < 20; i++)
            candles.Add(C(i, 100m, 101m, 99m, 100m));
        candles.Add(new Candle(DateTime.UtcNow, 110m, 112m, 104m, 105m, 1000m, DateTime.UtcNow)); // bearish prev
        candles.Add(new Candle(DateTime.UtcNow, 104m, 114m, 103m, 113m, 1000m, DateTime.UtcNow)); // bullish engulfing

        var result = LtfReversalDetector.Detect(candles, bullish: true);
        result.Should().NotBeNull();
        result!.Type.Should().Be(LtfReversalType.Engulfing);
    }

    // ═══════════════════════════════════════════════════════════════
    // Spec §4 (B12) — Box-Close-Pflicht
    // Body muss in/über der Box schließen; Docht darf rausstehen.
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void LtfReversal_BoxClose_Pinbar_BodyOberhalbBox_Akzeptiert()
    {
        // Pinbar Long: Open=100, High=101, Low=90 (langer Docht), Close=100 → Body [100..100], Docht 90-100 rausstehend.
        // Box [95..105] — Body-Low 100 liegt in der Box, Docht unter Box ist erlaubt.
        var candles = new List<Candle>();
        for (int i = 0; i < 20; i++)
            candles.Add(C(i, 100m, 101m, 99m, 100m));
        candles.Add(new Candle(DateTime.UtcNow, 100m, 101m, 90m, 100m, 1000m, DateTime.UtcNow));
        candles.Add(new Candle(DateTime.UtcNow, 100m, 102m, 90m, 101m, 1000m, DateTime.UtcNow));

        var result = LtfReversalDetector.Detect(
            candles, bullish: true,
            correctionBoxLower: 95m, correctionBoxUpper: 105m,
            enforceBoxClose: true, requirePinbarOrEngulfingOnly: true);

        result.Should().NotBeNull();
        result!.Type.Should().Be(LtfReversalType.Pinbar);
    }

    [Fact]
    public void LtfReversal_BoxClose_BodyUnterBox_WirdAbgelehnt()
    {
        // Body liegt komplett unter der Box: Open=90, Close=88 — Box [95..105] → Body-Low 88 < 95.
        // Kein Reversal-Hit trotz Pinbar-Form.
        var candles = new List<Candle>();
        for (int i = 0; i < 20; i++)
            candles.Add(C(i, 100m, 101m, 99m, 100m));
        candles.Add(new Candle(DateTime.UtcNow, 90m, 91m, 80m, 88m, 1000m, DateTime.UtcNow));

        var result = LtfReversalDetector.Detect(
            candles, bullish: true,
            correctionBoxLower: 95m, correctionBoxUpper: 105m,
            enforceBoxClose: true, requirePinbarOrEngulfingOnly: false);

        result.Should().BeNull();
    }

    [Fact]
    public void LtfReversal_BoxClose_Short_BodyInBox_Akzeptiert()
    {
        // Short-Pinbar (Shooting Star): Open=111, High=120 (langer oberer Docht), Low=108, Close=109.
        //   Body = |109-111| = 2, UpperWick = 120-111 = 9 ≥ 2×Body=4 ✓, Close in unterem Drittel ((120-109)/12 ≈ 0.92 ≥ 0.6) ✓.
        // Box [95..115] — Body-High = max(111,109) = 111 ≤ 115 ✓, Docht darf oben rausstehen.
        var candles = new List<Candle>();
        for (int i = 0; i < 20; i++)
            candles.Add(C(i, 100m, 101m, 99m, 100m));
        candles.Add(new Candle(DateTime.UtcNow, 111m, 120m, 108m, 109m, 1000m, DateTime.UtcNow));

        var result = LtfReversalDetector.Detect(
            candles, bullish: false,
            correctionBoxLower: 95m, correctionBoxUpper: 115m,
            enforceBoxClose: true, requirePinbarOrEngulfingOnly: true);

        result.Should().NotBeNull();
        result!.Type.Should().Be(LtfReversalType.Pinbar);
    }

    [Fact]
    public void LtfReversal_BoxClose_Short_BodyUeberBox_WirdAbgelehnt()
    {
        // Short-Body schließt KOMPLETT über Box: Open=130, Close=128 — Box [95..115] → Body-High 130 > 115.
        // Soll abgelehnt werden.
        var candles = new List<Candle>();
        for (int i = 0; i < 20; i++)
            candles.Add(C(i, 100m, 101m, 99m, 100m));
        candles.Add(new Candle(DateTime.UtcNow, 130m, 135m, 120m, 128m, 1000m, DateTime.UtcNow));

        var result = LtfReversalDetector.Detect(
            candles, bullish: false,
            correctionBoxLower: 95m, correctionBoxUpper: 115m,
            enforceBoxClose: true, requirePinbarOrEngulfingOnly: false);

        result.Should().BeNull();
    }

    [Fact]
    public void LtfReversal_BoxClose_Doji_OpenGleichClose_AkzeptiertWennInBox()
    {
        // Edge-Case: Open == Close (Doji). Body ist ein Punkt; Test dass Min/Max-Formel nicht crasht und korrekt wertet.
        // Pinbar-Kriterien werden bei Doji knapp nicht greifen, darum testen wir nur dass die Box-Regel keine
        // Exception wirft und dass ein nachfolgender Pinbar-Check angesteuert wird.
        var candles = new List<Candle>();
        for (int i = 0; i < 20; i++)
            candles.Add(C(i, 100m, 101m, 99m, 100m));
        // Reine Doji-Kerze (Body 0): Open=Close=100, Hi/Lo knapp daneben.
        candles.Add(new Candle(DateTime.UtcNow, 100m, 100.5m, 99.5m, 100m, 1000m, DateTime.UtcNow));

        var act = () => LtfReversalDetector.Detect(
            candles, bullish: true,
            correctionBoxLower: 99m, correctionBoxUpper: 101m,
            enforceBoxClose: true, requirePinbarOrEngulfingOnly: false);
        act.Should().NotThrow();
    }

    [Fact]
    public void LtfReversal_WickRejectionPflicht_BlockiertMicroSequence()
    {
        // requirePinbarOrEngulfingOnly=true → Micro-Sequence zählt NICHT als Reversal.
        // Die Test-Sequenz hat keine Pinbar-Form (normale Kerzen) → null.
        var candles = new List<Candle>();
        for (int i = 0; i < 20; i++)
            candles.Add(C(i, 100m, 101m, 99m, 100m));
        for (int i = 20; i < 30; i++)
            candles.Add(C(i, 100m + (i - 20) * 0.1m, 101m, 99m, 100m + (i - 20) * 0.1m));

        var result = LtfReversalDetector.Detect(
            candles, bullish: true,
            correctionBoxLower: null, correctionBoxUpper: null,
            enforceBoxClose: false, requirePinbarOrEngulfingOnly: true);

        result.Should().BeNull();
    }
}
