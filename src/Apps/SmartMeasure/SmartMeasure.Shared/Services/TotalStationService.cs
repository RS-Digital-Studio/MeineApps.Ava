namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.17 (MVP): Stationierung + Radial-Projektion. Echte Tachymeter-
/// Genauigkeit braucht den ARCore-Heading + Depth-API-Distanz aus der Activity —
/// dieser Service liefert die mathematische Kernlogik (Distance/Bearing → Lat/Lon).</summary>
public sealed class TotalStationService : ITotalStationService
{
    private readonly ICoordinateService _coordinateService;
    private (double lat, double lon, double alt, double heading)? _station;

    public TotalStationService(ICoordinateService coordinateService)
    {
        _coordinateService = coordinateService;
    }

    public void SetStationOrigin(double latitude, double longitude, double altitude, double headingDeg)
        => _station = (latitude, longitude, altitude, headingDeg);

    public (double latitude, double longitude, double altitude)? Station => _station == null
        ? null
        : (_station.Value.lat, _station.Value.lon, _station.Value.alt);

    (double latitude, double longitude, double altitude, double headingDeg)? ITotalStationService.Station
        => _station == null ? null : (_station.Value.lat, _station.Value.lon, _station.Value.alt, _station.Value.heading);

    public (double latitude, double longitude, double altitude) ProjectTarget(
        double distanceMeters, double bearingDeg, double pitchDeg)
    {
        if (_station == null)
            throw new InvalidOperationException("Stationierung fehlt — zuerst SetStationOrigin aufrufen");

        // Bearing absolut (Stations-Heading + relatives Bearing) auf 0..360 normalisiert.
        var absoluteBearingDeg = (_station.Value.heading + bearingDeg) % 360.0;
        if (absoluteBearingDeg < 0) absoluteBearingDeg += 360;

        // Horizontal-Distanz (Pitch-Komponente abziehen)
        var pitchRad = pitchDeg * Math.PI / 180.0;
        var horizontal = distanceMeters * Math.Cos(pitchRad);
        var dh = distanceMeters * Math.Sin(pitchRad);

        // East/North-Offset in Metern (lokal-tangential)
        var bearingRad = absoluteBearingDeg * Math.PI / 180.0;
        var east = horizontal * Math.Sin(bearingRad);
        var north = horizontal * Math.Cos(bearingRad);

        var (lat, lon, alt) = _coordinateService.LocalToLatLon(
            east, north, dh,
            _station.Value.lat, _station.Value.lon, _station.Value.alt);
        return (lat, lon, alt);
    }
}
