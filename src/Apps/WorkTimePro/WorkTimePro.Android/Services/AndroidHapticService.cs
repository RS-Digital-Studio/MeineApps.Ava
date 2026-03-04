using Android.OS;
using MeineApps.Core.Ava.Services;

namespace WorkTimePro.Android.Services;

/// <summary>
/// Android-Implementierung von IHapticService mit Vibrator API.
/// </summary>
public sealed class AndroidHapticService : IHapticService
{
    public bool IsEnabled { get; set; } = true;

    public void Tick()
    {
        if (!IsEnabled) return;
        try
        {
            var vibrator = (Vibrator?)global::Android.App.Application.Context
                .GetSystemService(global::Android.Content.Context.VibratorService);
            if (vibrator == null || !vibrator.HasVibrator) return;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                vibrator.Vibrate(VibrationEffect.CreatePredefined(VibrationEffect.EffectTick));
            }
            else
            {
#pragma warning disable CS0618 // Veraltete API für ältere Android-Versionen
                vibrator.Vibrate(20);
#pragma warning restore CS0618
            }
        }
        catch
        {
            // Haptic nicht verfügbar - kein Problem
        }
    }

    public void Click()
    {
        if (!IsEnabled) return;
        try
        {
            var vibrator = (Vibrator?)global::Android.App.Application.Context
                .GetSystemService(global::Android.Content.Context.VibratorService);
            if (vibrator == null || !vibrator.HasVibrator) return;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                vibrator.Vibrate(VibrationEffect.CreatePredefined(VibrationEffect.EffectClick));
            }
            else
            {
#pragma warning disable CS0618 // Veraltete API für ältere Android-Versionen
                vibrator.Vibrate(50);
#pragma warning restore CS0618
            }
        }
        catch
        {
            // Haptic nicht verfügbar - kein Problem
        }
    }

    public void HeavyClick()
    {
        if (!IsEnabled) return;
        try
        {
            var vibrator = (Vibrator?)global::Android.App.Application.Context
                .GetSystemService(global::Android.Content.Context.VibratorService);
            if (vibrator == null || !vibrator.HasVibrator) return;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                vibrator.Vibrate(VibrationEffect.CreatePredefined(VibrationEffect.EffectHeavyClick));
            }
            else
            {
#pragma warning disable CS0618
                vibrator.Vibrate(100);
#pragma warning restore CS0618
            }
        }
        catch
        {
            // Haptic nicht verfügbar - kein Problem
        }
    }
}
