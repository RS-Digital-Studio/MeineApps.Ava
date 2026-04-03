using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Implementiert das Ascension-System (Meta-Prestige).
/// Freigeschaltet nach 3x Legende-Prestige.
/// Resettet Prestige-Daten komplett, gibt Ascension-Punkte für permanente Perks.
/// </summary>
public sealed class AscensionService : IAscensionService
{
    private readonly IGameStateService _gameStateService;
    private readonly ISaveGameService _saveGameService;
    private readonly IAudioService _audioService;

    /// <summary>Gecachte Perk-Definitionen (statisch, ändert sich nicht zur Laufzeit).</summary>
    private static readonly List<AscensionPerk> AllPerks = AscensionPerk.GetAll();

    public event EventHandler? AscensionCompleted;

    public AscensionService(
        IGameStateService gameStateService,
        ISaveGameService saveGameService,
        IAudioService audioService)
    {
        _gameStateService = gameStateService;
        _saveGameService = saveGameService;
        _audioService = audioService;

        // Bei State-Wechsel (Load/Import/Reset/Prestige) ggf. Caches invalidieren
        _gameStateService.StateLoaded += (_, _) => { /* Keine lokalen Caches - State-Zugriff immer direkt */ };
    }

    // ===================================================================
    // VORAUSSETZUNGEN
    // ===================================================================

    public bool CanAscend =>
        _gameStateService.State.Prestige.LegendeCount >= 3;

    // ===================================================================
    // AP-BERECHNUNG
    // ===================================================================

    public int CalculateAscensionPoints()
    {
        var state = _gameStateService.State;
        var prestige = state.Prestige;
        int legendeCount = prestige.LegendeCount;

        // Basis: 1 AP pro 500 PP (ausgegebene + verfügbare)
        int apFromPP = prestige.TotalPrestigePoints / 500;

        // Bonus: 1 AP pro 2 Legende-Prestiges
        int apFromLegende = legendeCount / 2;

        // Bonus: +2 wenn alle 8 Basis-Workshops auf MaxLevel (1000)
        int maxLevelCount = 0;
        for (int i = 0; i < state.Workshops.Count; i++)
        {
            if (state.Workshops[i].Level >= Workshop.MaxLevel)
                maxLevelCount++;
        }
        int apFromMaxLevel = maxLevelCount >= 8 ? 2 : 0;

        // Bonus: +1 wenn alle 12 Meisterwerkzeuge gesammelt
        int apFromTools = state.CollectedMasterTools.Count >= 12 ? 1 : 0;

        // Premium-Bonus: +1 AP
        int premiumBonus = state.IsPremium ? 1 : 0;

        // Skalierungs-Bonus: +2 AP pro bereits durchgeführte Ascension (belohnt Langzeitspieler)
        int apFromScaling = state.Ascension.AscensionLevel * 2;

        // Minimum 5 AP damit sich Ascension immer lohnt
        return Math.Max(5, apFromPP + apFromLegende + apFromMaxLevel + apFromTools + premiumBonus + apFromScaling);
    }

    // ===================================================================
    // ASCENSION DURCHFÜHREN
    // ===================================================================

