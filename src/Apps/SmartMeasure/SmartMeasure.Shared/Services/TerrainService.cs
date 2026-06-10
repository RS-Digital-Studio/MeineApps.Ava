using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>
/// Geländemodell-Berechnungen: Bowyer-Watson Delaunay-Triangulierung, Konturlinien
/// (Marching Triangles), Flächen- und Volumen-Berechnung.
///
/// Robustheit:
/// - Duplikat-Dedup via Epsilon (Punkte < 1mm Abstand werden gemergt)
/// - CCW-Orientation für alle neu erzeugten Dreiecke (Delaunay-Test korrekt)
/// - Vertex-Höhe-Perturbation bei Konturlinien (vermeidet doppelte Segmente)
/// - Konvex-Hüllen-Shoelace für Flächenberechnung aus ungeordneten Mess-Punkten
/// - Vorberechnete Face-Normalen im Mesh (spart 24k sqrt/s beim Render)
/// </summary>
public class TerrainService : ITerrainService
{
    /// <summary>Mindest-Abstand zwischen Messpunkten für Delaunay (1mm Epsilon)</summary>
    private const double PointMergeEpsilon = 0.001;

    /// <summary>Toleranz für Barizentrisch-Tests (Punkt auf Dreiecks-Kante)</summary>
    private const double BarycentricEpsilon = 1e-6;

    /// <summary>Perturbation für Konturlinien-Höhen wenn exakt auf Vertex</summary>
    private const double ContourVertexEpsilon = 1e-9;

    public TerrainMesh CreateMesh(double[] x, double[] y, double[] z)
    {
        if (x.Length < 3 || x.Length != y.Length || x.Length != z.Length)
        {
            // Degenerierter Input — leeres Mesh zurückgeben mit Bounds aus verfügbaren Daten
            return BuildMesh(x, y, z, Array.Empty<int>());
        }

        // 1. Duplikate mergen (Mess-Streuung kann dicht benachbarte Messwiederholungen erzeugen)
        var (uniqueX, uniqueY, uniqueZ) = DeduplicatePoints(x, y, z);

        if (uniqueX.Length < 3)
            return BuildMesh(uniqueX, uniqueY, uniqueZ, Array.Empty<int>());

        // 2. Triangulation
        var triangles = BowyerWatson(uniqueX, uniqueY);

        // 3. Orientation normalisieren (alle CCW im XY-Plane)
        NormalizeWinding(uniqueX, uniqueY, triangles);

        // 4. Mesh + Face-Normalen bauen
        return BuildMesh(uniqueX, uniqueY, uniqueZ, triangles);
    }

    public List<ContourLine> CreateContourLines(TerrainMesh mesh, double interval)
    {
        var contours = new List<ContourLine>();
        if (mesh.TriangleCount == 0 || interval <= 0) return contours;

        var minH = Math.Floor(mesh.MinZ / interval) * interval;
        var maxH = Math.Ceiling(mesh.MaxZ / interval) * interval;

        // Intersection-Puffer wiederverwenden (wird pro Dreieck geleert)
        var intersections = new List<(double x, double y)>(3);

        for (var h = minH; h <= maxH; h += interval)
        {
            // Höhe leicht perturbieren wenn sie exakt auf einem Vertex liegt — sonst
            // produziert TryAddEdgeIntersection doppelte Punkte an der Vertex-Kante.
            var effectiveH = PerturbContourHeight(mesh, h);

            var contour = new ContourLine { Height = h };

            for (int t = 0; t < mesh.TriangleCount; t++)
            {
                var i0 = mesh.Triangles[t * 3];
                var i1 = mesh.Triangles[t * 3 + 1];
                var i2 = mesh.Triangles[t * 3 + 2];

                intersections.Clear();
                TryAddEdgeIntersection(intersections, effectiveH,
                    mesh.X[i0], mesh.Y[i0], mesh.Z[i0],
                    mesh.X[i1], mesh.Y[i1], mesh.Z[i1]);
                TryAddEdgeIntersection(intersections, effectiveH,
                    mesh.X[i1], mesh.Y[i1], mesh.Z[i1],
                    mesh.X[i2], mesh.Y[i2], mesh.Z[i2]);
                TryAddEdgeIntersection(intersections, effectiveH,
                    mesh.X[i2], mesh.Y[i2], mesh.Z[i2],
                    mesh.X[i0], mesh.Y[i0], mesh.Z[i0]);

                // Bei 3 Intersections (Höhe nahe Vertex): die zwei mit grösstem Abstand wählen
                if (intersections.Count == 3)
                {
                    var (a, b) = PickLongestSegment(intersections);
                    contour.Segments.Add(((float)a.x, (float)a.y, (float)b.x, (float)b.y));
                }
                else if (intersections.Count == 2)
                {
                    contour.Segments.Add((
                        (float)intersections[0].x, (float)intersections[0].y,
                        (float)intersections[1].x, (float)intersections[1].y));
                }
            }

            if (contour.Segments.Count > 0)
                contours.Add(contour);
        }

        return contours;
    }

