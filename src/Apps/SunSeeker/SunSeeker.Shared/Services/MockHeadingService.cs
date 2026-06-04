using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Services;

/// <summary>
/// Desktop-/Test-Ersatz fuer die Bewegungssensoren. Liefert eine feste, per
/// <see cref="SetReading"/> aenderbare Messung (Default: Sued, 35 Grad geneigt). Kein echter Sensor.
/// </summary>
public sealed class MockHeadingService : IHeadingService
{
    private HeadingReading _current = new(
        DeviceAzimuth: 180, MagneticAzimuth: 176, Declination: 4,
        Tilt: 35, AzimuthReliable: true, Accuracy: HeadingAccuracy.High);

    public bool IsAvailable => true;

    public HeadingReading Current => _current;

    public event EventHandler<HeadingReading>? Changed;

    public void Start() { }

    public void Stop() { }

    public void SetLocation(GeoLocation location) { }

    /// <summary>Setzt die simulierte Messung (Desktop-Experimente / UI-Tests).</summary>
    public void SetReading(double deviceAzimuth, double tilt)
    {
        _current = _current with { DeviceAzimuth = deviceAzimuth, Tilt = tilt };
        Changed?.Invoke(this, _current);
    }
}
