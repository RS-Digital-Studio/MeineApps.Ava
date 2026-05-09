namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// v2.0.39 Audit-Fix U1: Zeigt Bestandsspielern beim Update einen kumulativen
/// "Was ist neu"-Dialog mit den neuen Features seit ihrer letzten App-Version.
/// </summary>
public interface IWhatsNewService
{
    /// <summary>
    /// Zeigt den Dialog wenn der Spieler ein App-Update installiert hat. No-op fuer
    /// brandneue Spieler (kein Save) und bereits aktualisierte Spieler.
    /// </summary>
    Task ShowWhatsNewIfNeededAsync();
}
