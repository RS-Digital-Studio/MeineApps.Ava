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

    /// <summary>
    /// Geplanter Limit-Entry-Preis (aus SignalResult.EntryPrice). Nach Fill steht der echte
    /// Fill-Preis bei BingX. Seit 17.04.2026 persistiert.
    /// </summary>
    public decimal EntryPrice { get; set; }

    /// <summary>
    /// Symbol (z.B. "BTC-USDT"). Ab v1.1.5 Pflicht, da Dictionary-Key jetzt
    /// Level-Suffix enthält (Symbol#SequenceId). Bei Legacy-Einträgen (v1.1.4)
    /// wird das Symbol beim Laden aus dem Key extrahiert.
    /// </summary>
    public string Symbol { get; set; } = "";

    /// <summary>
    /// SK Triple-Entry (15.04.2026): SequenceId der platzierenden Sequenz,
    /// enthält _L500/_L618/_L667 Suffix. Wird für Cancel-by-SequenceRoot
    /// benötigt, wenn die Sequenz invalidiert wird bevor der Preis den
    /// Invalidation-Level erreicht.
    /// Kann null sein bei Nicht-SK-Strategien.
    /// </summary>
    public string? SequenceId { get; set; }

    // BUCH-ONLY: OverExtensionLevel (138.2%) entfernt — kein Buch-Konzept.
}
