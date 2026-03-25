namespace FitnessRechner.Models;

/// <summary>
/// Fasten-Plan-Typen für Intervallfasten.
/// </summary>
public enum FastingPlan
{
    /// <summary>16 Stunden fasten, 8 Stunden essen.</summary>
    Plan16_8,

    /// <summary>18 Stunden fasten, 6 Stunden essen.</summary>
    Plan18_6,

    /// <summary>20 Stunden fasten, 4 Stunden essen.</summary>
    Plan20_4,

    /// <summary>Benutzerdefinierte Fasten-/Essenszeiten.</summary>
    Custom
}

/// <summary>
/// Ein einzelner Fasten-Eintrag in der History.
/// </summary>
public class FastingRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Startzeit (UTC).</summary>
    public DateTime StartTime { get; set; }

    /// <summary>Endzeit (UTC). Null wenn noch aktiv.</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>Verwendeter Fasten-Plan.</summary>
    public FastingPlan Plan { get; set; }

    /// <summary>Fasten-Dauer in Stunden (Plan-Vorgabe).</summary>
    public int FastingHours { get; set; }

    /// <summary>Wurde die Fasten-Periode vollständig durchgehalten?</summary>
    public bool IsCompleted { get; set; }
}
