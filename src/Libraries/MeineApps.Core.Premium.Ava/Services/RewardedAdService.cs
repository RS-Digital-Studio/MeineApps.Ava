namespace MeineApps.Core.Premium.Ava.Services;

/// <summary>
/// Desktop-Simulator fuer Rewarded Ads.
/// Auf Android wird diese Klasse durch AndroidRewardedAdService ersetzt (DI Override in MainActivity).
/// </summary>
public sealed class RewardedAdService : IRewardedAdService
{
    private readonly IPurchaseService _purchaseService;
    private bool _isDisabled;

    public RewardedAdService(IPurchaseService purchaseService)
    {
        _purchaseService = purchaseService;
    }

    public bool IsAvailable => !_isDisabled && !_purchaseService.IsPremium;

    public async Task<bool> ShowAdAsync()
    {
        if (!IsAvailable) return false;
        // Desktop: Simuliert 1 Sekunde "Ad schauen"
        await Task.Delay(1000);
        return true;
    }

    public Task<bool> ShowAdAsync(string placement)
    {
        // Desktop: Placement wird ignoriert, Verhalten identisch
        return ShowAdAsync();
    }

    public void Disable()
    {
        _isDisabled = true;
    }

    public event Action? AdUnavailable;
}
