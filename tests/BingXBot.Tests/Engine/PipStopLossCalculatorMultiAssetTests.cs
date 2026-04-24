using BingXBot.Core.Enums;
using BingXBot.Engine.Risk;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Multi-Asset-Tests für PipStopLossCalculator (v1.2.6, 19.04.2026):
/// - Silber-Bugfix: XAG hat Pip-Wert 0.01 (nicht 0.1 wie Gold)
/// - Gold-Anpassung: 25/35 Pips (statt 15/20) für M15-Noise
/// - 0.15%-Floor in CalculateBookStopLoss
/// - TradFi-Pip-Scale ignoriert pipScale &lt; 1.0
/// </summary>
public class PipStopLossCalculatorMultiAssetTests
{
    // ═══════════════════════════════════════════════════════════════
    // FIX 1: Silber-Pip-Wert (BUG-Fix)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("NCCOXAG2USD-USDT")]
    [InlineData("SILVER-USDT")]
    [InlineData("XAGUSD")]
    public void Silver_PipWert_001_NichtMehrBuggy(string symbol)
    {
        // Silber @ 30 USD/oz, 15 Pips Single-Trade
        // Vor v1.2.6: 15 × 0.1 = 1.5 USD = 5% SL → BUG
        // Nach v1.2.6: 15 × 0.01 = 0.15 USD = 0.5% SL → OK
        var slDistance = PipStopLossCalculator.CalculateSlDistance(
            symbol, MarketCategory.Commodity, entryPrice: 30m, isSingleTrade: true);

        slDistance.Should().Be(0.15m, "15 Pips × 0.01 = 0.15 bei Silber @ 30");
        var slPercent = slDistance / 30m;
        slPercent.Should().BeApproximately(0.005m, 0.0001m, "0.5% SL ist handelbar");
    }

    // ═══════════════════════════════════════════════════════════════
    // FIX 2: Gold-Pip-Count (25/35 statt 15/20)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("NCCOGOLD2USD-USDT")]
    [InlineData("GOLD-USDT")]
    [InlineData("XAUUSD")]
    public void Gold_PipCount_25Single_35Multi(string symbol)
    {
        // Gold @ 2600 USD/oz
        // Single: 25 Pips × 0.1 = 2.5 USD = 0.096%
        var slSingle = PipStopLossCalculator.CalculateSlDistance(
            symbol, MarketCategory.Commodity, entryPrice: 2600m, isSingleTrade: true);
        slSingle.Should().Be(2.5m);

        // Multi (Additional): 35 Pips × 0.1 = 3.5 USD = 0.135%
        var slMulti = PipStopLossCalculator.CalculateSlDistance(
            symbol, MarketCategory.Commodity, entryPrice: 2600m, isSingleTrade: false);
        slMulti.Should().Be(3.5m);
    }

    [Fact]
    public void Gold_BookStopLoss_FloorGreift_BeiKleinemRetracement()
    {
        // Gold @ 2600, Pip-Cap = 25 × 0.1 = 2.5 USD = 0.096% — UNTER 0.15% Floor
        // Floor sollte SL auf 0.15% × 2600 = 3.9 USD weiten
        var sl = PipStopLossCalculator.CalculateBookStopLoss(
            "GOLD-USDT", MarketCategory.Commodity,
            entryPrice: 2600m, isLong: true,
            fib786: 2580m,    // 78.6% liegt 20 USD darunter
            point0: 2500m,    // weit weg
            isSingleTrade: true);

        var slDistance = 2600m - sl;
        slDistance.Should().BeGreaterThanOrEqualTo(2600m * 0.0015m, "Floor 0.15% greift");
        slDistance.Should().BeLessThanOrEqualTo(20m, "78.6%-Retracement ist gleichzeitig Cap");
    }

    // ═══════════════════════════════════════════════════════════════
    // FIX 3: 0.15%-Floor in CalculateBookStopLoss
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Forex_BookStopLoss_FloorWirktAuchAufKleinenStrecken()
    {
        // EURUSD @ 1.08, Pip-Cap = 15 × 0.0001 = 0.0015 = 0.139%
        // 78.6% sehr eng (1.0795) → Floor 0.15% × 1.08 = 0.00162 sollte greifen
        var sl = PipStopLossCalculator.CalculateBookStopLoss(
            "EURUSD-USDT", MarketCategory.Forex,
            entryPrice: 1.08m, isLong: true,
            fib786: 1.0795m,
            point0: 1.07m,
            isSingleTrade: true);

        var slDistance = 1.08m - sl;
        slDistance.Should().BeGreaterThanOrEqualTo(1.08m * 0.0015m - 0.00001m, "Floor 0.15% greift bei kleinem Pip-Cap");
    }

