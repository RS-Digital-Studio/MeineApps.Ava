using System.Globalization;
using System.Text;
using System.Text.Json;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>
/// Wavefront OBJ+MTL Export für Blender.
///
/// Koordinatensystem: Wir exportieren in Standard-Blender-Konvention
/// (X=rechts, Y=forward, Z=up, right-handed) — 1:1 identisch mit unseren
/// UTM-Koordinaten (X=Ost, Y=Nord, Z=Höhe). Kein Swap, kein Flip. Damit sind
/// Normalen, Winding und Orientierung beim Standard-OBJ-Import korrekt.
///
/// Flächenelemente nutzen Ear-Clipping-Triangulation (funktioniert für konkave
/// Polygone — Fan-Triangulation hätte bei L-Formen oder Hufeisen falsche
/// Dreiecke erzeugt).
/// </summary>
public class BlenderExportService : IBlenderExportService
{
    // Höhen-Farbverlauf analog zu TerrainRenderer
    private static readonly (double r, double g, double b)[] HeightColors =
    [
        (27 / 255.0, 94 / 255.0, 32 / 255.0),
        (76 / 255.0, 175 / 255.0, 80 / 255.0),
        (253 / 255.0, 216 / 255.0, 53 / 255.0),
        (255 / 255.0, 143 / 255.0, 0 / 255.0),
        (93 / 255.0, 64 / 255.0, 55 / 255.0)
    ];

    // Material-Farben für Gartenelemente (Kd-Werte)
    private static readonly Dictionary<GardenElementType, (double r, double g, double b)> MaterialColors = new()
    {
        [GardenElementType.Weg] = (0.6, 0.6, 0.6),
        [GardenElementType.Beet] = (0.5, 0.3, 0.1),
        [GardenElementType.Rasen] = (0.2, 0.7, 0.2),
        [GardenElementType.Mauer] = (0.7, 0.7, 0.7),
        [GardenElementType.Zaun] = (0.5, 0.35, 0.2),
        [GardenElementType.Terrasse] = (0.7, 0.6, 0.5),
        [GardenElementType.Grenze] = (1.0, 0.4, 0.0),
        [GardenElementType.Gebaeude] = (0.55, 0.55, 0.6),
        [GardenElementType.Wasser] = (0.2, 0.5, 0.8),
        [GardenElementType.Kante] = (0.8, 0.8, 0.2),
    };

    private const float DefaultWegWidth = 1.2f;
    private const float DefaultMauerWidth = 0.3f;
    private const float DefaultMauerHeight = 1.0f;
    private const float DefaultZaunWidth = 0.1f;
    private const float DefaultZaunHeight = 1.2f;

    public async Task<string> ExportObjAsync(TerrainMesh mesh, List<GardenElement> elements,
        string outputDir, string projectName)
    {
        Directory.CreateDirectory(outputDir);

        var objPath = Path.Combine(outputDir, $"{projectName}.obj");
        var mtlPath = Path.Combine(outputDir, $"{projectName}.mtl");

        await WriteMtlFileAsync(mtlPath, elements);

        var obj = new StringBuilder();
        WriteObjHeader(obj, mesh, projectName);

        // OBJ ist 1-basiert, Objekte teilen sich globalen Vertex/Normal-Pool
        var ctx = new ObjWriteContext { VertexOffset = 1, NormalOffset = 1 };

        WriteTerrainObject(obj, mesh, ctx);

        var elementCounts = new Dictionary<GardenElementType, int>();
        foreach (var element in elements)
        {
            var points = ParsePoints(element.PointsJson);
            if (points.Count < 2) continue;

            elementCounts.TryGetValue(element.ElementType, out var count);
            count++;
            elementCounts[element.ElementType] = count;

            var objectName = $"{element.ElementType}_{count}";
            WriteGardenElementObject(obj, element, points, objectName, ctx);
        }

        await File.WriteAllTextAsync(objPath, obj.ToString(), Encoding.UTF8);
        return objPath;
    }

    public async Task<string> ExportTerrainObjAsync(TerrainMesh mesh, string outputDir, string projectName)
    {
        Directory.CreateDirectory(outputDir);

        var objPath = Path.Combine(outputDir, $"{projectName}.obj");
        var mtlPath = Path.Combine(outputDir, $"{projectName}.mtl");

        await WriteTerrainOnlyMtlAsync(mtlPath);

        var obj = new StringBuilder();
        WriteObjHeader(obj, mesh, projectName);
        var ctx = new ObjWriteContext { VertexOffset = 1, NormalOffset = 1 };
        WriteTerrainObject(obj, mesh, ctx);

        await File.WriteAllTextAsync(objPath, obj.ToString(), Encoding.UTF8);
        return objPath;
    }

