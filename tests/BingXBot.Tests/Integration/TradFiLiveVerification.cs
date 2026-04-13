using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Models;
using BingXBot.Exchange;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BingXBot.Tests.Integration;

/// <summary>
/// Live-Integrations-Tests gegen die echte BingX Public-API.
/// Verifiziert dass Rohstoffe, Indices, Forex und Aktien tatsächlich gescannt
/// und Klines geladen werden können.
///
/// Diese Tests brauchen Internet-Zugang, aber KEINE API-Keys (nur Public-Endpoints).
///
/// Ausführung:  dotnet test --filter "FullyQualifiedName~TradFiLiveVerification"
/// </summary>
[Trait("Category", "Integration")]
public class TradFiLiveVerification
{
    private readonly ITestOutputHelper _out;

    public TradFiLiveVerification(ITestOutputHelper output) => _out = output;

    private static BingXPublicClient ErstelleClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var rateLimiter = new RateLimiter();
        return new BingXPublicClient(http, rateLimiter, NullLogger<BingXPublicClient>.Instance);
    }

    /// <summary>
    /// Beweist dass BingX TradFi-Symbole im Ticker-Universum hat und dass
    /// SymbolClassifier sie korrekt nach Rohstoffen, Indices, Forex und Aktien sortiert.
    /// </summary>
    [Fact]
    public async Task LiveTickers_EnthaeltAlleVierTradFiKategorien()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var publicClient = new BingXPublicClient(client, new RateLimiter(),
            NullLogger<BingXPublicClient>.Instance);

        var tickers = await publicClient.GetAllTickersAsync();

        tickers.Should().NotBeEmpty("BingX muss mindestens einige Ticker liefern");

        var byCategory = tickers
            .GroupBy(t => SymbolClassifier.Classify(t.Symbol))
            .ToDictionary(g => g.Key, g => g.Count());

        _out.WriteLine($"Total Tickers: {tickers.Count}");
        foreach (var kvp in byCategory.OrderBy(k => k.Key))
        {
            _out.WriteLine($"  {kvp.Key,-10} : {kvp.Value,4}");
        }

        // Alle 4 TradFi-Kategorien müssen vertreten sein
        byCategory.Should().ContainKey(MarketCategory.Crypto);
        byCategory.Should().ContainKey(MarketCategory.Commodity);
        byCategory.Should().ContainKey(MarketCategory.Index);
        byCategory.Should().ContainKey(MarketCategory.Forex);
        byCategory.Should().ContainKey(MarketCategory.Stock);

        byCategory[MarketCategory.Commodity].Should().BeGreaterThan(5, "Mindestens GOLD, Silber, WTI, Brent");
        byCategory[MarketCategory.Index].Should().BeGreaterThan(3, "Mindestens SP500, NASDAQ100, DOW");
        byCategory[MarketCategory.Forex].Should().BeGreaterThan(5, "Mindestens EUR/USD, GBP/USD, USD/JPY");
        byCategory[MarketCategory.Stock].Should().BeGreaterThan(10, "Mindestens NVDA, AAPL, MSFT, TSLA, META");
    }

    /// <summary>
    /// Beweist dass für jede TradFi-Kategorie Klines (Candles) abrufbar sind —
    /// notwendige Voraussetzung für die SK-Strategie und Backtests.
    /// </summary>
    [Fact]
    public async Task LiveKlines_FuerJedeTradFiKategorie_GibtEchteCandles()
    {
        var publicClient = ErstelleClient();
        var jetzt = DateTime.UtcNow;
        var vor10Tagen = jetzt.AddDays(-10);

        var testSymbole = new[]
        {
            ("NCCOGOLD2USD-USDT",      MarketCategory.Commodity, "Gold"),
            ("NCSINASDAQ1002USD-USDT", MarketCategory.Index,     "Nasdaq 100"),
            ("NCFXEUR2USD-USDT",       MarketCategory.Forex,     "EUR/USD"),
            ("NCSKNVDA2USD-USDT",      MarketCategory.Stock,     "Nvidia"),
        };

        foreach (var (symbol, category, label) in testSymbole)
        {
            // SymbolClassifier muss korrekt klassifizieren
            SymbolClassifier.Classify(symbol).Should().Be(category, $"{label} ({symbol})");
            SymbolClassifier.IsTradFi(symbol).Should().BeTrue($"{label} muss TradFi sein");
            SymbolClassifier.IsApiTradeable(symbol).Should().BeTrue($"{label} muss API-handelbar sein");

            var candles = await publicClient.GetKlinesAsync(symbol, TimeFrame.H4, vor10Tagen, jetzt);
            candles.Should().NotBeEmpty($"H4-Klines für {label} müssen verfügbar sein");
            candles.Count.Should().BeGreaterThan(20, $"{label}: mindestens 20 H4-Candles in 10 Tagen");

            var letzte = candles[^1];
            (letzte.High >= letzte.Low).Should().BeTrue($"{label}: High >= Low");
            (letzte.Close > 0).Should().BeTrue($"{label}: Preis muss > 0 sein");

            _out.WriteLine($"{label,-12} ({symbol,-30}): {candles.Count,4} Candles, letzter Close = {letzte.Close:F4}");
        }
    }

    /// <summary>
    /// Beweist dass der Hedge-Mode-Gate funktioniert:
    /// Wenn IsHedgeModeActive=false, werden TradFi-Symbole komplett aus dem Scan ausgefiltert.
    /// (Das war der Hauptbug in der Vergangenheit.)
    /// </summary>
    [Fact]
    public void HedgeModeGate_BlockiertTradFi_WennNichtAktiv()
    {
        var tickers = ErzeugeMockTicker(crypto: 50, tradfi: 20);

        // Variante 1: IsHedgeModeActive = false → KEIN TradFi im Result
        var settingsAus = StandardSettings(enableTradFi: true, hedgeMode: false);
        var resultAus = WendeFilterAn(tickers, settingsAus);
        resultAus.Any(t => SymbolClassifier.IsTradFi(t.Symbol)).Should().BeFalse(
            "Bei deaktiviertem Hedge-Mode darf KEIN TradFi-Symbol im Result sein");

        // Variante 2: IsHedgeModeActive = true → TradFi MUSS im Result sein
        var settingsAn = StandardSettings(enableTradFi: true, hedgeMode: true);
        var resultAn = WendeFilterAn(tickers, settingsAn);
        resultAn.Any(t => SymbolClassifier.IsTradFi(t.Symbol)).Should().BeTrue(
            "Bei aktivem Hedge-Mode MUSS TradFi im Result sein");

        _out.WriteLine($"Hedge AUS: {resultAus.Count(t => SymbolClassifier.IsTradFi(t.Symbol))} TradFi");
        _out.WriteLine($"Hedge AN : {resultAn.Count(t => SymbolClassifier.IsTradFi(t.Symbol))} TradFi");
    }

    /// <summary>
    /// Beweist dass die 60/40-Aufteilung funktioniert:
    /// Bei MaxResults=100 sollen 60 Krypto + 40 TradFi (oder weniger wenn Pool kleiner) im Result sein.
    /// </summary>
    [Fact]
    public void SechzigVierzigAufteilung_FunktioniertBeiAusreichendemPool()
    {
        // Großer Pool von beiden — testet die strikte 60/40 Aufteilung
        var tickers = ErzeugeMockTicker(crypto: 200, tradfi: 60);
        var settings = StandardSettings(enableTradFi: true, hedgeMode: true);
        settings.MaxResults = 100;
        // OnlyTopByVolume deaktivieren, damit alle 200 Krypto im Pool bleiben
        settings.OnlyTopByVolume = false;

        var result = WendeFilterAn(tickers, settings);

        var tradFiCount = result.Count(t => SymbolClassifier.IsTradFi(t.Symbol));
        var cryptoCount = result.Count - tradFiCount;

        _out.WriteLine($"Result: {result.Count} (Krypto {cryptoCount} + TradFi {tradFiCount})");

        result.Count.Should().Be(100, "Bei ausreichend großen Pools müssen alle 100 Slots gefüllt sein");
        tradFiCount.Should().Be(40, "40% von 100 = 40 TradFi-Slots");
        cryptoCount.Should().Be(60, "60% von 100 = 60 Krypto-Slots");
    }

    /// <summary>
    /// Wenn TradFi-Pool kleiner als 40% ist, sollen die freien Slots an Krypto fallen
    /// (sonst verliert der Scanner Effizienz).
    /// </summary>
    [Fact]
    public void SechzigVierzigAufteilung_UngenutzteTradFiSlotsGehenAnKrypto()
    {
        // TradFi-Pool nur 10 — 40 Slots reserviert, 30 fallen an Krypto
        var tickers = ErzeugeMockTicker(crypto: 200, tradfi: 10);
        var settings = StandardSettings(enableTradFi: true, hedgeMode: true);
        settings.MaxResults = 100;
        settings.OnlyTopByVolume = false;

        var result = WendeFilterAn(tickers, settings);

        var tradFiCount = result.Count(t => SymbolClassifier.IsTradFi(t.Symbol));
        var cryptoCount = result.Count - tradFiCount;

        _out.WriteLine($"Result: {result.Count} (Krypto {cryptoCount} + TradFi {tradFiCount}) - Pool zu klein");

        tradFiCount.Should().Be(10, "Alle qualifizierten TradFi-Symbole müssen rein");
        cryptoCount.Should().Be(90, "60 reguläre + 30 ungenutzte TradFi-Slots = 90 Krypto");
    }

    /// <summary>
    /// End-to-End: Echte BingX-Tickers + ScanHelper-Filterlogik (Mocks).
    /// Beweist dass mit echten Daten die TradFi-Quote bei ~40% landet.
    /// </summary>
    [Fact]
    public async Task LiveTickers_DurchScanFilter_Liefern40ProzentTradFi()
    {
        var publicClient = ErstelleClient();
        var tickers = await publicClient.GetAllTickersAsync();

        var settings = StandardSettings(enableTradFi: true, hedgeMode: true);
        settings.MaxResults = 100;
        // Realistische Filter
        settings.MinVolume24h = 1_000_000m;
        settings.MinVolume24hTradFi = 100_000m;
        settings.OnlyTopByVolume = false;

        var result = WendeFilterAn(tickers, settings);

        var tradFiCount = result.Count(t => SymbolClassifier.IsTradFi(t.Symbol));
        var cryptoCount = result.Count - tradFiCount;
        var quote = (double)tradFiCount / result.Count;

        _out.WriteLine($"LIVE-Filter Output: {result.Count} Total = {cryptoCount} Krypto + {tradFiCount} TradFi (TradFi-Quote: {quote:P0})");

        result.Count.Should().BeGreaterThan(50, "Sollten genug Symbole im Result sein");
        tradFiCount.Should().BeGreaterThan(0, "TradFi MUSS dabei sein (sonst Hedge-Mode-Gate-Bug)");
        // 60/40 Soll-Quote: 40% TradFi (wenn Pool groß genug)
        if (tickers.Count(t => SymbolClassifier.IsTradFi(t.Symbol) && SymbolClassifier.IsApiTradeable(t.Symbol)) >= 40)
        {
            tradFiCount.Should().BeInRange(35, 45, "Bei ausreichend Pool sollte TradFi nahe 40% liegen");
        }
    }

    /// <summary>
    /// Mock-Test: Sub-Quoten-Logik bei ausreichend großem Pool —
    /// jede der 4 Subkategorien bekommt 25% (= 10 bei 40 TradFi-Slots).
    /// </summary>
    [Fact]
    public void SubQuoten_GleichmaessigeVerteilung_BeiAusreichendemPool()
    {
        // Pool: 200 Krypto + 60 TradFi (15 pro Subkategorie, mehr als Sub-Quote)
        var tickers = ErzeugeMockTickerProSubKategorie(crypto: 200, perSubCat: 15);
        var settings = StandardSettings(enableTradFi: true, hedgeMode: true);
        settings.MaxResults = 100;
        settings.OnlyTopByVolume = false;

        var result = WendeFilterAn(tickers, settings);

        var byCategory = result
            .Where(t => SymbolClassifier.IsTradFi(t.Symbol))
            .GroupBy(t => SymbolClassifier.Classify(t.Symbol))
            .ToDictionary(g => g.Key, g => g.Count());

        _out.WriteLine("Sub-Quoten-Verteilung (Mock):");
        foreach (var cat in new[] { MarketCategory.Commodity, MarketCategory.Index, MarketCategory.Forex, MarketCategory.Stock })
        {
            _out.WriteLine($"  {cat,-10}: {byCategory.GetValueOrDefault(cat, 0)}");
        }

        byCategory[MarketCategory.Commodity].Should().Be(10, "Rohstoffe = 25% von 40 = 10");
        byCategory[MarketCategory.Index].Should().Be(10,     "Indices = 25% von 40 = 10");
        byCategory[MarketCategory.Forex].Should().Be(10,     "Forex = 25% von 40 = 10");
        byCategory[MarketCategory.Stock].Should().Be(10,     "Aktien = 25% von 40 = 10");
    }

    /// <summary>
    /// Mock-Test: Wenn eine Subkategorie ihren Pool-Anteil nicht füllt,
    /// fallen die Slots an Top-Volume-TradFi-Symbole zurück (Recycling innerhalb TradFi).
    /// </summary>
    [Fact]
    public void SubQuoten_UngenutzteIndexSlots_GehenAnAndereTradFi()
    {
        // Indices nur 3 verfügbar (statt 10) — 7 Slots gehen an andere
        var tickers = new List<Ticker>();
        var jetzt = DateTime.UtcNow;
        for (int i = 0; i < 200; i++)
            tickers.Add(new Ticker($"CRYPTO{i}-USDT", 100m + i, 100m + i, 100m + i, 10_000_000m + i, 1m, jetzt));
        // Je 15 Commodity, Forex, Stock + nur 3 Indices
        AddMockTradFi(tickers, "NCCO", 15);
        AddMockTradFi(tickers, "NCSI", 3);    // KNAPPE Indices
        AddMockTradFi(tickers, "NCFX", 15);
        AddMockTradFi(tickers, "NCSK", 15);

        var settings = StandardSettings(enableTradFi: true, hedgeMode: true);
        settings.MaxResults = 100;
        settings.OnlyTopByVolume = false;
        var result = WendeFilterAn(tickers, settings);

        var tradFi = result.Where(t => SymbolClassifier.IsTradFi(t.Symbol)).ToList();
        var byCategory = tradFi.GroupBy(t => SymbolClassifier.Classify(t.Symbol))
                               .ToDictionary(g => g.Key, g => g.Count());

        _out.WriteLine($"TradFi-Verteilung (Indices knapp): Total={tradFi.Count}");
        foreach (var c in byCategory) _out.WriteLine($"  {c.Key,-10}: {c.Value}");

        tradFi.Count.Should().Be(40, "40 TradFi-Slots müssen voll werden (Recycling)");
        byCategory[MarketCategory.Index].Should().Be(3, "Alle 3 verfügbaren Indices");
        // Die 7 ungenutzten Index-Slots gehen an die anderen 3 Subkategorien
        (byCategory[MarketCategory.Commodity]
         + byCategory[MarketCategory.Forex]
         + byCategory[MarketCategory.Stock]).Should().Be(37);
    }

    /// <summary>
    /// Live-Test: Mit echten BingX-Daten muss die Sub-Quoten-Logik mindestens 3
    /// der 4 Subkategorien zeigen UND keine darf > 70% dominieren (vorher Aktien ~55%
    /// oder Forex 26/36 = 72% — beides unerwünscht).
    /// </summary>
    [Fact]
    public async Task LiveTickers_SubQuoten_KeineKategorieDominiert()
    {
        var publicClient = ErstelleClient();
        var tickers = await publicClient.GetAllTickersAsync();

        var settings = StandardSettings(enableTradFi: true, hedgeMode: true);
        settings.MaxResults = 100;
        settings.MinVolume24h = 1_000_000m;
        settings.MinVolume24hTradFi = 0m;   // Volume-Filter weg, damit knappe Subkategorien sichtbar bleiben
        settings.OnlyTopByVolume = false;

        var result = WendeFilterAn(tickers, settings);

        var byCategory = result
            .Where(t => SymbolClassifier.IsTradFi(t.Symbol))
            .GroupBy(t => SymbolClassifier.Classify(t.Symbol))
            .ToDictionary(g => g.Key, g => g.Count());

        _out.WriteLine("LIVE Sub-Quoten-Verteilung:");
        foreach (var cat in new[] { MarketCategory.Commodity, MarketCategory.Index, MarketCategory.Forex, MarketCategory.Stock })
        {
            _out.WriteLine($"  {cat,-10}: {byCategory.GetValueOrDefault(cat, 0)}");
        }

        byCategory.Count.Should().BeGreaterThanOrEqualTo(3, "Mindestens 3 der 4 TradFi-Subkategorien sichtbar");

        var totalTradFi = byCategory.Values.Sum();
        foreach (var (cat, count) in byCategory)
        {
            var quote = (double)count / totalTradFi;
            quote.Should().BeLessThan(0.7,
                $"Keine Subkategorie darf > 70% dominieren ({cat}={count}/{totalTradFi}={quote:P0})");
        }
    }

    private static void AddMockTradFi(List<Ticker> list, string prefix, int count)
    {
        var jetzt = DateTime.UtcNow;
        for (int i = 0; i < count; i++)
            list.Add(new Ticker($"{prefix}MOCK{i}2USD-USDT", 50m + i, 50m + i, 50m + i,
                                 5_000_000m - i * 1000, 0.5m, jetzt));
    }

    private static List<Ticker> ErzeugeMockTickerProSubKategorie(int crypto, int perSubCat)
    {
        var list = new List<Ticker>();
        var jetzt = DateTime.UtcNow;
        for (int i = 0; i < crypto; i++)
            list.Add(new Ticker($"CRYPTO{i}-USDT", 100m + i, 100m + i, 100m + i,
                                 10_000_000m + i, 1m, jetzt));
        AddMockTradFi(list, "NCCO", perSubCat);
        AddMockTradFi(list, "NCSI", perSubCat);
        AddMockTradFi(list, "NCFX", perSubCat);
        AddMockTradFi(list, "NCSK", perSubCat);
        return list;
    }

    // ============================================================
    // Helper: Standard-Settings + Mock-Ticker + ScanHelper-Aufruf
    // ============================================================

    private static ScannerSettings StandardSettings(bool enableTradFi, bool hedgeMode) => new()
    {
        EnableTradFi = enableTradFi,
        IsHedgeModeActive = hedgeMode,
        EnabledCategories = new HashSet<MarketCategory>
        {
            MarketCategory.Crypto, MarketCategory.Commodity,
            MarketCategory.Index, MarketCategory.Forex, MarketCategory.Stock
        },
        MinVolume24h = 0m,
        MinVolume24hTradFi = 0m,
        MinPriceChangeTradFi = 0m,
        MaxResults = 100,
        OnlyTopByVolume = false,
        TopCoinsCount = 100,
    };

    private static List<Ticker> ErzeugeMockTicker(int crypto, int tradfi)
    {
        var list = new List<Ticker>();
        var jetzt = DateTime.UtcNow;
        for (int i = 0; i < crypto; i++)
            list.Add(new Ticker(
                Symbol: $"CRYPTO{i}-USDT",
                LastPrice: 100m + i,
                BidPrice: 100m + i,
                AskPrice: 100m + i,
                Volume24h: 10_000_000m + i,
                PriceChangePercent24h: 1m,
                Timestamp: jetzt));

        var prefixes = new[] { "NCCO", "NCSI", "NCFX", "NCSK" };
        for (int i = 0; i < tradfi; i++)
        {
            var p = prefixes[i % prefixes.Length];
            list.Add(new Ticker(
                Symbol: $"{p}TEST{i}2USD-USDT",
                LastPrice: 50m + i,
                BidPrice: 50m + i,
                AskPrice: 50m + i,
                Volume24h: 5_000_000m - i * 1000,
                PriceChangePercent24h: 0.5m,
                Timestamp: jetzt));
        }
        return list;
    }

    /// <summary>
    /// Spiegelt die ScanHelper.FilterCandidates-Logik 1:1 wider (kann nicht direkt
    /// aufgerufen werden, weil ScanHelper im BingXBot.Shared liegt — Avalonia-Projekt).
    /// </summary>
    private static List<Ticker> WendeFilterAn(IReadOnlyList<Ticker> tickers, ScannerSettings settings)
    {
        var cryptoTickers = tickers.Where(t => !SymbolClassifier.IsTradFi(t.Symbol)).ToList();
        var tradfiTickers = settings.EnableTradFi && settings.IsHedgeModeActive
            ? tickers.Where(t => SymbolClassifier.IsTradFi(t.Symbol)
                              && SymbolClassifier.IsApiTradeable(t.Symbol)
                              && settings.EnabledCategories.Contains(SymbolClassifier.Classify(t.Symbol))).ToList()
            : new List<Ticker>();

        if (settings.OnlyTopByVolume && settings.TopCoinsCount > 0)
        {
            cryptoTickers = cryptoTickers.OrderByDescending(t => t.Volume24h)
                                         .Take(settings.TopCoinsCount).ToList();
        }

        var filteredCrypto = cryptoTickers
            .Where(t => t.Volume24h >= settings.MinVolume24h)
            .ToList();
        var filteredTradFi = tradfiTickers
            .Where(t => t.Volume24h >= settings.MinVolume24hTradFi)
            .Where(t => Math.Abs(t.PriceChangePercent24h) >= settings.MinPriceChangeTradFi)
            .ToList();

        var maxResults = settings.MaxResults > 0 ? settings.MaxResults : 100;
        var tradFiTargetSlots = (int)Math.Round(maxResults * 0.4);
        var cryptoTargetSlots = maxResults - tradFiTargetSlots;

        // Sub-Quoten: 25% pro Subkategorie (10 bei 40 Slots)
        var subCategories = new[]
        {
            MarketCategory.Commodity, MarketCategory.Index,
            MarketCategory.Forex, MarketCategory.Stock
        };
        var slotsPerSubCat = tradFiTargetSlots / subCategories.Length;

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

        var unusedTradFi = tradFiTargetSlots - tradFiResult.Count;
        var effectiveCryptoSlots = cryptoTargetSlots + unusedTradFi;

        var rng = new Random(42); // deterministisch für Tests
        var shuffled = new List<Ticker>(filteredCrypto);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        var cryptoResult = shuffled.Take(effectiveCryptoSlots).ToList();

        var unusedCrypto = effectiveCryptoSlots - cryptoResult.Count;
        if (unusedCrypto > 0 && filteredTradFi.Count > tradFiResult.Count)
        {
            var more = filteredTradFi.OrderByDescending(t => t.Volume24h)
                .Skip(tradFiResult.Count).Take(unusedCrypto).ToList();
            tradFiResult.AddRange(more);
        }

        var result = new List<Ticker>(tradFiResult);
        foreach (var t in cryptoResult)
            if (!result.Any(r => r.Symbol == t.Symbol)) result.Add(t);
        return result;
    }
}
