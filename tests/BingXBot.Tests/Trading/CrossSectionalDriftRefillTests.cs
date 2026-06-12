using System.Text.Json;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Trading;
using BingXBot.Trading.CrossSectional;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingXBot.Tests.Trading;

/// <summary>
/// Real-Money-Safety-Tests fuer den Drift-Refill des <see cref="CrossSectionalTradingService"/>:
/// Extern (manuell) geschlossene Korb-Positionen werden erkannt und die freien Slots mit einem
/// frischen Momentum-Ranking aufgefuellt — mit Zwei-Tick-Bestaetigung gegen API-Glitches,
/// Wiedereroeffnungs-Sperre fuer geschlossene Symbole und Schutz von Fremd-Positionen.
/// </summary>
public sealed class CrossSectionalDriftRefillTests : IDisposable
{
    private readonly string _stateFile = Path.Combine(Path.GetTempPath(), $"xsec-drift-test-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_stateFile); } catch { /* Temp-Cleanup ist Best-Effort. */ }
    }

    private static CrossSectionalSettings Cfg() => new()
    {
        LongK = 1,
        ShortK = 1,
        LookbackCandles = 10,
        RiskAdjusted = false,        // ROC pur — deterministisch fuer die Momentum-Erwartung
        UniverseTopN = 10,
        IncludeTradFi = true,
        LeverageCap = 1,
        MarginUtilization = 0.75m,
        RebalanceDays = 21,
    };

    /// <summary>Kerzen mit konstantem prozentualen Schritt pro Kerze (positiv = Long-Momentum).</summary>
    private static List<Candle> Trend(decimal start, decimal stepPercent, int count = 60)
    {
        var candles = new List<Candle>(count);
        var price = start;
        var t = DateTime.UtcNow - TimeSpan.FromHours(4 * count);
        for (var i = 0; i < count; i++)
        {
            var close = price * (1 + stepPercent / 100m);
            candles.Add(new Candle(t, price, Math.Max(price, close), Math.Min(price, close), close, 1000m, t + TimeSpan.FromHours(4)));
            price = close;
            t += TimeSpan.FromHours(4);
        }
        return candles;
    }

    private sealed class FakeMarketData : IPublicMarketDataClient
    {
        public Dictionary<string, List<Candle>> Klines { get; } = new();
        public int TickerCalls { get; private set; }

        public Task<List<Ticker>> GetAllTickersAsync(CancellationToken ct = default)
        {
            TickerCalls++;
            var tickers = Klines
                .Select(kv => new Ticker(kv.Key, kv.Value[^1].Close, 0m, 0m, 1_000_000m, 0m, DateTime.UtcNow))
                .ToList();
            return Task.FromResult(tickers);
        }

        public Task<List<Candle>> GetKlinesAsync(string symbol, TimeFrame tf, DateTime from, DateTime to, CancellationToken ct = default)
            => Task.FromResult(Klines.TryGetValue(symbol, out var c) ? c : new List<Candle>());

        public Task<List<string>> GetAllSymbolsAsync(CancellationToken ct = default)
            => Task.FromResult(Klines.Keys.ToList());

        public Task<DateTime> GetServerTimeAsync(CancellationToken ct = default)
            => Task.FromResult(DateTime.UtcNow);
    }

    private async Task<CrossSectionalTradingService> CreateServiceAsync(
        FakeExchangeClient exchange, FakeMarketData marketData, Dictionary<string, Side> basket)
    {
        // Korb via persistierten State setzen (derselbe Pfad wie Live-Crash-Recovery).
        var state = new { LastRebalanceUtc = DateTime.UtcNow - TimeSpan.FromDays(1), Basket = basket };
        await File.WriteAllTextAsync(_stateFile, JsonSerializer.Serialize(state));

        var service = new CrossSectionalTradingService(
            exchange, marketData, new RiskSettings { MaxOpenPositions = 10 }, Cfg(),
            new BotEventBus(), NullLogger.Instance, _stateFile);
        await service.RestoreOrAdoptStateAsync();
        return service;
    }

