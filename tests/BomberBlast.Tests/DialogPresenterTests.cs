using BomberBlast.Services;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests fuer DialogPresenter (Welle 6 MainViewModel-Refactor ).
/// Validiert Alert/Confirm-State-Mutationen, IsAnyDialogOpen-Aggregat
/// und TaskCompletionSource-Roundtrip von ShowConfirmAsync.
/// </summary>
public class DialogPresenterTests
{
    [Fact]
    public void NeueInstanz_HatLeerenState()
    {
        var sut = new DialogPresenter();

        sut.IsAlertDialogVisible.Should().BeFalse();
        sut.IsConfirmDialogVisible.Should().BeFalse();
        sut.IsAnyDialogOpen.Should().BeFalse();
        sut.AlertDialogTitle.Should().BeEmpty();
        sut.ConfirmDialogTitle.Should().BeEmpty();
    }

    [Fact]
    public void ShowAlert_SetztSichtbarkeitUndState()
    {
        var sut = new DialogPresenter();

        sut.ShowAlert("Titel", "Nachricht", "OK");

        sut.IsAlertDialogVisible.Should().BeTrue();
        sut.AlertDialogTitle.Should().Be("Titel");
        sut.AlertDialogMessage.Should().Be("Nachricht");
        sut.AlertDialogButtonText.Should().Be("OK");
        sut.IsAnyDialogOpen.Should().BeTrue();
    }

    [Fact]
    public void ShowAlert_FeuertStateChangedEinmal()
    {
        var sut = new DialogPresenter();
        var fired = 0;
        sut.StateChanged += () => fired++;

        sut.ShowAlert("T", "M", "OK");

        fired.Should().Be(1);
    }

    [Fact]
    public void DismissAlert_VersteckUndFeuertEvent()
    {
        var sut = new DialogPresenter();
        sut.ShowAlert("T", "M", "OK");
        var fired = 0;
        sut.StateChanged += () => fired++;

        sut.DismissAlert();

        sut.IsAlertDialogVisible.Should().BeFalse();
        sut.IsAnyDialogOpen.Should().BeFalse();
        fired.Should().Be(1);
    }

    [Fact]
    public void DismissAlert_NoOpWennNichtSichtbar()
    {
        var sut = new DialogPresenter();
        var fired = 0;
        sut.StateChanged += () => fired++;

        sut.DismissAlert();

        fired.Should().Be(0, "StateChanged darf nicht feuern wenn kein Alert offen war");
    }

    [Fact]
    public async Task ShowConfirmAsync_NochOffen_LiefertNichtAbgeschlossenenTask()
    {
        var sut = new DialogPresenter();

        var task = sut.ShowConfirmAsync("T", "M", "Ja", "Nein");

        sut.IsConfirmDialogVisible.Should().BeTrue();
        sut.IsAnyDialogOpen.Should().BeTrue();
        sut.ConfirmDialogAcceptText.Should().Be("Ja");
        sut.ConfirmDialogCancelText.Should().Be("Nein");
        task.IsCompleted.Should().BeFalse();

        sut.CancelConfirm();
        var result = await task;
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AcceptConfirm_LoestTaskMitTrueAuf()
    {
        var sut = new DialogPresenter();
        var task = sut.ShowConfirmAsync("T", "M", "Ja", "Nein");

        sut.AcceptConfirm();

        var result = await task;
        result.Should().BeTrue();
        sut.IsConfirmDialogVisible.Should().BeFalse();
    }

    [Fact]
    public async Task CancelConfirm_LoestTaskMitFalseAuf()
    {
        var sut = new DialogPresenter();
        var task = sut.ShowConfirmAsync("T", "M", "Ja", "Nein");

        sut.CancelConfirm();

        var result = await task;
        result.Should().BeFalse();
        sut.IsConfirmDialogVisible.Should().BeFalse();
    }

    [Fact]
    public void AcceptConfirm_NoOpWennNichtSichtbar()
    {
        var sut = new DialogPresenter();
        var fired = 0;
        sut.StateChanged += () => fired++;

        sut.AcceptConfirm();

        fired.Should().Be(0);
    }

    [Fact]
    public async Task DoppelterAcceptConfirm_LoestTaskNurEinmalAuf()
    {
        var sut = new DialogPresenter();
        var task = sut.ShowConfirmAsync("T", "M", "Ja", "Nein");

        sut.AcceptConfirm();
        sut.AcceptConfirm();  // 2. Aufruf — sollte keinen Crash, kein TaskAlreadyCompleted

        var result = await task;
        result.Should().BeTrue();
    }

    [Fact]
    public void SetWhatsNewVisible_BeeinflusstAggregat()
    {
        var sut = new DialogPresenter();

        sut.SetWhatsNewVisible(true);
        sut.IsAnyDialogOpen.Should().BeTrue();
        sut.IsAlertDialogVisible.Should().BeFalse();
        sut.IsConfirmDialogVisible.Should().BeFalse();

        sut.SetWhatsNewVisible(false);
        sut.IsAnyDialogOpen.Should().BeFalse();
    }

    [Fact]
    public void SetWhatsNewVisible_NoOpBeiGleichemWert()
    {
        var sut = new DialogPresenter();
        var fired = 0;
        sut.StateChanged += () => fired++;

        sut.SetWhatsNewVisible(false);  // Default-Wert

        fired.Should().Be(0);
    }

    [Fact]
    public void IsAnyDialogOpen_OderVerknuepftAlleDreiFlags()
    {
        var sut = new DialogPresenter();

        sut.IsAnyDialogOpen.Should().BeFalse();

        sut.ShowAlert("T", "M", "OK");
        sut.IsAnyDialogOpen.Should().BeTrue();

        sut.DismissAlert();
        sut.IsAnyDialogOpen.Should().BeFalse();

        _ = sut.ShowConfirmAsync("T", "M", "J", "N");
        sut.IsAnyDialogOpen.Should().BeTrue();

        sut.CancelConfirm();
        sut.IsAnyDialogOpen.Should().BeFalse();

        sut.SetWhatsNewVisible(true);
        sut.IsAnyDialogOpen.Should().BeTrue();

        sut.SetWhatsNewVisible(false);
        sut.IsAnyDialogOpen.Should().BeFalse();
    }

    [Fact]
    public void ShowAlert_NullArgs_WerdenZuLeerString()
    {
        var sut = new DialogPresenter();

        sut.ShowAlert(null!, null!, null!);

        sut.AlertDialogTitle.Should().BeEmpty();
        sut.AlertDialogMessage.Should().BeEmpty();
        sut.AlertDialogButtonText.Should().BeEmpty();
        sut.IsAlertDialogVisible.Should().BeTrue();
    }

    [Fact]
    public async Task NachAcceptConfirm_NeuerShowConfirm_LiefertNeuenTask()
    {
        var sut = new DialogPresenter();
        var task1 = sut.ShowConfirmAsync("T1", "M1", "J", "N");
        sut.AcceptConfirm();
        await task1;

        var task2 = sut.ShowConfirmAsync("T2", "M2", "Ja", "Nein");

        task2.Should().NotBeSameAs(task1);
        sut.ConfirmDialogTitle.Should().Be("T2");
    }
}
