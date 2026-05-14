namespace BomberBlast.Services;

/// <summary>
/// Default-Implementation von <see cref="IDialogPresenter"/> (Welle 6 MainViewModel-Refactor).
///
/// <para>
/// Phase 1: Leeres Geruest. Die Alert/Confirm-Logik wird in Phase 2 aus
/// <see cref="BomberBlast.ViewModels.MainViewModel"/> hier hin verschoben.
/// </para>
/// </summary>
public sealed class DialogPresenter : IDialogPresenter
{
    public bool IsAlertDialogVisible { get; private set; }
    public string AlertDialogTitle { get; private set; } = "";
    public string AlertDialogMessage { get; private set; } = "";
    public string AlertDialogButtonText { get; private set; } = "";

    public bool IsConfirmDialogVisible { get; private set; }
    public string ConfirmDialogTitle { get; private set; } = "";
    public string ConfirmDialogMessage { get; private set; } = "";
    public string ConfirmDialogAcceptText { get; private set; } = "";
    public string ConfirmDialogCancelText { get; private set; } = "";

    private bool _isWhatsNewVisible;

    public bool IsAnyDialogOpen => IsAlertDialogVisible || IsConfirmDialogVisible || _isWhatsNewVisible;

    public event Action? StateChanged;

    public void ShowAlert(string title, string message, string buttonText)
        => throw new NotImplementedException("Wird in Phase 2 gefuellt.");

    public Task<bool> ShowConfirmAsync(string title, string message, string acceptText, string cancelText)
        => throw new NotImplementedException("Wird in Phase 2 gefuellt.");

    public void DismissAlert() => throw new NotImplementedException("Phase 2.");
    public void AcceptConfirm() => throw new NotImplementedException("Phase 2.");
    public void CancelConfirm() => throw new NotImplementedException("Phase 2.");

    public void SetWhatsNewVisible(bool visible)
    {
        if (_isWhatsNewVisible == visible) return;
        _isWhatsNewVisible = visible;
        StateChanged?.Invoke();
    }
}
