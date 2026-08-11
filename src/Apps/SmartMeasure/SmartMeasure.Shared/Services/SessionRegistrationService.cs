using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>
/// 2D-Helmert-Einpassung (Translation + Rotation, kein Maßstab) über Passpunkt-Paare.
/// Vertragsdetails + Begründung → <see cref="ISessionRegistrationService"/>.
///
/// Ablauf:
/// 1. Beide Punktmengen in EIN lokales Meter-System bringen (UTM relativ zum Bestands-Schwerpunkt).
/// 2. Paare bilden: greedy nearest-neighbour, kürzeste Distanzen zuerst, jeder Punkt nur einmal.
/// 3. Rotation aus den schwerpunkt-zentrierten Paaren schließen (Summe der Kreuz-/Skalarprodukte),
///    Translation aus der Schwerpunkt-Differenz.
/// 4. Plausibilität prüfen, dann auf ALLE neuen Punkte anwenden (auch die ohne Partner).
/// </summary>
public sealed class SessionRegistrationService : ISessionRegistrationService
{
    private readonly ICoordinateService _coordinateService;

    public SessionRegistrationService(ICoordinateService coordinateService)
    {
        _coordinateService = coordinateService;
    }

    public SessionRegistrationResult Register(
        IReadOnlyList<SurveyPoint> existing,
        IReadOnlyList<SurveyPoint> incoming,
        SessionRegistrationOptions? options = null)
    {
        var opt = options ?? SessionRegistrationOptions.Default;
        var unchanged = incoming.ToList();

        // Erste Sitzung: es gibt keinen Bestand, auf den eingepasst werden könnte.
        if (existing.Count == 0 || incoming.Count == 0)
            return Reject(unchanged, SessionRegistrationRejection.NoExistingPoints);

        // Gemeinsames lokales Meter-System. Referenz ist der BESTANDS-Schwerpunkt: der Bestand
        // ist die Bezugsgröße, auf die eingepasst wird, und bleibt unverändert.
        var refLat = existing.Average(p => p.Latitude);
        var refLon = existing.Average(p => p.Longitude);

        var oldXy = ToLocal(existing, refLat, refLon);
        var newXy = ToLocal(incoming, refLat, refLon);

        var pairs = FindPairs(oldXy, newXy, opt.MatchRadiusMeters);
        if (pairs.Count < opt.MinPairs)
            return Reject(unchanged, SessionRegistrationRejection.TooFewPairs, pairs.Count);

        // Schwerpunkte der GEPAARTEN Punkte (nicht aller) — nur sie tragen Information.
        var oldCx = pairs.Average(p => oldXy[p.oldIdx].x);
        var oldCy = pairs.Average(p => oldXy[p.oldIdx].y);
        var newCx = pairs.Average(p => newXy[p.newIdx].x);
        var newCy = pairs.Average(p => newXy[p.newIdx].y);

        // Rotation aus zentrierten Paaren: der Winkel, der Σ|R·neu − alt|² minimiert, ergibt
        // sich als atan2(Σ Kreuzprodukt, Σ Skalarprodukt).
        double crossSum = 0, dotSum = 0, spread = 0;
        foreach (var (oldIdx, newIdx, _) in pairs)
        {
            var ax = oldXy[oldIdx].x - oldCx;
            var ay = oldXy[oldIdx].y - oldCy;
            var bx = newXy[newIdx].x - newCx;
            var by = newXy[newIdx].y - newCy;

            crossSum += bx * ay - by * ax;
            dotSum += bx * ax + by * ay;
            spread += bx * bx + by * by;
        }

        // Liegen alle Passpunkte praktisch auf einem Punkt, ist die Rotation nicht bestimmbar
        // (crossSum und dotSum gehen beide gegen 0 → atan2 wird Rauschen).
        if (spread < 0.01) // < 10 cm² Gesamt-Streuung
            return Reject(unchanged, SessionRegistrationRejection.DegenerateGeometry, pairs.Count);

        var theta = Math.Atan2(crossSum, dotSum);
        var cos = Math.Cos(theta);
        var sin = Math.Sin(theta);

        // Translation so, dass der rotierte neue Schwerpunkt auf den alten fällt.
        var tx = oldCx - (newCx * cos - newCy * sin);
        var ty = oldCy - (newCx * sin + newCy * cos);

        // Restklaffung über die Paare bewerten, BEVOR irgendetwas angewandt wird.
        double sumSq = 0;
        foreach (var (oldIdx, newIdx, _) in pairs)
        {
            var (rx, ry) = Apply(newXy[newIdx], cos, sin, tx, ty);
            var dx = rx - oldXy[oldIdx].x;
            var dy = ry - oldXy[oldIdx].y;
            sumSq += dx * dx + dy * dy;
        }
        var rms = Math.Sqrt(sumSq / pairs.Count);

        var rotationDeg = theta * 180.0 / Math.PI;
        var translation = Math.Sqrt(tx * tx + ty * ty);

        if (Math.Abs(rotationDeg) > opt.MaxRotationDeg)
            return Reject(unchanged, SessionRegistrationRejection.RotationImplausible,
                pairs.Count, rotationDeg, translation, rms);

        if (rms > opt.MaxResidualMeters)
            return Reject(unchanged, SessionRegistrationRejection.ResidualTooLarge,
                pairs.Count, rotationDeg, translation, rms);

        // Auf ALLE neuen Punkte anwenden — auch auf die ohne Partner. Genau das ist der Sinn:
        // die Passpunkte definieren die Transformation, die für den ganzen Satz gilt.
        var transformed = new List<SurveyPoint>(incoming.Count);
        for (var i = 0; i < incoming.Count; i++)
        {
            var (rx, ry) = Apply(newXy[i], cos, sin, tx, ty);
            var (lat, lon, _) = _coordinateService.LocalToLatLon(rx, ry, 0, refLat, refLon, 0);

            // Höhe bleibt unangetastet: der Höhenbezug kommt aus Anker-Höhe + Boden-Offset und
            // hat mit der Lage-Verdrehung nichts zu tun. Eine Höhen-Einpassung wäre ein eigener
            // Schritt (und bräuchte andere Toleranzen).
            var p = incoming[i];
            p.Latitude = lat;
            p.Longitude = lon;
            transformed.Add(p);
        }

        return new SessionRegistrationResult(
            Applied: true,
            Points: transformed,
            PairCount: pairs.Count,
            RotationDeg: rotationDeg,
            TranslationMeters: translation,
            ResidualRmsMeters: rms,
            Rejection: SessionRegistrationRejection.None);
    }

