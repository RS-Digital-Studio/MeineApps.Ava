using System.Globalization;
using System.Text;
using System.Text.Json;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Wavefront OBJ+MTL Export fuer Blender (Terrain + Gartenelemente)</summary>
public class BlenderExportService : IBlenderExportService
{
    // Hoehen-Farbverlauf analog zu TerrainRenderer (Gruen → Gelb → Orange → Braun)
    private static readonly (double r, double g, double b)[] HeightColors =
    [
        (27 / 255.0, 94 / 255.0, 32 / 255.0),     // Dunkelgruen (tief)
        (76 / 255.0, 175 / 255.0, 80 / 255.0),     // Hellgruen
        (253 / 255.0, 216 / 255.0, 53 / 255.0),    // Gelb
        (255 / 255.0, 143 / 255.0, 0 / 255.0),     // Orange
        (93 / 255.0, 64 / 255.0, 55 / 255.0)       // Braun (hoch)
    ];

    // Material-Farben fuer Gartenelemente (Kd-Werte)
    private static readonly Dictionary<GardenElementType, (double r, double g, double b)> MaterialColors = new()
    {
        [GardenElementType.Weg] = (0.6, 0.6, 0.6),         // Grau
        [GardenElementType.Beet] = (0.5, 0.3, 0.1),        // Braun
        [GardenElementType.Rasen] = (0.2, 0.7, 0.2),       // Hellgruen
        [GardenElementType.Mauer] = (0.7, 0.7, 0.7),       // Hellgrau
        [GardenElementType.Zaun] = (0.5, 0.35, 0.2),       // Holz
        [GardenElementType.Terrasse] = (0.7, 0.6, 0.5),    // Sandstein
        [GardenElementType.Grenze] = (1.0, 0.4, 0.0),      // Orange (Grenzlinie)
        [GardenElementType.Gebaeude] = (0.55, 0.55, 0.6),   // Blaugrau
        [GardenElementType.Wasser] = (0.2, 0.5, 0.8),       // Blau
        [GardenElementType.Kante] = (0.8, 0.8, 0.2),        // Gelb (Hilfslinie)
    };

    // Standard-Dimensionen fuer extrudierte Elemente
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

        // MTL-Datei schreiben
        await WriteMtlFileAsync(mtlPath, elements);

        // OBJ-Datei schreiben
        var obj = new StringBuilder();
        WriteObjHeader(obj, mesh, projectName);

        // Globaler Vertex/Normal-Offset (OBJ ist 1-basiert, Objekte teilen den Pool)
        int vertexOffset = 1;
        int normalOffset = 1;

        // Terrain exportieren
        vertexOffset = WriteTerrainObject(obj, mesh, vertexOffset);
        normalOffset += mesh.TriangleCount; // Terrain schreibt eine Normale pro Dreieck

        // Gartenelemente exportieren
        var elementCounts = new Dictionary<GardenElementType, int>();
        foreach (var element in elements)
        {
            var points = ParsePoints(element.PointsJson);
            if (points.Count < 2) continue;

            elementCounts.TryGetValue(element.ElementType, out var count);
            count++;
            elementCounts[element.ElementType] = count;

            var objectName = $"{element.ElementType}_{count}";
            var prevVertexOffset = vertexOffset;
            vertexOffset = WriteGardenElementObject(obj, element, points, objectName, vertexOffset, normalOffset);
            // Normalen-Offset aktualisieren (Flaechen-Elemente schreiben 1 Normale, Extrudierte keine)
            if (element.ElementType is GardenElementType.Beet or GardenElementType.Rasen or GardenElementType.Terrasse)
                normalOffset++;

        }

        await File.WriteAllTextAsync(objPath, obj.ToString(), Encoding.UTF8);

