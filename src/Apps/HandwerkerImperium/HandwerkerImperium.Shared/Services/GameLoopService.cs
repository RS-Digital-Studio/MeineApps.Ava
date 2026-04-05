using Avalonia.Threading;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Zentraler Game-Loop-Timer fuer Idle-Einkommen, laufende Kosten,
/// Worker-State-Updates, Research-Timer und Event-Checks.
/// Auto-Save alle 30 Sekunden.
///
/// Partial-Aufteilung:
/// - GameLoopService.cs (diese Datei): Felder, Konstruktor, Timer, OnTimerTick, Dispose
/// - GameLoopService.Automation.cs: AutoCollect, AutoAccept, AutoAssign
/// - GameLoopService.PeriodicChecks.cs: Periodische Pruefungen, Lieferant, Meisterwerkzeuge, Workshop-Spezialeffekte
/// - GameLoopService.PrestigeCache.cs: Prestige-Shop-Effekte-Cache
/// </summary>
public sealed partial class GameLoopService : IGameLoopService, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly ISaveGameService _saveGameService;
    private readonly IWorkerService? _workerService;
    private readonly IResearchService? _researchService;
    private readonly IEventService? _eventService;
    private readonly IPrestigeService? _prestigeService;
    private readonly IQuickJobService? _quickJobService;
    private readonly IDailyChallengeService? _dailyChallengeService;
    private readonly IWeeklyMissionService? _weeklyMissionService;
    private readonly IManagerService? _managerService;
    private readonly IGuildService? _guildService;
    private readonly ISeasonalEventService? _seasonalEventService;
    private readonly IBattlePassService? _battlePassService;
    private readonly ICraftingService? _craftingService;
    private readonly IGuildTickService? _guildTickService;
    private readonly IVipService? _vipService;
    private readonly IRebirthService? _rebirthService;
    private readonly IAscensionService? _ascensionService;
    private readonly IIncomeCalculatorService? _incomeCalculator;
    private readonly IAutoProductionService? _autoProductionService;
    private readonly IChallengeConstraintService? _challengeConstraints;
    private DispatcherTimer? _timer;
    private DateTime _sessionStart;
    private bool _isPaused;
    private bool _disposed;
    private int _tickCount;
    private string? _lastAppliedSpecialEffectId; // Verhindert doppelte Anwendung von SpecialEffects

    // Gecachte Workshop-Referenzen (vermeidet FirstOrDefault pro Tick)
    private Workshop? _cachedMasterSmith;
    private Workshop? _cachedInnovationLab;
    private bool _workshopCacheDirty = true;
    // Gecachter ExtraWorkerSlots-Wert (vermeidet redundante Zuweisung pro Tick)
    private int _lastExtraWorkerSlots = -1;
    private decimal _lastLevelResistance = -1m;
    // Gecachter MasterTool-Einkommens-Bonus (aendert sich nur bei Freischaltung, nicht pro Tick)
    private decimal _cachedMasterToolBonus = -1m;

    // Benannte Event-Handler fuer sauberes Unsubscribe in Dispose()
    private readonly EventHandler _stateLoadedHandler;
    private readonly Action? _vipLevelChangedHandler;

    private const int AutoSaveIntervalTicks = 30;
    private const int EventCheckIntervalTicks = 300; // Check events every 5 minutes
    private const int DeliveryCheckIntervalTicks = 10; // Lieferung alle 10 Ticks pruefen
    private const int MasterToolCheckIntervalTicks = 120; // Meisterwerkzeuge alle 2 Minuten pruefen
    private const int WeeklyMissionCheckIntervalTicks = 60; // Weekly Mission Reset alle 60 Ticks
    private const int ManagerCheckIntervalTicks = 120; // Manager Unlock Check alle 2 Minuten
    private const int SeasonalEventCheckIntervalTicks = 300; // Saisonales Event alle 5 Minuten pruefen
    private const int BattlePassSeasonCheckIntervalTicks = 300; // Battle Pass Saison alle 5 Minuten pruefen
    private const int AutomationCheckIntervalTicks = 5; // Automation alle 5 Ticks
    private const int AutoAssignIntervalTicks = 60; // AutoAssign alle 60 Ticks
    private const int QuickJobCheckIntervalTicks = 60; // QuickJob Rotation + Deadline-Check alle 60 Ticks

    public bool IsRunning => _timer?.IsEnabled ?? false;
    public TimeSpan SessionDuration => DateTime.UtcNow - _sessionStart;

    /// <summary>
    /// Workshop-Cache invalidieren (z.B. nach Workshop-Kauf).
    /// Invalidiert auch Prestige-Effekte, damit neue Workshops den UpgradeDiscount erhalten.
    /// </summary>
    public void InvalidateWorkshopCache()
    {
        _workshopCacheDirty = true;
        _prestigeEffectsDirty = true;
    }

    private void RefreshWorkshopCacheIfNeeded(GameState state)
    {
        if (!_workshopCacheDirty) return;
        _cachedMasterSmith = null;
        _cachedInnovationLab = null;
        for (int i = 0; i < state.Workshops.Count; i++)
        {
            var ws = state.Workshops[i];
            if (ws.Type == WorkshopType.MasterSmith && ws.IsUnlocked)
                _cachedMasterSmith = ws;
            else if (ws.Type == WorkshopType.InnovationLab && ws.IsUnlocked)
                _cachedInnovationLab = ws;
        }
        _workshopCacheDirty = false;
    }

    public event EventHandler<GameTickEventArgs>? OnTick;

    public GameLoopService(
        IGameStateService gameStateService,
        ISaveGameService saveGameService,
        IWorkerService? workerService = null,
        IResearchService? researchService = null,
        IEventService? eventService = null,
        IPrestigeService? prestigeService = null,
        IQuickJobService? quickJobService = null,
        IDailyChallengeService? dailyChallengeService = null,
        IWeeklyMissionService? weeklyMissionService = null,
        IManagerService? managerService = null,
        IGuildService? guildService = null,
        ISeasonalEventService? seasonalEventService = null,
        IBattlePassService? battlePassService = null,
        ICraftingService? craftingService = null,
        IGuildTickService? guildTickService = null,
        IVipService? vipService = null,
        IRebirthService? rebirthService = null,
        IAscensionService? ascensionService = null,
        IIncomeCalculatorService? incomeCalculator = null,
        IAutoProductionService? autoProductionService = null,
        IChallengeConstraintService? challengeConstraints = null)
    {
        _gameStateService = gameStateService;
        _saveGameService = saveGameService;
        _workerService = workerService;
        _researchService = researchService;
        _eventService = eventService;
        _prestigeService = prestigeService;
        _quickJobService = quickJobService;
        _dailyChallengeService = dailyChallengeService;
        _weeklyMissionService = weeklyMissionService;
        _managerService = managerService;
        _guildService = guildService;
        _seasonalEventService = seasonalEventService;
        _battlePassService = battlePassService;
        _craftingService = craftingService;
        _guildTickService = guildTickService;
        _vipService = vipService;
        _rebirthService = rebirthService;
        _ascensionService = ascensionService;
        _incomeCalculator = incomeCalculator;
        _autoProductionService = autoProductionService;
        _challengeConstraints = challengeConstraints;

        // Bei State-Wechsel (Load/Import/Reset/Prestige) alle Caches invalidieren
        _stateLoadedHandler = (_, _) => ResetAllCaches();
        _gameStateService.StateLoaded += _stateLoadedHandler;

        // Bei VIP-Level-Wechsel Prestige-Effekte invalidieren (aktualisiert VipCostReduction auf Workshops)
        if (_vipService != null)
        {
            _vipLevelChangedHandler = () => _prestigeEffectsDirty = true;
            _vipService.VipLevelChanged += _vipLevelChangedHandler;
        }
    }

    /// <summary>
    /// Setzt alle internen Caches zurueck (bei State-Load, Import, Prestige-Reset).
    /// Verhindert stale Referenzen auf verwaiste Objekte nach Prestige.
    /// </summary>
    private void ResetAllCaches()
    {
        _workshopCacheDirty = true;
        _prestigeEffectsDirty = true;
        _cachedMasterSmith = null;
        _cachedInnovationLab = null;
        _lastExtraWorkerSlots = -1;
        _lastLevelResistance = -1m;
        _lastAppliedSpecialEffectId = null;
        _cachedMasterToolBonus = -1m;
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

        // Nur die aktive Zeit seit letztem Start/Resume akkumulieren
        _gameStateService.Statistics.TotalPlayTimeSeconds += (long)(DateTime.UtcNow - _sessionStart).TotalSeconds;
        _gameStateService.State.LastPlayedAt = DateTime.UtcNow;

        _saveGameService.SaveAsync().FireAndForget();
    }

    public void Pause()
    {
        _isPaused = true;
        _timer?.Stop();

        // Bisherige aktive Session-Zeit akkumulieren
        _gameStateService.Statistics.TotalPlayTimeSeconds += (long)(DateTime.UtcNow - _sessionStart).TotalSeconds;
        _gameStateService.State.LastPlayedAt = DateTime.UtcNow;

        // Session-Start auf jetzt setzen, damit Stop() danach nicht die gleiche Zeitspanne nochmal zaehlt
        _sessionStart = DateTime.UtcNow;

        _saveGameService.SaveAsync().FireAndForget();
    }

    public void Resume()
    {
        _isPaused = false;
        // Session-Start neu setzen damit Pause-Zeit nicht mitgezaehlt wird
        _sessionStart = DateTime.UtcNow;
        _timer?.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_isPaused || !_gameStateService.IsInitialized)
            return;

        var state = _gameStateService.State;
        var now = DateTime.UtcNow;

        // Caches aktualisieren
        RefreshWorkshopCacheIfNeeded(state);
        RefreshPrestigeEffectsIfNeeded(state);

        // 0. Research- und Gebaeude-Effekte sammeln
        var researchEffects = _researchService?.GetTotalEffects();
        UpdateExtraWorkerSlots(state, researchEffects);

        // 1-3. Einkommen + Kosten via IncomeCalculatorService (eliminiert Duplikation mit OfflineProgress)
        if (_cachedMasterToolBonus < 0)
            _cachedMasterToolBonus = MasterTool.GetTotalIncomeBonus(state.CollectedMasterTools);

        // Event-Effects einmal holen und durchreichen (vermeidet redundante Aufrufe in IncomeCalculator)
        var eventEffects = _eventService?.GetCurrentEffects();

        decimal grossIncome = _incomeCalculator?.CalculateGrossIncome(state, _cachedPrestigeIncomeBonus, _cachedMasterToolBonus,
                                  researchEffects, eventEffects)
                              ?? state.TotalIncomePerSecond;
        grossIncome = _incomeCalculator?.ApplySoftCap(state, grossIncome) ?? grossIncome;
        decimal costs = _incomeCalculator?.CalculateCosts(state, researchEffects, eventEffects) ?? state.TotalCostsPerSecond;

        // 3b. Event-Einmaleffekte + SpecialEffects verarbeiten
        var currentEventId = state.ActiveEvent?.Id;
        if (currentEventId != null && currentEventId != _lastAppliedSpecialEffectId)
        {
            _lastAppliedSpecialEffectId = currentEventId;

            // WorkerStrike: Alle Worker-Stimmungen um 20 senken (einmalig)
            if (eventEffects?.SpecialEffect == "mood_drop_all_20")
            {
                foreach (var ws in state.Workshops)
                foreach (var worker in ws.Workers)
                    worker.Mood = Math.Max(0m, worker.Mood - 20m);
            }

            // Event-ReputationChange anwenden (einmalig bei Event-Start)
            if (eventEffects != null && eventEffects.ReputationChange != 0)
            {
                state.Reputation.ReputationScore = Math.Clamp(
                    state.Reputation.ReputationScore + (int)eventEffects.ReputationChange, 0, 100);
            }
        }
        else if (currentEventId == null)
        {
            _lastAppliedSpecialEffectId = null;
        }

        // 4. Net earnings (can be negative!)
        decimal netEarnings = grossIncome - costs;

        // Speed boost doubles net earnings
        if (state.IsSpeedBoostActive && netEarnings > 0)
        {
            netEarnings *= 2m;
        }

        // Feierabend-Rush: 2x Boost (stacked mit SpeedBoost, PrestigeShop-Bonus erhoeht auf 3x)
        if (state.IsRushBoostActive && netEarnings > 0)
        {
            decimal rushMultiplier = 2m;
            // Prestige-Shop Rush-Verstaerker (gecacht)
            if (_cachedPrestigeRushBonus > 0) rushMultiplier += _cachedPrestigeRushBonus;
            netEarnings *= rushMultiplier;
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
        // Workers.Count > 0 als Guard statt GrossIncomePerSecond > 0 (vermeidet LINQ .Sum() pro Tick)
        foreach (var ws in state.Workshops)
        {
            if (ws.Workers.Count > 0)
            {
                var grossInc = ws.GrossIncomePerSecond;
                if (grossInc > 0)
                {
                    // TotalEarned trackt absichtlich das Roh-Einkommen (GrossIncomePerSecond)
                    // pro Workshop, OHNE globale Multiplikatoren (Events, Prestige, Rush etc.).
                    // So bleibt die Workshop-Statistik vergleichbar und inflationsfrei.
                    ws.TotalEarned += grossInc;
                    for (int wi = 0; wi < ws.Workers.Count; wi++)
                    {
                        var worker = ws.Workers[wi];
                        if (!worker.IsWorking) continue;
                        // LevelFitFactor beruecksichtigen (Workshop-Level-Malus fuer niedrige Tiers)
                        worker.TotalEarned += ws.BaseIncomePerWorker * worker.EffectiveEfficiency * ws.GetWorkerLevelFitFactor(worker);
                    }
                }
            }
        }

        // 7. Update worker states (mood, fatigue, XP)
        _workerService?.UpdateWorkerStates(1.0);

        // 8. Update research timer
        _researchService?.UpdateTimer(1.0);

        // Periodische Checks (gestaffelte Intervalle mit Offsets, vermeidet Lastspitzen)
        _tickCount++;
        ProcessPeriodicChecks(state, now);

        // Housekeeping
        state.LastPlayedAt = now;
        if (_tickCount % AutoSaveIntervalTicks == 0)
            _saveGameService.SaveAsync().FireAndForget();

        OnTick?.Invoke(this, new GameTickEventArgs(netEarnings, state.Money, SessionDuration));
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Event-Subscriptions abmelden (verhindert Memory-Leaks)
        _gameStateService.StateLoaded -= _stateLoadedHandler;
        if (_vipService != null && _vipLevelChangedHandler != null)
            _vipService.VipLevelChanged -= _vipLevelChangedHandler;

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
