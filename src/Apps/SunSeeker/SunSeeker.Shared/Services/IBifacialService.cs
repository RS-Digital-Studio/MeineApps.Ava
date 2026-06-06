using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Services;

/// <summary>
/// Liefert Bifazial-Empfehlungen: geschätzten Mehrertrag (Bereich) je Untergrund,
/// Steilwinkel-Zuschlag und konkrete Aufstell-Tipps.
/// </summary>
public interface IBifacialService
{
    /// <summary>Bifazial-Empfehlung für Untergrund + Panel.</summary>
    BifacialAdvice GetAdvice(GroundType ground, PanelProfile panel);
}
