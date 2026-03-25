using System.Device.I2c;
using Iot.Device.Ads1115;

namespace GardenControl.Server.Hardware;

/// <summary>
/// Liest Bodenfeuchtesensoren über den ADS1115 16-Bit ADC.
///
/// Die kapazitiven Sensoren liefern ein analoges Signal (0-3.3V):
/// - Trockener Boden → hohe Spannung → hoher ADC-Wert (~26000)
/// - Nasser Boden → niedrige Spannung → niedriger ADC-Wert (~12000)
///
/// Bei 10m Kabellänge: Abgeschirmtes Kabel verwenden (z.B. CAT5/6).
/// Die analogen Signale sind bei den geringen Strömen der kapazitiven
/// Sensoren auch über 10m stabil genug.
/// </summary>
public class SensorService : ISensorService
{
    private readonly ILogger<SensorService> _logger;
    private Ads1115? _adc;
    private I2cDevice? _i2cDevice;
    private readonly object _lock = new();
    private bool _disposed;

    // I2C-Bus 1 ist Standard auf dem Raspberry Pi
    private const int I2cBusId = 1;
    // Standard-Adresse wenn ADDR an GND
    private const int Ads1115Address = 0x48;

    public bool IsAvailable { get; private set; }

    public SensorService(ILogger<SensorService> logger)
    {
        _logger = logger;
    }

    public void Initialize()
    {
        try
        {
            var settings = new I2cConnectionSettings(I2cBusId, Ads1115Address);
            _i2cDevice = I2cDevice.Create(settings);
            _adc = new Ads1115(_i2cDevice, InputMultiplexer.AIN0, MeasuringRange.FS4096);

            // Testlesung um Verfügbarkeit zu prüfen
            _adc.ReadRaw(InputMultiplexer.AIN0);
            IsAvailable = true;

            _logger.LogInformation("ADS1115 initialisiert auf I2C Bus {Bus}, Adresse 0x{Addr:X2}",
                I2cBusId, Ads1115Address);
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            _logger.LogError(ex, "ADS1115 nicht erreichbar - läuft der Server auf einem Raspberry Pi?");
        }
    }

    public int ReadRawValue(int channel)
    {
        lock (_lock)
        {
            if (_adc == null || !IsAvailable)
                return -1;

            try
            {
                var mux = channel switch
                {
                    0 => InputMultiplexer.AIN0,
                    1 => InputMultiplexer.AIN1,
                    2 => InputMultiplexer.AIN2,
                    3 => InputMultiplexer.AIN3,
                    _ => throw new ArgumentOutOfRangeException(nameof(channel), "Kanal muss 0-3 sein")
                };

                return _adc.ReadRaw(mux);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Lesen von ADC-Kanal {Channel}", channel);
                return -1;
            }
        }
    }

    public Dictionary<int, int> ReadAllChannels(IEnumerable<int> channels)
    {
        var result = new Dictionary<int, int>();
        foreach (var channel in channels)
        {
            result[channel] = ReadRawValue(channel);
        }
        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _adc?.Dispose();
        _i2cDevice?.Dispose();

        GC.SuppressFinalize(this);
    }
}
