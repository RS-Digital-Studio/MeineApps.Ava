namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Zentraler Dialog-Service für Alert- und Confirm-Dialoge.
/// Wird direkt in Child-ViewModels injiziert statt Event-Routing durch MainViewModel.
/// Implementiert von DialogViewModel (Singleton).
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Zeigt einen Alert-Dialog mit Titel, Nachricht und einem Button.
    /// </summary>
    void ShowAlertDialog(string title, string message, string buttonText);

    /// <summary>
    /// Zeigt einen Confirm-Dialog mit Titel, Nachricht, Accept- und Cancel-Button.
    /// Gibt true zurück wenn Accept geklickt wurde.
    /// </summary>
    Task<bool> ShowConfirmDialog(string title, string message, string acceptText, string cancelText);
}
