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

        // Placement-spezifische Ad-Unit-ID aus AdConfig holen
        var adUnitId = AdConfig.GetRewardedAdUnitId(_appName, placement);
        Android.Util.Log.Info(Tag, $"ShowAdAsync ({placement}): AdUnitId={adUnitId}, App={_appName}");

        // On-Demand laden und zeigen (eigene Ad-Unit-ID pro Placement)
        var result = await _helper.LoadAndShowAsync(adUnitId);
        Android.Util.Log.Info(Tag, $"ShowAdAsync ({placement}): Ergebnis={result}");
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
