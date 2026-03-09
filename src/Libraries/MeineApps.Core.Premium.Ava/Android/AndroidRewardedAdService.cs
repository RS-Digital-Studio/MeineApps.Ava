using MeineApps.Core.Premium.Ava.Services;

namespace MeineApps.Core.Premium.Ava.Droid;

/// <summary>
/// Android-Implementierung von IRewardedAdService mit echten AdMob Rewarded Ads.
/// Linked File - wird per Compile Include in Android-Projekte eingebunden.
/// Ersetzt den Desktop-RewardedAdService per DI Override in MainActivity.
/// Unterstuetzt Multi-Placement: verschiedene Ad-Unit-IDs pro Feature.
/// </summary>
public sealed class AndroidRewardedAdService : IRewardedAdService
{
    private const string Tag = "AndroidRewardedAdService";

    private readonly RewardedAdHelper _helper;
    private readonly IPurchaseService _purchaseService;
    private readonly string _appName;
    private bool _isDisabled;

    public event Action? AdUnavailable;

    /// <param name="helper">RewardedAdHelper Instanz (wird in MainActivity erstellt)</param>
    /// <param name="purchaseService">Fuer Premium-Check</param>
    /// <param name="appName">App-Name fuer AdConfig Lookup (z.B. "BomberBlast")</param>
    public AndroidRewardedAdService(RewardedAdHelper helper, IPurchaseService purchaseService, string appName)
    {
        _helper = helper;
        _purchaseService = purchaseService;
        _appName = appName;
        Android.Util.Log.Info(Tag, $"Erstellt fuer App: {appName}, Premium: {purchaseService.IsPremium}");
    }

    public bool IsAvailable => !_isDisabled && !_purchaseService.IsPremium;

    public async Task<bool> ShowAdAsync()
    {
        if (!IsAvailable)
        {
            Android.Util.Log.Warn(Tag, $"ShowAdAsync (default): Nicht verfuegbar - disabled={_isDisabled}, premium={_purchaseService.IsPremium}");
            return false;
        }

        // Default-Placement: Vorgeladene Ad zeigen
        if (!_helper.IsLoaded)
        {
            Android.Util.Log.Info(Tag, "ShowAdAsync (default): Ad noch nicht geladen, warte 2s...");
            await Task.Delay(2000);
            if (!_helper.IsLoaded)
            {
                Android.Util.Log.Warn(Tag, "ShowAdAsync (default): Ad nach 2s immer noch nicht geladen");
                AdUnavailable?.Invoke();
                return false;
            }
        }

        Android.Util.Log.Info(Tag, "ShowAdAsync (default): Zeige vorgeladene Ad");
        var result = await _helper.ShowAsync();
        if (!result)
            AdUnavailable?.Invoke();
        return result;
    }

    public async Task<bool> ShowAdAsync(string placement)
    {
        if (!IsAvailable)
        {
            Android.Util.Log.Warn(Tag, $"ShowAdAsync ({placement}): Nicht verfuegbar - disabled={_isDisabled}, premium={_purchaseService.IsPremium}");
            return false;
        }

        // 1. Placement-spezifische vorgeladene Ad verwenden (beste Attribution + sofort)
        if (_helper.IsPlacementLoaded(placement))
        {
            Android.Util.Log.Info(Tag, $"ShowAdAsync ({placement}): Placement-spezifische Pre-Load-Ad verfuegbar");
            var placementResult = await _helper.ShowPlacementAdAsync(placement);
            Android.Util.Log.Info(Tag, $"ShowAdAsync ({placement}): Ergebnis (placement-preloaded)={placementResult}");
            if (!placementResult)
                AdUnavailable?.Invoke();
            return placementResult;
        }

        // 2. Default vorgeladene Ad verwenden (sofortige Anzeige, Default-Attribution)
        if (_helper.IsLoaded)
        {
            Android.Util.Log.Info(Tag, $"ShowAdAsync ({placement}): Default Pre-Load-Ad verfuegbar, zeige sofort");
            var preloadedResult = await _helper.ShowAsync();
            Android.Util.Log.Info(Tag, $"ShowAdAsync ({placement}): Ergebnis (default-preloaded)={preloadedResult}");
            if (!preloadedResult)
                AdUnavailable?.Invoke();
            return preloadedResult;
        }

        // 3. Fallback: Placement-spezifisch on-demand laden (2-8s Verzoegerung)
        var adUnitId = AdConfig.GetRewardedAdUnitId(_appName, placement);
        Android.Util.Log.Info(Tag, $"ShowAdAsync ({placement}): Kein Pre-Load verfuegbar, lade on-demand: {adUnitId}");

        var result = await _helper.LoadAndShowAsync(adUnitId);
        Android.Util.Log.Info(Tag, $"ShowAdAsync ({placement}): Ergebnis (on-demand)={result}");
        if (!result)
            AdUnavailable?.Invoke();
        return result;
    }

    public void Disable()
    {
        Android.Util.Log.Info(Tag, "Rewarded Ads deaktiviert");
        _isDisabled = true;
    }
}
