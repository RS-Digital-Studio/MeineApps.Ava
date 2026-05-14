using BomberBlast.Services;
using BomberBlast.ViewModels;

namespace BomberBlast.Navigation;

/// <summary>
/// Default-Implementation von <see cref="IBottomTabController"/>.
///
/// <para>
/// Haelt:
/// </para>
/// <list type="bullet">
/// <item>5 Sub-Tab-Bools fuer kombinierte Views (Shop/Spin, Profile/Achievements,
///       Settings/Help, Cards/Collection, Challenges/Missions).</item>
/// <item>IsBottomTabBarVisible — true nur fuer die 4 Haupt-Tabs (Home/Play/Shop/Profile).</item>
/// <item>10 SwitchToXxxTab-Methoden — setzen den jeweiligen Sub-Tab-Bool und rufen
///       die OnAppearing-Methode des zugehoerigen VMs ueber die Registry.</item>
/// <item>OnBottomTabChanged-Handler — uebersetzt einen <see cref="BottomTab"/>-Wechsel in eine
///       NavigationRequest (idempotent: kein Re-Navigate wenn die View schon zum Tab gehoert).</item>
/// </list>
///
/// <para>
/// Die Quelle der Wahrheit fuer <see cref="ActiveView"/> bleibt bei MainViewModel — diese ruft
/// <see cref="OnActiveViewChanged"/> bei jedem View-Wechsel und der Controller sync-t Hub-State +
/// IsBottomTabBarVisible.
/// </para>
/// </summary>
public sealed class BottomTabController : IBottomTabController
{
    private readonly IBottomTabHub _hub;
    private readonly IChildViewModelRegistry _registry;
    private readonly Action<NavigationRequest> _navigate;

    private bool _isShopSpinTab;
    private bool _isProfileAchievementsTab;
    private bool _isSettingsHelpTab;
    private bool _isCardsCollectionTab;
    private bool _isChallengesMissionsTab;
    private bool _isBottomTabBarVisible;

    public event Action? StateChanged;

    public BottomTabController(
        IBottomTabHub hub,
        IChildViewModelRegistry registry,
        Action<NavigationRequest> navigate)
    {
        _hub = hub;
        _registry = registry;
        _navigate = navigate;
        _hub.ActiveTabChanged += OnHubActiveTabChanged;
    }

    public bool IsShopSpinTab
    {
        get => _isShopSpinTab;
        set { if (_isShopSpinTab == value) return; _isShopSpinTab = value; StateChanged?.Invoke(); }
    }

    public bool IsProfileAchievementsTab
    {
        get => _isProfileAchievementsTab;
        set { if (_isProfileAchievementsTab == value) return; _isProfileAchievementsTab = value; StateChanged?.Invoke(); }
    }

    public bool IsSettingsHelpTab
    {
        get => _isSettingsHelpTab;
        set { if (_isSettingsHelpTab == value) return; _isSettingsHelpTab = value; StateChanged?.Invoke(); }
    }

    public bool IsCardsCollectionTab
    {
        get => _isCardsCollectionTab;
        set { if (_isCardsCollectionTab == value) return; _isCardsCollectionTab = value; StateChanged?.Invoke(); }
    }

    public bool IsChallengesMissionsTab
    {
        get => _isChallengesMissionsTab;
        set { if (_isChallengesMissionsTab == value) return; _isChallengesMissionsTab = value; StateChanged?.Invoke(); }
    }

    public bool IsBottomTabBarVisible => _isBottomTabBarVisible;

    /// <summary>
    /// Wird vom MainViewModel.partial OnActiveViewChanged gerufen. Synchronisiert den Hub
    /// (Tab-Highlight bleibt korrekt egal ob Navigation ueber Tab-Bar oder MainMenu-Button kam)
    /// und berechnet die Tab-Bar-Sichtbarkeit (true nur auf den 4 Haupt-Tabs).
    /// </summary>
    public void OnActiveViewChanged(ActiveView view)
    {
        var visible = view is ActiveView.MainMenu or ActiveView.PlayHub
            or ActiveView.Shop or ActiveView.Profile;
        if (visible != _isBottomTabBarVisible)
        {
            _isBottomTabBarVisible = visible;
            StateChanged?.Invoke();
        }

        var tab = TabForActiveView(view);
        if (tab.HasValue)
            _hub.SetActiveTab(tab.Value);
    }

