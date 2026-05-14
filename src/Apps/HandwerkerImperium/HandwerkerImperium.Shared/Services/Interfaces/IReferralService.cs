using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Friend-Invite Reward-Loop. K-Factor-Driver — Voodoo / Lion erreichen
/// 30% ihrer Installs ueber solche Empfehlungssysteme.
/// </summary>
public interface IReferralService
{
    /// <summary>Stellt sicher dass ein eigener Code generiert ist (idempotent).</summary>
    void EnsureOwnCode();

    /// <summary>Liefert den eigenen 6-stelligen Code.</summary>
    string GetOwnCode();

    /// <summary>Spieler gibt einen fremden Code ein (One-Shot beim ersten Start).</summary>
    /// <returns>true bei Erfolg, false wenn schon einmal genutzt oder ungueltig.</returns>
    bool SubmitReferralCode(string code);

    /// <summary>Wird vom Server / Telemetrie ausgeloest wenn ein eingeladener Spieler 24h aktiv war.</summary>
    void OnReferralSucceeded();

    /// <summary>Pruefst und zahlt die naechste eingeloeste Tier-Belohnung aus.</summary>
    /// <returns>Geclaimter Tier (1, 5, 10) oder null wenn nichts faellig.</returns>
    int? TryClaimNextTier();

    /// <summary>Aktueller Stand der Empfehlungen.</summary>
    int SuccessfulReferrals { get; }

    /// <summary>Permanenter Income-Bonus (max 5% bei Tier 10).</summary>
    decimal PermanentIncomeBonus { get; }

    /// <summary>Event fuer UI: Tier wurde freigeschaltet (1/5/10).</summary>
    event EventHandler<int>? TierUnlocked;
}
