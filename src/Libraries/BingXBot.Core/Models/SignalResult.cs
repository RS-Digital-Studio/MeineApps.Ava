using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

public record SignalResult(
    Signal Signal,
    decimal Confidence,
    decimal? EntryPrice,
    decimal? StopLoss,
    decimal? TakeProfit,
    string Reason,
    decimal? TakeProfit2 = null,
    int ConfluenceScore = 0,
    bool PreferLimitOrder = false,
    /// <summary>SK-Regel: SL NICHT in den Gewinn verschieben (B-C Korrektionen stoppen aus).</summary>
    bool DisableSmartBreakeven = false,
    /// <summary>SK-Regel: Zusätzlicher Entry (halbe Position) für Staffelung 50er voll + 66.7er halb.</summary>
    bool IsAdditionalEntry = false);
