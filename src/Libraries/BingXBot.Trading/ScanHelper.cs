using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.Indicators;
using BingXBot.Engine.Risk;

namespace BingXBot.Trading;

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

    /// <summary>
    /// Multi-TF Standalone (15.04.2026): Filtert Kandidaten für eine bestimmte Navigator-TF.
    /// Krypto nutzt <see cref="ScannerSettings.MinVolume24hByTf"/>, TradFi nutzt separat
    /// <see cref="ScannerSettings.MinVolume24hTradFiByTf"/> (deutlich niedriger — TradFi-Liquidität).
    /// MaxResults wird aus <see cref="ScannerSettings.MaxResultsByTf"/> gezogen.
    /// </summary>
    public static List<Ticker> FilterCandidatesForTimeframe(
        IReadOnlyList<Ticker> tickers, ScannerSettings settings, TimeFrame navigatorTf)
    {
#pragma warning disable CS0618 // Legacy-Single-TF-Fallback wenn ByTf-Map keinen Wert hat
        var minVolCrypto = settings.MinVolume24hByTf.TryGetValue(navigatorTf, out var vc) && vc > 0
            ? vc : settings.MinVolume24h;
        var minVolTradFi = settings.MinVolume24hTradFiByTf.TryGetValue(navigatorTf, out var vt) && vt > 0
            ? vt : settings.MinVolume24hTradFi;
        var minChgCrypto = settings.MinPriceChangeByTf.TryGetValue(navigatorTf, out var pc) && pc >= 0
            ? pc : settings.MinPriceChange;
        var minChgTradFi = settings.MinPriceChangeTradFiByTf.TryGetValue(navigatorTf, out var pt) && pt >= 0
            ? pt : settings.MinPriceChangeTradFi;
        var maxResults = settings.MaxResultsByTf.TryGetValue(navigatorTf, out var mr) && mr > 0
            ? mr : settings.MaxResults;
#pragma warning restore CS0618

        return FilterCandidatesCore(tickers, settings,
            minVolCrypto, minVolTradFi, minChgCrypto, minChgTradFi, maxResults);
    }

    /// <summary>Legacy-Overload: Nutzt die globalen Settings-Werte (für Backtest + Single-TF-UI).</summary>
    public static List<Ticker> FilterCandidates(IReadOnlyList<Ticker> tickers, ScannerSettings settings)
    {
#pragma warning disable CS0618 // Legacy-Single-TF-Pfad
        return FilterCandidatesCore(tickers, settings,
            settings.MinVolume24h, settings.MinVolume24hTradFi,
            settings.MinPriceChange, settings.MinPriceChangeTradFi,
            settings.MaxResults > 0 ? settings.MaxResults : 100);
#pragma warning restore CS0618
    }

    private static List<Ticker> FilterCandidatesCore(
        IReadOnlyList<Ticker> tickers, ScannerSettings settings,
        decimal minVolCrypto, decimal minVolTradFi,
        decimal minChgCrypto, decimal minChgTradFi,
        int maxResults)
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

        // User-Vorgabe 13.04.2026: ALLE 4 TradFi-Kategorien immer aktiv. Kein EnabledCategories-Filter mehr.
        // EnabledCategories wird nur noch fuer Anzeige/Log verwendet — immer alle 5 drin gehalten.
        settings.EnabledCategories.Add(MarketCategory.Crypto);
        settings.EnabledCategories.Add(MarketCategory.Commodity);
        settings.EnabledCategories.Add(MarketCategory.Index);
        settings.EnabledCategories.Add(MarketCategory.Forex);
        settings.EnabledCategories.Add(MarketCategory.Stock);

        var tradfiTickers = settings.EnableTradFi && settings.IsHedgeModeActive
            ? tickers.Where(t => SymbolClassifier.IsTradFi(t.Symbol)
                              && SymbolClassifier.IsApiTradeable(t.Symbol)).ToList()
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

        // Krypto: Volume-Filter (per-TF wenn vorhanden, sonst global).
        // PriceChange optional — wenn >0 gesetzt wird er angewendet.
        var filteredCrypto = cryptoTickers
            .Where(t => t.Volume24h >= minVolCrypto)
            .Where(t => minChgCrypto <= 0m || Math.Abs(t.PriceChangePercent24h) >= minChgCrypto)
            .Where(t => settings.Blacklist.Count == 0 || !settings.Blacklist.Contains(t.Symbol))
            .ToList();

        // TradFi: Volume-Filter deutlich niedriger (siehe MinVolume24hTradFiByTf).
        // Sanity-Check auf 100k als Minimum — darunter sind nur inaktive Symbole.
        var filteredTradFi = tradfiTickers
            .Where(t => t.Volume24h >= Math.Max(100_000m, minVolTradFi))
            .Where(t => minChgTradFi <= 0m || Math.Abs(t.PriceChangePercent24h) >= minChgTradFi)
            .Where(t => settings.Blacklist.Count == 0 || !settings.Blacklist.Contains(t.Symbol))
            .ToList();

        // 60/40 Aufteilung: 60% Krypto, 40% TradFi (User-Vorgabe 13.04.2026)
        // Ungenutzte Slots einer Seite werden an die andere Seite weitergegeben,
        // damit das Scan-Volumen erhalten bleibt wenn ein Pool kleiner ist.
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

    // BUCH-ONLY: CheckCorrelationAsync entfernt. Das Buch kennt keine Pearson-Korrelation als Gate.

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
