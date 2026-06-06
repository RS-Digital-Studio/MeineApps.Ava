namespace SunSeeker.Shared.Models;

/// <summary>
/// Geografische Position. Breitengrad positiv = Nord, Längengrad positiv = Ost,
/// Höhe in Metern über dem Ellipsoid (für die Sonnenstandsberechnung nicht relevant,
/// für die Refraktion vernachlässigbar).
/// </summary>
public readonly record struct GeoLocation(double Latitude, double Longitude, double AltitudeMeters = 0)
{
    /// <summary>Nordhalbkugel (Breitengrad >= 0). Bestimmt die optimale Himmelsrichtung
    /// (Süd auf der Nord-, Nord auf der Südhalbkugel).</summary>
    public bool IsNorthernHemisphere => Latitude >= 0;
}
