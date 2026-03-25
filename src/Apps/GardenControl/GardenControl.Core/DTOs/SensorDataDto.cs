namespace GardenControl.Core.DTOs;

/// <summary>
/// Sensor-Daten für einen einzelnen Messzeitpunkt.
/// Wird periodisch über SignalR an alle verbundenen Clients gesendet.
/// </summary>
public class SensorDataDto
{
    public DateTime TimestampUtc { get; set; }
    public List<SensorValueDto> Values { get; set; } = [];
}

/// <summary>
/// Einzelner Sensorwert
/// </summary>
public class SensorValueDto
{
    public int ZoneId { get; set; }
    public int RawValue { get; set; }
    public double MoisturePercent { get; set; }
}
