using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Engine.Risk;

/// <summary>
/// v1.6.2 Phase 12 — Pure-Function-Estimator fuer Market-Order-Slippage.
///
/// Walks die Levels eines Order-Books bis die gewuenschte Menge gefuellt ist und liefert
/// den gewichteten Durchschnitts-Fill-Preis + Slippage-Prozent zum Referenzpreis.
/// </summary>
public static class SlippageEstimator
{
    /// <summary>
    /// Estimate fuer eine Market-Order. Buy walks die Asks (aufsteigend), Sell walks die Bids
    /// (absteigend). Wenn das Buch nicht genug Liquiditaet hat, wird <see cref="SlippageEstimate.InsufficientLiquidity"/>
    /// gesetzt und FilledQuantity ist die tatsaechlich verfuegbare Menge.
    /// </summary>
    public static SlippageEstimate Estimate(OrderBook book, Side side, decimal quantity, decimal referencePrice)
    {
        if (quantity <= 0m || referencePrice <= 0m)
            return new SlippageEstimate(referencePrice, 0m, 0m, InsufficientLiquidity: false);

        var levels = side == Side.Buy ? book.Asks : book.Bids;
        if (levels == null || levels.Count == 0)
            return new SlippageEstimate(referencePrice, 0m, 0m, InsufficientLiquidity: true);

        decimal remaining = quantity;
        decimal totalCost = 0m;
        decimal filled = 0m;

        foreach (var lvl in levels)
        {
            if (remaining <= 0m) break;
            var take = Math.Min(remaining, lvl.Quantity);
            totalCost += take * lvl.Price;
            filled += take;
            remaining -= take;
        }

        if (filled <= 0m)
            return new SlippageEstimate(referencePrice, 0m, 0m, InsufficientLiquidity: true);

        var avgFill = totalCost / filled;
        // Slippage-Prozent: bei Buy ist avgFill ≥ refPrice (positiv = schlechter), bei Sell umgekehrt.
        var slippagePercent = side == Side.Buy
            ? (avgFill - referencePrice) / referencePrice * 100m
            : (referencePrice - avgFill) / referencePrice * 100m;

        return new SlippageEstimate(
            EstimatedAvgFillPrice: avgFill,
            SlippagePercent: slippagePercent,
            FilledQuantity: filled,
            InsufficientLiquidity: remaining > 0m);
    }
}
