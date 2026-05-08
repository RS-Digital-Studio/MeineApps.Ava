namespace BingXBot.Core.Models;

/// <summary>
/// v1.6.2 Phase 12 — Order-Book-Snapshot fuer Slippage-Estimate.
/// Levels sind Bid (absteigend nach Preis) und Ask (aufsteigend nach Preis).
/// </summary>
public sealed record OrderBook(
    string Symbol,
    DateTime TimestampUtc,
    IReadOnlyList<OrderBookLevel> Bids,
    IReadOnlyList<OrderBookLevel> Asks);

/// <summary>Ein Order-Book-Level: Preis × Quantitaet.</summary>
public sealed record OrderBookLevel(decimal Price, decimal Quantity);

/// <summary>
/// v1.6.2 Phase 12 — Geschätzter Slippage-Effekt vor Market-Order-Place.
/// </summary>
public sealed record SlippageEstimate(
    decimal EstimatedAvgFillPrice,
    /// <summary>Slippage in % zum Referenz-Preis (positiv = schlechter Fill, 0 = ideal).</summary>
    decimal SlippagePercent,
    /// <summary>Wie viel der Order-Quantitaet im Buch verfuegbar war (≤ requestedQuantity).</summary>
    decimal FilledQuantity,
    /// <summary>True wenn das Buch nicht genug Liquiditaet hatte.</summary>
    bool InsufficientLiquidity);
