using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Implementiert das 7-Tier Prestige-System (Bronze bis Legende).
/// Jeder Tier setzt Fortschritt zurück, gewährt permanente Multiplikatoren und Prestige-Punkte.
/// Höhere Tiers bewahren progressiv mehr Spielfortschritt.
/// </summary>
public sealed partial class PrestigeService : IPrestigeService
{
    private readonly IGameStateService _gameStateService;
    private readonly ISaveGameService _saveGameService;
    private readonly IAscensionService _ascensionService;

    // Gecachte Prestige-Shop-Effekte (invalidiert bei Shop-Kauf und State-Load)
    private bool _effectCacheDirty = true;
    private decimal _cachedCostReduction;
    private decimal _cachedMoodDecayReduction;
    private decimal _cachedXpMultiplier;

    public event EventHandler? PrestigeCompleted;

    public PrestigeService(
        IGameStateService gameStateService,
        ISaveGameService saveGameService,
        IAscensionService ascensionService)
    {
        _gameStateService = gameStateService;
        _saveGameService = saveGameService;
        _ascensionService = ascensionService;

        // Bei State-Wechsel (Load/Import/Reset/Prestige) Cache invalidieren
        _gameStateService.StateLoaded += (_, _) => _effectCacheDirty = true;
    }

    public bool CanPrestige(PrestigeTier tier)
    {
        if (tier == PrestigeTier.None) return false;

        var state = _gameStateService.State;
        return state.Prestige.CanPrestige(tier, state.PlayerLevel);
    }

    public int GetPrestigePoints(decimal currentRunMoney)
    {
        // Basis-Punkte nur aus dem aktuellen Durchlauf (floor(sqrt(currentRunMoney / 100_000)))
        return PrestigeData.CalculatePrestigePoints(currentRunMoney);
    }

