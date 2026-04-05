using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Zentraler Service fuer die Verwaltung des Spielzustands.
/// Einzige Quelle der Wahrheit fuer alle Spieldaten.
/// Erbt von IGameCurrencyService, IGameWorkshopService und IGameOrderService
/// fuer granulare Abhaengigkeiten (Interface Segregation).
/// </summary>
public interface IGameStateService : IGameCurrencyService, IGameWorkshopService, IGameOrderService
{
    /// <summary>
    /// Der aktuelle Spielzustand.
    /// </summary>
    GameState State { get; }

    /// <summary>
    /// Ob das Spiel initialisiert wurde.
    /// </summary>
    bool IsInitialized { get; }

    // ===================================================================
    // AUTOMATION LEVEL-GATES
    // ===================================================================

    /// <summary>Auto-Collect freigeschaltet ab Level 15.</summary>
    bool IsAutoCollectUnlocked { get; }

    /// <summary>Auto-Accept freigeschaltet ab Level 25.</summary>
    bool IsAutoAcceptUnlocked { get; }

    /// <summary>Auto-Assign freigeschaltet ab Level 50.</summary>
    bool IsAutoAssignUnlocked { get; }

    // ===================================================================
    // EVENTS (nur noch State-uebergreifende Events)
    // ===================================================================

    /// <summary>Wird ausgeloest wenn der Spielzustand geladen wird.</summary>
    event EventHandler? StateLoaded;

    /// <summary>
    /// Feuert wenn Prestige-Shop-Boni sich geaendert haben (Kauf, Prestige-Reset, State-Load).
    /// Ermoeglicht Services mit eigenen Prestige-Shop-Caches die Invalidierung.
    /// </summary>
    event EventHandler? PrestigeShopPurchased;

    // ===================================================================
    // ZUSTANDSVERWALTUNG
    // ===================================================================

    /// <summary>
    /// Initialisiert den Spielzustand (neues Spiel oder geladen).
    /// </summary>
    void Initialize(GameState? loadedState = null);

    /// <summary>
    /// Setzt den Spielzustand fuer ein neues Spiel zurueck.
    /// </summary>
    void Reset();

    /// <summary>
    /// Prestige-Shop-Bonus-Cache invalidieren (nach Kauf, Prestige-Reset oder State-Load).
    /// Betrifft gecachte GS-Bonus, XP-Bonus und OrderReward-Bonus.
    /// Feuert auch PrestigeShopPurchased Event fuer andere Services (z.B. CraftingService).
    /// </summary>
    void InvalidatePrestigeBonusCache();

    // ===================================================================
    // LOCK-DELEGATION (fuer zukuenftige Service-Extraktion)
    // ===================================================================

    /// <summary>Fuehrt eine Aktion unter dem State-Lock aus.</summary>
    void ExecuteWithLock(Action action);

    /// <summary>Fuehrt eine Funktion unter dem State-Lock aus und gibt das Ergebnis zurueck.</summary>
    T ExecuteWithLock<T>(Func<T> func);

    // ===================================================================
    // EVENT-AUSLOESUNG (fuer extrahierte Services)
    // ===================================================================

    /// <summary>Loest WorkshopUpgraded + MoneyChanged Events aus.</summary>
    void RaiseWorkshopUpgraded(WorkshopType type, int oldLevel, int newLevel, decimal cost, decimal moneyBefore, decimal moneyAfter);

    /// <summary>Loest WorkerHired + MoneyChanged Events aus.</summary>
    void RaiseWorkerHired(WorkshopType type, Worker worker, decimal cost, int workerCount, decimal moneyBefore, decimal moneyAfter);

    /// <summary>Loest OrderCompleted Event aus.</summary>
    void RaiseOrderCompleted(Order order, decimal moneyReward, int xpReward, MiniGameRating avgRating);

    /// <summary>Loest MiniGameResultRecorded Event aus.</summary>
    void RaiseMiniGameResultRecorded(MiniGameRating rating);

    /// <summary>Loest MoneyChanged Event aus.</summary>
    void RaiseMoneyChanged(decimal oldAmount, decimal newAmount);

    // ===================================================================
    // KOMFORT-ZUGRIFFE (Law of Demeter)
    // Vermeidet tiefe Zugriffsketten wie State.Prestige.TotalPrestigeCount
    // ===================================================================

    /// <summary>Automatisierungs-Einstellungen (Auto-Collect, Auto-Accept, etc.).</summary>
    AutomationSettings Automation => State.Automation;

    /// <summary>App-Einstellungen (Sound, Sprache, Grafik, etc.).</summary>
    SettingsData Settings => State.Settings;

    /// <summary>Prestige-Daten (Tier, Punkte, History, Shop, Challenges).</summary>
    PrestigeData Prestige => State.Prestige;

    /// <summary>Statistik-Daten (Zaehler, Bestzeiten, Tracking).</summary>
    StatisticsData Statistics => State.Statistics;

    /// <summary>Tutorial-Status (SeenHints, Abschluss-Flags).</summary>
    TutorialState Tutorial => State.Tutorial;

    /// <summary>Boost-Daten (Speed, XP, Rush, Soft-Cap).</summary>
    BoostData Boosts => State.Boosts;

    /// <summary>Täglicher Fortschritt (Daily Rewards, Quick Jobs, Welcome Back, Weekly Missions).</summary>
    DailyProgressData DailyProgress => State.DailyProgress;

    /// <summary>Kosmetische Daten (Themes, Skins).</summary>
    CosmeticData Cosmetics => State.Cosmetics;

    /// <summary>Aktuelles Spieler-Level.</summary>
    int PlayerLevel => State.PlayerLevel;
}
