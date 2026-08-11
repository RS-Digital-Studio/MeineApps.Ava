using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Trading;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BingXBot.Tests.Trading;

// Audit 11.08.2026 — Glitch-Guard: GetPositionsAsync liefert bei BingX-Schema-Drift/data:null
// eine LEERE Liste statt einer Exception. Der TickerLoop-Orphan-Cleanup wertete das als "alle
// Positionen geschlossen" und cancelte die nativen SL/TP ALLER realen Positionen + entsorgte
// die Signale. Der Guard verlangt zwei leere Snapshots in Folge (analog Xsec-DriftConfirmTicks).
public class EmptyPositionsGlitchGuardTests
{
    private static TestableLiveServiceForTpRace CreateService(IExchangeClient exchange)
    {
        return new TestableLiveServiceForTpRace(
            restClient: exchange,
            publicClient: Substitute.For<IPublicMarketDataClient>(),
            strategyManager: new StrategyManager(),
            riskSettings: new RiskSettings(),
            scannerSettings: new ScannerSettings(),
            eventBus: new BotEventBus(),
            botSettings: new BotSettings());
    }

    private static TestableLiveServiceForTpRace ServiceWithTrackedSignal(FakeExchangeClient fake, string key = "BTC-USDT_Buy")
    {
        var service = CreateService(fake);
        var signal = new SignalResult(
            Signal.Long, 0.8m, 50000m, StopLoss: 49000m,
            TakeProfit: 52000m, Reason: "Test", TakeProfit2: 54000m);
        service._positionSignals[key] = signal;
        service.SetExitStateForTest(key, new PositionExitState
        {
            Signal = signal, Symbol = "BTC-USDT", Side = Side.Buy,
            EntryPrice = 50000m, OriginalQuantity = 1m, Tp2 = 54000m,
        });
        // Orphan-Grace (30 s) bewusst abgelaufen — nur der neue Glitch-Guard darf schuetzen.
        service._signalCreatedAt[key] = DateTime.UtcNow.AddMinutes(-5);
        return service;
    }

    [Fact]
    public async Task LeereAntwort_ErsterTick_SchuetztSignaleUndNativeOrders()
    {
        var fake = new FakeExchangeClient();   // leerer Positions-Snapshot (Glitch)
        var service = ServiceWithTrackedSignal(fake);

        await service.PublicOnBeforePriceTickerIteration(new List<Position>());

        service._positionSignals.Should().ContainKey("BTC-USDT_Buy", "ein einzelner leerer Snapshot ist kein Beweis");
        fake.CancelOrderCalls.Should().BeEmpty("native SL/TP duerfen nicht gecancelt werden");
    }

    [Fact]
    public async Task LeereAntwort_ZweiterTickInFolge_RaeumtBestaetigteOrphansAuf()
    {
        var fake = new FakeExchangeClient();
        var service = ServiceWithTrackedSignal(fake);

        await service.PublicOnBeforePriceTickerIteration(new List<Position>());   // Vormerkung
        await service.PublicOnBeforePriceTickerIteration(new List<Position>());   // Bestaetigung

        service._positionSignals.Should().NotContainKey("BTC-USDT_Buy",
            "zwei leere Snapshots in Folge = Position ist real geschlossen");
    }

    [Fact]
    public async Task LeereAntwort_ErholungDazwischen_ResettetDenZaehler()
    {
        var fake = new FakeExchangeClient();
        var service = ServiceWithTrackedSignal(fake);
        var realPosition = new Position("BTC-USDT", Side.Buy, 50000m, 50000m, 1m, 0m, 3,
            MarginType.Isolated, DateTime.UtcNow);

        await service.PublicOnBeforePriceTickerIteration(new List<Position>());              // Glitch-Tick
        await service.PublicOnBeforePriceTickerIteration(new List<Position> { realPosition }); // Erholung
        await service.PublicOnBeforePriceTickerIteration(new List<Position>());              // neuer Glitch

        service._positionSignals.Should().ContainKey("BTC-USDT_Buy",
            "der Zaehler muss bei Erholung zuruecksetzen — alternierende Glitches sind kein Beweis");
    }
}
