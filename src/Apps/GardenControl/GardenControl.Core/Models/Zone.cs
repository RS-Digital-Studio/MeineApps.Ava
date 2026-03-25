using SQLite;

namespace GardenControl.Core.Models;

/// <summary>
/// Bewässerungszone - verbindet einen Sensor mit einem Ventil.
/// Jede Zone steuert ein Beet/Bereich im Garten.
/// </summary>
[Table("Zones")]
public class Zone
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Name der Zone (z.B. "Tomaten-Beet", "Kräuter")</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>ADS1115 Kanal (0-3) für den Bodenfeuchtesensor</summary>
    public int SensorChannel { get; set; }

    /// <summary>GPIO-Pin (BCM) für das Relais des Magnetventils</summary>
    public int RelayGpioPin { get; set; }

    /// <summary>Feuchtigkeits-Schwellenwert in Prozent (0-100). Unter diesem Wert wird bewässert</summary>
    public int ThresholdPercent { get; set; } = 40;

    /// <summary>Bewässerungsdauer in Sekunden</summary>
    public int WateringDurationSeconds { get; set; } = 30;

    /// <summary>Abkühlzeit nach Bewässerung in Sekunden (verhindert Über-Bewässerung)</summary>
    public int CooldownSeconds { get; set; } = 300;

    /// <summary>Zone aktiv/deaktiviert</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Kalibrierung: ADC-Rohwert bei trockenem Boden</summary>
    public int CalibrationDryValue { get; set; } = 26000;

    /// <summary>Kalibrierung: ADC-Rohwert bei nassem Boden</summary>
    public int CalibrationWetValue { get; set; } = 12000;

    /// <summary>Zeitpunkt der letzten Kalibrierung (UTC)</summary>
    public DateTime? CalibratedAtUtc { get; set; }

    /// <summary>
    /// Berechnet die Bodenfeuchtigkeit in Prozent aus dem ADC-Rohwert.
    /// Kapazitive Sensoren: Trockener Boden = hoher Wert, nasser Boden = niedriger Wert.
    /// </summary>
    public double CalculateMoisturePercent(int rawAdcValue)
    {
        if (CalibrationDryValue == CalibrationWetValue)
            return 0;

        var percent = (double)(CalibrationDryValue - rawAdcValue) /
                      (CalibrationDryValue - CalibrationWetValue) * 100.0;

        return Math.Clamp(percent, 0, 100);
    }
}
