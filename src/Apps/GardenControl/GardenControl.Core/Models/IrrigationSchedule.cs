using SQLite;

namespace GardenControl.Core.Models;

/// <summary>
/// Zeitplan für eine Bewässerungszone.
/// Ermöglicht zeitbasierte Bewässerung (z.B. "Mo-Fr um 7:00 Uhr, 60 Sekunden").
/// Zusätzlich zur schwellenwertbasierten Automatik.
/// </summary>
[Table("IrrigationSchedules")]
public class IrrigationSchedule
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Zugehörige Zone</summary>
    [Indexed]
    public int ZoneId { get; set; }

    /// <summary>Aktiv/Deaktiviert</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Uhrzeit (Stunde, 0-23)</summary>
    public int Hour { get; set; } = 7;

    /// <summary>Uhrzeit (Minute, 0-59)</summary>
    public int Minute { get; set; }

    /// <summary>Bewässerungsdauer in Sekunden</summary>
    public int DurationSeconds { get; set; } = 60;

    /// <summary>Wochentage als Flags: Mo=1, Di=2, Mi=4, Do=8, Fr=16, Sa=32, So=64</summary>
    public int DayOfWeekFlags { get; set; } = 31; // Mo-Fr (Standard)

    /// <summary>Letzte Ausführung (UTC) - verhindert doppelte Ausführung</summary>
    public DateTime? LastExecutedUtc { get; set; }

    /// <summary>Prüft ob der Zeitplan heute an diesem Wochentag aktiv ist</summary>
    public bool IsActiveOnDay(DayOfWeek day)
    {
        var flag = day switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 4,
            DayOfWeek.Thursday => 8,
            DayOfWeek.Friday => 16,
            DayOfWeek.Saturday => 32,
            DayOfWeek.Sunday => 64,
            _ => 0
        };
        return (DayOfWeekFlags & flag) != 0;
    }

    /// <summary>Wochentage als lesbarer String (z.B. "Mo-Fr")</summary>
    public string DaysDescription
    {
        get
        {
            if (DayOfWeekFlags == 127) return "Täglich";
            if (DayOfWeekFlags == 31) return "Mo-Fr";
            if (DayOfWeekFlags == 96) return "Sa-So";

            var days = new List<string>();
            if ((DayOfWeekFlags & 1) != 0) days.Add("Mo");
            if ((DayOfWeekFlags & 2) != 0) days.Add("Di");
            if ((DayOfWeekFlags & 4) != 0) days.Add("Mi");
            if ((DayOfWeekFlags & 8) != 0) days.Add("Do");
            if ((DayOfWeekFlags & 16) != 0) days.Add("Fr");
            if ((DayOfWeekFlags & 32) != 0) days.Add("Sa");
            if ((DayOfWeekFlags & 64) != 0) days.Add("So");
            return string.Join(", ", days);
        }
    }
}
