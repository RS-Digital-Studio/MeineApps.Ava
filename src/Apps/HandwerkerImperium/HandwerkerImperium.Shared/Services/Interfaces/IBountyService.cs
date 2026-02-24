using HandwerkerImperium.Models.Firebase;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet globale Community-Bounties via Firebase.
/// </summary>
public interface IBountyService
{
    /// <summary>Lädt die aktive Bounty oder erstellt eine neue.</summary>
    Task<BountyDisplayData?> GetActiveBountyAsync();

    /// <summary>Leistet einen Beitrag zur aktiven Bounty.</summary>
    Task ContributeAsync(string bountyType, long amount);

    /// <summary>Prüft ob die Bounty abgeschlossen ist und vergibt Belohnungen.</summary>
    Task CheckAndFinalizeBountyAsync();
}

/// <summary>
/// Anzeige-Daten für die aktive Community-Bounty.
/// </summary>
public class BountyDisplayData
{
    public string BountyId { get; set; } = "";
    public string Type { get; set; } = "";
    public string TypeDisplayKey { get; set; } = "";
    public long Target { get; set; }
    public long Current { get; set; }
    public long OwnContribution { get; set; }
    public int Reward { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCompleted { get; set; }

    /// <summary>Fortschritt in Prozent (0-100).</summary>
    public double ProgressPercent => Target > 0 ? Math.Min(100.0, (double)Current / Target * 100) : 0;
}
