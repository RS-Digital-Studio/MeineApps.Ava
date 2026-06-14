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

        // Tokenisierte Edelmetalle (XAUT/PAXG = Gold-Token): preislich 1:1 Gold, nicht Crypto.
        // Vorher CryptoOther (no-op) → ein XAUT-Short teilte sich kein Budget mit Gold-Futures,
        // obwohl beide exakt dieselbe Preisquelle haben.
        if (Equals(asset, "XAUT", "PAXG"))
            return AssetCluster.TradFiCommodity;

        // BTC-Cluster: BTC + WBTC + BTC-Forks die quasi 1:1 zu BTC laufen.
        // BCH/LTC: hochkorrelierte Fork-/Payment-Coins (BCH ~0.8 zu BTC) — vorher no-op CryptoOther.
        if (Equals(asset, "BTC", "WBTC", "BTCB", "BTCDOM", "BCH", "LTC"))
            return AssetCluster.CryptoBtcMajor;

        // ETH-Cluster: ETH + alle Liquid-Staking-Derivate + ETH-Layer-2-Tokens die stark mit ETH korrelieren.
        if (Equals(asset, "ETH", "WETH", "STETH", "RETH", "CBETH", "WSTETH"))
            return AssetCluster.CryptoEthMajor;

        // Alt-L1 / moderne Large-Cap-Alts: konkurrierende L1-Chains + neuere High-Beta-Large-Caps
        // (L1/Infra/AI/RWA). Korrelieren in Risk-On/Off-Phasen stark untereinander und mit dem
        // breiten Alt-Markt — bei einem Flash-Crash fallen sie gemeinsam. Frueher landeten HYPE/TAO/
        // WLD/ENA/ONDO/FET u.a. im no-op CryptoOther → der Korrelations-Filter sah sie als unkorreliert
        // (Klumpenrisiko bei mehreren gleichzeitigen Alt-Longs auf kleinem Konto).
        if (Equals(asset, "SOL", "AVAX", "ADA", "DOT", "NEAR", "ATOM", "ALGO", "FTM", "TRX", "TON", "APT", "SUI", "INJ", "SEI", "TIA",
                          "HYPE", "TAO", "WLD", "ENA", "ONDO", "FET", "RENDER", "RNDR", "ZRO", "ARB", "OP", "STRK", "ASTER", "KAS", "JTO", "JUP",
                          "BNB", "XRP"))
            return AssetCluster.CryptoAltL1;

        // Alt-DeFi-Cluster: DeFi-Bluechips + Lending/DEX-/Perp-Plattform-Tokens.
        if (Equals(asset, "UNI", "AAVE", "LINK", "MKR", "SNX", "COMP", "CRV", "LDO", "GMX", "DYDX", "1INCH", "BAL", "SUSHI",
                          "PENDLE", "ETHFI", "ENS", "AERO"))
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
        return AreCorrelated(Classify(symbolA), Classify(symbolB));
    }

    /// <summary>
    /// True wenn zwei Cluster sich ein Korrelations-Budget teilen. Cluster-Gleichheit plus
    /// definierte Cross-Cluster-Paare; "Other"-Cluster zaehlen NIE als geteilt (sonst landen
    /// alle Edge-Cases im selben Topf).
    /// </summary>
    public static bool AreCorrelated(AssetCluster clA, AssetCluster clB)
    {
        if (clA == AssetCluster.CryptoOther || clA == AssetCluster.Other) return false;
        if (clB == AssetCluster.CryptoOther || clB == AssetCluster.Other) return false;
        if (clA == clB) return true;
        // US-Equity-Klumpen: Index-Perps (NASDAQ/SP500/...) und Einzelaktien (MSTR/NVDA/...)
        // korrelieren in Risk-Off-Events ~1:1 — getrennte Toepfe machten den Filter blind fuer
        // 3 parallele Equity-Shorts (NASDAQ+SP500+MSTR = 142 % der Balance, live beobachtet).
        return (clA == AssetCluster.TradFiIndex && clB == AssetCluster.TradFiStock)
            || (clA == AssetCluster.TradFiStock && clB == AssetCluster.TradFiIndex);
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
