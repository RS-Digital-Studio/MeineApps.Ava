using Android.OS;
using MeineApps.Core.Ava.Services;

namespace ZeitManager.Android.Services;

/// <summary>
/// Android-Implementierung von IHapticService mit Vibrator API.
/// </summary>
public sealed class AndroidHapticService : IHapticService
{
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Effekt-Stufen, plattformneutral. Werden erst innerhalb des API-Guards auf die
    /// VibrationEffect-Konstanten (API 29) abgebildet.
    /// </summary>
    private enum HapticEffect { Tick, Click, HeavyClick }

    public void Tick()
    {
        Vibrate(HapticEffect.Tick, durationMs: 20);
    }

    public void Click()
    {
        Vibrate(HapticEffect.Click, durationMs: 50);
    }

    public void HeavyClick()
    {
        Vibrate(HapticEffect.HeavyClick, durationMs: 100);
    }

    /// <summary>
    /// Loest eine Vibration aus. Drei Stufen je nach API-Level:
    /// API 29+ vordefinierter Effekt, API 26-28 OneShot mit Default-Amplitude,
    /// darunter (24-25) Legacy-Vibrate(long).
    /// </summary>
    private void Vibrate(HapticEffect effect, long durationMs)
    {
        if (!IsEnabled) return;
        try
        {
            var vibrator = GetVibrator();
            if (vibrator is null || !vibrator.HasVibrator) return;

            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                var predefined = effect switch
                {
                    HapticEffect.Tick => VibrationEffect.EffectTick,
                    HapticEffect.Click => VibrationEffect.EffectClick,
                    _ => VibrationEffect.EffectHeavyClick,
                };
                vibrator.Vibrate(VibrationEffect.CreatePredefined(predefined));
            }
            else if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                vibrator.Vibrate(VibrationEffect.CreateOneShot(durationMs, VibrationEffect.DefaultAmplitude));
            }
            else
            {
#pragma warning disable CS0618, CA1422 // Veraltete API fuer aeltere Android-Versionen (< 26) — einziger Weg
                vibrator.Vibrate(durationMs);
#pragma warning restore CS0618, CA1422
            }
        }
        catch
        {
            // Haptic nicht verfügbar - kein Problem
        }
    }

    /// <summary>
    /// Liefert den Vibrator. Ab API 31 ueber VibratorManager (Context.VibratorService ist
    /// dort veraltet), darunter ueber den klassischen System-Service.
    /// </summary>
    private static Vibrator? GetVibrator()
    {
        var context = global::Android.App.Application.Context;

        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var manager = (VibratorManager?)context
                .GetSystemService(global::Android.Content.Context.VibratorManagerService);
            return manager?.DefaultVibrator;
        }

#pragma warning disable CA1422 // Context.VibratorService ab API 31 veraltet — Legacy-Pfad fuer < 31
        return (Vibrator?)context.GetSystemService(global::Android.Content.Context.VibratorService);
#pragma warning restore CA1422
    }
}
