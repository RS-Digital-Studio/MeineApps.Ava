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

    /// <summary>Überladung ohne Funding-Rate (Abwärtskompatibilität).</summary>
    public RiskCheckResult ValidateTrade(SignalResult signal, MarketContext context)
        => ValidateTrade(signal, context, null);

    public RiskCheckResult ValidateTrade(SignalResult signal, MarketContext context, decimal? currentFundingRate)
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

        // 5. Netto-Exposure prüfen: Summe aller Positionswerte darf MaxNetExposurePercent nicht überschreiten
        if (context.Account.Balance > 0)
        {
            var currentExposure = CalculateNetExposure(context.OpenPositions, context.Account.Balance);
            var newPositionExposure = posSize * entryPrice / context.Account.Balance * 100m;
            if (currentExposure + newPositionExposure > _settings.MaxNetExposurePercent)
                return new RiskCheckResult(false,
                    $"Netto-Exposure {currentExposure + newPositionExposure:F1}% > {_settings.MaxNetExposurePercent}%", 0m);
        }

        // 6. Liquidation-Preis prüfen: Position darf nicht zu nah am Liquidationspreis eröffnet werden
        var leverage = _settings.MaxLeverage > 0 ? _settings.MaxLeverage : 1m;
        var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
        var liqPrice = CalculateLiquidationPrice(entryPrice, leverage, side);
        if (liqPrice > 0 && entryPrice > 0)
        {
            var liqDistancePercent = Math.Abs(entryPrice - liqPrice) / entryPrice * 100m;
            if (liqDistancePercent < _settings.MinLiquidationDistancePercent)
                return new RiskCheckResult(false,
                    $"Liquidationspreis zu nah: {liqDistancePercent:F1}% Abstand < {_settings.MinLiquidationDistancePercent}% Minimum (Liq={liqPrice:G6})", 0m);
        }

        // 7. Funding-Rate prüfen: Keine Trades gegen hohe Funding-Rate eröffnen
        if (_settings.ConsiderFundingRate && currentFundingRate.HasValue && currentFundingRate.Value != 0)
        {
            // Positive Funding = Longs zahlen, negative Funding = Shorts zahlen
            var isAdverse = (signal.Signal == Signal.Long && currentFundingRate.Value > 0) ||
                            (signal.Signal == Signal.Short && currentFundingRate.Value < 0);
            var absRate = Math.Abs(currentFundingRate.Value) * 100m; // In Prozent
            if (isAdverse && absRate > _settings.MaxAdverseFundingRatePercent)
                return new RiskCheckResult(false,
                    $"Funding-Rate {absRate:F3}% gegen Position > {_settings.MaxAdverseFundingRatePercent}% Maximum", 0m);
        }

        // 8. Taeglichen Drawdown pruefen (inkl. unrealisierter Verluste + Risiko der neuen Position)
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
                // Ohne SL: Worst-Case = gesamte Margin (Positionsgröße in %)
                newPositionRisk = context.Account.AvailableBalance * _settings.MaxPositionSizePercent / 100m * leverage;
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

        // 9. Gesamt-Drawdown pruefen
        if (totalDrawdownPercent >= _settings.MaxTotalDrawdownPercent)
            return new RiskCheckResult(false, $"Gesamt-Drawdown {totalDrawdownPercent:F1}% >= {_settings.MaxTotalDrawdownPercent}% (inkl. Risiko neuer Position)", 0m);

        return new RiskCheckResult(true, null, posSize);
    }

    public decimal CalculatePositionSize(string symbol, decimal entryPrice, decimal? stopLoss, AccountInfo account)
    {
        if (entryPrice <= 0 || account.AvailableBalance <= 0) return 0m;

        // MaxLeverage muss > 0 sein, sonst Fallback auf 1
        var leverage = _settings.MaxLeverage > 0 ? _settings.MaxLeverage : 1m;

        // H-1 Fix: Risiko-basiertes Position-Sizing wenn SL vorhanden.
        // MaxPositionSizePercent = max. Verlust in % der Balance bei SL-Hit.
        // Beispiel: 2% Risk bei 10.000 USDT Balance = max 200 USDT Verlust.
        if (stopLoss.HasValue && stopLoss.Value > 0)
        {
            var slDistance = Math.Abs(entryPrice - stopLoss.Value);
            if (slDistance > 0)
            {
                var maxLoss = account.AvailableBalance * _settings.MaxPositionSizePercent / 100m;
                var riskBasedQty = maxLoss / slDistance;

                // Cap: Positionswert darf nicht größer sein als Balance * Leverage
                var maxPositionValue = account.AvailableBalance * leverage;
                var maxQty = maxPositionValue / entryPrice;

                return Math.Min(riskBasedQty, maxQty);
            }
        }

        // Fallback ohne SL: %-basierte Margin-Sizing (konservativer)
        var marginAmount = account.AvailableBalance * _settings.MaxPositionSizePercent / 100m;
        var positionValue = marginAmount * leverage;

        return positionValue / entryPrice;
    }

    /// <summary>
    /// Berechnet den Liquidationspreis für Isolated Margin.
    /// Formel: Long: EntryPrice * (1 - 1/Leverage + MaintenanceMarginRate)
    ///         Short: EntryPrice * (1 + 1/Leverage - MaintenanceMarginRate)
    /// BingX Maintenance Margin Rate ~0.4% für die meisten Perpetuals.
    /// </summary>
    public decimal CalculateLiquidationPrice(decimal entryPrice, decimal leverage, Side side)
    {
        if (entryPrice <= 0 || leverage <= 0) return 0m;

        const decimal maintenanceMarginRate = 0.004m; // 0.4% BingX Standard

        if (side == Side.Buy)
            return entryPrice * (1m - 1m / leverage + maintenanceMarginRate);
        else
            return entryPrice * (1m + 1m / leverage - maintenanceMarginRate);
    }

    /// <summary>
    /// Berechnet das aktuelle Netto-Exposure aller offenen Positionen in % der Balance.
    /// Netto = Summe(Qty * MarkPrice) für jede Position, geteilt durch Balance.
    /// </summary>
    public decimal CalculateNetExposure(IReadOnlyList<Position> positions, decimal balance)
    {
        if (balance <= 0 || positions.Count == 0) return 0m;

        var totalExposure = positions.Sum(p => p.Quantity * p.MarkPrice);
        return totalExposure / balance * 100m;
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