    public async Task<bool> DoAscension()
    {
        if (!CanAscend) return false;

        var state = _gameStateService.State;
        int calculatedAP = CalculateAscensionPoints();

        // --- Ascension-Daten aktualisieren ---
        state.Ascension.AscensionLevel++;
        state.Ascension.AscensionPoints += calculatedAP;
        state.Ascension.TotalAscensionPoints += calculatedAP;
        state.Ascension.LastAscensionDate = DateTime.UtcNow;

        // --- EternalTools-Perk: Meisterwerkzeuge teilweise bewahren ---
        int eternalToolsLevel = GetEternalToolsLevel();
        var keptTools = new List<string>();
        if (eternalToolsLevel >= 3)
        {
            // Level 3 (Max): Alle Tools behalten
            keptTools.AddRange(state.CollectedMasterTools);
        }
        else if (eternalToolsLevel >= 1)
        {
            // Level 1: Erste 2 Tools, Level 2: Erste 4 Tools
            int keepCount = Math.Min(eternalToolsLevel * 2, state.CollectedMasterTools.Count);
            for (int i = 0; i < keepCount; i++)
                keptTools.Add(state.CollectedMasterTools[i]);
        }
        // Level 0: Keine Tools behalten

        // --- Kompletter Prestige-Reset (härtester Reset im Spiel) ---
        // PrestigeService.ResetProgress() als Referenz, aber wir resetten ALLES inkl. Prestige-Daten

        // Player Progress
        state.PlayerLevel = 1;
        state.CurrentXp = 0;
        state.TotalXp = 0;

        // Geld (mit Start-Kapital-Perk-Multiplikator)
        state.Money = 1000m * GetStartCapitalMultiplier();
        state.CurrentRunMoney = 0;
        state.TotalMoneySpent = 0m;

        // Workshops: Nur Carpenter Level 1 mit 1 E-Worker
        state.Workshops.Clear();
        state.UnlockedWorkshopTypes.Clear();
        state.UnlockedWorkshopTypes.Add(WorkshopType.Carpenter);
        var carpenter = Workshop.Create(WorkshopType.Carpenter);
        carpenter.IsUnlocked = true;
        carpenter.Workers.Add(Worker.CreateForTier(WorkerTier.E));
        state.Workshops.Add(carpenter);

        // Workers
        state.WorkerMarket = null;
        state.Statistics.TotalWorkersHired = 0;
        state.Statistics.TotalWorkersFired = 0;

        // Orders
        state.AvailableOrders.Clear();
        state.ActiveOrder = null;
        state.Statistics.TotalOrdersCompleted = 0;
        state.Statistics.OrdersCompletedToday = 0;
        state.Statistics.OrdersCompletedThisWeek = 0;
        state.LastOrderCooldownStart = DateTime.MinValue;
        state.WeeklyOrderReset = DateTime.UtcNow;
        state.Statistics.TotalMaterialOrdersCompleted = 0;
        state.Statistics.MaterialOrdersCompletedToday = 0;

        // Reputation
        state.Reputation = new CustomerReputation();
        state.LastReputationDecay = DateTime.UtcNow;

        // Research
        state.Researches = ResearchTree.CreateAll();
        state.ActiveResearchId = null;

        // Events
        state.ActiveEvent = null;
        state.LastEventCheck = DateTime.UtcNow;
        state.EventHistory.Clear();

        // Statistics (TotalPlayTimeSeconds + BestPerfectStreak bleiben!)
        state.Statistics.TotalMiniGamesPlayed = 0;
        state.Statistics.PerfectRatings = 0;
        state.Statistics.PerfectStreak = 0;
        // BestPerfectStreak bewahren (All-Time-Rekord)
        // PerfectRatingCounts resetten (Auto-Complete-Mastery muss neu erarbeitet werden)
        state.PerfectRatingCounts?.Clear();

        // Boosts
        state.SpeedBoostEndTime = DateTime.MinValue;
        state.XpBoostEndTime = DateTime.MinValue;
        state.RushBoostEndTime = DateTime.MinValue;
        state.LastFreeRushUsed = DateTime.MinValue;

        // Daily Rewards
        state.DailyRewardStreak = 0;
        state.LastDailyRewardClaim = DateTime.MinValue;

        // Lieferant
        state.PendingDelivery = null;
        state.NextDeliveryTime = DateTime.MinValue;
        state.Statistics.TotalDeliveriesClaimed = 0;

        // Quick Jobs
        state.QuickJobs.Clear();
        state.LastQuickJobRotation = DateTime.MinValue;
        state.TotalQuickJobsCompleted = 0;
        state.QuickJobsCompletedToday = 0;
        state.LastQuickJobDailyReset = DateTime.MinValue;

        // Daily Challenges
        state.DailyChallengeState = new DailyChallengeState();

        // Story (pending leeren, viewed bleiben erhalten)
        state.PendingStoryId = null;

        // Lucky Spin
        state.LuckySpin = new LuckySpinState();

        // Weekly Missions
        state.WeeklyMissionState = new WeeklyMissionState();

        // Welcome Back
        state.ActiveWelcomeBackOffer = null;

        // Tournament
        state.CurrentTournament = null;

        // Crafting
        state.CraftingInventory = new Dictionary<string, int>();
        state.ActiveCraftingJobs = [];

        // Daily Shop Offer
        state.DailyShopOffer = null;

        // Workshop-Spezialisierung
        foreach (var ws in state.Workshops)
            ws.WorkshopSpecialization = null;

        // --- ASCENSION-SPEZIFISCHE RESETS (über Prestige hinaus) ---

        // Prestige-Daten zuruecksetzen, aber permanente Felder bewahren:
        // - ClaimedMilestones (GS-Belohnungen, permanent)
        // - BestRunTimes (Speedrun-Bestzeiten, motivational)
        // - PurchasedShopItems + RepeatableItemCounts (PP-Investitionen, permanent)
        var preservedMilestones = state.Prestige.ClaimedMilestones;
        var preservedBestRunTimes = state.Prestige.BestRunTimes;
        var preservedShopItems = state.Prestige.PurchasedShopItems;
        var preservedRepeatableCounts = state.Prestige.RepeatableItemCounts;
        state.Prestige = new PrestigeData
        {
            ClaimedMilestones = preservedMilestones,
            BestRunTimes = preservedBestRunTimes,
            PurchasedShopItems = preservedShopItems,
            RepeatableItemCounts = preservedRepeatableCounts,
        };

        // Legacy-Felder synchron halten
        state.PrestigeLevel = 0;
        state.PrestigeMultiplier = 1.0m;

        // Manager komplett weg
        state.Managers.Clear();

        // Equipment komplett weg
        state.EquipmentInventory.Clear();

        // Gebäude komplett weg
        state.Buildings.Clear();

        // Meisterwerkzeuge: Abhängig vom EternalTools-Perk
        state.CollectedMasterTools.Clear();
        if (keptTools.Count > 0)
            state.CollectedMasterTools.AddRange(keptTools);

        // --- BEWAHRT (nicht angefasst) ---
        // - state.Ascension (Ascension-Daten inkl. Perks)
        // - state.WorkshopStars (Rebirth-Sterne, permanent)
        // - state.UnlockedAchievements
        // - state.IsPremium
        // - state.Tutorial.SeenHints (Tutorial)
        // - state.TotalMoneyEarned
        // - state.Statistics.TotalPlayTimeSeconds
        // - state.Settings.SoundEnabled, state.Settings.MusicEnabled, state.Settings.HapticsEnabled, state.Language
        // - state.CreatedAt
        // - state.BattlePass
        // - state.CurrentSeasonalEvent
        // - state.ClaimedLevelOffers
        // - state.HasPurchasedStarterPack
        // - state.VipLevel, state.TotalPurchaseAmount
        // - state.Friends
        // - state.GuildMembership
        // - state.PlayerGuid, state.PlayerName
        // - state.UnlockedCosmetics, state.ActiveCityThemeId, state.ActiveWorkshopSkins

        // Caches invalidieren
        state.InvalidateIncomeCache();
        state.InvalidateBuildingCache();
        state.InvalidateMaxOfflineHoursCache();

        _gameStateService.MarkDirty();

        // Speichern (kein ConfigureAwait - Event muss auf UI-Thread bleiben)
        await _saveGameService.SaveAsync();

        // Sound + Event
        _ = _audioService.PlaySoundAsync(GameSound.LevelUp);
        _audioService.Vibrate(VibrationType.Success);
        AscensionCompleted?.Invoke(this, EventArgs.Empty);

        return true;
    }

