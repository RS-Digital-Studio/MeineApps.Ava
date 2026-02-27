namespace HandwerkerImperium.Models;

/// <summary>
/// Repräsentiert das nächste empfohlene Ziel für den Spieler.
/// Dynamisch berechnet basierend auf Spielfortschritt.
/// </summary>
public class GameGoal
{
    /// <summary>Lokalisierter Beschreibungstext des Ziels.</summary>
    public string Description { get; init; } = "";

    /// <summary>Lokalisierter Belohnungstext (z.B. "x2 Einkommen!").</summary>
    public string RewardHint { get; init; } = "";

    /// <summary>Fortschritt zum Ziel (0.0-1.0).</summary>
    public double Progress { get; init; }

    /// <summary>Navigations-Route bei Tap (z.B. "imperium", "workshop_Carpenter").</summary>
    public string? NavigationRoute { get; init; }

    /// <summary>Material Icon Kind (z.B. "TrendingUp", "Star").</summary>
    public string IconKind { get; init; } = "TrendingUp";

    /// <summary>Priorität (niedrigere Zahl = höhere Prio).</summary>
    public int Priority { get; init; }
}
