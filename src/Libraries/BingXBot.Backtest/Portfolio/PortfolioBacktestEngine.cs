using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Backtest.Reports;
using BingXBot.Backtest.Simulation;
using BingXBot.Engine.Filters;
using BingXBot.Engine.Indicators;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies;
using Microsoft.Extensions.Logging;

namespace BingXBot.Backtest.Portfolio;

/// <summary>
/// Portfolio-Backtest ueber EIN gemeinsames Konto fuer alle Symbole, zeitlich gemergt.
/// Spiegelbild des Live-Bots: die konto-weiten Risk-Gates (MaxOpenPositions, MaxTotalMargin,
/// Korrelations-Cluster, Daily-Loss/Drawdown-Circuit) sehen ALLE offenen Positionen gleichzeitig,
/// und das risk-basierte Sizing teilt sich die EINE (sinkende/steigende) Equity — anders als der
/// alte Lab-Pfad, der pro Symbol eine eigene <see cref="BacktestEngine"/> mit frischem 1000-USDT-Konto
/// fuhr und die PnLs nur summierte (Gates feuerten dadurch NIE).
///
/// Fokus: Live-Strategie <c>TrendFollow-Fast</c> (H4-only, <see cref="IStrategy.RequiresHigherTimeframeContext"/>
/// = false, kein Entry-TF-Sub-Loop) → die Strategie-Evaluation pro Symbol ist der einfache Direktpfad
/// (ein MarketContext pro H4-Kerze), wie der else-Zweig in <see cref="BacktestEngine.RunAsync"/>.
/// Exit-/Entry-Verarbeitung wird ueber <see cref="BacktestExitProcessor"/> / <see cref="BacktestEntryProcessor"/>
/// geteilt — keine Duplikation der SL/TP/BE/Partial-Logik.
/// </summary>
public sealed class PortfolioBacktestEngine
{
    private readonly IPublicMarketDataClient _publicClient;
    private readonly ISymbolInfoProvider? _symbolInfo;
    private readonly ILogger _logger;

    /// <summary>Mindestanzahl Nav-Kerzen, sonst gilt das Symbol als nicht (durchgehend) gelistet und wird uebersprungen.</summary>
    private const int MinCandles = 50;

    /// <summary>Equity-Snapshot ~1×/Tag: bei H4 = 6 Kerzen/Tag → alle 6 Timeline-Schritte.</summary>
    private const int EquitySnapshotEverySteps = 6;

    public PortfolioBacktestEngine(IPublicMarketDataClient publicClient, ISymbolInfoProvider? symbolInfo, ILogger logger)
    {
        _publicClient = publicClient;
        _symbolInfo = symbolInfo;
        _logger = logger;
    }

