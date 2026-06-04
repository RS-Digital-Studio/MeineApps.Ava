namespace SunSeeker.Shared.Models;

/// <summary>
/// Sonnenzeiten eines Tages an einem Ort, alles in UTC. Bei Polartag/-nacht sind
/// <see cref="SunriseUtc"/>/<see cref="SunsetUtc"/> null und die entsprechenden Flags gesetzt.
/// </summary>
public readonly record struct SunTimes(
    DateTime? SunriseUtc,
    DateTime? SunsetUtc,
    DateTime SolarNoonUtc,
    double NoonElevation,
    bool PolarDay,
    bool PolarNight);
