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

// v1.4.0 .2/0.3 (Findings 0.2/0.3) — Doppel-Close-Race verhindern.
//
// Bot-platzierte TP1/TP2 als Reduce-Only-LIMIT auf BingX. Sobald die OrderIds im
// PositionExitState gesetzt sind, skippt der PriceTickerLoop den TP-Hit-Check (BingX fuellt
// nativ + WebSocket-Fill-Event triggert Phase-Transition). Ohne diesen Skip wuerde der Bot
// parallel zum BingX-Limit-Fill ein ClosePartialAsync mit pos.Quantity*0.5 absetzen — bei
// Limit-Partial-Fill ist pos.Quantity bereits reduziert → falsche Mengen.
public class Tp1LimitOnExchangeRaceTests
{
    [Fact]
    public void IsTpManagedByExchange_PhaseInitial_TrueWhenTp1OrderIdSet()
    {
        var es = new PositionExitState
        {
            Phase = ExitPhase.Initial,
            Tp1LimitOrderId = "binance-tp1-12345",
        };
        es.IsTpManagedByExchange.Should().BeTrue();
    }

    [Fact]
    public void IsTpManagedByExchange_PhaseInitial_FalseWhenTp1OrderIdNull()
    {
        var es = new PositionExitState { Phase = ExitPhase.Initial };
        es.IsTpManagedByExchange.Should().BeFalse();
    }

    [Fact]
    public void IsTpManagedByExchange_PhaseTp1Hit_TrueWhenTp2OrderIdSet()
    {
        var es = new PositionExitState
        {
            Phase = ExitPhase.Tp1Hit,
            Tp1LimitOrderId = null, // ist nach TP1-Fill geleert
            Tp2LimitOrderId = "binance-tp2-67890",
        };
        es.IsTpManagedByExchange.Should().BeTrue();
    }

    [Fact]
    public void IsTpManagedByExchange_PhaseTp1Hit_FalseWhenTp2OrderIdNull()
    {
        var es = new PositionExitState
        {
            Phase = ExitPhase.Tp1Hit,
            Tp1LimitOrderId = null,
            Tp2LimitOrderId = null, // Bot-Fallback aktiv
        };
        es.IsTpManagedByExchange.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessTpLimitFill_Tp1_TriggersPhaseTransition()
    {
        // Setup: Long-Position auf BTC mit TP1-LIMIT auf BingX. Position-Restmenge nach Fill = 0.5
        // (Original 1.0, Tp1CloseRatio 0.5).
        var fake = new FakeExchangeClient()
            .WithPosition("BTC-USDT", Side.Buy, qty: 0.5m, entry: 50000m);
        var service = CreateService(fake);

        var signal = new SignalResult(
            Signal.Long, 0.8m, 50000m, StopLoss: 49000m,
            TakeProfit: 52000m, Reason: "Test", TakeProfit2: 54000m);
        service._positionSignals["BTC-USDT_Buy"] = signal;
        service.SetExitStateForTest("BTC-USDT_Buy", new PositionExitState
        {
            Signal = signal,
            Phase = ExitPhase.Initial,
            Symbol = "BTC-USDT",
            Side = Side.Buy,
            EntryPrice = 50000m,
            OriginalQuantity = 1m,
            Tp2 = 54000m,
            Tp1LimitOrderId = "tp1-id",
            Tp2LimitOrderId = "tp2-id",
            EntryTime = DateTime.UtcNow.AddMinutes(-30),
        });

        // Act: TP1-Fill von WebSocket.
        await service.PublicProcessTpLimitFillAsync("BTC-USDT", "tp1-id");

        // Assert: Phase ist auf Tp1Hit, Tp1LimitOrderId genullt, Signal hat TP=Tp2.
        service.GetExitStateForTest("BTC-USDT_Buy")!.Phase.Should().Be(ExitPhase.Tp1Hit);
        service.GetExitStateForTest("BTC-USDT_Buy")!.Tp1LimitOrderId.Should().BeNull();
        service.GetExitStateForTest("BTC-USDT_Buy")!.PartialClosed.Should().BeTrue();
        service._positionSignals["BTC-USDT_Buy"].TakeProfit.Should().Be(54000m);
    }

    [Fact]
    public async Task ProcessTpLimitFill_UnknownOrderId_ReturnsFalse()
    {
        var fake = new FakeExchangeClient();
        var service = CreateService(fake);

        var processed = await service.PublicProcessTpLimitFillAsync("BTC-USDT", "irgendeine-fremde-id");

        processed.Should().BeFalse();
    }

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
}

internal sealed class TestableLiveServiceForTpRace : LiveTradingService
{
    public TestableLiveServiceForTpRace(
        IExchangeClient restClient,
        IPublicMarketDataClient publicClient,
        StrategyManager strategyManager,
        RiskSettings riskSettings,
        ScannerSettings scannerSettings,
        BotEventBus eventBus,
        BotSettings botSettings)
        : base(restClient, publicClient, strategyManager, riskSettings, scannerSettings, eventBus, botSettings)
    {
    }

    public void SetExitStateForTest(string key, PositionExitState state) =>
        _exitStates[key] = state;

    public PositionExitState? GetExitStateForTest(string key) =>
        _exitStates.TryGetValue(key, out var v) ? v : null;

    public Task<bool> PublicProcessTpLimitFillAsync(string symbol, string orderId) =>
        ProcessTpLimitFillAsync(symbol, orderId);
}
