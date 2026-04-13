using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
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
    /// Bei jedem Scan werden die Krypto-Kandidaten zufällig gemischt (Fisher-Yates-Shuffle).
    /// </summary>
    [ThreadStatic] private static Random? _rng;

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

        // Krypto und TradFi trennen (TradFi = NC-Prefix)
        // WICHTIG: TradFi braucht Hedge-Modus auf BingX (Error 101414 bei One-Way-Mode)
        var cryptoTickers = tickers.Where(t => !SymbolClassifier.IsTradFi(t.Symbol)).ToList();
        var tradfiTickers = settings.EnableTradFi && settings.IsHedgeModeActive
            ? tickers.Where(t => SymbolClassifier.IsTradFi(t.Symbol)
                              && SymbolClassifier.IsApiTradeable(t.Symbol)
                              && settings.EnabledCategories.Contains(SymbolClassifier.Classify(t.Symbol))).ToList()
            : new List<Ticker>();

        // Top-100 Krypto nach MARKET CAP (CoinGecko-Cache, stündlich aktualisiert).
        // Fallback auf Volume-Ranking wenn CoinGecko nicht erreichbar.
        if (settings.OnlyTopByVolume && settings.TopCoinsCount > 0)
        {
            if (MarketCapCache.IsLoaded)
            {
                // Echte Market-Cap-Filterung: Nur Coins die in den Top-N nach Market Cap sind
                cryptoTickers = cryptoTickers
                    .Where(t => MarketCapCache.IsTopCoin(t.Symbol, settings.TopCoinsCount))
                    .ToList();
            }
            else
            {
                // Fallback: Volume-Ranking (ungenau, aber besser als nichts)
                cryptoTickers = cryptoTickers.OrderByDescending(t => t.Volume24h)
                                             .Take(settings.TopCoinsCount).ToList();
            }
        }

        // Krypto: Nur Volume-Filter (PriceChange-Filter entfernt — reduzierte den Pool
        // bei ruhigem Markt auf ~15-20 Coins, sodass Rotation wirkungslos war)
        var filteredCrypto = cryptoTickers
            .Where(t => t.Volume24h >= settings.MinVolume24h)
            .Where(t => settings.Blacklist.Count == 0 || !settings.Blacklist.Contains(t.Symbol))
            .ToList();

        // TradFi: Eigener Volume- und PriceChange-Filter (niedrigere Schwellen als Krypto)
        var filteredTradFi = tradfiTickers
            .Where(t => t.Volume24h >= settings.MinVolume24hTradFi)
            .Where(t => Math.Abs(t.PriceChangePercent24h) >= settings.MinPriceChangeTradFi)
            .Where(t => settings.Blacklist.Count == 0 || !settings.Blacklist.Contains(t.Symbol))
            .ToList();

        // 60/40 Aufteilung: 60% Krypto, 40% TradFi (User-Vorgabe 13.04.2026)
        // Ungenutzte Slots einer Seite werden an die andere Seite weitergegeben,
        // damit das Scan-Volumen erhalten bleibt wenn ein Pool kleiner ist.
        var maxResults = settings.MaxResults > 0 ? settings.MaxResults : 100;
        var tradFiTargetSlots = (int)Math.Round(maxResults * 0.4);   // 40 bei 100
        var cryptoTargetSlots = maxResults - tradFiTargetSlots;       // 60 bei 100

        // TradFi-Subkategorien gleichmäßig verteilen (je 25% = 10 bei 40 Slots).
        // Ohne Sub-Quoten dominieren Aktien (~55% des Pools nach Volume) und
        // Indices/Rohstoffe verschwinden aus dem Scan.
        var subCategories = new[]
        {
            MarketCategory.Commodity,
            MarketCategory.Index,
            MarketCategory.Forex,
            MarketCategory.Stock
        };
        var slotsPerSubCat = tradFiTargetSlots / subCategories.Length;  // 10 bei 40

        var tradFiResult = new List<Ticker>();
        foreach (var subCat in subCategories)
        {
            var subResult = filteredTradFi
                .Where(t => SymbolClassifier.Classify(t.Symbol) == subCat)
                .OrderByDescending(t => t.Volume24h)
                .Take(slotsPerSubCat)
                .ToList();
            tradFiResult.AddRange(subResult);
        }

        // Ungenutzte Sub-Slots (z.B. Indices < 10 verfügbar) an Top-Volume-TradFi
        // verteilen, damit die 40 TradFi-Slots tatsächlich gefüllt werden.
        var unusedSubSlots = tradFiTargetSlots - tradFiResult.Count;
        if (unusedSubSlots > 0)
        {
            var alreadyChosen = tradFiResult.Select(t => t.Symbol).ToHashSet();
            var fillUp = filteredTradFi
                .Where(t => !alreadyChosen.Contains(t.Symbol))
                .OrderByDescending(t => t.Volume24h)
                .Take(unusedSubSlots);
            tradFiResult.AddRange(fillUp);
        }

        // Wenn TradFi weniger als Ziel füllt: ungenutzte Slots an Krypto weitergeben
        var unusedTradFiSlots = tradFiTargetSlots - tradFiResult.Count;
        var effectiveCryptoSlots = cryptoTargetSlots + unusedTradFiSlots;

        // Krypto: Shuffle + Slot-Limit (Rotation fairer Pool)
        var shuffled = ShuffleList(filteredCrypto);
        var cryptoResult = shuffled.Take(effectiveCryptoSlots).ToList();

        // Symmetrisch: Wenn Krypto weniger als Ziel füllt, ungenutzte Slots an TradFi
        var unusedCryptoSlots = effectiveCryptoSlots - cryptoResult.Count;
        if (unusedCryptoSlots > 0 && filteredTradFi.Count > tradFiResult.Count)
        {
            var additionalTradFi = filteredTradFi
                .OrderByDescending(t => t.Volume24h)
                .Skip(tradFiResult.Count)
                .Take(unusedCryptoSlots)
                .ToList();
            tradFiResult.AddRange(additionalTradFi);
        }

        // TradFi zuerst, danach Krypto (Reihenfolge im Scan-Loop)
        var result = new List<Ticker>(tradFiResult);
        foreach (var t in cryptoResult)
            if (!result.Any(r => r.Symbol == t.Symbol)) result.Add(t);

        return result;
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

    /// <summary>Fisher-Yates-Shuffle: Gibt eine neue zufällig gemischte Liste zurück.</summary>
    private static List<Ticker> ShuffleList(List<Ticker> source)
    {
        _rng ??= new Random();
        var list = new List<Ticker>(source);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}

/// <summary>Ergebnis der Kandidaten-Evaluation (Signal + Context + Candles für Korrelation).</summary>
