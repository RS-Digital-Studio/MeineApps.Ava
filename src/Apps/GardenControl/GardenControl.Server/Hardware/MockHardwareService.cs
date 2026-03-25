namespace GardenControl.Server.Hardware;

/// <summary>
/// Mock-Implementierung für Entwicklung/Testing auf Windows/Desktop
/// ohne echte GPIO/I2C-Hardware. Simuliert Sensoren und Relais.
/// </summary>
public class MockGpioService : IGpioService
{
    private readonly ILogger<MockGpioService> _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, bool> _pinStates = [];
    private int _pumpPin;

    public bool IsPumpActive => _pumpPin > 0 && _pinStates.GetValueOrDefault(_pumpPin, false);

    public MockGpioService(ILogger<MockGpioService> logger)
    {
        _logger = logger;
    }

    public void Initialize(int pumpPin, IEnumerable<int> relayPins)
    {
        _pumpPin = pumpPin;
        _pinStates[pumpPin] = false;
        foreach (var pin in relayPins)
            _pinStates[pin] = false;

        _logger.LogInformation("[MOCK] GPIO initialisiert: Pumpe={PumpPin}, Relais={Pins}",
            pumpPin, string.Join(",", relayPins));
    }

    public void SetPin(int pin, bool on)
    {
        _pinStates[pin] = on;
        _logger.LogDebug("[MOCK] Pin {Pin} → {State}", pin, on ? "EIN" : "AUS");
    }

    public bool GetPin(int pin) => _pinStates.GetValueOrDefault(pin, false);

    public void SetPump(bool on)
    {
        if (_pumpPin > 0)
        {
            _pinStates[_pumpPin] = on;
            _logger.LogInformation("[MOCK] Pumpe {State}", on ? "EIN" : "AUS");
        }
    }

    public void AllOff()
    {
        foreach (var pin in _pinStates.Keys)
            _pinStates[pin] = false;
        _logger.LogWarning("[MOCK] ALLE Ausgänge AUS");
    }

    public void Dispose() { }
}

/// <summary>
/// Mock-Sensor-Service: Simuliert Bodenfeuchtewerte die langsam sinken
/// und nach Bewässerung sprunghaft steigen.
/// </summary>
public class MockSensorService : ISensorService
{
    private readonly ILogger<MockSensorService> _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, int> _simulatedValues = new()
    {
        [0] = 18000, // ~60% Feuchtigkeit
        [1] = 22000, // ~30% Feuchtigkeit
        [2] = 15000, // ~80% Feuchtigkeit
    };

    private readonly Random _rng = new();
    private DateTime _lastUpdate = DateTime.UtcNow;

    public bool IsAvailable => true;

    public MockSensorService(ILogger<MockSensorService> logger)
    {
        _logger = logger;
    }

    public void Initialize()
    {
        _logger.LogInformation("[MOCK] Sensor-Service initialisiert (simulierte Werte)");
    }

    public int ReadRawValue(int channel)
    {
        SimulateDrift();

        if (_simulatedValues.TryGetValue(channel, out var value))
            return value + _rng.Next(-200, 200); // Leichtes Rauschen

        return -1;
    }

    public Dictionary<int, int> ReadAllChannels(IEnumerable<int> channels)
    {
        var result = new Dictionary<int, int>();
        foreach (var ch in channels)
            result[ch] = ReadRawValue(ch);
        return result;
    }

    /// <summary>Simuliert langsames Austrocknen</summary>
    private void SimulateDrift()
    {
        var elapsed = (DateTime.UtcNow - _lastUpdate).TotalSeconds;
        if (elapsed < 10) return;

        _lastUpdate = DateTime.UtcNow;

        foreach (var key in _simulatedValues.Keys)
        {
            // Wert steigt langsam (trockener) - ca. 100 pro Minute
            _simulatedValues.AddOrUpdate(key,
                _ => 18000,
                (_, current) => Math.Min(28000, current + (int)(elapsed * 1.5)));
        }
    }

    public void Dispose() { }
}
