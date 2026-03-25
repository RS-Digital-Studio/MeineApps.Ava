using Material.Icons;

namespace FitnessRechner.Services;

/// <summary>
/// Kategorie einer Aktivität.
/// </summary>
public enum ActivityCategory
{
    Cardio,
    Kraft,
    Sport,
    Alltag
}

/// <summary>
/// Definition einer Aktivität mit MET-Wert und Metadaten.
/// </summary>
public sealed class ActivityDefinition
{
    /// <summary>RESX-Key für den lokalisierten Aktivitätsnamen.</summary>
    public required string NameKey { get; init; }

    /// <summary>MET-Wert (Metabolic Equivalent of Task).</summary>
    public required double Met { get; init; }

    /// <summary>Material Icon Kind für die Aktivität.</summary>
    public required MaterialIconKind Icon { get; init; }

    /// <summary>Kategorie (Cardio, Kraft, Sport, Alltag).</summary>
    public required ActivityCategory Category { get; init; }
}

/// <summary>
/// Statische Datenbank mit 30 häufigen Aktivitäten und ihren MET-Werten.
/// MET-Werte basieren auf dem Compendium of Physical Activities.
/// Formel: kcal = MET * Gewicht_kg * Dauer_h
/// </summary>
public static class ActivityDatabase
{
    /// <summary>
    /// Alle verfügbaren Aktivitäten, sortiert nach Kategorie und MET-Wert.
    /// </summary>
    public static IReadOnlyList<ActivityDefinition> All { get; } = new List<ActivityDefinition>
    {
        // === Cardio ===
        new() { NameKey = "ActivityWalking", Met = 3.5, Icon = MaterialIconKind.Walk, Category = ActivityCategory.Cardio },
        new() { NameKey = "ActivityBriskWalking", Met = 4.5, Icon = MaterialIconKind.Walk, Category = ActivityCategory.Cardio },
        new() { NameKey = "ActivityJogging", Met = 7.0, Icon = MaterialIconKind.Run, Category = ActivityCategory.Cardio },
        new() { NameKey = "ActivityRunning", Met = 9.8, Icon = MaterialIconKind.RunFast, Category = ActivityCategory.Cardio },
        new() { NameKey = "ActivityCycling", Met = 6.0, Icon = MaterialIconKind.Bike, Category = ActivityCategory.Cardio },
        new() { NameKey = "ActivityCyclingFast", Met = 10.0, Icon = MaterialIconKind.Bike, Category = ActivityCategory.Cardio },
        new() { NameKey = "ActivitySwimming", Met = 7.0, Icon = MaterialIconKind.Swim, Category = ActivityCategory.Cardio },
        new() { NameKey = "ActivityHiit", Met = 8.0, Icon = MaterialIconKind.FlashOutline, Category = ActivityCategory.Cardio },
        new() { NameKey = "ActivityRowing", Met = 7.0, Icon = MaterialIconKind.Rowing, Category = ActivityCategory.Cardio },
        new() { NameKey = "ActivityJumpRope", Met = 11.0, Icon = MaterialIconKind.JumpRope, Category = ActivityCategory.Cardio },
        new() { NameKey = "ActivityDancing", Met = 4.5, Icon = MaterialIconKind.HumanFemale, Category = ActivityCategory.Cardio },

        // === Kraft ===
        new() { NameKey = "ActivityWeightTraining", Met = 5.0, Icon = MaterialIconKind.Dumbbell, Category = ActivityCategory.Kraft },
        new() { NameKey = "ActivityBodyweight", Met = 3.8, Icon = MaterialIconKind.HumanHandsup, Category = ActivityCategory.Kraft },
        new() { NameKey = "ActivityCrossFit", Met = 8.0, Icon = MaterialIconKind.WeightLifter, Category = ActivityCategory.Kraft },
        new() { NameKey = "ActivityYoga", Met = 2.5, Icon = MaterialIconKind.Yoga, Category = ActivityCategory.Kraft },
        new() { NameKey = "ActivityPilates", Met = 3.0, Icon = MaterialIconKind.HumanHandsup, Category = ActivityCategory.Kraft },
        new() { NameKey = "ActivityStretching", Met = 2.3, Icon = MaterialIconKind.HumanHandsup, Category = ActivityCategory.Kraft },

        // === Sport ===
        new() { NameKey = "ActivitySoccer", Met = 7.0, Icon = MaterialIconKind.Soccer, Category = ActivityCategory.Sport },
        new() { NameKey = "ActivityBasketball", Met = 6.5, Icon = MaterialIconKind.Basketball, Category = ActivityCategory.Sport },
        new() { NameKey = "ActivityTennis", Met = 7.0, Icon = MaterialIconKind.Tennis, Category = ActivityCategory.Sport },
        new() { NameKey = "ActivityBadminton", Met = 5.5, Icon = MaterialIconKind.Badminton, Category = ActivityCategory.Sport },
        new() { NameKey = "ActivityVolleyball", Met = 4.0, Icon = MaterialIconKind.Volleyball, Category = ActivityCategory.Sport },
        new() { NameKey = "ActivityTableTennis", Met = 4.0, Icon = MaterialIconKind.TableTennis, Category = ActivityCategory.Sport },
        new() { NameKey = "ActivityHiking", Met = 5.3, Icon = MaterialIconKind.Hiking, Category = ActivityCategory.Sport },
        new() { NameKey = "ActivityClimbing", Met = 8.0, Icon = MaterialIconKind.Carabiner, Category = ActivityCategory.Sport },

        // === Alltag ===
        new() { NameKey = "ActivityGardening", Met = 3.5, Icon = MaterialIconKind.Flower, Category = ActivityCategory.Alltag },
        new() { NameKey = "ActivityCleaning", Met = 3.0, Icon = MaterialIconKind.Broom, Category = ActivityCategory.Alltag },
        new() { NameKey = "ActivityCooking", Met = 2.0, Icon = MaterialIconKind.Stove, Category = ActivityCategory.Alltag },
        new() { NameKey = "ActivityStairClimbing", Met = 8.8, Icon = MaterialIconKind.Stairs, Category = ActivityCategory.Alltag },
        new() { NameKey = "ActivityPlayingWithKids", Met = 4.0, Icon = MaterialIconKind.HumanMaleChild, Category = ActivityCategory.Alltag },
    };

    /// <summary>
    /// Berechnet verbrannte Kalorien basierend auf MET, Gewicht und Dauer.
    /// Formel: kcal = MET * Gewicht_kg * Dauer_h
    /// </summary>
    public static double CalculateCalories(double metValue, double weightKg, int durationMinutes)
    {
        if (metValue <= 0 || weightKg <= 0 || durationMinutes <= 0)
            return 0;

        var durationHours = durationMinutes / 60.0;
        return metValue * weightKg * durationHours;
    }
}
