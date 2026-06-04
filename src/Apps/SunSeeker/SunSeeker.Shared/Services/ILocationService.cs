using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Services;

/// <summary>
/// Liefert die aktuelle geografische Position. Android: FusedLocationProvider/GPS.
/// Desktop: Mock mit festem (einstellbarem) Standort.
/// </summary>
public interface ILocationService
{
    /// <summary>Letzte bekannte Position, oder null bevor ein Fix vorliegt.</summary>
    GeoLocation? Current { get; }

    /// <summary>Ist ein Positions-Provider verfuegbar (Permission erteilt, GPS an)?</summary>
    bool IsAvailable { get; }

    /// <summary>Feuert bei jeder neuen Position (kann vom Background-Thread kommen).</summary>
    event EventHandler<GeoLocation>? LocationChanged;

    /// <summary>Holt einmalig die aktuelle Position (oder die letzte bekannte).</summary>
    Task<GeoLocation?> GetCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>Startet kontinuierliche Positions-Updates.</summary>
    void Start();

    /// <summary>Stoppt die Positions-Updates.</summary>
    void Stop();
}
