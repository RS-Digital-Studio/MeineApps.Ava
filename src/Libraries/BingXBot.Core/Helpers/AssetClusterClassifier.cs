using BingXBot.Core.Enums;

namespace BingXBot.Core.Helpers;

/// <summary>
/// Phase 18 / A4 — Statische Asset-Cluster-Map fuer Korrelations-basierte Risiko-Limits.
///
/// Hintergrund: Crypto-Majors korrelieren historisch zu 0.7-0.9. Drei gleichzeitige Long-Positionen
/// auf BTC/ETH/SOL sind effektiv ein 3× gehebelter BTC-Trade — bei einem Flash-Crash gehen alle
/// drei SLs gleichzeitig hit. <see cref="RiskSettings.MaxCorrelatedExposurePercent"/> setzt eine
/// Obergrenze fuer die Summe der Margins innerhalb eines Clusters in % der Wallet-Balance.
///
/// Defaults sind konservativ (gut bekannte Majors + grobe Buckets); Edge-Fall-Symbole landen im
/// Cluster <see cref="AssetCluster.Other"/> und teilen sich kein gemeinsames Risiko-Budget.
/// Cluster sind statisch; eine dynamische Korrelations-Berechnung (30 d Rolling-Pearson) ist als
/// Folgeschritt geplant.
/// </summary>
public static class AssetClusterClassifier
{
    private const StringComparison IgnoreCase = StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Liefert das Cluster fuer ein BingX-Symbol. TradFi-Klassifikation hat Vorrang vor Crypto-
    /// Cluster-Heuristik (TradFi-Korrelation wird ueber Asset-Klassen-Buckets, nicht Pearson, modelliert).
    /// </summary>
    public static AssetCluster Classify(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return AssetCluster.Other;

        // TradFi: 1:1 mapping zu Markt-Kategorien (Forex/Index/Commodity/Stock).
        var tradFiCategory = SymbolClassifier.Classify(symbol);
        switch (tradFiCategory)
        {
            case MarketCategory.Forex: return AssetCluster.TradFiForex;
            case MarketCategory.Index: return AssetCluster.TradFiIndex;
            case MarketCategory.Commodity: return AssetCluster.TradFiCommodity;
            case MarketCategory.Stock: return AssetCluster.TradFiStock;
        }

        // Crypto-Buckets — Symbol-Prefix-basiert (BingX-Format z.B. "BTC-USDT").
        var asset = ExtractBaseAsset(symbol);

        // BTC-Cluster: BTC + WBTC + BTC-Forks die quasi 1:1 zu BTC laufen.
        if (Equals(asset, "BTC", "WBTC", "BTCB", "BTCDOM"))
            return AssetCluster.CryptoBtcMajor;

        // ETH-Cluster: ETH + alle Liquid-Staking-Derivate + ETH-Layer-2-Tokens die stark mit ETH korrelieren.
        if (Equals(asset, "ETH", "WETH", "STETH", "RETH", "CBETH", "WSTETH"))
            return AssetCluster.CryptoEthMajor;

        // Alt-L1-Cluster: konkurrierende L1-Chains, korrelieren stark untereinander in Risk-On/Off.
        if (Equals(asset, "SOL", "AVAX", "ADA", "DOT", "NEAR", "ATOM", "ALGO", "FTM", "TRX", "TON", "APT", "SUI", "INJ", "SEI", "TIA"))
            return AssetCluster.CryptoAltL1;

        // Alt-DeFi-Cluster: DeFi-Bluechips + Lending/DEX-Tokens.
        if (Equals(asset, "UNI", "AAVE", "LINK", "MKR", "SNX", "COMP", "CRV", "LDO", "GMX", "DYDX", "1INCH", "BAL", "SUSHI"))
            return AssetCluster.CryptoAltDefi;

        // Meme-Cluster: hochvolatile Meme-Coins, hochkorreliert in Hype-Phasen.
        if (Equals(asset, "DOGE", "SHIB", "PEPE", "FLOKI", "WIF", "BONK", "MEME", "BABYDOGE", "POPCAT", "TURBO", "BOME"))
            return AssetCluster.CryptoMeme;

        // Stablecoin-Pair-Cluster: USDC/USDT/DAI gegen USDT — extrem niedriges, aber gleichgerichtetes Tail-Risk.
        if (Equals(asset, "USDC", "DAI", "BUSD", "TUSD", "FDUSD", "USDP"))
            return AssetCluster.CryptoStablePair;

        return AssetCluster.CryptoOther;
    }

    /// <summary>True wenn das Symbol einen Korrelations-Cluster mit mindestens einem anderen Symbol teilt.</summary>
    public static bool ShareCluster(string symbolA, string symbolB)
    {
        if (string.IsNullOrEmpty(symbolA) || string.IsNullOrEmpty(symbolB)) return false;
        var clA = Classify(symbolA);
        var clB = Classify(symbolB);
        // "Other"-Cluster zaehlen NICHT als geteilt (sonst landen alle Edge-Cases im selben Topf).
        if (clA == AssetCluster.CryptoOther || clA == AssetCluster.Other) return false;
        if (clB == AssetCluster.CryptoOther || clB == AssetCluster.Other) return false;
        return clA == clB;
    }

    /// <summary>Liefert das Base-Asset eines BingX-Symbols (z.B. "BTC" aus "BTC-USDT" oder "Ncco1OilWti2USD-USDT" → "OILWTI").</summary>
    private static string ExtractBaseAsset(string symbol)
    {
        // BingX-Format: "BTC-USDT" → "BTC". TradFi-Symbole sind oben bereits separat geclustert.
        var dash = symbol.IndexOf('-');
        return dash > 0 ? symbol[..dash] : symbol;
    }

    private static bool Equals(string asset, params string[] candidates)
    {
        foreach (var c in candidates)
            if (string.Equals(asset, c, IgnoreCase)) return true;
        return false;
    }
}

/// <summary>
/// Phase 18 / A4 — Cluster-Buckets fuer korrelations-basierte Exposure-Limits.
/// Innerhalb eines Clusters teilen sich Symbole das <see cref="RiskSettings.MaxCorrelatedExposurePercent"/>-Budget.
/// </summary>
public enum AssetCluster
{
    /// <summary>Edge-Case / Unbekannt — kein gemeinsames Limit.</summary>
    Other = 0,
    CryptoBtcMajor,
    CryptoEthMajor,
    CryptoAltL1,
    CryptoAltDefi,
    CryptoMeme,
    CryptoStablePair,
    /// <summary>Kein bekannter Bucket-Match (kein gemeinsames Limit).</summary>
    CryptoOther,
    TradFiForex,
    TradFiIndex,
    TradFiCommodity,
    TradFiStock
}
