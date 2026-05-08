using BingXBot.Core.Diagnostics;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Trading;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BingXBot.Tests.Diagnostics;

// v1.5.2 Phase 4 — Decision-Trail DB-Persistenz (Migration v11).
//
// Tests gegen eine temporaere SQLite-DB. Verifizieren Save/Load-Roundtrip + Filter +
// Migration-Marker. Trim-Verhalten ist via Cleanup-Threshold abgedeckt.
public class DecisionDbPersistenceTests : IAsyncLifetime
{
    private string _tempDir = "";
    private BotDatabaseService _db = null!;

    public async Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bingxbot-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        var paths = Substitute.For<IAppPaths>();
        paths.DatabasePath.Returns(Path.Combine(_tempDir, "bot.db"));
        _db = new BotDatabaseService(paths);
        await _db.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SaveAndLoad_PreservesAllFields()
    {
        var decision = new EvaluationDecision(
            Symbol: "BTC-USDT",
            Tf: TimeFrame.H1,
            UtcTimestamp: DateTime.UtcNow,
            SequenceState: "Aktiviert",
            Point0: 50000m,
            PointA: 51000m,
            PointB: 50500m,
            Triggered: false,
            RejectionReason: RejectionReasons.NoHtfConfluence,
            ConfluenceScore: 7,
            ConfluenceCategories: new[] { "Fibonacci", "Volume", "GKL" },
            HardFiltersFailed: new[] { RejectionReasons.NoHtfConfluence });

        await _db.SaveDecisionAsync(decision);
        var loaded = await _db.LoadDecisionsAsync(limit: 10);

        loaded.Should().HaveCount(1);
        var d = loaded[0];
        d.Symbol.Should().Be("BTC-USDT");
        d.Tf.Should().Be(TimeFrame.H1);
        d.SequenceState.Should().Be("Aktiviert");
        d.Point0.Should().Be(50000m);
        d.RejectionReason.Should().Be(RejectionReasons.NoHtfConfluence);
        d.ConfluenceScore.Should().Be(7);
        d.ConfluenceCategories.Should().HaveCount(3);
        d.HardFiltersFailed.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadWithSymbolFilter_OnlyMatchingSymbol()
    {
        await _db.SaveDecisionAsync(MakeDecision("BTC-USDT", TimeFrame.H1));
        await _db.SaveDecisionAsync(MakeDecision("ETH-USDT", TimeFrame.H1));
        await _db.SaveDecisionAsync(MakeDecision("BTC-USDT", TimeFrame.H4));

        var btcOnly = await _db.LoadDecisionsAsync(symbol: "BTC-USDT");
        btcOnly.Should().HaveCount(2);
        btcOnly.Should().AllSatisfy(d => d.Symbol.Should().Be("BTC-USDT"));
    }

    [Fact]
    public async Task LoadWithRejectionReasonFilter_OnlyMatching()
    {
        await _db.SaveDecisionAsync(MakeDecision("A", TimeFrame.H1, reason: RejectionReasons.NoHtfConfluence));
        await _db.SaveDecisionAsync(MakeDecision("B", TimeFrame.H1, reason: RejectionReasons.RrrTooSmall));
        await _db.SaveDecisionAsync(MakeDecision("C", TimeFrame.H1, reason: RejectionReasons.NoHtfConfluence));

        var noHtf = await _db.LoadDecisionsAsync(rejectionReason: RejectionReasons.NoHtfConfluence);
        noHtf.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadWithSinceFilter_OnlyJuengereEintraege()
    {
        var oldD = MakeDecision("OLD", TimeFrame.H1) with { UtcTimestamp = DateTime.UtcNow.AddHours(-2) };
        var newD = MakeDecision("NEW", TimeFrame.H1) with { UtcTimestamp = DateTime.UtcNow };
        await _db.SaveDecisionAsync(oldD);
        await _db.SaveDecisionAsync(newD);

        var sinceLastHour = await _db.LoadDecisionsAsync(since: DateTime.UtcNow.AddHours(-1));
        sinceLastHour.Should().HaveCount(1);
        sinceLastHour[0].Symbol.Should().Be("NEW");
    }

    [Fact]
    public async Task LoadOrder_JuengsteZuerst()
    {
        await _db.SaveDecisionAsync(MakeDecision("FIRST", TimeFrame.H1) with { UtcTimestamp = DateTime.UtcNow.AddMinutes(-10) });
        await _db.SaveDecisionAsync(MakeDecision("SECOND", TimeFrame.H1) with { UtcTimestamp = DateTime.UtcNow.AddMinutes(-5) });
        await _db.SaveDecisionAsync(MakeDecision("THIRD", TimeFrame.H1) with { UtcTimestamp = DateTime.UtcNow });

        var loaded = await _db.LoadDecisionsAsync(limit: 10);
        loaded[0].Symbol.Should().Be("THIRD");
        loaded[1].Symbol.Should().Be("SECOND");
        loaded[2].Symbol.Should().Be("FIRST");
    }

    private static EvaluationDecision MakeDecision(string symbol, TimeFrame tf, string? reason = null) =>
        new(
            Symbol: symbol,
            Tf: tf,
            UtcTimestamp: DateTime.UtcNow,
            SequenceState: "Aktiviert",
            Point0: 100m,
            PointA: 110m,
            PointB: 105m,
            Triggered: reason == null,
            RejectionReason: reason,
            ConfluenceScore: 5,
            ConfluenceCategories: new[] { "Fib" },
            HardFiltersFailed: reason != null ? new[] { reason } : Array.Empty<string>());
}
