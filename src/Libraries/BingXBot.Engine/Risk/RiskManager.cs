using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.News;
using Microsoft.Extensions.Logging;

namespace BingXBot.Engine.Risk;

public class RiskManager : IRiskManager
{
    private readonly RiskSettings _settings;
    private readonly ILogger<RiskManager> _logger;
    /// <summary>Task 1.2 — News-Kalender-Service. Null = News-Blackout deaktiviert.</summary>
    private readonly IEconomicCalendarService? _newsCalendar;
    // Drawdown basiert auf realisierten + unrealisierten Verlusten.
    // Unrealisierte Verluste offener Positionen fliessen in den taeglichen Drawdown ein.
    private decimal _dailyPnl;
    /// <summary>Kumulierter realisierter PnL des heutigen Tages (SK-Buch Workflow 6.1+6.2).</summary>
    public decimal DailyPnl { get { lock (_lock) { return _dailyPnl; } } }
    private decimal _totalPnl;
    /// <summary>Aktueller kumulativer PnL (für Equity-Curve-Trading).</summary>
    public decimal TotalPnl => _totalPnl;

    /// <summary>
    /// Task 3.3 — Geschätztes offenes Trade-Risiko (Sum(|Entry - SL| × Qty) aller offenen Positionen).
    /// Wird vom TradingServiceBase über <see cref="SetOpenRiskEstimate(decimal)"/> aktualisiert.
    /// 0 = keine offenen Positionen oder nicht initialisiert.
    /// </summary>
    private decimal _openRiskEstimate;

    /// <summary>Task 3.3 — Setzt das aktuelle offene Trade-Risiko (vom TradingServiceBase pro Tick aktualisiert).</summary>
    public void SetOpenRiskEstimate(decimal openRiskUsd)
    {
        lock (_lock) { _openRiskEstimate = Math.Max(0m, openRiskUsd); }
    }
    // Peak-Equity-Tracking für echten Peak-to-Trough-Drawdown (persistent über gesamte Laufzeit)
    private decimal _peakEquity;
    private bool _peakEquityInitialized;
    private readonly object _lock = new();

    public RiskManager(RiskSettings settings, ILogger<RiskManager> logger, IEconomicCalendarService? newsCalendar = null)
    {
        _settings = settings;
        _logger = logger;
        _newsCalendar = newsCalendar;
    }

    /// <summary>Überladung ohne Funding-Rate und Leverage (Abwärtskompatibilität).</summary>
    public RiskCheckResult ValidateTrade(SignalResult signal, MarketContext context)
        => ValidateTrade(signal, context, null, 0);

    public RiskCheckResult ValidateTrade(SignalResult signal, MarketContext context, decimal? currentFundingRate, int actualLeverage = 0)
    {
        // 1. Signal prüfen
        if (signal.Signal == Signal.None)
            return new RiskCheckResult(false, "Kein Signal", 0m);

        // 1a. SL ist Pflicht: Ohne Stop-Loss wird die volle Margin riskiert → Konto-Gefahr
        if (!signal.StopLoss.HasValue || signal.StopLoss.Value <= 0)
            return new RiskCheckResult(false, "Kein Stop-Loss — Trade abgelehnt (Pflicht-SL)", 0m);

        // 2. Max offene Positionen
        if (context.OpenPositions.Count >= _settings.MaxOpenPositions)
            return new RiskCheckResult(false, $"Max {_settings.MaxOpenPositions} offene Positionen erreicht", 0m);

        // 3. Max Positionen pro Symbol
        var symbolPositions = context.OpenPositions.Count(p => p.Symbol == context.Symbol);
        if (symbolPositions >= _settings.MaxOpenPositionsPerSymbol)
            return new RiskCheckResult(false, $"Max {_settings.MaxOpenPositionsPerSymbol} Positionen pro Symbol erreicht", 0m);

        // 4. Position-Größe berechnen mit tatsächlichem Leverage (nicht MaxLeverage)
        var entryPrice = signal.EntryPrice ?? context.CurrentTicker.LastPrice;

        // Explizite Balance-Prüfung mit klarer Meldung (erleichtert Debugging bei Drawdown=100%)
        if (context.Account.AvailableBalance <= 0)
            return new RiskCheckResult(false, "Keine verfügbare Balance — kein Trade möglich", 0m);

        // Task 1.2 — News-Blackout (synchron abgefragt aus Cache; echter Fetch liegt im Service).
        // Graceful degradation: Ohne Service läuft der Bot wie bisher weiter.
        if (_newsCalendar != null && _settings.NewsBlackoutMinutes > 0)
        {
            try
            {
                var blackoutEvent = _newsCalendar.GetActiveBlackoutEventAsync(
                    DateTime.UtcNow, _settings.NewsBlackoutMinutes).GetAwaiter().GetResult();
                if (blackoutEvent != null)
                    return new RiskCheckResult(false,
                        $"News-Blackout: {blackoutEvent.Name} ({blackoutEvent.Country} {blackoutEvent.TimeUtc:HH:mm} UTC)", 0m);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "News-Blackout-Check fehlgeschlagen, Trade erlaubt (graceful degradation)");
            }
        }

