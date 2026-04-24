using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

public record CompletedTrade(
    string Symbol,
    Side Side,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal Quantity,
    decimal Pnl,
    decimal Fee,
    DateTime EntryTime,
    DateTime ExitTime,
    string Reason,
    TradingMode Mode,
    /// <summary>Multi-TF Standalone: Navigator-TF des auslösenden Signals (für TF-Badge im Trade-Log).</summary>
    TimeFrame NavigatorTimeframe = TimeFrame.H4);
