namespace BomberBlast.Services;

/// <summary>
/// Default-Implementation von <see cref="IDialogPresenter"/>.
///
/// <para>
/// Haelt den Dialog-State (Alert + Confirm) plus den WhatsNew-Flag fuer das
/// <see cref="IsAnyDialogOpen"/>-Aggregat. Feuert pro Mutation einmal <see cref="StateChanged"/>
/// — MainViewModel-Forwarder ruft daraufhin alle <c>OnPropertyChanged</c> der bindbaren Properties.
/// </para>
///
/// <para>
/// <c>ShowConfirmAsync</c> nutzt einen <see cref="TaskCompletionSource{TResult}"/>:
/// <c>AcceptConfirm</c>/<c>CancelConfirm</c> resolven den Task mit <c>true</c>/<c>false</c>.
/// Aufrufer warten via <c>await</c>.
/// </para>
/// </summary>
public sealed class DialogPresenter : IDialogPresenter
{
    // Alert-State
    private bool _isAlertDialogVisible;
    private string _alertDialogTitle = "";
    private string _alertDialogMessage = "";
    private string _alertDialogButtonText = "";

    // Confirm-State
    private bool _isConfirmDialogVisible;
    private string _confirmDialogTitle = "";
    private string _confirmDialogMessage = "";
    private string _confirmDialogAcceptText = "";
    private string _confirmDialogCancelText = "";
    private TaskCompletionSource<bool>? _confirmDialogTcs;

    // WhatsNew-Flag (Aggregat-Beitrag, externer Lifecycle ueber WhatsNewVm)
    private bool _isWhatsNewVisible;

    public bool IsWhatsNewVisible => _isWhatsNewVisible;

    public bool IsAlertDialogVisible => _isAlertDialogVisible;
    public string AlertDialogTitle => _alertDialogTitle;
    public string AlertDialogMessage => _alertDialogMessage;
    public string AlertDialogButtonText => _alertDialogButtonText;

    public bool IsConfirmDialogVisible => _isConfirmDialogVisible;
    public string ConfirmDialogTitle => _confirmDialogTitle;
    public string ConfirmDialogMessage => _confirmDialogMessage;
    public string ConfirmDialogAcceptText => _confirmDialogAcceptText;
    public string ConfirmDialogCancelText => _confirmDialogCancelText;

    public bool IsAnyDialogOpen => _isAlertDialogVisible || _isConfirmDialogVisible || _isWhatsNewVisible;

    public event Action? StateChanged;

    public void ShowAlert(string title, string message, string buttonText)
    {
        _alertDialogTitle = title ?? "";
        _alertDialogMessage = message ?? "";
        _alertDialogButtonText = buttonText ?? "";
        _isAlertDialogVisible = true;
        StateChanged?.Invoke();
    }

    public Task<bool> ShowConfirmAsync(string title, string message, string acceptText, string cancelText)
    {
        _confirmDialogTitle = title ?? "";
        _confirmDialogMessage = message ?? "";
        _confirmDialogAcceptText = acceptText ?? "";
        _confirmDialogCancelText = cancelText ?? "";
        _confirmDialogTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _isConfirmDialogVisible = true;
        StateChanged?.Invoke();
        return _confirmDialogTcs.Task;
    }

    public void DismissAlert()
    {
        if (!_isAlertDialogVisible) return;
        _isAlertDialogVisible = false;
        StateChanged?.Invoke();
    }

    public void AcceptConfirm()
    {
        if (!_isConfirmDialogVisible) return;
        _isConfirmDialogVisible = false;
        var tcs = _confirmDialogTcs;
        _confirmDialogTcs = null;
        StateChanged?.Invoke();
        tcs?.TrySetResult(true);
    }

    public void CancelConfirm()
    {
        if (!_isConfirmDialogVisible) return;
        _isConfirmDialogVisible = false;
        var tcs = _confirmDialogTcs;
        _confirmDialogTcs = null;
        StateChanged?.Invoke();
        tcs?.TrySetResult(false);
    }

    public void SetWhatsNewVisible(bool visible)
    {
        if (_isWhatsNewVisible == visible) return;
        _isWhatsNewVisible = visible;
        StateChanged?.Invoke();
    }
}
