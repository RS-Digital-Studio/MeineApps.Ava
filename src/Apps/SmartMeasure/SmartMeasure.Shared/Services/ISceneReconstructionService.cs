namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.5: Akkumuliert Raw-Depth-Images ueber eine Session zu einer
/// dichten Punktwolke (~10^5 Punkte/min auf S25 Ultra), filtert via Voxel-Grid und
/// rekonstruiert ein wasserdichtes Mesh per Poisson-Surface. Export als PLY/OBJ
/// fuer Blender/Sketchup/ArchiCAD. Use-Cases: Gelaendemodell fuer
/// Landschaftsarchitekten, As-Built-Aufnahme einer Renovierung.</summary>
public interface ISceneReconstructionService
{
    /// <summary>Punktwolke aus der Session konsumieren — typisch nach Capture-Ende.</summary>
    Task<MeshExportResult> ReconstructAndExportAsync(
        IReadOnlyList<(double x, double y, double z)> pointCloud,
        SceneReconstructionOptions options,
        CancellationToken ct = default);
}

/// <summary>Optionen fuer die Mesh-Reconstruction.</summary>
public sealed record SceneReconstructionOptions(
    double VoxelSizeMeters = 0.02,
    int PoissonDepth = 9,
    MeshExportFormat Format = MeshExportFormat.Obj);

public enum MeshExportFormat { Obj, Ply }

public sealed record MeshExportResult(string FilePath, int VertexCount, int TriangleCount, double SurfaceAreaSquareMeters);
