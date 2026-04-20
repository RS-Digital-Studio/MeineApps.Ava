namespace SmartMeasure.Shared.Models;

/// <summary>Delaunay-Dreiecksgitter aus Vermessungspunkten.</summary>
/// <remarks>
/// Thread-Safety: Die Arrays werden nach Erstellung als immutable behandelt. Wenn neue
/// Messpunkte hinzukommen, MUSS ein neues TerrainMesh erzeugt werden — Renderer
/// halten beim Paint die Referenz fest, Mutationen während Render können
/// IndexOutOfRangeException auslösen.
/// </remarks>
public sealed class TerrainMesh
{
    /// <summary>Vertex-Positionen (x, y in Metern lokal, z = Hoehe relativ)</summary>
    public double[] X { get; init; } = [];
    public double[] Y { get; init; } = [];
    public double[] Z { get; init; } = [];

    /// <summary>Dreiecke als Index-Tripel (jedes Dreieck = 3 aufeinanderfolgende Indizes)</summary>
    public int[] Triangles { get; init; } = [];

    /// <summary>
    /// Vorberechnete Flächen-Normalen pro Dreieck (NormalsX[t], NormalsY[t], NormalsZ[t]).
    /// Einmal beim Mesh-Build berechnet und gecacht, damit Renderer sie nicht pro Frame
    /// neu berechnen müssen (~400 Dreiecke × 60 fps = 24k sqrt/s gespart).
    /// </summary>
    public float[] NormalsX { get; init; } = [];
    public float[] NormalsY { get; init; } = [];
    public float[] NormalsZ { get; init; } = [];

    /// <summary>Anzahl Vertices</summary>
    public int VertexCount => X.Length;

    /// <summary>Anzahl Dreiecke</summary>
    public int TriangleCount => Triangles.Length / 3;

    /// <summary>Minimale/Maximale Hoehe fuer Farbkodierung</summary>
    public double MinZ { get; init; }
    public double MaxZ { get; init; }

    /// <summary>Bounding Box</summary>
    public double MinX { get; init; }
    public double MaxX { get; init; }
    public double MinY { get; init; }
    public double MaxY { get; init; }
}

/// <summary>Eine Konturlinie (Isohypse) bei einer bestimmten Hoehe</summary>
public class ContourLine
{
    /// <summary>Hoehenwert dieser Konturlinie (relativ)</summary>
    public double Height { get; set; }

    /// <summary>Liniensegmente als Punkt-Paare (x1,y1, x2,y2, ...)</summary>
    public List<(float x1, float y1, float x2, float y2)> Segments { get; set; } = [];
}