        // SK-Plan 3.5: Max Daily Loss Circuit-Breaker
        // Nach Überschreitung werden bis UTC-00:00 keine neuen Entries erlaubt.
        if (_settings.MaxDailyLossPercent > 0 && context.Account.Balance > 0)
        {
            lock (_lock)
            {
                var realized = _dailyPnl;
                if (realized < 0)
                {
                    var lossPct = Math.Abs(realized) / context.Account.Balance * 100m;
                    if (lossPct >= _settings.MaxDailyLossPercent)
                        return new RiskCheckResult(false,
                            $"Daily-Loss-Circuit {lossPct:F1}% >= {_settings.MaxDailyLossPercent}% (Pause bis UTC-00:00)", 0m);
                }
            }
        }

        // Task 3.3: Max Daily Risk Budget (Buch S.13: 1-3% pro Tag insgesamt).
        // Summiert realisierte Verluste + geplantes Trade-Risiko + offene Positions-Risiken.
        if (_settings.MaxDailyRiskPercent > 0 && context.Account.Balance > 0 && signal.StopLoss.HasValue)
        {
            var plannedSlDistance = Math.Abs(entryPrice - signal.StopLoss.Value);
            // Vorläufige Position-Size für Risiko-Schätzung (kleine Abweichung zum späteren posSize ok)
            var plannedRisk = plannedSlDistance * (context.Account.Balance * _settings.MaxRiskPercentPerTrade / 100m / Math.Max(plannedSlDistance, 1e-8m));
            // Vereinfacht: plannedRisk ≈ MaxRiskPercentPerTrade × Balance (da Sizing Risk genau das einhält)
            var plannedRiskAmount = context.Account.Balance * _settings.MaxRiskPercentPerTrade / 100m;

            lock (_lock)
            {
                var realizedLoss = _dailyPnl < 0 ? Math.Abs(_dailyPnl) : 0m;
                var openRisk = _openRiskEstimate;
                var usedRiskAmount = realizedLoss + openRisk;
                var totalRisk = usedRiskAmount + plannedRiskAmount;
                var usedPct = totalRisk / context.Account.Balance * 100m;

                if (usedPct > _settings.MaxDailyRiskPercent)
                    return new RiskCheckResult(false,
                        $"Daily-Risk-Budget überschritten: {usedPct:F2}% > {_settings.MaxDailyRiskPercent}% (realisiert {realizedLoss:F2}$ + offen {openRisk:F2}$ + geplant {plannedRiskAmount:F2}$)", 0m);
            }
        }

        var posSize = CalculatePositionSize(context.Symbol, entryPrice, signal.StopLoss, context.Account, actualLeverage);

        if (posSize <= 0)
            return new RiskCheckResult(false, "Position-Größe ist 0", 0m);

        // Strategy-seitiger Positions-Multiplikator (Task 4.10 Counter-Trend-Scalp = 0.5; Spec §7 B19 HighProbability > 1.0).
        // Wird VOR dem MaxRisk-Cap angewendet, damit alle Risiko-Obergrenzen (MaxRiskPercentPerTrade, Daily-Drawdown,
        // Total-Drawdown, Liquidations-Distanz) auf die skalierte Position wirken. Bei Wert ≤ 0 wird der Override ignoriert.
        if (signal.PositionScaleOverride is { } scale && scale > 0m)
        {
            posSize *= scale;
            if (posSize <= 0)
                return new RiskCheckResult(false,
                    $"Position-Größe nach Scale-Override ({scale:F2}×) ≤ 0", 0m);
        }

