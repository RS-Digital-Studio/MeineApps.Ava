using Android.OS;
using ZeitManager.Services;

namespace ZeitManager.Android.Services;

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
#pragma warning disable CS0618 // Veraltete API fuer aeltere Android-Versionen
                vibrator.Vibrate(50);
#pragma warning restore CS0618
            }
        }
        catch
        {
            // Haptic nicht verfuegbar - kein Problem
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
            // Haptic nicht verfuegbar - kein Problem
        }
    }
}
