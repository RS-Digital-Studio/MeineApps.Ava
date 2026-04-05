namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Berechnet Einnahmen waehrend der Spieler offline war.
/// </summary>
public interface IOfflineProgressService
{
    /// <summary>
    /// Calculates and applies offline progress.
    /// Returns the earnings amount.
    /// </summary>
    decimal CalculateOfflineProgress();

    /// <summary>
    /// Gets the maximum offline duration (depends on premium status).
    /// </summary>
    TimeSpan GetMaxOfflineDuration();

    /// <summary>
    /// Checks how long the player was offline.
    /// </summary>
    TimeSpan GetOfflineDuration();
}
