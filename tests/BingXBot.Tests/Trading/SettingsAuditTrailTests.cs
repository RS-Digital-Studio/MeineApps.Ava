using BingXBot.Core.Configuration;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Trading;
using BingXBot.Trading.Local;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BingXBot.Tests.Trading;

// v1.6.3 Phase 14 — Settings-Change-Audit-Trail.
public class SettingsAuditTrailTests : IAsyncLifetime
{
    private string _tempDir = "";
    private BotDatabaseService _db = null!;

    public async Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bingxbot-audit-" + Guid.NewGuid().ToString("N"));
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
    public async Task SaveRisk_RecordsDiffPerChangedField()
    {
        var risk = new RiskSettings();
        var scanner = new ScannerSettings();
        var bot = new BotSettings();
        var backtest = new BacktestSettings();
        var svc = new LocalSettingsService(bot, risk, scanner, backtest, _db);

        var newRisk = new RiskSettings
        {
            MaxPositionSizePercent = risk.MaxPositionSizePercent + 1m,
            MaxLeverage = risk.MaxLeverage + 5m,
        };
        // Restliche Properties bleiben gleich → Diff hat exakt 2 Eintraege.
        CopyAllPublicPropsExceptOverrides(risk, newRisk,
            overrideNames: new[] { nameof(RiskSettings.MaxPositionSizePercent), nameof(RiskSettings.MaxLeverage) });

        await svc.SaveRiskAsync(newRisk);
        var history = await _db.GetSettingsHistoryAsync(limit: 50);
        history.Should().HaveCount(2);
        history.Should().Contain(c => c.Field == "Risk.MaxPositionSizePercent");
        history.Should().Contain(c => c.Field == "Risk.MaxLeverage");
    }

    [Fact]
    public async Task NoChanges_NoEntries()
    {
        var risk = new RiskSettings();
        var svc = new LocalSettingsService(new BotSettings(), risk, new ScannerSettings(), new BacktestSettings(), _db);

        // Save mit identischen Werten → kein Diff.
        await svc.SaveRiskAsync(new RiskSettings());
        var history = await _db.GetSettingsHistoryAsync(limit: 10);
        history.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAll_RecordsSnapshotInOneRow()
    {
        // Plan-Spez: 1× Snapshot pro SaveAllAsync-Call (in irgend EINER Diff-Row, nicht pro Field).
        var risk = new RiskSettings();
        var svc = new LocalSettingsService(new BotSettings(), risk, new ScannerSettings(), new BacktestSettings(), _db);

        var newRisk = new RiskSettings { MaxLeverage = 50m };
        var newScanner = new ScannerSettings { EnableFundingRateBonus = false };
        await svc.SaveAllAsync(new BingXBot.Contracts.Dto.FullSettingsDto(
            new BotSettings(), newRisk, newScanner, new BacktestSettings(), 0));
        var history = await _db.GetSettingsHistoryAsync(limit: 100);
        history.Should().NotBeEmpty();
        history.Should().Contain(c => !string.IsNullOrEmpty(c.Snapshot),
            "1× Full-Snapshot pro SaveAllAsync — egal in welcher Row");
        // Maximal eine Row mit Snapshot, sonst quadratische DB-Groesse.
        history.Count(c => !string.IsNullOrEmpty(c.Snapshot)).Should().Be(1);
    }

    [Fact]
    public async Task History_FilterByField_Works()
    {
        // Default MaxLeverage = 10m → wir muessen 11m + 22m setzen, damit der Diff feuert.
        var risk = new RiskSettings();
        var svc = new LocalSettingsService(new BotSettings(), risk, new ScannerSettings(), new BacktestSettings(), _db);

        await svc.SaveRiskAsync(new RiskSettings { MaxLeverage = 11m });
        await svc.SaveRiskAsync(new RiskSettings { MaxLeverage = 22m, MaxPositionSizePercent = 5m });

        var leverageOnly = await _db.GetSettingsHistoryAsync(fieldFilter: "Risk.MaxLeverage");
        leverageOnly.Should().HaveCountGreaterThanOrEqualTo(2);
        leverageOnly.Should().AllSatisfy(c => c.Field.Should().Be("Risk.MaxLeverage"));
    }

    [Fact]
    public async Task PurgeOldSettingsChanges_RemovesOnlyOlderThanCutoff()
    {
        // Direkt in DB schreiben mit einem alten und einem neuen Eintrag.
        await _db.LogSettingsChangesAsync(new[]
        {
            new SettingsChange(DateTime.UtcNow.AddDays(-100), "Risk.X", "1", "2", "Test", null),
            new SettingsChange(DateTime.UtcNow, "Risk.Y", "3", "4", "Test", null),
        });
        var beforePurge = await _db.GetSettingsHistoryAsync();
        beforePurge.Should().HaveCount(2);

        await _db.PurgeOldSettingsChangesAsync(DateTime.UtcNow.AddDays(-30));

        var afterPurge = await _db.GetSettingsHistoryAsync();
        afterPurge.Should().HaveCount(1);
        afterPurge[0].Field.Should().Be("Risk.Y");
    }

    /// <summary>
    /// Kopiert alle Public-Properties von src nach dst, AUSSER denen in overrideNames.
    /// So bleibt "newRisk" bis auf die explizit gesetzten Werte identisch zu "risk".
    /// </summary>
    private static void CopyAllPublicPropsExceptOverrides<T>(T src, T dst, string[] overrideNames) where T : class
    {
        var skip = new HashSet<string>(overrideNames);
        foreach (var p in typeof(T).GetProperties())
        {
            if (!p.CanRead || !p.CanWrite) continue;
            if (skip.Contains(p.Name)) continue;
            try { p.SetValue(dst, p.GetValue(src)); } catch { /* best-effort */ }
        }
    }
}
