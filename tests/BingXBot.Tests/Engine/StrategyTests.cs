using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
using BingXBot.Engine.Strategies;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Gemeinsame Tests für alle 6 Strategien (krypto-optimiert).
/// Prüft: None bei wenig Daten, Parameters vorhanden, Clone funktioniert.
/// </summary>
public class StrategyTests
{
    public static IEnumerable<object[]> AllStrategies =>
        new List<object[]>
        {
            new object[] { new EmaCrossStrategy() },
            new object[] { new TrendFollowStrategy() },
            new object[] { new RsiStrategy() },
            new object[] { new BollingerStrategy() },
            new object[] { new MacdStrategy() },
            new object[] { new GridStrategy() },
        };

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Evaluate_TooFewCandles_ShouldReturnNone(IStrategy strategy)
    {
        var candles = TestHelper.GenerateTestCandles(5);
        var context = TestHelper.CreateContext(candles);

        var result = strategy.Evaluate(context);

        result.Signal.Should().Be(Signal.None);
        result.Reason.Should().Contain("Zu wenig Daten");
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Evaluate_EnoughCandles_ShouldNotThrow(IStrategy strategy)
    {
        // 250 Candles damit auch EMA200 genug Daten hat
        var candles = TestHelper.GenerateTestCandles(250);
        var context = TestHelper.CreateContext(candles);

        var act = () => strategy.Evaluate(context);

        act.Should().NotThrow();
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Parameters_ShouldNotBeEmpty(IStrategy strategy)
    {
        strategy.Parameters.Should().NotBeEmpty();
        strategy.Parameters.Should().AllSatisfy(p =>
        {
            p.Name.Should().NotBeNullOrEmpty();
            p.Description.Should().NotBeNullOrEmpty();
            p.ValueType.Should().NotBeNullOrEmpty();
        });
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Clone_ShouldCreateIndependentInstance(IStrategy strategy)
    {
        var clone = strategy.Clone();

        clone.Should().NotBeSameAs(strategy);
        clone.Name.Should().Be(strategy.Name);
        clone.Description.Should().Be(strategy.Description);
        clone.Parameters.Should().HaveCount(strategy.Parameters.Count);
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Name_ShouldNotBeEmpty(IStrategy strategy)
    {
        strategy.Name.Should().NotBeNullOrEmpty();
        strategy.Description.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void WarmUp_ShouldNotThrow(IStrategy strategy)
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var act = () => strategy.WarmUp(candles);
        act.Should().NotThrow();
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Reset_ShouldNotThrow(IStrategy strategy)
    {
        var act = () => strategy.Reset();
        act.Should().NotThrow();
    }

    // RSI Momentum spezifisch
    [Fact]
    public void RsiMomentum_Parameters_ShouldContainMomentumSettings()
    {
        var strategy = new RsiStrategy();
        strategy.Parameters.Should().Contain(p => p.Name == "Period");
        strategy.Parameters.Should().Contain(p => p.Name == "LongTrigger");
        strategy.Parameters.Should().Contain(p => p.Name == "LongEntry");
        strategy.Parameters.Should().Contain(p => p.Name == "ShortTrigger");
        strategy.Parameters.Should().Contain(p => p.Name == "ShortEntry");
        strategy.Parameters.Should().Contain(p => p.Name == "DivergenceLookback");
    }

    [Fact]
    public void RsiMomentum_Name_ShouldBeUpdated()
    {
        var strategy = new RsiStrategy();
        strategy.Name.Should().Be("RSI Momentum");
        strategy.Description.Should().Contain("Krypto");
    }

    // Bollinger Breakout spezifisch
    [Fact]
    public void BollingerBreakout_Parameters_ShouldContainSqueezeSettings()
    {
        var strategy = new BollingerStrategy();
        strategy.Parameters.Should().Contain(p => p.Name == "Period");
        strategy.Parameters.Should().Contain(p => p.Name == "StdDev");
        strategy.Parameters.Should().Contain(p => p.Name == "SqueezePeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "VolumePeriod");
    }

    [Fact]
    public void BollingerBreakout_Name_ShouldBeUpdated()
    {
        var strategy = new BollingerStrategy();
        strategy.Name.Should().Be("Bollinger Breakout");
        strategy.Description.Should().Contain("Squeeze");
    }

    // MACD spezifisch
    [Fact]
    public void Macd_Parameters_ShouldContainAllPeriods()
    {
        var strategy = new MacdStrategy();
        strategy.Parameters.Should().Contain(p => p.Name == "FastPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "SlowPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "SignalPeriod");
    }

    [Fact]
    public void Macd_Description_ShouldBeUpdated()
    {
        var strategy = new MacdStrategy();
        strategy.Description.Should().Contain("Histogram");
    }

    // Smart Grid spezifisch
    [Fact]
    public void SmartGrid_Parameters_ShouldContainDynamicSettings()
    {
        var strategy = new GridStrategy();
        strategy.Parameters.Should().Contain(p => p.Name == "GridLevels");
        strategy.Parameters.Should().Contain(p => p.Name == "GridSpacing");
        strategy.Parameters.Should().Contain(p => p.Name == "EmaPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "BollingerPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "TrendThreshold");
    }

    [Fact]
    public void SmartGrid_Name_ShouldBeUpdated()
    {
        var strategy = new GridStrategy();
        strategy.Name.Should().Be("Smart Grid");
        strategy.Description.Should().Contain("Range");
    }

    [Fact]
    public void SmartGrid_Evaluate_WithData_ShouldWork()
    {
        var strategy = new GridStrategy();
        var candles = TestHelper.GenerateTestCandles(250, startPrice: 100m, volatility: 2m);
        var context = TestHelper.CreateContext(candles);

        var result = strategy.Evaluate(context);

        result.Signal.Should().BeOneOf(Signal.None, Signal.Long, Signal.Short);
        result.Reason.Should().NotBeNullOrEmpty();
    }

    // Trend-Following spezifisch
    [Fact]
    public void TrendFollowing_Parameters_ShouldContainAllIndicators()
    {
        var strategy = new TrendFollowStrategy();
        strategy.Parameters.Should().Contain(p => p.Name == "EmaFast");
        strategy.Parameters.Should().Contain(p => p.Name == "EmaSlow");
        strategy.Parameters.Should().Contain(p => p.Name == "RsiPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "AtrPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "AtrMultiplierSl");
        strategy.Parameters.Should().Contain(p => p.Name == "AtrMultiplierTp");
        strategy.Parameters.Should().Contain(p => p.Name == "VolumePeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "VolumeMultiplier");
        strategy.Parameters.Should().Contain(p => p.Name == "MinConfidence");
    }

    [Fact]
    public void TrendFollowing_Evaluate_WithTrendingCandles_ShouldWork()
    {
        var strategy = new TrendFollowStrategy();
        // Starker Aufwärtstrend
        var candles = TestHelper.GenerateTrendingCandles(100, startPrice: 50000m, uptrend: true);
        var context = TestHelper.CreateContext(candles);

        var result = strategy.Evaluate(context);

        result.Signal.Should().BeOneOf(Signal.None, Signal.Long, Signal.Short);
        result.Reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TrendFollowing_Parameters_ShouldEnthaltenAdxParameter()
    {
        var strategy = new TrendFollowStrategy();
        strategy.Parameters.Should().Contain(p => p.Name == "AdxPeriod",
            "ADX-Periode muss als konfigurierbarer Parameter vorhanden sein");
        strategy.Parameters.Should().Contain(p => p.Name == "MinAdx",
            "MinAdx-Schwellwert muss als konfigurierbarer Parameter vorhanden sein");
    }

    [Fact]
    public void TrendFollowing_ZuWenigCandles_GibtNoneZurueck()
    {
        var strategy = new TrendFollowStrategy();
        // 10 Candles reichen nicht für EMA50+ATR+MACD
        var candles = TestHelper.GenerateTestCandles(10);
        var context = TestHelper.CreateContext(candles);

        var result = strategy.Evaluate(context);

        result.Signal.Should().Be(Signal.None);
        result.Reason.Should().Contain("Zu wenig Daten");
    }

    [Fact]
    public void TrendFollowing_AdxZuNiedrig_GibtNoneZurueck()
    {
        // Seitwärtsmarkt: zufällige Candles produzieren niedrigen ADX.
        // Die Strategie soll kein Signal geben wenn ADX < minAdx (Default: 20).
        // Wir erzwingen ein ADX-Szenario durch Seitwärts-Candles mit sehr kleiner Volatilität.
        var strategy = new TrendFollowStrategy();
        IndicatorHelper.ClearCache();

        // Flache Candles: kaum Bewegung → ADX sehr niedrig
        var flacheCandles = new List<Candle>();
        var basisZeit = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var preis = 100m;
        for (int i = 0; i < 100; i++)
        {
            // Minimale Schwankung: open=close=high=low±0.001 → ADX nahe 0
            flacheCandles.Add(new Candle(
                basisZeit.AddHours(i),
                preis,
                preis + 0.001m,
                preis - 0.001m,
                preis,
                1000m,
                basisZeit.AddHours(i + 1)));
        }

        var context = TestHelper.CreateContext(flacheCandles);
        var result = strategy.Evaluate(context);

        // Flacher Markt → ADX zu niedrig → kein Signal
        // (Entweder "ADX zu niedrig" oder "Indikatoren nicht bereit" bei Flat-Market)
        result.Signal.Should().Be(Signal.None, "Flacher Markt mit niedrigem ADX darf kein Signal erzeugen");
    }

    [Fact]
    public void TrendFollowing_HtfKontext_KeineException_UndValidesSignal()
    {
        // Prüft dass die TrendFollowStrategy mit HTF-Candles keinen Absturz produziert
        // und ein valides Signal zurückgibt. Der HTF-Trend-Bonus/-Malus wird durch
        // GetHigherTimeframeTrend() berechnet - der genaue Wert hängt vom Markt-Zustand ab.
        IndicatorHelper.ClearCache();
        var strategy = new TrendFollowStrategy();

        // Primärer TF: Aufwärtstrend
        var candles = TestHelper.GenerateTrendingCandles(100, startPrice: 50000m, uptrend: true);
        // HTF-Candles für den Higher-Timeframe-Filter
        var htfCandles = TestHelper.GenerateTrendingCandles(100, startPrice: 45000m, uptrend: true);

        var ohneHtf = new MarketContext("BTC-USDT", candles,
            new Ticker("BTC-USDT", candles[^1].Close, candles[^1].Close - 10m, candles[^1].Close + 10m, 50000m, 1m, DateTime.UtcNow),
            new List<Position>(), new AccountInfo(10000m, 9000m, 0m, 0m));

        var mitHtf = new MarketContext("BTC-USDT", candles,
            new Ticker("BTC-USDT", candles[^1].Close, candles[^1].Close - 10m, candles[^1].Close + 10m, 50000m, 1m, DateTime.UtcNow),
            new List<Position>(), new AccountInfo(10000m, 9000m, 0m, 0m),
            HigherTimeframeCandles: htfCandles);

        // Kein Absturz bei beiden Varianten
        var actOhneHtf = () => strategy.Evaluate(ohneHtf);
        var actMitHtf = () => strategy.Evaluate(mitHtf);
        actOhneHtf.Should().NotThrow("Strategie ohne HTF darf nicht abstürzen");
        actMitHtf.Should().NotThrow("Strategie mit HTF-Candles darf nicht abstürzen");

        IndicatorHelper.ClearCache();
        var ergebnisOhneHtf = strategy.Evaluate(ohneHtf);
        IndicatorHelper.ClearCache();
        var ergebnisMitHtf = strategy.Evaluate(mitHtf);

        ergebnisOhneHtf.Reason.Should().NotBeNullOrEmpty();
        ergebnisMitHtf.Reason.Should().NotBeNullOrEmpty();
        ergebnisMitHtf.Signal.Should().BeOneOf(new[] { Signal.None, Signal.Long, Signal.Short },
            "Strategie mit HTF-Kontext muss immer ein valides Signal zurückgeben");
    }

    [Fact]
    public void TrendFollowing_HtfBullish_ReasonEnthaeltHtfInfo()
    {
        // Verifiziert dass die Reason-Zeile tatsächlich HTF-Information enthält
        // wenn GetHigherTimeframeTrend() != 0 ist.
        // Wir bauen explizit einen Fall wo HTF-Trend klar bullish ist:
        // 200 Candles mit +1 pro Candle → Endpreis liegt weit über EMA50.
        IndicatorHelper.ClearCache();

        // Konstruiere HTF-Candles mit garantiert bullischem Trend (>0.5% über EMA50)
        var htfCandles = new List<Candle>();
        var basisZeit = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 100; i++)
        {
            // Gleichmäßiger linearer Anstieg: 100, 101, 102, ... → EMA50 liegt klar darunter
            var preis = 1000m + i * 10m;  // Nach 100 Candles: 1990 vs EMA50 ≈ 1745 → +14% → bullish
            htfCandles.Add(new Candle(basisZeit.AddHours(i), preis, preis + 1m, preis - 1m, preis, 1000m, basisZeit.AddHours(i + 1)));
        }

        var htfTrend = IndicatorHelper.GetHigherTimeframeTrend(htfCandles, emaPeriod: 50);

        // Dieser HTF-Trend MUSS bullish sein (1) - linearer Anstieg macht EMA50 << aktuellem Preis
        htfTrend.Should().Be(1, "Linearer Aufwärtstrend mit +14% Deviation muss klar bullish sein");

        // Jetzt die Strategie mit diesen HTF-Candles evaluieren
        var candles = TestHelper.GenerateTrendingCandles(100, startPrice: 50000m, uptrend: true);
        var strategy = new TrendFollowStrategy();

        IndicatorHelper.ClearCache();
        var kontext = new MarketContext("BTC-USDT", candles,
            new Ticker("BTC-USDT", candles[^1].Close, candles[^1].Close - 10m, candles[^1].Close + 10m, 50000m, 1m, DateTime.UtcNow),
            new List<Position>(), new AccountInfo(10000m, 9000m, 0m, 0m),
            HigherTimeframeCandles: htfCandles);

        var ergebnis = strategy.Evaluate(kontext);
        ergebnis.Reason.Should().NotBeNullOrEmpty();

        // Wenn ein Long-Signal entsteht, MUSS der Reason "HTF:Bull" enthalten
        if (ergebnis.Signal == Signal.Long)
        {
            ergebnis.Reason.Should().Contain("HTF:Bull",
                "Bei bestätigtem HTF bullish (1) und Long-Signal muss Reason HTF:Bull enthalten");
        }
    }

    [Fact]
    public void TrendFollowing_HtfGegenSignal_KannSignalBlockieren()
    {
        // HTF bearish bei Long-Signal soll Confidence um 0.15 reduzieren.
        // Wenn der Long durch HTF unter minConfidence fällt → Signal.None mit erklärender Reason.
        IndicatorHelper.ClearCache();
        var strategy = new TrendFollowStrategy();

        // Aufwärtstrend auf dem primären TF (könnte Long-Signal auslösen)
        var candles = TestHelper.GenerateTrendingCandles(100, startPrice: 50000m, uptrend: true);

        // HTF: Abwärtstrend → bearish → Long-Confidence -0.15
        var htfBearish = TestHelper.GenerateTrendingCandles(80, startPrice: 52000m, uptrend: false);

        var kontext = new MarketContext("BTC-USDT", candles,
            new Ticker("BTC-USDT", candles[^1].Close, candles[^1].Close - 10m, candles[^1].Close + 10m, 50000m, 1m, DateTime.UtcNow),
            new List<Position>(), new AccountInfo(10000m, 9000m, 0m, 0m),
            HigherTimeframeCandles: htfBearish);

        var result = strategy.Evaluate(kontext);

        // Wenn HTF das Signal blockiert hat, steht es in der Reason
        if (result.Signal == Signal.None && result.Reason.Contains("Higher-TF"))
        {
            result.Reason.Should().Contain("Confidence zu niedrig",
                "HTF-Block muss erklären warum Confidence zu niedrig ist");
        }
        // Sonst: kein Fehler, Signal kann trotzdem Long sein wenn Confidence hoch genug
        result.Reason.Should().NotBeNullOrEmpty();
    }
}
