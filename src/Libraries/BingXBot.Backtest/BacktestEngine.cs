using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Backtest.Simulation;
using BingXBot.Backtest.Reports;
using BingXBot.Core.Models.ATI;
using BingXBot.Engine.ATI;
using BingXBot.Engine.Indicators;
using Microsoft.Extensions.Logging;

namespace BingXBot.Backtest;

public class BacktestEngine
{
    private readonly IExchangeClient? _dataSource; // Nur für GetKlinesAsync (historische Daten)
    private readonly IPublicMarketDataClient? _publicClient; // Öffentliche Marktdaten (kein API-Key nötig)
    private readonly ILogger<BacktestEngine> _logger;


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
        CancellationToken ct = default)
    {
        // 1. Historische Daten laden
        var allCandles = await LoadHistoricalDataAsync(symbol, timeFrame, from, to).ConfigureAwait(false);

        // Wenn keine Daten: Demo-Candles generieren
        if (allCandles.Count == 0)
        {
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

        // 1c. Entry-TF-Candles laden (SK-System: M15 als Trigger bei H4-Primary)
        List<Candle>? entryTfCandles = null;
        var entryTf = settings.EntryTimeFrame ?? timeFrame switch
        {
            TimeFrame.H4 => TimeFrame.M15,  // SK Holy Trinity: 15m-Trigger bei H4-Primary
            TimeFrame.H1 => TimeFrame.M15,
            TimeFrame.M15 => TimeFrame.M5,
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

        // 4c. Regime-Tracking pro Position (für Regime-spezifische Metriken)
        var positionRegimes = new Dictionary<string, MarketRegime>();
        var regimeDetector = new RegimeDetector();

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

        var iterationCount = allCandles.Count - warmupSize;
        for (int i = warmupSize; i < allCandles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            // Fortschritt melden
            var progressPercent = (int)((double)(i - warmupSize) / iterationCount * 100);
            progress?.Report(progressPercent);

            // Aktuellen Preis setzen
            var currentCandle = allCandles[i];
            simExchange.SetCurrentPrice(symbol, currentCandle.Close);

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
            var positions = await simExchange.GetPositionsAsync().ConfigureAwait(false);
            var account = await simExchange.GetAccountInfoAsync().ConfigureAwait(false);
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

                    // Entry-TF-Kontext bis zur aktuellen Sub-Kerze
                    var entryCandle = entryTfCandles[entryTfIdx];
                    var entryStart = Math.Max(0, entryTfIdx + 1 - 200);
                    IReadOnlyList<Candle> entryTfContext = new CandleSlice(entryTfCandles, entryStart, entryTfIdx + 1 - entryStart);

                    // Ticker mit dem Preis der Entry-TF-Kerze (nicht der H4-Kerze)
                    var subHalfSpread = entryCandle.Close * settings.SpreadPercent / 100m / 2m;
                    var subTicker = new Ticker(symbol, entryCandle.Close,
                        entryCandle.Close - subHalfSpread, entryCandle.Close + subHalfSpread,
                        entryCandle.Volume, 0m, entryCandle.CloseTime);

                    var subContext = new MarketContext(symbol, contextCandles, subTicker, positions, account, htfContext, category, entryTfContext);
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
                // Kein Entry-TF: Standard-Evaluation auf Primary-TF (wie bisher)
                var context = new MarketContext(symbol, contextCandles, ticker, positions, account, htfContext, category, null);
                signal = strategy.Evaluate(context);
            }

            // SL/TP-Check auf offene Positionen mit echten Werten aus dem Signal
            // positions ist bereits eine Kopie (IReadOnlyList aus SimulatedExchange), kein ToList() nötig
            foreach (var pos in positions)
            {
                var key = $"{pos.Symbol}_{pos.Side}";
                if (!positionSignals.TryGetValue(key, out var origSignal))
                    continue;

                // --- Multi-Stage Exit (wenn aktiviert und ExitState vorhanden) ---
                if (settings.SimulateMultiStageExit && exitTracking.TryGetValue(key, out var exitState))
                {
                    // Extreme-Price tracken (für Chandelier-Trailing nach TP1)
                    if (pos.Side == Side.Buy)
                        exitState.ExtremePriceSinceEntry = Math.Max(exitState.ExtremePriceSinceEntry, currentCandle.High);
                    else
                        exitState.ExtremePriceSinceEntry = Math.Min(exitState.ExtremePriceSinceEntry, currentCandle.Low);

                    // SK-System: Gestufter Breakeven (2× SL-Distanz → BE, 120% zum TP1 → SL auf TP1)
                    if (origSignal.DisableSmartBreakeven && origSignal.StopLoss.HasValue && origSignal.TakeProfit.HasValue)
                    {
                        var slDist = Math.Abs(exitState.EntryPrice - origSignal.StopLoss.Value);
                        var currentPrice = pos.Side == Side.Buy ? currentCandle.High : currentCandle.Low;
                        var currentProfit = pos.Side == Side.Buy
                            ? currentPrice - exitState.EntryPrice
                            : exitState.EntryPrice - currentPrice;
                        var entryToTp1 = Math.Abs(origSignal.TakeProfit.Value - exitState.EntryPrice);

                        // Stufe 2: SL auf TP1-Level (wenn Preis weit über TP1)
                        if (entryToTp1 > 0 && currentProfit / entryToTp1 >= 1.2m && !exitState.Tp2Closed)
                        {
                            positionSignals[key] = positionSignals[key] with { StopLoss = origSignal.TakeProfit.Value };
                        }
                        // Stufe 1: BE bei 2× SL-Distanz
                        else if (slDist > 0 && currentProfit >= slDist * 2m && !exitState.PartialClosed)
                        {
                            var beSl = pos.Side == Side.Buy
                                ? exitState.EntryPrice * 1.0015m
                                : exitState.EntryPrice * 0.9985m;
                            positionSignals[key] = positionSignals[key] with { StopLoss = beSl };
                        }
                    }

                    // TP1-Check: Partial Close wenn noch nicht geschehen
                    var tp1Hit = false;
                    if (!exitState.PartialClosed && origSignal.TakeProfit.HasValue)
                    {
                        tp1Hit = pos.Side == Side.Buy
                            ? currentCandle.High >= origSignal.TakeProfit.Value
                            : currentCandle.Low <= origSignal.TakeProfit.Value;
                    }

                    if (tp1Hit)
                    {
                        // SK-System: Tp1CloseRatioOverride hat Vorrang (0.5 = 50% bei TP1)
                        var tp1Ratio = origSignal.Tp1CloseRatioOverride ?? settings.Tp1CloseRatio;
                        var closeQty = Math.Round(exitState.OriginalQuantity * tp1Ratio, 6);
                        if (closeQty > 0)
                        {
                            simExchange.SetCurrentPrice(symbol, origSignal.TakeProfit!.Value);
                            await simExchange.ReducePositionAsync(symbol, pos.Side, closeQty).ConfigureAwait(false);
                            simExchange.SetCurrentPrice(symbol, currentCandle.Close);
                        }

                        // SK-System: Gestufter BE nach Kassing-Regeln
                        // Bei TP1-Hit prüfen ob 2× SL-Distanz bereits erreicht → BE setzen
                        if (origSignal.DisableSmartBreakeven)
                        {
                            var slDist = Math.Abs(exitState.EntryPrice - (origSignal.StopLoss ?? exitState.EntryPrice));
                            var profit = pos.Side == Side.Buy
                                ? origSignal.TakeProfit!.Value - exitState.EntryPrice
                                : exitState.EntryPrice - origSignal.TakeProfit!.Value;
                            // Bei TP1-Hit ist Gewinn >= SL-Distanz (TP1 > 2×SL normalerweise) → BE setzen
                            if (slDist > 0 && profit >= slDist * 2m)
                            {
                                var beSl = pos.Side == Side.Buy
                                    ? exitState.EntryPrice * 1.0015m
                                    : exitState.EntryPrice * 0.9985m;
                                positionSignals[key] = origSignal with { StopLoss = beSl, TakeProfit = exitState.Tp2 };
                            }
                            else
                            {
                                // Noch nicht 2× SL → SL bleibt unter Punkt A, nur TP umstellen
                                positionSignals[key] = origSignal with { TakeProfit = exitState.Tp2 };
                            }
                        }
                        else
                        {
                            // Smart Breakeven: SL = Entry + ATR-Puffer statt exakter Entry
                            var beSl = exitState.EntryPrice;
                            if (settings.SmartBreakevenAtrMultiplier > 0 && exitState.CurrentAtr > 0)
                            {
                                beSl = pos.Side == Side.Buy
                                    ? exitState.EntryPrice + exitState.CurrentAtr * settings.SmartBreakevenAtrMultiplier
                                    : exitState.EntryPrice - exitState.CurrentAtr * settings.SmartBreakevenAtrMultiplier;
                            }
                            positionSignals[key] = origSignal with
                            {
                                StopLoss = beSl,
                                TakeProfit = exitState.Tp2
                            };
                        }
                        exitState.PartialClosed = true;
                        exitState.MaxHoldHours = settings.MaxHoldHoursAfterTp1;
                        continue; // Position bleibt offen mit Rest-Menge
                    }

                    // Time-Exit: Max Haltezeit überschritten
                    var holdHours = (currentCandle.CloseTime - exitState.EntryTime).TotalHours;
                    if (holdHours >= exitState.MaxHoldHours)
                    {
                        // Vor TP1: nur schließen wenn nicht im Gewinn (analog Live-Trading)
                        var inProfit = pos.Side == Side.Buy
                            ? currentCandle.Close > exitState.EntryPrice
                            : currentCandle.Close < exitState.EntryPrice;

                        if (exitState.PartialClosed || !inProfit)
                        {
                            await simExchange.ClosePositionAsync(symbol, pos.Side).ConfigureAwait(false);
                            positionSignals.Remove(key);
                            exitTracking.Remove(key);
                            continue;
                        }
                    }

                    // Chandelier-Trailing nach TP1: SL nachziehen basierend auf Extreme-ATR
                    // SK-System: Kein Trailing (SL bleibt strukturell unter Punkt A)
                    if (exitState.PartialClosed && exitState.CurrentAtr > 0 && !origSignal.DisableSmartBreakeven)
                    {
                        decimal newTrailingSl;
                        if (pos.Side == Side.Buy)
                            newTrailingSl = exitState.ExtremePriceSinceEntry
                                - exitState.CurrentAtr * exitState.TrailingAtrMultiplier;
                        else
                            newTrailingSl = exitState.ExtremePriceSinceEntry
                                + exitState.CurrentAtr * exitState.TrailingAtrMultiplier;

                        // SL nur nach vorne verschieben (nie zurück)
                        var currentSl = positionSignals[key].StopLoss ?? exitState.EntryPrice;
                        var slImproved = pos.Side == Side.Buy
                            ? newTrailingSl > currentSl
                            : newTrailingSl < currentSl;

                        if (slImproved)
                            positionSignals[key] = positionSignals[key] with { StopLoss = newTrailingSl };

                        // ATR aktualisieren für nächste Candle
                        var atrValues = IndicatorHelper.CalculateAtr(contextCandles, 14);
                        exitState.CurrentAtr = atrValues[^1] ?? exitState.CurrentAtr;
                    }
                }

                // --- Standard SL/TP-Check (Fallback und finale Prüfung) ---
                // Auch bei Multi-Stage: prüft den aktuellen SL/TP (ggf. bereits auf BE/TP2 verschoben)
                var currentSignal = positionSignals[key]; // Kann durch Multi-Stage modifiziert sein
                var slHit = false;
                var tpHit = false;

                // Wenn beide (SL+TP) in einer Candle getroffen werden:
                // Candle-Richtung entscheidet welcher zuerst erreicht wurde.
                // Bullish Candle (Close>Open) → Preis ging zuerst hoch → TP bei Long wahrscheinlicher.
                // Bearish Candle (Close<Open) → Preis ging zuerst runter → SL bei Long wahrscheinlicher.
                if (pos.Side == Side.Buy)
                {
                    var slTriggered = currentSignal.StopLoss.HasValue && currentCandle.Low <= currentSignal.StopLoss.Value;
                    var tpTriggered = currentSignal.TakeProfit.HasValue && currentCandle.High >= currentSignal.TakeProfit.Value;
                    if (slTriggered && tpTriggered)
                    {
                        if (currentCandle.Close > currentCandle.Open)
                            tpHit = true; // Bullish → TP zuerst
                        else
                            slHit = true; // Bearish → SL zuerst
                    }
                    else if (slTriggered) slHit = true;
                    else if (tpTriggered) tpHit = true;
                }
                else // Short
                {
                    var slTriggered = currentSignal.StopLoss.HasValue && currentCandle.High >= currentSignal.StopLoss.Value;
                    var tpTriggered = currentSignal.TakeProfit.HasValue && currentCandle.Low <= currentSignal.TakeProfit.Value;
                    if (slTriggered && tpTriggered)
                    {
                        if (currentCandle.Close < currentCandle.Open)
                            tpHit = true; // Bearish → TP zuerst für Short
                        else
                            slHit = true; // Bullish → SL zuerst für Short
                    }
                    else if (slTriggered) slHit = true;
                    else if (tpTriggered) tpHit = true;
                }

                if (slHit)
                {
                    simExchange.SetCurrentPrice(symbol, currentSignal.StopLoss!.Value);
                    await simExchange.ClosePositionAsync(symbol, pos.Side).ConfigureAwait(false);
                    positionSignals.Remove(key);
                    exitTracking.Remove(key);
                    simExchange.SetCurrentPrice(symbol, currentCandle.Close);
                }
                else if (tpHit)
                {
                    // Pyramid TP2: Wenn TP1 schon geschlossen und Tp2CloseRatio < 1.0, nur Teil schließen
                    BacktestExitState? tp2State = null;
                    var isPartialTp2 = settings.SimulateMultiStageExit
                        && exitTracking.TryGetValue(key, out tp2State)
                        && tp2State.PartialClosed  // TP1 war schon
                        && !tp2State.Tp2Closed      // TP2 noch nicht
                        && settings.Tp2CloseRatio > 0 && settings.Tp2CloseRatio < 1m;

                    // SK: Full-Close bei TP2 → fällt in den else-Zweig (komplett schließen)
                    // Standard: Partial TP2 nur wenn Tp1 < 100% (sonst ist nichts mehr übrig)
                    var effectiveTp1Ratio = origSignal.Tp1CloseRatioOverride ?? settings.Tp1CloseRatio;
                    if (isPartialTp2 && tp2State != null && effectiveTp1Ratio < 1m
                        && !origSignal.DisableSmartBreakeven)
                    {
                        // TP2 Partial Close: Tp2CloseRatio der verbleibenden Position schließen
                        var remainingQty = pos.Quantity;
                        var tp2CloseQty = Math.Round(remainingQty * (settings.Tp2CloseRatio / (1m - settings.Tp1CloseRatio)), 6);
                        tp2CloseQty = Math.Min(tp2CloseQty, remainingQty);

                        if (tp2CloseQty > 0 && tp2CloseQty < remainingQty)
                        {
                            simExchange.SetCurrentPrice(symbol, currentSignal.TakeProfit!.Value);
                            await simExchange.ReducePositionAsync(symbol, pos.Side, tp2CloseQty).ConfigureAwait(false);
                            simExchange.SetCurrentPrice(symbol, currentCandle.Close);

                            // TP2 erledigt, Rest läuft mit Chandelier-Trailing weiter (kein TP mehr)
                            tp2State.Tp2Closed = true;
                            positionSignals[key] = currentSignal with { TakeProfit = null };
                        }
                        else
                        {
                            // Zu wenig übrig → komplett schließen
                            simExchange.SetCurrentPrice(symbol, currentSignal.TakeProfit!.Value);
                            await simExchange.ClosePositionAsync(symbol, pos.Side).ConfigureAwait(false);
                            positionSignals.Remove(key);
                            exitTracking.Remove(key);
                            simExchange.SetCurrentPrice(symbol, currentCandle.Close);
                        }
                    }
                    else
                    {
                        // Standard: Voller Close bei TP
                        simExchange.SetCurrentPrice(symbol, currentSignal.TakeProfit!.Value);
                        await simExchange.ClosePositionAsync(symbol, pos.Side).ConfigureAwait(false);
                        positionSignals.Remove(key);
                        exitTracking.Remove(key);
                        simExchange.SetCurrentPrice(symbol, currentCandle.Close);
                    }
                }
            }

            // Regime erkennen (wie im Live-Bot: ATI filtert Chaotic + Range)
            var regimeContext = new MarketContext(symbol, contextCandles, ticker, positions, account, htfContext, category, null);
            var features = FeatureEngine.Extract(regimeContext);
            var regimeState = regimeDetector.Detect(features);

            // Chaotic-Regime: Alle Positionen sofort schließen (wie Live-Bot PriceTickerLoop)
            if (regimeState.CurrentRegime == MarketRegime.Chaotic && positions.Count > 0)
            {
                foreach (var pos in positions)
                {
                    await simExchange.ClosePositionAsync(symbol, pos.Side).ConfigureAwait(false);
                    var key2 = $"{symbol}_{pos.Side}";
                    positionSignals.Remove(key2);
                    exitTracking.Remove(key2);
                }
            }

            // Trade ausführen wenn Signal (Regime-Filter: Kein neuer Trade in Range/Chaotic)
            // SK-System: Eigener ADX<15 Filter, Regime-Gate überspringen (DisableSmartBreakeven = SK-Flag)
            // Ohne das: ADX 15-25 = SK sagt "ok", RegimeDetector sagt "Range" → Trade verworfen
            if (signal.Signal is Signal.Long or Signal.Short
                && (signal.DisableSmartBreakeven
                    || regimeState.CurrentRegime is MarketRegime.TrendingBull or MarketRegime.TrendingBear))
            {
                // MarketContext für RiskManager: Aktuellen State verwenden
                var riskContext = new MarketContext(symbol, contextCandles, ticker, positions, account, htfContext, category, null);
                var riskCheck = riskManager.ValidateTrade(signal, riskContext);
                if (riskCheck.IsAllowed && riskCheck.AdjustedPositionSize > 0)
                {
                    var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
                    try
                    {
                        var order = await simExchange.PlaceOrderAsync(new OrderRequest(
                            symbol, side, OrderType.Market, riskCheck.AdjustedPositionSize)).ConfigureAwait(false);

                        // Nur SL/TP-Tracking speichern wenn Order tatsaechlich gefuellt wurde
                        if (order.Status == OrderStatus.Filled)
                        {
                            var key = $"{symbol}_{side}";
                            positionSignals[key] = signal;
                            positionRegimes[key] = regimeState.CurrentRegime;

                            // Multi-Stage Exit State erstellen
                            if (settings.SimulateMultiStageExit)
                            {
                                var atrValues = IndicatorHelper.CalculateAtr(contextCandles, 14);
                                var lastAtr = atrValues[^1] ?? 0m;
                                var entryPrice = order.Price;
                                exitTracking[key] = new BacktestExitState
                                {
                                    EntryPrice = entryPrice,
                                    OriginalQuantity = riskCheck.AdjustedPositionSize,
                                    EntryTime = currentCandle.CloseTime,
                                    Tp2 = signal.TakeProfit2,
                                    CurrentAtr = lastAtr,
                                    TrailingAtrMultiplier = settings.TrailingAtrMultiplier,
                                    MaxHoldHours = settings.MaxHoldHoursInitial,
                                    // Extreme-Price mit Entry initialisieren
                                    ExtremePriceSinceEntry = side == Side.Buy
                                        ? currentCandle.High
                                        : currentCandle.Low
                                };
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Order fehlgeschlagen: {Error}", ex.Message);
                    }
                }
            }
            else if (signal.Signal is Signal.CloseLong)
            {
                if (positions.Any(p => p.Side == Side.Buy))
                {
                    await simExchange.ClosePositionAsync(symbol, Side.Buy).ConfigureAwait(false);
                    positionSignals.Remove($"{symbol}_{Side.Buy}");
                    exitTracking.Remove($"{symbol}_{Side.Buy}");
                }
            }
            else if (signal.Signal is Signal.CloseShort)
            {
                if (positions.Any(p => p.Side == Side.Sell))
                {
                    await simExchange.ClosePositionAsync(symbol, Side.Sell).ConfigureAwait(false);
                    positionSignals.Remove($"{symbol}_{Side.Sell}");
                    exitTracking.Remove($"{symbol}_{Side.Sell}");
                }
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
            .Select(t =>
            {
                var tradeKey = $"{t.Symbol}_{t.Side}";
                positionRegimes.TryGetValue(tradeKey, out var regime);
                return t with { Mode = TradingMode.Backtest, Regime = regime };
            })
            .ToList();

        // RiskManager Stats updaten
        foreach (var trade in completedTrades)
            riskManager.UpdateDailyStats(trade);

        var report = PerformanceReport.FromTrades(completedTrades, equityCurve, settings.InitialBalance);
        _logger.LogInformation("Backtest abgeschlossen: {Trades} Trades, P&L: {Pnl:F2}, WinRate: {WR:F1}%",
            report.TotalTrades, report.TotalPnl, report.WinRate);

        return report;
    }

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

    /// <summary>Tracking-State für Multi-Stage Exit im Backtest.</summary>
    private sealed class BacktestExitState
    {
        /// <summary>Einstiegspreis der Position (für Break-Even-Berechnung).</summary>
        public decimal EntryPrice { get; init; }
        /// <summary>Ursprüngliche Positionsgröße (für Partial-Close-Berechnung).</summary>
        public decimal OriginalQuantity { get; init; }
        /// <summary>Zeitpunkt des Einstiegs (für Time-Exit).</summary>
        public DateTime EntryTime { get; init; }
        /// <summary>Zweites Take-Profit-Ziel (weiter entfernt als TP1).</summary>
        public decimal? Tp2 { get; set; }
        /// <summary>Ob TP1 bereits erreicht und Partial Close ausgeführt wurde.</summary>
        public bool PartialClosed { get; set; }
        /// <summary>Ob TP2 bereits erreicht und Partial Close ausgeführt wurde (Pyramid: Rest trailing).</summary>
        public bool Tp2Closed { get; set; }
        /// <summary>Höchster (Long) bzw. niedrigster (Short) Preis seit Entry (für Trailing).</summary>
        public decimal ExtremePriceSinceEntry { get; set; }
        /// <summary>Aktuelle ATR für Chandelier-Trailing-Berechnung.</summary>
        public decimal CurrentAtr { get; set; }
        /// <summary>ATR-Multiplikator für Trailing-Abstand.</summary>
        public decimal TrailingAtrMultiplier { get; set; }
        /// <summary>Maximale Haltezeit in Stunden (ändert sich nach TP1).</summary>
        public int MaxHoldHours { get; set; }
    }
}
