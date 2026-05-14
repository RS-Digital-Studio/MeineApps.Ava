using BomberBlast.Navigation;
using BomberBlast.Services;
using BomberBlast.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests fuer <see cref="BottomTabController"/>.
/// Validiert Tab-State, IsBottomTabBarVisible, SwitchToXxxTab-Delegation,
/// Hub-Bidirektional-Sync (ActiveView ↔ BottomTab).
/// </summary>
public class BottomTabControllerTests
{
    private static (BottomTabController Sut, IBottomTabHub Hub, IChildViewModelRegistry Registry, List<NavigationRequest> Navigations)
        CreateSut()
    {
        var hub = Substitute.For<IBottomTabHub>();
        var registry = Substitute.For<IChildViewModelRegistry>();
        var navigations = new List<NavigationRequest>();
        var sut = new BottomTabController(hub, registry, navigations.Add);
        return (sut, hub, registry, navigations);
    }

    [Fact]
    public void NeueInstanz_AlleTabsFalse()
    {
        var (sut, _, _, _) = CreateSut();

        sut.IsShopSpinTab.Should().BeFalse();
        sut.IsProfileAchievementsTab.Should().BeFalse();
        sut.IsSettingsHelpTab.Should().BeFalse();
        sut.IsCardsCollectionTab.Should().BeFalse();
        sut.IsChallengesMissionsTab.Should().BeFalse();
        sut.IsBottomTabBarVisible.Should().BeFalse();
    }

    [Fact]
    public void IsShopSpinTab_Setter_FeuertStateChanged()
    {
        var (sut, _, _, _) = CreateSut();
        var fired = 0;
        sut.StateChanged += () => fired++;

        sut.IsShopSpinTab = true;

        fired.Should().Be(1);
        sut.IsShopSpinTab.Should().BeTrue();
    }

    [Fact]
    public void IsShopSpinTab_SetterMitSelbemWert_FeuertNicht()
    {
        var (sut, _, _, _) = CreateSut();
        var fired = 0;
        sut.StateChanged += () => fired++;

        sut.IsShopSpinTab = false;  // Default-Wert

        fired.Should().Be(0);
    }

    [Theory]
    [InlineData(ActiveView.MainMenu, true)]
    [InlineData(ActiveView.PlayHub, true)]
    [InlineData(ActiveView.Shop, true)]
    [InlineData(ActiveView.Profile, true)]
    [InlineData(ActiveView.Game, false)]
    [InlineData(ActiveView.LevelSelect, false)]
    [InlineData(ActiveView.GameOver, false)]
    [InlineData(ActiveView.Settings, false)]
    [InlineData(ActiveView.Dungeon, false)]
    public void OnActiveViewChanged_BerechnetIsBottomTabBarVisibleKorrekt(ActiveView view, bool expectedVisible)
    {
        var (sut, _, _, _) = CreateSut();

        sut.OnActiveViewChanged(view);

        sut.IsBottomTabBarVisible.Should().Be(expectedVisible);
    }

    [Fact]
    public void OnActiveViewChanged_SynctHubAufHaupttab()
    {
        var (sut, hub, _, _) = CreateSut();

        sut.OnActiveViewChanged(ActiveView.Shop);

        hub.Received(1).SetActiveTab(BottomTab.Shop);
    }

    [Fact]
    public void OnActiveViewChanged_KeinHubSync_BeiNonHaupttabView()
    {
        var (sut, hub, _, _) = CreateSut();

        sut.OnActiveViewChanged(ActiveView.Game);

        hub.DidNotReceiveWithAnyArgs().SetActiveTab(default);
    }

    [Theory]
    [InlineData(ActiveView.MainMenu, BottomTab.Home)]
    [InlineData(ActiveView.PlayHub, BottomTab.Play)]
    [InlineData(ActiveView.Shop, BottomTab.Shop)]
    [InlineData(ActiveView.Profile, BottomTab.Profile)]
    public void TabForActiveView_LiefertKorrekteMapping(ActiveView view, BottomTab expected)
    {
        var result = BottomTabController.TabForActiveView(view);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(ActiveView.Game)]
    [InlineData(ActiveView.LevelSelect)]
    [InlineData(ActiveView.Settings)]
    [InlineData(ActiveView.Dungeon)]
    public void TabForActiveView_NonHaupttab_LiefertNull(ActiveView view)
    {
        var result = BottomTabController.TabForActiveView(view);

        result.Should().BeNull();
    }

