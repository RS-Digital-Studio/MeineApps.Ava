using Android.Content;
using Android.OS;
using BomberBlast.Services;

namespace BomberBlast.Droid;

/// <summary>
/// Android-spezifischer Vibration-Service via Vibrator-API (VIBRATE-Permission).
/// Tick: 15ms (Joystick-Richtungswechsel), Light: 25ms, Medium: 40ms, Heavy: 80ms.
/// Pattern (Level-Complete): drei kurze Vibrationen [50,50,50,50,50] ms.
/// API 26+ nutzt VibrationEffect.CreateOneShot/CreateWaveform, darunter deprecated Vibrate(long).
/// </summary>
public sealed class AndroidVibrationService : IVibrationService
{
    private readonly Vibrator? _vibrator;

    public bool IsEnabled { get; set; } = true;

    public AndroidVibrationService(Context context)
    {
#pragma warning disable CA1422 // VibratorService deprecated ab API 31, funktioniert weiter mit VIBRATE-Permission
        _vibrator = (Vibrator?)context.GetSystemService(Context.VibratorService);
#pragma warning restore CA1422
    }

    public void VibrateTick()    => Pulse(15);
    public void VibrateLight()   => Pulse(25);
    public void VibrateMedium()  => Pulse(40);
    public void VibrateHeavy()   => Pulse(80);

    public void VibratePattern()
    {
        if (!IsEnabled || _vibrator == null) return;
        try
        {
            long[] pattern = { 0, 50, 50, 50, 50, 50 };
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                _vibrator.Vibrate(VibrationEffect.CreateWaveform(pattern, -1));
        }
        catch (Java.Lang.SecurityException) { /* VIBRATE-Permission entzogen */ }
    }

    private void Pulse(int milliseconds)
    {
        if (!IsEnabled || _vibrator == null) return;
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                _vibrator.Vibrate(VibrationEffect.CreateOneShot(milliseconds, VibrationEffect.DefaultAmplitude));
        }
        catch (Java.Lang.SecurityException) { /* VIBRATE-Permission entzogen */ }
    }
}
