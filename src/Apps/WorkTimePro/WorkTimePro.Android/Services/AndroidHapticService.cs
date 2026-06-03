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

    // Welcher vordefinierte VibrationEffect (API 29+) gemeint ist — als Enum, damit die
    // EffectTick/EffectClick/EffectHeavyClick-Konstanten (alle API 29) erst INNERHALB des
    // Versions-Guards gelesen werden und der CA1416-Analyzer zufrieden ist.
    private enum HapticKind { Tick, Click, HeavyClick }

    public void Tick() => Vibrate(HapticKind.Tick, 20);

    public void Click() => Vibrate(HapticKind.Click, 50);

    public void HeavyClick() => Vibrate(HapticKind.HeavyClick, 100);

    private void Vibrate(HapticKind kind, long legacyMs)
    {
        if (!IsEnabled) return;
        try
        {
            var vibrator = GetVibrator();
            if (vibrator == null || !vibrator.HasVibrator) return;

            // Vibrate(VibrationEffect) ab API 26, CreatePredefined/Effect*-Konstanten ab API 29.
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                int predefinedEffect = kind switch
                {
                    HapticKind.Tick => VibrationEffect.EffectTick,
                    HapticKind.Click => VibrationEffect.EffectClick,
                    _ => VibrationEffect.EffectHeavyClick
                };
                vibrator.Vibrate(VibrationEffect.CreatePredefined(predefinedEffect));
            }
            else
            {
                // Fallback für API 24-28: ms-Vibrate (ab API 26 deprecated, aber hier korrekt).
#pragma warning disable CA1422 // Vibrate(long) ist ab API 26 deprecated, Fallback für API 24/25
#pragma warning disable CS0618 // Veraltete API für ältere Android-Versionen
                vibrator.Vibrate(legacyMs);
#pragma warning restore CS0618
#pragma warning restore CA1422
            }
        }
        catch
        {
            // Haptic nicht verfügbar - kein Problem
        }
    }
}
