using Android.App;
using Android.Gms.Games;
using MeineApps.Core.Ava.Services;
using BomberBlast.Services;

namespace MeineApps.Core.Premium.Ava.Droid;

/// <summary>
/// Android-Implementierung von IPlayGamesService mit Google Play Games Services v2.
/// Linked File - wird per Compile Include in BomberBlast.Android eingebunden.
///
/// GPGS v2: Automatisches Sign-In (kein manueller Button nötig).
/// PlayGamesSdk.Initialize() muss VOR dem ersten Client-Aufruf erfolgen.
/// </summary>
public class AndroidPlayGamesService : IPlayGamesService
{
    private const string Tag = "AndroidPlayGamesService";

    private readonly Activity _activity;
    private readonly IPreferencesService _preferences;
    private const string PREF_ENABLED = "PlayGamesEnabled";

    private bool _isSignedIn;
    private string? _playerName;

    public bool IsSignedIn => _isSignedIn;
    public string? PlayerName => _playerName;

    public bool IsEnabled
    {
        get => _preferences.Get(PREF_ENABLED, true);
        set => _preferences.Set(PREF_ENABLED, value);
    }

    public event EventHandler<bool>? SignInStatusChanged;

    public AndroidPlayGamesService(Activity activity, IPreferencesService preferences)
    {
        _activity = activity;
        _preferences = preferences;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SDK INITIALISIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// PlayGamesSdk.Initialize() aufrufen. Muss VOR SignInAsync() passieren.
    /// </summary>
    public void InitializeSdk()
    {
        try
        {
            PlayGamesSdk.Initialize(_activity);
            Android.Util.Log.Info(Tag, "PlayGamesSdk initialisiert");
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(Tag, $"SDK-Init fehlgeschlagen: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SIGN-IN
    // ═══════════════════════════════════════════════════════════════════════

    public Task<bool> SignInAsync()
    {
        if (!IsEnabled)
        {
            Android.Util.Log.Info(Tag, "GPGS deaktiviert - Sign-In übersprungen");
            return Task.FromResult(false);
        }

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
                            Android.Util.Log.Info(Tag, "Sign-In erfolgreich");
                            _playerName = "Google Play";
                        }
                        else
                        {
                            Android.Util.Log.Info(Tag, "Nicht authentifiziert");
                        }

                        SignInStatusChanged?.Invoke(this, authenticated);
                        tcs.TrySetResult(authenticated);
                    }
                    else
                    {
                        Android.Util.Log.Warn(Tag, $"Sign-In fehlgeschlagen: {task.Exception?.Message}");
                        _isSignedIn = false;
                        SignInStatusChanged?.Invoke(this, false);
                        tcs.TrySetResult(false);
                    }
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Error(Tag, $"Sign-In Callback Fehler: {ex.Message}");
                    tcs.TrySetResult(false);
                }
            }));
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(Tag, $"Sign-In Fehler: {ex.Message}");
            tcs.TrySetResult(false);
        }

        return tcs.Task;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LEADERBOARDS
    // ═══════════════════════════════════════════════════════════════════════

    public Task SubmitScoreAsync(string leaderboardId, long score)
    {
        if (!_isSignedIn || !IsEnabled || string.IsNullOrEmpty(leaderboardId))
            return Task.CompletedTask;

        // Platzhalter-IDs überspringen
        if (leaderboardId.StartsWith("TODO_"))
            return Task.CompletedTask;

        try
        {
            var leaderboardsClient = PlayGames.GetLeaderboardsClient(_activity);
            leaderboardsClient.SubmitScore(leaderboardId, score);
            Android.Util.Log.Info(Tag, $"Score {score} an Leaderboard {leaderboardId} gesendet");
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(Tag, $"SubmitScore Fehler: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task ShowLeaderboardsAsync()
    {
        if (!_isSignedIn || !IsEnabled)
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
                    if (task.IsSuccessful && task.Result is Android.Content.Intent intent)
                    {
                        _activity.StartActivityForResult(intent, 9001);
                    }
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Error(Tag, $"Leaderboards anzeigen Fehler: {ex.Message}");
                }
                tcs.TrySetResult(true);
            }));
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(Tag, $"ShowLeaderboards Fehler: {ex.Message}");
            tcs.TrySetResult(false);
        }

        return tcs.Task;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ACHIEVEMENTS
    // ═══════════════════════════════════════════════════════════════════════

    public Task UnlockAchievementAsync(string achievementId)
    {
        if (!_isSignedIn || !IsEnabled || string.IsNullOrEmpty(achievementId))
            return Task.CompletedTask;

        // Platzhalter-IDs überspringen
        if (achievementId.StartsWith("TODO_"))
            return Task.CompletedTask;

        try
        {
            var achievementsClient = PlayGames.GetAchievementsClient(_activity);
            achievementsClient.Unlock(achievementId);
            Android.Util.Log.Info(Tag, $"Achievement {achievementId} freigeschaltet");
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(Tag, $"UnlockAchievement Fehler: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task IncrementAchievementAsync(string achievementId, int steps)
    {
        if (!_isSignedIn || !IsEnabled || string.IsNullOrEmpty(achievementId) || steps <= 0)
            return Task.CompletedTask;

        // Platzhalter-IDs überspringen
        if (achievementId.StartsWith("TODO_"))
            return Task.CompletedTask;

        try
        {
            var achievementsClient = PlayGames.GetAchievementsClient(_activity);
            achievementsClient.Increment(achievementId, steps);
            Android.Util.Log.Info(Tag, $"Achievement {achievementId} um {steps} inkrementiert");
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(Tag, $"IncrementAchievement Fehler: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task ShowAchievementsAsync()
    {
        if (!_isSignedIn || !IsEnabled)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource<bool>();

        try
        {
            var achievementsClient = PlayGames.GetAchievementsClient(_activity);
            var intentTask = achievementsClient.GetAchievementsIntent();

            intentTask.AddOnCompleteListener(new OnCompleteListener(task =>
            {
                try
                {
                    if (task.IsSuccessful && task.Result is Android.Content.Intent intent)
                    {
                        _activity.StartActivityForResult(intent, 9002);
                    }
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Error(Tag, $"Achievements anzeigen Fehler: {ex.Message}");
                }
                tcs.TrySetResult(true);
            }));
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(Tag, $"ShowAchievements Fehler: {ex.Message}");
            tcs.TrySetResult(false);
        }

        return tcs.Task;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // JAVA CALLBACK HELPER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generischer OnCompleteListener für Android.Gms.Tasks.Task.
    /// Erbt von Java.Lang.Object (IJavaPeerable-Anforderung).
    /// </summary>
    private class OnCompleteListener : Java.Lang.Object, Android.Gms.Tasks.IOnCompleteListener
    {
        private readonly Action<Android.Gms.Tasks.Task> _callback;

        public OnCompleteListener(Action<Android.Gms.Tasks.Task> callback)
        {
            _callback = callback;
        }

        public void OnComplete(Android.Gms.Tasks.Task task)
        {
            _callback(task);
        }
    }
}
