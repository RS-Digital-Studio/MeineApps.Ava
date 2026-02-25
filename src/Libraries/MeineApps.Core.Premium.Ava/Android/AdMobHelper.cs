using Android.App;
using Android.Gms.Ads;
using Android.Views;
using Android.Widget;
using MeineApps.Core.Premium.Ava.Services;
using Xamarin.Google.UserMesssagingPlatform;

namespace MeineApps.Core.Premium.Ava.Droid;

/// <summary>
/// Helper for Google AdMob banner ads on Android.
/// This file is linked from all ad-supported Android projects via Compile Include.
/// It is NOT compiled as part of the MeineApps.Core.Premium.Ava (net10.0) library.
/// </summary>
public sealed class AdMobHelper : IDisposable
{
    private AdView? _adView;
    private IAdService? _adService;
    private IPurchaseService? _purchaseService;
    private Activity? _activity;
    private int _baseBottomMarginPx;
    private bool _disposed;

    /// <summary>
    /// Initialize Google Mobile Ads SDK mit Callback.
    /// Ads duerfen erst nach onComplete geladen werden, da das SDK intern asynchron initialisiert.
    /// Ohne Callback: "Failed to instantiate ClientApi class" + Error Code 0.
    /// </summary>
    public static void Initialize(Activity activity, Action? onComplete = null)
    {
        try
        {
#if DEBUG
            // Test-Device registrieren damit Test-Ads ausgeliefert werden (nur Debug)
            var requestConfig = new RequestConfiguration.Builder()
                .SetTestDeviceIds(new[] { AdConfig.TestDeviceId })
                .Build();
            MobileAds.RequestConfiguration = requestConfig;
#endif

            MobileAds.Initialize(activity, new InitCompleteListener(() =>
            {
                Android.Util.Log.Info("AdMobHelper", "SDK Initialisierung abgeschlossen");
                // Sicherstellen dass Callback auf UI-Thread laeuft
                activity.RunOnUiThread(() => onComplete?.Invoke());
            }));
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("AdMobHelper", $"Initialize failed: {ex.Message}");
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// Request GDPR consent via UMP (User Messaging Platform).
    /// Shows consent form if required for EU users.
    /// Call after MobileAds.Initialize.
    /// onComplete wird aufgerufen wenn Consent-Flow abgeschlossen (oder bei Fehler).
    /// </summary>
    public static void RequestConsent(Activity activity, Action? onComplete = null)
    {
        try
        {
            var consentInfo = UserMessagingPlatform.GetConsentInformation(activity);
            var requestParams = new ConsentRequestParameters.Builder()
                .SetTagForUnderAgeOfConsent(false)
                .Build();

            consentInfo.RequestConsentInfoUpdate(
                activity,
                requestParams,
                new ConsentSuccessListener(() =>
                {
                    if (consentInfo.IsConsentFormAvailable)
                    {
                        UserMessagingPlatform.LoadAndShowConsentFormIfRequired(
                            activity,
                            new ConsentFormDismissedListener(error =>
                            {
                                if (error != null)
                                    Android.Util.Log.Warn("AdMobHelper", $"Consent form dismissed with error: {error.Message}");
                                onComplete?.Invoke();
                            }));
                    }
                    else
                    {
                        // Kein Formular noetig (z.B. ausserhalb EU)
                        onComplete?.Invoke();
                    }
                }),
                new ConsentFailureListener(error =>
                {
                    Android.Util.Log.Error("AdMobHelper", $"Consent info update failed: {error?.Message}");
                    // Auch bei Fehler Ads versuchen zu laden
                    onComplete?.Invoke();
                }));
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("AdMobHelper", $"RequestConsent failed: {ex.Message}");
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// Attach a banner ad as a FrameLayout overlay above the Avalonia tab bar.
    /// For apps with bottom tabs, pass the tab bar height in dp so the ad sits above it.
    /// For apps without tabs (e.g. BomberBlast), pass 0 for ad at the very bottom.
    /// </summary>
    public void AttachToActivity(Activity activity, string adUnitId, IAdService adService,
        IPurchaseService purchaseService, int tabBarHeightDp = 0)
    {
        _adService = adService;
        _purchaseService = purchaseService;
        _activity = activity;

        // Subscribe to premium/ad status changes
        _adService.AdsStateChanged += OnAdsStateChanged;
        _purchaseService.PremiumStatusChanged += OnPremiumStatusChanged;

        // Don't create ad view for premium users
        if (_purchaseService.IsPremium || !_adService.AdsEnabled)
            return;

        try
        {
            var contentParent = activity.FindViewById<FrameLayout>(global::Android.Resource.Id.Content);
            if (contentParent == null) return;

            // Banner ad als Overlay ueber der AvaloniaView (volle Breite, adaptive Hoehe)
            _adView = new AdView(activity);
            _adView.AdUnitId = adUnitId;

            // Adaptiver Banner: Volle Bildschirmbreite statt fester 320x50dp
            // Vermeidet schwarze Streifen links/rechts auf breiten Geraeten
            var displayMetrics = activity.Resources?.DisplayMetrics;
            if (displayMetrics != null)
            {
                var widthDp = (int)(displayMetrics.WidthPixels / displayMetrics.Density);
                _adView.AdSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSize(activity, widthDp);
            }
            else
            {
                _adView.AdSize = AdSize.Banner;
            }

            var density = activity.Resources?.DisplayMetrics?.Density ?? 1f;
            _baseBottomMarginPx = (int)(tabBarHeightDp * density);

            var adParams = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent,
                GravityFlags.Bottom | GravityFlags.CenterHorizontal);
            adParams.BottomMargin = _baseBottomMarginPx;

            contentParent.AddView(_adView, adParams);

            // Adjust bottom margin for navigation bar insets (edge-to-edge)
            try
            {
                AndroidX.Core.View.ViewCompat.SetOnApplyWindowInsetsListener(_adView,
                    new AdInsetListener(_baseBottomMarginPx));
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("AdMobHelper", $"Inset listener failed: {ex.Message}");
            }

            // Banner sofort laden - AdMob SDK handhabt Consent-Status intern
            _adView.LoadAd(new AdRequest.Builder().Build());

            // Banner-Sichtbarkeit dem aktuellen Service-Zustand anpassen
            // (App kann HideBanner() vor AttachToActivity gerufen haben, z.B. MainMenu ohne Banner)
            if (_adService.BannerVisible)
                _adView.Visibility = ViewStates.Visible;
            else
                _adView.Visibility = ViewStates.Gone;
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("AdMobHelper", $"AttachToActivity failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Laedt den Banner-Ad. Erst aufrufen wenn Consent abgeschlossen ist.
    /// </summary>
    public void LoadBannerAd()
    {
        try
        {
            _adView?.LoadAd(new AdRequest.Builder().Build());
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("AdMobHelper", $"LoadBannerAd failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Resume ad loading (call in Activity.OnResume).
    /// </summary>
    public void Resume() => _adView?.Resume();

    /// <summary>
    /// Pause ad loading (call in Activity.OnPause).
    /// </summary>
    public void Pause() => _adView?.Pause();

    private void OnAdsStateChanged(object? sender, EventArgs e)
    {
        if (_adService == null || _adView == null) return;

        _activity?.RunOnUiThread(() =>
        {
            if (_adView == null) return;

            if (!_adService.AdsEnabled)
            {
                _adView.Visibility = ViewStates.Gone;
                return;
            }

            // Banner sichtbar/unsichtbar schalten
            _adView.Visibility = _adService.BannerVisible
                ? ViewStates.Visible
                : ViewStates.Gone;

            // Position aktualisieren (Top/Bottom)
            UpdateBannerPosition(_adService.IsBannerTop);
        });
    }

    private void OnPremiumStatusChanged(object? sender, EventArgs e)
    {
        if (_purchaseService?.IsPremium == true)
        {
            _activity?.RunOnUiThread(() =>
            {
                if (_adView != null)
                    _adView.Visibility = ViewStates.Gone;
            });
        }
    }

    /// <summary>
    /// Banner-Position zwischen oben und unten wechseln.
    /// Top: Fuer Landscape-Games (Controls unten). Bottom: Standard (ueber Tab-Bar).
    /// </summary>
    private void UpdateBannerPosition(bool top)
    {
        if (_adView?.LayoutParameters is not FrameLayout.LayoutParams lp) return;

        if (top)
        {
            lp.Gravity = GravityFlags.Top | GravityFlags.CenterHorizontal;
            lp.BottomMargin = 0;
        }
        else
        {
            lp.Gravity = GravityFlags.Bottom | GravityFlags.CenterHorizontal;
            lp.BottomMargin = _baseBottomMarginPx;
        }

        _adView.LayoutParameters = lp;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_adService != null)
            _adService.AdsStateChanged -= OnAdsStateChanged;
        if (_purchaseService != null)
            _purchaseService.PremiumStatusChanged -= OnPremiumStatusChanged;

        _adView?.Destroy();
        _adView = null;
    }

    /// <summary>
    /// Adjusts AdView bottom margin to account for navigation bar insets (edge-to-edge).
    /// </summary>
    private sealed class AdInsetListener : Java.Lang.Object, AndroidX.Core.View.IOnApplyWindowInsetsListener
    {
        private readonly int _baseBottomMarginPx;

        public AdInsetListener(int baseBottomMarginPx) => _baseBottomMarginPx = baseBottomMarginPx;

        public AndroidX.Core.View.WindowInsetsCompat? OnApplyWindowInsets(
            View? v, AndroidX.Core.View.WindowInsetsCompat? insets)
        {
            if (v == null || insets == null) return insets;
            var navBars = insets.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.NavigationBars());
            if (navBars != null && v.LayoutParameters is FrameLayout.LayoutParams lp)
            {
                lp.BottomMargin = _baseBottomMarginPx + navBars.Bottom;
                v.LayoutParameters = lp;
            }
            return insets;
        }
    }

    // SDK Initialisierungs-Callback
    private sealed class InitCompleteListener : Java.Lang.Object,
        Android.Gms.Ads.Initialization.IOnInitializationCompleteListener
    {
        private readonly Action _onComplete;
        public InitCompleteListener(Action onComplete) => _onComplete = onComplete;
        public void OnInitializationComplete(Android.Gms.Ads.Initialization.IInitializationStatus status)
            => _onComplete();
    }

    // UMP callback wrappers
    private sealed class ConsentSuccessListener : Java.Lang.Object,
        IConsentInformationOnConsentInfoUpdateSuccessListener
    {
        private readonly Action _onSuccess;
        public ConsentSuccessListener(Action onSuccess) => _onSuccess = onSuccess;
        public void OnConsentInfoUpdateSuccess() => _onSuccess();
    }

    private sealed class ConsentFailureListener : Java.Lang.Object,
        IConsentInformationOnConsentInfoUpdateFailureListener
    {
        private readonly Action<FormError?> _onFailure;
        public ConsentFailureListener(Action<FormError?> onFailure) => _onFailure = onFailure;
        public void OnConsentInfoUpdateFailure(FormError? error) => _onFailure(error);
    }

    private sealed class ConsentFormDismissedListener : Java.Lang.Object,
        IConsentFormOnConsentFormDismissedListener
    {
        private readonly Action<FormError?> _onDismissed;
        public ConsentFormDismissedListener(Action<FormError?> onDismissed) => _onDismissed = onDismissed;
        public void OnConsentFormDismissed(FormError? error) => _onDismissed(error);
    }
}
