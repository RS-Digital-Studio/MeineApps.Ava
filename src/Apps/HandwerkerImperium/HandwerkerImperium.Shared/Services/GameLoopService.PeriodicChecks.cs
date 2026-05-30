using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Partial: Periodische Pruefungen (Lieferant, Meisterwerkzeuge, ExtraWorkerSlots,
/// MasterSmith-Produktion, InnovationLab-Bonus, Auto-Produktion).
/// </summary>
public sealed partial class GameLoopService
{
    // Tier-1-Crafting-Materialien fuer MasterSmith (static readonly, keine Allokation pro Aufruf)
    private static readonly string[] Tier1CraftingProducts = ["planks", "pipes", "cables", "paint_mix", "roof_tiles"];

    // Gecachte Anzahl aller MasterTool-Definitionen (statisch, aendert sich nie zur Laufzeit)
    private static readonly int MasterToolDefinitionCount = MasterTool.GetAllDefinitions().Count;

    /// <summary>
    /// Event wenn ein Auftrag wegen abgelaufener Deadline verfaellt.
    /// </summary>
    public event EventHandler? OrderExpired;

    /// <summary>
    /// Event fuer neue Meisterwerkzeug-Freischaltungen (UI-Benachrichtigung).
    /// </summary>
    public event EventHandler<MasterToolDefinition>? MasterToolUnlocked;

    /// <summary>
    /// Event fuer neue Lieferungen (UI-Benachrichtigung).
    /// </summary>
    public event EventHandler<SupplierDelivery>? DeliveryArrived;

    /// <summary>
    /// Alle periodischen Checks mit gestaffelten Intervallen und Offsets.
    /// Klar gruppiert statt inline in OnTimerTick.
    /// </summary>
    private void ProcessPeriodicChecks(GameState state, DateTime now)
    {
        // Jedes Tick: Crafting-Timer + Workshop-Spezialeffekte
        _craftingService?.UpdateTimers();
        ApplyInnovationLabBonus(state);

        // Alle 5 Ticks: Automation (AutoCollect, AutoAccept)
        if (_tickCount % AutomationCheckIntervalTicks == 3)
            ProcessAutomation(state);

        // Alle 10 Ticks: Lieferant-System
        if (_tickCount % DeliveryCheckIntervalTicks == 0)
            CheckAndGenerateDelivery(state, now);

        // Alle 3 Ticks: Abgelaufene Live-Auftraege entfernen (v2.0.35 Feature D)
        if (_orderGeneratorService != null && _tickCount % OrderLiveExpireCheckTicks == 0)
            _orderGeneratorService.ExpireOldLiveOrders();

        // Alle 25 Ticks: Chance fuer neuen Live-Auftrag (v2.0.35 Feature D)
        if (_orderGeneratorService != null && _tickCount % OrderLiveSpawnCheckTicks == 17)
        {
            if (Random.Shared.NextDouble() < OrderLiveSpawnProbability)
                _orderGeneratorService.GenerateLiveOrder();
        }

        // Alle 60 Ticks (1 Min): Jobs, Orders, Weekly, AutoAssign, MasterSmith
        if (_tickCount % QuickJobCheckIntervalTicks == 0)
        {
            _quickJobService?.RotateIfNeeded();
            _dailyChallengeService?.CheckAndResetIfNewDay();

            // v2.0.35 Hotfix-2: Order-Expiry unter State-Lock, schuetzt AvailableOrders +
            // ParallelOrdersByWorkshop vor Race mit SaveAsync-Serializer auf ThreadPool.
            bool activeExpired = false;
            // v2.0.36: Lazy-Init — die Liste wird nur allokiert, wenn tatsaechlich ein
            // paralleler Auftrag abgelaufen ist (typisch 0 pro Tick). Spart 60×/h
            // GC-Druck ohne Effekt zu verlieren.
            List<WorkshopType>? expiredParallel = null;
            _gameStateService.ExecuteWithLock(() =>
            {
                // Nicht-Live-Auftraege via Deadline entfernen. Live-Auftraege laufen ueber
                // ExpireOldLiveOrders (alle 3 Ticks). Wuerde hier die doppelte Pruefung
                // laufen und einen IsLive-Auftrag mitten im MiniGame raus-reissen, da IsExpired
                // jetzt auch ExpiresAt beruecksichtigt.
                state.AvailableOrders.RemoveAll(o => !o.IsLive && o.IsExpired);

                // ActiveOrder-Expiry ausloesen — schuetzt aber laufende MiniGame-Sessions:
                // Sobald mindestens ein Task angefangen/abgeschlossen wurde (CurrentTaskIndex > 0
                // oder TaskResults.Count > 0), ist der Spieler klar im MiniGame und darf nicht
                // mittendrin abgeschnitten werden.
                if (state.ActiveOrder?.IsExpired == true
                    && state.ActiveOrder.CurrentTaskIndex == 0
                    && state.ActiveOrder.TaskResults.Count == 0)
                {
                    var expiredOrder = state.ActiveOrder;
                    expiredOrder.CurrentTaskIndex = 0;
                    expiredOrder.TaskResults.Clear();
                    // V7 (): Reservierte Materialien bei Expiry zurueckgeben.
                    ReleaseExpiredOrderReservation(state, expiredOrder);
                    state.ActiveOrder = null;
                    state.ParallelOrdersByWorkshop.Remove(expiredOrder.WorkshopType);
                    activeExpired = true;
                }

                // Parallele Orders auf Expiry pruefen — dedizierte Aufraeum-Logik (v2.0.35 Feature A).
                foreach (var kv in state.ParallelOrdersByWorkshop)
                {
                    if (kv.Value.IsExpired && kv.Value != state.ActiveOrder)
                    {
                        expiredParallel ??= new List<WorkshopType>();
                        expiredParallel.Add(kv.Key);
                    }
                }
                if (expiredParallel != null)
                {
                    for (int i = 0; i < expiredParallel.Count; i++)
                    {
                        // V7 (): Reservierte Materialien zurueckgeben bevor der Order weg ist.
                        if (state.ParallelOrdersByWorkshop.TryGetValue(expiredParallel[i], out var parallelExpired))
                            ReleaseExpiredOrderReservation(state, parallelExpired);
                        state.ParallelOrdersByWorkshop.Remove(expiredParallel[i]);
                    }
                }
            });

            // Events AUSSERHALB des Locks feuern — Subscriber duerfen State-Methoden
            // aufrufen ohne Re-Entrant-Lock-Problem.
            if (activeExpired)
                OrderExpired?.Invoke(this, EventArgs.Empty);
            if (expiredParallel != null)
            {
                for (int i = 0; i < expiredParallel.Count; i++)
                    OrderExpired?.Invoke(this, EventArgs.Empty);
            }
        }
        if (_tickCount % WeeklyMissionCheckIntervalTicks == 15)
            _weeklyMissionService?.CheckAndResetIfNewWeek();
        if (_tickCount % AutoAssignIntervalTicks == 30)
            ProcessAutoAssign(state);
        if (_tickCount % 60 == 45)
            ProduceMasterSmithMaterials(state);

        // Auto-Produktion: Alle 180 Ticks (3 Min) fuer Standard-Workshops, Offset 90
        if (_tickCount % GameBalanceConstants.AutoProductionIntervalSeconds == 90 && _autoProductionService != null)
        {
            long before = state.Statistics.TotalItemsAutoProduced;
            _autoProductionService.ProduceForAllWorkshops(state);
            int produced = (int)(state.Statistics.TotalItemsAutoProduced - before);
            if (produced > 0)
            {
                _dailyChallengeService?.OnItemsAutoProduced(produced);
                _weeklyMissionService?.OnItemsAutoProduced(produced);
            }
        }

        // Auto-Craft hoeherer Tiers: Alle 360 Ticks (6 Min), Offset 270
        // Kein BP-XP/SP fuer Auto-Craft (passives Einkommen soll nicht BP/Seasonal aufpumpen)
        if (_tickCount % 360 == 270 && _autoProductionService != null)
        {
            int crafted = _autoProductionService.AutoCraftHigherTiers(state);
            if (crafted > 0)
            {
                _dailyChallengeService?.OnItemsAutoProduced(crafted);
                _weeklyMissionService?.OnItemsAutoProduced(crafted);
            }
        }

        // Alle 120 Ticks (2 Min): Manager, Meisterwerkzeuge
        if (_tickCount % ManagerCheckIntervalTicks == 60)
            _managerService?.CheckAndUnlockManagers();
        if (_tickCount % MasterToolCheckIntervalTicks == 0)
            CheckMasterTools(state);

        // Alle 300 Ticks (5 Min): Events, Saison, BattlePass
        if (_tickCount % EventCheckIntervalTicks == 0)
            _eventService?.CheckForNewEvent();
        if (_tickCount % SeasonalEventCheckIntervalTicks == 150)
            _seasonalEventService?.CheckSeasonalEvent();
        if (_tickCount % BattlePassSeasonCheckIntervalTicks == 200)
            _battlePassService?.CheckNewSeason();

        // Gilden-Checks via GuildTickService (Boss, Hall, Achievements, War-Season)
        _guildTickService?.ProcessTick(state, _tickCount);

        // Taeglich: Reputation (Showroom + Decay)
        if ((now - state.LastReputationDecay).TotalHours >= 24)
        {
            state.LastReputationDecay = now;
            // v2.0.37: Tier-Snapshot fuer Event-Detection
            var tierBefore = state.Reputation.CurrentTier;
            var showroom = state.GetBuilding(BuildingType.Showroom);
            if (showroom != null && showroom.DailyReputationGain > 0)
            {
                state.Reputation.ReputationScore = Math.Min(100,
                    state.Reputation.ReputationScore + (int)Math.Ceiling(showroom.DailyReputationGain));
            }
            state.Reputation.DecayReputation();
            if (_gameStateService is GameStateService gss)
                gss.RaiseReputationTierChangedIfNeeded(tierBefore);
        }
    }

    /// <summary>
    /// Prueft ob neue Lieferung generiert werden soll.
    /// Intervall: 2-5 Minuten (reduziert durch Prestige-Shop-Bonus).
    /// </summary>
    private void CheckAndGenerateDelivery(GameState state, DateTime now)
    {
        // Challenge: KeinNetz blockiert Lieferanten komplett
        if (_challengeConstraints?.IsDeliveryBlocked() == true) return;

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

        // Naechstes Intervall: 2-5 Minuten (Prestige-Bonus reduziert, gecacht)
        int baseIntervalSec = Random.Shared.Next(120, 300);
        decimal deliveryBonus = _cachedPrestigeDeliveryBonus;
        if (deliveryBonus > 0)
            baseIntervalSec = (int)(baseIntervalSec * (1m - Math.Min(deliveryBonus, 0.50m)));
        state.NextDeliveryTime = now.AddSeconds(baseIntervalSec);

        DeliveryArrived?.Invoke(this, delivery);
    }

    /// <summary>
    /// Prueft ob neue Meisterwerkzeuge freigeschaltet werden koennen.
    /// </summary>
    private void CheckMasterTools(GameState state)
    {
        // Early-Exit: Alle Meisterwerkzeuge bereits gesammelt → nichts zu pruefen
        if (state.CollectedMasterTools.Count >= MasterToolDefinitionCount) return;

        // Mutation von CollectedMasterTools (List) unter dem State-Lock — racet sonst mit
        // dem AutoSave-Serializer. Events erst NACH dem Lock feuern (Subscriber machen UI-Arbeit).
        List<MasterToolDefinition>? unlocked = null;
        _gameStateService.ExecuteWithLock(() =>
        {
            foreach (var def in MasterTool.GetAllDefinitions())
            {
                if (state.CollectedMasterTools.Contains(def.Id))
                    continue;

                if (MasterTool.CheckEligibility(def.Id, state))
                {
                    state.CollectedMasterTools.Add(def.Id);
                    _cachedMasterToolBonus = -1m; // Cache invalidieren nach Freischaltung
                    (unlocked ??= new List<MasterToolDefinition>()).Add(def);
                }
            }
        });

        if (unlocked != null)
            foreach (var def in unlocked)
                MasterToolUnlocked?.Invoke(this, def);
    }

    /// <summary>
    /// Setzt ExtraWorkerSlots auf jedem Workshop basierend auf Research + Gebaeude-Boni.
    /// </summary>
    private void UpdateExtraWorkerSlots(GameState state, ResearchEffect? researchEffects)
    {
        int researchSlots = researchEffects?.ExtraWorkerSlots ?? 0;

        // WorkshopExtension-Gebaeude: Extra Slots pro Workshop
        var extension = state.GetBuilding(BuildingType.WorkshopExtension);
        int buildingSlots = extension?.ExtraWorkerSlots ?? 0;

        // Gilden-Forschung: +1 Worker-Slot pro Workshop
        int guildSlots = state.GuildMembership?.ResearchWorkerSlotBonus ?? 0;

        int totalExtra = researchSlots + buildingSlots + guildSlots;
        decimal levelResistance = Math.Min(researchEffects?.LevelResistanceBonus ?? 0m, 0.50m);

        // Nur zuweisen wenn sich der Wert geaendert hat (vermeidet N Zuweisungen pro Tick)
        if (totalExtra == _lastExtraWorkerSlots && levelResistance == _lastLevelResistance)
            return;
        _lastExtraWorkerSlots = totalExtra;
        _lastLevelResistance = levelResistance;

        foreach (var ws in state.Workshops)
        {
            ws.ExtraWorkerSlots = totalExtra;
            ws.LevelResistanceBonus = levelResistance;
        }
    }

    /// <summary>
    /// MasterSmith-Spezialeffekt: Produziert passiv Crafting-Materialien wenn Workshop besetzt ist.
    /// Generiert 1 zufaelliges Tier-1-Produkt pro Minute pro arbeitendem Worker.
    /// </summary>
    private void ProduceMasterSmithMaterials(GameState state)
    {
        var masterSmith = _cachedMasterSmith;
        if (masterSmith == null) return;

        int workingWorkers = 0;
        for (int w = 0; w < masterSmith.Workers.Count; w++)
            if (masterSmith.Workers[w].IsWorking) workingWorkers++;
        if (workingWorkers <= 0) return;

        // Mutation unter dem State-Lock — sonst racet die Dictionary-Mutation mit dem
        // AutoSave-Serializer (laeuft auf dem Background-Thread unter demselben Lock und
        // enumeriert CraftingInventory) → "Collection was modified" → verschluckter Save.
        _gameStateService.ExecuteWithLock(() =>
        {
            state.CraftingInventory ??= new Dictionary<string, int>();

            for (int i = 0; i < workingWorkers; i++)
            {
                var product = Tier1CraftingProducts[Random.Shared.Next(Tier1CraftingProducts.Length)];
                if (state.CraftingInventory.ContainsKey(product))
                    state.CraftingInventory[product]++;
                else
                    state.CraftingInventory[product] = 1;
            }

            state.Statistics.TotalItemsCrafted += workingWorkers;
        });
    }

    /// <summary>
    /// InnovationLab-Spezialeffekt: Beschleunigt Forschung proportional zur Worker-Anzahl.
    /// Pro arbeitendem Worker +0.5s Extra-Fortschritt pro Tick (2 Worker = 1s, 4 = 2s).
    /// Mindestens 1 Worker muss arbeiten, damit der Bonus greift.
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

        // Aktive Forschung ueber ResearchService holen (cached intern)
        var activeResearch = _researchService.GetActiveResearch();
        if (activeResearch?.StartedAt != null)
        {
            // Proportionaler Bonus: 0.5s pro Worker (skaliert mit Belegschaft)
            // BonusSeconds-Feld statt StartedAt-Manipulation (sauber, persistierbar)
            activeResearch.BonusSeconds += workingWorkers * 0.5;
        }
    }

    /// <summary>
    /// V7 (): Gibt reservierte Materialien eines abgelaufenen Auftrags
    /// zurueck (kein Verbrauch). MUSS unter dem State-Lock laufen (Aufrufer haelt ihn bereits).
    /// </summary>
    private static void ReleaseExpiredOrderReservation(Models.GameState state, Models.Order order)
    {
        if (!order.MaterialOfferAccepted || order.MaterialOffer == null) return;
        foreach (var (productId, required) in order.MaterialOffer)
        {
            int reserved = state.ReservedInventory.GetValueOrDefault(productId, 0);
            int release = Math.Min(required, reserved);
            state.ReservedInventory[productId] = reserved - release;
            if (state.ReservedInventory[productId] <= 0)
                state.ReservedInventory.Remove(productId);
        }
        order.MaterialOfferAccepted = false;
    }
}
