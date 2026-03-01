using Android.App;
using Android.Gms.Ads;
using Android.Gms.Ads.Rewarded;
using Android.Runtime;

namespace MeineApps.Core.Premium.Ava.Droid;

/// <summary>
/// Helper fuer Google AdMob Rewarded Ads auf Android.
/// Linked File - wird per Compile Include in Android-Projekte eingebunden.
/// NICHT kompiliert im net10.0 Library-Projekt.
/// Unterstuetzt Pre-Loading (Load + ShowAsync) und On-Demand (LoadAndShowAsync).
/// </summary>
public sealed class RewardedAdHelper : IDisposable
{
    private const string Tag = "RewardedAdHelper";
    private const int LoadTimeoutMs = 8000; // 8 Sekunden Timeout fuer On-Demand Ad-Laden
    private const int MaxRetryAttempts = 3;
    private static readonly int[] RetryDelaysMs = [5000, 15000, 30000]; // Exponentieller Backoff: 5s, 15s, 30s

    private RewardedAd? _rewardedAd;
    private Activity? _activity;
    private string _defaultAdUnitId = "";
    private bool _isLoading;
    private bool _disposed;
    private int _retryCount;

    /// <summary>Ob eine Rewarded Ad geladen und bereit ist</summary>
    public bool IsLoaded => _rewardedAd != null;

    /// <summary>Initialisiert und laedt die erste Rewarded Ad (Default-Placement)</summary>
    public void Load(Activity activity, string adUnitId)
    {
        _activity = activity;
        _defaultAdUnitId = adUnitId;
        Android.Util.Log.Info(Tag, $"Load aufgerufen mit AdUnitId: {adUnitId}");
        LoadInternal(adUnitId);
    }

    private void LoadInternal(string adUnitId)
    {
        if (_isLoading || _activity == null || _disposed)
        {
            Android.Util.Log.Warn(Tag, $"LoadInternal abgebrochen: isLoading={_isLoading}, activity={_activity != null}, disposed={_disposed}");
            return;
        }
        _isLoading = true;

        try
        {
            var adRequest = new AdRequest.Builder().Build();
            Android.Util.Log.Info(Tag, $"Lade Rewarded Ad: {adUnitId}");
            _activity.RunOnUiThread(() =>
            {
                RewardedAd.Load(_activity, adUnitId, adRequest, new LoadCallback(this));
            });
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(Tag, $"LoadInternal Exception: {ex.Message}");
            _isLoading = false;
        }
    }

    /// <summary>Zeigt die vorgeladene Rewarded Ad. Gibt true zurueck wenn User Belohnung verdient hat.</summary>
    public Task<bool> ShowAsync()
    {
        if (_rewardedAd == null || _activity == null)
        {
            Android.Util.Log.Warn(Tag, $"ShowAsync abgebrochen: rewardedAd={_rewardedAd != null}, activity={_activity != null}");
            return Task.FromResult(false);
        }

        var tcs = new TaskCompletionSource<bool>();
        var fullScreenCallback = new FullScreenCallback(tcs, this);
        var rewardCallback = new RewardCallback(fullScreenCallback);

        Android.Util.Log.Info(Tag, "ShowAsync: Zeige vorgeladene Rewarded Ad");
        _activity.RunOnUiThread(() =>
        {
            try
            {
                _rewardedAd!.FullScreenContentCallback = fullScreenCallback;
                _rewardedAd.Show(_activity!, rewardCallback);
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error(Tag, $"ShowAsync Exception: {ex.Message}");
                tcs.TrySetResult(false);
            }
        });

        return tcs.Task;
    }

    /// <summary>
    /// Laedt eine Rewarded Ad mit einer bestimmten Ad-Unit-ID und zeigt sie sofort.
    /// Fuer Placements die nicht vorgeladen werden (On-Demand).
    /// Gibt true zurueck wenn User Belohnung verdient hat.
    /// </summary>
    public async Task<bool> LoadAndShowAsync(string adUnitId)
    {
        if (_activity == null || _disposed)
        {
            Android.Util.Log.Warn(Tag, $"LoadAndShowAsync abgebrochen: activity={_activity != null}, disposed={_disposed}");
            return false;
        }

        var tcs = new TaskCompletionSource<bool>();
        var activity = _activity;

        try
        {
            var adRequest = new AdRequest.Builder().Build();
            Android.Util.Log.Info(Tag, $"LoadAndShowAsync: Lade Ad on-demand: {adUnitId}");
            activity.RunOnUiThread(() =>
            {
                RewardedAd.Load(activity, adUnitId, adRequest, new OnDemandLoadCallback(this, activity, tcs));
            });
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(Tag, $"LoadAndShowAsync Exception: {ex.Message}");
            tcs.TrySetResult(false);
        }

        // Timeout damit der await nicht ewig haengt falls Callback nie feuert
        var timeoutTask = Task.Delay(LoadTimeoutMs);
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
            Android.Util.Log.Error(Tag, $"LoadAndShowAsync TIMEOUT nach {LoadTimeoutMs}ms fuer: {adUnitId}");
            tcs.TrySetResult(false);
            return false;
        }