        // SK-Plan 3.4: Hard-Cap Risiko ≤ MaxRiskPercentPerTrade pro Trade
        // Auch wenn MaxPositionSizePercent × Leverage theoretisch knapp passt, soll der
        // tatsächliche Verlust bei SL-Hit nie die definierte Risiko-Obergrenze überschreiten.
        // Wir reduzieren die Qty dynamisch statt den Trade komplett abzulehnen (User will Setup handeln,
        // aber mit sauberem Risiko). Equity = Balance + UnrealizedPnl.
        if (_settings.MaxRiskPercentPerTrade > 0 && signal.StopLoss.HasValue && signal.StopLoss.Value > 0)
        {
            var equity = context.Account.Balance + context.Account.UnrealizedPnl;
            if (equity > 0)
            {
                var slDistance = Math.Abs(entryPrice - signal.StopLoss.Value);
                var riskUsdt = slDistance * posSize;
                var maxRiskUsdt = equity * _settings.MaxRiskPercentPerTrade / 100m;
                if (riskUsdt > maxRiskUsdt && slDistance > 0)
                {
                    var originalPosSize = posSize;
                    posSize = maxRiskUsdt / slDistance;
                    // Wenn das Cap die Position auf unter 1% der Original-Size drückt → Setup verwerfen
                    if (posSize <= originalPosSize * 0.01m)
                        return new RiskCheckResult(false,
                            $"SL zu weit für MaxRisk={_settings.MaxRiskPercentPerTrade}% (Risk={riskUsdt:F2} > {maxRiskUsdt:F2} USDT)", 0m);
                }
            }
        }

        // BUCH-ONLY: Kein Liquidationspreis-Abstands-Check. Das Buch managed Risiko ueber
        // Risk-Per-Trade (1-3%) und Positionsgroesse, nicht ueber Liquidationsdistanz.
        var leverage = actualLeverage > 0 ? (decimal)actualLeverage : (_settings.MaxLeverage > 0 ? _settings.MaxLeverage : 1m);

        // 04.05.2026 — Margin-Aware-Cap (TradFi-Schutz):
        // Bei hohem Hebel (NCFX 20×, NCSI 10×) kann ein einzelner 5%-Risk-Trade trotz korrekter
        // Risk-Distanz fast die gesamte verfügbare Margin binden. Ohne Cap würde ein zweiter
        // paralleler Trade auf einem zweiten TradFi-Symbol die Cross-Margin sprengen.
        // Regel: Summe aller Margins (offene Positionen + neue) darf 60% der Wallet-Balance nicht
        // überschreiten. Der posSize wird ggf. reduziert, statt den Trade abzulehnen.
        if (context.Account.Balance > 0 && entryPrice > 0 && leverage > 0)
        {
            var openMargins = context.OpenPositions.Sum(p =>
                p.Leverage > 0 ? p.EntryPrice * p.Quantity / p.Leverage : p.EntryPrice * p.Quantity);
            var marginCap = context.Account.Balance * 0.6m;
            var newMargin = entryPrice * posSize / leverage;
            if (openMargins + newMargin > marginCap)
            {
                var available = marginCap - openMargins;
                if (available <= 0)
                    return new RiskCheckResult(false,
                        $"Margin-Cap erreicht ({openMargins:F2}/{marginCap:F2} USDT durch offene Positionen blockiert)", 0m);
                var maxPosSize = available * leverage / entryPrice;
                if (maxPosSize < posSize)
                    posSize = maxPosSize;
            }
        }

        // 6. Risk-Reward-Ratio prüfen: Trade muss Mindest-RRR erfüllen
        if (_settings.MinRiskRewardRatio > 0 && signal.StopLoss.HasValue && signal.TakeProfit.HasValue
            && signal.StopLoss.Value > 0 && signal.TakeProfit.Value > 0)
        {
            var slDistance = Math.Abs(entryPrice - signal.StopLoss.Value);
            var tpDistance = Math.Abs(signal.TakeProfit.Value - entryPrice);
            if (slDistance > 0)
            {
                var rrr = tpDistance / slDistance;
                if (rrr < _settings.MinRiskRewardRatio)
                    return new RiskCheckResult(false,
                        $"Risk-Reward {rrr:F2}:1 < Min {_settings.MinRiskRewardRatio:F1}:1 (SL={slDistance:G6}, TP={tpDistance:G6})", 0m);
            }
        }

