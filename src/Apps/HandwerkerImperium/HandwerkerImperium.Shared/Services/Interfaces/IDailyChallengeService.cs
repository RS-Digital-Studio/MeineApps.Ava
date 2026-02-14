using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet taegliche Herausforderungen mit Belohnungen.
/// </summary>
public interface IDailyChallengeService
{
    DailyChallengeState GetState();
    void CheckAndResetIfNewDay();
    bool ClaimReward(string challengeId);
    bool ClaimAllCompletedBonus();
    bool AreAllCompleted { get; }
    bool HasUnclaimedRewards { get; }
    decimal AllCompletedBonusAmount { get; }

    /// <summary>
    /// Setzt den Fortschritt einer Challenge zurueck (Retry per Rewarded Ad, max 1x pro Challenge).
    /// </summary>
    bool RetryChallenge(string challengeId);

    /// <summary>
    /// Wird ausgelöst wenn sich der Fortschritt einer Challenge ändert.
    /// </summary>
    event EventHandler? ChallengeProgressChanged;
}
