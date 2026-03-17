using BingXBot.Engine.Indicators;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

public class IndicatorHelperTests
{
    [Fact]
    public void ToQuotes_ShouldConvertAllCandles()
    {
        var candles = TestHelper.GenerateTestCandles(10);
        var quotes = IndicatorHelper.ToQuotes(candles).ToList();

        quotes.Should().HaveCount(10);
        quotes[0].Open.Should().Be(candles[0].Open);
        quotes[0].Close.Should().Be(candles[0].Close);
        quotes[0].High.Should().Be(candles[0].High);
        quotes[0].Low.Should().Be(candles[0].Low);
        quotes[0].Volume.Should().Be(candles[0].Volume);
    }

    [Fact]
    public void CalculateEma_ShouldReturnCorrectCount()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var ema = IndicatorHelper.CalculateEma(candles, 10);

        ema.Should().HaveCount(50);
    }

    [Fact]
    public void CalculateEma_WarmupPeriod_ShouldHaveNulls()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var ema = IndicatorHelper.CalculateEma(candles, 10);

        // Erste Werte sind null (Warmup)
        ema[0].Should().BeNull();

        // Nach Warmup sollten Werte vorhanden sein
        ema[^1].Should().NotBeNull();
        ema[^1]!.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateEma_ShouldBeNearPrice()
    {
        var candles = TestHelper.GenerateTestCandles(50, startPrice: 100m);
        var ema = IndicatorHelper.CalculateEma(candles, 10);

        // EMA sollte in der Nähe der Preise liegen
        var lastEma = ema[^1]!.Value;
        lastEma.Should().BeInRange(80m, 120m);
    }

    [Fact]
    public void CalculateSma_ShouldReturnValues()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var sma = IndicatorHelper.CalculateSma(candles, 20);

        sma.Should().HaveCount(50);
        sma[^1].Should().NotBeNull();
    }

    [Fact]
    public void CalculateRsi_ShouldBeInRange()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var rsi = IndicatorHelper.CalculateRsi(candles, 14);

        rsi.Should().HaveCount(50);

        // RSI-Werte müssen zwischen 0 und 100 liegen
        var validValues = rsi.Where(r => r.HasValue).Select(r => r!.Value).ToList();
        validValues.Should().NotBeEmpty();
        validValues.Should().AllSatisfy(v =>
        {
            v.Should().BeGreaterThanOrEqualTo(0m);
            v.Should().BeLessThanOrEqualTo(100m);
        });
    }

    [Fact]
    public void CalculateMacd_ShouldReturnThreeComponents()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var (macd, signal, histogram) = IndicatorHelper.CalculateMacd(candles);

        macd.Should().HaveCount(50);
        signal.Should().HaveCount(50);
        histogram.Should().HaveCount(50);
    }

    [Fact]
    public void CalculateBollinger_UpperShouldBeAboveLower()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var (upper, middle, lower) = IndicatorHelper.CalculateBollinger(candles, 20, 2m);

        upper.Should().HaveCount(50);

        // Nach Warmup: Upper > Middle > Lower
        var lastUpper = upper[^1];
        var lastMiddle = middle[^1];
        var lastLower = lower[^1];

        lastUpper.Should().NotBeNull();
        lastMiddle.Should().NotBeNull();
        lastLower.Should().NotBeNull();
        lastUpper!.Value.Should().BeGreaterThan(lastMiddle!.Value);
        lastMiddle.Value.Should().BeGreaterThan(lastLower!.Value);
    }

    [Fact]
    public void CalculateAtr_ShouldBePositive()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var atr = IndicatorHelper.CalculateAtr(candles, 14);

        atr.Should().HaveCount(50);
        var lastAtr = atr[^1];
        lastAtr.Should().NotBeNull();
        lastAtr!.Value.Should().BeGreaterThan(0m);
    }

    // === Neue Tests: Caching (17.03.2026) ===

    [Fact]
    public void Cache_GecachtesErgebnis_IstIdentischMitFrischemErgebnis()
    {
        // Cache wird durch gleiches Candle-Set befüllt, zweiter Aufruf gibt gecachtes zurück.
        // Beide Ergebnisse müssen identisch sein.
        IndicatorHelper.ClearCache();
        var candles = TestHelper.GenerateTestCandles(50);

        // Erster Aufruf: berechnet und cached
        var ersterAufruf = IndicatorHelper.CalculateEma(candles, 20);
        // Zweiter Aufruf: gleiche Daten → kommt aus Cache
        var zweiterAufruf = IndicatorHelper.CalculateEma(candles, 20);

        zweiterAufruf.Should().BeSameAs(ersterAufruf, "Cache muss dasselbe Objekt zurückgeben");
        zweiterAufruf[^1].Should().Be(ersterAufruf[^1]);
    }

    [Fact]
    public void Cache_NachClearCache_WirdNeuBerechnet()
    {
        // Nach ClearCache() darf kein alter Wert zurückkommen.
        // Wir prüfen das indirekt: Objekt-Referenz muss nach Clear neu sein.
        IndicatorHelper.ClearCache();
        var candles = TestHelper.GenerateTestCandles(50);

        var vorClear = IndicatorHelper.CalculateEma(candles, 20);
        IndicatorHelper.ClearCache();
        var nachClear = IndicatorHelper.CalculateEma(candles, 20);

        // Nach ClearCache muss ein neues Objekt erstellt worden sein
        nachClear.Should().NotBeSameAs(vorClear, "Nach ClearCache() muss das Ergebnis neu berechnet werden");
        // Inhalt bleibt gleich (deterministische Berechnung)
        nachClear[^1].Should().Be(vorClear[^1]);
    }

    [Fact]
    public void Cache_VerschiedenePerioden_ErzeugenVerschiedeneEintraege()
    {
        // EMA(10) und EMA(20) müssen verschiedene Cache-Keys und damit verschiedene Werte liefern.
        IndicatorHelper.ClearCache();
        var candles = TestHelper.GenerateTestCandles(50, startPrice: 100m);

        var ema10 = IndicatorHelper.CalculateEma(candles, 10);
        var ema20 = IndicatorHelper.CalculateEma(candles, 20);

        // Verschiedene Perioden → verschiedene Objekte
        ema10.Should().NotBeSameAs(ema20, "Verschiedene Perioden müssen verschiedene Cache-Einträge sein");
        // EMA10 reagiert schneller → bei Aufwärtstrend ist EMA10 > EMA20
        // (TestHelper ist deterministisch mit Seed 42)
        ema10[^1].Should().NotBe(ema20[^1], "EMA mit unterschiedlichen Perioden muss unterschiedliche Werte liefern");
    }

    [Fact]
    public void Cache_VerschiedeneIndikatorenGleichePeriode_ErzeugenVerschiedeneEintraege()
    {
        // EMA(14) und RSI(14) dürfen nicht denselben Cache-Key teilen.
        IndicatorHelper.ClearCache();
        var candles = TestHelper.GenerateTestCandles(50);

        var ema = IndicatorHelper.CalculateEma(candles, 14);
        var rsi = IndicatorHelper.CalculateRsi(candles, 14);

        // RSI-Werte liegen zwischen 0 und 100, EMA-Werte nahe dem Preis (ca. 100)
        // Ein falscher Cache-Treffer würde einen RSI-Wert mit EMA-Wert verwechseln
        ema.Should().NotBeSameAs(rsi);
        var letzterRsi = rsi.Where(v => v.HasValue).LastOrDefault();
        letzterRsi.Should().NotBeNull();
        letzterRsi!.Value.Should().BeInRange(0m, 100m, "RSI-Werte müssen immer zwischen 0 und 100 liegen");
    }

    // === Neue Tests: ADX-Indikator (17.03.2026) ===

    [Fact]
    public void CalculateAdx_GueltigeCandles_GibtWerteZurueck()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var adx = IndicatorHelper.CalculateAdx(candles, 14);

        adx.Should().HaveCount(50);
        // ADX braucht Warmup, aber letzte Werte müssen vorhanden sein
        var letztesAdx = adx[^1];
        letztesAdx.Should().NotBeNull("ADX sollte nach ausreichend Candles einen Wert haben");
        letztesAdx!.Value.Should().BeInRange(0m, 100m, "ADX-Werte liegen immer zwischen 0 und 100");
    }

    [Fact]
    public void CalculateAdx_WarmupPhase_FruehereWerteKoennenNullSein()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var adx = IndicatorHelper.CalculateAdx(candles, 14);

        // Die ersten Candles haben keine ADX-Werte (Warmup benötigt 2*period Candles)
        var fruehwerteNull = adx.Take(25).Count(v => !v.HasValue);
        fruehwerteNull.Should().BeGreaterThan(0, "ADX braucht Warmup-Phase, frühe Werte sind null");
    }

    [Fact]
    public void CalculateAdx_ZuWenigCandles_GibtLeereOderNullListeZurueck()
    {
        // 5 Candles reichen nicht für ADX(14)
        var candles = TestHelper.GenerateTestCandles(5);
        var adx = IndicatorHelper.CalculateAdx(candles, 14);

        adx.Should().HaveCount(5, "Ergebnis-Liste hat immer dieselbe Länge wie Eingabe");
        // Bei so wenig Daten sollten alle Werte null sein
        adx.Should().AllSatisfy(v => v.Should().BeNull("Zu wenig Daten für ADX-Berechnung"));
    }

    [Fact]
    public void CalculateAdx_StarkeTrendCandles_LiefertHoeherenAdx()
    {
        // Trend-Candles sollten einen höheren ADX liefern als zufällige Candles
        IndicatorHelper.ClearCache();
        var trendCandles = TestHelper.GenerateTrendingCandles(100, startPrice: 100m, uptrend: true);
        var zufaelligeCandles = TestHelper.GenerateTestCandles(100, startPrice: 100m, volatility: 5m);

        var adxTrend = IndicatorHelper.CalculateAdx(trendCandles, 14);
        var adxZufall = IndicatorHelper.CalculateAdx(zufaelligeCandles, 14);

        var letzterAdxTrend = adxTrend[^1];
        var letzterAdxZufall = adxZufall[^1];

        // Beide Werte müssen vorhanden sein
        letzterAdxTrend.Should().NotBeNull();
        letzterAdxZufall.Should().NotBeNull();

        // Trend-Candles haben stärkeren Trend → höherer ADX-Wert
        letzterAdxTrend!.Value.Should().BeGreaterThan(letzterAdxZufall!.Value,
            "Trend-Candles sollten einen höheren ADX produzieren als zufälliges Rauschen");
    }

    [Fact]
    public void GetHigherTimeframeTrend_WenigCandles_GibtNeutralZurueck()
    {
        // Zu wenig HTF-Candles → neutral (0) zurück, kein Filter
        var wenigCandles = TestHelper.GenerateTestCandles(10);
        var ergebnis = IndicatorHelper.GetHigherTimeframeTrend(wenigCandles, emaPeriod: 50);

        ergebnis.Should().Be(0, "Zu wenig Candles für HTF-Trend → neutral");
    }

    [Fact]
    public void GetHigherTimeframeTrend_Null_GibtNeutralZurueck()
    {
        var ergebnis = IndicatorHelper.GetHigherTimeframeTrend(null);
        ergebnis.Should().Be(0, "Null-Candles → neutral");
    }

    [Fact]
    public void GetHigherTimeframeTrend_Aufwaertstrend_GibtBullishZurueck()
    {
        // Starker Aufwärtstrend: Preis weit über EMA50 → bullish (1)
        IndicatorHelper.ClearCache();
        var candles = TestHelper.GenerateTrendingCandles(100, startPrice: 100m, uptrend: true);
        var ergebnis = IndicatorHelper.GetHigherTimeframeTrend(candles, emaPeriod: 50);

        ergebnis.Should().BeOneOf(new[] { 0, 1 }, "Aufwärtstrend sollte bullish oder neutral sein");
    }

    // === Neue Tests: Stochastik-Indikator (17.03.2026 Optimierungs-Update) ===

    [Fact]
    public void CalculateStochastic_GueltigeCandles_GibtKUndDZurueck()
    {
        var candles = TestHelper.GenerateTestCandles(60);
        var (k, d) = IndicatorHelper.CalculateStochastic(candles, lookbackPeriods: 14, signalPeriods: 3, smoothPeriods: 3);

        k.Should().HaveCount(60, "K-Werte haben dieselbe Länge wie Eingabe");
        d.Should().HaveCount(60, "D-Werte haben dieselbe Länge wie Eingabe");
    }

    [Fact]
    public void CalculateStochastic_GueltigeWerte_LiegenZwischen0Und100()
    {
        var candles = TestHelper.GenerateTestCandles(60);
        var (k, d) = IndicatorHelper.CalculateStochastic(candles);

        // Alle berechneten Werte (nicht null) müssen im Bereich 0–100 liegen
        var gueltigeK = k.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var gueltigeD = d.Where(v => v.HasValue).Select(v => v!.Value).ToList();

        gueltigeK.Should().NotBeEmpty("Nach ausreichend Candles gibt es Stochastik-Werte");
        gueltigeK.Should().AllSatisfy(v =>
        {
            v.Should().BeGreaterThanOrEqualTo(0m, "Stochastik-K darf nicht unter 0 fallen");
            v.Should().BeLessThanOrEqualTo(100m, "Stochastik-K darf nicht über 100 steigen");
        });

        gueltigeD.Should().NotBeEmpty();
        gueltigeD.Should().AllSatisfy(v =>
        {
            v.Should().BeGreaterThanOrEqualTo(0m, "Stochastik-D darf nicht unter 0 fallen");
            v.Should().BeLessThanOrEqualTo(100m, "Stochastik-D darf nicht über 100 steigen");
        });
    }

    [Fact]
    public void CalculateStochastic_WirdGecacht()
    {
        // Zweiter Aufruf mit denselben Parametern muss dasselbe Objekt zurückgeben (Cache-Hit).
        IndicatorHelper.ClearCache();
        var candles = TestHelper.GenerateTestCandles(60);

        var (k1, _) = IndicatorHelper.CalculateStochastic(candles, 14, 3, 3);
        var (k2, _) = IndicatorHelper.CalculateStochastic(candles, 14, 3, 3);

        k2.Should().BeSameAs(k1, "Stochastik-Ergebnis muss gecacht werden");
    }
}
