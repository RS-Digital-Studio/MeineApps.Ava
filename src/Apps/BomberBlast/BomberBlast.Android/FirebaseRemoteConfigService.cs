using BomberBlast.Services;
using Firebase.RemoteConfig;

namespace BomberBlast.Droid;

/// <summary>
/// Android-Implementation von <see cref="IRemoteConfigService"/> (Sprint 2.1 AAA-Audit #1).
///
/// <para>
/// Erbt von <see cref="DefaultsRemoteConfigService"/> — die eingebetteten JSON-Defaults bleiben
/// als Fallback fuer Offline + Erststart aktiv. Nach einem erfolgreichen Firebase-Fetch werden
/// einzelne Keys via <see cref="DefaultsRemoteConfigService.ApplyRawRemoteValue"/> ueberschrieben.
/// </para>
///
/// <para>
/// Cache-TTL: 1h in Production, 5min in Debug — verhindert Quota-Throttling bei Firebase
/// (Free-Tier: 5 Fetches/Stunde pro App-Instanz). Der initiale Fetch laeuft non-blocking
/// parallel zum App-Start, damit das erste Frame nicht auf das Netzwerk wartet.
/// </para>
/// </summary>
public sealed class FirebaseRemoteConfigService : DefaultsRemoteConfigService
{
    private const long FetchIntervalProductionSeconds = 3600;  // 1h
    private const long FetchIntervalDebugSeconds = 300;        // 5min

    /// <summary>Firebase Remote Config liefert Source==2 fuer echte Server-Werte (Static=0, Default=1).</summary>
    private const int RemoteConfigSourceRemote = 2;

    private readonly bool _isDebugBuild;
    private FirebaseRemoteConfig? _firebase;

    public FirebaseRemoteConfigService(IAppLogger logger, bool isDebugBuild)
        : base(logger)
    {
        _isDebugBuild = isDebugBuild;
    }

    public override async Task InitializeAsync()
    {
        // 1. Eingebettete JSON-Defaults laden (Basis-Klasse) — App ist sofort funktionsfaehig.
        await base.InitializeAsync().ConfigureAwait(false);

        // 2. Firebase Remote Config konfigurieren.
        try
        {
            _firebase = FirebaseRemoteConfig.Instance;

            long minFetchInterval = _isDebugBuild
                ? FetchIntervalDebugSeconds
                : FetchIntervalProductionSeconds;

            var settings = new FirebaseRemoteConfigSettings.Builder()
                .SetMinimumFetchIntervalInSeconds(minFetchInterval)
                .Build();

            await SetConfigSettingsAsync(settings).ConfigureAwait(false);

            // 3. Ersten Fetch non-blocking anstossen — Ergebnis aktualisiert die Overrides
            //    sobald es da ist, blockiert aber nicht den App-Start.
            _ = FetchAndActivateAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError("FirebaseRemoteConfig: Init fehlgeschlagen — laufe mit eingebetteten Defaults.", ex);
            _firebase = null;
        }
    }

    public override Task<bool> FetchAndActivateAsync()
    {
        if (_firebase is null)
            return Task.FromResult(false);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _firebase.FetchAndActivate()
                .AddOnCompleteListener(new FetchCompleteListener(this, tcs));
        }
        catch (Exception ex)
        {
            Logger.LogError("FirebaseRemoteConfig: FetchAndActivate fehlgeschlagen.", ex);
            tcs.TrySetResult(false);
        }
        return tcs.Task;
    }

    /// <summary>
    /// Uebernimmt nach erfolgreichem Fetch alle bekannten Keys (<see cref="RemoteConfigKeys.All"/>),
    /// die wirklich vom Server kamen (Source==Remote). Static/Default-Werte werden ignoriert —
    /// dafuer sind bereits die eingebetteten JSON-Defaults zustaendig.
    /// </summary>
    private void ApplyRemoteValues()
    {
        if (_firebase is null) return;

        int applied = 0;
        foreach (var key in RemoteConfigKeys.All)
        {
            try
            {
                var value = _firebase.GetValue(key);
                if (value is null || value.Source != RemoteConfigSourceRemote)
                    continue;

                var raw = value.AsString();
                if (string.IsNullOrEmpty(raw))
                    continue;

                ApplyRawRemoteValue(key, raw);
                applied++;
            }
            catch (Exception ex)
            {
                // Ein einzelner kaputter Key darf den Rest des Imports nicht stoppen.
                Logger.LogError($"FirebaseRemoteConfig: Key '{key}' konnte nicht uebernommen werden.", ex);
            }
        }

        if (applied > 0)
        {
            Logger.LogInfo($"FirebaseRemoteConfig: {applied} Remote-Werte uebernommen.");
            RaiseConfigChanged();
        }
    }

    /// <summary>
    /// Wrappt die SetConfigSettings-Operation der Tasks-API in ein .NET-Task.
    /// </summary>
    private Task SetConfigSettingsAsync(FirebaseRemoteConfigSettings settings)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _firebase!.SetConfigSettingsAsync(settings)
                .AddOnCompleteListener(new SettingsCompleteListener(tcs));
        }
        catch (Exception ex)
        {
            Logger.LogError("FirebaseRemoteConfig: SetConfigSettings fehlgeschlagen.", ex);
            tcs.TrySetResult(false);
        }
        return tcs.Task;
    }

    // ─── Java-Callback-Wrapper fuer Android.Gms.Tasks-API ─────────────────────────

    private sealed class FetchCompleteListener : Java.Lang.Object, Android.Gms.Tasks.IOnCompleteListener
    {
        private readonly FirebaseRemoteConfigService _service;
        private readonly TaskCompletionSource<bool> _tcs;

        public FetchCompleteListener(FirebaseRemoteConfigService service, TaskCompletionSource<bool> tcs)
        {
            _service = service;
            _tcs = tcs;
        }

        public void OnComplete(Android.Gms.Tasks.Task task)
        {
            bool updated = false;
            try
            {
                if (task.IsSuccessful)
                {
                    // FetchAndActivate-Result: true = neue Werte aktiviert, false = nichts Neues.
                    updated = task.Result is Java.Lang.Boolean jb && jb.BooleanValue();
                    if (updated)
                        _service.ApplyRemoteValues();
                }
            }
            catch
            {
                // Best-Effort — bei Fehler bleiben die Defaults aktiv.
            }
            _tcs.TrySetResult(updated);
        }
    }

    private sealed class SettingsCompleteListener : Java.Lang.Object, Android.Gms.Tasks.IOnCompleteListener
    {
        private readonly TaskCompletionSource<bool> _tcs;
        public SettingsCompleteListener(TaskCompletionSource<bool> tcs) => _tcs = tcs;

        public void OnComplete(Android.Gms.Tasks.Task task)
            => _tcs.TrySetResult(task.IsSuccessful);
    }
}
