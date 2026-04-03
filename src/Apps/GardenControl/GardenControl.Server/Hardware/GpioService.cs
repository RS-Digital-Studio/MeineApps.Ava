using System.Device.Gpio;

namespace GardenControl.Server.Hardware;

/// <summary>
/// GPIO-Steuerung über System.Device.Gpio.
/// Steuert Relais-Modul (4 Kanäle) für 3 Ventile + 1 Pumpe.
///
/// WICHTIG: Relais-Module sind meist "active low" (LOW = Relais EIN).
/// Diese Implementierung unterstützt beide Modi über den Konstruktor-Parameter.
/// Standard: Active-HIGH (HIGH = Relais EIN, passend für die meisten 5V-Relais-Module mit Optokoppler).
/// </summary>
public class GpioService : IGpioService
{
    private readonly ILogger<GpioService> _logger;
    private readonly GpioController _gpio;
    private readonly bool _activeLow;
    private readonly object _lock = new();

    private int _pumpPin;
    private readonly HashSet<int> _initializedPins = [];
    private bool _disposed;

    /// <param name="activeLow">true = LOW schaltet Relais ein (häufig bei China-Modulen)</param>
    public GpioService(ILogger<GpioService> logger, bool activeLow = false)
    {
        _logger = logger;
        _activeLow = activeLow;
        _gpio = new GpioController();
    }

    public bool IsPumpActive
    {
        get
        {
            lock (_lock)
                return _pumpPin > 0 && GetPinInternal(_pumpPin);
        }
    }

    public void Initialize(int pumpPin, IEnumerable<int> relayPins)
    {
        lock (_lock)
        {
            _pumpPin = pumpPin;

            // Pumpe initialisieren
            InitPin(pumpPin);

            // Ventil-Relais initialisieren
            foreach (var pin in relayPins)
                InitPin(pin);

            _logger.LogInformation("GPIO initialisiert: Pumpe={PumpPin}, Relais={Pins}",
                pumpPin, string.Join(",", relayPins));
        }
    }

    public void SetPin(int pin, bool on)
    {
        lock (_lock)
        {
            if (!_initializedPins.Contains(pin))
            {
                _logger.LogWarning("Pin {Pin} nicht initialisiert", pin);
                return;
            }

            // Bei Active-Low invertieren
            var value = _activeLow ? (on ? PinValue.Low : PinValue.High)
                                   : (on ? PinValue.High : PinValue.Low);

            _gpio.Write(pin, value);
            _logger.LogDebug("GPIO {Pin} → {State}", pin, on ? "EIN" : "AUS");
        }
    }

    public bool GetPin(int pin)
    {
        lock (_lock)
            return GetPinInternal(pin);
    }

    public void SetPump(bool on)
    {
        // SetPin() nimmt bereits den Lock - nicht doppelt locken (Deadlock!)
        if (_pumpPin <= 0) return;
        SetPin(_pumpPin, on);
        _logger.LogInformation("Pumpe {State}", on ? "EIN" : "AUS");
    }

    public void AllOff()
    {
        lock (_lock)
        {
            foreach (var pin in _initializedPins)
            {
                var offValue = _activeLow ? PinValue.High : PinValue.Low;
                _gpio.Write(pin, offValue);
            }
            _logger.LogWarning("ALLE GPIO-Ausgänge AUS (Notfall/Shutdown)");
        }
    }

    private void InitPin(int pin)
    {
        if (_initializedPins.Contains(pin)) return;

        _gpio.OpenPin(pin, PinMode.Output);
        // Sicherheitshalber ausschalten beim Init
        var offValue = _activeLow ? PinValue.High : PinValue.Low;
        _gpio.Write(pin, offValue);
        _initializedPins.Add(pin);
    }

    private bool GetPinInternal(int pin)
    {
        if (!_initializedPins.Contains(pin)) return false;

        var value = _gpio.Read(pin);
        return _activeLow ? value == PinValue.Low : value == PinValue.High;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        AllOff();
        _gpio.Dispose();

        GC.SuppressFinalize(this);
    }
}