    // ─── SwitchToXxxTab-Methoden (10 Stueck) ────────────────────────────────
    // Setzen den Sub-Tab-Bool und rufen OnAppearing am zugehoerigen VM ueber die Registry.
    // EnsureXxx() instanziiert den Lazy-VM beim ersten Aufruf.

    public void SwitchToShopTab()
    {
        IsShopSpinTab = false;
        _registry.EnsureShop().OnAppearing();
    }

    public void SwitchToSpinTab()
    {
        IsShopSpinTab = true;
        _registry.EnsureLuckySpin().OnAppearing();
    }

    public void SwitchToProfileTab()
    {
        IsProfileAchievementsTab = false;
        _registry.EnsureProfile().OnAppearing();
    }

    public void SwitchToAchievementsTab()
    {
        IsProfileAchievementsTab = true;
        _registry.EnsureAchievements().OnAppearing();
    }

    public void SwitchToSettingsTab()
    {
        IsSettingsHelpTab = false;
        _registry.SettingsVm.OnAppearing();
    }

    public void SwitchToHelpTab()
    {
        IsSettingsHelpTab = true;
        // HelpVm hat keine OnAppearing-Implementierung (XAML-only).
    }

    public void SwitchToDeckTab()
    {
        IsCardsCollectionTab = false;
        _registry.EnsureDeck().OnAppearing();
    }

    public void SwitchToCollectionTab()
    {
        IsCardsCollectionTab = true;
        _registry.EnsureCollection().OnAppearing();
    }

    public void SwitchToDailyChallengeTab()
    {
        IsChallengesMissionsTab = false;
        _registry.EnsureDailyChallenge().OnAppearing();
    }

    public void SwitchToMissionsTab()
    {
        IsChallengesMissionsTab = true;
        _registry.EnsureWeeklyChallenge().OnAppearing();
    }

    public void ResetTabStates()
    {
        var changed =
            _isShopSpinTab || _isProfileAchievementsTab || _isSettingsHelpTab
            || _isCardsCollectionTab || _isChallengesMissionsTab;
        _isShopSpinTab = false;
        _isProfileAchievementsTab = false;
        _isSettingsHelpTab = false;
        _isCardsCollectionTab = false;
        _isChallengesMissionsTab = false;
        if (changed) StateChanged?.Invoke();
    }

    /// <summary>
    /// Reagiert auf Tab-Wechsel via Bottom-Tab-Bar. Idempotent: wenn die aktuelle View
    /// schon zum Tab gehoert, passiert nichts (vermeidet Re-Navigation bei der
    /// bidirektionalen Sync ActiveView → Tab → ActiveView).
    /// </summary>
    private void OnHubActiveTabChanged(BottomTab tab)
    {
        // ActiveView-Check fehlt: Wir kennen die aktuelle View hier nicht — der idempotente
        // Schutz greift bereits im NavigationCoordinator (Re-Navigate auf dieselbe Route ist No-Op).
        switch (tab)
        {
            case BottomTab.Home: _navigate(new GoMainMenu()); break;
            case BottomTab.Play: _navigate(new GoPlayHub()); break;
            case BottomTab.Shop: _navigate(new GoShop()); break;
            case BottomTab.Profile: _navigate(new GoProfile()); break;
        }
    }

    /// <summary>Mappt eine ActiveView auf ihren Bottom-Tab — null wenn die View zu keinem Tab gehoert.</summary>
    public static BottomTab? TabForActiveView(ActiveView view) => view switch
    {
        ActiveView.MainMenu => BottomTab.Home,
        ActiveView.PlayHub => BottomTab.Play,
        ActiveView.Shop => BottomTab.Shop,
        ActiveView.Profile => BottomTab.Profile,
        _ => null,
    };
}
