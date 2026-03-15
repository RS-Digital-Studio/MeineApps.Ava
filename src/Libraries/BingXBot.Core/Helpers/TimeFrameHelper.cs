using BingXBot.Core.Enums;

namespace BingXBot.Core.Helpers;

/// <summary>
/// Zentraler Helper für TimeFrame-Konvertierungen (DRY).
/// Ersetzt duplizierte Maps in BingXRestClient und BingXDataFeed.
/// </summary>
public static class TimeFrameHelper
{
    private static readonly Dictionary<TimeFrame, string> IntervalMap = new()
    {
        [TimeFrame.M1] = "1m",
        [TimeFrame.M3] = "3m",
        [TimeFrame.M5] = "5m",
        [TimeFrame.M15] = "15m",
        [TimeFrame.M30] = "30m",
        [TimeFrame.H1] = "1h",
        [TimeFrame.H2] = "2h",
        [TimeFrame.H4] = "4h",
        [TimeFrame.H6] = "6h",
        [TimeFrame.H12] = "12h",
        [TimeFrame.D1] = "1d",
        [TimeFrame.W1] = "1w",
        [TimeFrame.MN1] = "1M"
    };

    /// <summary>
    /// Konvertiert TimeFrame-Enum in BingX API Interval-String (z.B. "1h", "15m").
    /// </summary>
    public static string ToIntervalString(TimeFrame tf) => IntervalMap.GetValueOrDefault(tf, "1h");

    /// <summary>
    /// Gibt die Dauer eines einzelnen Kline-Intervalls zurück.
    /// </summary>
    public static TimeSpan ToDuration(TimeFrame tf) => tf switch
    {
        TimeFrame.M1 => TimeSpan.FromMinutes(1),
        TimeFrame.M3 => TimeSpan.FromMinutes(3),
        TimeFrame.M5 => TimeSpan.FromMinutes(5),
        TimeFrame.M15 => TimeSpan.FromMinutes(15),
        TimeFrame.M30 => TimeSpan.FromMinutes(30),
        TimeFrame.H1 => TimeSpan.FromHours(1),
        TimeFrame.H2 => TimeSpan.FromHours(2),
        TimeFrame.H4 => TimeSpan.FromHours(4),
        TimeFrame.H6 => TimeSpan.FromHours(6),
        TimeFrame.H12 => TimeSpan.FromHours(12),
        TimeFrame.D1 => TimeSpan.FromDays(1),
        TimeFrame.W1 => TimeSpan.FromDays(7),
        TimeFrame.MN1 => TimeSpan.FromDays(30),
        _ => TimeSpan.FromHours(1)
    };
}
