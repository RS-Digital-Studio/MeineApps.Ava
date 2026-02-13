using SQLite;

namespace ZeitManager.Models;

[Table("TimerPresets")]
public class TimerPreset
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public long DurationTicks { get; set; }

    public bool AutoRepeat { get; set; }

    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("O");

    [Ignore]
    public TimeSpan Duration
    {
        get => TimeSpan.FromTicks(DurationTicks);
        set => DurationTicks = value.Ticks;
    }

    [Ignore]
    public string DurationFormatted
    {
        get
        {
            var ts = Duration;
            if ((int)ts.TotalHours > 0)
                return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
            return $"{ts.Minutes}m {ts.Seconds:D2}s";
        }
    }
}
