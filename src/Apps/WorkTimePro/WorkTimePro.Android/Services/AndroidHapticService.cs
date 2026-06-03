using Android.OS;
using MeineApps.Core.Ava.Services;

namespace WorkTimePro.Android.Services;

/// <summary>
/// Android-Implementierung von IHapticService mit Vibrator-API.
/// Der API-31+ Pfad (VibratorManager) ist in eine eigene [SupportedOSPlatform]-Methode
/// ausgelagert, damit der Mono-JIT auf älteren Geräten (API 28/29) VibratorManager NICHT
/// auflöst → sonst nativer abort_application, weil der Java-Typ dort nicht existiert.
/// (Pattern wie HandwerkerImperium AndroidAudioService.) Vibrator wird einmalig gecacht.
/// </summary>
public sealed class AndroidHapticService : IHapticService
{
    public bool IsEnabled { get; set; } = true;

    private Vibrator? _vibrator;
    private bool _vibratorResolved;

    private Vibrator? GetVibrator()
    {
        if (_vibratorResolved) return _vibrator;
        _vibratorResolved = true;
        try
        {
            _vibrator = OperatingSystem.IsAndroidVersionAtLeast(31)
                ? GetVibratorApi31()
                : GetVibratorLegacy();
        }
        catch
        {
            _vibrator = null;
        }
        return _vibrator;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android31.0")]
    private static Vibrator? GetVibratorApi31()
    {
        var vibratorManager = (VibratorManager?)global::Android.App.Application.Context
            .GetSystemService(global::Android.Content.Context.VibratorManagerService);
        return vibratorManager?.DefaultVibrator;
    }

    private static Vibrator? GetVibratorLegacy()
    {
#pragma warning disable CA1422 // VibratorService ist ab API 31 deprecated, aber Fallback für ältere APIs
        return (Vibrator?)global::Android.App.Application.Context
            .GetSystemService(global::Android.Content.Context.VibratorService);
#pragma warning restore CA1422
    }

    public void Tick() => Vibrate(VibrationEffect.EffectTick, 20);

    public void Click() => Vibrate(VibrationEffect.EffectClick, 50);

    public void HeavyClick() => Vibrate(VibrationEffect.EffectHeavyClick, 100);

    private void Vibrate(int predefinedEffect, long legacyMs)
    {
        if (!IsEnabled) return;
        try
        {
            var vibrator = GetVibrator();
            if (vibrator == null || !vibrator.HasVibrator) return;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                vibrator.Vibrate(VibrationEffect.CreatePredefined(predefinedEffect));
            }
            else
            {
#pragma warning disable CS0618 // Veraltete API für ältere Android-Versionen
                vibrator.Vibrate(legacyMs);
#pragma warning restore CS0618
            }
        }
        catch
        {
            // Haptic nicht verfügbar - kein Problem
        }
    }
}