    public double? InterpolateHeight(TerrainMesh mesh, double px, double py)
    {
        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            var i0 = mesh.Triangles[t * 3];
            var i1 = mesh.Triangles[t * 3 + 1];
            var i2 = mesh.Triangles[t * 3 + 2];

            var (u, v, w) = Barycentric(
                px, py,
                mesh.X[i0], mesh.Y[i0],
                mesh.X[i1], mesh.Y[i1],
                mesh.X[i2], mesh.Y[i2]);

            // Toleranz für numerische Ungenauigkeiten — Punkte auf Dreieckskanten
            // sollen trotzdem ein Ergebnis liefern
            if (u >= -BarycentricEpsilon && v >= -BarycentricEpsilon && w >= -BarycentricEpsilon)
                return u * mesh.Z[i0] + v * mesh.Z[i1] + w * mesh.Z[i2];
        }

        return null;
    }

    /// <summary>
    /// Berechnet die Fläche in m² als Shoelace-Formel über die konvexe Hülle.
    /// Die Input-Punkte können in beliebiger Mess-Reihenfolge vorliegen —
    /// wir bilden erst die konvexe Hülle (Andrew's Monotone Chain) damit die
    /// Shoelace-Formel überhaupt sinnvolle Ergebnisse liefert.
    /// </summary>
    public double CalculateArea2D(double[] x, double[] y)
    {
        if (x.Length < 3) return 0;

        var hull = ConvexHull(x, y);
        if (hull.Count < 3) return 0;

        double area = 0;
        for (int i = 0; i < hull.Count; i++)
        {
            int j = (i + 1) % hull.Count;
            area += hull[i].x * hull[j].y;
            area -= hull[j].x * hull[i].y;
        }
        return Math.Abs(area) / 2.0;
    }

    public double CalculateArea3D(TerrainMesh mesh)
    {
        double totalArea = 0;
        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            var i0 = mesh.Triangles[t * 3];
            var i1 = mesh.Triangles[t * 3 + 1];
            var i2 = mesh.Triangles[t * 3 + 2];

            var ax = mesh.X[i1] - mesh.X[i0];
            var ay = mesh.Y[i1] - mesh.Y[i0];
            var az = mesh.Z[i1] - mesh.Z[i0];
            var bx = mesh.X[i2] - mesh.X[i0];
            var by = mesh.Y[i2] - mesh.Y[i0];
            var bz = mesh.Z[i2] - mesh.Z[i0];

            // Kreuzprodukt |a × b| / 2 = Dreiecksfläche im 3D
            var cx = ay * bz - az * by;
            var cy = az * bx - ax * bz;
            var cz = ax * by - ay * bx;

            totalArea += Math.Sqrt(cx * cx + cy * cy + cz * cz) / 2.0;
        }
        return totalArea;
    }

    public (double fill, double cut) CalculateVolume(TerrainMesh mesh, double referenceHeight)
    {
        double fill = 0, cut = 0;

        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            var i0 = mesh.Triangles[t * 3];
            var i1 = mesh.Triangles[t * 3 + 1];
            var i2 = mesh.Triangles[t * 3 + 2];

            // Dreieck-Grundfläche in XY-Projektion (korrekt für Prisma-Volumen)
            var ax = mesh.X[i1] - mesh.X[i0];
            var ay = mesh.Y[i1] - mesh.Y[i0];
            var bx = mesh.X[i2] - mesh.X[i0];
            var by = mesh.Y[i2] - mesh.Y[i0];
            var area = Math.Abs(ax * by - ay * bx) / 2.0;

            var avgZ = (mesh.Z[i0] + mesh.Z[i1] + mesh.Z[i2]) / 3.0;
            var diff = referenceHeight - avgZ;

            if (diff > 0)
                fill += area * diff;
            else
                cut += area * Math.Abs(diff);
        }

        return (fill, cut);
    }

    #region Mesh-Helpers

    private static TerrainMesh BuildMesh(double[] x, double[] y, double[] z, int[] triangles)
    {
        var (nx, ny, nz) = ComputeFaceNormals(x, y, z, triangles);

        return new TerrainMesh
        {
            X = x,
            Y = y,
            Z = z,
            Triangles = triangles,
            NormalsX = nx,
            NormalsY = ny,
            NormalsZ = nz,
            MinX = x.Length > 0 ? x.Min() : 0,
            MaxX = x.Length > 0 ? x.Max() : 0,
            MinY = y.Length > 0 ? y.Min() : 0,
            MaxY = y.Length > 0 ? y.Max() : 0,
            MinZ = z.Length > 0 ? z.Min() : 0,
            MaxZ = z.Length > 0 ? z.Max() : 0,
        };
    }

    /// <summary>Pro Dreieck eine normierte Flächen-Normale — einmalig berechnet, Renderer cached.</summary>
    private static (float[] nx, float[] ny, float[] nz) ComputeFaceNormals(
        double[] x, double[] y, double[] z, int[] triangles)
    {
        var count = triangles.Length / 3;
        if (count == 0) return ([], [], []);

        var nx = new float[count];
        var ny = new float[count];
        var nz = new float[count];

        for (int t = 0; t < count; t++)
        {
            var i0 = triangles[t * 3];
            var i1 = triangles[t * 3 + 1];
            var i2 = triangles[t * 3 + 2];

            var ax = x[i1] - x[i0];
            var ay = y[i1] - y[i0];
            var az = z[i1] - z[i0];
            var bx = x[i2] - x[i0];
            var by = y[i2] - y[i0];
            var bz = z[i2] - z[i0];

            var cx = ay * bz - az * by;
            var cy = az * bx - ax * bz;
            var cz = ax * by - ay * bx;

            var len = Math.Sqrt(cx * cx + cy * cy + cz * cz);
            if (len < 1e-12)
            {
                // Degeneriertes Dreieck — Normale nach oben als Fallback
                nx[t] = 0; ny[t] = 0; nz[t] = 1;
            }
            else
            {
                nx[t] = (float)(cx / len);
                ny[t] = (float)(cy / len);
                nz[t] = (float)(cz / len);
            }
        }

        return (nx, ny, nz);
    }

    /// <summary>
    /// Entfernt Punkte die näher als <see cref="PointMergeEpsilon"/> beieinander liegen.
    /// Wichtig für Delaunay-Stabilität — quasi-kollineare Punkte aus RTK-Streuung
    /// lassen die Circumcircle-Determinante numerisch instabil werden.
    /// </summary>
    private static (double[] x, double[] y, double[] z) DeduplicatePoints(
        double[] x, double[] y, double[] z)
    {
        var keep = new List<(double x, double y, double z)>(x.Length);
        var epsSq = PointMergeEpsilon * PointMergeEpsilon;

        for (int i = 0; i < x.Length; i++)
        {
            bool duplicate = false;
            foreach (var p in keep)
            {
                var dx = x[i] - p.x;
                var dy = y[i] - p.y;
                if (dx * dx + dy * dy < epsSq)
                {
                    duplicate = true;
                    break;
                }
            }
            if (!duplicate)
                keep.Add((x[i], y[i], z[i]));
        }

        var rx = new double[keep.Count];
        var ry = new double[keep.Count];
        var rz = new double[keep.Count];
        for (int i = 0; i < keep.Count; i++)
        {
            rx[i] = keep[i].x;
            ry[i] = keep[i].y;
            rz[i] = keep[i].z;
        }
        return (rx, ry, rz);
    }

    /// <summary>
    /// Stellt sicher dass alle Dreiecke CCW orientiert sind (positive signed area in XY).
    /// Delaunay-Circumcircle-Test setzt CCW voraus — sonst falsches Vorzeichen.
    /// </summary>
    private static void NormalizeWinding(double[] x, double[] y, int[] triangles)
    {
        for (int t = 0; t < triangles.Length; t += 3)
        {
            var a = triangles[t];
            var b = triangles[t + 1];
            var c = triangles[t + 2];

            var signedArea = (x[b] - x[a]) * (y[c] - y[a]) - (x[c] - x[a]) * (y[b] - y[a]);
            if (signedArea < 0)
            {
                // CW → Swap b und c für CCW
                triangles[t + 1] = c;
                triangles[t + 2] = b;
            }
        }
    }

    #endregion

    #region Bowyer-Watson Delaunay

    /// <summary>Bowyer-Watson inkrementelle Delaunay-Triangulierung (O(n²) Worst Case)</summary>
    private static int[] BowyerWatson(double[] x, double[] y)
    {
        int n = x.Length;
        var triangles = new List<(int a, int b, int c)>();

        // Super-Triangle gross genug damit alle Punkte darin liegen
        var minX = x.Min() - 1;
        var maxX = x.Max() + 1;
        var minY = y.Min() - 1;
        var maxY = y.Max() + 1;
        var dx = maxX - minX;
        var dy = maxY - minY;
        var dMax = Math.Max(dx, dy) * 10; // Faktor 10 statt 2 — robuster gegen enge Point-Sets

        var allX = new double[n + 3];
        var allY = new double[n + 3];
        Array.Copy(x, allX, n);
        Array.Copy(y, allY, n);
        allX[n] = minX - dMax; allY[n] = minY - dMax;
        allX[n + 1] = minX + dx / 2; allY[n + 1] = maxY + dMax;
        allX[n + 2] = maxX + dMax; allY[n + 2] = minY - dMax;

        // Super-Triangle explizit CCW (gegen den Uhrzeigersinn in mathematischem Y-up)
        triangles.Add(OrientCcw(allX, allY, n, n + 1, n + 2));

        // Einmaliges stackalloc vor allen Schleifen — pro Dreieck haben wir immer
        // genau 3 Kanten, der Span kann also wiederverwendet werden.
        Span<(int a, int b)> edges = stackalloc (int a, int b)[3];
        var badTriangles = new List<int>(16);

        for (int i = 0; i < n; i++)
        {
            badTriangles.Clear();

            for (int t = 0; t < triangles.Count; t++)
            {
                var (a, b, c) = triangles[t];
                if (IsInCircumcircle(allX[i], allY[i],
                    allX[a], allY[a], allX[b], allY[b], allX[c], allY[c]))
                {
                    badTriangles.Add(t);
                }
            }

            // Polygon-Loch: Kanten die nur in EINEM bad triangle vorkommen
            var polygon = new List<(int a, int b)>();
            for (int bi = 0; bi < badTriangles.Count; bi++)
            {
                var t = badTriangles[bi];
                var (a, b, c) = triangles[t];
                edges[0] = (a, b);
                edges[1] = (b, c);
                edges[2] = (c, a);

                for (int ei = 0; ei < 3; ei++)
                {
                    var edge = edges[ei];
                    bool shared = false;
                    for (int bj = 0; bj < badTriangles.Count && !shared; bj++)
                    {
                        if (bj == bi) continue;
                        var (a2, b2, c2) = triangles[badTriangles[bj]];
                        shared = SharesEdge(edge, a2, b2, c2);
                    }
                    if (!shared)
                        polygon.Add(edge);
                }
            }

            // Bad Triangles rückwärts entfernen damit Indizes stabil bleiben
            badTriangles.Sort();
            for (int t = badTriangles.Count - 1; t >= 0; t--)
                triangles.RemoveAt(badTriangles[t]);

            // Neue Dreiecke aus Polygon-Kanten zum neuen Punkt — jeweils CCW orientiert
            foreach (var (a, b) in polygon)
                triangles.Add(OrientCcw(allX, allY, i, a, b));
        }

        // Super-Triangle-referenzierende Dreiecke entfernen
        triangles.RemoveAll(t => t.a >= n || t.b >= n || t.c >= n);

        var result = new int[triangles.Count * 3];
        for (int t = 0; t < triangles.Count; t++)
        {
            result[t * 3] = triangles[t].a;
            result[t * 3 + 1] = triangles[t].b;
            result[t * 3 + 2] = triangles[t].c;
        }

        return result;
    }

    /// <summary>Gibt das Tripel in CCW-Reihenfolge zurück (positive signed area).</summary>
    private static (int a, int b, int c) OrientCcw(double[] x, double[] y, int a, int b, int c)
    {
        var signedArea = (x[b] - x[a]) * (y[c] - y[a]) - (x[c] - x[a]) * (y[b] - y[a]);
        return signedArea >= 0 ? (a, b, c) : (a, c, b);
    }

    /// <summary>
    /// Circumcircle-Test mit Epsilon-Toleranz. Voraussetzung: CCW-Orientierung.
    /// Positive Determinante (+ Epsilon) = Punkt strikt innerhalb Umkreis.
    /// </summary>
    private static bool IsInCircumcircle(double px, double py,
        double ax, double ay, double bx, double by, double cx, double cy)
    {
        var dx = ax - px;
        var dy = ay - py;
        var ex = bx - px;
        var ey = by - py;
        var fx = cx - px;
        var fy = cy - py;

        var ap = dx * dx + dy * dy;
        var bp = ex * ex + ey * ey;
        var cp = fx * fx + fy * fy;

        var det = dx * (ey * cp - bp * fy)
                - dy * (ex * cp - bp * fx)
                + ap * (ex * fy - ey * fx);

        // Epsilon > 0 statt > Toleranz — sehr knappe Punkte werden NICHT als "drinnen"
        // gezählt, was Endless-Loops bei fast-kollinearen Konfigurationen vermeidet.
        return det > 1e-12;
    }

    private static bool SharesEdge((int a, int b) edge, int a2, int b2, int c2)
    {
        return (edge.a == a2 && edge.b == b2) || (edge.a == b2 && edge.b == a2) ||
               (edge.a == b2 && edge.b == c2) || (edge.a == c2 && edge.b == b2) ||
               (edge.a == c2 && edge.b == a2) || (edge.a == a2 && edge.b == c2);
    }

    #endregion

    #region Helper-Methoden

    private static (double u, double v, double w) Barycentric(
        double px, double py,
        double ax, double ay, double bx, double by, double cx, double cy)
    {
        var v0x = cx - ax; var v0y = cy - ay;
        var v1x = bx - ax; var v1y = by - ay;
        var v2x = px - ax; var v2y = py - ay;

        var dot00 = v0x * v0x + v0y * v0y;
        var dot01 = v0x * v1x + v0y * v1y;
        var dot02 = v0x * v2x + v0y * v2y;
        var dot11 = v1x * v1x + v1y * v1y;
        var dot12 = v1x * v2x + v1y * v2y;

        var denom = dot00 * dot11 - dot01 * dot01;
        if (Math.Abs(denom) < 1e-18)
        {
            // Degenerierte Dreiecksfläche → Punkt nicht zuweisbar
            return (-1, -1, -1);
        }

        var invDenom = 1.0 / denom;
        var u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        var v = (dot00 * dot12 - dot01 * dot02) * invDenom;

        return (1 - u - v, v, u);
    }

    private static void TryAddEdgeIntersection(
        List<(double x, double y)> intersections, double h,
        double x0, double y0, double z0,
        double x1, double y1, double z1)
    {
        // Keine Kreuzung wenn beide Enden oberhalb oder beide unterhalb sind
        if ((z0 < h && z1 < h) || (z0 > h && z1 > h)) return;

        // Kante horizontal auf Höhe h — Kanten-Mittelpunkt wäre beliebig, überspringen
        if (Math.Abs(z1 - z0) < 1e-10) return;

        var t = (h - z0) / (z1 - z0);
        if (t < 0 || t > 1) return;

        var x = x0 + t * (x1 - x0);
        var y = y0 + t * (y1 - y0);

        // Dedup: Intersection auf gemeinsamem Vertex (schon bei vorheriger Kante erfasst)
        for (int i = 0; i < intersections.Count; i++)
        {
            var dx = intersections[i].x - x;
            var dy = intersections[i].y - y;
            if (dx * dx + dy * dy < 1e-14) return;
        }

        intersections.Add((x, y));
    }

    private static double PerturbContourHeight(TerrainMesh mesh, double h)
    {
        for (int i = 0; i < mesh.Z.Length; i++)
        {
            if (Math.Abs(mesh.Z[i] - h) < ContourVertexEpsilon)
                return h + ContourVertexEpsilon;
        }
        return h;
    }

    private static ((double x, double y) a, (double x, double y) b) PickLongestSegment(
        List<(double x, double y)> pts)
    {
        // 3-Punkt-Fall: die zwei weitest auseinander liegenden nehmen
        double maxDist = -1;
        int ai = 0, bi = 1;
        for (int i = 0; i < pts.Count; i++)
        for (int j = i + 1; j < pts.Count; j++)
        {
            var dx = pts[i].x - pts[j].x;
            var dy = pts[i].y - pts[j].y;
            var d = dx * dx + dy * dy;
            if (d > maxDist)
            {
                maxDist = d;
                ai = i;
                bi = j;
            }
        }
        return (pts[ai], pts[bi]);
    }

    /// <summary>
    /// Andrew's Monotone Chain — O(n log n) Convex Hull in CCW-Reihenfolge.
    /// Voraussetzung für Shoelace-Flächenformel bei ungeordneten Mess-Punkten.
    /// </summary>
    private static List<(double x, double y)> ConvexHull(double[] x, double[] y)
    {
        var points = new (double x, double y)[x.Length];
        for (int i = 0; i < x.Length; i++) points[i] = (x[i], y[i]);

        // Sortieren: X aufsteigend, bei Gleichstand Y aufsteigend
        Array.Sort(points, (p, q) => p.x != q.x
            ? p.x.CompareTo(q.x)
            : p.y.CompareTo(q.y));

        // Duplikate rausfiltern (selbes X+Y)
        var unique = new List<(double x, double y)>(points.Length);
        for (int i = 0; i < points.Length; i++)
        {
            if (i > 0 && Math.Abs(points[i].x - points[i - 1].x) < 1e-12
                      && Math.Abs(points[i].y - points[i - 1].y) < 1e-12) continue;
            unique.Add(points[i]);
        }

        if (unique.Count < 3) return unique;

        var n = unique.Count;
        var hull = new List<(double x, double y)>(2 * n);

        // Lower hull
        for (int i = 0; i < n; i++)
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], unique[i]) <= 0)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(unique[i]);
        }

        // Upper hull
        var lowerCount = hull.Count + 1;
        for (int i = n - 2; i >= 0; i--)
        {
            while (hull.Count >= lowerCount && Cross(hull[^2], hull[^1], unique[i]) <= 0)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(unique[i]);
        }

        hull.RemoveAt(hull.Count - 1); // letzter Punkt = erster
        return hull;
    }

    private static double Cross((double x, double y) o, (double x, double y) a, (double x, double y) b)
        => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

    #endregion
}
