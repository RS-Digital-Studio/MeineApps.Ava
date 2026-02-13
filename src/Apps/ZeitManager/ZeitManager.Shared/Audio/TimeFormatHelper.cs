namespace ZeitManager.Audio;

/// <summary>
/// Gemeinsamer Zeitformat-Helper für Stoppuhr und Timer.
/// </summary>
public static class TimeFormatHelper
{
    /// <summary>
    /// Formatiert eine TimeSpan als HH:MM:SS.cs oder MM:SS.cs
    /// </summary>
    public static string Format(TimeSpan time)
    {
        if (time.Hours > 0)
            return $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}.{time.Milliseconds / 10:D2}";
        return $"{time.Minutes:D2}:{time.Seconds:D2}.{time.Milliseconds / 10:D2}";
    }
}
