using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Export von Terrain + Gartenelementen als Wavefront OBJ+MTL fuer Blender</summary>
public interface IBlenderExportService
{
    /// <summary>TerrainMesh + GardenElements als OBJ+MTL exportieren</summary>
    Task<string> ExportObjAsync(TerrainMesh mesh, List<GardenElement> elements, string outputDir, string projectName);

    /// <summary>Nur TerrainMesh als OBJ exportieren (ohne Gartenelemente)</summary>
    Task<string> ExportTerrainObjAsync(TerrainMesh mesh, string outputDir, string projectName);
}