    public async Task<bool> DoPrestige(PrestigeTier tier)
    {
        if (!CanPrestige(tier)) return false;

        var state = _gameStateService.State;
        var prestige = state.Prestige;

        // Prestige-Punkte berechnen (nur aktueller Durchlauf, nicht kumulativ)
        int basePoints = GetPrestigePoints(state.CurrentRunMoney);
        int tierPoints = (int)Math.Round(basePoints * tier.GetPointMultiplier());

        // Bronze: Mindestens 15 PP (BAL-12: von 10 erhöht, damit beim ersten Prestige 3-4 Shop-Items kaufbar sind)
        if (tier == PrestigeTier.Bronze && tierPoints < 15)
            tierPoints = 15;

        // Challenge-PP: Additiver Bonus für aktive Run-Modifikatoren
        // z.B. Spartaner (+40%) + Sprint (+35%) = ×1.75
        if (prestige.ActiveChallenges.Count > 0)
        {
            decimal challengeMultiplier = ((IReadOnlyList<PrestigeChallengeType>)prestige.ActiveChallenges)
                .GetTotalPpMultiplier();
            tierPoints = (int)Math.Round(tierPoints * challengeMultiplier);
        }

        // Prestige-Pass: +50% Bonus auf Prestige-Punkte
        if (state.IsPrestigePassActive)
            tierPoints = (int)Math.Round(tierPoints * 1.5m);

        // Gilden-Forschung: Prestige-Punkte-Bonus (+10%)
        if (state.GuildMembership?.ResearchPrestigePointBonus > 0)
            tierPoints = (int)Math.Round(tierPoints * (1m + state.GuildMembership.ResearchPrestigePointBonus));

        // Bonus-PP aus Spielleistung (flat, NACH Tier-Multiplikator addiert)
        int bonusPp = CalculateBonusPrestigePoints(tier);
        tierPoints += bonusPp;

        prestige.PrestigePoints += tierPoints;
        prestige.TotalPrestigePoints += tierPoints;

        // Speedrun-Tracking: Run-Dauer berechnen VOR dem Reset
        var runDuration = prestige.RunStartTime > DateTime.MinValue
            ? DateTime.UtcNow - prestige.RunStartTime
            : TimeSpan.Zero;

        // Bestzeit prüfen und aktualisieren
        if (runDuration > TimeSpan.Zero)
        {
            var tierKey = tier.ToString();
            if (!prestige.BestRunTimes.TryGetValue(tierKey, out var bestTicks)
                || runDuration.Ticks < bestTicks)
            {
                prestige.BestRunTimes[tierKey] = runDuration.Ticks;
            }
        }

        // Tier-Zaehler erhoehen (VOR Multiplikator-Berechnung, damit tierCount stimmt)
        switch (tier)
        {
            case PrestigeTier.Bronze:
                prestige.BronzeCount++;
                break;
            case PrestigeTier.Silver:
                prestige.SilverCount++;
                break;
            case PrestigeTier.Gold:
                prestige.GoldCount++;
                break;
            case PrestigeTier.Platin:
                prestige.PlatinCount++;
                break;
            case PrestigeTier.Diamant:
                prestige.DiamantCount++;
                break;
            case PrestigeTier.Meister:
                prestige.MeisterCount++;
                break;
            case PrestigeTier.Legende:
                prestige.LegendeCount++;
                break;
        }

        // Hoechsten Tier tracken
        if (tier > prestige.CurrentTier)
            prestige.CurrentTier = tier;

        // Diminishing Returns: Jeder weitere Prestige desselben Tiers gibt weniger Bonus
        // Formel: baseBonus * 1/(1 + 0.1 * tierCount) - erster Prestige voller Bonus, 10. nur noch 50%
        // tierCount ist NACH Inkrement (oben), daher -1 fuer den Wert VOR diesem Prestige
        decimal baseBonus = tier.GetPermanentMultiplierBonus();
        int tierCount = tier switch
        {
            PrestigeTier.Bronze => prestige.BronzeCount - 1,
            PrestigeTier.Silver => prestige.SilverCount - 1,
            PrestigeTier.Gold => prestige.GoldCount - 1,
            PrestigeTier.Platin => prestige.PlatinCount - 1,
            PrestigeTier.Diamant => prestige.DiamantCount - 1,
            PrestigeTier.Meister => prestige.MeisterCount - 1,
            PrestigeTier.Legende => prestige.LegendeCount - 1,
            _ => 0
        };
        tierCount = Math.Max(0, tierCount);
        decimal diminishedBonus = baseBonus * (1m / (1m + 0.1m * tierCount));
        prestige.PermanentMultiplier += diminishedBonus;
        prestige.PermanentMultiplier = Math.Min(Math.Round(prestige.PermanentMultiplier, 3), MaxPermanentMultiplier);

        // History-Eintrag NACH Multiplikator-Berechnung (damit MultiplierAfter den echten Wert hat)
        // Level/Geld sind noch nicht resettet - PlayerLevel und TotalMoneyEarned stimmen noch
        prestige.History.Insert(0, new PrestigeHistoryEntry
        {
            Tier = tier,
            Date = DateTime.UtcNow,
            PointsEarned = tierPoints,
            PlayerLevel = state.PlayerLevel,
            MultiplierAfter = prestige.PermanentMultiplier,
            TotalMoneyEarned = state.TotalMoneyEarned,
            RunDurationTicks = runDuration.Ticks,
            Challenges = new List<PrestigeChallengeType>(prestige.ActiveChallenges),
            BonusPrestigePoints = bonusPp,
        });

        // Auf 20 Eintraege begrenzen
        if (prestige.History.Count > 20)
            prestige.History.RemoveRange(20, prestige.History.Count - 20);

        // Legacy-Felder synchron halten
        state.PrestigeLevel = prestige.TotalPrestigeCount;
        state.PrestigeMultiplier = prestige.PermanentMultiplier;

        // Reset durchfuehren
        ResetProgress(state, tier);

        // BAL-12: Speedrun-Phase nach Bronze-Prestige - 30min 3x Speed-Boost
        // Damit sich der erste Reset nicht wie Bestrafung anfühlt
        if (tier == PrestigeTier.Bronze)
        {
            state.SpeedBoostEndTime = DateTime.UtcNow.AddMinutes(30);
        }

        // KEIN ConfigureAwait(false): PrestigeCompleted-Event wird von MainViewModel
        // subscribed und feuert UI-Events (Celebration, Ceremony, FloatingText).
        // Muss auf dem Aufrufer-Thread (UI-Thread) bleiben.
        await _saveGameService.SaveAsync();

        PrestigeCompleted?.Invoke(this, EventArgs.Empty);

        // Meilensteine NACH dem Event prüfen (damit UI den Prestige-Erfolg zuerst zeigt)
        CheckAndAwardMilestones();

        return true;
    }

