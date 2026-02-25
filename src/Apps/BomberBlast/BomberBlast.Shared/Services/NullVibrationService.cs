namespace BomberBlast.Services;

/// <summary>
/// Desktop-Fallback: Keine Vibration verfügbar.
/// </summary>
public class NullVibrationService : IVibrationService
{
    public bool IsEnabled { get; set; } = true;
    public void VibrateLight() { }
    public void VibrateMedium() { }
    public void VibrateHeavy() { }
    public void VibratePattern() { }
}
