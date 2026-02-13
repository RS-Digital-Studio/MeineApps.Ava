namespace ZeitManager.Services;

/// <summary>
/// Erkennt Schüttelbewegungen des Geräts.
/// Desktop: Simulierter Shake per Button-Klick.
/// Android: Accelerometer-Sensor.
/// </summary>
public interface IShakeDetectionService
{
    /// <summary>Wird bei erkannter Schüttelbewegung ausgelöst.</summary>
    event EventHandler? ShakeDetected;

    /// <summary>Startet die Shake-Erkennung.</summary>
    void StartListening();

    /// <summary>Stoppt die Shake-Erkennung.</summary>
    void StopListening();

    /// <summary>True wenn ein physischer Sensor vorhanden ist (Android).</summary>
    bool HasPhysicalSensor { get; }

    /// <summary>Simuliert einen Shake (für Desktop ohne Sensor).</summary>
    void SimulateShake();
}
