using BomberBlast.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für AccountDeletionService (v2.0.44 — DSGVO Art. 17).
/// Validiert Best-Effort-Verhalten: Bei Network-Fehler werden lokale Daten trotzdem gelöscht.
/// Reihenfolge: 1. Firebase 2. Cloud-Save 3. Lokale Daten.
/// </summary>
public class AccountDeletionServiceTests
{
    private static AccountDeletionService CreateService(
        out ILeagueService league,
        out ICloudSaveService cloud,
        out IProgressService progress,
        out IHighScoreService scores,
        out InMemoryPreferences prefs)
    {
        league = Substitute.For<ILeagueService>();
        cloud = Substitute.For<ICloudSaveService>();
        var playGames = Substitute.For<IPlayGamesService>();
        prefs = new InMemoryPreferences();
        progress = Substitute.For<IProgressService>();
        scores = Substitute.For<IHighScoreService>();
        var logger = Substitute.For<ILogger<AccountDeletionService>>();

        league.DeleteOwnEntryAsync().Returns(Task.CompletedTask);
        cloud.DeleteCloudSaveAsync().Returns(Task.CompletedTask);

        return new AccountDeletionService(league, cloud, playGames, prefs, progress, scores, logger);
    }

    [Fact]
    public async Task DeleteAccountAsync_AllesErfolgreich_LiefertSuccess()
    {
        var svc = CreateService(out var league, out var cloud, out var progress, out var scores, out _);

        var result = await svc.DeleteAccountAsync();

        result.Success.Should().BeTrue();
        result.FirebaseLeagueDeleted.Should().BeTrue();
        result.CloudSaveDeleted.Should().BeTrue();
        result.LocalDataDeleted.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();

        await league.Received().DeleteOwnEntryAsync();
        await cloud.Received().DeleteCloudSaveAsync();
        scores.Received().ClearScores();
        progress.Received().ResetProgress();
    }

    [Fact]
    public async Task DeleteAccountAsync_FirebaseFehlschlaegt_LokaleDatenTrotzdemGeloescht()
    {
        var svc = CreateService(out var league, out var cloud, out var progress, out var scores, out _);
        league.DeleteOwnEntryAsync().Returns<Task>(_ => throw new InvalidOperationException("Firebase down"));

        var result = await svc.DeleteAccountAsync();

        result.Success.Should().BeFalse();
        result.FirebaseLeagueDeleted.Should().BeFalse();
        result.CloudSaveDeleted.Should().BeTrue("Cloud bleibt erreichbar");
        result.LocalDataDeleted.Should().BeTrue("DSGVO: Lokale Daten MÜSSEN entfernt werden");
        result.ErrorMessage.Should().Contain("Firebase");

        scores.Received().ClearScores();
        progress.Received().ResetProgress();
    }

    [Fact]
    public async Task DeleteAccountAsync_CloudSaveFehlschlaegt_LokaleDatenTrotzdemGeloescht()
    {
        var svc = CreateService(out _, out var cloud, out var progress, out var scores, out _);
        cloud.DeleteCloudSaveAsync().Returns<Task>(_ => throw new InvalidOperationException("Network"));

        var result = await svc.DeleteAccountAsync();

        result.CloudSaveDeleted.Should().BeFalse();
        result.LocalDataDeleted.Should().BeTrue();
        scores.Received().ClearScores();
        progress.Received().ResetProgress();
    }

    [Fact]
    public async Task DeleteAccountAsync_LokaleDatenWerdenAuchGeleert()
    {
        var svc = CreateService(out _, out _, out _, out _, out var prefs);
        prefs.Set("TestKey", "TestValue");

        await svc.DeleteAccountAsync();

        prefs.ContainsKey("TestKey").Should().BeFalse("Preferences.Clear() muss alle Keys entfernen");
    }

    [Fact]
    public async Task DeleteAccountAsync_AlleStepsFehlschlagen_ErrorMessageGesetzt()
    {
        var svc = CreateService(out var league, out var cloud, out var progress, out var scores, out _);
        league.DeleteOwnEntryAsync().Returns<Task>(_ => throw new InvalidOperationException("L"));
        cloud.DeleteCloudSaveAsync().Returns<Task>(_ => throw new InvalidOperationException("C"));
        scores.WhenForAnyArgs(s => s.ClearScores()).Throw(new InvalidOperationException("S"));

        var result = await svc.DeleteAccountAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
    }
}
