namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Gemeinsames Interface fuer Child-ViewModels die Navigation-Events an den MainViewModel weiterleiten.
/// Ermoeglicht Schleife statt einzelner Subscribe/Unsubscribe-Zeilen pro ViewModel.
/// </summary>
public interface INavigable
{
    /// <summary>
    /// Navigationsanfrage mit Route-String (z.B. ".." fuer zurueck, "workers" fuer Arbeitermarkt).
    /// </summary>
    event Action<string>? NavigationRequested;
}
