using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

public record SignalResult(
    Signal Signal,
    decimal Confidence,
    decimal? EntryPrice,
    decimal? StopLoss,
    decimal? TakeProfit,
    string Reason);
