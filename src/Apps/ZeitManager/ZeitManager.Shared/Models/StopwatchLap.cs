using ZeitManager.Audio;

namespace ZeitManager.Models;

public record StopwatchLap(
    int LapNumber,
    TimeSpan LapTime,
    TimeSpan TotalTime,
    DateTime Timestamp)
{
    public string LapTimeFormatted => TimeFormatHelper.Format(LapTime);
    public string TotalTimeFormatted => TimeFormatHelper.Format(TotalTime);
}
