using ZeitManager.Models;

namespace ZeitManager.Audio;

/// <summary>
/// Zentrale Sound-Definitionen. Eingebaute Töne mit Frequenz und Dauer.
/// </summary>
public static class SoundDefinitions
{
    public static readonly IReadOnlyList<SoundItem> BuiltInSounds =
    [
        new("default", "Default Beep"),
        new("alert_high", "Alert High"),
        new("alert_low", "Alert Low"),
        new("chime", "Chime"),
        new("bell", "Bell"),
        new("digital", "Digital"),
    ];

    /// <summary>
    /// Gibt Frequenz und Dauer für einen eingebauten Sound zurück.
    /// </summary>
    public static (int Frequency, int DurationMs) GetToneParams(string soundId)
    {
        return soundId switch
        {
            "alert_high" => (1200, 300),
            "alert_low" => (600, 500),
            "chime" => (880, 200),
            "bell" => (1000, 400),
            "digital" => (1500, 150),
            _ => (800, 300) // default
        };
    }
}
