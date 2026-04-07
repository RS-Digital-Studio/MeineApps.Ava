using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Core.Interfaces;

public interface IRiskManager
{
    RiskCheckResult ValidateTrade(SignalResult signal, MarketContext context);
    RiskCheckResult ValidateTrade(SignalResult signal, MarketContext context, decimal? currentFundingRate, int actualLeverage = 0);
    decimal CalculatePositionSize(string symbol, decimal entryPrice, decimal? stopLoss, AccountInfo account, int actualLeverage = 0);
    void UpdateDailyStats(CompletedTrade completedTrade);
    void ResetDailyStats();

    /// <summary>Setzt alle Statistiken zurück inkl. Peak-Equity (für kompletten Bot-Reset).</summary>
    void ResetAll();

    /// <summary>Berechnet den Liquidationspreis für eine Position (Isolated Margin).</summary>
    decimal CalculateLiquidationPrice(decimal entryPrice, decimal leverage, Side side);

    /// <summary>Berechnet das aktuelle Netto-Exposure aller offenen Positionen in % der Balance.</summary>
    decimal CalculateNetExposure(IReadOnlyList<Position> positions, decimal balance);
}
