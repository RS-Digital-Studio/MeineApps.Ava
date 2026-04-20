using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// MiniGame-Navigation + Abbruch-Bestaetigung.
/// Phase 2: s_miniGameRoutes + NavigateToMiniGame + IsAnyMiniGamePlaying + StopCurrentMiniGame
/// + ConfirmMiniGameAbortAsync leben hier.  Der Host (MainViewModel) liefert nur noch
/// Zustandszugriffe.
/// </summary>
public interface IMiniGameNavigator
{
    void AttachHost(INavigationHost host);

    /// <summary>True wenn das aktive MiniGame-VM gerade laeuft (Countdown oder Spiel).</summary>
    bool IsAnyMiniGamePlaying();

    /// <summary>Zeigt den Abbruch-Bestaetigungsdialog. Bei Bestaetigung: Stop + Dashboard.</summary>
    Task ConfirmMiniGameAbortAsync();

    /// <summary>Stoppt das aktive MiniGame sofort (ohne Rueckfrage).</summary>
    void StopCurrent();

    /// <summary>Mapped Route-Teil ("minigame/sawing") auf <see cref="ActivePage"/>.</summary>
    bool TryResolveRoute(string routePart, out ActivePage page);

    /// <summary>Navigiert zum MiniGame (setzt ActivePage + OrderId am aktiven VM).</summary>
    void NavigateToMiniGame(string routePart, string orderId);
}
