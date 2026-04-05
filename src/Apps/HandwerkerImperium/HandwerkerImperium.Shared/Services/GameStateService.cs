using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Zentraler Service für die Verwaltung des Spielzustands.
/// Thread-safe für Zugriff von UI-Thread und GameLoopService-Timer.
/// Aufgeteilt in Partial-Klassen: Money, Xp, Workshop, Orders.
/// </summary>
public sealed partial class GameStateService : IGameStateService
{
    private GameState _state = new();
    private readonly object _stateLock = new();

    public GameState State => _state;
    public bool IsInitialized { get; private set; }

    // Lazy-Resolution für zirkuläre Dependencies (gesetzt in App.axaml.cs nach DI-Aufbau)
    public IChallengeConstraintService? ChallengeConstraints { get; set; }

    // Automation Level-Gates (zentral, vermeidet Duplikation in ViewModels)
    // Nach dem ersten Prestige sind alle Features permanent freigeschaltet
    private bool HasEverPrestiged => _state.Prestige.TotalPrestigeCount > 0;
    public bool IsAutoCollectUnlocked => HasEverPrestiged || _state.PlayerLevel >= LevelThresholds.AutoCollect;
    public bool IsAutoAcceptUnlocked => HasEverPrestiged || _state.PlayerLevel >= LevelThresholds.AutoAccept;
    public bool IsAutoAssignUnlocked => HasEverPrestiged || _state.PlayerLevel >= LevelThresholds.AutoAssign;

    // Events
    public event EventHandler? PrestigeShopPurchased;
    public event EventHandler<MoneyChangedEventArgs>? MoneyChanged;
    public event EventHandler<LevelUpEventArgs>? LevelUp;
    public event EventHandler<XpGainedEventArgs>? XpGained;
    public event EventHandler<WorkshopUpgradedEventArgs>? WorkshopUpgraded;
    public event EventHandler<WorkerHiredEventArgs>? WorkerHired;
    public event EventHandler<OrderCompletedEventArgs>? OrderCompleted;
    public event EventHandler? StateLoaded;
    public event EventHandler<GoldenScrewsChangedEventArgs>? GoldenScrewsChanged;
    public event EventHandler<MiniGameResultRecordedEventArgs>? MiniGameResultRecorded;

    // ===================================================================
    // INITIALISIERUNG
    // ===================================================================

    public void Initialize(GameState? loadedState = null)
    {
        lock (_stateLock)
        {
            _state = loadedState ?? GameState.CreateNew();
        }

        IsInitialized = true;
        _prestigeBonusCacheDirty = true;
        StateLoaded?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        lock (_stateLock)
        {
            _state = GameState.CreateNew();
        }
        _prestigeBonusCacheDirty = true;
        StateLoaded?.Invoke(this, EventArgs.Empty);
    }
}
