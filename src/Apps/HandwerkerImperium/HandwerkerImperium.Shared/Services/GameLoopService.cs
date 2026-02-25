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
    private readonly IWeeklyMissionService? _weeklyMissionService;
    private readonly IManagerService? _managerService;
    private readonly IGuildService? _guildService;
    private readonly ISeasonalEventService? _seasonalEventService;
    private readonly IBattlePassService? _battlePassService;
    private readonly ICraftingService? _craftingService;
    private readonly ILeaderboardService? _leaderboardService;
    private readonly IBountyService? _bountyService;
    private readonly IGuildWarService? _guildWarService;
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

    private const int AutoSaveIntervalTicks = 30;
    private const int EventCheckIntervalTicks = 300; // Check events every 5 minutes
    private const int DeliveryCheckIntervalTicks = 10; // Lieferung alle 10 Ticks prüfen
    private const int MasterToolCheckIntervalTicks = 120; // Meisterwerkzeuge alle 2 Minuten prüfen
    private const int WeeklyMissionCheckIntervalTicks = 60; // Weekly Mission Reset alle 60 Ticks
    private const int ManagerCheckIntervalTicks = 120; // Manager Unlock Check alle 2 Minuten
    private const int SeasonalEventCheckIntervalTicks = 300; // Saisonales Event alle 5 Minuten prüfen
    private const int BattlePassSeasonCheckIntervalTicks = 300; // Battle Pass Saison alle 5 Minuten prüfen
    private const int AutomationCheckIntervalTicks = 5; // Automation alle 5 Ticks
    private const int AutoAssignIntervalTicks = 60; // AutoAssign alle 60 Ticks
    private const int LeaderboardSubmitIntervalTicks = 300; // Leaderboard-Score alle 5 Minuten submitten
    private const int BountyCheckIntervalTicks = 120; // Bounty-Fortschritt alle 2 Minuten prüfen
    private const int GuildWarCheckIntervalTicks = 300; // Guild-War alle 5 Minuten prüfen
    private const int QuickJobCheckIntervalTicks = 60; // QuickJob Rotation + Deadline-Check alle 60 Ticks

    // Tier-1-Crafting-Materialien für MasterSmith (static readonly, keine Allokation pro Aufruf)
    private static readonly string[] Tier1CraftingProducts = ["planks", "pipes", "cables", "paint_mix", "roof_tiles"];

    public bool IsRunning => _timer?.IsEnabled ?? false;
    public TimeSpan SessionDuration => DateTime.UtcNow - _sessionStart;

    /// <summary>
    /// Workshop-Cache invalidieren (z.B. nach Workshop-Kauf).
    /// </summary>
    public void InvalidateWorkshopCache() => _workshopCacheDirty = true;

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
        ILeaderboardService? leaderboardService = null,
        IBountyService? bountyService = null,
        IGuildWarService? guildWarService = null)
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
        _leaderboardService = leaderboardService;
        _bountyService = bountyService;
        _guildWarService = guildWarService;
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
        _gameStateService.State.TotalPlayTimeSeconds += (long)(DateTime.UtcNow - _sessionStart).TotalSeconds;
        _gameStateService.State.LastPlayedAt = DateTime.UtcNow;

        _saveGameService.SaveAsync().FireAndForget();
    }

    public void Pause()
    {
        _isPaused = true;
        _timer?.Stop();

        // Bisherige aktive Session-Zeit akkumulieren
        _gameStateService.State.TotalPlayTimeSeconds += (long)(DateTime.UtcNow - _sessionStart).TotalSeconds;
        _gameStateService.State.LastPlayedAt = DateTime.UtcNow;

        // Session-Start auf jetzt setzen, damit Stop() danach nicht die gleiche Zeitspanne nochmal zählt
        _sessionStart = DateTime.UtcNow;

        _saveGameService.SaveAsync().FireAndForget();
    }

    public void Resume()
    {
        _isPaused = false;
        // Session-Start neu setzen damit Pause-Zeit nicht mitgezählt wird
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

        // 0. Research- und Gebäude-Effekte sammeln
        var researchEffects = _researchService?.GetTotalEffects();
        UpdateExtraWorkerSlots(state, researchEffects);

        // 0b. Prestige-Shop Upgrade-Discount auf alle Workshops setzen
        // (muss pro Tick laufen, da neue Workshops jederzeit hinzukommen können)
        if (_cachedPrestigeUpgradeDiscount > 0)
        {
            for (int i = 0; i < state.Workshops.Count; i++)
                state.Workshops[i].UpgradeDiscount = _cachedPrestigeUpgradeDiscount;
        }

        // 1. Brutto-Einkommen berechnen (inkl. Research-Effizienz-Bonus, gekappt bei +50%)
        decimal grossIncome = state.TotalIncomePerSecond;

        // Prestige-Shop Income-Boni anwenden (gecacht)
        decimal shopIncomeBonus = _cachedPrestigeIncomeBonus;
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

        // 3d. Gilden-Bonus: +1% pro Gilden-Level, max +20%
        if (state.GuildMembership != null && state.GuildMembership.IncomeBonus > 0)
            grossIncome *= (1m + state.GuildMembership.IncomeBonus);

        // 3e. Gilden-Forschungs-Boni anwenden
        if (state.GuildMembership != null)
        {
            var gm = state.GuildMembership;

            // Einkommens-Bonus (+20% bei allen Forschungen)
            if (gm.ResearchIncomeBonus > 0)
                grossIncome *= (1m + gm.ResearchIncomeBonus);

            // Kosten-Reduktion (-10%)
            if (gm.ResearchCostReduction > 0)
                costs *= (1m - Math.Min(gm.ResearchCostReduction, 0.50m));

            // Effizienz-Bonus (+5%) - stackt mit Research-Effizienz
            if (gm.ResearchEfficiencyBonus > 0)
                grossIncome *= (1m + gm.ResearchEfficiencyBonus);
        }

        // 3f. Hard-Cap: Gesamt-Einkommens-Multiplikator auf +200% begrenzen (3.0x)
        // Betrifft: Prestige-Shop, Meisterwerkzeuge, Gilden-Boni, Research-Effizienz
        // baseIncome ist state.TotalIncomePerSecond (enthält bereits Prestige-PermanentMultiplier)
        if (state.TotalIncomePerSecond > 0)
        {
            decimal effectiveMultiplier = grossIncome / state.TotalIncomePerSecond;
            if (effectiveMultiplier > 3.0m)
                grossIncome = state.TotalIncomePerSecond * 3.0m;
        }

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
            // Prestige-Shop Rush-Verstärker (gecacht)
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
        foreach (var ws in state.Workshops)
        {
            if (ws.GrossIncomePerSecond > 0)
            {
                ws.TotalEarned += ws.GrossIncomePerSecond;
                for (int wi = 0; wi < ws.Workers.Count; wi++)
                {
                    var worker = ws.Workers[wi];
                    if (!worker.IsWorking) continue;
                    // LevelFitFactor berücksichtigen (Workshop-Level-Malus für niedrige Tiers)
                    worker.TotalEarned += ws.BaseIncomePerWorker * worker.EffectiveEfficiency * ws.GetWorkerLevelFitFactor(worker);
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

        // 9b. Quick Job Rotation + Daily Challenge Reset + Deadline-Check
        if (_tickCount % QuickJobCheckIntervalTicks == 0)
        {
            _quickJobService?.RotateIfNeeded();
            _dailyChallengeService?.CheckAndResetIfNewDay();

            // Abgelaufene Orders aus AvailableOrders entfernen
            state.AvailableOrders.RemoveAll(o => o.IsExpired);

            // Aktiven Order abbrechen wenn Deadline abgelaufen
            if (state.ActiveOrder?.IsExpired == true)
            {
                state.ActiveOrder.CurrentTaskIndex = 0;
                state.ActiveOrder.TaskResults.Clear();
                state.ActiveOrder = null;
                OrderExpired?.Invoke(this, EventArgs.Empty);
            }
        }

        // 9d. Lieferant: Prüfen ob neue Lieferung generiert werden soll
        if (_tickCount % DeliveryCheckIntervalTicks == 0)
        {
            CheckAndGenerateDelivery(state, now);
        }

        // 9e. Meisterwerkzeuge: Prüfen ob neue Tools freigeschaltet werden
        if (_tickCount % MasterToolCheckIntervalTicks == 0)
        {
            CheckMasterTools(state);
        }

        // 9f. Crafting-Timer jedes Tick aktualisieren
        _craftingService?.UpdateTimers();

        // 9f2. MasterSmith: Passiv Crafting-Materialien produzieren (alle 60 Ticks = 1 Minute)
        if (_tickCount % 60 == 45)
        {
            ProduceMasterSmithMaterials(state);
        }

        // 9f3. InnovationLab: Research-Geschwindigkeit verdoppeln (jedes Tick 1 Extra-Sekunde)
        ApplyInnovationLabBonus(state);

        // 9g. Weekly Mission Reset (alle 60 Ticks, Offset 15)
        if (_tickCount % WeeklyMissionCheckIntervalTicks == 15)
        {
            _weeklyMissionService?.CheckAndResetIfNewWeek();
        }

        // 9h. Manager Unlock Check (alle 120 Ticks, Offset 60)
        if (_tickCount % ManagerCheckIntervalTicks == 60)
        {
            _managerService?.CheckAndUnlockManagers();
        }

        // 9i. Gilden-Wochenziel: Firebase übernimmt Weekly-Reset bei RefreshGuildDetailsAsync()

        // 9j. Saisonales Event prüfen (alle 300 Ticks, Offset 150)
        if (_tickCount % SeasonalEventCheckIntervalTicks == 150)
        {
            _seasonalEventService?.CheckSeasonalEvent();
        }

        // 9k. Battle Pass Saison prüfen (alle 300 Ticks, Offset 200)
        if (_tickCount % BattlePassSeasonCheckIntervalTicks == 200)
        {
            _battlePassService?.CheckNewSeason();
        }

        // 9l. Automation: AutoCollect + AutoAccept (alle 5 Ticks)
        if (_tickCount % AutomationCheckIntervalTicks == 3)
        {
            ProcessAutomation(state);
        }

        // 9m. Automation: AutoAssign (alle 60 Ticks, Offset 30)
        if (_tickCount % AutoAssignIntervalTicks == 30)
        {
            ProcessAutoAssign(state);
        }

        // 9n. Leaderboard Score-Submit (alle 5 Minuten, Offset 180)
        if (_tickCount % LeaderboardSubmitIntervalTicks == 180 && _leaderboardService != null)
            _leaderboardService.SubmitScoresAsync().FireAndForget();

        // 9o. Community-Bounty Prüfung (alle 2 Minuten, Offset 90)
        if (_tickCount % BountyCheckIntervalTicks == 90 && _bountyService != null)
            _bountyService.CheckAndFinalizeBountyAsync().FireAndForget();

        // 9p. Guild-War Prüfung (alle 5 Minuten, Offset 240)
        if (_tickCount % GuildWarCheckIntervalTicks == 240 && _guildWarService != null)
            _guildWarService.CheckAndFinalizeWarAsync().FireAndForget();

        // 9c. Reputation: Showroom-DailyReputationGain + Decay (einmal pro Tag, persistiert)
        if ((now - state.LastReputationDecay).TotalHours >= 24)
        {
            state.LastReputationDecay = now;

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
        state.LastPlayedAt = now;

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
    /// Event wenn ein Auftrag wegen abgelaufener Deadline verfällt.
    /// </summary>
    public event EventHandler? OrderExpired;

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
    private void CheckAndGenerateDelivery(GameState state, DateTime now)
    {
        // Lieferung nur wenn keine wartet und Intervall abgelaufen
        if (state.PendingDelivery != null)
        {
            // Abgelaufene Lieferung entfernen
            if (state.PendingDelivery.IsExpired)
                state.PendingDelivery = null;
            return;
        }

        if (now < state.NextDeliveryTime)
            return;

        // Neue Lieferung generieren
        var delivery = SupplierDelivery.GenerateRandom(state);
        state.PendingDelivery = delivery;

        // Nächstes Intervall: 2-5 Minuten (Prestige-Bonus reduziert, gecacht)
        int baseIntervalSec = Random.Shared.Next(120, 300);
        decimal deliveryBonus = _cachedPrestigeDeliveryBonus;
        if (deliveryBonus > 0)
            baseIntervalSec = (int)(baseIntervalSec * (1m - Math.Min(deliveryBonus, 0.50m)));
        state.NextDeliveryTime = now.AddSeconds(baseIntervalSec);

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

    // Gecachte Prestige-Shop-Effekte (werden nur bei Kauf invalidiert)
    private bool _prestigeEffectsDirty = true;
    private decimal _cachedPrestigeIncomeBonus;
    private decimal _cachedPrestigeRushBonus;
    private decimal _cachedPrestigeDeliveryBonus;
    private decimal _cachedPrestigeUpgradeDiscount;

    /// <summary>
    /// Prestige-Effekt-Cache invalidieren (nach Prestige-Shop-Kauf oder Prestige-Reset).
    /// </summary>
    public void InvalidatePrestigeEffects() => _prestigeEffectsDirty = true;

    /// <summary>
    /// Berechnet alle 3 Prestige-Shop-Boni in einem einzigen Durchlauf und cacht sie.
    /// </summary>
    private void RefreshPrestigeEffectsIfNeeded(GameState state)
    {
        if (!_prestigeEffectsDirty) return;
        _prestigeEffectsDirty = false;

        _cachedPrestigeIncomeBonus = 0m;
        _cachedPrestigeRushBonus = 0m;
        _cachedPrestigeDeliveryBonus = 0m;
        _cachedPrestigeUpgradeDiscount = 0m;

        var purchased = state.Prestige.PurchasedShopItems;
        if (purchased.Count == 0) return;

        var allItems = PrestigeShop.GetAllItems();
        for (int i = 0; i < allItems.Count; i++)
        {
            var item = allItems[i];
            if (!purchased.Contains(item.Id)) continue;

            if (item.Effect.IncomeMultiplier > 0)
                _cachedPrestigeIncomeBonus += item.Effect.IncomeMultiplier;
            if (item.Effect.RushMultiplierBonus > 0)
                _cachedPrestigeRushBonus += item.Effect.RushMultiplierBonus;
            if (item.Effect.DeliverySpeedBonus > 0)
                _cachedPrestigeDeliveryBonus += item.Effect.DeliverySpeedBonus;
            if (item.Effect.UpgradeDiscount > 0)
                _cachedPrestigeUpgradeDiscount += item.Effect.UpgradeDiscount;
        }
    }

    /// <summary>
    /// Event wenn Automation eine Lieferung eingesammelt hat.
    /// </summary>
    public event EventHandler<SupplierDelivery>? AutoCollectedDelivery;

    /// <summary>
    /// Event wenn Automation einen Auftrag angenommen hat.
    /// </summary>
    public event EventHandler<Order>? AutoAcceptedOrder;

    /// <summary>
    /// Verarbeitet AutoCollect und AutoAccept Automation.
    /// </summary>
    private void ProcessAutomation(GameState state)
    {
        var auto = state.Automation;

        // AutoCollect: Lieferung einsammeln wenn vorhanden
        if (auto.AutoCollectDelivery && state.PlayerLevel >= 15 && state.PendingDelivery != null && !state.PendingDelivery.IsExpired)
        {
            var delivery = state.PendingDelivery;
            state.PendingDelivery = null;
            state.TotalDeliveriesClaimed++;

            // Lieferungs-Effekt anwenden
            switch (delivery.Type)
            {
                case DeliveryType.Money:
                    _gameStateService.AddMoney(delivery.Amount);
                    break;
                case DeliveryType.GoldenScrews:
                    _gameStateService.AddGoldenScrews((int)delivery.Amount);
                    break;
                case DeliveryType.Experience:
                    _gameStateService.AddXp((int)delivery.Amount);
                    break;
                case DeliveryType.MoodBoost:
                    foreach (var ws in state.Workshops)
                    foreach (var w in ws.Workers)
                        w.Mood = Math.Min(100m, w.Mood + delivery.Amount);
                    break;
                case DeliveryType.SpeedBoost:
                    state.SpeedBoostEndTime = DateTime.UtcNow.AddMinutes((double)delivery.Amount);
                    break;
            }

            AutoCollectedDelivery?.Invoke(this, delivery);
        }

        // AutoAccept: Besten Auftrag annehmen wenn kein aktiver vorhanden
        if (auto.AutoAcceptOrder && state.PlayerLevel >= 25 && state.ActiveOrder == null && state.AvailableOrders.Count > 0)
        {
            // Besten Auftrag wählen (höchste Belohnung) - ohne LINQ um Allokationen zu vermeiden
            Order? bestOrder = null;
            for (int i = 0; i < state.AvailableOrders.Count; i++)
            {
                var order = state.AvailableOrders[i];
                if (bestOrder == null || order.BaseReward > bestOrder.BaseReward)
                    bestOrder = order;
            }

            if (bestOrder != null)
            {
                state.ActiveOrder = bestOrder;
                state.AvailableOrders.Remove(bestOrder);
                AutoAcceptedOrder?.Invoke(this, bestOrder);
            }
        }
    }

    /// <summary>
    /// Verarbeitet AutoAssign: Weist idle Worker dem Workshop mit den meisten freien Plätzen zu.
    /// </summary>
    private void ProcessAutoAssign(GameState state)
    {
        if (!state.Automation.AutoAssignWorkers || state.PlayerLevel < 50)
            return;

        // Idle Worker finden (nicht zugewiesen zu einem Workshop)
        foreach (var ws in state.Workshops)
        {
            if (ws.Workers.Count >= ws.MaxWorkers) continue;

            // AutoAssign: Ruhende Worker mit niedriger Erschöpfung wieder arbeiten lassen
            foreach (var worker in ws.Workers)
            {
                if (worker.IsResting && worker.Fatigue <= 20m)
                {
                    worker.IsResting = false;
                }
            }
        }
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

        // Gilden-Forschung: +1 Worker-Slot pro Workshop
        int guildSlots = state.GuildMembership?.ResearchWorkerSlotBonus ?? 0;

        int totalExtra = researchSlots + buildingSlots + guildSlots;
        decimal levelResistance = Math.Min(researchEffects?.LevelResistanceBonus ?? 0m, 0.50m);

        foreach (var ws in state.Workshops)
        {
            ws.ExtraWorkerSlots = totalExtra;
            ws.LevelResistanceBonus = levelResistance;
        }
    }

    /// <summary>
    /// MasterSmith-Spezialeffekt: Produziert passiv Crafting-Materialien wenn Workshop besetzt ist.
    /// Generiert 1 zufälliges Tier-1-Produkt pro Minute pro arbeitendem Worker.
    /// </summary>
    private void ProduceMasterSmithMaterials(GameState state)
    {
        var masterSmith = _cachedMasterSmith;
        if (masterSmith == null) return;

        int workingWorkers = 0;
        for (int w = 0; w < masterSmith.Workers.Count; w++)
            if (masterSmith.Workers[w].IsWorking) workingWorkers++;
        if (workingWorkers <= 0) return;

        state.CraftingInventory ??= new Dictionary<string, int>();

        for (int i = 0; i < workingWorkers; i++)
        {
            var product = Tier1CraftingProducts[Random.Shared.Next(Tier1CraftingProducts.Length)];
            if (state.CraftingInventory.ContainsKey(product))
                state.CraftingInventory[product]++;
            else
                state.CraftingInventory[product] = 1;
        }

        state.TotalItemsCrafted += workingWorkers;
    }

    /// <summary>
    /// InnovationLab-Spezialeffekt: Verdoppelt Research-Geschwindigkeit wenn Workshop besetzt ist.
    /// Schiebt die StartedAt-Zeit der aktiven Forschung pro Tick um 1 Extra-Sekunde nach vorne.
    /// </summary>
    private void ApplyInnovationLabBonus(GameState state)
    {
        if (_researchService == null) return;
        if (string.IsNullOrEmpty(state.ActiveResearchId)) return;

        var innovationLab = _cachedInnovationLab;
        if (innovationLab == null) return;

        int workingWorkers = 0;
        for (int w = 0; w < innovationLab.Workers.Count; w++)
            if (innovationLab.Workers[w].IsWorking) workingWorkers++;
        if (workingWorkers <= 0) return;

        // Aktive Forschung über ResearchService holen (cached intern)
        var activeResearch = _researchService.GetActiveResearch();
        if (activeResearch?.StartedAt != null)
        {
            activeResearch.StartedAt = activeResearch.StartedAt.Value.AddSeconds(-1);
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