    // ===================================================================
    // PERK-UPGRADE
    // ===================================================================

    public bool UpgradePerk(string perkId)
    {
        var perk = FindPerk(perkId);
        if (perk == null) return false;

        var ascension = _gameStateService.State.Ascension;
        int currentLevel = ascension.GetPerkLevel(perkId);

        // Bereits auf Max-Level
        if (currentLevel >= perk.MaxLevel) return false;

        // Kosten prüfen (nächstes Level)
        int cost = perk.GetCost(currentLevel + 1);
        if (ascension.AscensionPoints < cost) return false;

        // Upgrade durchführen
        ascension.AscensionPoints -= cost;
        ascension.Perks[perkId] = currentLevel + 1;

        _gameStateService.MarkDirty();
        return true;
    }

    // ===================================================================
    // PERK-ABFRAGEN
    // ===================================================================

    public IReadOnlyList<AscensionPerk> GetAllPerks() => AllPerks;

    public decimal GetPerkValue(string perkId)
    {
        var perk = FindPerk(perkId);
        if (perk == null) return 0m;

        int level = _gameStateService.State.Ascension.GetPerkLevel(perkId);
        return level == 0 ? 0m : perk.GetValue(level);
    }

    public decimal GetGoldenScrewBonus()
    {
        // asc_golden_era: +20%/+50%/+100% Goldschrauben-Verdienst
        return GetPerkValue("asc_golden_era");
    }

    public decimal GetResearchSpeedBonus()
    {
        // asc_timeless_research: -15%/-30%/-50% Research-Dauer
        return GetPerkValue("asc_timeless_research");
    }

    public int GetStartReputation()
    {
        // asc_legendary_reputation: Start-Reputation 65/80/100 (Default 50)
        int level = _gameStateService.State.Ascension.GetPerkLevel("asc_legendary_reputation");
        if (level == 0) return 50; // Standard-Startwert
        var perk = FindPerk("asc_legendary_reputation");
        return perk != null ? (int)perk.GetValue(level) : 50;
    }

    public int GetQuickStartWorkshops()
    {
        // asc_quick_start: Start mit 2/4/8 Workshops
        int level = _gameStateService.State.Ascension.GetPerkLevel("asc_quick_start");
        if (level == 0) return 0;
        var perk = FindPerk("asc_quick_start");
        return perk != null ? (int)perk.GetValue(level) : 0;
    }

    public int GetEternalToolsLevel()
    {
        // asc_eternal_tools: Level 0-3 direkt
        return _gameStateService.State.Ascension.GetPerkLevel("asc_eternal_tools");
    }

    public decimal GetStartCapitalMultiplier()
    {
        // asc_start_capital: +100%/+500%/+1000% → Multiplikator 2.0/6.0/11.0
        decimal bonus = GetPerkValue("asc_start_capital");
        return bonus == 0m ? 1.0m : 1.0m + bonus;
    }

    // ===================================================================
    // HILFSMETHODEN
    // ===================================================================

    /// <summary>Findet eine Perk-Definition anhand der ID.</summary>
    private static AscensionPerk? FindPerk(string perkId)
    {
        for (int i = 0; i < AllPerks.Count; i++)
        {
            if (AllPerks[i].Id == perkId)
                return AllPerks[i];
        }
        return null;
    }
}
