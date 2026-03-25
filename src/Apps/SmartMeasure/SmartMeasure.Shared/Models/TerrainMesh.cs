namespace SmartMeasure.Shared.Models;

/// <summary>Delaunay-Dreiecksgitter aus Vermessungspunkten</summary>
public class TerrainMesh
{
    /// <summary>Vertex-Positionen (x, y in Metern lokal, z = Hoehe relativ)</summary>
    public double[] X { get; set; } = [];
    public double[] Y { get; set; } = [];
    public double[] Z { get; set; } = [];

    /// <summary>Dreiecke als Index-Tripel (jedes Dreieck = 3 aufeinanderfolgende Indizes)</summary>
    public int[] Triangles { get; set; } = [];

    /// <summary>Anzahl Vertices</summary>
    public int VertexCount => X.Length;

    /// <summary>Anzahl Dreiecke</summary>
    public int TriangleCount => Triangles.Length / 3;

    /// <summary>Minimale/Maximale Hoehe fuer Farbkodierung</summary>
    public double MinZ { get; set; }
    public double MaxZ { get; set; }

    /// <summary>Bounding Box</summary>
    public double MinX { get; set; }
    public double MaxX { get; set; }
    public double MinY { get; set; }
    public double MaxY { get; set; }
}

/// <summary>Eine Konturlinie (Isohypse) bei einer bestimmten Hoehe</summary>
public class ContourLine
{
    /// <summary>Hoehenwert dieser Konturlinie (relativ)</summary>
    public double Height { get; set; }

    /// <summary>Liniensegmente als Punkt-Paare (x1,y1, x2,y2, ...)</summary>
    public List<(float x1, float y1, float x2, float y2)> Segments { get; set; } = [];
}
