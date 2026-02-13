using Android.App;
using Android.Content;
using Android.OS;
using FitnessRechner.Services;

namespace FitnessRechner.Android;

/// <summary>
/// Android-Implementierung für haptisches Feedback über den Vibrator-Service.
/// Dual-Fallback: Android Q+ nutzt VibrationEffect, darunter HapticFeedback auf DecorView.
/// </summary>
public class AndroidHapticService : IHapticService
{
    private readonly Activity _activity;
    private readonly Vibrator? _vibrator;

    public bool IsEnabled { get; set; } = true;

    public AndroidHapticService(Activity activity)
    {
        _activity = activity;
#pragma warning disable CA1422 // VibratorService veraltet ab API 31, Fallback ist HapticFeedback
        _vibrator = activity.GetSystemService(Context.VibratorService) as Vibrator;
#pragma warning restore CA1422
    }

#pragma warning disable CA1416 // API-Level bereits via SdkInt-Check abgesichert
    public void Tick()
    {
        if (!IsEnabled) return;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            PerformEffect(VibrationEffect.EffectTick);
        else
            PerformHapticFeedback(global::Android.Views.FeedbackConstants.KeyboardTap);
    }

    public void Click()
    {
        if (!IsEnabled) return;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            PerformEffect(VibrationEffect.EffectClick);
        else
            PerformHapticFeedback(global::Android.Views.FeedbackConstants.ContextClick);
    }

    public void HeavyClick()
    {
        if (!IsEnabled) return;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            PerformEffect(VibrationEffect.EffectHeavyClick);
        else
            PerformHapticFeedback(global::Android.Views.FeedbackConstants.LongPress);
    }

    private void PerformEffect(int effectId)
    {
        try
        {
            if (_vibrator?.HasVibrator == true)
                _vibrator.Vibrate(VibrationEffect.CreatePredefined(effectId));
        }
        catch
        {
            // Vibration nicht verfügbar
        }
    }
#pragma warning restore CA1416

    private void PerformHapticFeedback(global::Android.Views.FeedbackConstants constant)
    {
        try
        {
            _activity.Window?.DecorView?.PerformHapticFeedback(constant);
        }
        catch
        {
            // Haptic nicht verfügbar
        }
    }
}
