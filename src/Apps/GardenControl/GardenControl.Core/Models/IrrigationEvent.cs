using GardenControl.Core.Enums;
using SQLite;

namespace GardenControl.Core.Models;

/// <summary>
/// Dokumentiert ein Bewässerungsereignis (Start/Stop) für die Historie.
/// </summary>
[Table("IrrigationEvents")]
public class IrrigationEvent
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Zugehörige Zone</summary>
    [Indexed]
    public int ZoneId { get; set; }

    /// <summary>Startzeitpunkt (UTC)</summary>
    public DateTime StartedAtUtc { get; set; }

    /// <summary>Endzeitpunkt (UTC), null wenn noch laufend</summary>
    public DateTime? EndedAtUtc { get; set; }

    /// <summary>Geplante Dauer in Sekunden</summary>
    public int PlannedDurationSeconds { get; set; }

    /// <summary>Tatsächliche Dauer in Sekunden</summary>
    public int ActualDurationSeconds { get; set; }

    /// <summary>Bodenfeuchtigkeit beim Start (%)</summary>
    public double MoistureAtStart { get; set; }

    /// <summary>Bodenfeuchtigkeit beim Ende (%)</summary>
    public double MoistureAtEnd { get; set; }

    /// <summary>Wie wurde die Bewässerung ausgelöst?</summary>
    public IrrigationTrigger Trigger { get; set; }
}
