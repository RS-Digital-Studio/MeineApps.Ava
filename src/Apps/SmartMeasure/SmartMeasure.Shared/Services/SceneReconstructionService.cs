using System.Globalization;
using System.IO;
using System.Text;

namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.5 (MVP): Voxel-Filter + Punktwolke-Export im ASCII-PLY/OBJ-Format.
/// Echte Poisson-Surface-Reconstruction ist tagelange Arbeit ohne externe Lib —
/// Blender/CloudCompare/MeshLab koennen die Punktwolke direkt importieren und selbst
/// meshen. Der Voxel-Filter alleine bringt schon massive Datenreduktion (von ~10^5
/// auf ~10^4 Punkte bei 2cm-Voxel-Groesse).</summary>
public sealed class SceneReconstructionService : ISceneReconstructionService
{
    private readonly IAppPaths _appPaths;

    public SceneReconstructionService(IAppPaths appPaths)
    {
        _appPaths = appPaths;
    }

    public Task<MeshExportResult> ReconstructAndExportAsync(
        IReadOnlyList<(double x, double y, double z)> pointCloud,
        SceneReconstructionOptions options,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            // 1. Voxel-Filter: gruppiere Punkte in 3D-Buckets, pro Bucket Mittelpunkt
            var filtered = VoxelFilter(pointCloud, options.VoxelSizeMeters);

            // 2. Export
            var fileName = $"Scene_{DateTime.UtcNow:yyyyMMdd_HHmmss}." +
                (options.Format == MeshExportFormat.Ply ? "ply" : "obj");
            var filePath = Path.Combine(_appPaths.ExportFolder, fileName);
            Directory.CreateDirectory(_appPaths.ExportFolder);

            if (options.Format == MeshExportFormat.Ply)
                WritePly(filePath, filtered);
            else
                WriteObj(filePath, filtered);

            return new MeshExportResult(filePath, filtered.Count, 0, 0);
        }, ct);
    }

    /// <summary>Public fuer Tests. Gruppiert Punkte in 3D-Voxel-Buckets. Pro Bucket
    /// wird der Schwerpunkt aller enthaltenen Punkte als Ausgabe-Punkt verwendet —
    /// reduziert dichte Bereiche und erhaelt die Geometrie-Struktur.</summary>
    public static IReadOnlyList<(double x, double y, double z)> VoxelFilter(
        IReadOnlyList<(double x, double y, double z)> input, double voxelSize)
    {
        if (input.Count == 0 || voxelSize <= 0) return [];

        var buckets = new Dictionary<(long, long, long), (double sumX, double sumY, double sumZ, int count)>();

        foreach (var (x, y, z) in input)
        {
            var key = (
                (long)Math.Floor(x / voxelSize),
                (long)Math.Floor(y / voxelSize),
                (long)Math.Floor(z / voxelSize));
            if (buckets.TryGetValue(key, out var b))
            {
                buckets[key] = (b.sumX + x, b.sumY + y, b.sumZ + z, b.count + 1);
            }
            else
            {
                buckets[key] = (x, y, z, 1);
            }
        }

        var result = new List<(double, double, double)>(buckets.Count);
        foreach (var (_, b) in buckets)
            result.Add((b.sumX / b.count, b.sumY / b.count, b.sumZ / b.count));
        return result;
    }

    private static void WritePly(string path, IReadOnlyList<(double x, double y, double z)> points)
    {
        using var sw = new StreamWriter(path);
        sw.WriteLine("ply");
        sw.WriteLine("format ascii 1.0");
        sw.WriteLine($"element vertex {points.Count}");
        sw.WriteLine("property float x");
        sw.WriteLine("property float y");
        sw.WriteLine("property float z");
        sw.WriteLine("end_header");
        var ci = CultureInfo.InvariantCulture;
        foreach (var (x, y, z) in points)
            sw.WriteLine($"{x.ToString("F4", ci)} {y.ToString("F4", ci)} {z.ToString("F4", ci)}");
    }

    private static void WriteObj(string path, IReadOnlyList<(double x, double y, double z)> points)
    {
        var sb = new StringBuilder(points.Count * 24);
        sb.AppendLine("# SmartMeasure SceneReconstruction Punktwolke");
        sb.AppendLine($"# {points.Count} Punkte, exportiert {DateTime.UtcNow:O}");
        var ci = CultureInfo.InvariantCulture;
        foreach (var (x, y, z) in points)
            sb.AppendLine($"v {x.ToString("F4", ci)} {y.ToString("F4", ci)} {z.ToString("F4", ci)}");
        File.WriteAllText(path, sb.ToString());
    }
}
