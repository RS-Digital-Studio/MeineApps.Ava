using BingXBot.Core.Models;

namespace BingXBot.Backtest;

/// <summary>
/// v1.6.4 Phase 13 — Verdict-Klassifikation fuer Trade-Replay.
/// </summary>
public enum TradeReplayVerdict
{
    /// <summary>PnL-Drift &lt; 1 % — Live und Backtest sind effektiv gleich.</summary>
    Identical,
    /// <summary>1-5 % Drift — Slippage-Anteil typisch erwartbar.</summary>
    MinorDrift,
    /// <summary>5-15 % Drift — Logik-Issue verdaechtig, manuelle Pruefung empfohlen.</summary>
    MajorDrift,
    /// <summary>&gt; 15 % Drift — ernster Bug, sofortige Untersuchung.</summary>
    LogicMismatch,
    /// <summary>Replay konnte nicht ausgefuehrt werden (fehlende Klines / Settings-Snapshot).</summary>
    Error,
}

/// <summary>
/// v1.6.4 Phase 13 — Trade-Replay-Report.
/// </summary>
public sealed record TradeReplayReport(
    CompletedTrade LiveTrade,
    CompletedTrade? BacktestTrade,
    decimal? EntryPriceDriftPercent,
    decimal? PnlDriftPercent,
    bool ExitReasonSame,
    TradeReplayVerdict Verdict,
    string? ErrorDetail)
{
    /// <summary>Liefert die Verdict-Klassifikation aus den Drift-Werten.</summary>
    public static TradeReplayVerdict ClassifyVerdict(decimal? pnlDriftPercent, bool exitReasonSame)
    {
        if (pnlDriftPercent is null) return TradeReplayVerdict.Error;
        var d = Math.Abs(pnlDriftPercent.Value);
        if (d < 1m && exitReasonSame) return TradeReplayVerdict.Identical;
        if (d < 5m) return TradeReplayVerdict.MinorDrift;
        if (d < 15m) return TradeReplayVerdict.MajorDrift;
        return TradeReplayVerdict.LogicMismatch;
    }
}