    private List<(double x, double y)> ToLocal(
        IReadOnlyList<SurveyPoint> points, double refLat, double refLon)
    {
        var result = new List<(double x, double y)>(points.Count);
        foreach (var p in points)
        {
            var (x, y, _) = _coordinateService.LatLonToLocal(
                p.Latitude, p.Longitude, 0, refLat, refLon, 0);
            result.Add((x, y));
        }
        return result;
    }

    /// <summary>Greedy nearest-neighbour: kürzeste Paare zuerst, jeder Punkt höchstens einmal.
    /// Dasselbe Prinzip wie <see cref="DifferentialSnapshotService"/> — kein Hungarian nötig,
    /// bei &lt; 500 Punkten ist O(N·M) unkritisch.</summary>
    private static List<(int oldIdx, int newIdx, double dist)> FindPairs(
        List<(double x, double y)> oldXy, List<(double x, double y)> newXy, double radius)
    {
        var candidates = new List<(int oldIdx, int newIdx, double dist)>();
        for (var i = 0; i < oldXy.Count; i++)
        {
            for (var j = 0; j < newXy.Count; j++)
            {
                var dx = newXy[j].x - oldXy[i].x;
                var dy = newXy[j].y - oldXy[i].y;
                var d = Math.Sqrt(dx * dx + dy * dy);
                if (d <= radius) candidates.Add((i, j, d));
            }
        }

        candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

        var usedOld = new HashSet<int>();
        var usedNew = new HashSet<int>();
        var pairs = new List<(int oldIdx, int newIdx, double dist)>();
        foreach (var c in candidates)
        {
            if (!usedOld.Add(c.oldIdx)) continue;
            if (!usedNew.Add(c.newIdx)) { usedOld.Remove(c.oldIdx); continue; }
            pairs.Add(c);
        }
        return pairs;
    }

    private static (double x, double y) Apply(
        (double x, double y) p, double cos, double sin, double tx, double ty)
        => (p.x * cos - p.y * sin + tx, p.x * sin + p.y * cos + ty);

    private static SessionRegistrationResult Reject(
        List<SurveyPoint> points, SessionRegistrationRejection reason,
        int pairCount = 0, double rotationDeg = 0, double translation = 0, double rms = 0)
        => new(false, points, pairCount, rotationDeg, translation, rms, reason);
}
