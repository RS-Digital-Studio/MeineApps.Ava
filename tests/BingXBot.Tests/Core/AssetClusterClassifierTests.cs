using BingXBot.Core.Helpers;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Core;

// Phase 18 / A4 — Cluster-Map fuer Korrelations-Filter (RiskManager).
public class AssetClusterClassifierTests
{
    [Theory]
    [InlineData("BTC-USDT", AssetCluster.CryptoBtcMajor)]
    [InlineData("WBTC-USDT", AssetCluster.CryptoBtcMajor)]
    [InlineData("BTCB-USDT", AssetCluster.CryptoBtcMajor)]
    public void Classify_BtcMajors_GoToBtcCluster(string symbol, AssetCluster expected)
    {
        AssetClusterClassifier.Classify(symbol).Should().Be(expected);
    }

    [Theory]
    [InlineData("ETH-USDT", AssetCluster.CryptoEthMajor)]
    [InlineData("WETH-USDT", AssetCluster.CryptoEthMajor)]
    [InlineData("STETH-USDT", AssetCluster.CryptoEthMajor)]
    public void Classify_EthMajors_GoToEthCluster(string symbol, AssetCluster expected)
    {
        AssetClusterClassifier.Classify(symbol).Should().Be(expected);
    }

    [Theory]
    [InlineData("SOL-USDT")]
    [InlineData("AVAX-USDT")]
    [InlineData("ADA-USDT")]
    [InlineData("DOT-USDT")]
    [InlineData("APT-USDT")]
    [InlineData("SUI-USDT")]
    public void Classify_AltL1s_GoToAltL1Cluster(string symbol)
    {
        AssetClusterClassifier.Classify(symbol).Should().Be(AssetCluster.CryptoAltL1);
    }

    [Theory]
    [InlineData("UNI-USDT")]
    [InlineData("AAVE-USDT")]
    [InlineData("LINK-USDT")]
    [InlineData("CRV-USDT")]
    public void Classify_DefiBlueChips_GoToDefiCluster(string symbol)
    {
        AssetClusterClassifier.Classify(symbol).Should().Be(AssetCluster.CryptoAltDefi);
    }

    [Theory]
    [InlineData("DOGE-USDT")]
    [InlineData("SHIB-USDT")]
    [InlineData("PEPE-USDT")]
    [InlineData("WIF-USDT")]
    public void Classify_Memes_GoToMemeCluster(string symbol)
    {
        AssetClusterClassifier.Classify(symbol).Should().Be(AssetCluster.CryptoMeme);
    }

    [Theory]
    [InlineData("RANDOMCOIN-USDT")]
    [InlineData("UNKNOWN-USDT")]
    [InlineData("XYZ-USDT")]
    public void Classify_UnknownAltcoins_GoToCryptoOther(string symbol)
    {
        // CryptoOther = "kein Cluster bekannt" — nicht in den Korrelations-Topf einrechnen.
        AssetClusterClassifier.Classify(symbol).Should().Be(AssetCluster.CryptoOther);
    }

    [Theory]
    [InlineData("NCFXEURUSD-USDT", AssetCluster.TradFiForex)]
    [InlineData("NCSIDAX40-USDT", AssetCluster.TradFiIndex)]
    [InlineData("NCCOOILWTI-USDT", AssetCluster.TradFiCommodity)]
    [InlineData("NCSKAAPL-USDT", AssetCluster.TradFiStock)]
    public void Classify_TradFi_HasOwnCluster(string symbol, AssetCluster expected)
    {
        AssetClusterClassifier.Classify(symbol).Should().Be(expected);
    }

    [Fact]
    public void Classify_CaseInsensitive()
    {
        AssetClusterClassifier.Classify("btc-usdt").Should().Be(AssetCluster.CryptoBtcMajor);
        AssetClusterClassifier.Classify("Eth-Usdt").Should().Be(AssetCluster.CryptoEthMajor);
    }

    [Fact]
    public void Classify_EmptySymbol_ReturnsOther()
    {
        AssetClusterClassifier.Classify(string.Empty).Should().Be(AssetCluster.Other);
    }

    [Fact]
    public void ShareCluster_TwoBtcMajors_True()
    {
        AssetClusterClassifier.ShareCluster("BTC-USDT", "WBTC-USDT").Should().BeTrue();
    }

    [Fact]
    public void ShareCluster_BtcAndEth_False()
    {
        AssetClusterClassifier.ShareCluster("BTC-USDT", "ETH-USDT").Should().BeFalse();
    }

    [Fact]
    public void ShareCluster_TwoUnknownAltcoins_False()
    {
        // CryptoOther teilt KEIN Cluster — sonst landen alle Unbekannten im selben Topf.
        AssetClusterClassifier.ShareCluster("RANDOMCOIN-USDT", "UNKNOWN-USDT").Should().BeFalse();
    }

    [Fact]
    public void ShareCluster_CryptoAndTradFi_False()
    {
        AssetClusterClassifier.ShareCluster("BTC-USDT", "NCFXEURUSD-USDT").Should().BeFalse();
    }
}
