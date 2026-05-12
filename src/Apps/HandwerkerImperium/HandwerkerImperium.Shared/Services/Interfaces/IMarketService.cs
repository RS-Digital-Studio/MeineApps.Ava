using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// V7 (Phase 3 Ressourcen-Plan): Material-Markt mit deterministischer Preis-Dynamik.
/// Pro Spieler + UTC-Tag deterministisch (Seed = PlayerId.GetHashCode() ^ UtcDay),
/// damit Save-Scumming verhindert wird. Innerhalb eines Tages oszilliert der Preis
/// in einer Sinus-Welle (+/-50%) um den Basis-Preis.
/// </summary>
public interface IMarketService
{
    /// <summary>True wenn der Markt fuer den Spieler freigeschaltet ist (Research logi_05).</summary>
    bool IsMarketAvailable { get; }

    /// <summary>Aktueller Kaufpreis eines Materials pro Stueck (inkl. Event-Modulator).</summary>
    decimal GetBuyPrice(string productId);

    /// <summary>Aktueller Verkaufspreis pro Stueck (Buy × 0.95 — 5% Spread).</summary>
    decimal GetSellPrice(string productId);

    /// <summary>
    /// Trend-Indikator: -1.0 (stark fallend) bis +1.0 (stark steigend). Wird auf Basis
    /// der naechsten Stunde berechnet (Vergleich Now vs. Now+1h).
    /// </summary>
    double GetPriceTrend(string productId);

    /// <summary>
    /// 24h-Preisreihe in 1h-Schritten (24 Punkte). Index 0 = Tagesanfang, 23 = letzte Stunde.
    /// Wird vom Heatmap-Renderer fuer die Vorschau verwendet.
    /// </summary>
    decimal[] Get24hPriceSeries(string productId);

    /// <summary>
    /// Kauf einer Material-Menge: zieht Geld ab, fuegt Material ins Lager (respektiert Stack-Limit).
    /// Returnt true bei Erfolg.
    /// </summary>
    bool TryBuy(string productId, int count);

    /// <summary>
    /// Verkauf einer Material-Menge zum aktuellen Markt-Preis (mit 5% Spread).
    /// Respektiert ReservedInventory (kann nicht reserviertes verkaufen).
    /// Returnt Erloes (0 bei Fehler).
    /// </summary>
    decimal TrySell(string productId, int count);

    /// <summary>Wird ausgeloest wenn sich der Markt-State aendert (UI-Refresh-Trigger).</summary>
    event Action? MarketChanged;
}
