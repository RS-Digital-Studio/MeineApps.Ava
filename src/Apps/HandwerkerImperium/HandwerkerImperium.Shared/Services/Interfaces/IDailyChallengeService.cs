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
    /// Wird aufgerufen wenn ein QuickJob abgeschlossen wird (fuer Challenge-Tracking).
    /// </summary>
    void OnQuickJobCompleted();

    /// <summary>
    /// Wird aufgerufen wenn ein Arbeiter-Training abgeschlossen wird.
    /// </summary>
    void OnWorkerTrained();

    /// <summary>
    /// Wird aufgerufen wenn ein Crafting-Produkt eingesammelt wird.
    /// </summary>
    void OnCraftingCompleted();

    /// <summary>
    /// Wird aufgerufen wenn ein Workshop ein neues Level erreicht.
    /// </summary>
    void OnWorkshopLevelReached();

    /// <summary>
    /// Wird aufgerufen wenn Items durch Auto-Produktion hergestellt werden.
    /// </summary>
    void OnItemsAutoProduced(int count);

    /// <summary>
    /// Wird aufgerufen wenn Items manuell verkauft werden.
    /// </summary>
    void OnItemsSold(int count);

    /// <summary>
    /// Wird aufgerufen wenn ein Lieferauftrag abgeschlossen wird.
    /// </summary>
    void OnMaterialOrderCompleted();

    /// <summary>
    /// Wird ausgeloest wenn sich der Fortschritt einer Challenge aendert.
    /// </summary>
    event EventHandler? ChallengeProgressChanged;
}
