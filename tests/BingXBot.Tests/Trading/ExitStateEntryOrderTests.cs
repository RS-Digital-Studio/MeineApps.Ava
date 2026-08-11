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

// Audit 11.08.2026 — Entry-Reihenfolge-Bug: Der ExitState wurde erst NACH
// PlaceOrderOnExchangeAsync angelegt, waehrend PlaceTpLimitOrdersAfterFillAsync (laeuft
// INNERHALB des Order-Calls) die TP-Limit-OrderIds in den ExitState schreiben will. Die IDs
// blieben dadurch bei JEDEM frischen Entry null → IsTpManagedByExchange=false → der
// Bot-5s-TP-Check lief parallel zu den BingX-Limits (Doppel-Close bei TP1, TP2-Bein
// amputiert). Der Fix registriert Signal + ExitState VOR dem Order-Call.
public class ExitStateEntryOrderTests
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

    private static SignalResult LongSignal() => new(
        Signal.Long, 0.8m, 50000m, StopLoss: 49000m,
        TakeProfit: 52000m, Reason: "Test", TakeProfit2: 54000m,
        DisableSmartBreakeven: true);

    [Fact]
    public async Task Entry_MitVorabRegistriertemExitState_SpeichertTpOrderIds()
    {
        // Spiegelt den gefixten Entry-Pfad: Signal + ExitState werden VOR dem Order-Call
        // registriert — die TP-Limit-Platzierung im Order-Call muss die OrderIds dann im
        // ExitState hinterlegen (IsTpManagedByExchange wird true).
        var fake = new FakeExchangeClient()
            .WithPosition("BTC-USDT", Side.Buy, qty: 1m, entry: 50000m);   // Fill sofort sichtbar
        var service = CreateService(fake);
        var signal = LongSignal();
        var key = "BTC-USDT_Buy";
        service._positionSignals[key] = signal;
        service.SetExitStateForTest(key, new PositionExitState
        {
            Signal = signal, Symbol = "BTC-USDT", Side = Side.Buy,
            EntryPrice = 50000m, OriginalQuantity = 1m, Tp2 = 54000m,
        });

        var ticker = new Ticker("BTC-USDT", 50000m, 0m, 0m, 1_000_000m, 0m, DateTime.UtcNow);
        var placed = await service.PublicPlaceOrderOnExchangeAsync(ticker, Side.Buy, 1m, signal, leverage: 3);

        placed.Should().BeTrue();
        var es = service.GetExitStateForTest(key)!;
        es.Tp1LimitOrderId.Should().NotBeNullOrEmpty("TP1-Limit-OrderId muss im ExitState landen");
        es.Tp2LimitOrderId.Should().NotBeNullOrEmpty("TP2-Limit-OrderId muss im ExitState landen");
        es.IsTpManagedByExchange.Should().BeTrue("sonst laeuft der Bot-TP-Check parallel zu den BingX-Limits");
    }

    [Fact]
    public void RestorePositionSignal_FuelltTp2UndOriginalQuantity()
    {
        // Recovery/Adoption legte den ExitState frueher OHNE Tp2/OriginalQuantity an:
        // Tp2==null → WS-TP1-Fill macht Full-Close bei 1.5R statt 50/50-Split;
        // OriginalQuantity==0 → Native-Close-Fallback bucht Qty 0, Runner-Zweig tot.
        var service = CreateService(new FakeExchangeClient());
        var signal = LongSignal();

        service.RestorePositionSignal("BTC-USDT", Side.Buy, signal, positionQuantity: 1.5m);

        var es = service.GetExitStateForTest("BTC-USDT_Buy")!;
        es.Tp2.Should().Be(54000m);
        es.OriginalQuantity.Should().Be(1.5m);
        es.IsRecovered.Should().BeTrue();
    }

    [Fact]
    public void GetBarStart_AlignedAufKerzengrenzen()
    {
        // Anti-Churn-Dedup-Fenster: H4-Kerzen sind UTC-aligned (00/04/08/12/16/20).
        var t = new DateTime(2026, 8, 11, 14, 37, 12, DateTimeKind.Utc);
        TradingServiceBase.GetBarStart(TimeFrame.H4, t)
            .Should().Be(new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc));
        TradingServiceBase.GetBarStart(TimeFrame.H1, t)
            .Should().Be(new DateTime(2026, 8, 11, 14, 0, 0, DateTimeKind.Utc));
        TradingServiceBase.GetBarStart(TimeFrame.D1, t)
            .Should().Be(new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc));
        // Zwei Zeitpunkte in derselben H4-Kerze liefern denselben Dedup-Schluessel.
        TradingServiceBase.GetBarStart(TimeFrame.H4, t.AddHours(1))
            .Should().Be(TradingServiceBase.GetBarStart(TimeFrame.H4, t));
        // Der naechste Kerzen-Start oeffnet ein neues Fenster.
        TradingServiceBase.GetBarStart(TimeFrame.H4, t.AddHours(2))
            .Should().Be(new DateTime(2026, 8, 11, 16, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void RestorePositionSignal_ExistierenderExitState_ZiehtFehlendeFelderNach()
    {
        // Restart-Sequenz: DB-Restore legt einen ExitState ohne Tp2/OriginalQuantity an,
        // danach laeuft RestorePositionSignal — die fehlenden Felder muessen nachgezogen
        // werden, vorhandene Werte bleiben unangetastet.
        var service = CreateService(new FakeExchangeClient());
        var signal = LongSignal();
        service.SetExitStateForTest("BTC-USDT_Buy", new PositionExitState
        {
            Signal = signal, Symbol = "BTC-USDT", Side = Side.Buy, EntryPrice = 50000m,
        });

        service.RestorePositionSignal("BTC-USDT", Side.Buy, signal, positionQuantity: 2m);

        var es = service.GetExitStateForTest("BTC-USDT_Buy")!;
        es.Tp2.Should().Be(54000m);
        es.OriginalQuantity.Should().Be(2m);
    }
}