        return objPath;
    }

    public async Task<string> ExportTerrainObjAsync(TerrainMesh mesh, string outputDir, string projectName)
    {
        Directory.CreateDirectory(outputDir);

        var objPath = Path.Combine(outputDir, $"{projectName}.obj");
        var mtlPath = Path.Combine(outputDir, $"{projectName}.mtl");

        // Minimale MTL-Datei nur mit Terrain-Material
        await WriteTerrainOnlyMtlAsync(mtlPath);

        var obj = new StringBuilder();
        WriteObjHeader(obj, mesh, projectName);
        WriteTerrainObject(obj, mesh, vertexOffset: 1);

        await File.WriteAllTextAsync(objPath, obj.ToString(), Encoding.UTF8);

        return objPath;
    }

    #region OBJ Generation

    private static void WriteObjHeader(StringBuilder obj, TerrainMesh mesh, string projectName)
    {
        obj.AppendLine($"# SmartMeasure Export - {projectName}");
        obj.AppendLine($"# Vertices: {mesh.VertexCount}, Triangles: {mesh.TriangleCount}");
        obj.AppendLine(CultureInfo.InvariantCulture, $"# Bounds: X[{mesh.MinX:F2}..{mesh.MaxX:F2}] Y[{mesh.MinY:F2}..{mesh.MaxY:F2}] Z[{mesh.MinZ:F2}..{mesh.MaxZ:F2}]");
        obj.AppendLine($"mtllib {projectName}.mtl");
        obj.AppendLine();
    }

    /// <summary>Terrain-Mesh als OBJ-Objekt schreiben. Gibt den naechsten Vertex-Offset zurueck.</summary>
    private static int WriteTerrainObject(StringBuilder obj, TerrainMesh mesh, int vertexOffset)
    {
        obj.AppendLine("o Terrain");
        obj.AppendLine("usemtl Terrain");

        // Vertices mit Vertex-Farben (OBJ Extension: v x y z r g b)
        // Blender Y-up: unsere X → OBJ X, unsere Z (Hoehe) → OBJ Y, unsere Y → OBJ Z (negiert fuer korrekte Orientierung)
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            var x = mesh.X[i];
            var y = mesh.Y[i];
            var z = mesh.Z[i];

            // Hoehen-Farbe berechnen
            var zNorm = mesh.MaxZ > mesh.MinZ
                ? (z - mesh.MinZ) / (mesh.MaxZ - mesh.MinZ)
                : 0.5;
            var (cr, cg, cb) = InterpolateHeightColor(zNorm);

            // v x z -y r g b (Y/Z-Swap: Blender Z-up → unsere Y wird -Z in Blender, unsere Z wird Y)
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "v {0:F6} {1:F6} {2:F6} {3:F4} {4:F4} {5:F4}",
                x, z, -y, cr, cg, cb));
        }

        // Normalen berechnen (pro Dreieck, flat shading)
        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            var i0 = mesh.Triangles[t * 3];
            var i1 = mesh.Triangles[t * 3 + 1];
            var i2 = mesh.Triangles[t * 3 + 2];

            // Kreuzprodukt (e1 x e2)
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

            // Y/Z-Swap fuer Normale
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "vn {0:F6} {1:F6} {2:F6}",
                nx, nz, -ny));
        }

        // Faces (1-basiert, mit Normalen-Index)
        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            var i0 = mesh.Triangles[t * 3] + vertexOffset;
            var i1 = mesh.Triangles[t * 3 + 1] + vertexOffset;
            var i2 = mesh.Triangles[t * 3 + 2] + vertexOffset;
            var normalIdx = t + 1; // Normalen sind ebenfalls 1-basiert, eine pro Dreieck

            obj.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "f {0}//{1} {2}//{3} {4}//{5}",
                i0, normalIdx, i1, normalIdx, i2, normalIdx));
        }

        obj.AppendLine();

        return vertexOffset + mesh.VertexCount;
    }

    /// <summary>Gartenelement als OBJ-Objekt. Gibt den naechsten Vertex-Offset zurueck.</summary>
    private static int WriteGardenElementObject(StringBuilder obj, GardenElement element,
        List<(double x, double y)> points, string objectName, int vertexOffset, int normalOffset)
    {
        obj.AppendLine($"o {objectName}");
        obj.AppendLine($"usemtl {element.ElementType}");

        switch (element.ElementType)
        {
            case GardenElementType.Beet:
            case GardenElementType.Rasen:
            case GardenElementType.Terrasse:
                return WriteFlatPolygon(obj, points, element, vertexOffset, normalOffset);

            case GardenElementType.Weg:
            case GardenElementType.Mauer:
            case GardenElementType.Zaun:
                return WriteExtrudedPolyline(obj, points, element, vertexOffset);

            default:
                return vertexOffset;
        }
    }

    /// <summary>Flaechen-Element: Fan-Triangulierung mit Punkt 0 als Faechermittelpunkt</summary>
    private static int WriteFlatPolygon(StringBuilder obj, List<(double x, double y)> points,
        GardenElement element, int vertexOffset, int normalOffset)
    {
        if (points.Count < 3) return vertexOffset;

        // Hoehe: Terrassen nutzen TargetAltitude (Aufschuettungshoehe), Mauern/Gebaeude nutzen Height
        var height = element.ElementType switch
        {
            GardenElementType.Terrasse when element.TargetAltitude > 0 => element.TargetAltitude,
            GardenElementType.Gebaeude when element.Height > 0 => (double)element.Height,
            _ => 0.01 // Minimal angehoben damit es auf dem Terrain sichtbar ist
        };

        // Vertices (Y/Z-Swap: x → x, height → y, -y → z)
        foreach (var (px, py) in points)
        {
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "v {0:F6} {1:F6} {2:F6}",
                px, height, -py));
        }

        // Normale (nach oben zeigend im Blender-Koordinatensystem)
        obj.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "vn {0:F6} {1:F6} {2:F6}",
            0.0, 1.0, 0.0));

        // Fan-Triangulierung (Punkt 0 als Zentrum), mit korrektem Normalen-Index
        for (int i = 1; i < points.Count - 1; i++)
        {
            var i0 = vertexOffset;
            var i1 = vertexOffset + i;
            var i2 = vertexOffset + i + 1;

            obj.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "f {0}//{1} {2}//{3} {4}//{5}",
                i0, normalOffset, i1, normalOffset, i2, normalOffset));
        }

        obj.AppendLine();

        return vertexOffset + points.Count;
    }

    /// <summary>Linien-Element: Extrudierte Quads entlang der Polylinie</summary>
    private static int WriteExtrudedPolyline(StringBuilder obj, List<(double x, double y)> points,
        GardenElement element, int vertexOffset)
    {
        if (points.Count < 2) return vertexOffset;

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
            GardenElementType.Weg => 0.05f, // Wege minimal erhoehen
            _ => 0.1f
        };

        var halfWidth = width / 2.0;
        var vertexCount = 0;

        // Fuer jedes Segment: 4 untere + 4 obere Vertices (Box-Extrusion)
        for (int i = 0; i < points.Count - 1; i++)
        {
            var (x1, y1) = points[i];
            var (x2, y2) = points[i + 1];

            // Richtungsvektor des Segments
            var dx = x2 - x1;
            var dy = y2 - y1;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001) continue;

            // Normalen-Vektor (senkrecht zur Richtung)
            var nx = -dy / len * halfWidth;
            var ny = dx / len * halfWidth;

            // 4 Punkte unten (Y/Z-Swap: x → x, 0 → y, -y → z)
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", x1 + nx, 0.0, -(y1 + ny)));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", x1 - nx, 0.0, -(y1 - ny)));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", x2 - nx, 0.0, -(y2 - ny)));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", x2 + nx, 0.0, -(y2 + ny)));

            // 4 Punkte oben
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", x1 + nx, (double)height, -(y1 + ny)));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", x1 - nx, (double)height, -(y1 - ny)));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", x2 - nx, (double)height, -(y2 - ny)));
            obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", x2 + nx, (double)height, -(y2 + ny)));

            // 6 Faces pro Segment-Box (Quads als je 2 Dreiecke)
            var b = vertexOffset + vertexCount; // Basis-Index (1-basiert)

            // Bottom: 1,2,3 + 1,3,4
            obj.AppendLine($"f {b} {b + 1} {b + 2}");
            obj.AppendLine($"f {b} {b + 2} {b + 3}");
            // Top: 5,7,6 + 5,8,7
            obj.AppendLine($"f {b + 4} {b + 6} {b + 5}");
            obj.AppendLine($"f {b + 4} {b + 7} {b + 6}");
            // Front: 1,4,8 + 1,8,5
            obj.AppendLine($"f {b} {b + 3} {b + 7}");
            obj.AppendLine($"f {b} {b + 7} {b + 4}");
            // Back: 2,6,7 + 2,7,3
            obj.AppendLine($"f {b + 1} {b + 5} {b + 6}");
            obj.AppendLine($"f {b + 1} {b + 6} {b + 2}");
            // Left: 1,5,6 + 1,6,2
            obj.AppendLine($"f {b} {b + 4} {b + 5}");
            obj.AppendLine($"f {b} {b + 5} {b + 1}");
            // Right: 4,3,7 + 4,7,8
            obj.AppendLine($"f {b + 3} {b + 2} {b + 6}");
            obj.AppendLine($"f {b + 3} {b + 6} {b + 7}");

            vertexCount += 8;
        }

        obj.AppendLine();

        return vertexOffset + vertexCount;
    }

    #endregion

    #region MTL Generation

    private static async Task WriteMtlFileAsync(string mtlPath, List<GardenElement> elements)
    {
        var mtl = new StringBuilder();
        mtl.AppendLine("# SmartMeasure Materials");
        mtl.AppendLine();

        // Terrain-Material
        mtl.AppendLine("newmtl Terrain");
        mtl.AppendLine("Kd 0.4 0.6 0.3");
        mtl.AppendLine("Ka 0.1 0.1 0.1");
        mtl.AppendLine("Ks 0.0 0.0 0.0");
        mtl.AppendLine("d 1.0");
        mtl.AppendLine();

        // Material fuer jeden verwendeten Element-Typ
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

    /// <summary>Hoehenfarbe interpolieren (analog zu TerrainRenderer)</summary>
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

    /// <summary>PointsJson parsen (gleich wie GardenPlanService)</summary>
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