    [Fact]
    public void Crypto_BookStopLoss_FloorIgnoriertWennPipCapSchonGroß()
    {
        // BTC @ 76000, Pip-Cap = 100 × 7.6 = 760 = 1.0% — DEUTLICH über 0.15% Floor
        // SL sollte vom 78.6% kommen, nicht vom Floor
        var sl = PipStopLossCalculator.CalculateBookStopLoss(
            "BTC-USDT", MarketCategory.Crypto,
            entryPrice: 76000m, isLong: true,
            fib786: 75500m,
            point0: 75000m,
            isSingleTrade: false);

        var slDistance = 76000m - sl;
        slDistance.Should().BeLessThanOrEqualTo(760m, "Pip-Cap = 100 Pips bleibt Obergrenze");
    }

    // ═══════════════════════════════════════════════════════════════
    // FIX 4: TradFi-Pip-Scale ignoriert pipScale < 1.0
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("EURUSD-USDT", MarketCategory.Forex)]
    [InlineData("NCSITSLA2USD-USDT", MarketCategory.Stock)]
    [InlineData("NCSINASDAQ100-USDT", MarketCategory.Index)]
    [InlineData("WTI-USDT", MarketCategory.Commodity)]
    public void TradFi_IgnoriertPipScaleUnter1(string symbol, MarketCategory category)
    {
        // Alle TradFi-Kategorien sollten pipScale=0.75 IGNORIEREN
        var slMitScale = PipStopLossCalculator.CalculateSlDistance(
            symbol, category, entryPrice: 100m, isSingleTrade: true, pipScale: 0.75m);
        var slOhneScale = PipStopLossCalculator.CalculateSlDistance(
            symbol, category, entryPrice: 100m, isSingleTrade: true, pipScale: 1.0m);

        slMitScale.Should().Be(slOhneScale, $"{category}: pipScale<1.0 wird ignoriert");
    }

    [Fact]
    public void Crypto_PipScaleWirdAngewandt()
    {
        // Crypto SOLLTE pipScale=0.75 anwenden (M15-Reduzierung)
        var slMitScale = PipStopLossCalculator.CalculateSlDistance(
            "BTC-USDT", MarketCategory.Crypto, entryPrice: 76000m, isSingleTrade: true, pipScale: 0.75m);
        var slOhneScale = PipStopLossCalculator.CalculateSlDistance(
            "BTC-USDT", MarketCategory.Crypto, entryPrice: 76000m, isSingleTrade: true, pipScale: 1.0m);

        slMitScale.Should().BeApproximately(slOhneScale * 0.75m, 0.01m, "Crypto skaliert mit pipScale");
    }

    [Fact]
    public void TradFi_PipScaleÜber1_WirdÜbernommen()
    {
        // Wenn jemand pipScale=1.5 für TradFi setzt (z.B. Wochenend-Buffer), soll das greifen
        var slMitScale = PipStopLossCalculator.CalculateSlDistance(
            "EURUSD-USDT", MarketCategory.Forex, entryPrice: 1.08m, isSingleTrade: true, pipScale: 1.5m);
        var slOhneScale = PipStopLossCalculator.CalculateSlDistance(
            "EURUSD-USDT", MarketCategory.Forex, entryPrice: 1.08m, isSingleTrade: true, pipScale: 1.0m);

        slMitScale.Should().BeApproximately(slOhneScale * 1.5m, 0.0001m, "pipScale > 1.0 wird auch für TradFi angewandt");
    }

    // ═══════════════════════════════════════════════════════════════
    // Cross-Asset Sanity-Tabelle (alle Kategorien sinnvolles SL%)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("BTC-USDT", MarketCategory.Crypto, 76000.0, 1.00)]    // 100 Pips × 7.6 = 760 = 1.0%
    [InlineData("ETH-USDT", MarketCategory.Crypto, 3200.0, 1.00)]      // 100 Pips × 0.32 = 32 = 1.0%
    [InlineData("NCCOWTI2USD-USDT", MarketCategory.Commodity, 75.0, 0.53)]      // Öl 40 × 0.01 = 0.4 = 0.53%
    [InlineData("NCSINAS100-USDT", MarketCategory.Index, 20000.0, 0.20)]         // 40 × 1 = 40 = 0.2%
    [InlineData("NCSISP500-USDT", MarketCategory.Index, 5500.0, 0.73)]          // 40 × 1 = 40 = 0.727%
    [InlineData("NCFXEURUSD-USDT", MarketCategory.Forex, 1.08, 0.15)]            // v1.2.7: 15 × 1.08 × 0.0001 = 0.00162 = 0.15% (prozentualer Pip + 0.15%-Floor)
    [InlineData("NCFXUSDJPY-USDT", MarketCategory.Forex, 150.0, 0.15)]           // v1.2.7: 15 × 150 × 0.0001 = 0.225 = 0.15% (prozentualer Pip statt fixe 0.01)
    [InlineData("NCSKTSLA-USDT", MarketCategory.Stock, 250.0, 0.20)]             // v1.2.6: 40 × 250 × 0.00005 = 0.5 = 0.20%
    [InlineData("NCSKNVDA-USDT", MarketCategory.Stock, 140.0, 0.20)]             // v1.2.6: 40 × 140 × 0.00005 = 0.28 = 0.20%
    [InlineData("NCSKBRK-USDT", MarketCategory.Stock, 600.0, 0.20)]              // v1.2.6: 40 × 600 × 0.00005 = 1.2 = 0.20% (proportional)
    public void CrossAsset_SlIstImSinnvollenBereich(string symbol, MarketCategory category, double entryPrice, double expectedPercent)
    {
        var sl = PipStopLossCalculator.CalculateSlDistance(symbol, category, (decimal)entryPrice, isSingleTrade: true);
        var slPercent = (double)(sl / (decimal)entryPrice) * 100;

        // ±10% Toleranz auf erwarteten Wert
        slPercent.Should().BeApproximately(expectedPercent, expectedPercent * 0.1, $"{symbol} @ {entryPrice} → {expectedPercent:F2}% SL");
    }

    // ═══════════════════════════════════════════════════════════════
    // FIX 5 (v1.2.6 Floor-Logik): fib786 zwischen Pip-Cap und Floor wird NICHT überschrieben
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void BookSL_EngeresFib786_alsPipCap_BleibtErhalten()
    {
        // EURUSD @ 1.08, Pip-Cap roh = 0.139%, Floor = 0.15%, fib786 = 0.18% (weiter weg)
        // Erwartet: SL = Pip-Cap (näher am Entry, dann Floor 0.15%)
        var sl = PipStopLossCalculator.CalculateBookStopLoss(
            "EURUSD-USDT", MarketCategory.Forex,
            entryPrice: 1.08m, isLong: true,
            fib786: 1.08m - 0.0018m, // -0.18%, weiter weg
            point0: 1.07m,
            isSingleTrade: true);

        var slDistance = 1.08m - sl;
        // Pip-Cap (0.139%) < Floor (0.15%) → Floor greift auf finalen SL
        slDistance.Should().BeGreaterThanOrEqualTo(1.08m * 0.0015m - 0.0001m);
    }

    [Fact]
    public void BookSL_Fib786_GenauZwischenPipCapUndFloor_PipCapGewinntNichtFib786()
    {
        // EURUSD @ 1.08, fib786 sehr eng (0.10%), Pip-Cap roh = 0.139%, Floor = 0.15%
        // Erwartet: fib786 (näher) gewinnt → Floor weitet auf 0.15%
        var sl = PipStopLossCalculator.CalculateBookStopLoss(
            "EURUSD-USDT", MarketCategory.Forex,
            entryPrice: 1.08m, isLong: true,
            fib786: 1.08m - 0.0010m, // -0.10%, näher am Entry als Pip-Cap
            point0: 1.07m,
            isSingleTrade: true);

        var slDistance = 1.08m - sl;
        // Floor 2 weitet auf 0.15%
        slDistance.Should().BeApproximately(1.08m * 0.0015m, 0.0001m);
    }

    // ═══════════════════════════════════════════════════════════════
    // FIX 6 (v1.2.6 TradingHoursFilter): Forex Sonntag 22:00 UTC
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(2026, 4, 19, 21, 30, false)] // Sonntag 21:30 UTC: Forex zu
    [InlineData(2026, 4, 19, 22, 0, true)]   // Sonntag 22:00 UTC: Forex auf (Sydney-Open)
    [InlineData(2026, 4, 19, 23, 30, true)]  // Sonntag 23:30 UTC: Forex auf
    [InlineData(2026, 4, 18, 12, 0, false)]  // Samstag 12:00 UTC: Forex zu
    public void Forex_Sonntag_AbZweiUndZwanzigUhrUtcOffen(int y, int m, int d, int h, int mi, bool expected)
    {
        var time = new DateTime(y, m, d, h, mi, 0, DateTimeKind.Utc);
        var result = BingXBot.Engine.Filters.TradingHoursFilter.IsMarketOpen("NCFXEURUSD2USD-USDT", time);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(2026, 4, 19, 22, 0)]  // Sonntag 22:00 UTC: Stock noch zu
    [InlineData(2026, 4, 19, 23, 30)] // Sonntag 23:30 UTC: Stock noch zu
    public void Stock_Sonntag_BleibtZu(int y, int m, int d, int h, int mi)
    {
        var time = new DateTime(y, m, d, h, mi, 0, DateTimeKind.Utc);
        var result = BingXBot.Engine.Filters.TradingHoursFilter.IsMarketOpen("NCSKTSLA2USD-USDT", time);
        result.Should().BeFalse("Stock öffnet erst Montag");
    }
}
