using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

/// <summary>
/// Persistierbarer Zustand einer pending Limit-Order für Recovery nach App-Neustart.
/// Ohne Persistenz geht die Fill-Detection verloren und TP-Orders werden nie platziert.
/// </summary>
public class PendingLimitOrderState
{
    /// <summary>BingX Order-ID der Limit-Order.</summary>
    public string OrderId { get; set; } = "";

    /// <summary>Zeitpunkt der Order-Platzierung.</summary>
    public DateTime PlacedAt { get; set; }

    /// <summary>Invalidation-Level (≈ Point0 / 78.6er). Preis jenseits dieses Levels → Order canceln.</summary>
    public decimal InvalidationLevel { get; set; }

    /// <summary>True = Long-Order, False = Short-Order.</summary>
    public bool IsLong { get; set; }

    /// <summary>TP1-Preis (161.8% Extension). Für TP-Platzierung nach Fill.</summary>
    public decimal? TakeProfit { get; set; }

    /// <summary>TP2-Preis (200% Extension + Buffer). Für TP-Platzierung nach Fill.</summary>
    public decimal? TakeProfit2 { get; set; }

    /// <summary>SK-Buch: SL NICHT in den Gewinn verschieben (B-C Korrektionen stoppen aus).</summary>
    public bool DisableSmartBreakeven { get; set; }
}
