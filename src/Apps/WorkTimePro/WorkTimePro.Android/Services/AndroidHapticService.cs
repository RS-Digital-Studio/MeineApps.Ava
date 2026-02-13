using Android.OS;
using WorkTimePro.Services;

namespace WorkTimePro.Android.Services;

/// <summary>
/// Android-Implementierung von IHapticService mit Vibrator API.
/// </summary>
public class AndroidHapticService : IHapticService
{
    public void Click()
    {
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
