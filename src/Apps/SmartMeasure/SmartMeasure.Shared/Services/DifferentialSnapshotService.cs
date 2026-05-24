using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.13: Nearest-Neighbor-Matching mit fester Schwellwert-Distanz.
/// Greedy-Algorithmus (kein Hungarian) — fuer N&lt;500 Punkte ausreichend, O(N*M) Laufzeit.
/// Distance-Sortierung garantiert dass die naechsten Paare zuerst gepaart werden;
/// kollidierende Matches gehen leer aus.</summary>
public sealed class DifferentialSnapshotService : IDifferentialSnapshotService
{
    private readonly ICoordinateService _coordinateService;

    public DifferentialSnapshotService(ICoordinateService coordinateService)
    {
        _coordinateService = coordinateService;
    }

    public DifferentialResult Compare(
        IReadOnlyList<SurveyPoint> oldPoints,
        IReadOnlyList<SurveyPoint> newPoints,
        double matchRadiusMeters = 1.0,
        double movedThresholdMeters = 0.10)
    {
        if (oldPoints.Count == 0 && newPoints.Count == 0)
            return new DifferentialResult([], [], []);

        // Alle Kandidaten-Paare unter Schwellwert sammeln (incl. Hoehe)
        var candidates = new List<(int oldIdx, int newIdx, double distance)>();
        for (var i = 0; i < oldPoints.Count; i++)
        {
            for (var j = 0; j < newPoints.Count; j++)
            {
                var horiz = _coordinateService.HaversineDistance(
                    oldPoints[i].Latitude, oldPoints[i].Longitude,
                    newPoints[j].Latitude, newPoints[j].Longitude);
                var dh = newPoints[j].Altitude - oldPoints[i].Altitude;
                var dist3d = Math.Sqrt(horiz * horiz + dh * dh);
                if (dist3d <= matchRadiusMeters)
                    candidates.Add((i, j, dist3d));
            }
        }

        // Greedy: kuerzeste Paare zuerst, blockiere bereits gepaarte Indizes.
        candidates.Sort((a, b) => a.distance.CompareTo(b.distance));
        var pairedOld = new HashSet<int>();
        var pairedNew = new HashSet<int>();
        var matches = new List<DifferentialMatch>();

        foreach (var (oldIdx, newIdx, distance) in candidates)
        {
            if (pairedOld.Contains(oldIdx) || pairedNew.Contains(newIdx)) continue;
            pairedOld.Add(oldIdx);
            pairedNew.Add(newIdx);
            var change = distance >= movedThresholdMeters
                ? DifferentialChange.Moved
                : DifferentialChange.Unchanged;
            matches.Add(new DifferentialMatch(oldPoints[oldIdx], newPoints[newIdx], distance, change));
        }

        var removed = new List<SurveyPoint>(oldPoints.Count - pairedOld.Count);
        for (var i = 0; i < oldPoints.Count; i++)
            if (!pairedOld.Contains(i)) removed.Add(oldPoints[i]);

        var added = new List<SurveyPoint>(newPoints.Count - pairedNew.Count);
        for (var j = 0; j < newPoints.Count; j++)
            if (!pairedNew.Contains(j)) added.Add(newPoints[j]);

        return new DifferentialResult(matches, added, removed);
    }
}
