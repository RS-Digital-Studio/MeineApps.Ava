using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Trading.Reconciliation;

/// <summary>
/// Phase 18 / G7 (Teil-Extraktion) — Pure-Function-Helper fuer TP-Order-Matching im
/// IdempotencyCheck-Pfad (siehe <see cref="LiveTradingService"/>.ProbeExistingTpOrderAsync).
///
/// Hintergrund: A2 Idempotency-Check probt vor jedem Retry per <c>GetOpenOrdersAsync</c> ob
/// die TP-Limit (Side+Qty+Price-Match mit Toleranz) bereits liegt. Die Match-Logik selbst
/// ist eine reine Funktion ohne Exchange-State — perfekt extrahierbar.
///
/// Testbarkeit: Vorher musste man den ganzen LiveTradingService instantiieren (mit DI-Stack +
/// IExchangeClient-Mock) um diesen Pfad zu testen. Jetzt sind die Match-Regeln (Toleranz-Fenster,
/// Side-Filter, ReduceOnly-Pflicht, Limit-Type-Pflicht) als pure Funktion isoliert getestbar.
///
/// Composition-Vorbild: Naechster Schritt waere die volle Extraktion von
/// <c>LiveTradingService.OrderPlacement</c> in einen <c>ITpOrderManager</c>-Service —
/// dieser Helper ist die erste Bauteil-Bibliothek dafuer.
/// </summary>
public static class TpOrderMatcher
{
    /// <summary>Default-Toleranz bei der Quantity-Matchung (0.5 % — BingX truncated nach Precision-Cache).</summary>
    public const decimal DefaultQuantityTolerancePercent = 0.005m;

    /// <summary>Default-Toleranz beim Preis (0.05 % — Tick-Round-Buffer).</summary>
    public const decimal DefaultPriceTolerancePercent = 0.0005m;

    /// <summary>
    /// Findet die erste <see cref="Order"/> in <paramref name="openOrders"/>, die zu der erwarteten
    /// TP-Reduce-Only-Limit-Order passt. Returns null wenn keine Match.
    ///
    /// Match-Kriterien (alle muessen passen):
    /// 1. Symbol identisch
    /// 2. Side identisch (= Schliess-Seite der Position)
    /// 3. Order-Type = Limit
    /// 4. ReduceOnly = true
    /// 5. Quantity ± Toleranz (default 0.5 %)
    /// 6. Price ± Toleranz (default 0.05 %)
    /// </summary>
    public static Order? FindMatchingTpOrder(
        IEnumerable<Order> openOrders,
        string symbol,
        Side closeSide,
        decimal expectedQuantity,
        decimal expectedPrice,
        decimal? quantityTolerancePercent = null,
        decimal? priceTolerancePercent = null)
    {
        var qtyTolerance = Math.Max(expectedQuantity * (quantityTolerancePercent ?? DefaultQuantityTolerancePercent), 1e-8m);
        var priceTolerance = Math.Max(expectedPrice * (priceTolerancePercent ?? DefaultPriceTolerancePercent), 1e-8m);

        foreach (var o in openOrders)
        {
            if (o.Symbol != symbol) continue;
            if (o.Side != closeSide) continue;
            if (!o.ReduceOnly) continue;
            if (o.Type != OrderType.Limit) continue;
            if (Math.Abs(o.Quantity - expectedQuantity) > qtyTolerance) continue;
            if (Math.Abs(o.Price - expectedPrice) > priceTolerance) continue;
            return o;
        }
        return null;
    }
}
