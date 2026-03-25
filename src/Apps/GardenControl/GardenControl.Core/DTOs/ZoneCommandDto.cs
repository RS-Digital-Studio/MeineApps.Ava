namespace GardenControl.Core.DTOs;

/// <summary>
/// Steuerbefehl für eine Zone (von Client an Server)
/// </summary>
public class ZoneCommandDto
{
    /// <summary>Zone-ID</summary>
    public int ZoneId { get; set; }

    /// <summary>Aktion: "start", "stop"</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Optionale Dauer in Sekunden (für "start")</summary>
    public int? DurationSeconds { get; set; }
}

/// <summary>
/// Konfigurationsupdate für eine Zone (von Client an Server)
/// </summary>
public class ZoneConfigDto
{
    public int ZoneId { get; set; }
    public string? Name { get; set; }
    public int? ThresholdPercent { get; set; }
    public int? WateringDurationSeconds { get; set; }
    public int? CooldownSeconds { get; set; }
    public bool? IsEnabled { get; set; }
}

/// <summary>
/// Kalibrierungsbefehl
/// </summary>
public class CalibrationCommandDto
{
    public int ZoneId { get; set; }

    /// <summary>"dry" oder "wet" - welcher Referenzwert wird gesetzt</summary>
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Systemkonfiguration (von Client an Server)
/// </summary>
public class SystemConfigDto
{
    public string? Mode { get; set; }
    public int? PollIntervalSeconds { get; set; }
}

/// <summary>
/// Verlaufsdaten-Abfrage
/// </summary>
public class HistoryQueryDto
{
    public int? ZoneId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int MaxResults { get; set; } = 1000;
}
