using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Central service for managing the game state.
/// Acts as the single source of truth for all game data.
/// </summary>
public interface IGameStateService
{
    /// <summary>
    /// The current game state.
    /// </summary>
    GameState State { get; }

    /// <summary>
    /// Whether the game has been initialized.
    /// </summary>
    bool IsInitialized { get; }

    // ===================================================================
    // EVENTS
    // ===================================================================

    /// <summary>Fired when money changes.</summary>
    event EventHandler<MoneyChangedEventArgs>? MoneyChanged;

    /// <summary>Fired when player levels up.</summary>
    event EventHandler<LevelUpEventArgs>? LevelUp;

    /// <summary>Fired when XP is gained.</summary>
    event EventHandler<XpGainedEventArgs>? XpGained;

    /// <summary>Fired when a workshop is upgraded.</summary>
    event EventHandler<WorkshopUpgradedEventArgs>? WorkshopUpgraded;

    /// <summary>Fired when a worker is hired.</summary>
    event EventHandler<WorkerHiredEventArgs>? WorkerHired;

    /// <summary>Fired when an order is completed.</summary>
    event EventHandler<OrderCompletedEventArgs>? OrderCompleted;

    /// <summary>Fired when game state is loaded.</summary>
    event EventHandler? StateLoaded;

    /// <summary>Fired when golden screws change.</summary>
    event EventHandler<GoldenScrewsChangedEventArgs>? GoldenScrewsChanged;

    // ===================================================================
    // MONEY OPERATIONS
    // ===================================================================

    /// <summary>
    /// Adds money to the player's balance.
    /// </summary>
    void AddMoney(decimal amount);

    /// <summary>
    /// Attempts to spend money. Returns true if successful.
    /// </summary>
    bool TrySpendMoney(decimal amount);

    /// <summary>
    /// Checks if the player can afford an amount.
    /// </summary>
    bool CanAfford(decimal amount);

    // ===================================================================
    // GOLDEN SCREWS OPERATIONS
    // ===================================================================

    /// <summary>
    /// Adds golden screws to the player's balance.
    /// </summary>
    void AddGoldenScrews(int amount);

    /// <summary>
    /// Attempts to spend golden screws. Returns true if successful.
    /// </summary>
    bool TrySpendGoldenScrews(int amount);

    /// <summary>
    /// Checks if the player has enough golden screws.
    /// </summary>
    bool CanAffordGoldenScrews(int amount);

    // ===================================================================
    // XP/LEVEL OPERATIONS
    // ===================================================================

    /// <summary>
    /// Adds XP to the player. Handles level-ups automatically.
    /// </summary>
    void AddXp(int amount);

    // ===================================================================
    // WORKSHOP OPERATIONS
    // ===================================================================

    /// <summary>
    /// Gets a workshop by type.
    /// </summary>
    Workshop? GetWorkshop(WorkshopType type);

    /// <summary>
    /// Attempts to upgrade a workshop. Returns true if successful.
    /// </summary>
    bool TryUpgradeWorkshop(WorkshopType type);

    /// <summary>
    /// Attempts to hire a worker for a workshop. Returns true if successful.
    /// </summary>
    bool TryHireWorker(WorkshopType type);

    /// <summary>
    /// Checks if a workshop is unlocked at the current player level.
    /// </summary>
    bool IsWorkshopUnlocked(WorkshopType type);

    // ===================================================================
    // ORDER OPERATIONS
    // ===================================================================

    /// <summary>
    /// Starts an order (moves it to active).
    /// </summary>
    void StartOrder(Order order);

    /// <summary>
    /// Gets the currently active order.
    /// </summary>
    Order? GetActiveOrder();

    /// <summary>
    /// Records a mini-game result for the active order.
    /// </summary>
    void RecordMiniGameResult(MiniGameRating rating);

    /// <summary>
    /// Completes the active order and grants rewards.
    /// </summary>
    void CompleteActiveOrder();

    /// <summary>
    /// Cancels the active order without rewards.
    /// </summary>
    void CancelActiveOrder();

    // ===================================================================
    // STATE MANAGEMENT
    // ===================================================================

    /// <summary>
    /// Initializes the game state (new game or loaded).
    /// </summary>
    void Initialize(GameState? loadedState = null);

    /// <summary>
    /// Resets the game state for a new game.
    /// </summary>
    void Reset();

    /// <summary>
    /// Marks the state as dirty (needs saving).
    /// </summary>
    void MarkDirty();
}
