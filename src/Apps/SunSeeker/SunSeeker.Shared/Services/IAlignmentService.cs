using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Services;

/// <summary>
/// Leitet aus Standort, Zeit und Panel die Soll-Ausrichtung ab und bewertet die aktuelle
/// Ist-Ausrichtung gegen Soll und aktuelle Sonne (Einfallswinkel / cosine-loss).
/// </summary>
public interface IAlignmentService
{
    /// <summary>Empfohlene Soll-Ausrichtung fuer das gewuenschte Ziel.</summary>
    AlignmentRecommendation GetRecommendation(GeoLocation location, DateTime utcNow, AlignmentGoal goal, PanelProfile panel);

    /// <summary>Bewertet die aktuelle Panel-Ausrichtung gegen Empfehlung und Sonnenstand.</summary>
    AlignmentState Evaluate(SolarPosition sun, double panelAzimuth, double panelTilt, AlignmentRecommendation recommendation);
}
