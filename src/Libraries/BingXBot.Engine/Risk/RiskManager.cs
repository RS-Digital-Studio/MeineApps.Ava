using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace BingXBot.Engine.Risk;

public class RiskManager : IRiskManager
{
    private readonly RiskSettings _settings;
    private readonly ILogger<RiskManager> _logger;
    // Drawdown basiert auf realisierten + unrealisierten Verlusten.
    // Unrealisierte Verluste offener Positionen fliessen in den taeglichen Drawdown ein.
    private decimal _dailyPnl;
    private decimal _totalPnl;
    private readonly object _lock = new();

    public RiskManager(RiskSettings settings, ILogger<RiskManager> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public RiskCheckResult ValidateTrade(SignalResult signal, MarketContext context)
    {
        // 1. Signal prüfen
        if (signal.Signal == Signal.None)
            return new RiskCheckResult(false, "Kein Signal", 0m);

        // 2. Max offene Positionen
        if (context.OpenPositions.Count >= _settings.MaxOpenPositions)
            return new RiskCheckResult(false, $"Max {_settings.MaxOpenPositions} offene Positionen erreicht", 0m);

        // 3. Max Positionen pro Symbol
        var symbolPositions = context.OpenPositions.Count(p => p.Symbol == context.Symbol);
        if (symbolPositions >= _settings.MaxOpenPositionsPerSymbol)
            return new RiskCheckResult(false, $"Max {_settings.MaxOpenPositionsPerSymbol} Positionen pro Symbol erreicht", 0m);

        // 4. Taeglichen Drawdown pruefen (inkl. unrealisierter Verluste offener Positionen)
        decimal dailyDrawdownPercent;
        decimal totalDrawdownPercent;
        lock (_lock)
        {
            // Unrealisierte Verluste einbeziehen (nur negative Werte)
            var unrealizedLoss = context.Account.UnrealizedPnl < 0 ? context.Account.UnrealizedPnl : 0m;
            var effectiveDailyPnl = _dailyPnl + unrealizedLoss;
            var effectiveTotalPnl = _totalPnl + unrealizedLoss;

            dailyDrawdownPercent = context.Account.Balance > 0
                ? Math.Abs(effectiveDailyPnl) / context.Account.Balance * 100m
                : 0m;
            totalDrawdownPercent = context.Account.Balance > 0
                ? Math.Abs(effectiveTotalPnl) / context.Account.Balance * 100m
                : 0m;
        }

        if (dailyDrawdownPercent >= _settings.MaxDailyDrawdownPercent)
            return new RiskCheckResult(false, $"Tages-Drawdown {dailyDrawdownPercent:F1}% >= {_settings.MaxDailyDrawdownPercent}%", 0m);

        // 5. Gesamt-Drawdown pruefen
        if (totalDrawdownPercent >= _settings.MaxTotalDrawdownPercent)
            return new RiskCheckResult(false, $"Gesamt-Drawdown {totalDrawdownPercent:F1}% >= {_settings.MaxTotalDrawdownPercent}%", 0m);

        // 6. Position-Größe berechnen
        var posSize = CalculatePositionSize(context.Symbol, signal.EntryPrice ?? context.CurrentTicker.LastPrice,
            signal.StopLoss, context.Account);

        if (posSize <= 0)
            return new RiskCheckResult(false, "Position-Größe ist 0", 0m);

        return new RiskCheckResult(true, null, posSize);
    }

    public decimal CalculatePositionSize(string symbol, decimal entryPrice, decimal? stopLoss, AccountInfo account)
    {
        if (entryPrice <= 0 || account.AvailableBalance <= 0) return 0m;

        // MaxLeverage muss > 0 sein, sonst Fallback auf 1
        var leverage = _settings.MaxLeverage > 0 ? _settings.MaxLeverage : 1m;
        var riskAmount = account.AvailableBalance * _settings.MaxPositionSizePercent / 100m;

        if (stopLoss.HasValue && stopLoss.Value > 0 && stopLoss.Value != entryPrice)
        {
            // Risiko-basiertes Sizing: riskAmount / SL-Distanz
            var slDistance = Math.Abs(entryPrice - stopLoss.Value);
            var slPercent = slDistance / entryPrice;

            if (slPercent > 0)
            {
                var positionValue = riskAmount / slPercent;
                // Leverage begrenzen
                var maxPositionValue = account.AvailableBalance * leverage;
                positionValue = Math.Min(positionValue, maxPositionValue);
                return positionValue / entryPrice;
            }
        }

        // Fallback: MaxPositionSizePercent direkt
        var fallbackValue = riskAmount * leverage;
        return fallbackValue / entryPrice;
    }

    public void UpdateDailyStats(CompletedTrade completedTrade)
    {
        lock (_lock)
        {
            _dailyPnl += completedTrade.Pnl;
            _totalPnl += completedTrade.Pnl;
        }
    }

    public void ResetDailyStats()
    {
        lock (_lock)
        {
            _dailyPnl = 0m;
        }
    }
}
