using BomberBlast.Navigation;
using BomberBlast.Services;
using BomberBlast.ViewModels;
using FluentAssertions;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests fuer <see cref="LifecycleHub"/>.
/// Validiert HandleBackPressed-Prioritaeten (Dialog &gt; Game &gt; Settings &gt; Sub-View &gt; Double-Back),
/// CloudSaveInitTask-Start und OnAdUnavailable-Bridge.
///
/// <para>
/// GameVm ist sealed und nicht mockbar — die GameVm-abhaengigen BackPress-Pfade (Score-Double,
/// Pause/Resume) sind ueber Integration-Tests abgedeckt. Hier getestet: die Pfade die ohne
/// GameVm laufen (Dialoge, Settings, Sub-Views, MainMenu-Double-Back).
/// </para>
/// </summary>
public class LifecycleHubTests
{
    private static (LifecycleHub Sut, IDialogPresenter Dialog, IChildViewModelRegistry Registry, INavigationCoordinator Nav, IRewardedAdService Ads)
        CreateSut()
    {
        // ICloudSaveService.TryLoadFromCloudAsync() liefert per NSubstitute-Default Task.CompletedTask.
        var cloudSave = Substitute.For<ICloudSaveService>();
        var dialog = Substitute.For<IDialogPresenter>();
        var registry = Substitute.For<IChildViewModelRegistry>();
        var nav = Substitute.For<INavigationCoordinator>();
        var localization = Substitute.For<ILocalizationService>();
        var ads = Substitute.For<IRewardedAdService>();
        var sound = Substitute.For<BomberBlast.Services.ISoundService>();
        var logger = Substitute.For<ILogger<LifecycleHub>>();

        var sut = new LifecycleHub(cloudSave, dialog, registry, nav, localization, ads, sound, logger);
        return (sut, dialog, registry, nav, ads);
    }

    [Fact]
    public void Ctor_StartetCloudSaveInitTask()
    {
        var (sut, _, _, _, _) = CreateSut();

        sut.CloudSaveInitTask.Should().NotBeNull();
    }

    [Fact]
    public void HandleBackPressed_ConfirmDialogOffen_SchliesstDialog_UndKonsumiert()
    {
        var (sut, dialog, _, nav, _) = CreateSut();
        dialog.IsConfirmDialogVisible.Returns(true);

        var result = sut.HandleBackPressed();

        result.Should().BeTrue();
        dialog.Received(1).CancelConfirm();
        nav.DidNotReceiveWithAnyArgs().NavigateTo(default!);
    }

    [Fact]
    public void HandleBackPressed_AlertDialogOffen_SchliesstAlert_UndKonsumiert()
    {
        var (sut, dialog, _, nav, _) = CreateSut();
        dialog.IsConfirmDialogVisible.Returns(false);
        dialog.IsAlertDialogVisible.Returns(true);

        var result = sut.HandleBackPressed();

        result.Should().BeTrue();
        dialog.Received(1).DismissAlert();
        nav.DidNotReceiveWithAnyArgs().NavigateTo(default!);
    }

    [Fact]
    public void HandleBackPressed_ConfirmHatVorrangVorAlert()
    {
        var (sut, dialog, _, _, _) = CreateSut();
        dialog.IsConfirmDialogVisible.Returns(true);
        dialog.IsAlertDialogVisible.Returns(true);

        sut.HandleBackPressed();

        dialog.Received(1).CancelConfirm();
        dialog.DidNotReceive().DismissAlert();
    }

    [Fact]
    public void HandleBackPressed_AufSettings_NavigiertZurueck()
    {
        var (sut, dialog, registry, nav, _) = CreateSut();
        dialog.IsConfirmDialogVisible.Returns(false);
        dialog.IsAlertDialogVisible.Returns(false);
        registry.GameVm.Returns((GameViewModel?)null);
        nav.ActiveView.Returns(ActiveView.Settings);

        var result = sut.HandleBackPressed();

        result.Should().BeTrue();
        nav.Received(1).NavigateTo(Arg.Any<GoBack>());
    }

