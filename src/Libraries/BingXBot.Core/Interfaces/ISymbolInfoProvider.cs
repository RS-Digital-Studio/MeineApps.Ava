using BingXBot.Core.Enums;

namespace BingXBot.Core.Interfaces;

/// <summary>
/// Abstraktion fuer Symbol-Handelsinformationen (Min-Qty, Min-Notional, Precision-Rundung).
/// Erlaubt dem Backtest (<c>BingXBot.Backtest</c>), die Live-Min-Order-Semantik zu spiegeln,
/// ohne auf <c>BingXBot.Exchange</c> (wo <c>SymbolInfoCache</c> lebt) zu referenzieren.
/// Implementierungen: <c>SymbolInfoCache</c> (Live + Lab) — dieselbe Instanz, die auch
/// <c>BingXRestClient</c> nutzt, damit Backtest und Live identische Mindest-Order-Regeln teilen.
/// Signaturen + Semantik sind an <c>SymbolInfoCache</c> angelehnt.
/// </summary>
public interface ISymbolInfoProvider
{
    /// <summary>
    /// Prueft ob eine Quantity die Mindestanforderungen erfuellt (MinQty + MinNotional).
    /// Liefert false, wenn quantity &lt; MinQty oder (price &gt; 0 und quantity*price &lt; MinNotional).
    /// </summary>
    bool MeetsMinimumOrder(string symbol, decimal quantity, decimal price);

    /// <summary>
    /// Rundet eine Quantity auf die erlaubte Precision (Floor/Truncate, nicht Round-Up).
    /// BingX lehnt Orders mit zu vielen Dezimalstellen ab.
    /// </summary>
    decimal TruncateQuantity(string symbol, decimal quantity);

    /// <summary>
    /// Side-aware Tick-Size-Rundung fuer SL/TP: Long (Buy) = Floor, Short (Sell) = Ceiling.
    /// Verhindert dass Tick-Rounding den geplanten Buffer auffrisst.
    /// </summary>
    decimal RoundPriceConservative(string symbol, decimal price, Side positionSide);

    /// <summary>Rundet einen Preis auf die erlaubte Precision (Standard-Rundung, MidpointRounding.ToEven).</summary>
    decimal RoundPrice(string symbol, decimal price);
}