        return await tcs.Task;
    }

    private void OnAdLoaded(RewardedAd ad)
    {
        Android.Util.Log.Info(Tag, "Pre-Load: Rewarded Ad erfolgreich geladen und C# Callback erreicht");
        _rewardedAd = ad;
        _isLoading = false;
        _retryCount = 0; // Retry-Zaehler zuruecksetzen bei Erfolg
    }

    private void OnAdFailedToLoad(LoadAdError error)
    {
        Android.Util.Log.Error(Tag, $"Pre-Load FEHLGESCHLAGEN: Code={error.Code}, Message={error.Message}, Domain={error.Domain}");
        _rewardedAd = null;
        _isLoading = false;
        ScheduleRetry();
    }

    /// <summary>
    /// Retry mit exponentiellem Backoff (5s, 15s, 30s).
    /// Wird nach fehlgeschlagenem Pre-Load aufgerufen.
    /// </summary>
    private async void ScheduleRetry()
    {
        if (_retryCount >= MaxRetryAttempts || _disposed || string.IsNullOrEmpty(_defaultAdUnitId))
        {
            Android.Util.Log.Warn(Tag, $"Retry abgebrochen: retryCount={_retryCount}/{MaxRetryAttempts}, disposed={_disposed}");
            return;
        }

        var delayMs = RetryDelaysMs[_retryCount];
        _retryCount++;
        Android.Util.Log.Info(Tag, $"Retry {_retryCount}/{MaxRetryAttempts} in {delayMs}ms");

        try
        {
            await Task.Delay(delayMs);
            if (!_disposed && _rewardedAd == null)
                LoadInternal(_defaultAdUnitId);
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(Tag, $"ScheduleRetry Exception: {ex.Message}");
        }
    }

    private void OnAdDismissed()
    {
        // Nach dem Schliessen neue Default-Ad laden fuer naechste Nutzung
        Android.Util.Log.Info(Tag, "Ad geschlossen, lade naechste Default-Ad");
        _rewardedAd = null;
        _retryCount = 0; // Retry-Zaehler zuruecksetzen fuer frischen Ladeversuch
        if (!string.IsNullOrEmpty(_defaultAdUnitId))
            LoadInternal(_defaultAdUnitId);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rewardedAd = null;
        _activity = null;
    }

    // ==================================================================================
    // JNI Workaround fuer Xamarin Binding Bug (OnAdLoaded wird sonst nie aufgerufen)
    // https://github.com/xamarin/GooglePlayServicesComponents/issues/425
    // https://gist.github.com/dtaylorus/63fef408cec34999a1e566bd5fac27e5
    //
    // Problem: RewardedAdLoadCallback erbt von AdLoadCallback<RewardedAd>.
    // Durch Java Generics Erasure wird onAdLoaded(RewardedAd) zu onAdLoaded(Object).
    // Die Xamarin-Binding generiert nur OnAdLoaded(Java.Lang.Object), aber der
    // [Register]-Attribut mit leerem Connector ("") verdrahtet keinen JNI Native Delegate.
    // Resultat: Java ruft onAdLoaded auf, C# bekommt den Aufruf nie.
    //
    // Fix: Eigene Basis-Klasse mit korrektem JNI Delegate via GetOnAdLoadedHandler.
    // ==================================================================================

    /// <summary>
    /// Basis-Klasse mit korrekt verdrahtetem OnAdLoaded JNI-Callback.
    /// Alle Rewarded-Callbacks muessen hiervon erben statt direkt von RewardedAdLoadCallback.
    /// </summary>
    private abstract class FixedRewardedAdLoadCallback : RewardedAdLoadCallback
    {
        private static Delegate? _cbOnAdLoaded;

        private static Delegate GetOnAdLoadedHandler()
        {
            _cbOnAdLoaded ??= JNINativeWrapper.CreateDelegate(
                (Action<IntPtr, IntPtr, IntPtr>)n_OnAdLoaded);
            return _cbOnAdLoaded;
        }

        private static void n_OnAdLoaded(IntPtr jnienv, IntPtr native_this, IntPtr native_p0)
        {
            var callback = GetObject<FixedRewardedAdLoadCallback>(jnienv, native_this, JniHandleOwnership.DoNotTransfer);
            var rewardedAd = GetObject<RewardedAd>(native_p0, JniHandleOwnership.DoNotTransfer);
            if (callback != null && rewardedAd != null)
            {
                Android.Util.Log.Info(Tag, "JNI: OnAdLoaded Callback korrekt empfangen");
                callback.OnRewardedAdLoaded(rewardedAd);
            }
            else
            {
                Android.Util.Log.Error(Tag, $"JNI: OnAdLoaded Marshalling fehlgeschlagen - callback={callback != null}, ad={rewardedAd != null}");
            }
        }

        [Register("onAdLoaded", "(Lcom/google/android/gms/ads/rewarded/RewardedAd;)V", "GetOnAdLoadedHandler")]
        public virtual void OnRewardedAdLoaded(RewardedAd rewardedAd) { }
    }

    /// <summary>Callback fuer Pre-Load Ad-Ladevorgang (Default-Placement)</summary>
    private sealed class LoadCallback : FixedRewardedAdLoadCallback
    {
        private readonly RewardedAdHelper _helper;

        public LoadCallback(RewardedAdHelper helper) => _helper = helper;

        public override void OnRewardedAdLoaded(RewardedAd ad) => _helper.OnAdLoaded(ad);

        public override void OnAdFailedToLoad(LoadAdError error) => _helper.OnAdFailedToLoad(error);
    }

    /// <summary>Callback fuer On-Demand Load+Show (laedt Ad und zeigt sie sofort)</summary>
    private sealed class OnDemandLoadCallback : FixedRewardedAdLoadCallback
    {
        private readonly RewardedAdHelper _helper;
        private readonly Activity _activity;
        private readonly TaskCompletionSource<bool> _tcs;

        public OnDemandLoadCallback(RewardedAdHelper helper, Activity activity, TaskCompletionSource<bool> tcs)
        {
            _helper = helper;
            _activity = activity;
            _tcs = tcs;
        }

        public override void OnRewardedAdLoaded(RewardedAd ad)
        {
            Android.Util.Log.Info(Tag, "On-Demand: Rewarded Ad geladen, zeige sofort");
            // Ad geladen → sofort zeigen
            var fullScreenCallback = new FullScreenCallback(_tcs, null);
            var rewardCallback = new RewardCallback(fullScreenCallback);
            _activity.RunOnUiThread(() =>
            {
                try
                {
                    ad.FullScreenContentCallback = fullScreenCallback;
                    ad.Show(_activity, rewardCallback);
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Error(Tag, $"On-Demand Show Exception: {ex.Message}");
                    _tcs.TrySetResult(false);
                }
            });
        }

        public override void OnAdFailedToLoad(LoadAdError error)
        {
            Android.Util.Log.Error(Tag, $"On-Demand Load FEHLGESCHLAGEN: Code={error.Code}, Message={error.Message}, Domain={error.Domain}");
            _tcs.TrySetResult(false);
        }
    }

    /// <summary>
    /// Separater Callback fuer FullScreenContent (Ad-Anzeige-Lifecycle).
    /// GETRENNT von IOnUserEarnedRewardListener um ACW-Probleme bei Dual-Inheritance zu vermeiden.
    /// </summary>
    private sealed class FullScreenCallback : FullScreenContentCallback
    {
        private readonly TaskCompletionSource<bool> _tcs;
        private readonly RewardedAdHelper? _helper;
        internal volatile bool Rewarded;

        public FullScreenCallback(TaskCompletionSource<bool> tcs, RewardedAdHelper? helper)
        {
            _tcs = tcs;
            _helper = helper;
        }

        public override void OnAdShowedFullScreenContent()
        {
            Android.Util.Log.Info(Tag, "Rewarded Ad wird angezeigt (Fullscreen)");
        }

        public override void OnAdDismissedFullScreenContent()
        {
            Android.Util.Log.Info(Tag, $"Rewarded Ad geschlossen, Belohnung verdient: {Rewarded}");
            _tcs.TrySetResult(Rewarded);
            _helper?.OnAdDismissed();
        }

        public override void OnAdFailedToShowFullScreenContent(AdError error)
        {
            Android.Util.Log.Error(Tag, $"Rewarded Ad Show FEHLGESCHLAGEN: Code={error.Code}, Message={error.Message}, Domain={error.Domain}");
            _tcs.TrySetResult(false);
        }
    }

    /// <summary>
    /// Separater Callback fuer Belohnung (IOnUserEarnedRewardListener).
    /// Eigene Klasse statt Dual-Inheritance auf FullScreenContentCallback.
    /// </summary>
    private sealed class RewardCallback : Java.Lang.Object, IOnUserEarnedRewardListener
    {
        private readonly FullScreenCallback _fullScreenCallback;

        public RewardCallback(FullScreenCallback fullScreenCallback) => _fullScreenCallback = fullScreenCallback;

        public void OnUserEarnedReward(IRewardItem reward)
        {
            Android.Util.Log.Info(Tag, $"Belohnung verdient: Type={reward.Type}, Amount={reward.Amount}");
            _fullScreenCallback.Rewarded = true;
        }
    }
}