    [Fact]
    public async Task Drift_ErsterTick_BestaetigtNurUndHandeltNicht()
    {
        // Korb: AAA long + BBB short. AAA wurde extern geschlossen (keine Position mehr).
        var ex = new FakeExchangeClient().WithPosition("BBB-USDT", Side.Sell, 1m, 100m);
        var md = new FakeMarketData
        {
            Klines = { ["AAA-USDT"] = Trend(100m, 1m), ["BBB-USDT"] = Trend(100m, -1m), ["CCC-USDT"] = Trend(50m, 2m) },
        };
        var svc = await CreateServiceAsync(ex, md, new() { ["AAA-USDT"] = Side.Buy, ["BBB-USDT"] = Side.Sell });

        await svc.RefillBasketDriftAsync(CancellationToken.None);

        // Zwei-Tick-Bestaetigung: erster Tick handelt NICHT (Schutz vor transientem API-Glitch).
        ex.PlaceOrderCalls.Should().BeEmpty();
        svc.CurrentBasket.Should().ContainKey("AAA-USDT");
    }

    [Fact]
    public async Task Drift_ZweiterTick_FuelltSlotMitNaechstbestemMomentum_GesperrtesSymbolBleibtZu()
    {
        var ex = new FakeExchangeClient().WithPosition("BBB-USDT", Side.Sell, 1m, 100m);
        var md = new FakeMarketData
        {
            Klines = { ["AAA-USDT"] = Trend(100m, 1m), ["BBB-USDT"] = Trend(100m, -1m), ["CCC-USDT"] = Trend(50m, 2m) },
        };
        var svc = await CreateServiceAsync(ex, md, new() { ["AAA-USDT"] = Side.Buy, ["BBB-USDT"] = Side.Sell });

        await svc.RefillBasketDriftAsync(CancellationToken.None);   // Tick 1: Drift vormerken
        await svc.RefillBasketDriftAsync(CancellationToken.None);   // Tick 2: bestaetigt → Refill

        // CCC (staerkstes positives Momentum unter den Kandidaten) fuellt den Long-Slot;
        // AAA ist bis zum naechsten Rebalance gesperrt und wird NICHT wiedereroeffnet.
        ex.PlaceOrderCalls.Select(p => (p.Symbol, p.Side)).Should().Contain(("CCC-USDT", Side.Buy));
        ex.PlaceOrderCalls.Select(p => p.Symbol).Should().NotContain("AAA-USDT");
        svc.CurrentBasket.Should().ContainKey("CCC-USDT").WhoseValue.Should().Be(Side.Buy);
        svc.CurrentBasket.Should().NotContainKey("AAA-USDT");
        svc.CurrentBasket.Should().ContainKey("BBB-USDT");          // gehaltene Position unangetastet
        ex.ClosePositionCalls.Should().BeEmpty();                   // kein Close von irgendwas
    }

    [Fact]
    public async Task Drift_FremdPosition_WirdWederGeschlossenNochInDenKorbUebernommen()
    {
        // User hat manuell XYZ long eroeffnet — der Refill darf sie weder schliessen noch adoptieren.
        var ex = new FakeExchangeClient()
            .WithPosition("BBB-USDT", Side.Sell, 1m, 100m)
            .WithPosition("XYZ-USDT", Side.Buy, 1m, 10m);
        var md = new FakeMarketData
        {
            Klines = { ["AAA-USDT"] = Trend(100m, 1m), ["BBB-USDT"] = Trend(100m, -1m), ["CCC-USDT"] = Trend(50m, 2m) },
        };
        var svc = await CreateServiceAsync(ex, md, new() { ["AAA-USDT"] = Side.Buy, ["BBB-USDT"] = Side.Sell });

        await svc.RefillBasketDriftAsync(CancellationToken.None);
        await svc.RefillBasketDriftAsync(CancellationToken.None);

        ex.ClosePositionCalls.Should().BeEmpty();
        svc.CurrentBasket.Should().NotContainKey("XYZ-USDT");
    }

