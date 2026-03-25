using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Bowyer-Watson Delaunay-Triangulierung + Konturlinien (Marching Squares)</summary>
public class TerrainService : ITerrainService
{
    public TerrainMesh CreateMesh(double[] x, double[] y, double[] z)
    {
        if (x.Length < 3)
            return new TerrainMesh { X = x, Y = y, Z = z };

        var triangles = BowyerWatson(x, y);

        var mesh = new TerrainMesh
        {
            X = x,
            Y = y,
            Z = z,
            Triangles = triangles,
            MinX = x.Min(),
            MaxX = x.Max(),
            MinY = y.Min(),
            MaxY = y.Max(),
            MinZ = z.Min(),
            MaxZ = z.Max()
        };

        return mesh;
    }

    public List<ContourLine> CreateContourLines(TerrainMesh mesh, double interval)
    {
        var contours = new List<ContourLine>();
        if (mesh.TriangleCount == 0 || interval <= 0) return contours;

        // Hoehenstufen berechnen
        var minH = Math.Floor(mesh.MinZ / interval) * interval;
        var maxH = Math.Ceiling(mesh.MaxZ / interval) * interval;

        for (var h = minH; h <= maxH; h += interval)
        {
            var contour = new ContourLine { Height = h };

            // Jedes Dreieck pruefen ob die Hoehenlinie durchgeht
            for (int t = 0; t < mesh.TriangleCount; t++)
            {
                var i0 = mesh.Triangles[t * 3];
                var i1 = mesh.Triangles[t * 3 + 1];
                var i2 = mesh.Triangles[t * 3 + 2];

                var z0 = mesh.Z[i0];
                var z1 = mesh.Z[i1];
                var z2 = mesh.Z[i2];

                // Schnittpunkte der Hoehe h mit den Dreieckskanten finden
                var intersections = new List<(float x, float y)>();
                TryAddEdgeIntersection(intersections, h,
                    mesh.X[i0], mesh.Y[i0], z0, mesh.X[i1], mesh.Y[i1], z1);
                TryAddEdgeIntersection(intersections, h,
                    mesh.X[i1], mesh.Y[i1], z1, mesh.X[i2], mesh.Y[i2], z2);
                TryAddEdgeIntersection(intersections, h,
                    mesh.X[i2], mesh.Y[i2], z2, mesh.X[i0], mesh.Y[i0], z0);

                if (intersections.Count >= 2)
                {
                    contour.Segments.Add((
                        intersections[0].x, intersections[0].y,
                        intersections[1].x, intersections[1].y));
                }
            }

            if (contour.Segments.Count > 0)
                contours.Add(contour);
        }

        return contours;
    }

    public double? InterpolateHeight(TerrainMesh mesh, double px, double py)
    {
        // Dreieck finden das den Punkt enthaelt, dann barizentrisch interpolieren
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

            if (u >= -0.001 && v >= -0.001 && w >= -0.001) // Punkt liegt im Dreieck
                return u * mesh.Z[i0] + v * mesh.Z[i1] + w * mesh.Z[i2];
        }

