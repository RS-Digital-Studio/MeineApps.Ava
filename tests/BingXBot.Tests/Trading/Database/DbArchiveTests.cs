using BingXBot.Core.Data;
using BingXBot.Core.Diagnostics;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Trading;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BingXBot.Tests.Trading.Database;

// v1.6.1 Phase 11 — DB-Archivierung (Trades + Decisions + Settings-History).
public class DbArchiveTests : IAsyncLifetime
{
    private string _tempDir = "";
    private string _archiveDir = "";
    private BotDatabaseService _db = null!;

    public async Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bingxbot-archive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _archiveDir = Path.Combine(_tempDir, "archives");
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
    public async Task ArchiveTrades_MovesOldEntries()
    {
        var oldTrade = MakeTrade(DateTime.UtcNow.AddMonths(-15));
        var newTrade = MakeTrade(DateTime.UtcNow.AddDays(-1));
        await _db.SaveTradeAsync(oldTrade);
        await _db.SaveTradeAsync(newTrade);

        var archived = await _db.ArchiveTradesAsync(DateTime.UtcNow.AddMonths(-12), _archiveDir);

        archived.Should().Be(1, "nur der alte Trade ist >12 Monate");
        var remaining = await _db.GetTradesAsync(limit: 100);
        remaining.Should().HaveCount(1);
        remaining[0].EntryTime.Should().BeAfter(DateTime.UtcNow.AddMonths(-12));

        // Archive-File existiert.
        var archiveFiles = Directory.GetFiles(_archiveDir, "bot-archive-*.db");
        archiveFiles.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ArchiveTrades_KeepsRecent()
    {
        // Alle Trades juenger als Cutoff → kein Archive.
        await _db.SaveTradeAsync(MakeTrade(DateTime.UtcNow.AddDays(-5)));
        await _db.SaveTradeAsync(MakeTrade(DateTime.UtcNow.AddDays(-30)));

        var archived = await _db.ArchiveTradesAsync(DateTime.UtcNow.AddYears(-1), _archiveDir);
        archived.Should().Be(0);
        (await _db.GetTradesAsync(limit: 100)).Should().HaveCount(2);
    }

    [Fact]
    public async Task ArchiveTrades_Idempotent_RunsTwiceSafely()
    {
        await _db.SaveTradeAsync(MakeTrade(DateTime.UtcNow.AddMonths(-15)));

        var first = await _db.ArchiveTradesAsync(DateTime.UtcNow.AddMonths(-12), _archiveDir);
        var second = await _db.ArchiveTradesAsync(DateTime.UtcNow.AddMonths(-12), _archiveDir);

        first.Should().Be(1);
        second.Should().Be(0, "zweiter Run findet keinen alten Trade mehr in der Live-DB");
    }

    [Fact]
    public async Task PurgeOldDecisions_RemovesOnlyOlderThanCutoff()
    {
        await _db.SaveDecisionAsync(MakeDecision(DateTime.UtcNow.AddDays(-60)));
        await _db.SaveDecisionAsync(MakeDecision(DateTime.UtcNow.AddDays(-1)));

        var purged = await _db.PurgeOldDecisionsAsync(DateTime.UtcNow.AddDays(-30));
        purged.Should().Be(1);
        var remaining = await _db.LoadDecisionsAsync(limit: 100);
        remaining.Should().HaveCount(1);
    }

    [Fact]
    public async Task PurgeOldDecisions_NoneToRemove()
    {
        await _db.SaveDecisionAsync(MakeDecision(DateTime.UtcNow));
        var purged = await _db.PurgeOldDecisionsAsync(DateTime.UtcNow.AddDays(-30));
        purged.Should().Be(0);
    }

    private static CompletedTrade MakeTrade(DateTime entryTime) =>
        new(
            Symbol: "BTC-USDT",
            Side: Side.Buy,
            EntryPrice: 50000m,
            ExitPrice: 50500m,
            Quantity: 0.01m,
            Pnl: 5m,
            Fee: 0.5m,
            EntryTime: entryTime,
            ExitTime: entryTime.AddMinutes(30),
            Reason: "Test",
            Mode: TradingMode.Paper,
            NavigatorTimeframe: TimeFrame.H1);

    private static EvaluationDecision MakeDecision(DateTime ts) =>
        new(
            Symbol: "BTC-USDT",
            Tf: TimeFrame.H1,
            UtcTimestamp: ts,
            SequenceState: "Aktiviert",
            Point0: 100m, PointA: 110m, PointB: 105m,
            Triggered: true,
            RejectionReason: null,
            ConfluenceScore: 5,
            ConfluenceCategories: Array.Empty<string>(),
            HardFiltersFailed: Array.Empty<string>());
}
