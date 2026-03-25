namespace FitnessRechner.Models;

/// <summary>
/// Ein einzelner Aktivitäts-/Sport-Eintrag mit MET-basierter Kalorienberechnung.
/// </summary>
public class ActivityEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Date { get; set; }
    public string ActivityName { get; set; } = "";
    public int DurationMinutes { get; set; }
    public double CaloriesBurned { get; set; }
    public double MetValue { get; set; }
    public string? Note { get; set; }
}
