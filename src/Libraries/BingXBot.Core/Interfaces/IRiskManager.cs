using BingXBot.Core.Models;

namespace BingXBot.Core.Interfaces;

public interface IRiskManager
{
    RiskCheckResult ValidateTrade(SignalResult signal, MarketContext context);
    decimal CalculatePositionSize(string symbol, decimal entryPrice, decimal? stopLoss, AccountInfo account);
    void UpdateDailyStats(CompletedTrade completedTrade);
    void ResetDailyStats();
}