    [Fact]
    public async Task KeinDrift_KeineUniversumsAbfrage()
    {
        // Alle Korb-Positionen offen → kein Drift → keine teuren Ticker-/Kline-Calls.
        var ex = new FakeExchangeClient()
            .WithPosition("AAA-USDT", Side.Buy, 1m, 100m)
            .WithPosition("BBB-USDT", Side.Sell, 1m, 100m);
        var md = new FakeMarketData
        {
            Klines = { ["AAA-USDT"] = Trend(100m, 1m), ["BBB-USDT"] = Trend(100m, -1m) },
        };
        var svc = await CreateServiceAsync(ex, md, new() { ["AAA-USDT"] = Side.Buy, ["BBB-USDT"] = Side.Sell });

        await svc.RefillBasketDriftAsync(CancellationToken.None);

        md.TickerCalls.Should().Be(0);
        ex.PlaceOrderCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task TransienterGlitch_PositionenWiederDa_KeineAktion()
    {
        // Tick 1: Exchange liefert (glitch-bedingt) keine Positionen. Tick 2: alles wieder da.
        var ex = new FakeExchangeClient()
            .WithPosition("AAA-USDT", Side.Buy, 1m, 100m)
            .WithPosition("BBB-USDT", Side.Sell, 1m, 100m);
        var md = new FakeMarketData
        {
            Klines = { ["AAA-USDT"] = Trend(100m, 1m), ["BBB-USDT"] = Trend(100m, -1m), ["CCC-USDT"] = Trend(50m, 2m) },
        };
        var svc = await CreateServiceAsync(ex, md, new() { ["AAA-USDT"] = Side.Buy, ["BBB-USDT"] = Side.Sell });

        var backup = (await ex.GetPositionsAsync()).ToList();
        ex.ClearPositions();
        await svc.RefillBasketDriftAsync(CancellationToken.None);   // Glitch-Tick: nur vormerken
        foreach (var p in backup) ex.WithPosition(p.Symbol, p.Side, p.Quantity, p.EntryPrice);
        await svc.RefillBasketDriftAsync(CancellationToken.None);   // Erholung: Vormerkung verfaellt

        ex.PlaceOrderCalls.Should().BeEmpty();
        svc.CurrentBasket.Should().HaveCount(2);
        svc.CurrentBasket.Should().ContainKeys("AAA-USDT", "BBB-USDT");
    }

    [Fact]
    public async Task Drift_SperrlisteUeberlebtRestart()
    {
        var ex = new FakeExchangeClient().WithPosition("BBB-USDT", Side.Sell, 1m, 100m);
        var md = new FakeMarketData
        {
            Klines = { ["AAA-USDT"] = Trend(100m, 1m), ["BBB-USDT"] = Trend(100m, -1m), ["CCC-USDT"] = Trend(50m, 2m) },
        };
        var svc = await CreateServiceAsync(ex, md, new() { ["AAA-USDT"] = Side.Buy, ["BBB-USDT"] = Side.Sell });
        await svc.RefillBasketDriftAsync(CancellationToken.None);
        await svc.RefillBasketDriftAsync(CancellationToken.None);
        svc.CurrentBasket.Should().ContainKey("CCC-USDT");

        // Neuer Service-Prozess (Crash/Reboot) laedt denselben State: AAA muss gesperrt bleiben,
        // sonst wuerde der naechste Drift-Refill das manuell geschlossene Symbol wiedereroeffnen.
        var ex2 = new FakeExchangeClient().WithPosition("BBB-USDT", Side.Sell, 1m, 100m);
        // CCC fehlt nach dem Neustart (z.B. ebenfalls manuell geschlossen) → Refill noetig.
        var svc2 = new CrossSectionalTradingService(
            ex2, md, new RiskSettings { MaxOpenPositions = 10 }, Cfg(),
            new BotEventBus(), NullLogger.Instance, _stateFile);
        await svc2.RestoreOrAdoptStateAsync();
        await svc2.RefillBasketDriftAsync(CancellationToken.None);
        await svc2.RefillBasketDriftAsync(CancellationToken.None);

        ex2.PlaceOrderCalls.Select(p => p.Symbol).Should().NotContain("AAA-USDT");
    }
}