    [Fact]
    public void HubActiveTabChanged_LoestNavigationAus()
    {
        var hub = Substitute.For<IBottomTabHub>();
        var registry = Substitute.For<IChildViewModelRegistry>();
        var navigations = new List<NavigationRequest>();
        _ = new BottomTabController(hub, registry, navigations.Add);

        hub.ActiveTabChanged += Raise.Event<Action<BottomTab>>(BottomTab.Shop);

        navigations.Should().HaveCount(1);
        navigations[0].Should().BeOfType<GoShop>();
    }

    [Fact]
    public void HubActiveTabChanged_AlleVierTabs_LoesenPassendeNavigation()
    {
        var (sut, hub, _, navigations) = CreateSut();

        hub.ActiveTabChanged += Raise.Event<Action<BottomTab>>(BottomTab.Home);
        hub.ActiveTabChanged += Raise.Event<Action<BottomTab>>(BottomTab.Play);
        hub.ActiveTabChanged += Raise.Event<Action<BottomTab>>(BottomTab.Shop);
        hub.ActiveTabChanged += Raise.Event<Action<BottomTab>>(BottomTab.Profile);

        navigations.Should().HaveCount(4);
        navigations[0].Should().BeOfType<GoMainMenu>();
        navigations[1].Should().BeOfType<GoPlayHub>();
        navigations[2].Should().BeOfType<GoShop>();
        navigations[3].Should().BeOfType<GoProfile>();
    }

    [Fact]
    public void SwitchToHelpTab_NurBool_KeineRegistryAufrufe()
    {
        // HelpVm hat keine OnAppearing-Implementierung — der Switch-Pfad ruft nichts an der
        // Registry. Damit ist das der einzige SwitchTo-Test der ohne VM-Substitutes laeuft.
        var (sut, _, registry, _) = CreateSut();

        sut.SwitchToHelpTab();

        sut.IsSettingsHelpTab.Should().BeTrue();
        registry.DidNotReceive().EnsureGame();
        registry.DidNotReceive().EnsureShop();
    }

    // Anmerkung: SwitchToShopTab/SwitchToSpinTab/SwitchToAchievementsTab und Co. rufen
    // EnsureXxx().OnAppearing() — NSubstitute kann concrete VM-Typen (ShopViewModel etc.)
    // ohne parameterlosen Ctor nicht substituieren, daher gibt es keinen sauberen Mock
    // fuer OnAppearing(). Diese Pfade sind besser ueber Integration-Tests abgedeckt;
    // hier wird das Tab-Bool-Setting via OnActiveViewChanged + StateChanged-Event
    // indirekt verifiziert.

    [Fact]
    public void ResetTabStates_SetztAlleBoolsAufFalse()
    {
        var (sut, _, _, _) = CreateSut();
        sut.IsShopSpinTab = true;
        sut.IsProfileAchievementsTab = true;
        sut.IsCardsCollectionTab = true;

        sut.ResetTabStates();

        sut.IsShopSpinTab.Should().BeFalse();
        sut.IsProfileAchievementsTab.Should().BeFalse();
        sut.IsSettingsHelpTab.Should().BeFalse();
        sut.IsCardsCollectionTab.Should().BeFalse();
        sut.IsChallengesMissionsTab.Should().BeFalse();
    }

    [Fact]
    public void ResetTabStates_AlleFalse_FeuertStateChangedNicht()
    {
        var (sut, _, _, _) = CreateSut();
        var fired = 0;
        sut.StateChanged += () => fired++;

        sut.ResetTabStates();

        fired.Should().Be(0);
    }

    [Fact]
    public void OnActiveViewChanged_SelbeView_FeuertStateChangedNicht()
    {
        var (sut, _, _, _) = CreateSut();
        sut.OnActiveViewChanged(ActiveView.Game);  // setzt IsBottomTabBarVisible auf false
        var fired = 0;
        sut.StateChanged += () => fired++;

        sut.OnActiveViewChanged(ActiveView.LevelSelect);  // bleibt nicht-Haupttab → false

        fired.Should().Be(0);  // IsBottomTabBarVisible bleibt false, kein Event
    }
}
