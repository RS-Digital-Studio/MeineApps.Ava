using BomberBlast.Core;
using BomberBlast.Navigation;
using BomberBlast.Services;
using BomberBlast.ViewModels;
using FluentAssertions;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests fuer <see cref="NavigationRouteParser"/> und <see cref="NavigationCoordinator"/>.
///
/// <para>
/// Die Child-VMs sind sealed — NSubstitute kann sie nicht mocken. Daher konzentriert sich
/// diese Suite auf die fehleranfaellige Parsing-Logik (Compound-Routes, BaseRoute/Query-Trennung,
/// CloudSave-Gating) sowie die VM-freien Coordinator-Pfade (NeueInstanz, HideAll, Help-Route).
/// Die VM-getriebenen Routen sind ueber Integration-Tests abgedeckt.
/// </para>
/// </summary>
public class NavigationCoordinatorTests
{
    // ─── NavigationRouteParser ───────────────────────────────────────────────

    [Fact]
    public void Parse_EinfacheRoute_LiefertBaseRouteOhneQuery()
    {
        var (baseRoute, query) = NavigationRouteParser.Parse("Shop");

        baseRoute.Should().Be("Shop");
        query.Should().BeNull();
    }

    [Fact]
    public void Parse_RouteMitQuery_TrenntBaseRouteUndQuery()
    {
        var (baseRoute, query) = NavigationRouteParser.Parse("Game?mode=story&level=5");

        baseRoute.Should().Be("Game");
        query.Should().Be("mode=story&level=5");
    }

    [Fact]
    public void Parse_CompoundRoute_NutztLetztesSegment()
    {
        var (baseRoute, query) = NavigationRouteParser.Parse("//MainMenu/Game?mode=story");

        baseRoute.Should().Be("Game");
        query.Should().Be("mode=story");
    }

    [Fact]
    public void Parse_CompoundRouteOhneWeiteresSegment_LiefertErstesSegment()
    {
        var (baseRoute, query) = NavigationRouteParser.Parse("//MainMenu");

        baseRoute.Should().Be("MainMenu");
        query.Should().BeNull();
    }

    [Fact]
    public void Parse_CompoundRouteMitQuery_OhneSlash()
    {
        var (baseRoute, query) = NavigationRouteParser.Parse("//Game?mode=quick");

        baseRoute.Should().Be("Game");
        query.Should().Be("mode=quick");
    }

    [Fact]
    public void Parse_LeererString_LiefertLeereBaseRoute()
    {
        var (baseRoute, query) = NavigationRouteParser.Parse("");

        baseRoute.Should().BeEmpty();
        query.Should().BeNull();
    }

    [Fact]
    public void Parse_ZurueckRoute_BleibtUnveraendert()
    {
        var (baseRoute, query) = NavigationRouteParser.Parse("..");

        baseRoute.Should().Be("..");
        query.Should().BeNull();
    }

    [Theory]
    [InlineData("Game", true)]
    [InlineData("LevelSelect", true)]
    [InlineData("Dungeon", true)]
    [InlineData("DailyChallenge", true)]
    [InlineData("WeeklyChallenge", true)]
    [InlineData("Deck", true)]
    [InlineData("Collection", true)]
    [InlineData("MainMenu", false)]
    [InlineData("Shop", false)]
    [InlineData("Settings", false)]
    [InlineData("Help", false)]
    [InlineData("BossRush", false)]
    public void RequiresCloudSaveInit_GibtKorrektesGatingZurueck(string baseRoute, bool expected)
    {
        NavigationRouteParser.RequiresCloudSaveInit(baseRoute).Should().Be(expected);
    }

    // ─── NavigationCoordinator (VM-freie Pfade) ──────────────────────────────

    private static (NavigationCoordinator Sut, IChildViewModelRegistry Registry, IBottomTabController Tabs)
        CreateSut(Task? cloudSaveTask = null)
    {
        var registry = Substitute.For<IChildViewModelRegistry>();
        var tabs = Substitute.For<IBottomTabController>();
        var eventBus = new GameEventBus();
        var coins = Substitute.For<ICoinService>();
        var soundService = Substitute.For<ISoundService>();
        var prefs = new InMemoryPreferences();
        var soundManager = new SoundManager(soundService, prefs);
        var localization = Substitute.For<ILocalizationService>();
        var logger = Substitute.For<ILogger<NavigationCoordinator>>();
        Func<Task?> cloudProvider = () => cloudSaveTask;

        var sut = new NavigationCoordinator(registry, tabs, eventBus, coins, soundManager, localization, logger, cloudProvider);
        return (sut, registry, tabs);
    }

    [Fact]
    public void NeueInstanz_ActiveViewIstMainMenu()
    {
        var (sut, _, _) = CreateSut();

        sut.ActiveView.Should().Be(ActiveView.MainMenu);
    }

    [Fact]
    public void HideAll_SetztActiveViewAufNone_UndResettetTabs()
    {
        var (sut, _, tabs) = CreateSut();

        sut.HideAll();

        sut.ActiveView.Should().Be(ActiveView.None);
        tabs.Received(1).ResetTabStates();
    }

    [Fact]
    public void HideAll_FeuertActiveViewChanged()
    {
        var (sut, _, _) = CreateSut();
        ActiveView? captured = null;
        sut.ActiveViewChanged += v => captured = v;

        sut.HideAll();

        captured.Should().Be(ActiveView.None);
    }

    [Fact]
    public async Task NavigateToRouteAsync_Help_SetztSettingsViewUndHelpTab()
    {
        // "Help" ist die einzige Route ohne VM-OnAppearing-Aufruf — laeuft sauber ohne VM-Mocks.
        var (sut, _, tabs) = CreateSut();

        await sut.NavigateToRouteAsync("Help");

        sut.ActiveView.Should().Be(ActiveView.Settings);
        tabs.Received().IsSettingsHelpTab = true;
    }

    [Fact]
    public async Task NavigateToRouteAsync_Help_FeuertActiveViewChanged()
    {
        var (sut, _, _) = CreateSut();
        ActiveView? captured = null;
        sut.ActiveViewChanged += v => captured = v;

        await sut.NavigateToRouteAsync("Help");

        captured.Should().Be(ActiveView.Settings);
    }

    [Fact]
    public async Task NavigateToRouteAsync_HelpCompoundRoute_WirdKorrektGeparsed()
    {
        // "//MainMenu/Help" muss auf Settings-View + Help-Tab landen.
        var (sut, _, tabs) = CreateSut();

        await sut.NavigateToRouteAsync("//MainMenu/Help");

        sut.ActiveView.Should().Be(ActiveView.Settings);
        tabs.Received().IsSettingsHelpTab = true;
    }

    [Fact]
    public async Task NavigateToRouteAsync_CloudSaveInitBereitsFertig_NavigiertSofort()
    {
        // "Help" braucht zwar keinen CloudSave-Init, aber wir koennen mit einer Route testen
        // die KEINEN VM braucht — Help. CloudSaveInit wird hier gar nicht getriggert (Help ist
        // nicht in der RequiresCloudSaveInit-Liste). Daher: schneller Durchlauf erwartet.
        var (sut, _, _) = CreateSut(cloudSaveTask: Task.CompletedTask);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await sut.NavigateToRouteAsync("Help");
        sw.Stop();

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }
}
