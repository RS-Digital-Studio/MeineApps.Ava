using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Backtest.Simulation;
using BingXBot.Backtest.Portfolio;
using BingXBot.Backtest.Reports;
using BingXBot.Engine.Indicators;
using Microsoft.Extensions.Logging;

namespace BingXBot.Backtest;

public class BacktestEngine
{
    private readonly IExchangeClient? _dataSource; // Nur für GetKlinesAsync (historische Daten)
    private readonly IPublicMarketDataClient? _publicClient; // Öffentliche Marktdaten (kein API-Key nötig)
    private readonly ILogger<BacktestEngine> _logger;

    /// <summary>
    /// Optionaler Bar-Progress (current, total). Wird ueber <see cref="SetBarProgress"/> gesetzt
    /// damit der bestehende RunAsync-Parameter-Signature nicht breakt.
    /// </summary>
    private IProgress<(int Current, int Total)>? _barProgress;

    /// <summary>
    /// Setzt den (current, total) Bar-Progress-Callback. Wird vom <c>LocalBacktestService</c>
    /// genutzt um Client-UI mit Bar-Zaehlern zu versorgen (BacktestStatusDto.CurrentBar/TotalBars).
    /// Thread-safe weil RunAsync single-threaded pro BacktestEngine-Instanz ist.
    /// </summary>
    public void SetBarProgress(IProgress<(int Current, int Total)>? barProgress) => _barProgress = barProgress;


    /// <summary>
    /// Konstruktor für echte Marktdaten über öffentlichen Client (kein API-Key nötig).
    /// </summary>
    public BacktestEngine(IPublicMarketDataClient publicClient, ILogger<BacktestEngine> logger)
    {
        _publicClient = publicClient;
        _logger = logger;
    }

