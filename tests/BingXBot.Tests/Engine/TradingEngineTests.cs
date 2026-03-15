using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BingXBot.Tests.Engine;

public class TradingEngineTests
{
    private TradingEngine CreateEngine(
        IExchangeClient? exchangeClient = null,
        IDataFeed? dataFeed = null,
        IMarketScanner? scanner = null,
        IRiskManager? riskManager = null)
    {
        var sm = new StrategyManager();
        return new TradingEngine(
            exchangeClient ?? Substitute.For<IExchangeClient>(),
            dataFeed ?? Substitute.For<IDataFeed>(),
            scanner ?? Substitute.For<IMarketScanner>(),
            riskManager ?? Substitute.For<IRiskManager>(),
            sm,
            new RiskSettings(),
            new ScannerSettings(),
            NullLogger<TradingEngine>.Instance);
    }

    [Fact]
    public void InitialState_ShouldBeStopped()
    {
        var engine = CreateEngine();
        engine.State.Should().Be(BotState.Stopped);
    }

    [Fact]
    public async Task StartAsync_ShouldTransitionToRunning()
    {
        var scanner = Substitute.For<IMarketScanner>();
        scanner.ScanAsync(Arg.Any<ScannerSettings>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<ScanResult>());

        var engine = CreateEngine(scanner: scanner);
        await engine.StartAsync(TradingMode.Paper);
        engine.State.Should().Be(BotState.Running);
        engine.Mode.Should().Be(TradingMode.Paper);

        await engine.StopAsync(); // Aufräumen
    }

    [Fact]
    public async Task StopAsync_ShouldTransitionToStopped()
    {
        var scanner = Substitute.For<IMarketScanner>();
        scanner.ScanAsync(Arg.Any<ScannerSettings>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<ScanResult>());

        var engine = CreateEngine(scanner: scanner);
        await engine.StartAsync(TradingMode.Paper);
        await engine.StopAsync();
        engine.State.Should().Be(BotState.Stopped);
    }

    [Fact]
    public async Task PauseAsync_ShouldTransitionToPaused()
    {
        var scanner = Substitute.For<IMarketScanner>();
        scanner.ScanAsync(Arg.Any<ScannerSettings>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<ScanResult>());

        var engine = CreateEngine(scanner: scanner);
        await engine.StartAsync(TradingMode.Paper);
        await engine.PauseAsync();
        engine.State.Should().Be(BotState.Paused);

        await engine.StopAsync();
    }

    [Fact]
    public async Task ResumeAsync_ShouldTransitionToRunning()
    {
        var scanner = Substitute.For<IMarketScanner>();
        scanner.ScanAsync(Arg.Any<ScannerSettings>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<ScanResult>());

        var engine = CreateEngine(scanner: scanner);
        await engine.StartAsync(TradingMode.Paper);
        await engine.PauseAsync();
        await engine.ResumeAsync();
        engine.State.Should().Be(BotState.Running);

        await engine.StopAsync();
    }

    [Fact]
    public async Task EmergencyStop_ShouldCloseAllPositions()
    {
        var exchangeClient = Substitute.For<IExchangeClient>();
        var scanner = Substitute.For<IMarketScanner>();
        scanner.ScanAsync(Arg.Any<ScannerSettings>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<ScanResult>());

        var engine = CreateEngine(exchangeClient: exchangeClient, scanner: scanner);
        await engine.StartAsync(TradingMode.Paper);
        await engine.EmergencyStopAsync();

        await exchangeClient.Received(1).CloseAllPositionsAsync();
        engine.State.Should().Be(BotState.EmergencyStop);
    }

    [Fact]
    public async Task StateChanged_ShouldFireOnTransition()
    {
        var states = new List<BotState>();
        var scanner = Substitute.For<IMarketScanner>();
        scanner.ScanAsync(Arg.Any<ScannerSettings>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<ScanResult>());

        var engine = CreateEngine(scanner: scanner);
        engine.StateChanged += (_, state) => states.Add(state);

        await engine.StartAsync(TradingMode.Paper);
        await engine.StopAsync();

        states.Should().Contain(BotState.Starting);
        states.Should().Contain(BotState.Running);
        states.Should().Contain(BotState.Stopped);
    }

    [Fact]
    public async Task StartAsync_WhenRunning_ShouldThrow()
    {
        var scanner = Substitute.For<IMarketScanner>();
        scanner.ScanAsync(Arg.Any<ScannerSettings>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<ScanResult>());

        var engine = CreateEngine(scanner: scanner);
        await engine.StartAsync(TradingMode.Paper);

        var act = () => engine.StartAsync(TradingMode.Paper);
        await act.Should().ThrowAsync<InvalidOperationException>();

        await engine.StopAsync();
    }
}
