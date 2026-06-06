namespace SunSeeker.Shared.Models;

/// <summary>
/// Sonnenstand zu einem Zeitpunkt. <see cref="Azimuth"/> ist geografisch (true north):
/// 0 = Nord, 90 = Ost, 180 = Süd, 270 = West. <see cref="Elevation"/> ist der Höhenwinkel
/// über dem Horizont in Grad (negativ = Sonne unter dem Horizont).
/// </summary>
public readonly record struct SolarPosition(double Azimuth, double Elevation, DateTime TimestampUtc)
{
    /// <summary>Zenitwinkel (90 - Elevation) in Grad.</summary>
    public double Zenith => 90.0 - Elevation;

    /// <summary>Steht die Sonne über dem Horizont?</summary>
    public bool IsDaylight => Elevation > 0;
}
