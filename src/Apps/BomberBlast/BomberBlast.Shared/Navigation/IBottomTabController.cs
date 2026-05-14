namespace BomberBlast.Navigation;

/// <summary>
/// Verwaltet den State der Bottom-Tab-Bar (.1 : Home / Play / Shop / Profile,
/// plus die 5 horizontalen Sub-Tabs (Shop/Spin, Profile/Achievements, Settings/Help,
/// Cards/Collection, Challenges/Missions).
///
/// <para>
/// Bindet bidirektional an <see cref="BomberBlast.Services.IBottomTabHub"/> und an die
/// <see cref="INavigationCoordinator"/> — Tab-Klick triggert Navigation, Navigation triggert
/// passenden Tab-Reset. Alle Tab-Bools bleiben in MainViewModel als Forwarder verfuegbar
/// fuer die bestehenden AXAML-Bindings.
/// </para>
/// </summary>
public interface IBottomTabController
{
    bool IsShopSpinTab { get; set; }
    bool IsProfileAchievementsTab { get; set; }
    bool IsSettingsHelpTab { get; set; }
    bool IsCardsCollectionTab { get; set; }
    bool IsChallengesMissionsTab { get; set; }
    bool IsBottomTabBarVisible { get; }

    /// <summary>Wird gefeuert wenn irgendein Tab-State sich aendert (pauschal — MainVM raised alle Properties neu).</summary>
    event Action? StateChanged;

    void SwitchToShopTab();
    void SwitchToSpinTab();
    void SwitchToProfileTab();
    void SwitchToAchievementsTab();
    void SwitchToSettingsTab();
    void SwitchToHelpTab();
    void SwitchToDeckTab();
    void SwitchToCollectionTab();
    void SwitchToDailyChallengeTab();
    void SwitchToMissionsTab();

    /// <summary>Setzt alle Tab-Bools auf false (Reset). Wird von HideAll-Pfaden gerufen.</summary>
    void ResetTabStates();
}
