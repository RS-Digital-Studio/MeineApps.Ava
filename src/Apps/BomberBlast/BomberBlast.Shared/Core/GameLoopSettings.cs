using MeineApps.Core.Ava.Services;

namespace BomberBlast.Core;

/// <summary>
/// Globale Settings für den Render-Loop (Target-FPS).
/// Default: 30 FPS (halbiert CPU/GPU-Last auf Android, vergleichbar zu Brawl-Stars-Mid-Tier-Mode).
/// User kann via Settings auf 60 FPS hochsetzen — adaptive Quality verifiziert per Telemetrie.
/// </summary>
public static class GameLoopSettings
{
    public const int FrameRate30 = 30;
    public const int FrameRate60 = 60;

    /// <summary>Preferences-Key für persistierten Wert</summary>
    public const string PrefKey = "TargetFrameRate";

    /// <summary>Aktuelle Ziel-FPS (30 oder 60). Default 30.</summary>
    public static int TargetFps { get; private set; } = FrameRate30;

    /// <summary>Tick-Intervall in Millisekunden basierend auf TargetFps.</summary>
    public static int TickIntervalMs => 1000 / TargetFps;

    /// <summary>Tick-Intervall als TimeSpan (für DispatcherTimer).</summary>
    public static TimeSpan TickInterval => TimeSpan.FromMilliseconds(TickIntervalMs);

    /// <summary>
    /// Wird beim App-Start aufgerufen um den persistierten Wert zu laden.
    /// </summary>
    public static void Initialize(IPreferencesService preferences)
    {
        var stored = preferences.Get(PrefKey, FrameRate30);
        SetTargetFps(stored);
    }

    /// <summary>
    /// Ändert die Ziel-FPS und persistiert den Wert.
    /// </summary>
    public static void SetTargetFps(int fps, IPreferencesService? preferences = null)
    {
        TargetFps = fps == FrameRate60 ? FrameRate60 : FrameRate30;
        TargetFpsChanged?.Invoke(null, TargetFps);
        preferences?.Set(PrefKey, TargetFps);
    }

    /// <summary>
    /// Feuert wenn TargetFps geändert wird. Render-Timer können sich registrieren um neu zu starten.
    /// </summary>
    public static event EventHandler<int>? TargetFpsChanged;
}
