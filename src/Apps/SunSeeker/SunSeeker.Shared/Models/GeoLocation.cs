namespace SunSeeker.Shared.Models;

/// <summary>
/// Geografische Position. Breitengrad positiv = Nord, Laengengrad positiv = Ost,
/// Hoehe in Metern ueber dem Ellipsoid (fuer die Sonnenstandsberechnung nicht relevant,
/// fuer die Refraktion vernachlaessigbar).
/// </summary>
public readonly record struct GeoLocation(double Latitude, double Longitude, double AltitudeMeters = 0)
{
    /// <summary>Nordhalbkugel (Breitengrad >= 0). Bestimmt die optimale Himmelsrichtung
    /// (Sued auf der Nord-, Nord auf der Suedhalbkugel).</summary>
    public bool IsNorthernHemisphere => Latitude >= 0;
}
