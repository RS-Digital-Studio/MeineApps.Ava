using BingXBot.Core.Enums;

namespace BingXBot.Trading.NativeSlTpManager;

/// <summary>
/// Phase 18 / H7 — Composition-Extraktion aus <see cref="LiveTradingService"/>.SlTpManager.cs.
/// Verwaltet das Cleanup nativer SL/TP-Orders und das Update des nativen SLs nach BE-Trigger.
/// Stateless: keine internen Felder, nur Delegation an IExchangeClient + BotEventBus.
/// </summary>
public interface INativeSlTpManager
{
    /// <summary>
    /// Cancelt alle SL/TP-Orders fuer ein Symbol vor Position-Close. Side-Filter im Hedge-Mode
    /// (nur Reduce-Only-Orders der zu schliessenden Seite werden gecancelt).
    /// Bei Fehler: Best-Effort-Logging, kein Throw.
    /// </summary>
    Task CancelNativeSlTpOrdersAsync(string symbol, Side? originalPositionSide = null);

    /// <summary>
    /// Aktualisiert den nativen SL auf BingX (z.B. nach BE-Trigger). 3 Retry-Versuche.
    /// Bei finalem Fehler: KRITISCH-Log + optionale Desktop-Notification.
    /// </summary>
    Task UpdateNativeStopLossAsync(string symbol, Side side, decimal newStopLoss);
}