    #region OBJ Generation

    /// <summary>Hält Vertex-/Normalen-Zähler über Objekte hinweg.</summary>
    private sealed class ObjWriteContext
    {
        public int VertexOffset;
        public int NormalOffset;
    }

    private static void WriteObjHeader(StringBuilder obj, TerrainMesh mesh, string projectName)
    {
        obj.AppendLine($"# SmartMeasure Export - {projectName}");
        obj.AppendLine($"# Vertices: {mesh.VertexCount}, Triangles: {mesh.TriangleCount}");
        obj.AppendLine(CultureInfo.InvariantCulture,
            $"# Bounds: X[{mesh.MinX:F2}..{mesh.MaxX:F2}] Y[{mesh.MinY:F2}..{mesh.MaxY:F2}] Z[{mesh.MinZ:F2}..{mesh.MaxZ:F2}]");
        obj.AppendLine("# Koordinatensystem: Z-up (Blender-Standard), X=Ost, Y=Nord, Z=Höhe");
        obj.AppendLine($"mtllib {projectName}.mtl");
        obj.AppendLine();
    }

    private static void WriteTerrainObject(StringBuilder obj, TerrainMesh mesh, ObjWriteContext ctx)
    {
        obj.AppendLine("o Terrain");
        obj.AppendLine("usemtl Terrain");

        // Vertices mit Vertex-Farben (OBJ Extension: v x y z r g b)
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            var x = mesh.X[i];
            var y = mesh.Y[i];
            var z = mesh.Z[i];

            var zNorm = mesh.MaxZ > mesh.MinZ
                ? (z - mesh.MinZ) / (mesh.MaxZ - mesh.MinZ)
                : 0.5;
            var (cr, cg, cb) = InterpolateHeightColor(zNorm);

            obj.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "v {0:F6} {1:F6} {2:F6} {3:F4} {4:F4} {5:F4}",
                x, y, z, cr, cg, cb));
        }

        // Face-Normalen (flat shading) — Mesh hat sie bereits vorberechnet, einfach rausschreiben
        if (mesh.NormalsX.Length == mesh.TriangleCount)
        {
            for (int t = 0; t < mesh.TriangleCount; t++)
            {
                obj.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "vn {0:F6} {1:F6} {2:F6}",
                    mesh.NormalsX[t], mesh.NormalsY[t], mesh.NormalsZ[t]));
            }
        }
        else
        {
            // Fallback wenn Mesh ohne vorberechnete Normalen kommt (sollte nicht passieren)
            for (int t = 0; t < mesh.TriangleCount; t++)
            {
                var i0 = mesh.Triangles[t * 3];
                var i1 = mesh.Triangles[t * 3 + 1];
                var i2 = mesh.Triangles[t * 3 + 2];

                var e1x = mesh.X[i1] - mesh.X[i0];
                var e1y = mesh.Y[i1] - mesh.Y[i0];
                var e1z = mesh.Z[i1] - mesh.Z[i0];
                var e2x = mesh.X[i2] - mesh.X[i0];
                var e2y = mesh.Y[i2] - mesh.Y[i0];
                var e2z = mesh.Z[i2] - mesh.Z[i0];

                var nx = e1y * e2z - e1z * e2y;
                var ny = e1z * e2x - e1x * e2z;
                var nz = e1x * e2y - e1y * e2x;

                var len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len > 0) { nx /= len; ny /= len; nz /= len; }
                else { nx = 0; ny = 0; nz = 1; }

                obj.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "vn {0:F6} {1:F6} {2:F6}",
                    nx, ny, nz));
            }
        }

        // Faces (1-basiert, mit Normalen-Index)
        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            var i0 = mesh.Triangles[t * 3] + ctx.VertexOffset;
            var i1 = mesh.Triangles[t * 3 + 1] + ctx.VertexOffset;
            var i2 = mesh.Triangles[t * 3 + 2] + ctx.VertexOffset;
            var normalIdx = t + ctx.NormalOffset;

            obj.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "f {0}//{1} {2}//{3} {4}//{5}",
                i0, normalIdx, i1, normalIdx, i2, normalIdx));
        }

        obj.AppendLine();

        ctx.VertexOffset += mesh.VertexCount;
        ctx.NormalOffset += mesh.TriangleCount;
    }

    private static void WriteGardenElementObject(StringBuilder obj, GardenElement element,
        List<(double x, double y)> points, string objectName, ObjWriteContext ctx)
    {
        obj.AppendLine($"o {objectName}");
        obj.AppendLine($"usemtl {element.ElementType}");

        switch (element.ElementType)
        {
            case GardenElementType.Beet:
            case GardenElementType.Rasen:
            case GardenElementType.Terrasse:
                WriteFlatPolygon(obj, points, element, ctx);
                break;

            case GardenElementType.Weg:
            case GardenElementType.Mauer:
            case GardenElementType.Zaun:
                WriteExtrudedPolyline(obj, points, element, ctx);
                break;
        }
    }

    /// <summary>Flächen-Element: Ear-Clipping-Triangulation (funktioniert für konkave Polygone).</summary>
    private static void WriteFlatPolygon(StringBuilder obj, List<(double x, double y)> points,
        GardenElement element, ObjWriteContext ctx)
    {
        if (points.Count < 3) return;

        var height = element.ElementType switch
        {
            GardenElementType.Terrasse when element.TargetAltitude > 0 => element.TargetAltitude,
            GardenElementType.Gebaeude when element.Height > 0 => (double)element.Height,
            _ => 0.01
        };

        // Vertices: X=Ost, Y=Nord, Z=Höhe (Blender-Standard Z-up)
        foreach (var (px, py) in points)
        {
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "v {0:F6} {1:F6} {2:F6}",
                px, py, height));
        }

        // Normale zeigt in +Z (Blender up)
        obj.AppendLine("vn 0.000000 0.000000 1.000000");
        var normalIdx = ctx.NormalOffset;

        // Ear-Clipping-Triangulation — robust auch bei konkaven Polygonen
        var triangles = EarClippingTriangulate(points);

        foreach (var (a, b, c) in triangles)
        {
            var i0 = ctx.VertexOffset + a;
            var i1 = ctx.VertexOffset + b;
            var i2 = ctx.VertexOffset + c;

            obj.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "f {0}//{1} {2}//{3} {4}//{5}",
                i0, normalIdx, i1, normalIdx, i2, normalIdx));
        }

        obj.AppendLine();

        ctx.VertexOffset += points.Count;
        ctx.NormalOffset += 1;
    }

    /// <summary>
    /// Box-extrudierte Polylinie. Pro Segment 8 Vertices + 6 Normalen + 12 Dreiecke (6 Faces × 2).
    /// Alle Faces CCW von außen betrachtet, Normalen zeigen nach außen → Blender-Backface-Culling OK.
    /// </summary>
    private static void WriteExtrudedPolyline(StringBuilder obj, List<(double x, double y)> points,
        GardenElement element, ObjWriteContext ctx)
    {
        if (points.Count < 2) return;

        var width = element.Width > 0 ? element.Width : element.ElementType switch
        {
            GardenElementType.Weg => DefaultWegWidth,
            GardenElementType.Mauer => DefaultMauerWidth,
            GardenElementType.Zaun => DefaultZaunWidth,
            _ => 0.5f
        };

        var height = element.Height > 0 ? element.Height : element.ElementType switch
        {
            GardenElementType.Mauer => DefaultMauerHeight,
            GardenElementType.Zaun => DefaultZaunHeight,
            GardenElementType.Weg => 0.05f,
            _ => 0.1f
        };

        var halfWidth = width / 2.0;
        var segmentVertexCount = 0;
        var segmentCount = 0;

        // 1. Pass: Vertices + Normalen schreiben
        for (int i = 0; i < points.Count - 1; i++)
        {
            var (x1, y1) = points[i];
            var (x2, y2) = points[i + 1];

            var dx = x2 - x1;
            var dy = y2 - y1;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001) continue;

            var perpX = -dy / len * halfWidth;
            var perpY = dx / len * halfWidth;

            // 8 Vertices (Z-up Blender-Standard):
            // 0: start+perp bottom, 1: start-perp bottom, 2: end-perp bottom, 3: end+perp bottom
            // 4: start+perp top,    5: start-perp top,    6: end-perp top,    7: end+perp top
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} 0.000000", x1 + perpX, y1 + perpY));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} 0.000000", x1 - perpX, y1 - perpY));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} 0.000000", x2 - perpX, y2 - perpY));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} 0.000000", x2 + perpX, y2 + perpY));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", x1 + perpX, y1 + perpY, (double)height));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", x1 - perpX, y1 - perpY, (double)height));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", x2 - perpX, y2 - perpY, (double)height));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", x2 + perpX, y2 + perpY, (double)height));

            // 6 Face-Normalen (top, bottom, right, left, back, front)
            var segDirX = dx / len;
            var segDirY = dy / len;
            var perpUnitX = -dy / len;
            var perpUnitY = dx / len;

            obj.AppendLine("vn 0.000000 0.000000 1.000000");
            obj.AppendLine("vn 0.000000 0.000000 -1.000000");
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "vn {0:F6} {1:F6} 0.000000", perpUnitX, perpUnitY));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "vn {0:F6} {1:F6} 0.000000", -perpUnitX, -perpUnitY));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "vn {0:F6} {1:F6} 0.000000", -segDirX, -segDirY));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "vn {0:F6} {1:F6} 0.000000", segDirX, segDirY));

            // Vertex/Normal-Indices (1-basiert)
            var vB = ctx.VertexOffset + segmentVertexCount;
            var nB = ctx.NormalOffset + segmentCount * 6;

            // Top-Face (+Z, Normal 0): CCW von oben = (4, 5, 6, 7) gegen den Uhrzeigersinn
            AppendQuad(obj, vB + 4, vB + 7, vB + 6, vB + 5, nB + 0);
            // Bottom-Face (-Z, Normal 1): CCW von unten = (0, 1, 2, 3)
            AppendQuad(obj, vB + 0, vB + 1, vB + 2, vB + 3, nB + 1);
            // Right-Face (+perp, Normal 2): Vertices 0, 3, 7, 4 (rechte Seite entlang segment)
            AppendQuad(obj, vB + 0, vB + 3, vB + 7, vB + 4, nB + 2);
            // Left-Face (-perp, Normal 3): Vertices 1, 5, 6, 2
            AppendQuad(obj, vB + 1, vB + 5, vB + 6, vB + 2, nB + 3);
            // Start-Cap (-segDir, Normal 4): Vertices 0, 4, 5, 1
            AppendQuad(obj, vB + 0, vB + 4, vB + 5, vB + 1, nB + 4);
            // End-Cap (+segDir, Normal 5): Vertices 3, 2, 6, 7
            AppendQuad(obj, vB + 3, vB + 2, vB + 6, vB + 7, nB + 5);

            segmentVertexCount += 8;
            segmentCount++;
        }

        obj.AppendLine();
        ctx.VertexOffset += segmentVertexCount;
        ctx.NormalOffset += segmentCount * 6;
    }

    /// <summary>Quad als 2 Triangles mit gemeinsamer Normale schreiben (CCW).</summary>
    private static void AppendQuad(StringBuilder obj, int v0, int v1, int v2, int v3, int n)
    {
        obj.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "f {0}//{4} {1}//{4} {2}//{4}",
            v0, v1, v2, 0, n));
        obj.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "f {0}//{4} {1}//{4} {2}//{4}",
            v0, v2, v3, 0, n));
    }

    /// <summary>
    /// Ear-Clipping-Triangulation für 2D-Polygon. Funktioniert für konvexe UND konkave Polygone.
    /// O(n²) im Worst Case, für Gartenelemente (&lt;20 Ecken) absolut ausreichend.
    /// </summary>
    private static List<(int a, int b, int c)> EarClippingTriangulate(List<(double x, double y)> points)
    {
        var n = points.Count;
        var result = new List<(int a, int b, int c)>(Math.Max(n - 2, 1));
        if (n < 3) return result;

        // Orientierung sicherstellen (CCW)
        var indices = Enumerable.Range(0, n).ToList();
        if (SignedArea(points) < 0)
            indices.Reverse();

        var guard = indices.Count * indices.Count; // Endlos-Loop-Schutz
        while (indices.Count > 3 && guard-- > 0)
        {
            var found = false;
            for (int i = 0; i < indices.Count; i++)
            {
                var i0 = indices[(i - 1 + indices.Count) % indices.Count];
                var i1 = indices[i];
                var i2 = indices[(i + 1) % indices.Count];

                var a = points[i0];
                var b = points[i1];
                var c = points[i2];

                // Konvexer Vertex?
                if (Cross(a, b, c) <= 0) continue;

                // Kein anderer Punkt im Dreieck?
                var hasPointInside = false;
                for (int j = 0; j < indices.Count; j++)
                {
                    var jdx = indices[j];
                    if (jdx == i0 || jdx == i1 || jdx == i2) continue;
                    if (PointInTriangle(points[jdx], a, b, c))
                    {
                        hasPointInside = true;
                        break;
                    }
                }
                if (hasPointInside) continue;

                // Ear gefunden!
                result.Add((i0, i1, i2));
                indices.RemoveAt(i);
                found = true;
                break;
            }

            if (!found)
            {
                // Polygon ist degeneriert (selbstüberschneidend?) → Fallback auf Fan
                break;
            }
        }

        if (indices.Count == 3)
            result.Add((indices[0], indices[1], indices[2]));
        else if (indices.Count > 3)
        {
            // Fallback Fan wenn Ear-Clipping nicht fertig wurde
            for (int i = 1; i < indices.Count - 1; i++)
                result.Add((indices[0], indices[i], indices[i + 1]));
        }

        return result;
    }

    private static double SignedArea(List<(double x, double y)> pts)
    {
        double area = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            int j = (i + 1) % pts.Count;
            area += (pts[j].x - pts[i].x) * (pts[j].y + pts[i].y);
        }
        return -area / 2.0;
    }

    private static double Cross((double x, double y) a, (double x, double y) b, (double x, double y) c)
        => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

    private static bool PointInTriangle((double x, double y) p, (double x, double y) a,
        (double x, double y) b, (double x, double y) c)
    {
        var d1 = Cross(p, a, b);
        var d2 = Cross(p, b, c);
        var d3 = Cross(p, c, a);
        var hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        var hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

    #endregion

    #region MTL Generation

    private static async Task WriteMtlFileAsync(string mtlPath, List<GardenElement> elements)
    {
        var mtl = new StringBuilder();
        mtl.AppendLine("# SmartMeasure Materials");
        mtl.AppendLine();

        mtl.AppendLine("newmtl Terrain");
        mtl.AppendLine("Kd 0.4 0.6 0.3");
        mtl.AppendLine("Ka 0.1 0.1 0.1");
        mtl.AppendLine("Ks 0.0 0.0 0.0");
        mtl.AppendLine("d 1.0");
        mtl.AppendLine();

        var usedTypes = elements.Select(e => e.ElementType).Distinct();
        foreach (var type in usedTypes)
        {
            if (MaterialColors.TryGetValue(type, out var color))
            {
                mtl.AppendLine($"newmtl {type}");
                mtl.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "Kd {0:F2} {1:F2} {2:F2}", color.r, color.g, color.b));
                mtl.AppendLine("Ka 0.1 0.1 0.1");
                mtl.AppendLine("Ks 0.0 0.0 0.0");
                mtl.AppendLine("d 1.0");
                mtl.AppendLine();
            }
        }

        await File.WriteAllTextAsync(mtlPath, mtl.ToString(), Encoding.UTF8);
    }

    private static async Task WriteTerrainOnlyMtlAsync(string mtlPath)
    {
        var mtl = new StringBuilder();
        mtl.AppendLine("# SmartMeasure Materials");
        mtl.AppendLine();
        mtl.AppendLine("newmtl Terrain");
        mtl.AppendLine("Kd 0.4 0.6 0.3");
        mtl.AppendLine("Ka 0.1 0.1 0.1");
        mtl.AppendLine("Ks 0.0 0.0 0.0");
        mtl.AppendLine("d 1.0");
        mtl.AppendLine();

        await File.WriteAllTextAsync(mtlPath, mtl.ToString(), Encoding.UTF8);
    }

    #endregion

    #region Helpers

    private static (double r, double g, double b) InterpolateHeightColor(double t)
    {
        t = Math.Clamp(t, 0, 1);
        var segment = t * (HeightColors.Length - 1);
        var index = (int)segment;
        var frac = segment - index;

        if (index >= HeightColors.Length - 1)
            return HeightColors[^1];

        var c1 = HeightColors[index];
        var c2 = HeightColors[index + 1];

        return (
            c1.r + (c2.r - c1.r) * frac,
            c1.g + (c2.g - c1.g) * frac,
            c1.b + (c2.b - c1.b) * frac);
    }

    private static List<(double x, double y)> ParsePoints(string json)
    {
        try
        {
            var arrays = JsonSerializer.Deserialize<double[][]>(json);
            if (arrays == null) return [];
            return arrays.Where(a => a.Length >= 2).Select(a => (a[0], a[1])).ToList();
        }
        catch
        {
            return [];
        }
    }

    #endregion
}