    /// <summary>
    /// Prestige-Pass aktivieren (nach erfolgreichem IAP-Kauf).
    /// Setzt IsPrestigePassActive auf den aktuellen Durchlauf.
    /// </summary>
    public void ActivatePrestigePass()
    {
        _gameStateService.State.IsPrestigePassActive = true;
    }

    public IReadOnlyList<PrestigeShopItem> GetShopItems()
    {
        var allItems = PrestigeShop.GetAllItems();
        var prestige = _gameStateService.State.Prestige;
        var purchased = prestige.PurchasedShopItems;
        var currentTier = prestige.CurrentTier;

        // Kopien erstellen damit statische Instanzen nicht mutiert werden
        var result = new List<PrestigeShopItem>(allItems.Count);
        foreach (var item in allItems)
        {
            // Tier-locked Items: Nur sichtbar wenn Tier erreicht ODER bereits gekauft
            if (item.RequiredTier != PrestigeTier.None
                && currentTier < item.RequiredTier
                && !purchased.Contains(item.Id))
                continue;

            var copy = new PrestigeShopItem
            {
                Id = item.Id,
                NameKey = item.NameKey,
                DescriptionKey = item.DescriptionKey,
                Icon = item.Icon,
                Cost = item.Cost,
                Effect = item.Effect,
                Category = item.Category,
                IsRepeatable = item.IsRepeatable,
                RequiredTier = item.RequiredTier,
            };

            if (copy.IsRepeatable)
            {
                prestige.RepeatableItemCounts.TryGetValue(copy.Id, out var count);
                copy.PurchaseCount = count;
                copy.IsPurchased = false;
            }
            else
            {
                copy.IsPurchased = purchased.Contains(copy.Id);
            }

            result.Add(copy);
        }

        return result;
    }

    /// <summary>
    /// Berechnet die aktuellen Kosten für ein wiederholbares Item.
    /// Formel: Basiskosten * 2^(Kaufanzahl) → 10/20/40/80/160/320...
    /// </summary>
    public static int GetRepeatableItemCost(PrestigeShopItem item, int purchaseCount)
    {
        return item.Cost * (1 << Math.Min(purchaseCount, 15)); // Cap bei 2^15 um Overflow zu vermeiden
    }

    public bool BuyShopItem(string itemId)
    {
        var prestige = _gameStateService.State.Prestige;
        var allItems = PrestigeShop.GetAllItems();

        var item = allItems.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return false;

        if (item.IsRepeatable)
        {
            // Wiederholbar: Steigende Kosten
            prestige.RepeatableItemCounts.TryGetValue(itemId, out var count);
            int cost = GetRepeatableItemCost(item, count);

            if (prestige.PrestigePoints < cost) return false;

            prestige.PrestigePoints -= cost;
            prestige.RepeatableItemCounts[itemId] = count + 1;
            // PurchaseCount nicht auf statischem Item setzen (UI-Kopien werden in GetShopItems erstellt)
        }
        else
        {
            // Einmalig: Standard-Logik
            if (prestige.PurchasedShopItems.Contains(itemId)) return false;
            if (prestige.PrestigePoints < item.Cost) return false;

            prestige.PrestigePoints -= item.Cost;
            prestige.PurchasedShopItems.Add(itemId);
        }

        // Caches invalidieren (Shop-Effekte und MaxOfflineHours)
        _effectCacheDirty = true;
        _gameStateService.State.InvalidateMaxOfflineHoursCache();
        return true;
    }

    /// <summary>
    /// Maximaler Prestige-Multiplikator (nur Tier-Boni, nicht Shop-Income-Boni).
    /// Shop-Income-Boni werden separat im GameLoop/OfflineProgress angewendet.
    /// BAL-FIX: Von 50x auf 20x gesenkt - realistisch erreichbarer Bereich nach ~48 Prestiges.
    /// Diminishing Returns: 10. Prestige desselben Tiers bringt nur noch 50% Bonus.
    /// </summary>
    private const decimal MaxPermanentMultiplier = 20.0m;

