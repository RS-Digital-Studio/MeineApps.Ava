using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

public interface IGoalService
{
    /// <summary>
    /// Berechnet das aktuell empfohlene Ziel.
    /// Gecacht, wird nur bei Invalidierung neu berechnet.
    /// </summary>
    GameGoal? GetCurrentGoal();

    /// <summary>
    /// Erzwingt Neuberechnung (z.B. nach Workshop-Upgrade).
    /// </summary>
    void Invalidate();
}
