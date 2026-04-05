using HandwerkerImperium.Models.Events;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Service fuer Geld-, Goldschrauben- und XP-Operationen.
/// Teil der IGameStateService Interface Segregation.
/// </summary>
public interface IGameCurrencyService
{
    // ===================================================================
    // EVENTS
    // ===================================================================

    /// <summary>Wird ausgeloest wenn sich das Geld aendert.</summary>
    event EventHandler<MoneyChangedEventArgs>? MoneyChanged;

    /// <summary>Wird ausgeloest wenn sich die Goldschrauben aendern.</summary>
    event EventHandler<GoldenScrewsChangedEventArgs>? GoldenScrewsChanged;

    /// <summary>Wird ausgeloest wenn der Spieler aufsteigt.</summary>
    event EventHandler<LevelUpEventArgs>? LevelUp;

    /// <summary>Wird ausgeloest wenn XP gewonnen werden.</summary>
    event EventHandler<XpGainedEventArgs>? XpGained;

    // ===================================================================
    // GELD-OPERATIONEN
    // ===================================================================

    /// <summary>
    /// Fuegt Geld zum Spielerkonto hinzu.
    /// </summary>
    void AddMoney(decimal amount);

    /// <summary>
    /// Versucht Geld auszugeben. Gibt true zurueck bei Erfolg.
    /// </summary>
    bool TrySpendMoney(decimal amount);

    /// <summary>
    /// Prueft ob der Spieler sich einen Betrag leisten kann.
    /// </summary>
    bool CanAfford(decimal amount);

    // ===================================================================
    // GOLDSCHRAUBEN-OPERATIONEN
    // ===================================================================

    /// <summary>
    /// Fuegt Goldschrauben zum Spielerkonto hinzu.
    /// </summary>
    void AddGoldenScrews(int amount, bool fromPurchase = false);

    /// <summary>
    /// Versucht Goldschrauben auszugeben. Gibt true zurueck bei Erfolg.
    /// </summary>
    bool TrySpendGoldenScrews(int amount);

    /// <summary>
    /// Prueft ob der Spieler genug Goldschrauben hat.
    /// </summary>
    bool CanAffordGoldenScrews(int amount);

    // ===================================================================
    // XP/LEVEL-OPERATIONEN
    // ===================================================================

    /// <summary>
    /// Fuegt dem Spieler XP hinzu. Level-Ups werden automatisch verarbeitet.
    /// </summary>
    void AddXp(int amount);
}
