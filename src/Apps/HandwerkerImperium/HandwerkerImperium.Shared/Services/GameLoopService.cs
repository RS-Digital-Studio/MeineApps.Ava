using Avalonia.Threading;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
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
    private readonly IPrestigeService? _prestigeService;
    private readonly IQuickJobService? _quickJobService;
    private readonly IDailyChallengeService? _dailyChallengeService;
    private DispatcherTimer? _timer;
    private DateTime _sessionStart;
    private bool _isPaused;
    private bool _disposed;
    private int _tickCount;
    private string? _lastAppliedSpecialEffectId; // Verhindert doppelte Anwendung von SpecialEffects

    private const int AutoSaveIntervalTicks = 30;
    private const int EventCheckIntervalTicks = 300; // Check events every 5 minutes
    private const int DeliveryCheckIntervalTicks = 10; // Lieferung alle 10 Ticks prüfen
    private const int MasterToolCheckIntervalTicks = 120; // Meisterwerkzeuge alle 2 Minuten prüfen

    public bool IsRunning => _timer?.IsEnabled ?? false;
    public TimeSpan SessionDuration => DateTime.UtcNow - _sessionStart;

    public event EventHandler<GameTickEventArgs>? OnTick;

    public GameLoopService(
        IGameStateService gameStateService,
        ISaveGameService saveGameService,
        IWorkerService? workerService = null,
        IResearchService? researchService = null,
        IEventService? eventService = null,
        IPrestigeService? prestigeService = null,
        IQuickJobService? quickJobService = null,
        IDailyChallengeService? dailyChallengeService = null)
    {
        _gameStateService = gameStateService;
        _saveGameService = saveGameService;
        _workerService = workerService;
        _researchService = researchService;
        _eventService = eventService;
        _prestigeService = prestigeService;
        _quickJobService = quickJobService;
        _dailyChallengeService = dailyChallengeService;
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

        // 0. Research- und Gebäude-Effekte sammeln
        var researchEffects = _researchService?.GetTotalEffects();
        UpdateExtraWorkerSlots(state, researchEffects);

        // 1. Brutto-Einkommen berechnen (inkl. Research-Effizienz-Bonus, gekappt bei +50%)
        decimal grossIncome = state.TotalIncomePerSecond;

        // Prestige-Shop Income-Boni anwenden (pp_income_10/25/50)
        decimal shopIncomeBonus = GetPrestigeIncomeBonus(state);
        if (shopIncomeBonus > 0)
            grossIncome *= (1m + shopIncomeBonus);

        if (researchEffects != null && researchEffects.EfficiencyBonus > 0)
            grossIncome *= (1m + Math.Min(researchEffects.EfficiencyBonus, 0.50m));

        // 2. Event-Multiplikatoren anwenden
        var eventEffects = _eventService?.GetCurrentEffects();
        if (eventEffects != null)
        {
            grossIncome *= eventEffects.IncomeMultiplier;
        }

        // 3. Laufende Kosten berechnen (Prestige-Shop + Research + Storage-Gebäude)
        decimal costs = state.TotalCostsPerSecond;

        // Research CostReduction + WageReduction kombinieren
        decimal totalCostReduction = 0m;
        if (_prestigeService != null)
            totalCostReduction += _prestigeService.GetCostReduction();
        if (researchEffects != null)
            totalCostReduction += researchEffects.CostReduction + researchEffects.WageReduction;

        // Storage-Gebäude: Materialkosten-Reduktion
        var storage = state.GetBuilding(BuildingType.Storage);
        if (storage != null)
            totalCostReduction += storage.MaterialCostReduction * 0.5m; // 50% des Gebäude-Effekts auf Gesamtkosten

        if (totalCostReduction > 0)
            costs *= (1m - Math.Min(totalCostReduction, 0.50m)); // Cap bei 50% (verhindert Kosten → 0 Exploit)

        if (eventEffects != null)
        {
            costs *= eventEffects.CostMultiplier;
        }

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

        // TaxAudit: 10% Steuer auf Einkommen (dauerhaft während Event)
        if (eventEffects?.SpecialEffect == "tax_10_percent")
        {
            grossIncome *= 0.90m;
        }

        // 3c. Meisterwerkzeuge: Passiver Einkommens-Bonus
        decimal masterToolBonus = MasterTool.GetTotalIncomeBonus(state.CollectedMasterTools);
        if (masterToolBonus > 0)
            grossIncome *= (1m + masterToolBonus);

        // 4. Net earnings (can be negative!)
        decimal netEarnings = grossIncome - costs;

        // Speed boost doubles net earnings
        if (state.IsSpeedBoostActive && netEarnings > 0)
        {
            netEarnings *= 2m;
        }

        // Feierabend-Rush: 2x Boost (stacked mit SpeedBoost, PrestigeShop-Bonus erhöht auf 3x)
        if (state.IsRushBoostActive && netEarnings > 0)
        {
            decimal rushMultiplier = 2m;
            // Prestige-Shop Rush-Verstärker
            var rushBonus = GetPrestigeRushBonus(state);
            if (rushBonus > 0) rushMultiplier += rushBonus;
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

        // 9b. Quick Job Rotation + Daily Challenge Reset (alle 60 Ticks = 1 Minute)
        if (_tickCount % 60 == 0)
        {
            _quickJobService?.RotateIfNeeded();
            _dailyChallengeService?.CheckAndResetIfNewDay();
        }

        // 9d. Lieferant: Prüfen ob neue Lieferung generiert werden soll
        if (_tickCount % DeliveryCheckIntervalTicks == 0)
        {
            CheckAndGenerateDelivery(state);
        }

        // 9e. Meisterwerkzeuge: Prüfen ob neue Tools freigeschaltet werden
        if (_tickCount % MasterToolCheckIntervalTicks == 0)
        {
            CheckMasterTools(state);
        }

        // 9c. Reputation: Showroom-DailyReputationGain + Decay (einmal pro Tag, persistiert)
        if ((DateTime.UtcNow - state.LastReputationDecay).TotalHours >= 24)
        {
            state.LastReputationDecay = DateTime.UtcNow;

            // Showroom-Gebäude: Passive Reputation-Steigerung
            var showroom = state.GetBuilding(BuildingType.Showroom);
            if (showroom != null && showroom.DailyReputationGain > 0)
            {
                state.Reputation.ReputationScore = Math.Min(100,
                    state.Reputation.ReputationScore + (int)Math.Ceiling(showroom.DailyReputationGain));
            }

            // Reputation-Decay: Langsamer Abbau wenn keine Aufträge abgeschlossen werden
            state.Reputation.DecayReputation();
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

    /// <summary>
    /// Event für neue Meisterwerkzeug-Freischaltungen (UI-Benachrichtigung).
    /// </summary>
    public event EventHandler<MasterToolDefinition>? MasterToolUnlocked;

    /// <summary>
    /// Event für neue Lieferungen (UI-Benachrichtigung).
    /// </summary>
    public event EventHandler<SupplierDelivery>? DeliveryArrived;

    /// <summary>
    /// Prüft ob neue Lieferung generiert werden soll.
    /// Intervall: 2-5 Minuten (reduziert durch Prestige-Shop-Bonus).
    /// </summary>
    private void CheckAndGenerateDelivery(GameState state)
    {
        // Lieferung nur wenn keine wartet und Intervall abgelaufen
        if (state.PendingDelivery != null)
        {
            // Abgelaufene Lieferung entfernen
            if (state.PendingDelivery.IsExpired)
                state.PendingDelivery = null;
            return;
        }

        if (DateTime.UtcNow < state.NextDeliveryTime)
            return;

        // Neue Lieferung generieren
        var delivery = SupplierDelivery.GenerateRandom(state);
        state.PendingDelivery = delivery;

        // Nächstes Intervall: 2-5 Minuten (Prestige-Bonus reduziert)
        int baseIntervalSec = Random.Shared.Next(120, 300);
        decimal deliveryBonus = GetPrestigeDeliveryBonus(state);
        if (deliveryBonus > 0)
            baseIntervalSec = (int)(baseIntervalSec * (1m - Math.Min(deliveryBonus, 0.50m)));
        state.NextDeliveryTime = DateTime.UtcNow.AddSeconds(baseIntervalSec);

        DeliveryArrived?.Invoke(this, delivery);
    }

    /// <summary>
    /// Prüft ob neue Meisterwerkzeuge freigeschaltet werden können.
    /// </summary>
    private void CheckMasterTools(GameState state)
    {
        foreach (var def in MasterTool.GetAllDefinitions())
        {
            if (state.CollectedMasterTools.Contains(def.Id))
                continue;

            if (MasterTool.CheckEligibility(def.Id, state))
            {
                state.CollectedMasterTools.Add(def.Id);
                MasterToolUnlocked?.Invoke(this, def);
            }
        }
    }

    /// <summary>
    /// Berechnet Income-Multiplikator-Bonus aus gekauften Prestige-Shop-Items.
    /// </summary>
    private static decimal GetPrestigeIncomeBonus(GameState state)
    {
        var purchased = state.Prestige.PurchasedShopItems;
        if (purchased.Count == 0) return 0m;

        decimal bonus = 0m;
        foreach (var item in PrestigeShop.GetAllItems())
        {
            if (purchased.Contains(item.Id) && item.Effect.IncomeMultiplier > 0)
                bonus += item.Effect.IncomeMultiplier;
        }
        return bonus;
    }

    /// <summary>
    /// Berechnet Rush-Multiplikator-Bonus aus gekauften Prestige-Shop-Items.
    /// </summary>
    private static decimal GetPrestigeRushBonus(GameState state)
    {
        var purchased = state.Prestige.PurchasedShopItems;
        if (purchased.Count == 0) return 0m;

        decimal bonus = 0m;
        foreach (var item in PrestigeShop.GetAllItems())
        {
            if (purchased.Contains(item.Id) && item.Effect.RushMultiplierBonus > 0)
                bonus += item.Effect.RushMultiplierBonus;
        }
        return bonus;
    }

    /// <summary>
    /// Berechnet Lieferant-Speed-Bonus aus gekauften Prestige-Shop-Items.
    /// </summary>
    private static decimal GetPrestigeDeliveryBonus(GameState state)
    {
        var purchased = state.Prestige.PurchasedShopItems;
        if (purchased.Count == 0) return 0m;

        decimal bonus = 0m;
        foreach (var item in PrestigeShop.GetAllItems())
        {
            if (purchased.Contains(item.Id) && item.Effect.DeliverySpeedBonus > 0)
                bonus += item.Effect.DeliverySpeedBonus;
        }
        return bonus;
    }

    /// <summary>
    /// Setzt ExtraWorkerSlots auf jedem Workshop basierend auf Research + Gebäude-Boni.
    /// </summary>
    private static void UpdateExtraWorkerSlots(GameState state, ResearchEffect? researchEffects)
    {
        int researchSlots = researchEffects?.ExtraWorkerSlots ?? 0;

        // WorkshopExtension-Gebäude: Extra Slots pro Workshop
        var extension = state.GetBuilding(BuildingType.WorkshopExtension);
        int buildingSlots = extension?.ExtraWorkerSlots ?? 0;

        int totalExtra = researchSlots + buildingSlots;
        foreach (var ws in state.Workshops)
        {
            ws.ExtraWorkerSlots = totalExtra;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
