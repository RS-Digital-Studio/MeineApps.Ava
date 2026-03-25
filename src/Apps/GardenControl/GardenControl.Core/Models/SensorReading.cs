using SQLite;

namespace GardenControl.Core.Models;

/// <summary>
/// Einzelne Sensormessung - wird periodisch in der Datenbank gespeichert.
/// Standardmäßig alle 30 Sekunden, konfigurierbar.
/// </summary>
[Table("SensorReadings")]
public class SensorReading
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Zugehörige Zone</summary>
    [Indexed]
    public int ZoneId { get; set; }

    /// <summary>Messzeitpunkt (UTC)</summary>
    [Indexed]
    public DateTime TimestampUtc { get; set; }

    /// <summary>ADC-Rohwert (0-32767 bei ADS1115 im Single-Ended-Modus)</summary>
    public int RawValue { get; set; }

    /// <summary>Berechnete Bodenfeuchtigkeit in Prozent (0-100)</summary>
    public double MoisturePercent { get; set; }

    /// <summary>Wurde während dieser Messung bewässert?</summary>
    public bool WasWatering { get; set; }
}
