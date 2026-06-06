using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;

namespace BingXBot.Backtest.Portfolio;

/// <summary>
/// Verbucht abgeschlossene Trades im <see cref="IRiskManager"/> live-treu — Spiegel von
/// <c>TradingServiceBase.ProcessCompletedTrade</c> (BingXBot.Trading). Ohne diesen Helper riefen
/// die Backtest-Engines <see cref="IRiskManager.UpdateDailyStats"/> direkt auf, das bei JEDEM
/// <c>Pnl&lt;0</c> die Verlustserie hochzaehlt — auch bei Break-Even-Ausstoppungen. Der Live-Bot
/// erkennt BE-Exits (Pnl nahe 0) und setzt die Serie zurueck (Buch 6.8), sonst feuert die
/// LossStreak-Daempfung/-Pause im Backtest zu frueh (Backtest zu pessimistisch).
/// </summary>
internal static class BacktestRiskAccounting
{
    /// <summary>
    /// Streamt einen abgeschlossenen Trade in den RiskManager und korrigiert den Verlust-Zaehler
    /// um BE-Exits — identisch zu <c>TradingServiceBase.ProcessCompletedTrade</c> (Z. 1593-1604).
    /// </summary>
    public static void RecordCompletedTrade(IRiskManager riskManager, CompletedTrade trade)
    {
        // UpdateDailyStats erhoeht CurrentConsecutiveLosses bei Pnl<0 bedingungslos.
        riskManager.UpdateDailyStats(trade);

        // BE-Exit-Erkennung wortgleich zu TradingServiceBase.cs:1593-1594: Pnl innerhalb ±0,2 %
        // des Positionswerts (Entry × Quantity) gilt als Break-Even, nicht als Verlust.
        var isBreakEvenExit = trade.EntryPrice > 0
            && Math.Abs(trade.Pnl) < Math.Abs(trade.EntryPrice * trade.Quantity) * 0.002m;

        // BE bricht die Verlustserie (Buch 6.8: sofortiger Re-Entry erlaubt). UpdateDailyStats hatte
        // bei leicht negativem BE-Pnl faelschlich +1 gezaehlt — hier auf 0 zuruecksetzen.
        if (isBreakEvenExit)
            riskManager.SetConsecutiveLosses(0);
    }
}
