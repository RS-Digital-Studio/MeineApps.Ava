using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Core;

public class SymbolClassifierTests
{
    [Theory]
    [InlineData("BTC-USDT", MarketCategory.Crypto)]
    [InlineData("ETH-USDT", MarketCategory.Crypto)]
    [InlineData("SOL-USDT", MarketCategory.Crypto)]
    [InlineData("DOGE-USDT", MarketCategory.Crypto)]
    [InlineData("NCCOGOLD2USD-USDT", MarketCategory.Commodity)]
    [InlineData("NCCOXAG2USD-USDT", MarketCategory.Commodity)]
    [InlineData("NCCO1OILWTI2USD-USDT", MarketCategory.Commodity)]
    [InlineData("NCSINASDAQ1002USD-USDT", MarketCategory.Index)]
    [InlineData("NCSISP5002USD-USDT", MarketCategory.Index)]
    [InlineData("NCFXEUR2USD-USDT", MarketCategory.Forex)]
    [InlineData("NCFXGBP2USD-USDT", MarketCategory.Forex)]
    [InlineData("NCSKTSLA2USD-USDT", MarketCategory.Stock)]
    [InlineData("NCSKNVDA2USD-USDT", MarketCategory.Stock)]
    public void Classify_ErkenntKategorieKorrekt(string symbol, MarketCategory expected)
    {
        SymbolClassifier.Classify(symbol).Should().Be(expected);
    }

    [Theory]
    [InlineData("BTC-USDT", false)]
    [InlineData("ETH-USDT", false)]
    [InlineData("NCCOGOLD2USD-USDT", true)]
    [InlineData("NCSKTSLA2USD-USDT", true)]
    public void IsTradFi_ErkenntNCPrefix(string symbol, bool expected)
    {
        SymbolClassifier.IsTradFi(symbol).Should().Be(expected);
    }

    [Theory]
    [InlineData("NCCO724OILWTI2USD-USDT", true)]
    [InlineData("NCSI724NASDAQ1002USD-USDT", true)]
    [InlineData("NCCOGOLD2USD-USDT", false)]
    [InlineData("BTC-USDT", false)]
    public void Is24x7_Erkennt724Varianten(string symbol, bool expected)
    {
        SymbolClassifier.Is24x7(symbol).Should().Be(expected);
    }

    [Theory]
    [InlineData("NCCO724OILWTI2USD-USDT", true)]     // 7x24-Öl seit 13.04.2026 wieder offen
    [InlineData("NCCOGOLD2USD-USDT", true)]            // Gold offen
    [InlineData("BTC-USDT", true)]                      // Krypto immer
    [InlineData("NCFXEUR2USD-USDT", true)]             // EUR/USD offen
    [InlineData("NCFXUSD2HKD-USDT", false)]            // Exotic Forex (HKD) gesperrt
    [InlineData("NCFXUSD2SGD-USDT", false)]            // Exotic Forex (SGD) gesperrt
    public void IsApiTradeable_PrüftGesperrteSymbole(string symbol, bool expected)
    {
        SymbolClassifier.IsApiTradeable(symbol).Should().Be(expected);
    }

    [Theory]
    // BingX liefert Uppercase, aber externe Quellen (Doku, UI) verwenden oft Mixed-Case.
    // Alle Classifier-Methoden müssen case-insensitive arbeiten — sonst landet z.B.
    // "Ncco1Oilwti2USD-USDT" fälschlich im Krypto-Pool.
    [InlineData("ncco1oilwti2usd-usdt", MarketCategory.Commodity)]
    [InlineData("Ncco1Oilwti2USD-USDT", MarketCategory.Commodity)]
    [InlineData("ncsiSP5002usd-usdt", MarketCategory.Index)]
    [InlineData("ncfxeur2usd-usdt", MarketCategory.Forex)]
    [InlineData("ncskTSLA2USD-USDT", MarketCategory.Stock)]
    public void Classify_FunktioniertCaseInsensitive(string symbol, MarketCategory expected)
    {
        SymbolClassifier.Classify(symbol).Should().Be(expected);
    }

