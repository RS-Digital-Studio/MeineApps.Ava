namespace BomberBlast.Services;

/// <summary>
/// Zentraler Dialog-Manager fuer Alert + Confirm + zusammengesetzte Modal-Zustaende.
///
/// <para>
/// MainViewModel-Properties (<c>IsAlertDialogVisible</c>, <c>AlertDialogTitle</c>,
/// <c>ConfirmDialogMessage</c>, <c>IsAnyDialogOpen</c> usw.) sind ab Welle 6.1 nur noch Forwarder
/// auf diesen Service. Bindings im MainView.axaml bleiben unveraendert.
/// </para>
///
/// <para>
/// <c>IsAnyDialogOpen</c> ist ein Aggregat ueber Alert + Confirm + WhatsNew (per
/// <see cref="SetWhatsNewVisible"/>) und steuert den HitTest-Schutz des Pages-Panels im MainView.
/// </para>
/// </summary>
public interface IDialogPresenter
{
    bool IsAlertDialogVisible { get; }
    string AlertDialogTitle { get; }
    string AlertDialogMessage { get; }
    string AlertDialogButtonText { get; }

    bool IsConfirmDialogVisible { get; }
    string ConfirmDialogTitle { get; }
    string ConfirmDialogMessage { get; }
    string ConfirmDialogAcceptText { get; }
    string ConfirmDialogCancelText { get; }

    /// <summary>Aggregat: Alert ODER Confirm ODER WhatsNew offen. Steuert HitTest-Schutz im MainView.</summary>
    bool IsAnyDialogOpen { get; }

    /// <summary>Wird gefeuert wenn sich der Sichtbarkeits-State eines Dialogs aendert.</summary>
    event Action? StateChanged;

    void ShowAlert(string title, string message, string buttonText);
    Task<bool> ShowConfirmAsync(string title, string message, string acceptText, string cancelText);

    void DismissAlert();
    void AcceptConfirm();
    void CancelConfirm();

    /// <summary>
    /// Wird vom <c>WhatsNewViewModel</c>-Lifecycle aufgerufen damit <see cref="IsAnyDialogOpen"/>
    /// das WhatsNew-Modal mitberuecksichtigt (HitTest-Schutz).
    /// </summary>
    void SetWhatsNewVisible(bool visible);
}
