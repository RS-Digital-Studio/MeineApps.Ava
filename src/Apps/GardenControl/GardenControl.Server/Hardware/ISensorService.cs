namespace GardenControl.Server.Hardware;

/// <summary>
/// Liest analoge Sensorwerte über den ADS1115 ADC (I2C).
/// Der Raspberry Pi hat keinen eingebauten ADC - der ADS1115 liefert 4 Kanäle mit 16-Bit Auflösung.
///
/// Verkabelung:
/// - ADS1115 SDA → Pi GPIO 2 (Pin 3)
/// - ADS1115 SCL → Pi GPIO 3 (Pin 5)
/// - ADS1115 VDD → Pi 3.3V (Pin 1)
/// - ADS1115 GND → Pi GND (Pin 6)
/// - ADS1115 ADDR → GND (I2C-Adresse 0x48)
/// - Sensor 1 Signal → ADS1115 A0
/// - Sensor 2 Signal → ADS1115 A1
/// - Sensor 3 Signal → ADS1115 A2
/// - (A3 frei für Erweiterung)
/// </summary>
public interface ISensorService : IDisposable
{
    /// <summary>Initialisiert den I2C-Bus und den ADS1115</summary>
    void Initialize();

    /// <summary>Liest den Rohwert eines ADC-Kanals (0-3)</summary>
    int ReadRawValue(int channel);

    /// <summary>Liest alle konfigurierten Kanäle</summary>
    Dictionary<int, int> ReadAllChannels(IEnumerable<int> channels);

    /// <summary>Ist der ADC erreichbar?</summary>
    bool IsAvailable { get; }
}
