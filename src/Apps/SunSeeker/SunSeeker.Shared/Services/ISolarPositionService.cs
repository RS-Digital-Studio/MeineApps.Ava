using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Services;

/// <summary>
/// Berechnet den Sonnenstand (Azimut/Elevation) und die Sonnenzeiten fuer einen Ort und
/// Zeitpunkt — vollstaendig offline. Reine Astronomie-Mathematik, plattformneutral und testbar.
/// </summary>
public interface ISolarPositionService
{
    /// <summary>Sonnenstand zum Zeitpunkt <paramref name="utc"/> am Ort <paramref name="location"/>.</summary>
    SolarPosition GetPosition(GeoLocation location, DateTime utc);

    /// <summary>Sonnenauf-/-untergang und Sonnen-Hoechststand (alles UTC) fuer einen Tag.</summary>
    SunTimes GetSunTimes(GeoLocation location, DateOnly date);

    /// <summary>Tagesbahn der Sonne als Liste von Positionen im Raster <paramref name="stepMinutes"/>
    /// (UTC, ueber 24 Stunden ab Mitternacht UTC des Datums). Fuer die Sonnenbahn-Visualisierung.</summary>
    IReadOnlyList<SolarPosition> GetDayArc(GeoLocation location, DateOnly date, int stepMinutes = 10);
}
