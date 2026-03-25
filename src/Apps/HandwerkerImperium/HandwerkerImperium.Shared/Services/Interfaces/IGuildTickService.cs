using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Bündelt alle periodischen Gilden-Checks (Boss, Hall, Achievements, War-Season).
/// Wird vom GameLoopService aufgerufen statt 4 einzelne Gilden-Services direkt zu kennen.
/// Reduziert GameLoopService-Dependencies um 4.
/// </summary>
public interface IGuildTickService
{
    /// <summary>
    /// Führt alle fälligen Gilden-Checks basierend auf dem Tick-Zähler aus.
    /// Prüft intern ob der Spieler Gildenmitglied ist und welche Checks fällig sind.
    /// </summary>
    void ProcessTick(GameState state, int tickCount);
}
