using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.Indicators;
using BingXBot.Engine.Risk;

namespace BingXBot.Services;

/// <summary>
/// Gemeinsame Scan-Logik für Paper- und Live-Trading.
/// Extrahiert die identischen Teile: Kandidaten-Filterung, Klines+HTF-Laden,
/// Strategie-Evaluation, Korrelations-Check und Risk-Check.
/// </summary>
public static class ScanHelper
{
    /// <summary>
    /// Filtert Ticker nach Scanner-Kriterien (Volumen, Preisänderung, Black-/Whitelist).
    /// </summary>
    // Rotations-Offset: Pro Scan werden andere Symbole priorisiert
    private static int _rotationOffset;

    public static List<Ticker> FilterCandidates(IReadOnlyList<Ticker> tickers, ScannerSettings settings)
    {
        // Whitelist hat Priorität: Wenn gesetzt, NUR diese Symbole scannen
        if (settings.Whitelist.Count > 0)
        {
            return tickers
                .Where(t => settings.Whitelist.Contains(t.Symbol))
                .OrderByDescending(t => t.Volume24h)
                .ToList();
        }

        var filtered = tickers
            .Where(t => t.Volume24h >= settings.MinVolume24h)
            .Where(t => Math.Abs(t.PriceChangePercent24h) >= settings.MinPriceChange)
            .Where(t => settings.Blacklist.Count == 0 || !settings.Blacklist.Contains(t.Symbol))
            .ToList();

        // Rotation: Nicht immer die gleichen Top-N nach Volume.
        // Teile in 3 Gruppen: Top-Movers (Preisänderung), Top-Volume, Rotation.
        var maxResults = settings.MaxResults;
        var topMovers = filtered.OrderByDescending(t => Math.Abs(t.PriceChangePercent24h)).Take(maxResults / 3).ToList();
        var topVolume = filtered.OrderByDescending(t => t.Volume24h).Take(maxResults / 3).ToList();

        // Rotation: Jeder Scan nimmt andere Symbole aus dem Rest
        var alreadyPicked = new HashSet<string>(topMovers.Select(t => t.Symbol).Concat(topVolume.Select(t => t.Symbol)));
        var remaining = filtered.Where(t => !alreadyPicked.Contains(t.Symbol)).ToList();
        var rotationCount = Math.Min(maxResults - topMovers.Count - topVolume.Count, remaining.Count);
        var offset = Interlocked.Increment(ref _rotationOffset) * rotationCount % Math.Max(remaining.Count, 1);
        var rotated = remaining.Skip(offset).Take(rotationCount)
            .Concat(remaining.Take(Math.Max(0, rotationCount - (remaining.Count - offset))))
            .ToList();

        // Zusammenführen: Keine Duplikate
        var result = new List<Ticker>(topMovers);
        foreach (var t in topVolume)
            if (!result.Any(r => r.Symbol == t.Symbol)) result.Add(t);
        foreach (var t in rotated)
            if (!result.Any(r => r.Symbol == t.Symbol)) result.Add(t);

        return result.Take(maxResults).ToList();
    }

    /// <summary>
    /// Lädt Klines + HTF-Candles und evaluiert die Strategie für einen Kandidaten.
    /// Gibt null zurück wenn nicht genug Daten oder Signal=None.
    /// </summary>
    public static async Task<CandidateResult?> EvaluateCandidateAsync(
        Ticker ticker,
        IPublicMarketDataClient publicClient,
        StrategyManager strategyManager,
        ScannerSettings scannerSettings,
        IReadOnlyList<Position> positions,
        AccountInfo account,
        CancellationToken ct,
        BotEventBus? eventBus = null)
    {
        // Klines laden (letzte 100 Stunden-Candles)
        var candles = await publicClient.GetKlinesAsync(
            ticker.Symbol, scannerSettings.ScanTimeFrame,
            DateTime.UtcNow.AddHours(-100), DateTime.UtcNow, ct).ConfigureAwait(false);

        if (candles.Count < 50) return null;

        // Higher-Timeframe Candles (4h) für Multi-TF-Konfirmation
        List<Candle>? htfCandles = null;
        try
        {
            htfCandles = await publicClient.GetKlinesAsync(
                ticker.Symbol, TimeFrame.H4,
                DateTime.UtcNow.AddDays(-14), DateTime.UtcNow, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; } // Cancellation nicht verschlucken
        catch (Exception ex)
        {
            // HTF-Daten optional, aber Fehler loggen für Diagnose
            eventBus?.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Scanner",
                $"{ticker.Symbol}: HTF-Candles nicht verfügbar: {ex.Message}", ticker.Symbol));
        }

        // Strategie evaluieren
        var strategy = strategyManager.GetOrCreateForSymbol(ticker.Symbol);
        var context = new MarketContext(ticker.Symbol, candles, ticker, positions, account, htfCandles);
        var signal = strategy.Evaluate(context);

        if (signal.Signal == Signal.None) return null;

        return new CandidateResult(signal, context, candles);
    }

    /// <summary>
    /// Prüft Korrelation mit offenen Positionen. Gibt true zurück wenn Trade blockiert werden soll.
    /// </summary>
    public static async Task<bool> CheckCorrelationAsync(
        string symbol,
        IReadOnlyList<Position> positions,
        RiskSettings riskSettings,
        IPublicMarketDataClient publicClient,
        IReadOnlyList<Candle> candles,
        BotEventBus eventBus,
        string logPrefix,
        CancellationToken ct)
    {
        if (!riskSettings.CheckCorrelation || positions.Count == 0)
            return false;

        try
        {
            var isCorrelated = await CorrelationChecker.IsCorrelatedAsync(
                symbol, positions, riskSettings.MaxCorrelation, publicClient, ct,
                preloadedNewSymbolKlines: candles).ConfigureAwait(false);

            if (isCorrelated)
            {
                eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Risk",
                    $"{logPrefix}{symbol}: Abgelehnt - zu hohe Korrelation mit offener Position (>{riskSettings.MaxCorrelation:P0})",
                    symbol));
                return true;
            }
        }
        catch (Exception ex)
        {
            eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Risk",
                $"{logPrefix}{symbol}: Korrelations-Check fehlgeschlagen: {ex.Message}", symbol));
        }

        return false;
    }

    /// <summary>
    /// Prüft Risk-Management. Gibt null zurück wenn Trade erlaubt, sonst den Ablehnungsgrund.
    /// </summary>
    public static RiskCheckResult ValidateRisk(
        SignalResult signal,
        MarketContext context,
        IRiskManager riskManager,
        BotEventBus eventBus,
        string logPrefix,
        decimal? currentFundingRate = null)
    {
        var riskCheck = riskManager.ValidateTrade(signal, context, currentFundingRate);
        if (!riskCheck.IsAllowed)
        {
            eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Risk",
                $"{logPrefix}{context.Symbol}: Trade abgelehnt - {riskCheck.RejectionReason}", context.Symbol));
        }
        return riskCheck;
    }
}

/// <summary>Ergebnis der Kandidaten-Evaluation (Signal + Context + Candles für Korrelation).</summary>
public record CandidateResult(SignalResult Signal, MarketContext Context, IReadOnlyList<Candle> Candles);