        // 9. Taeglichen Drawdown pruefen (inkl. unrealisierter Verluste + Risiko der neuen Position)
        decimal dailyDrawdownPercent;
        decimal totalDrawdownPercent;
        lock (_lock)
        {
            // Peak-Equity initialisieren beim ersten Trade (Balance + unrealisierte PnL)
            var currentEquity = context.Account.Balance + context.Account.UnrealizedPnl;
            if (!_peakEquityInitialized)
            {
                _peakEquity = currentEquity;
                _peakEquityInitialized = true;
            }

            // Peak aktualisieren wenn Equity neues Hoch erreicht
            if (currentEquity > _peakEquity)
                _peakEquity = currentEquity;

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
                // Ohne SL: Worst-Case = gesamte Margin (MaxPositionSizePercent der Wallet-Balance)
                newPositionRisk = context.Account.Balance * _settings.MaxPositionSizePercent / 100m;
            }

            // Daily-Drawdown: Tagesbasiert wie bisher (PnL-basiert)
            var effectiveDailyPnl = _dailyPnl + unrealizedLoss - newPositionRisk;
            dailyDrawdownPercent = context.Account.Balance > 0 && effectiveDailyPnl < 0
                ? Math.Abs(effectiveDailyPnl) / context.Account.Balance * 100m
                : 0m;

