using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Desktop-Stub für Google Play Games (nicht verfügbar auf Desktop).
/// </summary>
public class PlayGamesService : IPlayGamesService
{
    public bool IsSignedIn => false;
    public bool SupportsCloudSave => false;
    public string? PlayerDisplayName => null;

    public Task<bool> SignInAsync() => Task.FromResult(false);
    public Task SubmitScoreAsync(string leaderboardId, long score) => Task.CompletedTask;
    public Task ShowLeaderboardsAsync() => Task.CompletedTask;
    public Task<string?> LoadCloudSaveAsync() => Task.FromResult<string?>(null);
    public Task<bool> SaveToCloudAsync(string jsonData, string description) => Task.FromResult(false);
    public Task<List<PlayGamesLeaderboardEntry>> LoadLeaderboardScoresAsync(string leaderboardId, int maxResults)
        => Task.FromResult(new List<PlayGamesLeaderboardEntry>());
}
