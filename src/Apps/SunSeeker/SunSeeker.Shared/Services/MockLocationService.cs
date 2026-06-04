using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Services;

/// <summary>
/// Desktop-/Test-Ersatz fuer den Positions-Provider. Liefert einen festen, per
/// <see cref="SetLocation"/> aenderbaren Standort (Default: Berlin). Kein echtes GPS.
/// </summary>
public sealed class MockLocationService : ILocationService
{
    private GeoLocation _current = new(52.5200, 13.4050, 38);

    public GeoLocation? Current => _current;

    public bool IsAvailable => true;

    public event EventHandler<GeoLocation>? LocationChanged;

    public Task<GeoLocation?> GetCurrentAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<GeoLocation?>(_current);

    public void Start() { }

    public void Stop() { }

    /// <summary>Setzt den simulierten Standort (Desktop-Experimente / Tests).</summary>
    public void SetLocation(GeoLocation location)
    {
        _current = location;
        LocationChanged?.Invoke(this, location);
    }
}
