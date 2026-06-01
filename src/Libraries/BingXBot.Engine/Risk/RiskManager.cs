using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
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

        // Phase 18 / A4 — Cluster-Korrelations-Limit (User-Ausnahme, opt-in).
        // Schuetzt vor "3× BTC durch BTC/ETH/SOL parallel"-Disasters bei Crypto-Flash-Crashes.
        // Nur greifen wenn Setting aktiviert UND Symbol in einem definierten Cluster (Other → Filter no-op).
        if (_settings.MaxCorrelatedExposurePercent > 0 && context.Account.Balance > 0)
        {
            var newCluster = AssetClusterClassifier.Classify(context.Symbol);
            if (newCluster != AssetCluster.Other && newCluster != AssetCluster.CryptoOther)
            {
                var clusterMargins = context.OpenPositions
                    .Where(p => AssetClusterClassifier.Classify(p.Symbol) == newCluster)
                    .Sum(p => p.Leverage > 0 ? p.EntryPrice * p.Quantity / p.Leverage : p.EntryPrice * p.Quantity);

                // Geplante neue Margin: konservativ als Risk-Per-Trade-Cap (vor Position-Sizing-Fluktuation).
                var plannedMargin = context.Account.Balance * _settings.MaxPositionSizePercent / 100m;
                var totalClusterMargin = clusterMargins + plannedMargin;
                var clusterPct = totalClusterMargin / context.Account.Balance * 100m;

                if (clusterPct > _settings.MaxCorrelatedExposurePercent)
                    return new RiskCheckResult(false,
                        $"Cluster-Limit {newCluster}: {clusterPct:F1}% > {_settings.MaxCorrelatedExposurePercent}% (offene {clusterMargins:F2}$ + geplant {plannedMargin:F2}$)", 0m);
            }

            // Aggregiertes gleichgerichtetes Crypto-Exposure (Audit-Fix D): Die feinen Cluster
            // (BTC/ETH/AltL1/AltDefi/Meme) umgehen das Cluster-Limit, sind bei einem BTC-Flash-Crash
            // aber ALLE korreliert (geballtes BTC-Beta). Daher zusaetzlich die Summe aller
            // gleichgerichteten (Long bzw. Short) Crypto-Margins gegen ein groesszuegigeres
            // Aggregat-Limit (1.5× Cluster-Schwelle) pruefen.
            if (BingXBot.Core.Helpers.SymbolClassifier.Classify(context.Symbol) == BingXBot.Core.Enums.MarketCategory.Crypto)
            {
                var newSide = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
                var sameSideCryptoMargin = context.OpenPositions
                    .Where(p => p.Side == newSide
                        && BingXBot.Core.Helpers.SymbolClassifier.Classify(p.Symbol) == BingXBot.Core.Enums.MarketCategory.Crypto)
                    .Sum(p => p.Leverage > 0 ? p.EntryPrice * p.Quantity / p.Leverage : p.EntryPrice * p.Quantity);
                var plannedMargin = context.Account.Balance * _settings.MaxPositionSizePercent / 100m;
                // Feste Untergrenze 40 %: greift nur bei echtem gleichgerichtetem Klumpen, nicht bei
                // scharfen Cluster-Schwellen (sonst wuerde ein knappes Cluster-Limit hier alles blocken).
                var aggregateLimit = Math.Max(_settings.MaxCorrelatedExposurePercent * 1.5m, 40m);
                var aggPct = (sameSideCryptoMargin + plannedMargin) / context.Account.Balance * 100m;
                if (aggPct > aggregateLimit)
                    return new RiskCheckResult(false,
                        $"Crypto-Netto-{newSide}-Limit: {aggPct:F1}% > {aggregateLimit:F1}% (gleichgerichtetes Crypto-Klumpenrisiko)", 0m);
            }
        }

        // 4. Position-Größe berechnen mit tatsächlichem Leverage (nicht MaxLeverage)
        var entryPrice = signal.EntryPrice ?? context.CurrentTicker.LastPrice;

        // Explizite Balance-Prüfung mit klarer Meldung (erleichtert Debugging bei Drawdown=100%)
        if (context.Account.AvailableBalance <= 0)
            return new RiskCheckResult(false, "Keine verfügbare Balance — kein Trade möglich", 0m);

        // Phase 18 / B1 — News-Blackout zweite Schicht (Strategy ist erste). Liest Pre-Resolved
        // aus dem MarketContext, vermeidet damit den fruehren GetAwaiter().GetResult()-Call pro
        // Trade-Validierung. Wenn weder Pre-Resolved noch Calendar verfuegbar sind: graceful
        // degradation (Bot tradet weiter, B4 Health-Check meldet defekten News-Service separat).
        if (_settings.NewsBlackoutMinutes > 0 && !string.IsNullOrEmpty(context.ResolvedNewsBlackoutEvent))
            return new RiskCheckResult(false, $"News-Blackout: {context.ResolvedNewsBlackoutEvent}", 0m);

        // SK-Plan 3.5: Max Daily Loss Circuit-Breaker
        // Bezieht realisierte UND offene Buchverluste ein: ein grosser unrealisierter Verlust soll
        // genauso neue Entries blockieren wie realisierter (sonst eroeffnet der Bot weiter, waehrend
        // offene Positionen tief im Minus stehen). Nur die negativen UnrealizedPnl summieren —
        // Gewinne einer Position duerfen die Verluste einer anderen nicht maskieren.
        if (_settings.MaxDailyLossPercent > 0 && context.Account.Balance > 0)
        {
            decimal unrealizedLoss = 0m;
            for (var i = 0; i < context.OpenPositions.Count; i++)
                if (context.OpenPositions[i].UnrealizedPnl < 0)
                    unrealizedLoss += context.OpenPositions[i].UnrealizedPnl; // <= 0
            lock (_lock)
            {
                var combined = _dailyPnl + unrealizedLoss; // beide <= 0
                if (combined < 0)
                {
                    var lossPct = Math.Abs(combined) / context.Account.Balance * 100m;
                    if (lossPct >= _settings.MaxDailyLossPercent)
                        return new RiskCheckResult(false,
                            $"Daily-Loss-Circuit {lossPct:F1}% >= {_settings.MaxDailyLossPercent}% (realisiert {_dailyPnl:F2}$ + offen {unrealizedLoss:F2}$)", 0m);
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

        // Loss-Streak-Pause als EXPLIZITER Reject (statt generisches "Position-Groesse ist 0" nachdem
        // GetPositionScalingFactor 0 liefert). Macht die Pause im Decision-Trail eindeutig sichtbar und
        // spart die ATR-Berechnung + Sizing. Schwelle = LossStreakPauseAtCount.
        if (_settings.EnableLossStreakDampening)
        {
            int losses;
            lock (_lock) losses = CurrentConsecutiveLosses;
            var pauseAt = Math.Max(1, _settings.LossStreakPauseAtCount);
            if (losses >= pauseAt)
                return new RiskCheckResult(false,
                    $"Loss-Streak-Pause aktiv ({losses} >= {pauseAt} Verluste in Folge)", 0m);
        }

        // Phase 18 / A5 — ATR-Prozent fuer Volatility-Targeting durchreichen, sofern aktiviert UND Candles vorhanden.
        decimal atrPct = 0m;
        if (_settings.EnableVolatilityTargeting && context.Candles.Count >= 14 && entryPrice > 0)
        {
            var atrSeries = IndicatorHelper.CalculateAtr(context.Candles, 14);
            if (atrSeries.Count > 0 && atrSeries[^1].HasValue)
                atrPct = atrSeries[^1]!.Value / entryPrice * 100m;
        }
        var posSize = CalculatePositionSize(context.Symbol, entryPrice, signal.StopLoss, context.Account, actualLeverage, atrPct);

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
                    // Wenn das Cap die Position auf unter MinPositionSizeRetentionPercent der Original-Size drückt → Setup verwerfen.
                    // 0 = Schwelle aus (Trade läuft mit jeder Größe weiter).
                    var minRetention = Math.Clamp(_settings.MinPositionSizeRetentionPercent, 0m, 1m);
                    if (minRetention > 0m && posSize <= originalPosSize * minRetention)
                        return new RiskCheckResult(false,
                            $"SL zu weit für MaxRisk={_settings.MaxRiskPercentPerTrade}% (Risk={riskUsdt:F2} > {maxRiskUsdt:F2} USDT, Rest={posSize / originalPosSize:P0} < {minRetention:P0})", 0m);
                }
            }
        }

        var leverage = actualLeverage > 0 ? (decimal)actualLeverage : (_settings.MaxLeverage > 0 ? _settings.MaxLeverage : 1m);

        // Liquidations-Safety-Net (User-Schutz, KEIN Buch-Risk-Management): Liegt der geplante SL
        // JENSEITS des Liquidationspreises, wird die Position liquidiert BEVOR der SL greift —
        // ein garantierter Totalverlust statt kontrolliertem SL-Verlust. Solche Trades lehnen wir ab.
        // Greift nur bei Leverage > 2x (darunter gibt CalculateLiquidationPrice 0 zurueck) — relevant
        // v.a. fuer hochgehebelte TradFi-Perps (NCFX 20x, NCSI 10x). Aendert keine Buch-Mechanik,
        // sondern verhindert nur einen sicheren Margin-Call. (Buch-Strip entfernte den Distanz-CHECK;
        // dieser Crash-Schutz ist davon unberuehrt.)
        if (signal.StopLoss is { } slForLiq && slForLiq > 0 && leverage > 2m)
        {
            var liqSide = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
            var liqPrice = CalculateLiquidationPrice(entryPrice, leverage, liqSide);
            if (liqPrice > 0)
            {
                var slBeyondLiq = liqSide == Side.Buy ? slForLiq <= liqPrice : slForLiq >= liqPrice;
                if (slBeyondLiq)
                    return new RiskCheckResult(false,
                        $"SL jenseits Liquidation (SL={slForLiq:F8} vs Liq={liqPrice:F8} @ {leverage:F0}x) — garantierter Totalverlust, abgelehnt", 0m);
            }
        }

        // Margin-Aware-Cap (TradFi-Schutz, konfigurierbar):
        // Bei hohem Hebel (NCFX 20×, NCSI 10×) kann ein einzelner 5%-Risk-Trade trotz korrekter
        // Risk-Distanz fast die gesamte verfügbare Margin binden. Ohne Cap würde ein zweiter
        // paralleler Trade auf einem zweiten TradFi-Symbol die Cross-Margin sprengen.
        // Cap = Σ aller Margins (offene + neue) ≤ MaxTotalMarginPercent der Wallet-Balance.
        // posSize wird ggf. reduziert; bei vollständig genutztem Cap → Reject.
        // MaxTotalMarginPercent = 0 → Filter aus.
        var marginCapPercent = _settings.MaxTotalMarginPercent;
        if (marginCapPercent > 0m && context.Account.Balance > 0 && entryPrice > 0 && leverage > 0)
        {
            // Phase 18 / C1 — for-Loop statt LINQ Sum mit Closure (Hot-Path-Allocation eliminieren).
            decimal openMargins = 0m;
            for (var i = 0; i < context.OpenPositions.Count; i++)
            {
                var p = context.OpenPositions[i];
                openMargins += p.Leverage > 0 ? p.EntryPrice * p.Quantity / p.Leverage : p.EntryPrice * p.Quantity;
            }
            var marginCap = context.Account.Balance * marginCapPercent / 100m;
            var newMargin = entryPrice * posSize / leverage;
            if (openMargins + newMargin > marginCap)
            {
                var available = marginCap - openMargins;
                if (available <= 0)
                    return new RiskCheckResult(false,
                        $"Margin-Cap erreicht ({openMargins:F2}/{marginCap:F2} USDT durch offene Positionen blockiert, {marginCapPercent:F0}% der Balance)", 0m);
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

            // Drawdown-Aggregation OHNE planned-newPositionRisk:
            // MaxRiskPercentPerTrade-Cap (oben) hat das Trade-Risiko bereits hart begrenzt — wenn wir den
            // gleichen Betrag noch einmal in beide DD-Schwellen einrechnen, ist der Risiko-Schirm dreifach
            // aufgespannt. Resultat: realisierte + unrealisierte Verluste reichen weiterhin, neue Setups
            // werden aber nicht mehr für Worst-Case-Szenarien doppelt bestraft.
            var effectiveDailyPnl = _dailyPnl + unrealizedLoss;
            dailyDrawdownPercent = context.Account.Balance > 0 && effectiveDailyPnl < 0
                ? Math.Abs(effectiveDailyPnl) / context.Account.Balance * 100m
                : 0m;

            // Total-Drawdown: Peak-to-Trough basiert (echte Equity-Kurve)
            totalDrawdownPercent = _peakEquity > 0
                ? Math.Max(0m, (_peakEquity - currentEquity) / _peakEquity * 100m)
                : 0m;
        }

        if (_settings.MaxDailyDrawdownPercent > 0 && dailyDrawdownPercent >= _settings.MaxDailyDrawdownPercent)
            return new RiskCheckResult(false, $"Tages-Drawdown {dailyDrawdownPercent:F1}% >= {_settings.MaxDailyDrawdownPercent}%", 0m);

        // 10. Gesamt-Drawdown pruefen
        if (totalDrawdownPercent >= _settings.MaxTotalDrawdownPercent)
            return new RiskCheckResult(false, $"Gesamt-Drawdown {totalDrawdownPercent:F1}% >= {_settings.MaxTotalDrawdownPercent}%", 0m);

        return new RiskCheckResult(true, null, posSize);
    }

    /// <summary>
    /// Berechnet die Positionsgröße (Quantity) basierend auf Positionswert-Cap und Risiko-Cap.
    /// MaxPositionSizePercent = max. Positionswert in % der Balance (NICHT Margin, unabhängig vom Leverage).
    /// </summary>
    public decimal CalculatePositionSize(string symbol, decimal entryPrice, decimal? stopLoss, AccountInfo account, int actualLeverage = 0)
        => CalculatePositionSize(symbol, entryPrice, stopLoss, account, actualLeverage, atrPercent: 0m);

    /// <summary>
    /// Phase 18 / A5 — Erweiterte Variante mit ATR-Prozent-Wert fuer Volatility-Targeting.
    /// Wenn <see cref="RiskSettings.EnableVolatilityTargeting"/> true UND <paramref name="atrPercent"/> &gt; 0:
    /// Quantity wird um <c>min(VolScaleCap, VolatilityTargetPercent / atrPercent)</c> skaliert.
    /// Bei <paramref name="atrPercent"/> = 0 oder Setting aus: kein Scaling (= Legacy-Verhalten).
    /// </summary>
    public decimal CalculatePositionSize(string symbol, decimal entryPrice, decimal? stopLoss, AccountInfo account, int actualLeverage, decimal atrPercent)
    {
        if (entryPrice <= 0 || account.Balance <= 0) return 0m;

        // Leverage: User-eingestellter Wert (kein adaptiver Abzug hier)
        var leverage = actualLeverage > 0 ? (decimal)actualLeverage : (_settings.MaxLeverage > 0 ? _settings.MaxLeverage : 1m);

        // MaxPositionSizePercent der Wallet-Balance = die Margin für diesen Trade.
        // SK-Plan 4.8 + 5.1 + 5.5: Zusätzliche Scaling-Faktoren.
        var scaleFactor = GetPositionScalingFactor(account);
        var margin = account.Balance * _settings.MaxPositionSizePercent / 100m * scaleFactor;
        var qty = margin * leverage / entryPrice;

        // Phase 18 / A5 — Volatility-Targeting (opt-in).
        if (_settings.EnableVolatilityTargeting && atrPercent > 0 && _settings.VolatilityTargetPercent > 0)
        {
            var volScale = _settings.VolatilityTargetPercent / atrPercent;
            var cap = _settings.VolatilityScaleCap > 0 ? _settings.VolatilityScaleCap : 1.5m;
            volScale = Math.Min(cap, volScale);
            qty *= volScale;
        }

        return qty;
    }

    /// <summary>
    /// SK-Plan 4.8 + 5.1: Kombinierter Scaling-Factor für Position-Sizing.
    /// 4.8 Loss-Streak-Dampening (Buch S.13): >=3 Verluste → 0.5×, >=5 → 0 (Pause).
    /// 5.1 Equity-Curve-Scaling: Drawdown vom Peak ab Schwelle → linear runter bis 0.5×.
    /// Beide Faktoren multiplizieren sich. Phase 18 (09.05.2026) — vorher toter Stub.
    /// </summary>
    public decimal GetPositionScalingFactor(AccountInfo account)
    {
        decimal factor = 1m;

        // 4.8 Loss-Streak-Dampening (Buch S.13). Schwellen sind konfigurierbar
        // (LossStreakHalveAtCount / LossStreakPauseAtCount) — Buch-Werte 3/5 als Default;
        // gelockerter User-Default 4/7 in den Settings dokumentiert.
        if (_settings.EnableLossStreakDampening)
        {
            int losses;
            lock (_lock) losses = CurrentConsecutiveLosses;
            var pauseAt = Math.Max(1, _settings.LossStreakPauseAtCount);
            var halveAt = Math.Max(1, _settings.LossStreakHalveAtCount);
            if (losses >= pauseAt) return 0m;            // Pause — keine neue Position
            if (losses >= halveAt) factor *= 0.5m;       // Position halbieren
        }

        // 5.1 Equity-Curve-Scaling (linear)
        // Erfordert initialisierten Peak (sonst kein verlässlicher Drawdown-Bezug).
        if (_settings.EnableEquityCurveScaling && _peakEquityInitialized && _peakEquity > 0)
        {
            var equity = account.Balance + account.UnrealizedPnl;
            var ddPct = (_peakEquity - equity) / _peakEquity * 100m;
            var threshold = _settings.EquityCurveScalingThresholdPercent;
            if (ddPct > threshold)
            {
                // Bei (threshold + 10%) Drawdown ist factor bei 0.5×.
                var lerp = Math.Min(1m, (ddPct - threshold) / 10m);
                factor *= 1m - 0.5m * lerp;
            }
        }

        return Math.Max(0m, factor);
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

    // Rolling-Metriken: Ringpuffer der letzten N Trades.
    // Phase 18 / H1 — Queue<T> statt List<T> fuer O(1) Dequeue (vorher List.RemoveAt(0) = O(n)).
    private readonly Queue<CompletedTrade> _rollingTrades = new();
    private const int RollingWindowSize = 30;
    // Phase 18 / C1 — Cache fuer RecentTrades-Snapshot. Vorher allokierte jeder UI-Read ein
    // neues `ToList()`. Cache wird bei UpdateDailyStats/ResetAll invalidated und beim naechsten
    // Read 1× neu gebaut. Phase H1 — alle Lese-Properties (Sharpe/WinRate/ProfitFactor) gehen
    // jetzt ueber dieses Snapshot statt Queue direkt → keine Mehrfach-Enumeration der Queue.
    private CompletedTrade[]? _recentTradesSnapshot;

    /// <summary>Phase 18 / H1 — Lazy-rebuild des Snapshots (muss innerhalb des _lock laufen).</summary>
    private CompletedTrade[] GetRecentTradesSnapshotLocked()
    {
        if (_recentTradesSnapshot == null)
            _recentTradesSnapshot = _rollingTrades.ToArray();
        return _recentTradesSnapshot;
    }

    /// <summary>Zugriff auf die letzten Trades für PnL-Kalender und Statistiken (gecached, lazy rebuild).</summary>
    public IReadOnlyList<CompletedTrade> RecentTrades
    {
        get { lock (_lock) return GetRecentTradesSnapshotLocked(); }
    }

    /// <summary>Rolling WinRate der letzten 30 Trades (0-1).</summary>
    public decimal RollingWinRate
    {
        get
        {
            lock (_lock)
            {
                var snapshot = GetRecentTradesSnapshotLocked();
                if (snapshot.Length == 0) return 0m;
                int wins = 0;
                for (var i = 0; i < snapshot.Length; i++)
                    if (snapshot[i].Pnl > 0) wins++;
                return (decimal)wins / snapshot.Length;
            }
        }
    }

    /// <summary>Rolling ProfitFactor der letzten 30 Trades.</summary>
    public decimal RollingProfitFactor
    {
        get
        {
            lock (_lock)
            {
                var snapshot = GetRecentTradesSnapshotLocked();
                decimal wins = 0m, losses = 0m;
                for (var i = 0; i < snapshot.Length; i++)
                {
                    var pnl = snapshot[i].Pnl;
                    if (pnl > 0) wins += pnl;
                    else if (pnl < 0) losses += -pnl;
                }
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
                var snapshot = GetRecentTradesSnapshotLocked();
                if (snapshot.Length < 5) return 0m;
                // Prozentuale Returns normalisiert auf Positionswert (nicht absolute PnL).
                // Phase H1 — for-Loop statt LINQ, fuellt direkt in Pre-Allocated double[].
                var validCount = 0;
                for (var i = 0; i < snapshot.Length; i++)
                    if (snapshot[i].EntryPrice > 0 && snapshot[i].Quantity > 0) validCount++;
                if (validCount < 5) return 0m;

                var returns = new double[validCount];
                var idx = 0;
                for (var i = 0; i < snapshot.Length; i++)
                {
                    var t = snapshot[i];
                    if (t.EntryPrice > 0 && t.Quantity > 0)
                        returns[idx++] = (double)(t.Pnl / (t.EntryPrice * t.Quantity));
                }

                double sum = 0;
                for (var i = 0; i < returns.Length; i++) sum += returns[i];
                var avg = sum / returns.Length;
                double sumSqDiff = 0;
                for (var i = 0; i < returns.Length; i++) { var d = returns[i] - avg; sumSqDiff += d * d; }
                // Sample-Varianz (N-1) für korrekte Schätzung bei kleinen Stichproben
                var variance = sumSqDiff / (returns.Length - 1);
                var stdDev = Math.Sqrt(variance);
                if (stdDev <= 0) return 0m;

                // Annualisierung: Tatsächliche Trade-Frequenz statt fixem sqrt(365).
                // sqrt(365) nimmt 1 Trade/Tag an — bei H4-Swing (0.3/Tag) oder Scalping (5/Tag) verzerrt.
                var first = snapshot[0].ExitTime;
                var last = snapshot[snapshot.Length - 1].ExitTime;
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
    /// Phase 18 / B1 — Pre-Resolved News-Blackout-Event-Name (oder null) als async Helper.
    /// Wird vom <c>TradingServiceBase</c> einmal pro Scan-Tick aufgerufen und in den
    /// <see cref="MarketContext.ResolvedNewsBlackoutEvent"/> aller Symbol-Evaluationen gepusht —
    /// ersetzt den frueheren GetAwaiter().GetResult()-Pfad pro Symbol.
    /// </summary>
    public async Task<string?> ResolveActiveNewsBlackoutAsync(CancellationToken ct = default)
    {
        if (_newsCalendar == null || _settings.NewsBlackoutMinutes <= 0) return null;
        try
        {
            var ev = await _newsCalendar.GetActiveBlackoutEventAsync(
                DateTime.UtcNow, _settings.NewsBlackoutMinutes, ct).ConfigureAwait(false);
            ResetNewsCheckFailures();
            if (ev == null) return null;
            return $"{ev.Name} ({ev.Country} {ev.TimeUtc:HH:mm} UTC)";
        }
        catch (Exception ex)
        {
            // Phase 18 / B4 — Failure-Counter + structured Log statt stillem Schlucken.
            var newCount = Interlocked.Increment(ref _newsCheckFailureCount);
            _logger.LogWarning(ex, "News-Blackout-Probe fehlgeschlagen (Failure #{Count}) — Trade wird ohne Filter durchgewinkt", newCount);
            // Phase 18 / H2 — Edge-Transition: ab Threshold die UI informieren.
            if (newCount >= NewsServiceDegradedThreshold && !_lastNewsServiceDegraded)
            {
                _lastNewsServiceDegraded = true;
                NewsServiceHealthChanged?.Invoke(true, newCount, $"News-Probe {newCount}× in Folge fehlgeschlagen ({ex.GetType().Name}).");
            }
            return null;
        }
    }

    /// <summary>Phase 18 / B4 — Anzahl konsekutiver News-Service-Failures.</summary>
    private int _newsCheckFailureCount;
    public int NewsCheckFailureCount => _newsCheckFailureCount;

    /// <summary>Phase 18 / B4 — Reset des News-Failure-Counters bei erfolgreichem Probe.</summary>
    public void ResetNewsCheckFailures()
    {
        var was = Interlocked.Exchange(ref _newsCheckFailureCount, 0);
        // Phase 18 / H2 — Recovery-Edge-Transition: war degraded → Recovery-Event publishen.
        if (was >= NewsServiceDegradedThreshold && !_lastNewsServiceDegraded)
        {
            // Bereits unter Threshold gemeldet, nichts zu tun.
        }
        else if (was >= NewsServiceDegradedThreshold && _lastNewsServiceDegraded)
        {
            _lastNewsServiceDegraded = false;
            NewsServiceHealthChanged?.Invoke(false, 0, "Recovery: News-Probe wieder erfolgreich.");
        }
    }

    /// <summary>Phase 18 / H2 — Schwelle ab der News-Service als degraded gemeldet wird (5 Failures).</summary>
    private const int NewsServiceDegradedThreshold = 5;
    /// <summary>Phase 18 / H2 — Edge-Transition-State fuer NewsServiceHealthChanged.</summary>
    private bool _lastNewsServiceDegraded;
    /// <summary>
    /// Phase 18 / H2 — Callback-Hook fuer Edge-Transition (degraded → recovered und umgekehrt).
    /// TradingServiceBase verdrahtet das auf <c>LocalBotEventStream.PublishNewsServiceDegraded</c>.
    /// Args: (isDegraded, failureCount, reason).
    /// </summary>
    public Action<bool, int, string?>? NewsServiceHealthChanged { get; set; }

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
        var lossPauseAt = Math.Max(1, _settings.LossStreakPauseAtCount);
        if (CurrentConsecutiveLosses >= lossPauseAt)
            return $"{CurrentConsecutiveLosses} Verluste in Folge (≥ {lossPauseAt} → Auto-Pause empfohlen)";

        return null;
    }

    public void UpdateDailyStats(CompletedTrade completedTrade)
    {
        lock (_lock)
        {
            _dailyPnl += completedTrade.Pnl;
            _totalPnl += completedTrade.Pnl;

            // Rolling-Window aktualisieren — Phase H1 — Queue<T> mit O(1) Enqueue/Dequeue
            // (vorher List<T>.RemoveAt(0) = O(n) Memory-Move bei jedem Trade).
            _rollingTrades.Enqueue(completedTrade);
            while (_rollingTrades.Count > RollingWindowSize)
                _rollingTrades.Dequeue();

            // Phase 18 / C1 — Snapshot-Cache invalidieren (lazy rebuild beim naechsten Lese-Property).
            _recentTradesSnapshot = null;

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
            _recentTradesSnapshot = null; // Phase 18 / C1
            CurrentConsecutiveLosses = 0;
        }
    }

    /// <summary>
    /// Rehydriert nach einem Engine-Restart den realisierten Tages-PnL, den kumulativen PnL und die
    /// Peak-Equity aus der Persistenz. Ohne diese Rekonstruktion startet der RiskManager nach JEDEM
    /// Neustart mit dailyPnl=0/totalPnl=0/peakEquity=0 → der Daily-Loss-Circuit und der
    /// Total-Drawdown-Schutz sind amnesisch und greifen erst wieder, wenn das Limit ein zweites Mal
    /// (ab 0 gerechnet) erreicht wird. Wird beim Live-Start aus den heutigen + allen Live-Trades
    /// sowie dem hoechsten Equity-Snapshot aufgerufen.
    /// </summary>
    /// <param name="todaysRealizedPnl">Summe der heute (UTC) realisierten Trade-PnLs.</param>
    /// <param name="totalRealizedPnl">Summe aller realisierten Trade-PnLs (Laufzeit-Total).</param>
    /// <param name="peakEquity">Hoechster bekannter Equity-Stand; ≤ 0 laesst die Peak-Equity uninitialisiert.</param>
    public void RestoreStats(decimal todaysRealizedPnl, decimal totalRealizedPnl, decimal peakEquity)
    {
        lock (_lock)
        {
            _dailyPnl = todaysRealizedPnl;
            _totalPnl = totalRealizedPnl;
            if (peakEquity > 0m)
            {
                _peakEquity = peakEquity;
                _peakEquityInitialized = true;
            }
        }
    }
}
