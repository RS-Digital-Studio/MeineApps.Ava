namespace HandwerkerImperium.Models;

/// <summary>
/// Interface für Models die einen Fortschrittswert bereitstellen (DailyChallenge, WeeklyMission).
/// Ersetzt Reflection-basierten Zugriff auf Progress-Property.
/// </summary>
public interface IProgressProvider
{
    double Progress { get; }
}
