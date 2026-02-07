using Avalonia.Threading;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Manages the game loop timer for idle earnings, running costs,
/// worker state updates, research timers, and event checks.
/// Auto-saves every 30 seconds.
/// </summary>
public class GameLoopService : IGameLoopService, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly ISaveGameService _saveGameService;
    private readonly IWorkerService? _workerService;
    private readonly IResearchService? _researchService;
    private readonly IEventService? _eventService;
    private DispatcherTimer? _timer;
    private DateTime _sessionStart;
    private bool _isPaused;
    private bool _disposed;
    private int _tickCount;

    private const int AutoSaveIntervalTicks = 30;
    private const int EventCheckIntervalTicks = 300; // Check events every 5 minutes

    public bool IsRunning => _timer?.IsEnabled ?? false;
    public TimeSpan SessionDuration => DateTime.UtcNow - _sessionStart;

    public event EventHandler<GameTickEventArgs>? OnTick;

    public GameLoopService(
        IGameStateService gameStateService,
        ISaveGameService saveGameService,
        IWorkerService? workerService = null,
        IResearchService? researchService = null,
        IEventService? eventService = null)
    {
        _gameStateService = gameStateService;
        _saveGameService = saveGameService;
        _workerService = workerService;
        _researchService = researchService;
        _eventService = eventService;
    }

    public void Start()
    {
        if (_timer != null && _timer.IsEnabled)
            return;

        _sessionStart = DateTime.UtcNow;
        _isPaused = false;
        _tickCount = 0;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    public void Stop()
    {
        if (_timer == null) return;

        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _timer = null;

        _gameStateService.State.TotalPlayTimeSeconds += (long)SessionDuration.TotalSeconds;
        _gameStateService.State.LastPlayedAt = DateTime.UtcNow;

        _saveGameService.SaveAsync().FireAndForget();
    }

    public void Pause()
    {
        _isPaused = true;
        _timer?.Stop();

        _gameStateService.State.LastPlayedAt = DateTime.UtcNow;
        _saveGameService.SaveAsync().FireAndForget();
    }

    public void Resume()
    {
        _isPaused = false;
        _timer?.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_isPaused || !_gameStateService.IsInitialized)
            return;

        var state = _gameStateService.State;

        // 1. Calculate gross income
        decimal grossIncome = state.TotalIncomePerSecond;

        // 2. Apply event multipliers
        var eventEffects = _eventService?.GetCurrentEffects();
        if (eventEffects != null)
        {
            grossIncome *= eventEffects.IncomeMultiplier;
        }

        // 3. Calculate running costs per second
        decimal costs = state.TotalCostsPerSecond;
        if (eventEffects != null)
        {
            costs *= eventEffects.CostMultiplier;
        }

        // 4. Net earnings (can be negative!)
        decimal netEarnings = grossIncome - costs;

        // Speed boost doubles net earnings
        if (state.IsSpeedBoostActive && netEarnings > 0)
        {
            netEarnings *= 2m;
        }

        // 5. Apply net earnings
        if (netEarnings != 0)
        {
            if (netEarnings > 0)
            {
                _gameStateService.AddMoney(netEarnings);
            }
            else
            {
                // Negative: costs exceed income
                // Don't let money go below 0 from costs alone
                if (state.Money + netEarnings > 0)
                {
                    _gameStateService.TrySpendMoney(-netEarnings);
                }
            }
        }

        // 6. Track earnings per workshop
        foreach (var ws in state.Workshops)
        {
            if (ws.GrossIncomePerSecond > 0)
            {
                ws.TotalEarned += ws.GrossIncomePerSecond;
                foreach (var worker in ws.Workers.Where(w => w.IsWorking))
                {
                    worker.TotalEarned += ws.BaseIncomePerWorker * worker.EffectiveEfficiency;
                }
            }
        }

        // 7. Update worker states (mood, fatigue, XP)
        _workerService?.UpdateWorkerStates(1.0);

        // 8. Update research timer
        _researchService?.UpdateTimer(1.0);

        // 9. Check for events periodically
        _tickCount++;
        if (_tickCount % EventCheckIntervalTicks == 0)
        {
            _eventService?.CheckForNewEvent();
        }

        // 10. Update last played time
        state.LastPlayedAt = DateTime.UtcNow;

        // 11. Auto-save periodically
        if (_tickCount % AutoSaveIntervalTicks == 0)
        {
            _saveGameService.SaveAsync().FireAndForget();
        }

        // 12. Fire tick event
        OnTick?.Invoke(this, new GameTickEventArgs(
            netEarnings,
            state.Money,
            SessionDuration));
    }

    public void Dispose()
    {
        if (_disposed) return;

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
