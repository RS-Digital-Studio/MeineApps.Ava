using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
using Microsoft.Extensions.Logging;

namespace BingXBot.Engine.Scanner;

/// <summary>
/// Multi-dimensionaler Market Scanner mit Trend-Filter, Volatilitäts-Scoring und Volume-Profiling.
/// Bewertet Kandidaten anhand von 5 Dimensionen statt nur |Price%| * Volume.
/// </summary>
public class MarketScanner : IMarketScanner
{
    private readonly IExchangeClient _exchangeClient;
    private readonly ILogger<MarketScanner> _logger;

    // Optionaler öffentlicher Client für Klines-Abfragen (kein API-Key nötig)
    private readonly IPublicMarketDataClient? _publicClient;

    public MarketScanner(IExchangeClient exchangeClient, ILogger<MarketScanner> logger)
    {
        _exchangeClient = exchangeClient;
        _logger = logger;
    }

    public MarketScanner(IExchangeClient exchangeClient,
        IPublicMarketDataClient publicClient, ILogger<MarketScanner> logger)
        : this(exchangeClient, logger)
    {
        _publicClient = publicClient;
    }

    public async IAsyncEnumerable<ScanResult> ScanAsync(
        ScannerSettings settings,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Alle Ticker holen
        var tickers = await _exchangeClient.GetAllTickersAsync();

        // MarketCap-Cache aktualisieren (CoinGecko, max 1x pro Stunde)
        try { await MarketCapCache.RefreshIfNeededAsync().ConfigureAwait(false); }
        catch { /* Fallback auf Volume-Ranking */ }

        // Krypto und TradFi trennen
        var cryptoTickers = tickers.Where(t => !SymbolClassifier.IsTradFi(t.Symbol)).ToList();
        var tradfiTickers = settings.EnableTradFi && settings.IsHedgeModeActive
            ? tickers.Where(t => SymbolClassifier.IsTradFi(t.Symbol)
                              && SymbolClassifier.IsApiTradeable(t.Symbol)
                              && settings.EnabledCategories.Contains(SymbolClassifier.Classify(t.Symbol))).ToList()
            : new List<Ticker>();

        // Top-100 Krypto nach Market Cap (CoinGecko) oder Fallback Volume
        if (settings.OnlyTopByVolume && settings.TopCoinsCount > 0)
        {
            if (MarketCapCache.IsLoaded)
            {
                cryptoTickers = cryptoTickers
                    .Where(t => MarketCapCache.IsTopCoin(t.Symbol, settings.TopCoinsCount))
                    .ToList();
            }
            else
            {
                cryptoTickers = cryptoTickers
                    .OrderByDescending(t => t.Volume24h)
                    .Take(settings.TopCoinsCount).ToList();
            }
            _logger.LogDebug("Top-{Count} Filter (MarketCap={IsMcLoaded}): {Total} Ticker → {CryptoCount} Krypto + {TradFiCount} TradFi",
                settings.TopCoinsCount, MarketCapCache.IsLoaded, tickers.Count, cryptoTickers.Count, tradfiTickers.Count);
        }

        // Basis-Filter (Volumen, Preis, Black/Whitelist)
        var filteredCrypto = cryptoTickers
            .Where(t => !settings.Blacklist.Contains(t.Symbol))
            .Where(t => settings.Whitelist.Count == 0 || settings.Whitelist.Contains(t.Symbol))
            .Where(t => t.Volume24h >= settings.MinVolume24h)
            .Where(t => Math.Abs(t.PriceChangePercent24h) >= settings.MinPriceChange)
            .ToList();

        var filteredTradFi = tradfiTickers
            .Where(t => !settings.Blacklist.Contains(t.Symbol))
            .Where(t => t.Volume24h >= settings.MinVolume24hTradFi)
            .ToList();

        var filtered = filteredCrypto.Concat(filteredTradFi).ToList();

        // Klines parallel vorladen (SemaphoreSlim begrenzt gleichzeitige API-Calls)
        var semaphore = new SemaphoreSlim(5, 5);
        var klineCache = new ConcurrentDictionary<string, IReadOnlyList<Candle>?>();
        var klineTasks = filtered.Select(async ticker =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var candles = await _exchangeClient.GetKlinesAsync(ticker.Symbol, settings.ScanTimeFrame, 100)
                    .ConfigureAwait(false);
                klineCache[ticker.Symbol] = candles;
            }
            catch
            {
                klineCache[ticker.Symbol] = null;
            }
            finally { semaphore.Release(); }
        });
        await Task.WhenAll(klineTasks).ConfigureAwait(false);

        // Multi-dimensionales Scoring (sequenziell, Klines bereits vorgeladen)
        var scored = new List<ScanResult>();
        foreach (var ticker in filtered)
        {
            ct.ThrowIfCancellationRequested();

            var (score, indicators) = await CalculateAdvancedScoreAsync(ticker, settings, ct, klineCache.GetValueOrDefault(ticker.Symbol)).ConfigureAwait(false);
            if (score > 0)
            {
                scored.Add(new ScanResult(
                    ticker.Symbol,
                    score,
                    settings.Mode.ToString(),
                    indicators));
            }
        }

        // Nach Score sortiert zurückgeben
        foreach (var result in scored.OrderByDescending(r => r.Score).Take(settings.MaxResults))
        {
            yield return result;
        }
    }

    /// <summary>
    /// 5-dimensionales Scoring: Trend (30%) + Volumen (25%) + Momentum (20%) + Volatilität (15%) + Struktur (10%).
    /// Gibt (Score, Indicators) zurück. Score = 0 wenn Kandidat abgelehnt wird.
    /// </summary>
    private async Task<(decimal Score, Dictionary<string, decimal> Indicators)> CalculateAdvancedScoreAsync(
        Ticker ticker, ScannerSettings settings, CancellationToken ct, IReadOnlyList<Candle>? preloadedCandles = null)
    {
        var indicators = new Dictionary<string, decimal>
        {
            ["Volume24h"] = ticker.Volume24h,
            ["PriceChange"] = ticker.PriceChangePercent24h,
            ["LastPrice"] = ticker.LastPrice
        };

        // Vorgeladene Klines verwenden, oder nachladen als Fallback
        var candles = preloadedCandles;
        if (candles == null)
        {
            try
            {
                candles = await _exchangeClient.GetKlinesAsync(ticker.Symbol, settings.ScanTimeFrame, 100)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Klines für {Symbol} nicht verfügbar: {Error}", ticker.Symbol, ex.Message);
            }
        }

        // Ohne Klines: Vereinfachtes Scoring (nur Ticker-Daten)
        if (candles == null || candles.Count < 50)
        {
            var simpleScore = CalculateSimpleScore(ticker, settings.Mode);
            return (simpleScore, indicators);
        }

        // === 1. TREND-SCORE (30%) - Richtung und Stärke des Trends ===
        var ema20 = IndicatorHelper.CalculateEma(candles, 20);
        var ema50 = IndicatorHelper.CalculateEma(candles, 50);
        var adx = IndicatorHelper.CalculateAdx(candles);

        var lastEma20 = ema20.Count > 0 && ema20[^1].HasValue ? ema20[^1]!.Value : 0m;
        var lastEma50 = ema50.Count > 0 && ema50[^1].HasValue ? ema50[^1]!.Value : 0m;
        var lastAdx = adx.Count > 0 && adx[^1].HasValue ? adx[^1]!.Value : 0m;
        var close = candles[^1].Close;

        // Trend-Richtung: Preis über EMA20 > EMA50 = bullish
        var emaTrendAligned = (lastEma20 > 0 && lastEma50 > 0 && close > lastEma20 && lastEma20 > lastEma50) ||
                              (lastEma20 > 0 && lastEma50 > 0 && close < lastEma20 && lastEma20 < lastEma50);
        var trendDirectionScore = emaTrendAligned ? 1.0m : 0.3m;
        // ADX-Stärke: >25 = starker Trend, >40 = sehr stark
        var adxScore = lastAdx > 40 ? 1.0m : lastAdx > 25 ? 0.7m : lastAdx > 20 ? 0.4m : 0.1m;
        var trendScore = (trendDirectionScore * 0.5m + adxScore * 0.5m);

        indicators["TrendScore"] = Math.Round(trendScore, 3);
        indicators["ADX"] = Math.Round(lastAdx, 1);

        // === 2. VOLUMEN-SCORE (25%) - Relatives Volumen und Konsistenz ===
        var volumeRatio = CalculateVolumeRatio(candles);
        // Höheres Volumen = besserer Score (aber nicht linear, Diminishing Returns ab 3x)
        var volumeScore = volumeRatio > 3m ? 1.0m
            : volumeRatio > 2m ? 0.8m
            : volumeRatio > 1.5m ? 0.6m
            : volumeRatio > 1.0m ? 0.4m
            : 0.1m;

        indicators["VolumeRatio"] = Math.Round(volumeRatio, 2);
        indicators["VolumeScore"] = Math.Round(volumeScore, 3);

        // === 3. MOMENTUM-SCORE (20%) - RSI + MACD-Richtung ===
        var rsi = IndicatorHelper.CalculateRsi(candles);
        var macd = IndicatorHelper.CalculateMacd(candles);
        var lastRsi = rsi.Count > 0 && rsi[^1].HasValue ? rsi[^1]!.Value : 50m;
        var lastMacdHist = macd.Histogram.Count > 0 && macd.Histogram[^1].HasValue ? macd.Histogram[^1]!.Value : 0m;
        var prevMacdHist = macd.Histogram.Count > 1 && macd.Histogram[^2].HasValue ? macd.Histogram[^2]!.Value : 0m;

        // RSI im gesunden Bereich (nicht überkauft/überverkauft)
        var rsiScore = lastRsi is >= 40 and <= 70 ? 0.8m
            : lastRsi is >= 30 and <= 80 ? 0.5m
            : 0.1m;
        // MACD-Histogram wachsend = bullish Momentum
        var macdGrowing = Math.Abs(lastMacdHist) > Math.Abs(prevMacdHist) && Math.Sign((double)lastMacdHist) == Math.Sign((double)prevMacdHist);
        var macdScore = macdGrowing ? 0.8m : 0.3m;
        var momentumScore = rsiScore * 0.5m + macdScore * 0.5m;

        indicators["RSI"] = Math.Round(lastRsi, 1);
        indicators["MomentumScore"] = Math.Round(momentumScore, 3);

        // === 4. VOLATILITÄTS-SCORE (15%) - ATR vs. Durchschnitt ===
        var atr = IndicatorHelper.CalculateAtr(candles, 14);
        var lastAtr = atr.Count > 0 && atr[^1].HasValue ? atr[^1]!.Value : 0m;
        var atrPercent = close > 0 ? lastAtr / close * 100m : 0m;
        // Ideal: 1-4% ATR für H4 (genug Bewegung, nicht chaotisch)
        var volatilityScore = atrPercent is >= 1m and <= 4m ? 1.0m
            : atrPercent is >= 0.5m and <= 6m ? 0.6m
            : 0.2m;

        indicators["ATR%"] = Math.Round(atrPercent, 2);
        indicators["VolatilityScore"] = Math.Round(volatilityScore, 3);

        // === 5. STRUKTUR-SCORE (10%) - Pullback-Tiefe, kein Überschießen ===
        // Wie weit ist der Preis von EMA20 entfernt? Nahe = guter Entry, weit = Überkauft
        var distFromEma = lastEma20 > 0 ? Math.Abs(close - lastEma20) / lastEma20 * 100m : 0m;
        var structureScore = distFromEma < 1m ? 1.0m    // Sehr nahe an EMA = guter Pullback-Entry
            : distFromEma < 2m ? 0.7m
            : distFromEma < 4m ? 0.4m
            : 0.1m;                                      // >4% = überdehnt, schlechter Entry

        indicators["EMA20Dist%"] = Math.Round(distFromEma, 2);
        indicators["StructureScore"] = Math.Round(structureScore, 3);

        // === Gesamt-Score (gewichtet) ===
        var totalScore = settings.Mode switch
        {
            ScanMode.Momentum => trendScore * 0.20m + volumeScore * 0.25m + momentumScore * 0.30m + volatilityScore * 0.15m + structureScore * 0.10m,
            ScanMode.Breakout => trendScore * 0.15m + volumeScore * 0.35m + momentumScore * 0.25m + volatilityScore * 0.15m + structureScore * 0.10m,
            ScanMode.Reversal => trendScore * 0.10m + volumeScore * 0.20m + momentumScore * 0.15m + volatilityScore * 0.20m + structureScore * 0.35m,
            ScanMode.VolumeSurge => trendScore * 0.10m + volumeScore * 0.50m + momentumScore * 0.15m + volatilityScore * 0.15m + structureScore * 0.10m,
            _ => trendScore * 0.30m + volumeScore * 0.25m + momentumScore * 0.20m + volatilityScore * 0.15m + structureScore * 0.10m
        };

        // Modus-spezifische Abzüge
        if (settings.Mode == ScanMode.Momentum && !emaTrendAligned)
            totalScore *= 0.5m; // Momentum ohne Trend-Alignment = halber Score

        if (settings.Mode == ScanMode.Breakout && volumeRatio < 1.5m)
            totalScore *= 0.3m; // Breakout ohne Volumen-Bestätigung = stark abgewertet

        indicators["TotalScore"] = Math.Round(totalScore, 4);
        return (totalScore, indicators);
    }

    /// <summary>
    /// Fallback-Scoring ohne Klines (nur Ticker-Daten).
    /// Normalisiert auf [0, 1] Bereich, damit SimpleScore nicht das Advanced-Scoring dominiert.
    /// </summary>
    private static decimal CalculateSimpleScore(Ticker ticker, ScanMode mode)
    {
        var raw = mode switch
        {
            ScanMode.Momentum => Math.Abs(ticker.PriceChangePercent24h) * (ticker.Volume24h / 1_000_000m) * 0.01m,
            ScanMode.Reversal => (100m - Math.Abs(ticker.PriceChangePercent24h)) * (ticker.Volume24h / 1_000_000m) * 0.01m,
            ScanMode.Breakout => Math.Abs(ticker.PriceChangePercent24h) * 2m * (ticker.Volume24h / 1_000_000m) * 0.01m,
            ScanMode.VolumeSurge => ticker.Volume24h / 1_000_000m * 0.01m,
            _ => ticker.Volume24h / 1_000_000m * 0.01m
        };
        // Clamp auf [0, 1] damit Kandidaten ohne Klines (SimpleScore) nicht die
        // korrekt berechneten Advanced-Scores (ebenfalls 0-1) dominieren
        return Math.Min(1m, raw);
    }

    /// <summary>Aktuelles Volumen relativ zum 20-Perioden-Durchschnitt.</summary>
    private static decimal CalculateVolumeRatio(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < 21) return 1m;
        var currentVolume = candles[^1].Volume;
        var avgVolume = 0m;
        for (int i = candles.Count - 21; i < candles.Count - 1; i++)
            avgVolume += candles[i].Volume;
        avgVolume /= 20m;
        return avgVolume > 0 ? currentVolume / avgVolume : 1m;
    }
}