    public decimal GetPermanentMultiplier()
    {
        // Nur Tier-Multiplikator zurückgeben (bereits gekappt beim Schreiben in DoPrestige).
        // Shop-Income-Boni (pp_income_10/25/50) werden separat in GameLoop + OfflineProgress angewendet.
        return _gameStateService.State.Prestige.PermanentMultiplier;
    }

    public decimal GetCostReduction()
    {
        RefreshEffectCacheIfNeeded();
        return _cachedCostReduction;
    }

    public decimal GetMoodDecayReduction()
    {
        RefreshEffectCacheIfNeeded();
        return _cachedMoodDecayReduction;
    }

    public decimal GetXpMultiplier()
    {
        RefreshEffectCacheIfNeeded();
        return _cachedXpMultiplier;
    }

    /// <summary>
    /// Berechnet alle Prestige-Shop-Effekte in einem einzigen Durchlauf und cacht sie.
    /// Wird bei Shop-Kauf und State-Load invalidiert.
    /// </summary>
    private void RefreshEffectCacheIfNeeded()
    {
        if (!_effectCacheDirty) return;
        _effectCacheDirty = false;

        decimal costReduction = 0m;
        decimal moodDecayReduction = 0m;
        decimal xpMultiplier = 0m;
        decimal orderRewardBonus = 0m;
        decimal researchSpeedBonus = 0m;

        var prestige = _gameStateService.State.Prestige;
        var purchased = prestige.PurchasedShopItems;
        var allItems = PrestigeShop.GetAllItems();

        for (int i = 0; i < allItems.Count; i++)
        {
            var item = allItems[i];

            if (item.IsRepeatable)
            {
                // Wiederholbare Items: Effekt × Kaufanzahl
                if (prestige.RepeatableItemCounts.TryGetValue(item.Id, out var count) && count > 0)
                {
                    if (item.Effect.OrderRewardBonus > 0)
                        orderRewardBonus += item.Effect.OrderRewardBonus * count;
                    if (item.Effect.DeliverySpeedBonus > 0)
                    {
                        // DeliverySpeedBonus wird im GameLoopService separat gecacht
                        // → hier nicht nochmal cachen, nur über purchased-Loop
                    }
                }
                continue;
            }

            if (!purchased.Contains(item.Id)) continue;

            if (item.Effect.CostReduction > 0)
                costReduction += item.Effect.CostReduction;
            if (item.Effect.MoodDecayReduction > 0)
                moodDecayReduction += item.Effect.MoodDecayReduction;
            if (item.Effect.XpMultiplier > 0)
                xpMultiplier += item.Effect.XpMultiplier;
            if (item.Effect.OrderRewardBonus > 0)
                orderRewardBonus += item.Effect.OrderRewardBonus;
            if (item.Effect.ResearchSpeedBonus > 0)
                researchSpeedBonus += item.Effect.ResearchSpeedBonus;
        }

        _cachedCostReduction = Math.Min(costReduction, 0.50m);
        _cachedMoodDecayReduction = Math.Min(moodDecayReduction, 0.50m);
        _cachedXpMultiplier = xpMultiplier;
        _cachedOrderRewardBonus = Math.Min(orderRewardBonus, 1.0m); // Cap bei +100%
        _cachedResearchSpeedBonus = Math.Min(researchSpeedBonus, 0.50m); // Cap bei -50%
    }

