using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Trading;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BingXBot.Tests.Trading.Database;

// Persistenz von Mode + Engine des letzten Engine-Starts (SaveResumeEngineAsync /
// LoadResumeEngineAsync). Ohne diese Persistenz startete Auto-Resume nach einem Pi-Reboot IMMER
// den Scalper-Default — im Worst-Case der Live-Scalper mit Echtgeld statt des laufenden
// Paper-Cross-Sectional-Tests. Temp-DB-Setup analog SettingsAuditTrailTests.
public class ResumeEnginePersistenceTests : IAsyncLifetime
{
    private string _tempDir = "";
    private BotDatabaseService _db = null!;

    public async ValueTask InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bingxbot-resume-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        var paths = Substitute.For<IAppPaths>();
        paths.DatabasePath.Returns(Path.Combine(_tempDir, "bot.db"));
        _db = new BotDatabaseService(paths);
        await _db.InitializeAsync();
    }

    public ValueTask DisposeAsync()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task LoadResumeEngine_OhneVorherigesSave_GibtNull()
    {
        // Nie persistiert → null (Auto-Resume faellt auf den BotStartRequest-Default zurueck).
        var loaded = await _db.LoadResumeEngineAsync();
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task SaveLoadResumeEngine_PaperCrossSectional_Roundtrip()
    {
        await _db.SaveResumeEngineAsync(TradingMode.Paper, EngineMode.CrossSectional);

        var loaded = await _db.LoadResumeEngineAsync();

        loaded.Should().NotBeNull();
        loaded!.Value.Mode.Should().Be(TradingMode.Paper);
        loaded.Value.Engine.Should().Be(EngineMode.CrossSectional);
    }

    [Fact]
    public async Task SaveLoadResumeEngine_LiveScalper_Roundtrip()
    {
        await _db.SaveResumeEngineAsync(TradingMode.Live, EngineMode.Scalper);

        var loaded = await _db.LoadResumeEngineAsync();

        loaded.Should().NotBeNull();
        loaded!.Value.Mode.Should().Be(TradingMode.Live);
        loaded.Value.Engine.Should().Be(EngineMode.Scalper);
    }

    [Fact]
    public async Task SaveResumeEngine_ZweiterSave_Ueberschreibt()
    {
        // InsertOrReplace → der juengste Save gewinnt (kein Akkumulieren alter Werte).
        await _db.SaveResumeEngineAsync(TradingMode.Paper, EngineMode.Scalper);
        await _db.SaveResumeEngineAsync(TradingMode.Live, EngineMode.CrossSectional);

        var loaded = await _db.LoadResumeEngineAsync();

        loaded.Should().NotBeNull();
        loaded!.Value.Mode.Should().Be(TradingMode.Live);
        loaded.Value.Engine.Should().Be(EngineMode.CrossSectional);
    }
}