    public async Task<PerformanceReport> RunAsync(
        IReadOnlyList<string> symbols,
        TimeFrame navTf,
        DateTime from,
        DateTime to,
        BotSettings settings,
        string strategyName = "TrendFollow-Fast",
        IProgress<int>? progress = null,
        CancellationToken ct = default,
        Func<string, IStrategy>? strategyFactory = null,
        Action<int>? onStepOpenPositions = null)
    {
        // Tests koennen eine deterministische Strategie injizieren (erzwungene Signale fuer Gates-Tests).
        // Default = StrategyFactory.Create — Lab/Produktiv-Pfad unveraendert.
        var createStrategy = strategyFactory ?? StrategyFactory.Create;
        // 1. Pro Symbol Nav-Kerzen laden, Warmup, eigene Strategie-Instanz.
        var states = new List<PortfolioSymbolState>();
        foreach (var symbol in symbols)
        {
            ct.ThrowIfCancellationRequested();
            var nav = await _publicClient.GetKlinesAsync(symbol, navTf, from, to, ct).ConfigureAwait(false);
            if (nav.Count < MinCandles)
            {
                _logger.LogWarning("Portfolio: {Symbol} {TF} hat nur {Count} Kerzen (< {Min}) — uebersprungen (nicht/teilgelistet, kein Demo-Fallback).",
                    symbol, navTf, nav.Count, MinCandles);
                continue;
            }

            // Warmup-Fenster wie Single-Symbol-Engine: 50..200, max Count-1.
            var warmupSize = Math.Min(Math.Max(50, nav.Count / 4), 200);
            warmupSize = Math.Min(warmupSize, nav.Count - 1);

            var strategy = createStrategy(strategyName);
            strategy.WarmUp(nav.Take(warmupSize).ToList());
            strategy.Reset();

            // Trading erst nach Warmup: TradingStartCloseTime = CloseTime der ersten Post-Warmup-Kerze.
            var state = new PortfolioSymbolState(symbol, nav, strategy) { TradingStartCloseTime = nav[warmupSize].CloseTime };
            states.Add(state);
        }

        if (states.Count == 0)
        {
            _logger.LogWarning("Portfolio: kein einziges Symbol mit genug Kerzen — leerer Report.");
            return new PerformanceReport();
        }

        _logger.LogInformation("Portfolio-Backtest: {Symbols} Symbole, {TF}, {From}..{To}",
            states.Count, navTf, from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"));

        // GAP 11 / GAP 4: Live-Spiegel-Vorfilter aktiv? (siehe BacktestSettings.EnableScannerPrefilter/EnableBtcHealthScale).
        var enableScannerPrefilter = settings.Backtest.EnableScannerPrefilter;
        var enableBtcHealthScale = settings.Backtest.EnableBtcHealthScale;
        if (enableScannerPrefilter || enableBtcHealthScale)
            _logger.LogInformation("Portfolio-Live-Spiegel: Scanner-Vorfilter={Scan}, BTC-Health-Scale={Btc}",
                enableScannerPrefilter, enableBtcHealthScale);

        // GAP 4: BTC-USDT D1 + H4 separat vorladen (zusaetzlich zu den Universe-Symbolen) — fuer die
        // BTC-Health-Berechnung pro Zeitschritt. Inkrementelle Slices (kein Look-Ahead) entlang der Timeline.
        // CalculateBtcHealth braucht D1 >= 55 und H4 >= 20 Kerzen — sonst bleibt btcHealth fuer den Schritt null.
        List<Candle>? btcD1 = null;
        List<Candle>? btcH4 = null;
        if (enableBtcHealthScale)
        {
            // D1 frueher beginnen lassen, damit zu Backtest-Start genug D1-Warmup (>=55) vorliegt.
            var btcFrom = from.AddDays(-120);
            btcD1 = await _publicClient.GetKlinesAsync("BTC-USDT", TimeFrame.D1, btcFrom, to, ct).ConfigureAwait(false);
            btcH4 = await _publicClient.GetKlinesAsync("BTC-USDT", TimeFrame.H4, btcFrom, to, ct).ConfigureAwait(false);
            if (btcD1.Count == 0 || btcH4.Count == 0)
            {
                _logger.LogWarning("Portfolio: BTC-Health-Scale aktiv, aber BTC-USDT D1/H4 nicht ladbar (D1={D1}, H4={H4}) — Scale wird uebersprungen.",
                    btcD1.Count, btcH4.Count);
                enableBtcHealthScale = false;
            }
        }

        // 2. EIN Konto + EIN RiskManager.
        var bt = settings.Backtest;
        using var simExchange = new SimulatedExchange(bt, _symbolInfo);
        var riskManager = new RiskManager(settings.Risk, Microsoft.Extensions.Logging.Abstractions.NullLogger<RiskManager>.Instance);

        var fundingRate = bt.SimulatedFundingRatePercent / 100m;
        simExchange.SetFundingRate(fundingRate);

        // 3. Gemeinsame Tracking-Strukturen (Key {symbol}_{side}).
        var positionSignals = new Dictionary<string, SignalResult>();
        var exitTracking = new Dictionary<string, BacktestExitState>();
        var equityCurve = new List<EquityPoint>();

        // 4. Gemergte Timeline.
        var timeline = MergedTimeline.Build(states.Select(s => (IReadOnlyList<Candle>)s.Nav));
        if (timeline.Count == 0)
            return new PerformanceReport();

        var lastDate = timeline[0].Date;
        var lastFundingTime = timeline[0];
        var lastCompletedTradeCount = 0;
        // Entry-Reihenfolge nach 24h-Volumen absteigend (Live-Scanner-Prio): Volumen-Proxy = aktuelle
        // Kerzen-Volumen × Preis (Quote-Volumen). Wird pro Timeline-Schritt fuer die aktiven Symbole sortiert.
        var activeBuffer = new List<PortfolioSymbolState>(states.Count);

        // GAP 4: inkrementelle BTC-D1/H4-Indizes entlang der Timeline (kein Look-Ahead — nur Kerzen mit CloseTime <= t).
        var btcD1Idx = -1;
        var btcH4Idx = -1;

        for (var step = 0; step < timeline.Count; step++)
        {
            ct.ThrowIfCancellationRequested();
            var t = timeline[step];
            progress?.Report((int)((double)step / timeline.Count * 100));

            simExchange.SetCurrentBacktestTime(t);

            // GAP 4: BTC-Health fuer diesen Zeitschritt aus inkrementellen D1/H4-Slices (CloseTime <= t).
            BtcHealthResult? btcHealth = null;
            if (enableBtcHealthScale)
            {
                while (btcD1Idx < btcD1!.Count - 1 && btcD1[btcD1Idx + 1].CloseTime <= t) btcD1Idx++;
                while (btcH4Idx < btcH4!.Count - 1 && btcH4[btcH4Idx + 1].CloseTime <= t) btcH4Idx++;
                // CalculateBtcHealth braucht D1 >= 55 und H4 >= 20 (Live: D1 > 55, H4 > 20).
                if (btcD1Idx + 1 >= 55 && btcH4Idx + 1 >= 20)
                {
                    var d1Slice = new CandleSlice(btcD1, 0, btcD1Idx + 1);
                    var h4Slice = new CandleSlice(btcH4!, 0, btcH4Idx + 1);
                    // Funding=0: Backtest hat keine historischen Funding-Rates pro BTC-Kerze (Live nutzt Live-Cache).
                    btcHealth = MarketFilter.CalculateBtcHealth(d1Slice, h4Slice, 0m);
                }
            }

            // a. Tageswechsel EINMAL pro Kalendertag konto-weit (nicht pro Symbol).
            if (t.Date != lastDate)
            {
                riskManager.ResetDailyStats();
                lastDate = t.Date;
            }

            // b. Symbole vorschieben + Preise ALLER Symbole mit Kerze bei t setzen (konto-weite Equity/Margin korrekt).
            activeBuffer.Clear();
            foreach (var s in states)
            {
                s.AdvanceTo(t);
                if (s.NavIdx < 0) continue;
                var candle = s.CurrentCandle;
                // Preis immer auf den zuletzt bekannten Close setzen (auch wenn das Symbol an DIESEM
                // Schritt keine frische Kerze hat) — sonst zeigt die Equity stale/fehlende Preise.
                simExchange.SetCurrentPrice(s.Symbol, candle.Close);

                if (!s.HasCandleAt(t)) continue;
                // Nur Symbole, die ihren Warmup abgeschlossen haben, sind handelbar.
                if (t < s.TradingStartCloseTime) continue;

                // Dynamische Slippage pro Symbol vorbereiten (wie Single-Engine), ATR aus den letzten 15 Kerzen.
                if (bt.UseDynamicSlippage && s.NavIdx >= 15)
                {
                    var atrSliceStart = s.NavIdx + 1 - 15;
                    IReadOnlyList<Candle> atrSlice = new CandleSlice(s.Nav, atrSliceStart, 15);
                    var atrVals = IndicatorHelper.CalculateAtr(atrSlice, 14);
                    var lastAtr = atrVals.Count > 0 && atrVals[^1].HasValue ? atrVals[^1]!.Value : 0m;

                    var volStart = Math.Max(0, s.NavIdx - 20);
                    decimal avgVol = 0m; var volCount = 0;
                    for (var v = volStart; v < s.NavIdx; v++) { avgVol += s.Nav[v].Volume; volCount++; }
                    avgVol = volCount > 0 ? avgVol / volCount : candle.Volume;
                    var volRatio = avgVol > 0 ? candle.Volume / avgVol : 1m;
                    simExchange.SetMarketConditions(s.Symbol, lastAtr, volRatio);
                }

                activeBuffer.Add(s);
            }

            // c. Funding alle 8h konto-weit auf alle offenen Positionen.
            if (bt.SimulateFundingRate && (t - lastFundingTime).TotalHours >= 8)
            {
                simExchange.ApplyFundingRate(fundingRate);
                lastFundingTime = t;
            }

            // Indikator-Cache periodisch leeren (unbegrenztes Wachstum verhindern).
            if (step % 500 == 0 && step > 0)
                IndicatorHelper.ClearCache();

            // d. NF8: konto-weiten OpenRisk auffrischen (Summe ueber ALLE offenen Positionen, portfolio-weit).
            var positions = await simExchange.GetPositionsAsync().ConfigureAwait(false);
            RefreshOpenRisk(riskManager, positions, positionSignals);

            // e. EXITS zuerst — pro aktivem Symbol nur dessen Positionen.
            foreach (var s in activeBuffer)
            {
                var symPositions = positions.Where(p => p.Symbol == s.Symbol).ToList();
                if (symPositions.Count == 0) continue;
                await BacktestExitProcessor.ProcessExitsAsync(
                    simExchange, symPositions, positionSignals, exitTracking,
                    bt, settings.Risk, s.Symbol, s.CurrentCandle, _symbolInfo).ConfigureAwait(false);
            }

            // f. ENTRIES — Reihenfolge nach 24h-Volumen absteigend (Live-Scanner-Prio: hohe Liquiditaet zuerst).
            //    Volumen-Proxy = Kerzen-Volumen × Close (Quote-Volumen-Naeherung).
            activeBuffer.Sort((a, b) =>
            {
                var va = a.CurrentCandle.Volume * a.CurrentCandle.Close;
                var vb = b.CurrentCandle.Volume * b.CurrentCandle.Close;
                return vb.CompareTo(va);
            });

            foreach (var s in activeBuffer)
            {
                var candle = s.CurrentCandle;

                // GAP 11: Live-Scanner-Vorfilter VOR der Strategie-Evaluation (wie TradingServiceBase:
                // FilterCandidatesForTimeframe → IsMarketOpen → IsSessionAllowed). Symbol/Zeitschritt, der
                // den Filter nicht passiert, erzeugt keinen Entry-Versuch. Flag aus → kein Effekt (Backward-Compat).
                if (enableScannerPrefilter)
                {
                    var scanTicker = BuildScanTicker(s, candle);
                    if (!PassesScannerFilter(scanTicker, s.Category, navTf, settings.Scanner))
                        continue;
                    if (!TradingHoursFilter.IsMarketOpen(s.Symbol, candle.CloseTime))
                        continue;
                    if (!TradingHoursFilter.IsSessionAllowed(candle.CloseTime, settings.EnabledSessions))
                        continue;
                }

                var contextCandles = s.ContextSlice(200);

                var halfSpread = candle.Close * bt.SpreadPercent / 100m / 2m;
                var ticker = new Ticker(s.Symbol, candle.Close,
                    candle.Close - halfSpread, candle.Close + halfSpread,
                    candle.Volume, 0m, candle.CloseTime);

                // Konto-Snapshot (einmal pro Symbol — zwischen Evaluate und Entry mutiert nichts daran).
                var account = await simExchange.GetAccountInfoAsync().ConfigureAwait(false);

                // EVALUATE mit dem PRE-EXIT-Positions-Snapshot (Schritt d), exakt wie die Single-Symbol-Engine:
                // dort laeuft Evaluate VOR ProcessExits mit denselben pre-exit Positionen. Strategien wie
                // TrendFollow blocken so per "position_open"-Guard ein Re-Entry auf der Exit-Kerze (kein Look-Ahead
                // auf den frisch frei gewordenen Slot) — das ist der Schluessel zur Bit-Identitaet (Regression).
                var evalContext = new MarketContext(
                    s.Symbol, contextCandles, ticker, positions, account,
                    FilterTimeframeCandles: null,
                    Category: s.Category,
                    DailyCandles: null,
                    WeeklyCandles: null,
                    NavigatorTimeframe: navTf,
                    ScannerSettings: settings.Scanner,
                    RiskSettings: settings.Risk,
                    NowUtc: candle.CloseTime);

                var signal = s.Strategy.Evaluate(evalContext);

                // ENTRY/ValidateTrade-Kontext mit FRISCHEN Positionen (EIN gemeinsames Konto):
                // so sehen die konto-weiten Gates die intra-Step bereits eroeffneten Positionen frueherer
                // Symbole + alle Exits dieses Schritts (Live-Scanner-Realismus → Gates greifen, GAP 1).
                var freshPositions = await simExchange.GetPositionsAsync().ConfigureAwait(false);
                var context = new MarketContext(
                    s.Symbol, contextCandles, ticker, freshPositions, account,
                    FilterTimeframeCandles: null,
                    Category: s.Category,
                    DailyCandles: null,
                    WeeklyCandles: null,
                    NavigatorTimeframe: navTf,
                    ScannerSettings: settings.Scanner,
                    RiskSettings: settings.Risk,
                    NowUtc: candle.CloseTime);

                int adaptLeverage = 0;
                if (signal.Signal is Signal.Long or Signal.Short)
                {
                    // Leverage durchreichen — wie Live: (int)catSettings.MaxLeverage, SetLeverageAsync vor Order.
                    var catSettings = settings.Risk.GetCategorySettings(s.Category);
                    adaptLeverage = (int)catSettings.MaxLeverage;
                    var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
                    await simExchange.SetLeverageAsync(s.Symbol, adaptLeverage, side).ConfigureAwait(false);
                }

                await BacktestEntryProcessor.ProcessEntryAsync(
                    simExchange, riskManager, signal, context, s.Symbol, candle,
                    positionSignals, exitTracking, freshPositions, _logger, adaptLeverage, btcHealth).ConfigureAwait(false);
            }

            // g. NF9: neu abgeschlossene Trades konto-weit in den RiskManager streamen.
            var completedAfterStep = simExchange.GetCompletedTrades();
            while (lastCompletedTradeCount < completedAfterStep.Count)
            {
                riskManager.UpdateDailyStats(completedAfterStep[lastCompletedTradeCount]);
                lastCompletedTradeCount++;
            }

            // Test-Seam: gleichzeitig offene Positionen NACH allen Entries dieses Schritts melden
            // (Invariante fuer den Gates-Test: nie mehr als MaxOpenPositions offen).
            if (onStepOpenPositions != null)
            {
                var afterEntries = await simExchange.GetPositionsAsync().ConfigureAwait(false);
                onStepOpenPositions(afterEntries.Count);
            }

            // Equity-Snapshot ~1×/Tag (konto-weit).
            if (step % EquitySnapshotEverySteps == 0)
            {
                var eq = await simExchange.GetAccountInfoAsync().ConfigureAwait(false);
                equityCurve.Add(new EquityPoint(t, eq.Balance + eq.UnrealizedPnl));
            }
        }

        IndicatorHelper.ClearCache();

        // 5. Final: alle Positionen schliessen, restliche Trades streamen, Report.
        await simExchange.CloseAllPositionsAsync().ConfigureAwait(false);
        positionSignals.Clear();
        exitTracking.Clear();

        var finalAccount = await simExchange.GetAccountInfoAsync().ConfigureAwait(false);
        equityCurve.Add(new EquityPoint(timeline[^1], finalAccount.Balance));

        progress?.Report(100);

        var completedTrades = simExchange.GetCompletedTrades()
            .Select(tr => tr with { Mode = TradingMode.Backtest })
            .ToList();
        while (lastCompletedTradeCount < completedTrades.Count)
        {
            riskManager.UpdateDailyStats(completedTrades[lastCompletedTradeCount]);
            lastCompletedTradeCount++;
        }

        var report = PerformanceReport.FromTrades(completedTrades, equityCurve, bt.InitialBalance);
        _logger.LogInformation("Portfolio-Backtest fertig: {Trades} Trades, PnL {Pnl:F2}, WinRate {WR:F1}%, MaxDD {DD:F1}%",
            report.TotalTrades, report.TotalPnl, report.WinRate, report.MaxDrawdownPercent);
        return report;
    }

    /// <summary>
    /// NF8 portfolio-weit: OpenRisk = Σ |Entry − SL| × Qty ueber ALLE offenen Positionen (egal welches Symbol),
    /// wie BacktestEngine, aber konto-weit. Speist <see cref="RiskManager.SetOpenRiskEstimate"/>.
    /// </summary>
    private static void RefreshOpenRisk(
        RiskManager riskManager,
        IReadOnlyList<Position> positions,
        Dictionary<string, SignalResult> positionSignals)
    {
        if (positions.Count == 0)
        {
            riskManager.SetOpenRiskEstimate(0m);
            return;
        }

        decimal openRisk = 0m;
        for (var p = 0; p < positions.Count; p++)
        {
            var pos = positions[p];
            var key = $"{pos.Symbol}_{pos.Side}";
            if (!positionSignals.TryGetValue(key, out var sig)) continue;
            if (!sig.StopLoss.HasValue || sig.StopLoss.Value <= 0) continue;
            openRisk += Math.Abs(pos.EntryPrice - sig.StopLoss.Value) * pos.Quantity;
        }
        riskManager.SetOpenRiskEstimate(openRisk);
    }

    /// <summary>
    /// GAP 11: Baut einen synthetischen 24h-Ticker fuer den Scanner-Vorfilter aus den letzten 6 H4-Kerzen
    /// (= ein Handelstag bei H4). <c>Volume24h</c> = Σ(Kerzen-Volumen × Close) der letzten 6 Kerzen
    /// (Quote-Volumen in USDT, wie der Live-Ticker es liefert — NICHT das Basis-Volumen). <c>PriceChangePercent24h</c>
    /// = (Close − Close_vor_6_Kerzen) / Close_vor_6_Kerzen × 100. Bei &lt; 6 vorliegenden Kerzen wird mit
    /// den verfuegbaren gerechnet (Warmup garantiert ohnehin &gt;= 50 Kerzen vor dem ersten Trade).
    /// </summary>
    private static Ticker BuildScanTicker(PortfolioSymbolState s, Candle candle)
    {
        var idx = s.NavIdx;
        var lookback = Math.Min(6, idx + 1);
        decimal quoteVol = 0m;
        for (var i = idx - lookback + 1; i <= idx; i++)
            quoteVol += s.Nav[i].Volume * s.Nav[i].Close;

        var refIdx = idx - lookback;                       // Close vor dem 24h-Fenster
        var prevClose = refIdx >= 0 ? s.Nav[refIdx].Close : s.Nav[Math.Max(0, idx - lookback + 1)].Close;
        var changePct = prevClose > 0m ? (candle.Close - prevClose) / prevClose * 100m : 0m;

        return new Ticker(s.Symbol, candle.Close, candle.Close, candle.Close, quoteVol, changePct, candle.CloseTime);
    }

    /// <summary>
    /// GAP 11: Per-Symbol-Spiegel von <c>ScanHelper.FilterCandidatesForTimeframe</c>/<c>FilterCandidatesCore</c>
    /// (ScanHelper liegt in BingXBot.Trading → vom Backtest nicht referenzierbar, daher hier nachgebaut).
    /// Prueft NUR die per-Symbol-Praedikate (Whitelist-Prioritaet, MinVolume24h + MinPriceChange
    /// kategorie-spezifisch Crypto/TradFi, TradFi-100k-Floor, Blacklist). Die portfolio-weite 60/40-Quote +
    /// MaxResults-Selektion ist hier irrelevant — das Symbol-Universum ist im Backtest fix vorgegeben.
    /// </summary>
    private static bool PassesScannerFilter(Ticker t, MarketCategory category, TimeFrame navTf, ScannerSettings settings)
    {
        // Whitelist hat Prioritaet: wenn gesetzt, nur diese Symbole (ohne Volume/Change-Filter — wie ScanHelper).
        if (settings.Whitelist.Count > 0)
            return settings.Whitelist.Contains(t.Symbol);

        var isTradFi = SymbolClassifier.IsTradFi(t.Symbol);

#pragma warning disable CS0618 // Legacy-Single-TF-Fallback wenn die ByTf-Map keinen Wert hat (wie ScanHelper).
        var minVol = isTradFi
            ? (settings.MinVolume24hTradFiByTf.TryGetValue(navTf, out var vt) && vt > 0 ? vt : settings.MinVolume24hTradFi)
            : (settings.MinVolume24hByTf.TryGetValue(navTf, out var vc) && vc > 0 ? vc : settings.MinVolume24h);
        var minChg = isTradFi
            ? (settings.MinPriceChangeTradFiByTf.TryGetValue(navTf, out var pt) && pt >= 0 ? pt : settings.MinPriceChangeTradFi)
            : (settings.MinPriceChangeByTf.TryGetValue(navTf, out var pc) && pc >= 0 ? pc : settings.MinPriceChange);
#pragma warning restore CS0618

        // TradFi-Sanity-Floor 100k (wie ScanHelper.FilterCandidatesCore).
        if (isTradFi) minVol = Math.Max(100_000m, minVol);

        if (t.Volume24h < minVol) return false;
        if (minChg > 0m && Math.Abs(t.PriceChangePercent24h) < minChg) return false;
        if (settings.Blacklist.Count > 0 && settings.Blacklist.Contains(t.Symbol)) return false;

        return true;
    }
}
