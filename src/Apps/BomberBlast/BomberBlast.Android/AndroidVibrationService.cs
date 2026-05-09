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
        Waveform(new long[] { 0, 50, 50, 50, 50, 50 });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // v2.0.45 — Haptic-Library: Native Patterns für 8 Spielereignisse.
    // VibrationEffect.createWaveform liefert kontextspezifisches Feedback.
    // Auf älteren Androids fallen wir auf einfaches Pulse zurück.
    // ═══════════════════════════════════════════════════════════════════════

    public void VibrateBombPlant()       => Waveform(new long[] { 0, 10, 20, 10 });
    public void VibrateSpecialBomb()     => Waveform(new long[] { 0, 15, 15, 15, 15, 15 });
    public void VibratePickUp()          => Pulse(20);
    public void VibrateShieldHit()       => Waveform(new long[] { 0, 35, 30, 35 });
    public void VibrateDeath()           => Pulse(200);
    public void VibrateLevelComplete()   => Waveform(new long[] { 0, 60, 40, 90, 40, 120 });
    public void VibrateBossRoar()        => Pulse(400);
    public void VibrateCurse()           => Waveform(new long[] { 0, 30, 80, 30 });
    public void VibrateCombo()           => Waveform(new long[] { 0, 12, 30, 12, 30, 12, 30, 12 });
    public void VibrateAchievement()     => Waveform(new long[] { 0, 40, 30, 40, 30, 100 });

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

    private void Waveform(long[] pattern)
    {
        if (!IsEnabled || _vibrator == null) return;
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                _vibrator.Vibrate(VibrationEffect.CreateWaveform(pattern, -1));
        }
        catch (Java.Lang.SecurityException) { /* VIBRATE-Permission entzogen */ }
    }
}
