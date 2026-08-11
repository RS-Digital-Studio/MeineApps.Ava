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

    private static CrossSectionalSettings Cfg(int longK = 1, int shortK = 1) => new()
    {
        LongK = longK,
        ShortK = shortK,
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
        FakeExchangeClient exchange, FakeMarketData marketData, Dictionary<string, Side> basket,
        CrossSectionalSettings? cfg = null, Dictionary<string, Side>? pendingCloses = null)
    {
        // Korb via persistierten State setzen (derselbe Pfad wie Live-Crash-Recovery).
        var state = new
        {
            LastRebalanceUtc = DateTime.UtcNow - TimeSpan.FromDays(1),
            Basket = basket,
            PendingCloses = pendingCloses,
        };
        await File.WriteAllTextAsync(_stateFile, JsonSerializer.Serialize(state));

        var service = new CrossSectionalTradingService(
            exchange, marketData, new RiskSettings { MaxOpenPositions = 10 }, cfg ?? Cfg(),
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

    [Fact]
    public async Task Drift_AlternierenderGlitch_WirdNichtFaelschlichAlsClosedGewertet()
    {
        // AAA flackert (fehlt Tick1, da Tick2, fehlt Tick3): der Zaehler resettet bei Anwesenheit,
        // erreicht also nie DriftConfirmTicks → kein faelschlicher Refill. BBB bleibt durchgehend offen.
        var md = new FakeMarketData
        {
            Klines = { ["AAA-USDT"] = Trend(100m, 1m), ["BBB-USDT"] = Trend(100m, -1m), ["CCC-USDT"] = Trend(50m, 2m) },
        };
        var fx = new FakeExchangeClient().WithPosition("BBB-USDT", Side.Sell, 1m, 100m);
        var svc = await CreateServiceAsync(fx, md, new() { ["AAA-USDT"] = Side.Buy, ["BBB-USDT"] = Side.Sell });

        // Tick1: AAA fehlt (nur BBB offen) → Zaehler AAA=1
        await svc.RefillBasketDriftAsync(CancellationToken.None);
        // Tick2: AAA wieder da → Zaehler AAA=0
        fx.WithPosition("AAA-USDT", Side.Buy, 1m, 100m);
        await svc.RefillBasketDriftAsync(CancellationToken.None);
        // Tick3: AAA fehlt erneut → Zaehler AAA=1 (NICHT >= DriftConfirmTicks)
        fx.ClearPositions();
        fx.WithPosition("BBB-USDT", Side.Sell, 1m, 100m);
        await svc.RefillBasketDriftAsync(CancellationToken.None);

        fx.PlaceOrderCalls.Should().BeEmpty();
        svc.CurrentBasket.Should().ContainKey("AAA-USDT");
    }

    [Fact]
    public async Task Rebalancer_OnClosed_FeuertNurFuerVerifizierteCloses()
    {
        // AAA (im Ziel, bleibt) + BBB (raus → wird geschlossen) + CCC (FailClose simuliert? nein:
        // hier nur erfolgreicher Close von BBB → onClosed genau 1×).
        var ex = new FakeExchangeClient()
            .WithPosition("AAA-USDT", Side.Buy, 1m, 100m)
            .WithPosition("BBB-USDT", Side.Buy, 1m, 100m);
        var target = new Dictionary<string, Side> { ["AAA-USDT"] = Side.Buy };
        var prices = new Dictionary<string, decimal> { ["AAA-USDT"] = 100m, ["BBB-USDT"] = 100m };
        var cats = new[] { "AAA-USDT", "BBB-USDT" }.ToDictionary(s => s, _ => MarketCategory.Crypto);
        var closedSymbols = new List<string>();

        await CrossSectionalRebalancer.ReconcileAsync(
            ex, target, prices, cats,
            new CrossSectionalSettings { LongK = 1, ShortK = 1, LeverageCap = 1, MarginUtilization = 0.75m },
            new RiskSettings { MaxOpenPositions = 10 },
            onClosed: pos => closedSymbols.Add(pos.Symbol));

        closedSymbols.Should().ContainSingle().Which.Should().Be("BBB-USDT");
    }

    [Fact]
    public async Task Rebalancer_OnClosed_FeuertNICHTBeiFehlgeschlagenemClose()
    {
        // FailCloses=true → BBB bleibt offen → onClosed darf NICHT feuern (kein realer Close).
        var ex = new FakeExchangeClient { FailCloses = true }
            .WithPosition("BBB-USDT", Side.Buy, 1m, 100m);
        var target = new Dictionary<string, Side> { ["AAA-USDT"] = Side.Buy };
        var prices = new Dictionary<string, decimal> { ["AAA-USDT"] = 100m, ["BBB-USDT"] = 100m };
        var cats = new[] { "AAA-USDT", "BBB-USDT" }.ToDictionary(s => s, _ => MarketCategory.Crypto);
        var closedSymbols = new List<string>();

        // closeSettleDelay Zero: der Fail-Close bleibt in jedem Read sichtbar — ohne Zero
        // wuerde der Settle-Retry hier real 3×2 s warten.
        await CrossSectionalRebalancer.ReconcileAsync(
            ex, target, prices, cats,
            new CrossSectionalSettings { LongK = 1, ShortK = 1, LeverageCap = 1, MarginUtilization = 0.75m },
            new RiskSettings { MaxOpenPositions = 10 },
            onClosed: pos => closedSymbols.Add(pos.Symbol),
            closeSettleDelay: TimeSpan.Zero);

        closedSymbols.Should().BeEmpty();
    }

    [Fact]
    public async Task LeererKorb_RefillBautVolleSlotsAuf()
    {
        // Ein fehlgeschlagener Rebalance (Filled leer: Settle-Race, Insufficient Margin, Wochenende)
        // hinterlaesst einen leeren Korb. Vor dem Fix war das eine Sackgasse: der Drift-Refill stieg
        // bei leerem Korb sofort aus → Bot bis zu RebalanceDays (9 Tage) flat.
        var ex = new FakeExchangeClient();
        var md = new FakeMarketData
        {
            Klines = { ["AAA-USDT"] = Trend(100m, 1m), ["BBB-USDT"] = Trend(100m, -1m), ["CCC-USDT"] = Trend(50m, 2m) },
        };
        var svc = await CreateServiceAsync(ex, md, new());   // leerer Korb, Rebalance nicht faellig

        await svc.RefillBasketDriftAsync(CancellationToken.None);

        // LongK=1/ShortK=1: staerkstes positives Momentum (CCC) long, staerkstes negatives (BBB) short.
        ex.PlaceOrderCalls.Select(p => (p.Symbol, p.Side)).Should().Contain(("CCC-USDT", Side.Buy));
        ex.PlaceOrderCalls.Select(p => (p.Symbol, p.Side)).Should().Contain(("BBB-USDT", Side.Sell));
        svc.CurrentBasket.Should().HaveCount(2);
    }

    [Fact]
    public async Task Drift_UnbestaetigtFehlendesSymbol_WirdBeiFreiemSlotNichtWiedereroeffnet()
    {
        // Chronisch freier Long-Slot (LongK=2, Korb hat nur 1 Long) + AAA fehlt seit DIESEM Tick
        // (Miss-Zaehler 1, unbestaetigt). Vor dem Fix stand AAA im Reconcile-Ziel und wurde im
        // selben Tick wiedereroeffnet — die Zwei-Tick-Bestaetigung war wirkungslos, sobald
        // irgendein Slot frei war.
        var ex = new FakeExchangeClient().WithPosition("BBB-USDT", Side.Sell, 1m, 100m);
        var md = new FakeMarketData
        {
            Klines =
            {
                ["AAA-USDT"] = Trend(100m, 1m), ["BBB-USDT"] = Trend(100m, -1m),
                ["CCC-USDT"] = Trend(50m, 2m), ["DDD-USDT"] = Trend(80m, 0.5m),
            },
        };
        var svc = await CreateServiceAsync(
            ex, md, new() { ["AAA-USDT"] = Side.Buy, ["BBB-USDT"] = Side.Sell }, Cfg(longK: 2));

        await svc.RefillBasketDriftAsync(CancellationToken.None);   // Tick 1: AAA-Miss unbestaetigt

        // Der freie Long-Slot wird mit dem besten Kandidaten (CCC) gefuellt — AAA darf trotz
        // freiem Slot NICHT wiedereroeffnet werden (Zwei-Tick-Bestaetigung laeuft noch).
        ex.PlaceOrderCalls.Select(p => (p.Symbol, p.Side)).Should().Contain(("CCC-USDT", Side.Buy));
        ex.PlaceOrderCalls.Select(p => p.Symbol).Should().NotContain("AAA-USDT");
        ex.ClosePositionCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task FehlCloseRetry_SchliesstRestUndSchuetztIhnNichtAlsFremdPosition()
    {
        // XXX-Long ist ein haengengebliebener Close des letzten Rebalance (PendingCloses im State).
        // Der Drift-Tick muss ihn erneut schliessen — vor dem Fix galt er als Fremd-Position und
        // wurde bis zum naechsten Rebalance konserviert (Ueber-Exposure).
        var ex = new FakeExchangeClient()
            .WithPosition("BBB-USDT", Side.Sell, 1m, 100m)
            .WithPosition("XXX-USDT", Side.Buy, 1m, 50m);
        var md = new FakeMarketData
        {
            Klines = { ["BBB-USDT"] = Trend(100m, -1m), ["CCC-USDT"] = Trend(50m, 2m) },
        };
        var svc = await CreateServiceAsync(
            ex, md, new() { ["BBB-USDT"] = Side.Sell },
            pendingCloses: new() { ["XXX-USDT"] = Side.Buy });

        await svc.RefillBasketDriftAsync(CancellationToken.None);

        ex.ClosePositionCalls.Should().Contain(("XXX-USDT", Side.Buy));
        (await ex.GetPositionsAsync()).Should().NotContain(p => p.Symbol == "XXX-USDT");
        svc.CurrentBasket.Should().NotContainKey("XXX-USDT");
    }

    [Fact]
    public async Task FehlCloseRetry_BleibtBeiErneutemFehlschlagInDerListe()
    {
        // Der Close wirft weiterhin (z.B. Rate-Limit): das Symbol muss in der Retry-Liste bleiben
        // und im naechsten Tick erneut versucht werden.
        var ex = new FakeExchangeClient { CloseThrows = true }
            .WithPosition("BBB-USDT", Side.Sell, 1m, 100m)
            .WithPosition("XXX-USDT", Side.Buy, 1m, 50m);
        var md = new FakeMarketData
        {
            Klines = { ["BBB-USDT"] = Trend(100m, -1m) },
        };
        var svc = await CreateServiceAsync(
            ex, md, new() { ["BBB-USDT"] = Side.Sell },
            pendingCloses: new() { ["XXX-USDT"] = Side.Buy });

        await svc.RefillBasketDriftAsync(CancellationToken.None);
        await svc.RefillBasketDriftAsync(CancellationToken.None);

        // Beide Ticks haben den Close versucht (Retry-Liste hat ueberlebt).
        ex.ClosePositionCalls.Count(c => c == ("XXX-USDT", Side.Buy)).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task StopFuerAutoRestart_LiquidiertKorbNicht()
    {
        // Watchdog-Auto-Restart (Stop -> Start binnen Sekunden): der mehrtaegige Korb darf NICHT
        // liquidiert werden — vorher kostete jeder Restart den vollen Fee-Round-Trip und sperrte
        // die Korb-Symbole anschliessend als "extern geschlossen" (Korb aus Raengen 4-6).
        var ex = new FakeExchangeClient()
            .WithPosition("AAA-USDT", Side.Buy, 1m, 100m)
            .WithPosition("BBB-USDT", Side.Sell, 1m, 100m);
        var md = new FakeMarketData
        {
            Klines = { ["AAA-USDT"] = Trend(100m, 1m), ["BBB-USDT"] = Trend(100m, -1m) },
        };
        var svc = await CreateServiceAsync(ex, md, new() { ["AAA-USDT"] = Side.Buy, ["BBB-USDT"] = Side.Sell });

        await svc.StartAsync();
        await svc.StopAsync(closePositions: false);

        ex.CallLog.Should().NotContain("CloseAllPositionsAsync");
        (await ex.GetPositionsAsync()).Should().HaveCount(2, "der Korb muss den Restart ueberleben");
    }

    [Fact]
    public void WochenendGate_BlocktSamstagUndSonntag()
    {
        // Sa/So sind alle Nicht-Forex-TradFi-Maerkte zu — der Rebalance wird verschoben.
        CrossSectionalTradingService.IsTradFiWeekendBlackout(new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc))
            .Should().BeTrue("8.8.2026 ist ein Samstag");
        CrossSectionalTradingService.IsTradFiWeekendBlackout(new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc))
            .Should().BeTrue("9.8.2026 ist ein Sonntag");
        CrossSectionalTradingService.IsTradFiWeekendBlackout(new DateTime(2026, 8, 10, 0, 30, 0, DateTimeKind.Utc))
            .Should().BeFalse("10.8.2026 ist ein Montag");
    }

    [Fact]
    public async Task Drift_SeitenFlipDurchUser_SchliesstUserPositionNichtUndGibtKorbSeiteAuf()
    {
        // Korb: AAA long. Der User hat AAA long geschlossen und bewusst AAA SHORT eroeffnet.
        // Vor dem Fix stand AAA=Buy (alte Korb-Seite) im Reconcile-Ziel: der Rebalancer haette
        // die User-Short-Position geschlossen und AAA long wiedereroeffnet.
        var ex = new FakeExchangeClient()
            .WithPosition("AAA-USDT", Side.Sell, 1m, 100m)    // User-Flip
            .WithPosition("BBB-USDT", Side.Sell, 1m, 100m);   // Korb-Short unveraendert
        var md = new FakeMarketData
        {
            Klines = { ["AAA-USDT"] = Trend(100m, 1m), ["BBB-USDT"] = Trend(100m, -1m), ["CCC-USDT"] = Trend(50m, 2m) },
        };
        var svc = await CreateServiceAsync(ex, md, new() { ["AAA-USDT"] = Side.Buy, ["BBB-USDT"] = Side.Sell });

        await svc.RefillBasketDriftAsync(CancellationToken.None);

        ex.ClosePositionCalls.Should().BeEmpty();                                      // User-Short unangetastet
        ex.PlaceOrderCalls.Select(p => (p.Symbol, p.Side)).Should().NotContain(("AAA-USDT", Side.Buy));
        svc.CurrentBasket.Should().NotContainKey("AAA-USDT");                          // Korb-Seite aufgegeben
        ex.PlaceOrderCalls.Select(p => (p.Symbol, p.Side)).Should().Contain(("CCC-USDT", Side.Buy));  // Slot neu gefuellt
    }
}