    [Theory]
    // Echte BingX-Symbole aus dem Live-API-Snapshot (13.04.2026)
    [InlineData("NCCOGOLD2USD-USDT", MarketCategory.Commodity)]
    [InlineData("NCCO1OILWTI2USD-USDT", MarketCategory.Commodity)]
    [InlineData("NCCO7241OILWTI2USD-USDT", MarketCategory.Commodity)]
    [InlineData("NCCO724OILBRENT2USD-USDT", MarketCategory.Commodity)]
    [InlineData("NCCOXAG2USD-USDT", MarketCategory.Commodity)]
    [InlineData("NCCOPALLADIUM2USD-USDT", MarketCategory.Commodity)]
    [InlineData("NCCONATURALGAS2USD-USDT", MarketCategory.Commodity)]
    [InlineData("NCCOHEATINGOIL2USD-USDT", MarketCategory.Commodity)]
    [InlineData("NCCOGASOLINE2USD-USDT", MarketCategory.Commodity)]
    [InlineData("NCSISP5002USD-USDT", MarketCategory.Index)]
    [InlineData("NCSI724SP5002USD-USDT", MarketCategory.Index)]
    [InlineData("NCSINASDAQ1002USD-USDT", MarketCategory.Index)]
    [InlineData("NCSIDOWJONES2USD-USDT", MarketCategory.Index)]
    [InlineData("NCSIRUSSELL20002USD-USDT", MarketCategory.Index)]
    [InlineData("NCSINIKKEI2252USD-USDT", MarketCategory.Index)]
    [InlineData("NCSIDXY2USD-USDT", MarketCategory.Index)]
    [InlineData("NCSIEWJ2USD-USDT", MarketCategory.Index)]
    [InlineData("NCFXEUR2GBP-USDT", MarketCategory.Forex)]
    [InlineData("NCFXUSD2JPY-USDT", MarketCategory.Forex)]
    [InlineData("NCFXUSDBRL2USD-USDT", MarketCategory.Forex)]
    [InlineData("NCSKASML2USD-USDT", MarketCategory.Stock)]
    [InlineData("NCSKMSFT2USD-USDT", MarketCategory.Stock)]
    [InlineData("NCSKHOOD2USD-USDT", MarketCategory.Stock)]
    public void Classify_RealistischeBingXSymbole_Erkennung(string symbol, MarketCategory expected)
    {
        SymbolClassifier.Classify(symbol).Should().Be(expected);
    }

    [Fact]
    public void GetDisplayName_KryptoBleibtUnverändert()
    {
        SymbolClassifier.GetDisplayName("BTC-USDT").Should().Be("BTC-USDT");
    }

    [Fact]
    public void GetDisplayName_GoldWirdVereinfacht()
    {
        SymbolClassifier.GetDisplayName("NCCOGOLD2USD-USDT").Should().Be("GOLD");
    }

    [Fact]
    public void GetDisplayName_NasdaqWirdVereinfacht()
    {
        SymbolClassifier.GetDisplayName("NCSINASDAQ1002USD-USDT").Should().Be("NASDAQ100");
    }

    [Fact]
    public void GetDisplayName_TeslaWirdVereinfacht()
    {
        SymbolClassifier.GetDisplayName("NCSKTSLA2USD-USDT").Should().Be("TSLA");
    }

    [Fact]
    public void GetCategoryDisplayName_AlleKategorien()
    {
        SymbolClassifier.GetCategoryDisplayName(MarketCategory.Crypto).Should().Be("Krypto");
        SymbolClassifier.GetCategoryDisplayName(MarketCategory.Commodity).Should().Be("Rohstoffe");
        SymbolClassifier.GetCategoryDisplayName(MarketCategory.Index).Should().Be("Indices");
        SymbolClassifier.GetCategoryDisplayName(MarketCategory.Forex).Should().Be("Forex");
        SymbolClassifier.GetCategoryDisplayName(MarketCategory.Stock).Should().Be("Aktien");
    }
}
