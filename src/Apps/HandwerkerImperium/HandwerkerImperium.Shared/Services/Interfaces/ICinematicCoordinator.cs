using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// (MainViewModel-Zerlegung): Koordiniert die Prestige-Cinematic
/// (4-Phasen-Renderer, 14s, Skip+TapToContinue). Aus MainViewModel extrahiert.
///
/// Verantwortlichkeiten:
/// - Subscribe auf <c>IPrestigeService.CinematicReady</c>
/// - Tier-Namen lokalisieren (RESX <c>Prestige{Tier}</c>-Keys)
/// - Audio-Track waehrend Cinematic auf <c>MusicTrack.Celebration</c> wechseln,
///   danach zurueck auf <c>MusicTrack.IdleWorkshop</c>
/// - Analytics-Events (Skipped / Completed) feuern
/// - View-Trigger via <see cref="CinematicReady"/>-Event
/// </summary>
public interface ICinematicCoordinator
{
    /// <summary>Wird auf dem UI-Thread gefeuert, sobald die Cinematic gestartet werden soll.</summary>
    event Action<PrestigeCinematicData>? CinematicReady;

    /// <summary>View ruft auf bei Tap-to-Skip waehrend der Cinematic.</summary>
    void OnSkipped();

    /// <summary>View ruft auf wenn die Cinematic beendet ist (Tap-to-Continue oder Auto-Timeout).</summary>
    void OnDismissed();
}
