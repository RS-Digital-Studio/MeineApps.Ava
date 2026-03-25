namespace GardenControl.Server.Hardware;

/// <summary>
/// Steuert die GPIO-Pins des Raspberry Pi für Relais (Ventile + Pumpe).
/// Alle Methoden sind thread-safe.
/// </summary>
public interface IGpioService : IDisposable
{
    /// <summary>Initialisiert alle konfigurierten GPIO-Pins als Output</summary>
    void Initialize(int pumpPin, IEnumerable<int> relayPins);

    /// <summary>Setzt einen GPIO-Pin auf HIGH (Relais ein)</summary>
    void SetPin(int pin, bool on);

    /// <summary>Liest den aktuellen Zustand eines Pins</summary>
    bool GetPin(int pin);

    /// <summary>Schaltet die Pumpe ein/aus</summary>
    void SetPump(bool on);

    /// <summary>Ist die Pumpe aktiv?</summary>
    bool IsPumpActive { get; }

    /// <summary>Schaltet ALLE Ausgänge aus (Notfall/Shutdown)</summary>
    void AllOff();
}
