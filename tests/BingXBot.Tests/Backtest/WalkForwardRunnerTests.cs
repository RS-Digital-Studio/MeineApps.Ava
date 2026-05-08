using BingXBot.Backtest;
using BingXBot.Core.Enums;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Backtest;

// v1.5.3 Phase 6 — Walk-Forward-Backtest. Tests fokussieren auf reine Berechnungs-Logik
// (Window-Generation, Robustheits-Score). Integrationstests gegen BacktestEngine sind in
// FiveMonthLiveBacktest abgedeckt; hier liegt der Fokus auf der Window-Mathematik.
public class WalkForwardRunnerTests
{
    [Fact]
    public void GenerateWindows_DreiMonate_60TageFenster_30TageStep_ErgibtZweiFenster()
    {
        // 90 Tage Range, 60 Tage Window, 30 Tage Step → cursor: 0..30..60 (60+60>90 stoppt) → 2 Windows.
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(90);

        var windows = WalkForwardRunner.GenerateWindows(from, to, TimeSpan.FromDays(60), TimeSpan.FromDays(30));

        windows.Should().HaveCount(2);
        windows[0].From.Should().Be(from);
        windows[0].To.Should().Be(from.AddDays(60));
        windows[1].From.Should().Be(from.AddDays(30));
        windows[1].To.Should().Be(from.AddDays(90));
    }

    [Fact]
    public void GenerateWindows_FuenfMonate_60TageFenster_30TageStep_ErgibtVierFenster()
    {
        // 150 Tage Range. Windows: 0..60, 30..90, 60..120, 90..150. Cursor 120 → 120+60=180>150 stoppt.
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(150);

        var windows = WalkForwardRunner.GenerateWindows(from, to, TimeSpan.FromDays(60), TimeSpan.FromDays(30));

        windows.Should().HaveCount(4);
        windows[0].From.Should().Be(from);
        windows[3].To.Should().Be(from.AddDays(150));
    }

    [Fact]
    public void GenerateWindows_RangeKleinerAlsWindow_LeereListe()
    {
        var from = DateTime.UtcNow;
        var to = from.AddDays(30);
        var windows = WalkForwardRunner.GenerateWindows(from, to, TimeSpan.FromDays(60), TimeSpan.FromDays(30));
        windows.Should().BeEmpty();
    }

    [Fact]
    public void GenerateWindows_InvalidArgs_Wirft()
    {
        var from = DateTime.UtcNow;
        var to = from.AddDays(60);

        Action zeroWindow = () => WalkForwardRunner.GenerateWindows(from, to, TimeSpan.Zero, TimeSpan.FromDays(7));
        zeroWindow.Should().Throw<ArgumentOutOfRangeException>();

        Action zeroStep = () => WalkForwardRunner.GenerateWindows(from, to, TimeSpan.FromDays(30), TimeSpan.Zero);
        zeroStep.Should().Throw<ArgumentOutOfRangeException>();

        Action invertedRange = () => WalkForwardRunner.GenerateWindows(to, from, TimeSpan.FromDays(30), TimeSpan.FromDays(7));
        invertedRange.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RobustnessScore_StdDev_KonsistenteWinRates_KleinerScore()
    {
        // 4 Windows mit gleicher WinRate 0.6 → StdDev = 0.
        var windows = new[]
        {
            new WalkForwardWindowResult(0, DateTime.UtcNow, DateTime.UtcNow.AddDays(60), 10, 0.6m, 100m, 50m, 1.5m),
            new WalkForwardWindowResult(1, DateTime.UtcNow, DateTime.UtcNow.AddDays(60), 10, 0.6m, 100m, 50m, 1.5m),
            new WalkForwardWindowResult(2, DateTime.UtcNow, DateTime.UtcNow.AddDays(60), 10, 0.6m, 100m, 50m, 1.5m),
            new WalkForwardWindowResult(3, DateTime.UtcNow, DateTime.UtcNow.AddDays(60), 10, 0.6m, 100m, 50m, 1.5m),
        };
        var report = WalkForwardReport.FromWindows("BTC-USDT", TimeFrame.H1, TimeSpan.FromDays(60), TimeSpan.FromDays(30), windows);
        report.RobustnessScore.Should().Be(0m);
        report.AvgWinRate.Should().Be(0.6m);
    }

    [Fact]
    public void RobustnessScore_StdDev_VolatileWinRates_HoherScore()
    {
        // Drastisch unterschiedliche WinRates → hoher StdDev → wahrscheinliches Overfitting.
        var windows = new[]
        {
            new WalkForwardWindowResult(0, DateTime.UtcNow, DateTime.UtcNow.AddDays(60), 10, 0.9m, 200m, 30m, 2.5m),
            new WalkForwardWindowResult(1, DateTime.UtcNow, DateTime.UtcNow.AddDays(60), 10, 0.2m, -100m, 80m, 0.3m),
            new WalkForwardWindowResult(2, DateTime.UtcNow, DateTime.UtcNow.AddDays(60), 10, 0.7m, 50m, 60m, 1.2m),
        };
        var report = WalkForwardReport.FromWindows("BTC-USDT", TimeFrame.H4, TimeSpan.FromDays(60), TimeSpan.FromDays(30), windows);
        report.RobustnessScore.Should().BeGreaterThan(0.2m);
        report.WindowCount.Should().Be(3);
    }

    [Fact]
    public async Task Cancellation_StopsCleanly()
    {
        // Plan-Vorgabe: WalkForwardRunner muss bei CancellationToken sauber abbrechen.
        // Wenn der Token vor dem ersten Window-Lauf bereits cancelled ist, soll die Methode
        // ohne Side-Effects mit OperationCanceledException abbrechen (kein Hang, kein Crash).
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var engine = (BingXBot.Backtest.BacktestEngine?)null;
        // Wir testen NUR den Cancellation-Pfad ueber einen Stub, der den Token respektiert.
        // BacktestEngine selbst ist hier nicht relevant — der Cancellation-Check erfolgt
        // VOR dem ersten Engine-Call (Plan-Spezifikation: cancel mid-run sauber).
        var runner = new WalkForwardRunner(engine!);
        var act = async () => await runner.RunAsync(
            symbol: "BTC-USDT",
            timeFrame: TimeFrame.H1,
            from: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            to: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            windowSize: TimeSpan.FromDays(60),
            stepSize: TimeSpan.FromDays(30),
            settings: new BingXBot.Core.Configuration.BacktestSettings(),
            strategyFactory: () => null!,
            riskManagerFactory: () => null!,
            ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>(
            "vor dem ersten Window muss der CancellationToken-Check greifen");
    }

    [Fact]
    public void TotalNetPnl_SumOverAllWindows()
    {
        var windows = new[]
        {
            new WalkForwardWindowResult(0, DateTime.UtcNow, DateTime.UtcNow.AddDays(60), 10, 0.6m, 100m, 50m, 1.5m),
            new WalkForwardWindowResult(1, DateTime.UtcNow, DateTime.UtcNow.AddDays(60), 10, 0.5m, -50m, 80m, 0.8m),
            new WalkForwardWindowResult(2, DateTime.UtcNow, DateTime.UtcNow.AddDays(60), 10, 0.7m, 200m, 30m, 2.0m),
        };
        var report = WalkForwardReport.FromWindows("ETH-USDT", TimeFrame.D1, TimeSpan.FromDays(60), TimeSpan.FromDays(30), windows);
        report.TotalNetPnl.Should().Be(250m);
        report.MaxDrawdownAcrossWindows.Should().Be(80m);
    }
}