    [Fact]
    public void HandleBackPressed_AufSubView_NavigiertZumMainMenu()
    {
        var (sut, dialog, registry, nav, _) = CreateSut();
        dialog.IsConfirmDialogVisible.Returns(false);
        dialog.IsAlertDialogVisible.Returns(false);
        registry.GameVm.Returns((GameViewModel?)null);
        nav.ActiveView.Returns(ActiveView.Shop);

        var result = sut.HandleBackPressed();

        result.Should().BeTrue();
        nav.Received(1).NavigateTo(Arg.Any<GoMainMenu>());
    }

    [Theory]
    [InlineData(ActiveView.HighScores)]
    [InlineData(ActiveView.League)]
    [InlineData(ActiveView.Dungeon)]
    [InlineData(ActiveView.BattlePass)]
    [InlineData(ActiveView.Profile)]
    public void HandleBackPressed_VerschiedeneSubViews_NavigierenZumMainMenu(ActiveView view)
    {
        var (sut, dialog, registry, nav, _) = CreateSut();
        dialog.IsConfirmDialogVisible.Returns(false);
        dialog.IsAlertDialogVisible.Returns(false);
        registry.GameVm.Returns((GameViewModel?)null);
        nav.ActiveView.Returns(view);

        var result = sut.HandleBackPressed();

        result.Should().BeTrue();
        nav.Received(1).NavigateTo(Arg.Any<GoMainMenu>());
    }

    [Fact]
    public void HandleBackPressed_AufMainMenu_ErsterDruck_KonsumiertMitExitHint()
    {
        var (sut, dialog, registry, nav, _) = CreateSut();
        dialog.IsConfirmDialogVisible.Returns(false);
        dialog.IsAlertDialogVisible.Returns(false);
        registry.GameVm.Returns((GameViewModel?)null);
        nav.ActiveView.Returns(ActiveView.MainMenu);
        string? exitHint = null;
        sut.ExitHintRequested += msg => exitHint = msg;

        var result = sut.HandleBackPressed();

        // Erster Back-Druck auf MainMenu: BackPressHelper zeigt Exit-Hint + konsumiert (true).
        result.Should().BeTrue();
        exitHint.Should().NotBeNull();
        nav.DidNotReceiveWithAnyArgs().NavigateTo(default!);
    }

    [Fact]
    public void HandleBackPressed_AufMainMenu_ZweiterDruckInnerhalbFenster_KonsumiertNicht()
    {
        var (sut, dialog, registry, nav, _) = CreateSut();
        dialog.IsConfirmDialogVisible.Returns(false);
        dialog.IsAlertDialogVisible.Returns(false);
        registry.GameVm.Returns((GameViewModel?)null);
        nav.ActiveView.Returns(ActiveView.MainMenu);

        var first = sut.HandleBackPressed();   // zeigt Hint, konsumiert
        var second = sut.HandleBackPressed();  // innerhalb Fenster → App darf schliessen

        first.Should().BeTrue();
        second.Should().BeFalse("zweiter Back-Druck innerhalb des Double-Back-Fensters beendet die App");
    }

    [Fact]
    public void HandleBackPressed_AufNone_KonsumiertNicht()
    {
        var (sut, dialog, registry, nav, _) = CreateSut();
        dialog.IsConfirmDialogVisible.Returns(false);
        dialog.IsAlertDialogVisible.Returns(false);
        registry.GameVm.Returns((GameViewModel?)null);
        nav.ActiveView.Returns(ActiveView.None);

        var result = sut.HandleBackPressed();

        result.Should().BeFalse();
    }

    [Fact]
    public void OnAdUnavailable_ViaEvent_ZeigtAlert()
    {
        var (_, dialog, _, _, ads) = CreateSut();

        ads.AdUnavailable += Raise.Event<Action>();

        dialog.ReceivedWithAnyArgs(1).ShowAlert(default!, default!, default!);
    }
}
