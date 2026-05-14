namespace BomberBlast.Navigation;

/// <summary>
/// Default-Implementation von <see cref="IBottomTabController"/>.
///
/// <para>
/// Aktuell minimaler State (alle Bools = false, ResetTabStates funktional) — die
/// SwitchToXxxTab-Methoden und IsBottomTabBarVisible werden noch aus
/// <see cref="BomberBlast.ViewModels.MainViewModel"/> hier hin verschoben.
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
        => throw new NotImplementedException("Wird noch aus MainViewModel migriert.");

    public void SwitchToShopTab() => throw new NotImplementedException("Migration ausstehend.");
    public void SwitchToSpinTab() => throw new NotImplementedException("Migration ausstehend.");
    public void SwitchToProfileTab() => throw new NotImplementedException("Migration ausstehend.");
    public void SwitchToAchievementsTab() => throw new NotImplementedException("Migration ausstehend.");
    public void SwitchToSettingsTab() => throw new NotImplementedException("Migration ausstehend.");
    public void SwitchToHelpTab() => throw new NotImplementedException("Migration ausstehend.");
    public void SwitchToDeckTab() => throw new NotImplementedException("Migration ausstehend.");
    public void SwitchToCollectionTab() => throw new NotImplementedException("Migration ausstehend.");
    public void SwitchToDailyChallengeTab() => throw new NotImplementedException("Migration ausstehend.");
    public void SwitchToMissionsTab() => throw new NotImplementedException("Migration ausstehend.");

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