        return null;
    }

    public double CalculateArea2D(double[] x, double[] y)
    {
        if (x.Length < 3) return 0;

        // Shoelace-Formel
        double area = 0;
        for (int i = 0; i < x.Length; i++)
        {
            int j = (i + 1) % x.Length;
            area += x[i] * y[j];
            area -= x[j] * y[i];
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

            // Heron's Formel fuer 3D-Dreiecksflaeche
            var ax = mesh.X[i1] - mesh.X[i0];
            var ay = mesh.Y[i1] - mesh.Y[i0];
            var az = mesh.Z[i1] - mesh.Z[i0];
            var bx = mesh.X[i2] - mesh.X[i0];
            var by = mesh.Y[i2] - mesh.Y[i0];
            var bz = mesh.Z[i2] - mesh.Z[i0];

            // Kreuzprodukt
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

            // Dreieck-Grundflaeche (2D)
            var ax = mesh.X[i1] - mesh.X[i0];
            var ay = mesh.Y[i1] - mesh.Y[i0];
            var bx = mesh.X[i2] - mesh.X[i0];
            var by = mesh.Y[i2] - mesh.Y[i0];
            var area = Math.Abs(ax * by - ay * bx) / 2.0;

            // Mittlere Hoehendifferenz zur Referenz
            var avgZ = (mesh.Z[i0] + mesh.Z[i1] + mesh.Z[i2]) / 3.0;
            var diff = referenceHeight - avgZ;

            if (diff > 0)
                fill += area * diff;  // Aufschuettung noetig
            else
                cut += area * Math.Abs(diff); // Abtrag noetig
        }

        return (fill, cut);
    }

    #region Bowyer-Watson Delaunay

    /// <summary>Bowyer-Watson inkrementelle Delaunay-Triangulierung</summary>
    private static int[] BowyerWatson(double[] x, double[] y)
    {
        int n = x.Length;
        var triangles = new List<(int a, int b, int c)>();

        // Super-Triangle (umschliesst alle Punkte)
        var minX = x.Min() - 1;
        var maxX = x.Max() + 1;
        var minY = y.Min() - 1;
        var maxY = y.Max() + 1;
        var dx = maxX - minX;
        var dy = maxY - minY;
        var dMax = Math.Max(dx, dy) * 2;

        // Super-Triangle Vertices (Indizes n, n+1, n+2)
        var sx = new double[] { minX - dMax, minX + dx / 2, maxX + dMax };
        var sy = new double[] { minY - dMax, maxY + dMax, minY - dMax };

        // Erweiterte Koordinaten-Arrays (Original + Super-Triangle)
        var allX = new double[n + 3];
        var allY = new double[n + 3];
        Array.Copy(x, allX, n);
        Array.Copy(y, allY, n);
        allX[n] = sx[0]; allY[n] = sy[0];
        allX[n + 1] = sx[1]; allY[n + 1] = sy[1];
        allX[n + 2] = sx[2]; allY[n + 2] = sy[2];

        triangles.Add((n, n + 1, n + 2));

        // Punkte inkrementell einfuegen
        for (int i = 0; i < n; i++)
        {
            var badTriangles = new List<int>();

            // Finde alle Dreiecke deren Umkreis den neuen Punkt enthaelt
            for (int t = 0; t < triangles.Count; t++)
            {
                var (a, b, c) = triangles[t];
                if (IsInCircumcircle(allX[i], allY[i],
                    allX[a], allY[a], allX[b], allY[b], allX[c], allY[c]))
                {
                    badTriangles.Add(t);
                }
            }

            // Polygon-Loch finden (Kanten die nur einmal vorkommen)
            var polygon = new List<(int a, int b)>();
            foreach (var t in badTriangles)
            {
                var (a, b, c) = triangles[t];
                var edges = new[] { (a, b), (b, c), (c, a) };

                foreach (var edge in edges)
                {
                    bool shared = false;
                    foreach (var t2 in badTriangles)
                    {
                        if (t2 == t) continue;
                        var (a2, b2, c2) = triangles[t2];
                        if (SharesEdge(edge, a2, b2, c2))
                        {
                            shared = true;
                            break;
                        }
                    }
                    if (!shared)
                        polygon.Add(edge);
                }
            }

            // Bad Triangles entfernen (rueckwaerts)
            badTriangles.Sort();
            for (int t = badTriangles.Count - 1; t >= 0; t--)
                triangles.RemoveAt(badTriangles[t]);

            // Neue Dreiecke aus Polygon-Kanten zum neuen Punkt
            foreach (var (a, b) in polygon)
                triangles.Add((i, a, b));
        }

        // Super-Triangle Dreiecke entfernen
        triangles.RemoveAll(t => t.a >= n || t.b >= n || t.c >= n);

        // In flaches Array konvertieren
        var result = new int[triangles.Count * 3];
        for (int t = 0; t < triangles.Count; t++)
        {
            result[t * 3] = triangles[t].a;
            result[t * 3 + 1] = triangles[t].b;
            result[t * 3 + 2] = triangles[t].c;
        }

        return result;
    }

    /// <summary>Prueft ob Punkt (px,py) im Umkreis des Dreiecks liegt</summary>
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

        return det > 0;
    }

    /// <summary>Prueft ob eine Kante mit einem Dreieck geteilt wird</summary>
    private static bool SharesEdge((int a, int b) edge, int a2, int b2, int c2)
    {
        var edges2 = new[] { (a2, b2), (b2, c2), (c2, a2) };
        foreach (var e2 in edges2)
        {
            if ((edge.a == e2.Item1 && edge.b == e2.Item2) ||
                (edge.a == e2.Item2 && edge.b == e2.Item1))
                return true;
        }
        return false;
    }

    #endregion

    #region Hilfsmethoden

    /// <summary>Barizentrische Koordinaten fuer Punkt P im Dreieck ABC</summary>
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

        var invDenom = 1.0 / (dot00 * dot11 - dot01 * dot01);
        var u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        var v = (dot00 * dot12 - dot01 * dot02) * invDenom;

        return (1 - u - v, v, u);
    }

    /// <summary>Schnittpunkt einer Hoehenlinie mit einer Dreieckskante</summary>
    private static void TryAddEdgeIntersection(
        List<(float x, float y)> intersections, double h,
        double x0, double y0, double z0,
        double x1, double y1, double z1)
    {
        // Pruefe ob h zwischen z0 und z1 liegt
        if ((z0 < h && z1 < h) || (z0 > h && z1 > h) || Math.Abs(z1 - z0) < 1e-10)
            return;

        var t = (h - z0) / (z1 - z0);
        if (t < 0 || t > 1) return;

        intersections.Add(((float)(x0 + t * (x1 - x0)), (float)(y0 + t * (y1 - y0))));
    }

    #endregion
}
