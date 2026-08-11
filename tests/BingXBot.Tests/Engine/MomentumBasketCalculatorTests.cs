using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Portfolio;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Tests fuer den geteilten <see cref="MomentumBasketCalculator"/> (Cross-Sectional-Momentum-Kernlogik,
/// genutzt von Backtest <c>CrossSectionalMomentumEngine</c> und dem kuenftigen Live-Service).
/// </summary>
public class MomentumBasketCalculatorTests
{
    /// <summary>Geometrische Kerzen-Serie close[i] = 100·(1+g)^i — 30-Kerzen-ROC ist monoton in g.</summary>
    private static List<Candle> Series(decimal dailyG, int count = 40)
    {
        var candles = new List<Candle>();
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var price = 100m;
        for (var i = 0; i < count; i++)
        {
            var open = price;
            var close = price * (1m + dailyG);
            var high = Math.Max(open, close) * 1.001m;
            var low = Math.Min(open, close) * 0.999m;
            candles.Add(new Candle(baseTime.AddHours(4 * i), open, high, low, close, 1000m, baseTime.AddHours(4 * (i + 1))));
            price = close;
        }
        return candles;
    }

    [Fact]
    public void ComputeBasket_LongtStaerkste_ShortSchwaechste()
    {
        var universe = new (string, IReadOnlyList<Candle>)[]
        {
            ("AAA", Series(0.010m)),   // staerkster Aufwaerts
            ("BBB", Series(0.005m)),
            ("CCC", Series(0.001m)),
            ("DDD", Series(-0.003m)),
            ("EEE", Series(-0.010m)),  // staerkster Abwaerts
        };

        var basket = MomentumBasketCalculator.ComputeBasket(universe, lookback: 30, longK: 2, shortK: 2, riskAdjusted: false);

        basket.Should().HaveCount(4);
        basket["AAA"].Should().Be(Side.Buy);
        basket["BBB"].Should().Be(Side.Buy);
        basket["DDD"].Should().Be(Side.Sell);
        basket["EEE"].Should().Be(Side.Sell);
        basket.Should().NotContainKey("CCC"); // 3.-staerkster, faellt aus beiden Top-2 raus
    }

    [Fact]
    public void ComputeBasket_NurPositiveLong_NurNegativeShort()
    {
        // Alle aufwaerts → keine Shorts, auch wenn shortK > 0 (kein Symbol mit Momentum < 0).
        var universe = new (string, IReadOnlyList<Candle>)[]
        {
            ("AAA", Series(0.010m)),
            ("BBB", Series(0.005m)),
            ("CCC", Series(0.002m)),
        };

        var basket = MomentumBasketCalculator.ComputeBasket(universe, lookback: 30, longK: 5, shortK: 5, riskAdjusted: false);

        basket.Values.Should().OnlyContain(s => s == Side.Buy);
        basket.Should().HaveCount(3); // longK=5, aber nur 3 positive Symbole
    }

    [Fact]
    public void ComputeBasket_RespektiertK()
    {
        var universe = Enumerable.Range(0, 10)
            .Select(i => ($"S{i}", (IReadOnlyList<Candle>)Series(0.01m - i * 0.002m)))
            .ToArray();

        var basket = MomentumBasketCalculator.ComputeBasket(universe, lookback: 30, longK: 3, shortK: 3, riskAdjusted: false);

        basket.Count(kv => kv.Value == Side.Buy).Should().Be(3);
        basket.Count(kv => kv.Value == Side.Sell).Should().Be(3);
    }

    [Fact]
    public void ComputeBasket_RankBuffer_HaeltGehaltenesSymbolImFenster()
    {
        // Rang: S0 > S1 > S2 > S3 > S4 (alle positiv). Gehalten: S3 (Rang-Index 3).
        // longK=2, Buffer=2 → Fenster = Top-4 → S3 bleibt im Slot, nur 1 Slot wird neu (S0) besetzt.
        var universe = Enumerable.Range(0, 5)
            .Select(i => ($"S{i}", (IReadOnlyList<Candle>)Series(0.010m - i * 0.001m)))
            .ToArray();
        var held = new Dictionary<string, Side> { ["S3"] = Side.Buy };

        var basket = MomentumBasketCalculator.ComputeBasket(universe, lookback: 30, longK: 2, shortK: 0,
            riskAdjusted: false, skip: 0, currentBasket: held, exitRankBuffer: 2, clusterDiversify: false);

        basket.Should().HaveCount(2);
        basket.Should().ContainKey("S3");   // Hysterese: innerhalb Top-(K+Buffer) gehalten
        basket.Should().ContainKey("S0");   // bester Neuzugang fuellt den Rest-Slot
        basket.Should().NotContainKey("S1"); // verdraengt durch die gehaltene S3
    }

    [Fact]
    public void ComputeBasket_RankBuffer_TauschtAusserhalbDesFensters()
    {
        // Gehalten: S4 (Rang-Index 4). longK=2, Buffer=1 → Fenster = Top-3 → S4 fliegt raus.
        var universe = Enumerable.Range(0, 5)
            .Select(i => ($"S{i}", (IReadOnlyList<Candle>)Series(0.010m - i * 0.001m)))
            .ToArray();
        var held = new Dictionary<string, Side> { ["S4"] = Side.Buy };

        var basket = MomentumBasketCalculator.ComputeBasket(universe, lookback: 30, longK: 2, shortK: 0,
            riskAdjusted: false, skip: 0, currentBasket: held, exitRankBuffer: 1, clusterDiversify: false);

        basket.Keys.Should().BeEquivalentTo("S0", "S1"); // strenges Ranking, S4 ausserhalb des Buffers
    }

    [Fact]
    public void ComputeBasket_ClusterDiversify_MaxEinSymbolJeCluster()
    {
        // SOL und AVAX sind beide CryptoAltL1 — mit cdiv darf nur das staerkere (SOL) rein,
        // der zweite Long-Slot geht an DOGE (CryptoMeme) statt an AVAX.
        var universe = new (string, IReadOnlyList<Candle>)[]
        {
            ("SOL-USDT", Series(0.010m)),
            ("AVAX-USDT", Series(0.008m)),
            ("DOGE-USDT", Series(0.006m)),
            ("UNI-USDT", Series(0.004m)),
        };

        var basket = MomentumBasketCalculator.ComputeBasket(universe, lookback: 30, longK: 2, shortK: 0,
            riskAdjusted: false, skip: 0, currentBasket: null, exitRankBuffer: 0, clusterDiversify: true);

        basket.Keys.Should().BeEquivalentTo("SOL-USDT", "DOGE-USDT");
    }

    [Fact]
    public void ComputeBasket_ClusterDiversify_FallbackFuelltNachRangAuf()
    {
        // Nur EIN Cluster (AltL1) verfuegbar → strikte Cluster-Regel liefert nur 1 Slot,
        // der Fallback fuellt nach Rang auf volle K auf (Exposure-Paritaet zur Baseline).
        var universe = new (string, IReadOnlyList<Candle>)[]
        {
            ("SOL-USDT", Series(0.010m)),
            ("AVAX-USDT", Series(0.008m)),
            ("NEAR-USDT", Series(0.006m)),
        };

        var basket = MomentumBasketCalculator.ComputeBasket(universe, lookback: 30, longK: 2, shortK: 0,
            riskAdjusted: false, skip: 0, currentBasket: null, exitRankBuffer: 0, clusterDiversify: true);

        basket.Keys.Should().BeEquivalentTo("SOL-USDT", "AVAX-USDT");
    }

    [Fact]
    public void ComputeBasket_NeutraleParameter_IdentischZumBasispfad()
    {
        var universe = Enumerable.Range(0, 8)
            .Select(i => ($"S{i}", (IReadOnlyList<Candle>)Series(0.008m - i * 0.002m)))
            .ToArray();

        var basePath = MomentumBasketCalculator.ComputeBasket(universe, lookback: 30, longK: 3, shortK: 3, riskAdjusted: false);
        var extended = MomentumBasketCalculator.ComputeBasket(universe, lookback: 30, longK: 3, shortK: 3,
            riskAdjusted: false, skip: 0, currentBasket: null, exitRankBuffer: 0, clusterDiversify: false);

        extended.Should().BeEquivalentTo(basePath);
    }

    [Fact]
    public void Momentum_ZuWenigKerzen_GibtNull()
    {
        var shortSeries = Series(0.01m, count: 20);
        MomentumBasketCalculator.Momentum(shortSeries, lookback: 30, riskAdjusted: false).Should().BeNull();
    }

    [Fact]
    public void Momentum_PlainRoc_IstEndDurchVergangenheit()
    {
        var s = Series(0.01m, count: 40);
        var expected = s[^1].Close / s[s.Count - 1 - 30].Close - 1m;
        var mom = MomentumBasketCalculator.Momentum(s, lookback: 30, riskAdjusted: false);
        mom.Should().BeApproximately(expected, 0.0000001m);
    }

    [Fact]
    public void Momentum_RiskAdjusted_NormalisiertUndBleibtVorzeichentreu()
    {
        var up = MomentumBasketCalculator.Momentum(Series(0.01m), lookback: 30, riskAdjusted: true);
        var down = MomentumBasketCalculator.Momentum(Series(-0.01m), lookback: 30, riskAdjusted: true);
        up.Should().NotBeNull();
        down.Should().NotBeNull();
        up!.Value.Should().BePositive();
        down!.Value.Should().BeNegative();
    }

    // ─────────── BTC-Dominanz-Spread (ComputeDominanceBasket) ───────────

    /// <summary>Flache Serie mit definiertem Kerzen-Volumen (Quote-Volumen ∝ vol × 100).</summary>
    private static List<Candle> SeriesWithVolume(decimal volume, int count = 50)
    {
        var candles = new List<Candle>();
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < count; i++)
            candles.Add(new Candle(baseTime.AddHours(4 * i), 100m, 100.1m, 99.9m, 100m, volume, baseTime.AddHours(4 * (i + 1))));
        return candles;
    }

    [Fact]
    public void ComputeDominanceBasket_LongBtc_ShortVolumenstaerksteAlts_OhneTradFiUndGold()
    {
        var universe = new (string, IReadOnlyList<Candle>)[]
        {
            ("BTC-USDT", SeriesWithVolume(9000m)),
            ("ETH-USDT", SeriesWithVolume(5000m)),
            ("SOL-USDT", SeriesWithVolume(3000m)),
            ("DOGE-USDT", SeriesWithVolume(1000m)),
            ("NCCOGOLD2USD-USDT", SeriesWithVolume(8000m)),   // TradFi-Perp → nie im Short-Korb
            ("XAUT-USDT", SeriesWithVolume(7000m)),           // tokenisiertes Gold → nie im Short-Korb
        };

        var basket = MomentumBasketCalculator.ComputeDominanceBasket(universe, longK: 1, shortK: 2);

        basket.Should().HaveCount(3);
        basket["BTC-USDT"].Should().Be(Side.Buy);
        basket["ETH-USDT"].Should().Be(Side.Sell);
        basket["SOL-USDT"].Should().Be(Side.Sell);   // Top-2 nach Volumen; DOGE faellt raus
        basket.Should().NotContainKey("NCCOGOLD2USD-USDT");
        basket.Should().NotContainKey("XAUT-USDT");
    }

    [Fact]
    public void ComputeDominanceBasket_OhneBtcAnker_LeererKorb()
    {
        // Ohne Anker waere der Rest ein net-short-Direktionaltrade → bewusst leer.
        var universe = new (string, IReadOnlyList<Candle>)[]
        {
            ("ETH-USDT", SeriesWithVolume(5000m)),
            ("SOL-USDT", SeriesWithVolume(3000m)),
        };

        var basket = MomentumBasketCalculator.ComputeDominanceBasket(universe, longK: 1, shortK: 2);

        basket.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDominanceBasket_LongK0_NurShorts_FuerDriftRefill()
    {
        // Drift-Refill: BTC-Anker wird bereits gehalten → nur Short-Slots auffuellen.
        var universe = new (string, IReadOnlyList<Candle>)[]
        {
            ("ETH-USDT", SeriesWithVolume(5000m)),
            ("SOL-USDT", SeriesWithVolume(3000m)),
            ("DOGE-USDT", SeriesWithVolume(1000m)),
        };

        var basket = MomentumBasketCalculator.ComputeDominanceBasket(universe, longK: 0, shortK: 1);

        basket.Should().HaveCount(1);
        basket["ETH-USDT"].Should().Be(Side.Sell);
    }

    [Fact]
    public void ComputeDominanceBasket_UnerreichbaresSymbol_NaechsterAltRuecktNach()
    {
        // Live-Befund 11.08.2026: bei kleinem Konto passt die Min-Order der volumenstaerksten Alts
        // nicht in den Slot — ohne Filter blieb der Slot dauerhaft Cash und die Short-Seite
        // unterinvestiert (Spread netto long). Mit Filter rueckt der naechste erreichbare Alt nach.
        var universe = new (string, IReadOnlyList<Candle>)[]
        {
            ("BTC-USDT", SeriesWithVolume(9000m)),
            ("ETH-USDT", SeriesWithVolume(5000m)),    // unerreichbar
            ("SOL-USDT", SeriesWithVolume(3000m)),
            ("DOGE-USDT", SeriesWithVolume(1000m)),
        };

        var basket = MomentumBasketCalculator.ComputeDominanceBasket(
            universe, longK: 1, shortK: 2, canTrade: s => s != "ETH-USDT");

        basket.Should().HaveCount(3);
        basket["BTC-USDT"].Should().Be(Side.Buy);
        basket.Should().NotContainKey("ETH-USDT");
        basket["SOL-USDT"].Should().Be(Side.Sell);
        basket["DOGE-USDT"].Should().Be(Side.Sell);   // rueckt in den frei gewordenen Slot nach
    }

    [Fact]
    public void ComputeDominanceBasket_OhneFilter_UnveraendertesRanking()
    {
        // Backtest-Paritaet: ohne canTrade (Backtest kennt keine Min-Order-Daten) bleibt die
        // Auswahl bit-identisch zum validierten Screen.
        var universe = new (string, IReadOnlyList<Candle>)[]
        {
            ("BTC-USDT", SeriesWithVolume(9000m)),
            ("ETH-USDT", SeriesWithVolume(5000m)),
            ("SOL-USDT", SeriesWithVolume(3000m)),
            ("DOGE-USDT", SeriesWithVolume(1000m)),
        };

        var mitNull = MomentumBasketCalculator.ComputeDominanceBasket(universe, longK: 1, shortK: 2, canTrade: null);
        var ohneParameter = MomentumBasketCalculator.ComputeDominanceBasket(universe, longK: 1, shortK: 2);

        mitNull.Should().BeEquivalentTo(ohneParameter);
        mitNull.Should().ContainKey("ETH-USDT");
    }
}
