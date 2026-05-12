namespace MeineApps.Core.Ava.Localization;

/// <summary>
/// Markiert ein ViewModel als "lokalisierbar". Implementierende VMs werden
/// vom MainViewModel-Forwarder automatisch beim <see cref="ILocalizationService.LanguageChanged"/>-Event
/// neu lokalisiert (Audit M23).
///
/// <para>Konvention: Alle App-VMs mit String-Properties die aus <c>ILocalizationService.GetString</c>
/// stammen implementieren entweder dieses Interface ODER haben ein leeres <c>OnAppearing()</c> das
/// die Strings frisch laed.</para>
///
/// <para>Vorteil: Statt einer Ad-hoc-Liste im LanguageChanged-Handler kann der Forwarder
/// alle injizierten VMs iterieren und nur die instanziierten neu lokalisieren.</para>
///
/// <para>Verwendung:</para>
/// <code>
/// public sealed partial class MyViewModel : ViewModelBase, INavigable, ILocalizable
/// {
///     public void UpdateLocalizedTexts()
///     {
///         Title = _localization.GetString("MyTitle") ?? "Title";
///         // ...
///     }
/// }
/// </code>
/// </summary>
public interface ILocalizable
{
    /// <summary>
    /// Wird vom MainViewModel beim Sprachwechsel aufgerufen.
    /// Implementierung soll alle Localized-String-Properties neu laden.
    /// </summary>
    void UpdateLocalizedTexts();
}