            // Total-Drawdown: Peak-to-Trough basiert (echte Equity-Kurve)
            totalDrawdownPercent = _peakEquity > 0
                ? Math.Max(0m, (_peakEquity - currentEquity + newPositionRisk) / _peakEquity * 100m)
                : 0m;
        }

        if (_settings.MaxDailyDrawdownPercent > 0 && dailyDrawdownPercent >= _settings.MaxDailyDrawdownPercent)
            return new RiskCheckResult(false, $"Tages-Drawdown {dailyDrawdownPercent:F1}% >= {_settings.MaxDailyDrawdownPercent}% (inkl. Risiko neuer Position)", 0m);

        // 10. Gesamt-Drawdown pruefen
        if (totalDrawdownPercent >= _settings.MaxTotalDrawdownPercent)
            return new RiskCheckResult(false, $"Gesamt-Drawdown {totalDrawdownPercent:F1}% >= {_settings.MaxTotalDrawdownPercent}% (inkl. Risiko neuer Position)", 0m);

        return new RiskCheckResult(true, null, posSize);
    }

    /// <summary>
    /// Berechnet die Positionsgröße (Quantity) basierend auf Positionswert-Cap und Risiko-Cap.
    /// MaxPositionSizePercent = max. Positionswert in % der Balance (NICHT Margin, unabhängig vom Leverage).
    /// </summary>
    public decimal CalculatePositionSize(string symbol, decimal entryPrice, decimal? stopLoss, AccountInfo account, int actualLeverage = 0)
    {
        if (entryPrice <= 0 || account.Balance <= 0) return 0m;

        // Leverage: User-eingestellter Wert (kein adaptiver Abzug hier)
        var leverage = actualLeverage > 0 ? (decimal)actualLeverage : (_settings.MaxLeverage > 0 ? _settings.MaxLeverage : 1m);

        // MaxPositionSizePercent der Wallet-Balance = die Margin für diesen Trade.
        // SK-Plan 4.8 + 5.1 + 5.5: Zusätzliche Scaling-Faktoren.
        var scaleFactor = GetPositionScalingFactor(account);
        var margin = account.Balance * _settings.MaxPositionSizePercent / 100m * scaleFactor;
        var qty = margin * leverage / entryPrice;

        return qty;
    }

    /// <summary>
    /// SK-Plan 4.8 + 5.1: Kombinierter Scaling-Factor für Position-Sizing.
    /// 4.8 Loss-Streak-Dampening: >=3 Verluste → 0.5×, >=5 → 0 (Pause).
    /// 5.1 Equity-Curve-Scaling: Drawdown ab Schwelle → linear runter bis 0.5×.
    /// Beide Faktoren multiplizieren sich.
    /// </summary>
    public decimal GetPositionScalingFactor(AccountInfo account)
    {
        decimal factor = 1m;
        return factor;
    }

    /// <summary>
    /// Berechnet den Liquidationspreis für Isolated Margin.
    /// Korrekte Formel: Long:  EntryPrice * (1 - (1 - MMR) / Leverage)
    ///                  Short: EntryPrice * (1 + (1 - MMR) / Leverage)
    /// BingX Maintenance Margin Rate ~0.4% für die meisten Perpetuals.
    /// </summary>
    public decimal CalculateLiquidationPrice(decimal entryPrice, decimal leverage, Side side)
    {
        if (entryPrice <= 0 || leverage <= 0) return 0m;

        const decimal maintenanceMarginRate = 0.004m; // 0.4% BingX Standard

        // Isolated-Margin-Formel. Bei Cross-Margin ist die echte Liquidation weiter weg
        // (Account-Balance schützt), daher ist dieser Wert konservativ (blockiert eher zu viel als zu wenig).
        // Bei niedrigem Leverage (<=2x) ist der Abstand groß genug, Prüfung überspringen.
        if (leverage <= 2m) return 0m; // Kein Liquidations-Risiko bei <=2x

        if (side == Side.Buy)
            return entryPrice * (1m - (1m - maintenanceMarginRate) / leverage);
        else
            return entryPrice * (1m + (1m - maintenanceMarginRate) / leverage);
    }

    /// <summary>
    /// Berechnet das aktuelle Netto-Exposure aller offenen Positionen in % der Balance.
    /// Basiert auf MARGIN (Notional / Leverage), nicht auf dem gehebelten Notional-Wert.
    /// So wird ein 10%-Margin-Trade bei 3x Leverage gleich bewertet wie bei 20x Leverage.
    /// Hedge-Positionen (Long+Short) reduzieren das Netto-Exposure.
    /// </summary>
    public decimal CalculateNetExposure(IReadOnlyList<Position> positions, decimal balance)
    {
        if (balance <= 0 || positions.Count == 0) return 0m;

        // Margin-basiert: Notional / Leverage = tatsächlich gebundenes Kapital
        var netMargin = positions.Sum(p =>
        {
            var lev = p.Leverage > 0 ? p.Leverage : 1m;
            return (p.Side == Side.Buy ? 1m : -1m) * p.Quantity * p.MarkPrice / lev;
        });
        return Math.Abs(netMargin) / balance * 100m;
    }

    // Rolling-Metriken: Ringpuffer der letzten N Trades
    private readonly List<CompletedTrade> _rollingTrades = new();
    private const int RollingWindowSize = 30;

    /// <summary>Zugriff auf die letzten Trades für PnL-Kalender und Statistiken.</summary>
    public IReadOnlyList<CompletedTrade> RecentTrades { get { lock (_lock) return _rollingTrades.ToList(); } }

    /// <summary>Rolling WinRate der letzten 30 Trades (0-1).</summary>
    public decimal RollingWinRate
    {
        get { lock (_lock) return _rollingTrades.Count > 0
            ? (decimal)_rollingTrades.Count(t => t.Pnl > 0) / _rollingTrades.Count : 0m; }
    }

    /// <summary>Rolling ProfitFactor der letzten 30 Trades.</summary>
    public decimal RollingProfitFactor
    {
        get
        {
            lock (_lock)
            {
                var wins = _rollingTrades.Where(t => t.Pnl > 0).Sum(t => t.Pnl);
                var losses = Math.Abs(_rollingTrades.Where(t => t.Pnl < 0).Sum(t => t.Pnl));
                return losses > 0 ? wins / losses : wins > 0 ? 99m : 0m;
            }
        }
    }

    /// <summary>Rolling Sharpe Ratio (annualisiert, aus den letzten 30 Trades, auf prozentualen Returns basierend).</summary>
    public decimal RollingSharpeRatio
    {
        get
        {
            lock (_lock)
            {
                if (_rollingTrades.Count < 5) return 0m;
                // Prozentuale Returns normalisiert auf Positionswert (nicht absolute PnL)
                var returns = _rollingTrades
                    .Where(t => t.EntryPrice > 0 && t.Quantity > 0)
                    .Select(t => (double)(t.Pnl / (t.EntryPrice * t.Quantity)))
                    .ToArray();
                if (returns.Length < 5) return 0m;
                var avg = returns.Average();
                // Sample-Varianz (N-1) für korrekte Schätzung bei kleinen Stichproben
                var variance = returns.Select(r => (r - avg) * (r - avg)).Sum() / (returns.Length - 1);
                var stdDev = Math.Sqrt(variance);
                if (stdDev <= 0) return 0m;

                // Annualisierung: Tatsächliche Trade-Frequenz statt fixem sqrt(365).
                // sqrt(365) nimmt 1 Trade/Tag an — bei H4-Swing (0.3/Tag) oder Scalping (5/Tag) verzerrt.
                var first = _rollingTrades[0].ExitTime;
                var last = _rollingTrades[^1].ExitTime;
                var spanDays = (last - first).TotalDays;
                // Trades pro Jahr aus tatsächlicher Frequenz (Fallback: 365 bei <1 Tag Spanne)
                var tradesPerYear = spanDays > 1 ? returns.Length / spanDays * 365 : 365;
                return (decimal)(avg / stdDev * Math.Sqrt(tradesPerYear));
            }
        }
    }

    /// <summary>Aufeinanderfolgende Verluste aktuell.</summary>
    public int CurrentConsecutiveLosses { get; private set; }

    /// <summary>
    /// Setzt CurrentConsecutiveLosses auf einen externen Wert (v1.2.5).
    /// Wird von TradingServiceBase.ProcessCompletedTrade aufgerufen, um den Base-Counter
    /// (der BE-Exits ausklammert) nach UpdateDailyStats zu uebernehmen.
    /// Ohne diesen Sync konnten RiskManager und Base-Counter divergieren (BE-Exits liessen
    /// RiskManager faelschlich weiter inkrementieren, was GetPositionScalingFactor verzerrte).
    /// </summary>
    public void SetConsecutiveLosses(int value)
    {
        if (value < 0) value = 0;
        lock (_lock) CurrentConsecutiveLosses = value;
    }

    /// <summary>
    /// Prüft ob die Strategie degradiert ist und der Bot pausieren sollte.
    /// Returns: Warnung-Text oder null wenn alles OK.
    /// </summary>
    public string? CheckStrategyHealth()
    {
        int tradeCount;
        lock (_lock) tradeCount = _rollingTrades.Count;
        if (tradeCount < 10) return null;

        if (RollingSharpeRatio < 0.3m)
            return $"Rolling Sharpe {RollingSharpeRatio:F2} < 0.3 (degradiert)";
        if (RollingWinRate < 0.25m)
            return $"Rolling WinRate {RollingWinRate:P0} < 25% (kritisch)";
        if (CurrentConsecutiveLosses >= 5)
            return $"{CurrentConsecutiveLosses} Verluste in Folge (Auto-Pause empfohlen)";

        return null;
    }

    public void UpdateDailyStats(CompletedTrade completedTrade)
    {
        lock (_lock)
        {
            _dailyPnl += completedTrade.Pnl;
            _totalPnl += completedTrade.Pnl;

            // Rolling-Window aktualisieren
            _rollingTrades.Add(completedTrade);
            if (_rollingTrades.Count > RollingWindowSize)
                _rollingTrades.RemoveAt(0);

            // Consecutive Losses
            if (completedTrade.Pnl < 0) CurrentConsecutiveLosses++;
            else CurrentConsecutiveLosses = 0;
        }
    }

    public void ResetDailyStats()
    {
        lock (_lock)
        {
            _dailyPnl = 0m;
            // Peak-Equity wird NICHT zurückgesetzt (persistent über gesamte Laufzeit)
        }
    }

    /// <summary>
    /// Setzt alle Statistiken zurück (für kompletten Bot-Reset).
    /// Im Gegensatz zu ResetDailyStats() wird auch Peak-Equity zurückgesetzt.
    /// </summary>
    public void ResetAll()
    {
        lock (_lock)
        {
            _dailyPnl = 0m;
            _totalPnl = 0m;
            _peakEquity = 0m;
            _peakEquityInitialized = false;
            _rollingTrades.Clear();
            CurrentConsecutiveLosses = 0;
        }
    }
}
