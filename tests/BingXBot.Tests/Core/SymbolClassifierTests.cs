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
    [InlineData("NCCO724OILWTI2USD-USDT", false)]   // 7x24-Öl gesperrt
    [InlineData("NCCOGOLD2USD-USDT", true)]           // Gold offen
    [InlineData("BTC-USDT", true)]                     // Krypto immer
    [InlineData("NCFXEUR2USD-USDT", true)]            // EUR/USD offen
    public void IsApiTradeable_PrüftGesperrteSymbole(string symbol, bool expected)
    {
        SymbolClassifier.IsApiTradeable(symbol).Should().Be(expected);
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
