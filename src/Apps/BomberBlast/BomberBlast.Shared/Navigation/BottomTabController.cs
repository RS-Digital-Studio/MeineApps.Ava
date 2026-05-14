namespace BomberBlast.Navigation;

/// <summary>
/// Default-Implementation von <see cref="IBottomTabController"/> (Welle 6 MainViewModel-Refactor).
///
/// <para>
/// : Leeres Geruest mit minimalem State (alle Bools = false). Die Logik wird in 
/// (BottomTabController-Migration) aus <see cref="BomberBlast.ViewModels.MainViewModel"/> hier
/// hin verschoben.
/// </para>
/// </summary>
public sealed class BottomTabController : IBottomTabController
{
    private bool _isShopSpinTab;
    private bool _isProfileAchievementsTab;
    private bool _isSettingsHelpTab;
    private bool _isCardsCollectionTab;
    private bool _isChallengesMissionsTab;

    public event Action? StateChanged;

    public bool IsShopSpinTab { get => _isShopSpinTab; set { _isShopSpinTab = value; StateChanged?.Invoke(); } }
    public bool IsProfileAchievementsTab { get => _isProfileAchievementsTab; set { _isProfileAchievementsTab = value; StateChanged?.Invoke(); } }
    public bool IsSettingsHelpTab { get => _isSettingsHelpTab; set { _isSettingsHelpTab = value; StateChanged?.Invoke(); } }
    public bool IsCardsCollectionTab { get => _isCardsCollectionTab; set { _isCardsCollectionTab = value; StateChanged?.Invoke(); } }
    public bool IsChallengesMissionsTab { get => _isChallengesMissionsTab; set { _isChallengesMissionsTab = value; StateChanged?.Invoke(); } }

    public bool IsBottomTabBarVisible
        => throw new NotImplementedException("Wird in  gefuellt.");

    public void SwitchToShopTab() => throw new NotImplementedException(".");
    public void SwitchToSpinTab() => throw new NotImplementedException(".");
    public void SwitchToProfileTab() => throw new NotImplementedException(".");
    public void SwitchToAchievementsTab() => throw new NotImplementedException(".");
    public void SwitchToSettingsTab() => throw new NotImplementedException(".");
    public void SwitchToHelpTab() => throw new NotImplementedException(".");
    public void SwitchToDeckTab() => throw new NotImplementedException(".");
    public void SwitchToCollectionTab() => throw new NotImplementedException(".");
    public void SwitchToDailyChallengeTab() => throw new NotImplementedException(".");
    public void SwitchToMissionsTab() => throw new NotImplementedException(".");

    public void ResetTabStates()
    {
        _isShopSpinTab = false;
        _isProfileAchievementsTab = false;
        _isSettingsHelpTab = false;
        _isCardsCollectionTab = false;
        _isChallengesMissionsTab = false;
        StateChanged?.Invoke();
    }
}
