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
    /// <summary>SK Holy Trinity: TP1 Close-Ratio Override (0.5 = 50%). Null = globaler Default aus RiskSettings.</summary>
    decimal? Tp1CloseRatioOverride = null);
