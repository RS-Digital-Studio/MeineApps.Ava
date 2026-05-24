using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.4: Volumen- und Aushub-Messung. Liefert ein Volumen aus einer
/// geschlossenen Polygon-Kontur (Grundflaeche) und einer Tiefen-/Hoehen-Differenz.
/// Use-Cases: Aushub einer Grube (Pool, Fundament), Aufschuettung, Mutterboden-Bedarf.</summary>
public interface IVolumeService
{
    /// <summary>Truncated Prism: V = A * h. Verwendet eine geschlossene Garten-Kontur
    /// als Grundflaeche und eine konstante Tiefe (m). Liefert Volumen in m^3.</summary>
    VolumeEstimate EstimatePrism(GardenElement closedContour, double depthMeters);

    /// <summary>Geschichteter Prism: pro Hoehen-Schicht ein Polygon, V = sum(A_i * dh).
    /// Wenn alle Polygone identisch sind, entspricht es <see cref="EstimatePrism"/>.</summary>
    VolumeEstimate EstimateLayered(IReadOnlyList<(GardenElement contour, double layerHeightMeters)> layers);

    /// <summary>Trapez-Volumen aus Top- und Boden-Polygon (linear gemittelte Flaeche * Hoehe).
    /// Naehrung fuer Aushuebe mit schraegen Waenden.</summary>
    VolumeEstimate EstimateFrustum(GardenElement topContour, GardenElement bottomContour, double heightMeters);
}

/// <summary>Geschaetztes Volumen mit Materialschaetzung.</summary>
public sealed record VolumeEstimate(
    double VolumeCubicMeters,
    double SurfaceAreaSquareMeters,
    double BaseAreaSquareMeters,
    IReadOnlyList<VolumeMaterialEstimate> MaterialOptions);

/// <summary>Material-Tonnen-Schaetzung pro Material-Typ (Dichte aus Festwerten).</summary>
public sealed record VolumeMaterialEstimate(string Material, double DensityKgPerM3, double Tonnes);
