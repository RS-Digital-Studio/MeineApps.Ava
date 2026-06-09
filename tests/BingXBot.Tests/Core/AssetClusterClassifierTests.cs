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
    // BCH/LTC sind hochkorrelierte Fork-/Payment-Coins (BCH ~0.8 zu BTC). Frueher landeten sie
    // im no-op CryptoOther → der Korrelations-Filter sah sie als unkorreliert zu BTC. Jetzt im
    // BtcMajor-Cluster (korrigierte Zuordnung).
    [InlineData("BCH-USDT", AssetCluster.CryptoBtcMajor)]
    [InlineData("LTC-USDT", AssetCluster.CryptoBtcMajor)]
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
    // BNB/XRP korrelieren in Risk-On/Off-Phasen stark mit dem breiten Alt-Markt. Frueher landeten
    // sie im no-op CryptoOther → der Filter sah sie als unkorreliert. Jetzt im AltL1-Cluster
    // (korrigierte Zuordnung).
    [InlineData("BNB-USDT")]
    [InlineData("XRP-USDT")]
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

    [Theory]
    // Tokenisierte Edelmetalle: XAUT/PAXG sind Gold-Token (preislich 1:1 Gold, nicht Crypto).
    // Frueher CryptoOther (no-op) → ein XAUT-Short teilte sich kein Budget mit Gold-Futures,
    // obwohl beide exakt dieselbe Preisquelle haben. Jetzt im TradFiCommodity-Cluster (korrigiert).
    [InlineData("XAUT-USDT")]
    [InlineData("PAXG-USDT")]
    public void Classify_TokenizedGold_GoesToTradFiCommodity(string symbol)
    {
        AssetClusterClassifier.Classify(symbol).Should().Be(AssetCluster.TradFiCommodity);
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

    // === AreCorrelated — Cross-Cluster-Korrelations-Matrix ===

    [Fact]
    public void AreCorrelated_SameCluster_True()
    {
        // Cluster-Gleichheit teilt immer ein Korrelations-Budget.
        AssetClusterClassifier.AreCorrelated(AssetCluster.CryptoBtcMajor, AssetCluster.CryptoBtcMajor)
            .Should().BeTrue();
    }

    [Fact]
    public void AreCorrelated_IndexAndStock_BothDirections_True()
    {
        // US-Equity-Klumpen: Index-Perps (NASDAQ/SP500) und Einzelaktien (MSTR/NVDA) korrelieren
        // in Risk-Off-Events ~1:1 → ein gemeinsames Budget. Muss in BEIDE Richtungen gelten.
        AssetClusterClassifier.AreCorrelated(AssetCluster.TradFiIndex, AssetCluster.TradFiStock)
            .Should().BeTrue();
        AssetClusterClassifier.AreCorrelated(AssetCluster.TradFiStock, AssetCluster.TradFiIndex)
            .Should().BeTrue();
    }

    [Theory]
    // "Other"/"CryptoOther" sind die Edge-Case-Toepfe — sie teilen NIE ein Budget, sonst landen
    // alle Unbekannten im selben Cluster. Gilt unabhaengig vom Partner-Cluster.
    [InlineData(AssetCluster.CryptoOther, AssetCluster.CryptoBtcMajor)]
    [InlineData(AssetCluster.CryptoBtcMajor, AssetCluster.CryptoOther)]
    [InlineData(AssetCluster.Other, AssetCluster.TradFiIndex)]
    [InlineData(AssetCluster.TradFiIndex, AssetCluster.Other)]
    [InlineData(AssetCluster.CryptoOther, AssetCluster.Other)]
    public void AreCorrelated_OtherClusters_NeverCorrelated(AssetCluster a, AssetCluster b)
    {
        AssetClusterClassifier.AreCorrelated(a, b).Should().BeFalse();
    }

    [Fact]
    public void AreCorrelated_DifferentNonPairedClusters_False()
    {
        // BTC-Major und Forex sind weder gleich noch ein definiertes Cross-Cluster-Paar.
        AssetClusterClassifier.AreCorrelated(AssetCluster.CryptoBtcMajor, AssetCluster.TradFiForex)
            .Should().BeFalse();
    }

    [Fact]
    public void ShareCluster_NasdaqIndexAndMstrStock_True()
    {
        // Konkretes US-Equity-Klumpen-Szenario: NASDAQ-100-Index-Perp + MicroStrategy-Aktie.
        // Index ↔ Stock teilen ein Budget → ShareCluster muss true liefern.
        AssetClusterClassifier.ShareCluster("NCSINASDAQ1002USD-USDT", "NCSKMSTR2USD-USDT")
            .Should().BeTrue();
    }

    [Fact]
    public void ShareCluster_BchAndLtc_True()
    {
        // Beide nach der Korrektur im BtcMajor-Cluster → teilen ein Budget.
        AssetClusterClassifier.ShareCluster("BCH-USDT", "LTC-USDT").Should().BeTrue();
    }

    [Fact]
    public void ShareCluster_XautAndGoldFuture_True()
    {
        // Gold-Token (XAUT) und Gold-Future (NCCO) sind beide TradFiCommodity → ein Budget.
        AssetClusterClassifier.ShareCluster("XAUT-USDT", "NCCOGOLD2USD-USDT").Should().BeTrue();
    }
}
