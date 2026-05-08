using BingXBot.Core.Configuration;
using BingXBot.Core.Models;

namespace BingXBot.Backtest;

/// <summary>
/// v1.6.4 Phase 13 — Trade-Replay-Runner.
/// Reproduziert einen Live-Trade als deterministischen Backtest-Run und vergleicht das
/// Outcome. Ziel: validieren dass <c>BacktestEngine</c> und <c>TradingServiceBase</c>
/// denselben Pfad nehmen, plus User-Frage "Live hat anders gehandelt als Backtest" beantwortbar.
///
/// Voraussetzung: Phase 14 Settings-Audit-Trail liefert den Settings-Snapshot zum Trade-Zeitpunkt.
/// </summary>
public sealed class TradeReplayRunner
{
    private readonly BacktestEngine _engine;

    public TradeReplayRunner(BacktestEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Replay-Berechnung als pure Function — testbar ohne BacktestEngine. Vergleicht zwei
    /// CompletedTrades und liefert Drift + Verdict.
    /// </summary>
    public static TradeReplayReport CompareTrades(CompletedTrade live, CompletedTrade? backtest)
    {
        if (backtest is null)
            return new TradeReplayReport(live, null, null, null, false, TradeReplayVerdict.Error, "Backtest lieferte keinen Trade");

        // Entry-Drift (Slippage-Anteil): Wie weit weicht der Backtest-Entry vom Live-Entry ab.
        var entryDrift = live.EntryPrice == 0m ? 0m
            : (backtest.EntryPrice - live.EntryPrice) / live.EntryPrice * 100m;
        // PnL-Drift in Prozent vom Live-PnL.
        decimal? pnlDrift = null;
        if (live.Pnl != 0m)
            pnlDrift = (backtest.Pnl - live.Pnl) / Math.Abs(live.Pnl) * 100m;

        var exitReasonSame = string.Equals(
            ExtractExitCategory(live.Reason),
            ExtractExitCategory(backtest.Reason),
            StringComparison.OrdinalIgnoreCase);

        var verdict = TradeReplayReport.ClassifyVerdict(pnlDrift, exitReasonSame);
        return new TradeReplayReport(live, backtest, entryDrift, pnlDrift, exitReasonSame, verdict, null);
    }

    /// <summary>
    /// Reduziert einen Reason-String wie "Take-Profit bei 51000.00" auf die Kategorie ("TP").
    /// Erlaubt Reason-Vergleich auch wenn die konkreten Preise minimal differieren.
    /// </summary>
    public static string ExtractExitCategory(string reason)
    {
        if (string.IsNullOrEmpty(reason)) return "";
        var lower = reason.ToLowerInvariant();
        if (lower.Contains("stop-loss")) return "SL";
        if (lower.Contains("take-profit") || lower.Contains("tp1") || lower.Contains("tp2") || lower.Contains("tp ")) return "TP";
        if (lower.Contains("breakeven") || lower.Contains("be ")) return "BE";
        if (lower.Contains("runner")) return "Runner";
        if (lower.Contains("close-signal")) return "CloseSignal";
        if (lower.Contains("notfall") || lower.Contains("emergency")) return "Emergency";
        if (lower.Contains("partial")) return "Partial";
        return "Other";
    }

    /// <summary>
    /// Replay-Run gegen den BacktestEngine. Lieft den Live-Trade aus der DB und feuert einen
    /// 1-Symbol-Backtest fuer das Zeitfenster. Settings-Snapshot wird aus Phase 14 geladen
    /// (oder Current-Settings als Fallback). Engine-Result wird gegen LiveTrade verglichen.
    /// </summary>
    public async Task<TradeReplayReport> ReplayAsync(
        CompletedTrade liveTrade,
        BotSettings settings,
        Func<BingXBot.Core.Interfaces.IStrategy> strategyFactory,
        Func<BingXBot.Core.Interfaces.IRiskManager> riskManagerFactory,
        CancellationToken ct = default)
    {
        try
        {
            // Window: 200 Candles vor EntryTime bis 50 Candles nach ExitTime (Plan-Spez).
            var tfMinutes = liveTrade.NavigatorTimeframe switch
            {
                BingXBot.Core.Enums.TimeFrame.M15 => 15,
                BingXBot.Core.Enums.TimeFrame.H1 => 60,
                BingXBot.Core.Enums.TimeFrame.H4 => 240,
                BingXBot.Core.Enums.TimeFrame.D1 => 1440,
                BingXBot.Core.Enums.TimeFrame.W1 => 10_080,
                _ => 60,
            };
            var from = liveTrade.EntryTime.AddMinutes(-tfMinutes * 200);
            var to = liveTrade.ExitTime.AddMinutes(tfMinutes * 50);

            var report = await _engine.RunAsync(
                strategyFactory(), riskManagerFactory(),
                liveTrade.Symbol, liveTrade.NavigatorTimeframe,
                from, to, settings.Backtest,
                scannerSettings: settings.Scanner, riskSettings: settings.Risk, ct: ct).ConfigureAwait(false);

            // Erster matching Trade aus dem Backtest-Report.
            var btTrade = report.Trades.FirstOrDefault(t =>
                t.Symbol == liveTrade.Symbol && t.Side == liveTrade.Side
                && Math.Abs((t.EntryTime - liveTrade.EntryTime).TotalMinutes) < tfMinutes * 5);

            return CompareTrades(liveTrade, btTrade);
        }
        catch (Exception ex)
        {
            return new TradeReplayReport(liveTrade, null, null, null, false, TradeReplayVerdict.Error, ex.Message);
        }
    }
}
