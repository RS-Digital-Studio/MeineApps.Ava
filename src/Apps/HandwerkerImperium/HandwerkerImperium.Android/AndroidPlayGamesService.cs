using Android.App;
using Android.Gms.Games;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Android;

/// <summary>
/// Android-Implementierung für Google Play Games Services v2.
/// Unterstützt Sign-In, Leaderboards und Score-Submit.
/// Cloud Save (Snapshots) ist in Play Games v2 NuGet (121.0.0.2) nicht verfügbar
/// → Stub-Implementierung die false/null zurückgibt.
/// PlayGamesSdk.Initialize() muss VOR dem ersten Aufruf erfolgen (in MainActivity).
/// </summary>
public class AndroidPlayGamesService : IPlayGamesService
{
    private const string Tag = "PlayGames";

    private readonly Activity _activity;
    private bool _isSignedIn;
    private string? _playerDisplayName;

    // Leaderboard IDs (aus Play Console)
    public const string LeaderboardTotalEarnings = "CgkIoeDj0ZMKEAIQDg";
    public const string LeaderboardBestWorkshop = "CgkIoeDj0ZMKEAIQDw";
    public const string LeaderboardPerfectRatings = "CgkIoeDj0ZMKEAIQEA";
    public const string LeaderboardPrestigeMaster = "CgkIoeDj0ZMKEAIQEQ";
    public const string LeaderboardPlayerLevel = "CgkIoeDj0ZMKEAIQEg";

    // Gilden-Leaderboard IDs (Platzhalter, müssen in Play Console angelegt werden)
    public const string LeaderboardGuildWood = "TODO_GUILD_WOOD";
    public const string LeaderboardGuildMetal = "TODO_GUILD_METAL";
    public const string LeaderboardGuildElectric = "TODO_GUILD_ELECTRIC";
    public const string LeaderboardGuildBuild = "TODO_GUILD_BUILD";
    public const string LeaderboardGuildDesign = "TODO_GUILD_DESIGN";

    // Achievement IDs (aus Play Console)
    public const string AchievementFirstSteps = "CgkIoeDj0ZMKEAIQAQ";
    public const string AchievementTeamBuilder = "CgkIoeDj0ZMKEAIQAg";
    public const string AchievementDeveloper = "CgkIoeDj0ZMKEAIQAw";
    public const string AchievementScientist = "CgkIoeDj0ZMKEAIQBA";
    public const string AchievementPerfection = "CgkIoeDj0ZMKEAIQBQ";
    public const string AchievementReliableWorker = "CgkIoeDj0ZMKEAIQBg";
    public const string AchievementMaximumPower = "CgkIoeDj0ZMKEAIQBw";
    public const string AchievementBigBusiness = "CgkIoeDj0ZMKEAIQCA";
    public const string AchievementMillionaire = "CgkIoeDj0ZMKEAIQCQ";
    public const string AchievementOnFire = "CgkIoeDj0ZMKEAIQCg";
    public const string AchievementWeekWarrior = "CgkIoeDj0ZMKEAIQCw";
    public const string AchievementGenius = "CgkIoeDj0ZMKEAIQDA";
    public const string AchievementGoldenLegend = "CgkIoeDj0ZMKEAIQDQ";
    public const string AchievementNewBeginning = "CgkIoeDj0ZMKEAIQEw";

    public bool IsSignedIn => _isSignedIn;

    // Cloud Save nicht verfügbar in Play Games v2 NuGet
    public bool SupportsCloudSave => false;
    public string? PlayerDisplayName => _playerDisplayName;

    /// <summary>
    /// Prüft ob die Activity noch gültig ist (verhindert native Java-Exceptions).
    /// </summary>
    private bool IsActivityValid => !_activity.IsFinishing && !_activity.IsDestroyed;

