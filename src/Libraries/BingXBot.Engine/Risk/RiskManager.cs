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
    // Hinweis: Drawdown basiert auf realisierten Verlusten (abgeschlossene Trades).
    // Unrealisierte Verluste offener Positionen fließen NICHT in die Berechnung ein.
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

        // 4. Täglichen Drawdown prüfen
        decimal dailyDrawdownPercent;
        decimal totalDrawdownPercent;
        lock (_lock)
        {
            dailyDrawdownPercent = context.Account.Balance > 0
                ? Math.Abs(_dailyPnl) / context.Account.Balance * 100m
                : 0m;
            totalDrawdownPercent = context.Account.Balance > 0
                ? Math.Abs(_totalPnl) / context.Account.Balance * 100m
                : 0m;
        }

        if (_dailyPnl < 0 && dailyDrawdownPercent >= _settings.MaxDailyDrawdownPercent)
            return new RiskCheckResult(false, $"Tages-Drawdown {dailyDrawdownPercent:F1}% >= {_settings.MaxDailyDrawdownPercent}%", 0m);

        // 5. Gesamt-Drawdown prüfen
        if (_totalPnl < 0 && totalDrawdownPercent >= _settings.MaxTotalDrawdownPercent)
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
                var maxPositionValue = account.AvailableBalance * _settings.MaxLeverage;
                positionValue = Math.Min(positionValue, maxPositionValue);
                return positionValue / entryPrice;
            }
        }

        // Fallback: MaxPositionSizePercent direkt
        var fallbackValue = riskAmount * _settings.MaxLeverage;
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