    /// <summary>
    /// Konstruktor für IExchangeClient (Mock/Test oder authentifizierter Client).
    /// </summary>
    public BacktestEngine(IExchangeClient dataSource, ILogger<BacktestEngine> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<PerformanceReport> RunAsync(
        IStrategy strategy,
        IRiskManager riskManager,
        string symbol,
        TimeFrame timeFrame,
        DateTime from,
        DateTime to,
        BacktestSettings settings,
        IProgress<int>? progress = null,
        CancellationToken ct = default,
        ScannerSettings? scannerSettings = null,
        RiskSettings? riskSettings = null,
        List<Candle>? preloadedWeekly = null,
        List<Candle>? preloadedDaily = null)
    {
        // Hinweis wenn der Backtest ohne Settings läuft — per-TF-Scoring-Schwellen + ATR-Multiplikatoren fehlen dann.
        if (scannerSettings == null)
            _logger.LogWarning("Backtest ohne ScannerSettings gestartet — per-TF-Scoring und ATR-Multiplikatoren nutzen Fallbacks. " +
                               "Ergebnisse weichen ggf. von Live-Verhalten ab.");

        // 1. Historische Daten laden
        var allCandles = await LoadHistoricalDataAsync(symbol, timeFrame, from, to).ConfigureAwait(false);

        // Wenn keine Daten: Demo-Candles NUR generieren wenn es keine echte Datenquelle gibt
        // (reiner Test-Kontext ohne PublicClient/ExchangeClient). Sobald eine echte Datenquelle
        // konfiguriert ist (Lab/Live-Backtest mit _publicClient), sind leere Klines ein echtes
        // "Symbol nicht/noch nicht gelistet"-Signal — KEINE synthetischen Demo-Candles, sonst flutet
        // ein deterministischer Sinus-Random-Walk (den ein Trendfolger trivial gewinnt) die Statistik.
        // Genau das machte den OOS-2024-Lauf wertlos (ZEC ohne 2024-Daten → 311 Phantom-Trades).
        var hasRealDataSource = _publicClient != null || _dataSource != null;
        if (allCandles.Count == 0)
        {
            if (hasRealDataSource)
            {
                _logger.LogWarning("Keine echten Klines fuer {Symbol} {TF} im Zeitraum {From}..{To} — Run uebersprungen (kein Demo-Fallback bei echter Datenquelle).",
                    symbol, timeFrame, from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"));
                return new PerformanceReport();
            }
            _logger.LogWarning("Keine historischen Daten verfügbar, generiere Demo-Daten");
            var candleDuration = TimeFrameHelper.ToDuration(timeFrame);
            var duration = to - from;
            var count = duration.TotalSeconds > 0
                ? Math.Max(200, (int)(duration / candleDuration))
                : 500;
            // Maximal 5000 Demo-Candles
            count = Math.Min(count, 5000);
            allCandles = GenerateDemoCandles(count, 50000m, timeFrame);
        }

        if (allCandles.Count < 2)
            return new PerformanceReport();

        _logger.LogInformation("Backtest gestartet: {Symbol} {TF} {Count} Candles", symbol, timeFrame, allCandles.Count);

        // 1b. HTF-Candles laden (für Trend-Konfirmation in der Strategie)
        List<Candle>? htfCandles = null;
        if (settings.HtfTimeFrame.HasValue && settings.HtfTimeFrame.Value != timeFrame)
        {
            try
            {
                htfCandles = await LoadHistoricalDataAsync(symbol, settings.HtfTimeFrame.Value, from.AddDays(-14), to).ConfigureAwait(false);
                if (htfCandles.Count > 0)
                    _logger.LogInformation("HTF-Candles geladen: {TF} {Count} Candles", settings.HtfTimeFrame.Value, htfCandles.Count);
            }
            catch
            {
                // HTF-Candles sind optional - Strategie funktioniert auch ohne (weniger Score-Punkte)
            }
        }

        // 1b-SK. Weekly- und Daily-Candles laden (SK-Workflow Phase 1: Kontext / BLASH / Fahrplan)
        // Live-Bot reicht weeklyCandles + dailyCandles separat durch — Backtest muss das gleiche tun,
        // sonst arbeitet DetermineFahrplanBias / MarketContextAnalyzer auf leeren bzw. falschen Daten.
        // Multi-TF-Backtest (Caller ruft für D1/H4/H1/M15 nacheinander auf) kann die Kerzen per
        // preloadedWeekly/preloadedDaily durchreichen — spart N × identische Kline-Requests.
        List<Candle>? weeklyCandles = preloadedWeekly;
        List<Candle>? dailyCandles = preloadedDaily;
        if (weeklyCandles == null && timeFrame != TimeFrame.W1)
        {
            try
            {
                // Weekly braucht mehr Historie (min 20 Kerzen für Sequenz-Detection = 5 Monate)
                weeklyCandles = await LoadHistoricalDataAsync(symbol, TimeFrame.W1, from.AddDays(-365), to).ConfigureAwait(false);
                if (weeklyCandles.Count > 0)
                    _logger.LogInformation("W1-Fahrplan-Candles geladen: {Count}", weeklyCandles.Count);
            }
            catch { /* W1 optional — Fahrplan-Bias fällt zurück auf D1 */ }
        }
        if (dailyCandles == null && timeFrame != TimeFrame.D1)
        {
            try
            {
                // Daily braucht min 60 Kerzen für BLASH-Berechnung (60d-Range) + Puffer
                dailyCandles = await LoadHistoricalDataAsync(symbol, TimeFrame.D1, from.AddDays(-120), to).ConfigureAwait(false);
                if (dailyCandles.Count > 0)
                    _logger.LogInformation("D1-Fahrplan-Candles geladen: {Count}", dailyCandles.Count);
            }
            catch { /* D1 optional — Kontext-Gate arbeitet dann nur mit W1 */ }
        }

        // 1c. Entry-TF-Candles laden (SK-System: Sub-Iteration für präzise Intra-Candle-Trigger).
        // Multi-TF Standalone: Nur für H4/H1 relevant (Legacy SK-Buch M30-Trigger bei H4-Primary).
        // M15-Navigator + kleiner: keine Sub-Iteration — Navigator-Close ist Trigger (1:1 zum Live-Bot).
        // Der pre-Multi-TF M5-Navigator lief ebenfalls ohne Sub-Iteration; Konsistenz gewahrt.
        List<Candle>? entryTfCandles = null;
        var entryTf = settings.EntryTimeFrame ?? timeFrame switch
        {
            TimeFrame.H4 => TimeFrame.M30,  // SK-Buch: M30 als Entry-Chart bei H4-Primary
            TimeFrame.H1 => TimeFrame.M15,
            _ => (TimeFrame?)null
        };
        if (entryTf.HasValue && entryTf.Value != timeFrame)
        {
            try
            {
                // Entry-TF ab from laden (nicht nur -7d), damit der gesamte Backtest-Zeitraum abgedeckt ist
                entryTfCandles = await LoadHistoricalDataAsync(symbol, entryTf.Value, from.AddDays(-2), to).ConfigureAwait(false);
                if (entryTfCandles.Count > 0)
                    _logger.LogInformation("Entry-TF-Candles geladen: {TF} {Count} Candles", entryTf.Value, entryTfCandles.Count);
            }
            catch { /* Entry-TF optional */ }
        }

        // Markt-Kategorie bestimmen
        var category = Core.Helpers.SymbolClassifier.Classify(symbol);

        // 2. SimulatedExchange erstellen
        var simExchange = new SimulatedExchange(settings);
        var equityCurve = new List<EquityPoint>();

        // 3. Warmup-Phase (erste 50 Candles oder 25% der Daten, max 200)
        var warmupSize = Math.Min(Math.Max(50, allCandles.Count / 4), 200);
        warmupSize = Math.Min(warmupSize, allCandles.Count - 1);
        var warmupCandles = allCandles.Take(warmupSize).ToList();
        strategy.WarmUp(warmupCandles);
        strategy.Reset(); // Reset nach WarmUp, Evaluate nutzt die Candles direkt

        // 4. SL/TP-Tracking: speichert das zugehörige Signal pro offener Position
        var positionSignals = new Dictionary<string, SignalResult>();

        // 4a. Multi-Stage Exit State pro Position
        var exitTracking = new Dictionary<string, BacktestExitState>();

        // 4b. Funding-Rate-Tracking: Alle 8h auf offene Positionen anwenden
        var fundingRate = settings.SimulatedFundingRatePercent / 100m; // z.B. 0.01% → 0.0001
        simExchange.SetFundingRate(fundingRate);
        var lastFundingTime = allCandles.Count > warmupSize ? allCandles[warmupSize].OpenTime : DateTime.UtcNow;

        // 5. Candle-Iteration
        // HTF inkrementeller Index: Letzte HTF-Candle die vor/bei Warmup-Start schließt
        var htfIdx = -1;
        if (htfCandles is { Count: > 0 })
        {
            var firstAfter = htfCandles.FindIndex(c => c.CloseTime > allCandles[warmupSize].CloseTime);
            htfIdx = firstAfter switch
            {
                -1 => htfCandles.Count - 1,  // Alle HTF-Candles vor Warmup → letzte nehmen
                0 => -1,                       // Keine HTF-Candle vor Warmup → noch kein Kontext
                _ => firstAfter - 1            // Normalfall: letzte vor Warmup-Start
            };
        }

        // Entry-TF inkrementeller Index (analog zu HTF)
        var entryTfIdx = -1;
        if (entryTfCandles is { Count: > 0 })
        {
            var firstAfterEntry = entryTfCandles.FindIndex(c => c.CloseTime > allCandles[warmupSize].CloseTime);
            entryTfIdx = firstAfterEntry switch
            {
                -1 => entryTfCandles.Count - 1,
                0 => -1,
                _ => firstAfterEntry - 1
            };
        }

        // D1- und W1-Indizes: ebenfalls inkrementell, damit der Hot-Path innerhalb der
        // Sub-Iteration keine Binary-Search (DailySliceUpTo) braucht. Initialisierung:
        // letzte D1/W1-Kerze, die vor Warmup-Ende geschlossen hat.
        int InitIdx(List<Candle>? src)
        {
            if (src is null || src.Count == 0) return -1;
            var firstAfter = src.FindIndex(c => c.CloseTime > allCandles[warmupSize].CloseTime);
            return firstAfter switch
            {
                -1 => src.Count - 1,
                0 => -1,
                _ => firstAfter - 1,
            };
        }
        var dailyIdx = InitIdx(dailyCandles);
        var weeklyIdx = InitIdx(weeklyCandles);

        // NF9 Fix — Backtest streamt RiskManager-Updates live statt nur am Ende. Vorher war
        // riskManager._dailyPnl waehrend des Runs immer 0 → LossStreakDampening, EquityCurve-
        // Scaling und MaxDailyLoss-Circuit haben im Backtest nie gefeuert. Trades liefen mit
        // voller Position, Backtest-Ergebnisse waren systematisch zu optimistisch.
        var lastBacktestDate = allCandles[warmupSize].CloseTime.Date;
        var lastCompletedTradeCount = 0;

        var iterationCount = allCandles.Count - warmupSize;
        for (int i = warmupSize; i < allCandles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            // Fortschritt melden — sowohl Prozent-Progress als auch (current, total)-Callback
            // (letzteres fuellt BacktestStatusDto.CurrentBar/TotalBars fuer Remote-UI).
            var progressPercent = (int)((double)(i - warmupSize) / iterationCount * 100);
            progress?.Report(progressPercent);
            _barProgress?.Report((i - warmupSize, iterationCount));

            // Aktuellen Preis setzen
            var currentCandle = allCandles[i];
            simExchange.SetCurrentPrice(symbol, currentCandle.Close);
            // Backtest-Zeit pro Candle setzen damit CompletedTrade-EntryTime/ExitTime die echten
            // Candle-Timestamps bekommen (nicht DateTime.UtcNow zum Test-Ausfuehrungs-Zeitpunkt).
            simExchange.SetCurrentBacktestTime(currentCandle.CloseTime);

            // Dynamische Slippage: ATR und Volume-Ratio an SimulatedExchange übergeben
            if (settings.UseDynamicSlippage && i >= 15)
            {
                var sliceStart = Math.Max(0, i + 1 - 15);
                var sliceLen = i + 1 - sliceStart;
                IReadOnlyList<Candle> atrSlice = new CandleSlice(allCandles, sliceStart, sliceLen);
                var atrVals = IndicatorHelper.CalculateAtr(atrSlice, 14);
                var lastAtrVal = atrVals.Count > 0 && atrVals[^1].HasValue ? atrVals[^1]!.Value : 0m;

                // Volume-Ratio: aktuelles Volumen / Durchschnitt der letzten 20
                var volStart = Math.Max(0, i - 20);
                var avgVol = 0m;
                var volCount = 0;
                for (int v = volStart; v < i; v++)
                {
                    avgVol += allCandles[v].Volume;
                    volCount++;
                }
                avgVol = volCount > 0 ? avgVol / volCount : currentCandle.Volume;
                var volRatio = avgVol > 0 ? currentCandle.Volume / avgVol : 1m;

                simExchange.SetMarketConditions(symbol, lastAtrVal, volRatio);
            }

            // Funding-Rate alle 8h anwenden (Perpetual Futures Standard)
            if (settings.SimulateFundingRate && (currentCandle.CloseTime - lastFundingTime).TotalHours >= 8)
            {
                simExchange.ApplyFundingRate(fundingRate);
                lastFundingTime = currentCandle.CloseTime;
            }

            // Kontext erstellen: Index-basierter Slice statt Take/TakeLast (O(1) statt O(n))
            var contextStart = Math.Max(0, i + 1 - 200);
            var contextCount = i + 1 - contextStart;
            IReadOnlyList<Candle> contextCandles = new CandleSlice(allCandles, contextStart, contextCount);

            // Indikator-Cache periodisch leeren um unbegrenztes Wachstum zu verhindern
            if ((i - warmupSize) % 500 == 0 && i > warmupSize)
                IndicatorHelper.ClearCache();

            // NF9 Fix — Tageswechsel-Reset (analog Live-TradingServiceBase). Verhindert dass
            // DailyPnl ueber das gesamte Backtest-Fenster akkumuliert und MaxDailyLoss-Circuit
            // dauerhaft blockiert wird.
            var currentDate = currentCandle.CloseTime.Date;
            if (currentDate != lastBacktestDate)
            {
                riskManager.ResetDailyStats();
                lastBacktestDate = currentDate;
            }

            var positions = await simExchange.GetPositionsAsync().ConfigureAwait(false);
            var account = await simExchange.GetAccountInfoAsync().ConfigureAwait(false);

            // NF8 Fix (Backtest-Variante) — OpenRisk pro Tick auffrischen, damit MaxDailyRiskPercent
            // offene Positionen sieht. Im Live macht das TradingServiceBase.ScanAndTradeAsync, hier
            // analog vor dem RiskManager.ValidateTrade-Aufruf.
            if (positions.Count > 0)
            {
                decimal openRiskBacktest = 0m;
                for (var p = 0; p < positions.Count; p++)
                {
                    var pos = positions[p];
                    var posKey = $"{pos.Symbol}_{pos.Side}";
                    if (!positionSignals.TryGetValue(posKey, out var posSig)) continue;
                    if (!posSig.StopLoss.HasValue || posSig.StopLoss.Value <= 0) continue;
                    openRiskBacktest += Math.Abs(pos.EntryPrice - posSig.StopLoss.Value) * pos.Quantity;
                }
                riskManager.SetOpenRiskEstimate(openRiskBacktest);
            }
            else
            {
                riskManager.SetOpenRiskEstimate(0m);
            }
            // Realistischer Bid/Ask-Spread statt Candle Low/High
            // (Low/High ist der Candle-Range, nicht der Spread → wäre massiv überzeichnet)
            var halfSpread = currentCandle.Close * settings.SpreadPercent / 100m / 2m;
            var ticker = new Ticker(symbol, currentCandle.Close,
                currentCandle.Close - halfSpread, currentCandle.Close + halfSpread,
                currentCandle.Volume, 0m, currentCandle.CloseTime);

            // HTF-Kontext: Candles bis zum aktuellen Zeitpunkt (nicht in die Zukunft schauen)
            // Inkrementeller Index statt FindLastIndex pro Iteration (O(1) statt O(n))
            if (htfCandles != null && htfCandles.Count > 0)
            {
                while (htfIdx < htfCandles.Count - 1 && htfCandles[htfIdx + 1].CloseTime <= currentCandle.CloseTime)
                    htfIdx++;
            }
            IReadOnlyList<Candle>? htfContext = null;
            if (htfCandles != null && htfIdx >= 20)
            {
                var htfStart = Math.Max(0, htfIdx + 1 - 100);
                htfContext = new CandleSlice(htfCandles, htfStart, htfIdx + 1 - htfStart);
            }

            // D1/W1-Indizes pro Primary-Iteration fortschreiben (innerhalb einer Primary-Kerze
            // schließt i.d.R. keine D1/W1, daher reicht ein Advance pro Primary-Takt).
            // Sub-Iteration trimmt nochmal lokal, falls Primary bereits in der Zukunft liegt.
            while (dailyCandles is { Count: > 0 } && dailyIdx < dailyCandles.Count - 1
                && dailyCandles[dailyIdx + 1].CloseTime <= currentCandle.CloseTime)
                dailyIdx++;
            while (weeklyCandles is { Count: > 0 } && weeklyIdx < weeklyCandles.Count - 1
                && weeklyCandles[weeklyIdx + 1].CloseTime <= currentCandle.CloseTime)
                weeklyIdx++;

            // Zero-Copy Prefix-Slices für Primary-Context (Fallback + RiskContext).
            IReadOnlyList<Candle>? dailyPrefix = dailyIdx >= 0 && dailyCandles is not null
                ? new CandleSlice(dailyCandles, 0, dailyIdx + 1) : null;
            IReadOnlyList<Candle>? weeklyPrefix = weeklyIdx >= 0 && weeklyCandles is not null
                ? new CandleSlice(weeklyCandles, 0, weeklyIdx + 1) : null;

            // Entry-TF Sub-Iteration: SK-Signale entstehen auf M15-Takt, nicht H4-Takt.
            // Innerhalb jeder H4-Kerze alle zugehörigen M15-Kerzen durchiterieren und
            // die Strategie bei jeder M15-Kerze evaluieren. Ohne das werden 15/16 M15-Signale verpasst.
            SignalResult signal = new(Signal.None, 0m, null, null, null, "");

            if (entryTfCandles != null && entryTfCandles.Count > 0)
            {
                // Alle Entry-TF-Kerzen durchiterieren die innerhalb dieser Primary-Kerze schließen
                while (entryTfIdx < entryTfCandles.Count - 1 && entryTfCandles[entryTfIdx + 1].CloseTime <= currentCandle.CloseTime)
                {
                    entryTfIdx++;
                    if (entryTfIdx < 20) continue;

                    // Entry-TF-Kontext bis zur aktuellen Sub-Kerze (nur für Trigger, nicht für FilterTimeframeCandles)
                    var entryCandle = entryTfCandles[entryTfIdx];

                    // Ticker mit dem Preis der Entry-TF-Kerze (nicht der H4-Kerze)
                    var subHalfSpread = entryCandle.Close * settings.SpreadPercent / 100m / 2m;
                    var subTicker = new Ticker(symbol, entryCandle.Close,
                        entryCandle.Close - subHalfSpread, entryCandle.Close + subHalfSpread,
                        entryCandle.Volume, 0m, entryCandle.CloseTime);

                    // Filter-TF-Slice zum aktuellen Sub-Kerzen-Zeitpunkt (Live-Mapping: GetFilterTimeframe).
                    // htfCandles sind hier die Filter-TF-Kerzen (BacktestViewModel setzt HtfTimeFrame = GetFilterTimeframe(tf)).
                    IReadOnlyList<Candle>? subFilterTf = null;
                    if (htfCandles is { Count: > 0 })
                    {
                        // htfIdx steht bereits auf Primary-Close. In der Sub-Iteration kann die Filter-TF
                        // aber bereits weiter sein (M30-Sub bei H1-Filter → 1 Schritt, H1-Sub bei H4-Filter → 0).
                        // Für Korrektheit (auch M15-Backtest mit M5-Filter = viele Filter-Kerzen pro Sub):
                        // Lokaler Index der auf entryCandle.CloseTime trimmt.
                        var localHtfIdx = htfIdx;
                        while (localHtfIdx < htfCandles.Count - 1 && htfCandles[localHtfIdx + 1].CloseTime <= entryCandle.CloseTime)
                            localHtfIdx++;
                        // Rückwärts trimmen falls htfIdx bereits in die Zukunft zeigt (Primary-Close > Sub-Close)
                        while (localHtfIdx >= 0 && htfCandles[localHtfIdx].CloseTime > entryCandle.CloseTime)
                            localHtfIdx--;
                        if (localHtfIdx >= 0)
                        {
                            var htfStart = Math.Max(0, localHtfIdx + 1 - 100);
                            subFilterTf = new CandleSlice(htfCandles, htfStart, localHtfIdx + 1 - htfStart);
                        }
                    }

                    // D1/W1 konservativ auf entryCandle.CloseTime trimmen — innerhalb einer Primary-Kerze
                    // schließt keine D1/W1 (außer bei D1=24h, die mit Primary synchron schließt, dann ist der
                    // Primary-Index korrekt). Rückwärts trimmen falls Primary bereits schloss.
                    IReadOnlyList<Candle>? subDaily = null;
                    if (dailyIdx >= 0 && dailyCandles is { Count: > 0 })
                    {
                        var localD = dailyIdx;
                        while (localD >= 0 && dailyCandles[localD].CloseTime > entryCandle.CloseTime) localD--;
                        if (localD >= 0) subDaily = new CandleSlice(dailyCandles, 0, localD + 1);
                    }
                    IReadOnlyList<Candle>? subWeekly = null;
                    if (weeklyIdx >= 0 && weeklyCandles is { Count: > 0 })
                    {
                        var localW = weeklyIdx;
                        while (localW >= 0 && weeklyCandles[localW].CloseTime > entryCandle.CloseTime) localW--;
                        if (localW >= 0) subWeekly = new CandleSlice(weeklyCandles, 0, localW + 1);
                    }

                    var subContext = new MarketContext(
                        symbol, contextCandles, subTicker, positions, account,
                        FilterTimeframeCandles: subFilterTf,
                        Category: category,
                        DailyCandles: subDaily,
                        WeeklyCandles: subWeekly,
                        NavigatorTimeframe: timeFrame,
                        ScannerSettings: scannerSettings,
                        RiskSettings: riskSettings,
                        NowUtc: entryCandle.CloseTime);
                    signal = strategy.Evaluate(subContext);

                    // Bei Signal sofort raus aus der Sub-Iteration (Trade platzieren)
                    if (signal.Signal is Signal.Long or Signal.Short or Signal.CloseLong or Signal.CloseShort)
                    {
                        simExchange.SetCurrentPrice(symbol, entryCandle.Close);
                        break;
                    }
                }
            }
            else
            {
                // Kein Entry-TF (Navigator-TF ist selbst Entry-Chart): Filter-TF = htfContext-Slot.
                // htfContext ist hier die Filter-TF (BacktestSettings.HtfTimeFrame = GetFilterTimeframe(tf)).
                var context = new MarketContext(
                    symbol, contextCandles, ticker, positions, account,
                    FilterTimeframeCandles: htfContext,
                    Category: category,
                    DailyCandles: dailyPrefix,
                    WeeklyCandles: weeklyPrefix,
                    NavigatorTimeframe: timeFrame,
                    ScannerSettings: scannerSettings,
                    RiskSettings: riskSettings,
                    NowUtc: currentCandle.CloseTime);
                signal = strategy.Evaluate(context);
            }

            // SL/TP-Check auf offene Positionen mit echten Werten aus dem Signal
            // (extrahiert in BacktestExitProcessor — wiederverwendbar fuer den Portfolio-Backtest).
            await BacktestExitProcessor.ProcessExitsAsync(
                simExchange, positions, positionSignals, exitTracking,
                settings, riskSettings, symbol, currentCandle).ConfigureAwait(false);

            // Trade ausführen wenn Signal (SK-Buch: keine Regime-Filter, SK hat eigene Workflow-Regeln).
            // MarketContext für RiskManager: gleiche Zuordnung wie bei der Strategy. Der Kontext-Bau bleibt
            // hier (braucht viele loop-lokale Variablen); die Entry-Logik selbst wandert in den Processor
            // (wiederverwendbar fuer den Portfolio-Backtest).
            var riskContext = new MarketContext(
                symbol, contextCandles, ticker, positions, account,
                FilterTimeframeCandles: htfContext,
                Category: category,
                DailyCandles: dailyPrefix,
                WeeklyCandles: weeklyPrefix,
                NavigatorTimeframe: timeFrame,
                ScannerSettings: scannerSettings,
                RiskSettings: riskSettings,
                NowUtc: currentCandle.CloseTime);
            await BacktestEntryProcessor.ProcessEntryAsync(
                simExchange, riskManager, signal, riskContext, symbol, currentCandle,
                positionSignals, exitTracking, positions, _logger).ConfigureAwait(false);

            // NF9 Fix — Neu abgeschlossene Trades dieser Iteration in den RiskManager streamen,
            // damit LossStreakDampening, EquityCurveScaling und MaxDailyLoss-Circuit korrekt
            // ueber die Backtest-Laufzeit greifen (vorher: nur final-Loop am Ende → keinerlei
            // dynamische Reaktion waehrend des Runs).
            var completedAfterTick = simExchange.GetCompletedTrades();
            while (lastCompletedTradeCount < completedAfterTick.Count)
            {
                riskManager.UpdateDailyStats(completedAfterTick[lastCompletedTradeCount]);
                lastCompletedTradeCount++;
            }

            // Equity Snapshot (alle 10 Candles)
            if (i % 10 == 0)
            {
                var eq = await simExchange.GetAccountInfoAsync().ConfigureAwait(false);
                equityCurve.Add(new EquityPoint(currentCandle.CloseTime, eq.Balance + eq.UnrealizedPnl));
            }
        }

        // Indikator-Cache leeren um Memory-Wachstum zu begrenzen
        IndicatorHelper.ClearCache();

        // 6. Alle offenen Positionen schließen
        await simExchange.CloseAllPositionsAsync().ConfigureAwait(false);
        positionSignals.Clear();
        exitTracking.Clear();

        // Finaler Equity-Punkt
        var finalAccount = await simExchange.GetAccountInfoAsync().ConfigureAwait(false);
        equityCurve.Add(new EquityPoint(allCandles[^1].CloseTime, finalAccount.Balance));

        progress?.Report(100);

        // 7. Report erstellen
        var completedTrades = simExchange.GetCompletedTrades()
            .Select(t => t with { Mode = TradingMode.Backtest })
            .ToList();

        // NF9 Fix — Verbleibende Trades aus CloseAllPositionsAsync (Final-Close am Backtest-Ende)
        // nachholen. Diese kamen erst nach der letzten Iteration herein und sind im Stream-Update
        // noch nicht gezaehlt. Vorher wurde hier eine komplette Re-Iteration ueber alle Trades
        // gemacht — das hat alle bereits gestreamten Trades doppelt aktualisiert.
        while (lastCompletedTradeCount < completedTrades.Count)
        {
            riskManager.UpdateDailyStats(completedTrades[lastCompletedTradeCount]);
            lastCompletedTradeCount++;
        }

        var report = PerformanceReport.FromTrades(completedTrades, equityCurve, settings.InitialBalance);
        _logger.LogInformation("Backtest abgeschlossen: {Trades} Trades, P&L: {Pnl:F2}, WinRate: {WR:F1}%",
            report.TotalTrades, report.TotalPnl, report.WinRate);

        return report;
    }

    /// <summary>
    /// Öffentlicher Zugang zum historischen Daten-Loader — Caller (z. B. BacktestViewModel)
    /// kann damit W1/D1 einmalig vorladen und über die <c>preloadedWeekly/preloadedDaily</c>-
    /// Parameter an mehrere <see cref="RunAsync"/>-Aufrufe weiterreichen.
    /// </summary>
    public Task<List<Candle>> LoadCandlesAsync(string symbol, TimeFrame timeFrame, DateTime from, DateTime to)
        => LoadHistoricalDataAsync(symbol, timeFrame, from, to);

    /// <summary>
    /// Lädt historische Candle-Daten. Versucht zuerst den Public Client (echte BingX-Daten),
    /// dann IExchangeClient (Mock/Test), zuletzt Demo-Candles.
    /// </summary>
    private async Task<List<Candle>> LoadHistoricalDataAsync(string symbol, TimeFrame timeFrame, DateTime from, DateTime to)
    {
        // Priorität 1: Öffentlicher Client (echte BingX-Daten, kein API-Key nötig)
        if (_publicClient != null)
        {
            _logger.LogInformation("Lade echte Marktdaten von BingX für {Symbol}...", symbol);
            try
            {
                var candles = await _publicClient.GetKlinesAsync(symbol, timeFrame, from, to).ConfigureAwait(false);
                if (candles.Count > 0)
                {
                    _logger.LogInformation("{Count} echte Candles geladen ({From} bis {To})",
                        candles.Count, from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"));
                    return candles;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Public Client fehlgeschlagen, versuche Fallback...");
            }
        }

        // Priorität 2: IExchangeClient (für Tests/Mocks/authentifizierte Clients)
        if (_dataSource != null)
        {
            return await LoadFromExchangeClientAsync(symbol, timeFrame, from, to).ConfigureAwait(false);
        }

        // Priorität 3: Leere Liste zurückgeben (Caller generiert Demo-Daten wenn nötig)
        _logger.LogWarning("Keine Datenquelle verfügbar für {Symbol}", symbol);
        return [];
    }

    /// <summary>
    /// Lädt Candle-Daten über den IExchangeClient (paginiert, max 1440 pro Request).
    /// </summary>
    private async Task<List<Candle>> LoadFromExchangeClientAsync(string symbol, TimeFrame timeFrame, DateTime from, DateTime to)
    {
        var candleDuration = TimeFrameHelper.ToDuration(timeFrame);
        var duration = to - from;
        var expectedCandles = duration.TotalSeconds > 0
            ? (int)(duration / candleDuration)
            : 1440;

        const int batchSize = 1440;

        if (expectedCandles <= batchSize)
        {
            // Einzelner Request reicht
            var limit = Math.Min(expectedCandles + 50, batchSize);
            var candles = await _dataSource!.GetKlinesAsync(symbol, timeFrame, limit).ConfigureAwait(false);
            return candles
                .Where(c => c.OpenTime >= from && c.OpenTime <= to)
                .OrderBy(c => c.OpenTime)
                .ToList();
        }

        // Paginiert laden: mehrere 1440er Batches
        // Abbruch wenn sich allCandles.Count nicht mehr ändert (API liefert immer gleiche Daten ohne Offset)
        var allCandles = new List<Candle>();
        var batchCount = (int)Math.Ceiling((double)expectedCandles / batchSize);
        var prevCount = -1;

        for (int batch = 0; batch < batchCount; batch++)
        {
            var candles = await _dataSource!.GetKlinesAsync(symbol, timeFrame, batchSize).ConfigureAwait(false);
            if (candles.Count == 0)
                break;

            allCandles.AddRange(candles);

            // Duplikate entfernen (nach OpenTime), da Batches sich überlappen können
            allCandles = allCandles
                .DistinctBy(c => c.OpenTime)
                .OrderBy(c => c.OpenTime)
                .ToList();

            // Kein Fortschritt → API liefert immer gleiche Daten (kein from/to-Offset)
            if (allCandles.Count == prevCount)
                break;
            prevCount = allCandles.Count;

            // Genug Daten?
            var filtered = allCandles.Where(c => c.OpenTime >= from && c.OpenTime <= to).ToList();
            if (filtered.Count >= expectedCandles)
                break;
        }

        _logger.LogInformation("Geladen: {Count} Candles für {Symbol} ({Batches} Batches)",
            allCandles.Count, symbol, batchCount);

        return allCandles
            .Where(c => c.OpenTime >= from && c.OpenTime <= to)
            .OrderBy(c => c.OpenTime)
            .ToList();
    }

    /// <summary>
    /// Generiert historische Candle-Daten für Demo/Test-Zwecke.
    /// Erzeugt realistische Preisbewegungen mit Trend und Volatilität.
    /// </summary>
    public static List<Candle> GenerateDemoCandles(int count, decimal startPrice = 50000m, TimeFrame tf = TimeFrame.H1)
    {
        var candles = new List<Candle>();
        var rng = new Random(42); // Deterministisch für reproduzierbare Ergebnisse
        var price = startPrice;
        var candleDuration = TimeFrameHelper.ToDuration(tf);
        var baseTime = DateTime.UtcNow.AddTicks(-candleDuration.Ticks * count);

        for (int i = 0; i < count; i++)
        {
            // Realistischere Preisbewegung mit Trend-Komponente
            var trend = Math.Sin(i * 0.02) * 0.3; // Langsamer Sinus-Trend
            var noise = (rng.NextDouble() - 0.5) * 2;
            var volatility = startPrice * 0.005m; // 0.5% Volatilität
            var change = (decimal)(trend + noise) * volatility;

            var open = price;
            var close = price + change;
            var high = Math.Max(open, close) + (decimal)rng.NextDouble() * volatility * 0.5m;
            var low = Math.Min(open, close) - (decimal)rng.NextDouble() * volatility * 0.5m;
            low = Math.Max(low, 1m); // Mindestpreis

            var volume = 1000m + (decimal)rng.NextDouble() * 2000m;
            var time = baseTime.Add(candleDuration * i);

            candles.Add(new Candle(time, open, high, low, close, volume, time.Add(candleDuration)));
            price = Math.Max(close, 1m);
        }
        return candles;
    }

    /// <summary>
    /// Schneidet eine Kerzen-Liste auf das Prefix zu, das spätestens zum angegebenen Zeitpunkt
    /// geschlossen hat — verhindert Look-Ahead von D1/W1-Kerzen in laufenden Backtest-Iterationen.
    /// Wird aktuell nicht mehr im Hot-Path genutzt (dailyIdx/weeklyIdx inkrementell),
    /// bleibt als Fallback-Helper für künftige Backtest-Varianten.
    /// </summary>
    private static IReadOnlyList<Candle>? DailySliceUpToBs(List<Candle>? source, DateTime upToClose)
    {
        if (source is null or { Count: 0 }) return null;

        // Binary-Search: letzte Kerze mit CloseTime <= upToClose
        int lo = 0, hi = source.Count - 1, lastFit = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (source[mid].CloseTime <= upToClose) { lastFit = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        if (lastFit < 0) return null;
        return new CandleSlice(source, 0, lastFit + 1);
    }

    /// <summary>Zero-Copy Slice über eine Candle-Liste (vermeidet GetRange-Allokation pro Candle im Backtest).</summary>
    private sealed class CandleSlice : IReadOnlyList<Candle>
    {
        private readonly List<Candle> _source;
        private readonly int _offset;
        public int Count { get; }

        public CandleSlice(List<Candle> source, int offset, int count)
        {
            _source = source;
            _offset = offset;
            Count = count;
        }

        public Candle this[int index] => _source[_offset + index];

        public IEnumerator<Candle> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return _source[_offset + i];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
