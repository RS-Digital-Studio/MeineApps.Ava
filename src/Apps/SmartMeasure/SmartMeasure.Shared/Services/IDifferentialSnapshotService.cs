using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.13: Vergleicht zwei Vermessungs-Snapshots desselben Grundstuecks
/// und liefert Differenzen (verschobene/neue/verschwundene Punkte). Use-Cases:
/// Erosion am Hang, Setzung eines neuen Gebaeudes ueber Jahre, Saison-Veraenderungen
/// im Garten.</summary>
public interface IDifferentialSnapshotService
{
    /// <summary>Vergleicht alte vs. neue Punkt-Liste. Matching via Nearest-Neighbor mit
    /// Schwellwert; bei mehrdeutigen Matches gewinnt der naechstere Partner zuerst.</summary>
    /// <param name="oldPoints">Punkte der vorherigen Session.</param>
    /// <param name="newPoints">Punkte der aktuellen Session.</param>
    /// <param name="matchRadiusMeters">Maximaler Abstand fuer ein Matching-Paar (Default 1.0 m).</param>
    /// <param name="movedThresholdMeters">Distanz ab der ein Paar als "verschoben" markiert wird (Default 0.10 m).</param>
    DifferentialResult Compare(
        IReadOnlyList<SurveyPoint> oldPoints,
        IReadOnlyList<SurveyPoint> newPoints,
        double matchRadiusMeters = 1.0,
        double movedThresholdMeters = 0.10);
}

/// <summary>Ergebnis von <see cref="IDifferentialSnapshotService.Compare"/>.</summary>
public sealed record DifferentialResult(
    IReadOnlyList<DifferentialMatch> Matches,
    IReadOnlyList<SurveyPoint> Added,
    IReadOnlyList<SurveyPoint> Removed);

/// <summary>Ein Punkt-Paar (alt/neu) mit der gemessenen Distanz und einer Klassifikation.</summary>
public sealed record DifferentialMatch(
    SurveyPoint Old,
    SurveyPoint New,
    double DistanceMeters,
    DifferentialChange Change);

/// <summary>Aenderungs-Klassifikation eines gepaarten Punktes.</summary>
public enum DifferentialChange
{
    /// <summary>Distanz unter <c>movedThresholdMeters</c> — Punkt gilt als unveraendert.</summary>
    Unchanged,
    /// <summary>Punkt wurde messbar verschoben.</summary>
    Moved,
}
