using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// 7-Tage-Limited-Time-Events. RemoteConfig-getrieben, mit eigenem
/// Reward-Track. Erzeugt FOMO + Re-Engagement.
///
/// Lifecycle:
/// 1. Beim Loading-Pipeline-Step wird <see cref="InitializeAsync"/> aufgerufen.
/// 2. Aktives Event aus RemoteConfig laden (oder null).
/// 3. <see cref="AddScore"/> wird von Game-Code aufgerufen (z.B. nach Order-Complete bei DoubleReward).
/// 4. <see cref="TryClaimNextReward"/> zahlt eingelöste Tier-Belohnungen aus.
/// </summary>
public interface ILiveEventService
{
    /// <summary>Aktives Event (null = kein Event aktiv).</summary>
    LiveEvent? CurrentEvent { get; }

    /// <summary>Aktuelles Template (null = kein Event aktiv).</summary>
    LiveEventTemplate? CurrentTemplate { get; }

    /// <summary>Reward-Tier-Schwellen (z.B. 100, 500, 2000 Punkte).</summary>
    IReadOnlyList<int> RewardTierThresholds { get; }

    /// <summary>Initialisiert aus RemoteConfig.</summary>
    Task InitializeAsync();

    /// <summary>Spieler-Aktion erhoeht den Score (z.B. nach Order-Complete).</summary>
    void AddScore(long points);

    /// <summary>Pruefst ob das Event noch laeuft (vergleicht EndsAtIso mit UtcNow).</summary>
    bool IsActive { get; }

    /// <summary>Naechste faellige Reward-Tier auszahlen.</summary>
    /// <returns>Reward-Tier-Index (0/1/2) oder null wenn nichts faellig.</returns>
    int? TryClaimNextReward();

    /// <summary>Event: neues Event gestartet.</summary>
    event EventHandler<LiveEvent>? EventStarted;

    /// <summary>Event: aktuelles Event abgelaufen.</summary>
    event EventHandler<LiveEvent>? EventEnded;
}
