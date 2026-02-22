namespace BomberBlast.Services;

/// <summary>
/// No-Op Implementierung für Desktop (kein Google Play Games).
/// </summary>
public class NullPlayGamesService : IPlayGamesService
{
    public bool IsSignedIn => false;
    public string? PlayerName => null;
    public bool IsEnabled { get; set; }

#pragma warning disable CS0067 // Event wird im Null-Service nie gefeuert
    public event EventHandler<bool>? SignInStatusChanged;
#pragma warning restore CS0067

    public Task<bool> SignInAsync() => Task.FromResult(false);
    public Task SubmitScoreAsync(string leaderboardId, long score) => Task.CompletedTask;
    public Task ShowLeaderboardsAsync() => Task.CompletedTask;
    public Task ShowAchievementsAsync() => Task.CompletedTask;
    public Task UnlockAchievementAsync(string achievementId) => Task.CompletedTask;
    public Task IncrementAchievementAsync(string achievementId, int steps) => Task.CompletedTask;
    public Task<bool> SaveToCloudAsync(string jsonData) => Task.FromResult(false);
    public Task<string?> LoadCloudSaveAsync() => Task.FromResult<string?>(null);
}
