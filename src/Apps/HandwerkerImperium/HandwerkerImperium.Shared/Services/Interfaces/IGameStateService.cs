using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Zentraler Service fuer die Verwaltung des Spielzustands.
/// Einzige Quelle der Wahrheit fuer alle Spieldaten.
/// </summary>
public interface IGameStateService
{
    // ===================================================================
    // STATE
    // ===================================================================

    /// <summary>Der aktuelle Spielzustand.</summary>
    GameState State { get; }

    /// <summary>Ob das Spiel initialisiert wurde.</summary>
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
    // EVENTS — CURRENCY
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
    // EVENTS — WORKSHOP
    // ===================================================================

    /// <summary>Wird ausgeloest wenn eine Werkstatt aufgewertet wird.</summary>
    event EventHandler<WorkshopUpgradedEventArgs>? WorkshopUpgraded;

    /// <summary>Wird ausgeloest wenn ein Arbeiter eingestellt wird.</summary>
    event EventHandler<WorkerHiredEventArgs>? WorkerHired;

    // ===================================================================
    // EVENTS — ORDERS
    // ===================================================================

    /// <summary>Wird ausgeloest wenn ein Auftrag abgeschlossen wird.</summary>
    event EventHandler<OrderCompletedEventArgs>? OrderCompleted;

    /// <summary>Wird ausgeloest wenn ein MiniGame-Ergebnis aufgezeichnet wird.</summary>
    event EventHandler<MiniGameResultRecordedEventArgs>? MiniGameResultRecorded;

    // ===================================================================
    // EVENTS — STATE
    // ===================================================================

    /// <summary>Wird ausgeloest wenn der Spielzustand geladen wird.</summary>
    event EventHandler? StateLoaded;

    /// <summary>
    /// Feuert wenn Prestige-Shop-Boni sich geaendert haben (Kauf, Prestige-Reset, State-Load).
    /// Ermoeglicht Services mit eigenen Prestige-Shop-Caches die Invalidierung.
    /// </summary>
    event EventHandler? PrestigeShopPurchased;

    // ===================================================================
    // GELD / GOLDSCHRAUBEN / XP
    // ===================================================================

    /// <summary>Fuegt Geld zum Spielerkonto hinzu.</summary>
    void AddMoney(decimal amount);

    /// <summary>Versucht Geld auszugeben. Gibt true zurueck bei Erfolg.</summary>
    bool TrySpendMoney(decimal amount);

    /// <summary>Prueft ob der Spieler sich einen Betrag leisten kann.</summary>
    bool CanAfford(decimal amount);

    /// <summary>Fuegt Goldschrauben zum Spielerkonto hinzu.</summary>
    void AddGoldenScrews(int amount, bool fromPurchase = false);

    /// <summary>Versucht Goldschrauben auszugeben. Gibt true zurueck bei Erfolg.</summary>
    bool TrySpendGoldenScrews(int amount);

    /// <summary>Prueft ob der Spieler genug Goldschrauben hat.</summary>
    bool CanAffordGoldenScrews(int amount);

    /// <summary>Fuegt dem Spieler XP hinzu. Level-Ups werden automatisch verarbeitet.</summary>
    void AddXp(int amount);

    // ===================================================================
    // WERKSTATT-OPERATIONEN
    // ===================================================================

    /// <summary>Gibt eine Werkstatt nach Typ zurueck.</summary>
    Workshop? GetWorkshop(WorkshopType type);

    /// <summary>Versucht eine Werkstatt aufzuwerten. Gibt true zurueck bei Erfolg.</summary>
    bool TryUpgradeWorkshop(WorkshopType type);

    /// <summary>
    /// Upgradet einen Workshop mehrfach (Bulk Buy). Gibt Anzahl Upgrades zurueck.
    /// count=0 bedeutet Max (so viele wie bezahlbar).
    /// </summary>
    int TryUpgradeWorkshopBulk(WorkshopType type, int count);

    /// <summary>Versucht einen Arbeiter fuer eine Werkstatt einzustellen.</summary>
    bool TryHireWorker(WorkshopType type);

    /// <summary>Prueft ob eine Werkstatt beim aktuellen Spieler-Level freigeschaltet ist.</summary>
    bool IsWorkshopUnlocked(WorkshopType type);

    /// <summary>
    /// Kauft eine Werkstatt frei (Level-Anforderung muss erfuellt sein, Kosten werden abgezogen).
    /// </summary>
    bool TryPurchaseWorkshop(WorkshopType type, decimal costOverride = -1);

    /// <summary>Prueft ob eine Werkstatt kaufbar ist (Level erreicht, nicht bereits freigeschaltet).</summary>
    bool CanPurchaseWorkshop(WorkshopType type);

    // ===================================================================
    // AUFTRAGS-OPERATIONEN
    // ===================================================================

    /// <summary>Startet einen Auftrag (verschiebt ihn in den aktiven Status).</summary>
    void StartOrder(Order order);

    /// <summary>Gibt den aktuell aktiven Auftrag zurueck.</summary>
    Order? GetActiveOrder();

    /// <summary>Zeichnet ein MiniGame-Ergebnis fuer den aktiven Auftrag auf.</summary>
    void RecordMiniGameResult(MiniGameRating rating);

    /// <summary>Schliesst den aktiven Auftrag ab und vergibt Belohnungen.</summary>
    void CompleteActiveOrder();

    /// <summary>
    /// Berechnet den kombinierten Auftrags-Belohnungsmultiplikator aus Research, Gebaeuden,
    /// Reputation, Events und Stammkunden. Wird fuer korrekte Belohnungsanzeige in MiniGames verwendet.
    /// </summary>
    decimal GetOrderRewardMultiplier(Order order);

    /// <summary>Bricht den aktiven Auftrag ohne Belohnungen ab.</summary>
    void CancelActiveOrder();

    /// <summary>
    /// Schliesst einen Lieferauftrag ab: Items abziehen, Belohnung gutschreiben.
    /// Gibt den Geld-Ertrag zurueck (0 wenn nicht moeglich).
    /// </summary>
    decimal CompleteMaterialOrder(Order order);

    /// <summary>Zaehlt ein Perfect-Rating fuer den angegebenen MiniGame-Typ.</summary>
    void RecordPerfectRating(MiniGameType type);

    /// <summary>
    /// Prueft ob Auto-Complete fuer diesen MiniGame-Typ verfuegbar ist.
    /// Premium-Spieler: 25 Perfects, Free-Spieler: 50 Perfects.
    /// </summary>
    bool CanAutoComplete(MiniGameType type, bool isPremium);

    // ===================================================================
    // ZUSTANDSVERWALTUNG
    // ===================================================================

    /// <summary>Initialisiert den Spielzustand (neues Spiel oder geladen).</summary>
    void Initialize(GameState? loadedState = null);

    /// <summary>Setzt den Spielzustand fuer ein neues Spiel zurueck.</summary>
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
    // KOMFORT-ZUGRIFFE (Law of Demeter)
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
