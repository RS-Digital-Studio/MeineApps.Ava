using MeineApps.Core.Ava.Services;

namespace MeineApps.Core.Premium.Ava.Services;

/// <summary>
/// AdMob implementation of the ad service.
/// Manages ad state via IPreferencesService.
/// Actual AdMob integration must be done in platform-specific code.
/// </summary>
public sealed class AdMobService : IAdService
{
    private const string AdsDisabledKey = "ads_disabled";
    private readonly IPreferencesService _preferences;
    private string? _bannerId;
    private bool _adsEnabled = true;
    private bool _bannerVisible;
    private bool _isBannerTop;

    public bool AdsEnabled => _adsEnabled;
    public bool BannerVisible => _bannerVisible;
    public bool IsBannerTop => _isBannerTop;

    public event EventHandler? AdsStateChanged;

    public AdMobService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _adsEnabled = !_preferences.Get(AdsDisabledKey, false);
    }

    public void Initialize(string bannerId)
    {
        _bannerId = bannerId;
    }

    public void ShowBanner()
    {
        if (!_adsEnabled) return;

        _bannerVisible = true;
        AdsStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void HideBanner()
    {
        _bannerVisible = false;
        AdsStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetBannerPosition(bool top)
    {
        if (_isBannerTop == top) return;
        _isBannerTop = top;
        AdsStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DisableAds()
    {
        _adsEnabled = false;
        _bannerVisible = false;
        _preferences.Set(AdsDisabledKey, true);
        AdsStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
