using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// AAA-Audit P1: Liefert House-Ad-Karten fuer die 11 eigenen Apps des Portfolios.
/// </summary>
public interface ICrossPromoService
{
    /// <summary>Alle Promo-Apps ausser der eigenen (HandwerkerImperium wird gefiltert).</summary>
    IReadOnlyList<CrossPromoApp> GetAvailable();

    /// <summary>
    /// Liefert die gerade aktive Rotations-Auswahl (1-3 Apps). Wird taeglich rotiert,
    /// um keine Banner-Blindness zu erzeugen.
    /// </summary>
    IReadOnlyList<CrossPromoApp> GetCurrentRotation(int count = 1);

    /// <summary>Loggt einen Klick auf eine Promo-Karte (Analytics-Event).</summary>
    void TrackClick(CrossPromoApp app);
}