    /// <summary>
    /// Setzt den Spielfortschritt basierend auf dem Prestige-Tier zurück.
    /// Verschärfte Erhaltung (eine Stufe höher): Gold=Research, Platin=ShopItems,
    /// Diamant=MasterTools, Meister=Buildings+Equipment, Legende=Manager+BestWorkers.
    /// Ascension-Perks: StartCapital, QuickStart, EternalTools, LegendaryReputation.
    /// </summary>
    private void ResetProgress(GameState state, PrestigeTier tier)
    {
        // Startgeld berechnen: Tier-Basis (skalierend) + Shop-Boni
        decimal startMoney = tier.GetTierStartMoney();
        var purchased = state.Prestige.PurchasedShopItems;
        var allItems = PrestigeShop.GetAllItems();
        foreach (var item in allItems)
        {
            if (purchased.Contains(item.Id) && item.Effect.ExtraStartMoney > 0)
            {
                startMoney += item.Effect.ExtraStartMoney;
            }
        }

        // Ascension-Perk: Start-Kapital-Multiplikator (1.0 = kein Bonus, 1.5 = +50%, etc.)
        decimal ascCapitalMultiplier = _ascensionService.GetStartCapitalMultiplier();
        if (ascCapitalMultiplier > 1.0m)
            startMoney = Math.Round(startMoney * ascCapitalMultiplier, 0);

        // Start-Worker-Tier aus Shop bestimmen
        var startWorkerTier = WorkerTier.E;
        foreach (var item in allItems)
        {
            if (purchased.Contains(item.Id) && item.Effect.StartingWorkerTier != null)
            {
                if (Enum.TryParse<WorkerTier>(item.Effect.StartingWorkerTier, out var shopTier) && shopTier > startWorkerTier)
                {
                    startWorkerTier = shopTier;
                }
            }
        }

        // === LEGENDE: Beste Worker VOR dem Reset sichern ===
        if (tier.KeepsBestWorkers())
        {
            state.Prestige.KeptWorkers.Clear();
            foreach (var ws in state.Workshops)
            {
                if (ws.Workers.Count > 0)
                {
                    var bestWorker = ws.Workers.OrderByDescending(w => w.Efficiency).First();
                    // Worker zurücksetzen für neuen Durchlauf (Mood/Fatigue/Training)
                    bestWorker.Mood = 80m;
                    bestWorker.Fatigue = 0m;
                    bestWorker.IsResting = false;
                    bestWorker.IsTraining = false;
                    bestWorker.RestStartedAt = null;
                    bestWorker.TrainingStartedAt = null;
                    state.Prestige.KeptWorkers[ws.Type.ToString()] = bestWorker;
                }
            }
        }

        // === RESET: Player Progress ===
        state.PlayerLevel = 1;
        state.CurrentXp = 0;
        state.TotalXp = 0;

        // === RESET: Money (TotalMoneyEarned bleibt, CurrentRunMoney wird zurückgesetzt!) ===
        state.Money = startMoney;
        state.CurrentRunMoney = 0;
        state.TotalMoneySpent = 0m;

        // === RESET: Workshops -> nur Carpenter Level 1 mit 1 Worker ===
        state.Workshops.Clear();
        state.UnlockedWorkshopTypes.Clear();
        state.UnlockedWorkshopTypes.Add(WorkshopType.Carpenter);

        var carpenter = Workshop.Create(WorkshopType.Carpenter);
        carpenter.IsUnlocked = true;

        // Legende: Gesicherten Carpenter-Worker wiederverwenden (wenn besser als Start-Tier)
        if (state.Prestige.KeptWorkers.TryGetValue(WorkshopType.Carpenter.ToString(), out var keptCarpenterWorker)
            && keptCarpenterWorker.Tier >= startWorkerTier)
        {
            carpenter.Workers.Add(keptCarpenterWorker);
            state.Prestige.KeptWorkers.Remove(WorkshopType.Carpenter.ToString());
        }
        else
        {
            carpenter.Workers.Add(Worker.CreateForTier(startWorkerTier));
        }

        state.Workshops.Add(carpenter);

        // === Ascension-Perk: Quick-Start - Workshops sofort freischalten ===
        int quickStartCount = _ascensionService.GetQuickStartWorkshops();
        if (quickStartCount > 0)
        {
            // Workshop-Reihenfolge (ohne Carpenter, der ist immer frei)
            WorkshopType[] unlockOrder =
            [
                WorkshopType.Plumber, WorkshopType.Electrician, WorkshopType.Painter,
                WorkshopType.Roofer, WorkshopType.Contractor, WorkshopType.Architect,
                WorkshopType.GeneralContractor, WorkshopType.MasterSmith
            ];

            int toUnlock = Math.Min(quickStartCount, unlockOrder.Length);
            for (int i = 0; i < toUnlock; i++)
            {
                var wsType = unlockOrder[i];
                if (state.UnlockedWorkshopTypes.Contains(wsType)) continue;

                state.UnlockedWorkshopTypes.Add(wsType);
                var ws = Workshop.Create(wsType);
                ws.IsUnlocked = true;

                // Legende-Worker wiederverwenden (wenn besser als Start-Tier)
                if (state.Prestige.KeptWorkers.TryGetValue(wsType.ToString(), out var keptWorker)
                    && keptWorker.Tier >= startWorkerTier)
                {
                    ws.Workers.Add(keptWorker);
                    state.Prestige.KeptWorkers.Remove(wsType.ToString());
                }
                else
                {
                    ws.Workers.Add(Worker.CreateForTier(startWorkerTier));
                }

                state.Workshops.Add(ws);
            }
        }

        // === RESET: Workers ===
        state.WorkerMarket = null;
        state.TotalWorkersHired = 0;
        state.TotalWorkersFired = 0;

        // === RESET: Orders ===
        state.AvailableOrders.Clear();
        state.ActiveOrder = null;
        state.TotalOrdersCompleted = 0;
        state.OrdersCompletedToday = 0;
        state.OrdersCompletedThisWeek = 0;
        state.LastOrderCooldownStart = DateTime.MinValue;
        state.WeeklyOrderReset = DateTime.UtcNow;

        // === RESET: Reputation (Ascension-Perk: höhere Start-Reputation) ===
        int startReputation = _ascensionService.GetStartReputation();
        state.Reputation = new CustomerReputation { ReputationScore = startReputation };
        state.LastReputationDecay = DateTime.UtcNow;

        // === RESET: Buildings (Meister+ behält Gebäude, Level wird weiter unten auf 1 gesetzt) ===
        if (!tier.KeepsBuildings())
            state.Buildings.Clear();

        // === RESET: Research (Gold+ behält Research) ===
        if (!tier.KeepsResearch())
        {
            state.Researches = ResearchTree.CreateAll();
            state.ActiveResearchId = null;
        }

        // === RESET: Events ===
        state.ActiveEvent = null;
        state.LastEventCheck = DateTime.UtcNow;
        state.EventHistory.Clear();

        // === RESET: Statistics (TotalPlayTimeSeconds bleibt!) ===
        state.TotalMiniGamesPlayed = 0;
        state.PerfectRatings = 0;
        state.PerfectStreak = 0;
        state.BestPerfectStreak = 0;

        // === RESET: Boosts ===
        state.SpeedBoostEndTime = DateTime.MinValue;
        state.XpBoostEndTime = DateTime.MinValue;
        state.RushBoostEndTime = DateTime.MinValue;
        state.LastFreeRushUsed = DateTime.MinValue;

        // === RESET: Daily Rewards ===
        state.DailyRewardStreak = 0;
        state.LastDailyRewardClaim = DateTime.MinValue;

        // === RESET: Lieferant ===
        state.PendingDelivery = null;
        state.NextDeliveryTime = DateTime.MinValue;
        state.TotalDeliveriesClaimed = 0;

        // === RESET: Quick Jobs ===
        state.QuickJobs.Clear();
        state.LastQuickJobRotation = DateTime.MinValue;
        state.TotalQuickJobsCompleted = 0;
        state.QuickJobsCompletedToday = 0;
        state.LastQuickJobDailyReset = DateTime.MinValue;

        // === RESET: Daily Challenges ===
        state.DailyChallengeState = new DailyChallengeState();

        // === RESET: Story (pending Story leeren, viewed bleiben erhalten) ===
        state.PendingStoryId = null;

        // === RESET: Meisterwerkzeuge (Diamant+ behält sie, Ascension-Perk: teilweise behalten) ===
        if (!tier.KeepsMasterTools())
        {
            int eternalLevel = _ascensionService.GetEternalToolsLevel();
            if (eternalLevel >= 3)
            {
                // Level 3 (Max): Alle Meisterwerkzeuge behalten
            }
            else if (eternalLevel > 0)
            {
                // Level 1: Erste 2 Tools, Level 2: Erste 4 Tools
                int keepCount = Math.Min(eternalLevel * 2, state.CollectedMasterTools.Count);
                var toolsToKeep = state.CollectedMasterTools.GetRange(0, keepCount);
                state.CollectedMasterTools.Clear();
                state.CollectedMasterTools.AddRange(toolsToKeep);
            }
            else
            {
                // Kein Perk: Alle löschen (bisheriges Verhalten)
                state.CollectedMasterTools.Clear();
            }
        }

        // === RESET: Lucky Spin (immer zurücksetzen) ===
        state.LuckySpin = new LuckySpinState();

        // === RESET: Weekly Missions (immer zurücksetzen) ===
        state.WeeklyMissionState = new WeeklyMissionState();

        // === RESET: Welcome Back Offer (immer zurücksetzen) ===
        state.ActiveWelcomeBackOffer = null;

        // === RESET: Tournament (immer zurücksetzen) ===
        state.CurrentTournament = null;

        // === RESET: Crafting (immer zurücksetzen, null-safe für alte Saves) ===
        state.CraftingInventory = new Dictionary<string, int>();
        state.ActiveCraftingJobs = [];

        // === RESET: Daily Shop Offer (immer zurücksetzen) ===
        state.DailyShopOffer = null;

        // === RESET: Workshop-Spezialisierung (immer zurücksetzen) ===
        foreach (var ws in state.Workshops)
        {
            ws.WorkshopSpecialization = null;
        }

        // === Gilden-Mitgliedschaft bleibt bestehen (Firebase kümmert sich um Weekly-Reset) ===

        // === BEDINGT: Equipment (Meister+ behält es) ===
        if (!tier.KeepsEquipment())
        {
            state.EquipmentInventory.Clear();
            foreach (var ws in state.Workshops)
            {
                foreach (var worker in ws.Workers)
                {
                    worker.EquippedItem = null;
                }
            }
        }

        // === BEDINGT: Manager (Legende behält sie, Level→1) ===
        if (!tier.KeepsManagers())
        {
            state.Managers.Clear();
        }
        else
        {
            // Manager behalten, aber Level auf 1 zurücksetzen
            foreach (var mgr in state.Managers)
            {
                mgr.Level = 1;
            }
        }

        // Legende-Worker werden bereits VOR dem Workshop-Reset gesichert (s.o.)
        // und beim Workshop-Unlock via GetOrCreateWorkshop() angewendet.

        // === RESET: Gebäude (Meister+ behält sie mit Level→1) ===
        if (tier.KeepsBuildings())
        {
            foreach (var b in state.Buildings)
            {
                b.Level = 1;
            }
        }
        // (Unterhalb von Meister werden Buildings bereits oben via .Clear() zurückgesetzt)

        // === PRESERVED (nicht angefasst): ===
        // - state.Prestige (PrestigeData mit Punkten, Shop-Items, Tier-Counts)
        // - state.UnlockedAchievements
        // - state.IsPremium
        // - state.SeenHints (kontextuelles Tutorial)
        // - state.TotalMoneyEarned
        // - state.TotalPlayTimeSeconds
        // - state.SoundEnabled, state.MusicEnabled, state.HapticsEnabled, state.Language
        // - state.CreatedAt
        // - state.BattlePass (zeitbasiert + bezahlt)
        // - state.CurrentSeasonalEvent (zeitbasiert)
        // - state.ClaimedLevelOffers
        // - state.HasPurchasedStarterPack
        // - state.VipLevel, state.TotalPurchaseAmount
        // - state.Friends
        // - state.TotalTournamentsPlayed
        // - state.StreakRescueUsed

        // === Prestige-Pass bleibt permanent aktiv (einmaliger IAP-Kauf, nicht pro Prestige) ===
        // Nicht mehr zurückgesetzt - der Spieler zahlt einmal 2,99 EUR und hat dauerhaft +50% PP.

        // Gold prestige preserves shop items (already in PrestigeData.PurchasedShopItems,
        // which is not touched by reset). Nothing extra needed here.

        // === Speedrun: Neuen Run starten ===
        state.Prestige.RunStartTime = DateTime.UtcNow;

        // === Challenges: Aktive Challenges bleiben für den neuen Run erhalten ===
        // (Spieler hat sie VOR dem Prestige gewählt, Constraints werden ab jetzt enforced)
    }
}
