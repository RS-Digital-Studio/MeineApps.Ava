namespace MeineApps.Core.Premium.Ava.Services;

/// <summary>
/// Service for 14-day trial system
/// </summary>
public interface ITrialService
{
    /// <summary>
    /// Date when trial was started
    /// </summary>
    DateTime? TrialStartDate { get; }

    /// <summary>
    /// Whether trial has been started
    /// </summary>
    bool IsTrialStarted { get; }

    /// <summary>
    /// Whether user has seen the trial offer dialog
    /// </summary>
    bool HasSeenTrialOffer { get; }

    /// <summary>
    /// Days remaining in trial (0 if expired or not started)
    /// </summary>
    int DaysRemaining { get; }

    /// <summary>
    /// Total trial duration in days (single source of truth for the progress display).
    /// </summary>
    int TrialDurationDays { get; }

    /// <summary>
    /// Whether trial is currently active (started and not expired)
    /// </summary>
    bool IsTrialActive { get; }

    /// <summary>
    /// Whether trial has expired
    /// </summary>
    bool IsTrialExpired { get; }

    /// <summary>
    /// Whether ads should be shown (trial expired and not premium)
    /// </summary>
    bool ShouldShowAds { get; }

    /// <summary>
    /// Initialize the trial service
    /// </summary>
    void Initialize();

    /// <summary>
    /// Start the 14-day trial
    /// </summary>
    void StartTrial();

    /// <summary>
    /// Mark trial offer as seen (user declined)
    /// </summary>
    void MarkTrialOfferAsSeen();

    /// <summary>
    /// Event fired when trial status changes
    /// </summary>
    event EventHandler? TrialStatusChanged;
}
