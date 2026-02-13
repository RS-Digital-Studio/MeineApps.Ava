namespace ZeitManager.Services;

/// <summary>
/// Desktop-Implementierung: Kein physischer Sensor, Shake wird per Button simuliert.
/// </summary>
public class DesktopShakeDetectionService : IShakeDetectionService
{
    public event EventHandler? ShakeDetected;

    public bool HasPhysicalSensor => false;

    public void StartListening() { }
    public void StopListening() { }

    public void SimulateShake()
    {
        ShakeDetected?.Invoke(this, EventArgs.Empty);
    }
}
