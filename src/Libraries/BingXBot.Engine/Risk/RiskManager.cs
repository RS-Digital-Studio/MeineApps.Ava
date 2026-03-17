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

        // 4. Position-Größe berechnen (vor Drawdown-Check, damit wir das Risiko kennen)
        var entryPrice = signal.EntryPrice ?? context.CurrentTicker.LastPrice;
        var posSize = CalculatePositionSize(context.Symbol, entryPrice, signal.StopLoss, context.Account);

        if (posSize <= 0)
            return new RiskCheckResult(false, "Position-Größe ist 0", 0m);

        // 5. Taeglichen Drawdown pruefen (inkl. unrealisierter Verluste + Risiko der neuen Position)
        decimal dailyDrawdownPercent;
        decimal totalDrawdownPercent;
        lock (_lock)
        {
            // Unrealisierte Verluste: Summe ALLER negativen PnL einzelner Positionen,
            // nicht die Netto-Summe. Verhindert dass Gewinne einer Position
            // die Verluste einer anderen maskieren.
            var unrealizedLoss = context.OpenPositions
                .Where(p => p.UnrealizedPnl < 0)
                .Sum(p => p.UnrealizedPnl); // Ist negativ oder 0

            // Worst-Case-Risiko der NEUEN Position berechnen:
            // Wenn SL gesetzt: Verlust = SL-Distanz * Quantity
            // Ohne SL: Verlust = MaxPositionSizePercent vom Konto (konservative Schätzung)
            var newPositionRisk = 0m;
            if (signal.StopLoss.HasValue && signal.StopLoss.Value > 0)
            {
                var slDistance = Math.Abs(entryPrice - signal.StopLoss.Value);
                newPositionRisk = slDistance * posSize; // Maximaler Verlust bei SL-Hit
            }
            else
            {
                // Ohne SL: konservativ das gesamte riskAmount als Worst-Case annehmen
                newPositionRisk = context.Account.AvailableBalance * _settings.MaxPositionSizePercent / 100m;
            }

            var effectiveDailyPnl = _dailyPnl + unrealizedLoss - newPositionRisk;
            var effectiveTotalPnl = _totalPnl + unrealizedLoss - newPositionRisk;

            // Drawdown ist nur bei negativem PnL relevant.
            // Positive PnL (Gewinn) soll NICHT als Drawdown gezaehlt werden.
            dailyDrawdownPercent = context.Account.Balance > 0 && effectiveDailyPnl < 0
                ? Math.Abs(effectiveDailyPnl) / context.Account.Balance * 100m
                : 0m;
            totalDrawdownPercent = context.Account.Balance > 0 && effectiveTotalPnl < 0
                ? Math.Abs(effectiveTotalPnl) / context.Account.Balance * 100m
                : 0m;
        }

        if (dailyDrawdownPercent >= _settings.MaxDailyDrawdownPercent)
            return new RiskCheckResult(false, $"Tages-Drawdown {dailyDrawdownPercent:F1}% >= {_settings.MaxDailyDrawdownPercent}% (inkl. Risiko neuer Position)", 0m);

        // 6. Gesamt-Drawdown pruefen
        if (totalDrawdownPercent >= _settings.MaxTotalDrawdownPercent)
            return new RiskCheckResult(false, $"Gesamt-Drawdown {totalDrawdownPercent:F1}% >= {_settings.MaxTotalDrawdownPercent}% (inkl. Risiko neuer Position)", 0m);

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

        // Fallback ohne StopLoss: Konservativ - halbes Risiko als Margin, max 5x Leverage.
        // Ohne SL ist das Verlustrisiko unbekannt, daher deutlich vorsichtiger.
        var fallbackMargin = riskAmount * 0.5m;
        var fallbackLeverage = Math.Min(leverage, 5m);
        var fallbackValue = fallbackMargin * fallbackLeverage;
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
