using GardenControl.Core.Enums;

namespace GardenControl.Core.DTOs;

/// <summary>
/// Komplettstatus des Systems - wird über SignalR an Clients gepusht.
/// </summary>
public class SystemStatusDto
{
    public SystemMode Mode { get; set; }
    public bool PumpActive { get; set; }
    public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
    public TimeSpan Uptime { get; set; }
    public List<ZoneStatusDto> Zones { get; set; } = [];

    /// <summary>Aktuelle Wetterdaten (null wenn kein API-Key konfiguriert)</summary>
    public WeatherDto? Weather { get; set; }

    /// <summary>Bewässerung wegen Wetter pausiert?</summary>
    public bool WeatherPaused { get; set; }

    /// <summary>Grund für Wetter-Pause</summary>
    public string? WeatherPauseReason { get; set; }
}

/// <summary>
/// Status einer einzelnen Zone
/// </summary>
public class ZoneStatusDto
{
    public int ZoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ZoneState State { get; set; }
    public double MoisturePercent { get; set; }
    public int RawAdcValue { get; set; }
    public int ThresholdPercent { get; set; }
    public SensorStatus SensorStatus { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? LastWateredUtc { get; set; }
    public int? RemainingWateringSeconds { get; set; }
}
