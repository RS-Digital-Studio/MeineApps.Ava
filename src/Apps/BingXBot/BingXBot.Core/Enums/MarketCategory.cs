namespace BingXBot.Core.Enums;

/// <summary>
/// Asset-Kategorie eines Symbols. Bestimmt Leverage-Defaults, Indikator-Anpassungen,
/// Trading-Hours und ATI-Feature-Masking.
/// </summary>
public enum MarketCategory : byte
{
    /// <summary>Kryptowährungen (BTC-USDT, ETH-USDT, ...). 24/7, Funding alle 8h.</summary>
    Crypto,
    /// <summary>Rohstoffe (Gold, Silver, Oil). NCCO-Prefix. Trading-Hours Mo-Fr.</summary>
    Commodity,
    /// <summary>Indices (Nasdaq100, S&P500, Dow Jones). NCSI-Prefix. Trading-Hours Mo-Fr.</summary>
    Index,
    /// <summary>Forex (EUR/USD, GBP/USD). NCFX-Prefix. 24/5 (Mo-Fr).</summary>
    Forex,
    /// <summary>Aktien (TSLA, AAPL, NVDA). NCSK-Prefix. US-Marktzeiten.</summary>
    Stock
}