    public AndroidPlayGamesService(Activity activity)
    {
        _activity = activity;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SIGN-IN
    // ═══════════════════════════════════════════════════════════════════════

    public Task<bool> SignInAsync()
    {
        if (!IsActivityValid)
            return Task.FromResult(false);

        var tcs = new TaskCompletionSource<bool>();

        try
        {
            var gamesSignInClient = PlayGames.GetGamesSignInClient(_activity);
            var authTask = gamesSignInClient.IsAuthenticated();

            authTask.AddOnCompleteListener(new OnCompleteListener(task =>
            {
                try
                {
                    if (task.IsSuccessful)
                    {
                        var result = task.Result as Java.Lang.Boolean;
                        bool authenticated = result?.BooleanValue() ?? false;

                        _isSignedIn = authenticated;
                        if (authenticated)
                        {
                            global::Android.Util.Log.Info(Tag, "Sign-In erfolgreich");
                            // Spielername laden
                            LoadPlayerName();
                        }
                        else
                        {
                            global::Android.Util.Log.Info(Tag, "Nicht authentifiziert");
                        }

                        tcs.TrySetResult(authenticated);
                    }
                    else
                    {
                        global::Android.Util.Log.Warn(Tag, $"Sign-In fehlgeschlagen: {task.Exception?.Message}");
                        _isSignedIn = false;
                        tcs.TrySetResult(false);
                    }
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Error(Tag, $"Sign-In Callback Fehler: {ex.Message}");
                    tcs.TrySetResult(false);
                }
            }));
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error(Tag, $"Sign-In Fehler: {ex.Message}");
            tcs.TrySetResult(false);
        }

        return tcs.Task;
    }

    private void LoadPlayerName()
    {
        if (!IsActivityValid) return;

        try
        {
            // Play Games v2: PlayGames.GetPlayersClient() existiert nicht im NuGet-Binding.
            // Spielername wird stattdessen über GamesSignInClient geholt (falls verfügbar).
            // Fallback: Kein Spielername verfügbar.
            global::Android.Util.Log.Info(Tag, "Spielername-Laden: PlayersClient nicht in NuGet-Binding verfügbar");
            _playerDisplayName = null;
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error(Tag, $"LoadPlayerName Fehler: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LEADERBOARDS
    // ═══════════════════════════════════════════════════════════════════════

    public Task SubmitScoreAsync(string leaderboardId, long score)
    {
        if (!_isSignedIn || string.IsNullOrEmpty(leaderboardId))
            return Task.CompletedTask;

        // Platzhalter-IDs überspringen
        if (leaderboardId.StartsWith("TODO_"))
            return Task.CompletedTask;

        if (!IsActivityValid)
            return Task.CompletedTask;

        try
        {
            var leaderboardsClient = PlayGames.GetLeaderboardsClient(_activity);
            leaderboardsClient.SubmitScore(leaderboardId, score);
            global::Android.Util.Log.Info(Tag, $"Score {score} an Leaderboard {leaderboardId} gesendet");
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error(Tag, $"SubmitScore Fehler: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task ShowLeaderboardsAsync()
    {
        if (!_isSignedIn || !IsActivityValid)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource<bool>();

        try
        {
            var leaderboardsClient = PlayGames.GetLeaderboardsClient(_activity);
            var intentTask = leaderboardsClient.GetAllLeaderboardsIntent();

            intentTask.AddOnCompleteListener(new OnCompleteListener(task =>
            {
                try
                {
                    if (task.IsSuccessful && task.Result is global::Android.Content.Intent intent)
                    {
                        _activity.StartActivityForResult(intent, 9001);
                    }
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Error(Tag, $"Leaderboards anzeigen Fehler: {ex.Message}");
                }
                tcs.TrySetResult(true);
            }));
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error(Tag, $"ShowLeaderboards Fehler: {ex.Message}");
            tcs.TrySetResult(false);
        }

        return tcs.Task;
    }

    public Task<List<PlayGamesLeaderboardEntry>> LoadLeaderboardScoresAsync(string leaderboardId, int maxResults)
    {
        if (!_isSignedIn || string.IsNullOrEmpty(leaderboardId) || !IsActivityValid)
            return Task.FromResult(new List<PlayGamesLeaderboardEntry>());

        // Platzhalter-IDs → leere Liste
        if (leaderboardId.StartsWith("TODO_"))
            return Task.FromResult(new List<PlayGamesLeaderboardEntry>());

        // Play Games v2 NuGet (121.0.0.2) bietet keine LoadTopScores() API.
        // Leaderboard-Daten nur über native UI (GetAllLeaderboardsIntent) anzeigbar.
        // Für programmatischen Zugriff neueres NuGet oder native Java-API nötig.
        global::Android.Util.Log.Info(Tag, $"LoadLeaderboardScores: API nicht im NuGet-Binding verfügbar (Leaderboard {leaderboardId})");
        return Task.FromResult(new List<PlayGamesLeaderboardEntry>());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CLOUD SAVE (Snapshots API - NICHT VERFÜGBAR in Play Games v2 NuGet)
    // ═══════════════════════════════════════════════════════════════════════
    // Die Snapshots-API (GetSnapshotsClient, ISnapshot, SnapshotMetadataChange.Builder)
    // ist im Xamarin.GooglePlayServices.Games.V2 NuGet (121.0.0.2) nicht gebunden.
    // Cloud Save wird deshalb als Stub implementiert.
    // Wenn ein neueres NuGet die API bindet, kann dies hier aktiviert werden.

    public Task<bool> SaveToCloudAsync(string jsonData, string description)
    {
        // Play Games v2 Snapshots API nicht im NuGet-Binding (121.0.0.2) verfügbar
        global::Android.Util.Log.Warn(Tag, "Cloud Save nicht verfügbar: Snapshots-API fehlt im NuGet-Binding");
        return Task.FromResult(false);
    }

    public Task<string?> LoadCloudSaveAsync()
    {
        // Play Games v2 Snapshots API nicht im NuGet-Binding (121.0.0.2) verfügbar
        global::Android.Util.Log.Warn(Tag, "Cloud Load nicht verfügbar: Snapshots-API fehlt im NuGet-Binding");
        return Task.FromResult<string?>(null);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // JAVA CALLBACK HELPER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generischer OnCompleteListener für Android.Gms.Tasks.Task.
    /// Erbt von Java.Lang.Object (IJavaPeerable-Anforderung).
    /// </summary>
    private class OnCompleteListener : Java.Lang.Object, global::Android.Gms.Tasks.IOnCompleteListener
    {
        private readonly Action<global::Android.Gms.Tasks.Task> _callback;

        public OnCompleteListener(Action<global::Android.Gms.Tasks.Task> callback)
        {
            _callback = callback;
        }

        public void OnComplete(global::Android.Gms.Tasks.Task task)
        {
            _callback(task);
        }
    }
}
