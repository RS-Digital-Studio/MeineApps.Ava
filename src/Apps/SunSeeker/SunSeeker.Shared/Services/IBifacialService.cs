using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Services;

/// <summary>
/// Liefert Bifazial-Empfehlungen: geschaetzten Mehrertrag (Bereich) je Untergrund,
/// Steilwinkel-Zuschlag und konkrete Aufstell-Tipps.
/// </summary>
public interface IBifacialService
{
    /// <summary>Bifazial-Empfehlung fuer Untergrund + Panel.</summary>
    BifacialAdvice GetAdvice(GroundType ground, PanelProfile panel);
}
