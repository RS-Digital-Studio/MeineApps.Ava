using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Core.Simulation;
using BingXBot.Backtest.Reports;
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
        var allCandles = await LoadHistoricalDataAsync(symbol, timeFrame, from, to);

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

        // 5. Candle-Iteration
        for (int i = warmupSize; i < allCandles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            // Fortschritt melden
            var progressPercent = (int)((double)(i - warmupSize) / (allCandles.Count - warmupSize) * 100);
            progress?.Report(progressPercent);

            // Aktuellen Preis setzen
            var currentCandle = allCandles[i];
            simExchange.SetCurrentPrice(symbol, currentCandle.Close);

            // Kontext erstellen (letzte N Candles bis aktuell)
            var contextCandles = allCandles.Take(i + 1).TakeLast(Math.Min(i + 1, 200)).ToList();
            var positions = await simExchange.GetPositionsAsync();
            var account = await simExchange.GetAccountInfoAsync();
            var ticker = new Ticker(symbol, currentCandle.Close, currentCandle.Low, currentCandle.High,
                currentCandle.Volume, 0m, currentCandle.CloseTime);

            var context = new MarketContext(symbol, contextCandles, ticker, positions, account);

            // Strategie evaluieren
            var signal = strategy.Evaluate(context);

            // SL/TP-Check auf offene Positionen mit echten Werten aus dem Signal
            foreach (var pos in positions.ToList())
            {
                var key = $"{pos.Symbol}_{pos.Side}";
                if (!positionSignals.TryGetValue(key, out var origSignal))
                    continue;

                var slHit = false;
                var tpHit = false;

                if (pos.Side == Side.Buy)
                {
                    // Long: SL wird bei Low getroffen, TP bei High
                    if (origSignal.StopLoss.HasValue && currentCandle.Low <= origSignal.StopLoss.Value)
                        slHit = true;
                    else if (origSignal.TakeProfit.HasValue && currentCandle.High >= origSignal.TakeProfit.Value)
                        tpHit = true;
                }
                else // Short
                {
                    // Short: SL wird bei High getroffen, TP bei Low
                    if (origSignal.StopLoss.HasValue && currentCandle.High >= origSignal.StopLoss.Value)
                        slHit = true;
                    else if (origSignal.TakeProfit.HasValue && currentCandle.Low <= origSignal.TakeProfit.Value)
                        tpHit = true;
                }

                if (slHit)
                {
                    simExchange.SetCurrentPrice(symbol, origSignal.StopLoss!.Value);
                    await simExchange.ClosePositionAsync(symbol, pos.Side);
                    positionSignals.Remove(key);
                    simExchange.SetCurrentPrice(symbol, currentCandle.Close);
                }
                else if (tpHit)
                {
                    simExchange.SetCurrentPrice(symbol, origSignal.TakeProfit!.Value);
                    await simExchange.ClosePositionAsync(symbol, pos.Side);
                    positionSignals.Remove(key);
                    simExchange.SetCurrentPrice(symbol, currentCandle.Close);
                }
            }

            // Trade ausführen wenn Signal
            if (signal.Signal is Signal.Long or Signal.Short)
            {
                var riskCheck = riskManager.ValidateTrade(signal, context);
                if (riskCheck.IsAllowed && riskCheck.AdjustedPositionSize > 0)
                {
                    var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
                    try
                    {
                        await simExchange.PlaceOrderAsync(new OrderRequest(
                            symbol, side, OrderType.Market, riskCheck.AdjustedPositionSize));

                        // Signal für SL/TP-Tracking speichern
                        var key = $"{symbol}_{side}";
                        positionSignals[key] = signal;
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
                    await simExchange.ClosePositionAsync(symbol, Side.Buy);
                    positionSignals.Remove($"{symbol}_{Side.Buy}");
                }
            }
            else if (signal.Signal is Signal.CloseShort)
            {
                if (positions.Any(p => p.Side == Side.Sell))
                {
                    await simExchange.ClosePositionAsync(symbol, Side.Sell);
                    positionSignals.Remove($"{symbol}_{Side.Sell}");
                }
            }

            // Equity Snapshot (alle 10 Candles)
            if (i % 10 == 0)
            {
                var eq = await simExchange.GetAccountInfoAsync();
                equityCurve.Add(new EquityPoint(currentCandle.CloseTime, eq.Balance + eq.UnrealizedPnl));
            }
        }

        // 6. Alle offenen Positionen schließen
        await simExchange.CloseAllPositionsAsync();
        positionSignals.Clear();

        // Finaler Equity-Punkt
        var finalAccount = await simExchange.GetAccountInfoAsync();
        equityCurve.Add(new EquityPoint(allCandles[^1].CloseTime, finalAccount.Balance));

        progress?.Report(100);

        // 7. Report erstellen
        var completedTrades = simExchange.GetCompletedTrades()
            .Select(t => t with { Mode = TradingMode.Backtest })
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
                var candles = await _publicClient.GetKlinesAsync(symbol, timeFrame, from, to);
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
            return await LoadFromExchangeClientAsync(symbol, timeFrame, from, to);
        }

        // Priorität 3: Leere Liste zurückgeben (Caller generiert Demo-Daten wenn nötig)
        _logger.LogWarning("Keine Datenquelle verfügbar für {Symbol}", symbol);
        return new List<Candle>();
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
            var candles = await _dataSource!.GetKlinesAsync(symbol, timeFrame, limit);
            return candles
                .Where(c => c.OpenTime >= from && c.OpenTime <= to)
                .OrderBy(c => c.OpenTime)
                .ToList();
        }

        // Paginiert laden: mehrere 1440er Batches
        var allCandles = new List<Candle>();
        var batchCount = (int)Math.Ceiling((double)expectedCandles / batchSize);

        for (int batch = 0; batch < batchCount; batch++)
        {
            var candles = await _dataSource!.GetKlinesAsync(symbol, timeFrame, batchSize);
            if (candles.Count == 0)
                break;

            allCandles.AddRange(candles);

            // Duplikate entfernen (nach OpenTime), da Batches sich überlappen können
            allCandles = allCandles
                .DistinctBy(c => c.OpenTime)
                .OrderBy(c => c.OpenTime)
                .ToList();

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
}
