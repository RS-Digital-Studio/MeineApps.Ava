using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Zentraler Service für Prestige-Challenge-Constraints.
/// Alle Consumer-Services (GameLoopService, OfflineProgressService etc.) fragen
/// dieses Interface ab statt selbst Challenge-Logik zu implementieren.
/// </summary>
public interface IChallengeConstraintService
{
    /// <summary>Gibt die aktiven Challenges des aktuellen Runs zurück.</summary>
    IReadOnlyList<PrestigeChallengeType> GetActiveChallenges();

    /// <summary>Prüft ob eine bestimmte Challenge aktiv ist.</summary>
    bool IsChallengeActive(PrestigeChallengeType challenge);

    /// <summary>Max Worker pro Workshop (3 bei Spartaner, sonst defaultMax).</summary>
    int GetMaxWorkers(int defaultMax);

    /// <summary>Upgrade-Kosten-Multiplikator (2.0 bei Inflationszeit, sonst 1.0).</summary>
    decimal GetUpgradeCostMultiplier();

    /// <summary>Ob Workshop-Unlock gesperrt ist (SoloMeister: nur 1 Workshop).</summary>
    bool IsWorkshopUnlockBlocked(int currentWorkshopCount);

    /// <summary>Ob Forschung gesperrt ist (OhneForschung).</summary>
    bool IsResearchBlocked();

    /// <summary>Ob Offline-Einkommen deaktiviert ist (Sprint).</summary>
    bool IsOfflineIncomeBlocked();

    /// <summary>Ob Lieferanten deaktiviert sind (KeinNetz).</summary>
    bool IsDeliveryBlocked();

    /// <summary>
    /// Challenge aktivieren/deaktivieren (Toggle). Max 3 gleichzeitig.
    /// Gibt false zurück wenn Challenge nicht togglebar (z.B. SoloMeister + QuickStart).
    /// </summary>
    bool ToggleChallenge(PrestigeChallengeType challenge);

    /// <summary>
    /// Berechnet den additiven PP-Multiplikator aller aktiven Challenges.
    /// z.B. Spartaner (+40%) + Sprint (+35%) = 1.75x.
    /// </summary>
    decimal GetChallengePpMultiplier();
}
