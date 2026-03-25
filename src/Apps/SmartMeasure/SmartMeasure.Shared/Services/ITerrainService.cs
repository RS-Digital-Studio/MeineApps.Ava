using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Gelaendemodell: Delaunay-Triangulierung, Konturlinien, Hoehen-Interpolation</summary>
public interface ITerrainService
{
    /// <summary>Erstellt ein Dreiecksgitter aus Messpunkten (Bowyer-Watson Delaunay)</summary>
    TerrainMesh CreateMesh(double[] x, double[] y, double[] z);

    /// <summary>Berechnet Konturlinien fuer ein gegebenes Hoehen-Intervall</summary>
    List<ContourLine> CreateContourLines(TerrainMesh mesh, double interval);

    /// <summary>Interpoliert die Hoehe an einem beliebigen Punkt im Mesh</summary>
    double? InterpolateHeight(TerrainMesh mesh, double x, double y);

    /// <summary>Berechnet die 2D-Flaeche des konvexen Huellen-Polygons (Shoelace)</summary>
    double CalculateArea2D(double[] x, double[] y);

    /// <summary>Berechnet die 3D-Oberflaechengroesse (Summe der Dreiecksflaechen)</summary>
    double CalculateArea3D(TerrainMesh mesh);

    /// <summary>Berechnet Volumen ueber/unter einer Referenzhoehe (fuer Terrassen)</summary>
    (double fill, double cut) CalculateVolume(TerrainMesh mesh, double referenceHeight);
}
