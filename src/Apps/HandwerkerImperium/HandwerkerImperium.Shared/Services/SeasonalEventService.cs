using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet saisonale Events (4x pro Jahr, jeweils 1.-14. des Monats).
/// Frühling (März), Sommer (Juni), Herbst (September), Winter (Dezember).
/// Jedes Event hat eigene Währung (SP) und einen Shop mit exklusiven Items.
/// SP-Verdienst: 5 SP pro Auftrag (Basis), +3 bei Good, +5 bei Perfect.
/// </summary>
public sealed class SeasonalEventService : ISeasonalEventService, IDisposable
{
    private readonly IGameStateService _gameState;
    private readonly IWorkerService _workerService;
    private readonly ICraftingService _craftingService;
    private bool _disposed;

    // SP-Belohnungen pro Auftragsabschluss
    private const int BaseSpPerOrder = 5;
    private const int GoodBonusSp = 3;
    private const int PerfectBonusSp = 5;

    // SP-Belohnungen aus anderen Quellen
    private const int SpPerMiniGame = 2;
    private const int SpPerPerfectMiniGame = 4;
    private const int SpPerWorkerLevelUp = 1;
    private const int SpPerCraftingCollected = 3;

    public event Action? SeasonalEventChanged;

    public SeasonalEventService(IGameStateService gameState, IWorkerService workerService, ICraftingService craftingService)
    {
        _gameState = gameState;
        _workerService = workerService;
        _craftingService = craftingService;
        _gameState.OrderCompleted += OnOrderCompleted;
        _gameState.MiniGameResultRecorded += OnMiniGameResultRecorded;
        _workerService.WorkerLevelUp += OnWorkerLevelUp;
        _craftingService.CraftingProductCollected += OnCraftingProductCollected;
    }

    /// <summary>
    /// Vergibt SP bei Auftragsabschluss (nur wenn Event aktiv).
    /// </summary>
    private void OnOrderCompleted(object? sender, OrderCompletedEventArgs e)
    {
        if (!IsEventActive) return;

        int sp = BaseSpPerOrder;
        sp += e.AverageRating switch
        {
            MiniGameRating.Perfect => PerfectBonusSp,
            MiniGameRating.Good => GoodBonusSp,
            _ => 0
        };

        AddSeasonalCurrency(sp);
    }

    /// <summary>
    /// Vergibt SP bei MiniGame-Abschluss (nur wenn Event aktiv).
    /// </summary>
    private void OnMiniGameResultRecorded(object? sender, MiniGameResultRecordedEventArgs e)
    {
        if (!IsEventActive) return;
        int sp = e.Rating == MiniGameRating.Perfect ? SpPerPerfectMiniGame : SpPerMiniGame;
        AddSeasonalCurrency(sp);
    }

    /// <summary>
    /// Vergibt SP bei Worker-Level-Up (nur wenn Event aktiv).
    /// </summary>
    private void OnWorkerLevelUp(object? sender, Worker worker)
    {
        if (!IsEventActive) return;
        AddSeasonalCurrency(SpPerWorkerLevelUp);
    }

    /// <summary>
    /// Vergibt SP bei Crafting-Produkt-Einsammlung (nur wenn Event aktiv).
    /// </summary>
    private void OnCraftingProductCollected()
    {
        if (!IsEventActive) return;
        AddSeasonalCurrency(SpPerCraftingCollected);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _gameState.OrderCompleted -= OnOrderCompleted;
        _gameState.MiniGameResultRecorded -= OnMiniGameResultRecorded;
        _workerService.WorkerLevelUp -= OnWorkerLevelUp;
        _craftingService.CraftingProductCollected -= OnCraftingProductCollected;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public bool IsEventActive =>
        _gameState.State.CurrentSeasonalEvent != null &&
        _gameState.State.CurrentSeasonalEvent.IsActive;

    public void CheckSeasonalEvent()
    {
        var state = _gameState.State;
        var now = DateTime.UtcNow;
        var (isInWindow, season) = SeasonalEvent.CheckSeason(now);

        if (isInWindow)
        {
            // Zeitmanipulations-Schutz: Event-StartDate in Zukunft → blockieren
            if (state.CurrentSeasonalEvent != null && state.CurrentSeasonalEvent.StartDate > now)
            {
                state.CurrentSeasonalEvent = null;
                return;
            }

            // Im Saison-Zeitfenster und kein aktives Event → Event starten
            if (state.CurrentSeasonalEvent == null || !state.CurrentSeasonalEvent.IsActive)
            {
                var startDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var endDate = new DateTime(now.Year, now.Month, 14, 23, 59, 59, DateTimeKind.Utc);

                state.CurrentSeasonalEvent = new SeasonalEvent
                {
                    Season = season,
                    StartDate = startDate,
                    EndDate = endDate,
                    Currency = 0,
                    TotalPoints = 0,
                    CompletedOrders = 0
                };

                _gameState.MarkDirty();
                SeasonalEventChanged?.Invoke();
            }
        }
        else
        {
            // Außerhalb des Zeitfensters und Event noch aktiv → Event beenden
            if (state.CurrentSeasonalEvent != null)
            {
                state.CurrentSeasonalEvent = null;
                _gameState.MarkDirty();
                SeasonalEventChanged?.Invoke();
            }
        }
    }

    public void AddSeasonalCurrency(int amount)
    {
        var seasonalEvent = _gameState.State.CurrentSeasonalEvent;
        if (seasonalEvent == null || !seasonalEvent.IsActive) return;
        if (amount <= 0) return;

        seasonalEvent.Currency += amount;
        seasonalEvent.TotalPoints += amount;
        _gameState.MarkDirty();
        SeasonalEventChanged?.Invoke();
    }

    public bool BuySeasonalItem(string itemId)
    {
        var seasonalEvent = _gameState.State.CurrentSeasonalEvent;
        if (seasonalEvent == null || !seasonalEvent.IsActive) return false;

        // Bereits gekauft?
        if (seasonalEvent.PurchasedItems.Contains(itemId)) return false;

        // Shop-Item finden
        var shopItems = GetShopItems(seasonalEvent.Season);
        var item = shopItems.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return false;

        // Genug Währung?
        if (seasonalEvent.Currency < item.Cost) return false;

        // Kaufen
        seasonalEvent.Currency -= item.Cost;
        seasonalEvent.PurchasedItems.Add(itemId);

        // Effekt anwenden
        ApplySeasonalItemEffect(item.Effect);

        _gameState.MarkDirty();
        SeasonalEventChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Gibt die Shop-Items für eine bestimmte Saison zurück.
    /// 4 Basis-Items (alle Saisons gleich) + 2 saison-einzigartige Items.
    /// </summary>
    public static List<SeasonalShopItem> GetShopItems(Season season)
    {
        // Basis-Items die jede Saison hat (mit saisonaler Anpassung)
        string prefix = season.ToString().ToLowerInvariant();
        string icon = season switch
        {
            Season.Spring => "Flower",
            Season.Summer => "WhiteBalanceSunny",
            Season.Autumn => "Leaf",
            Season.Winter => "Snowflake",
            _ => "CalendarStar"
        };

        var items = new List<SeasonalShopItem>
        {
            new()
            {
                Id = $"{prefix}_income_boost",
                NameKey = $"Seasonal{season}IncomeBoost",
                DescriptionKey = $"Seasonal{season}IncomeBoostDesc",
                Cost = 50,
                Icon = icon,
                Effect = new SeasonalItemEffect { IncomeBonus = 0.10m }
            },
            new()
            {
                Id = $"{prefix}_xp_pack",
                NameKey = $"Seasonal{season}XpPack",
                DescriptionKey = $"Seasonal{season}XpPackDesc",
                Cost = 30,
                Icon = icon,
                Effect = new SeasonalItemEffect { XpBonus = 500 }
            },
            new()
            {
                Id = $"{prefix}_screw_bundle",
                NameKey = $"Seasonal{season}ScrewBundle",
                DescriptionKey = $"Seasonal{season}ScrewBundleDesc",
                Cost = 75,
                Icon = icon,
                Effect = new SeasonalItemEffect { GoldenScrews = 15 }
            },
            new()
            {
                Id = $"{prefix}_speed_boost",
                NameKey = $"Seasonal{season}SpeedBoost",
                DescriptionKey = $"Seasonal{season}SpeedBoostDesc",
                Cost = 100,
                Icon = icon,
                Effect = new SeasonalItemEffect { SpeedBoostMinutes = 120 }
            }
        };

        // 2 saison-einzigartige Items (Phase 2.9)
        // Effekte werden in GameLoopService/OfflineProgressService ausgewertet
        items.AddRange(GetUniqueSeasonItems(season, icon));

        return items;
    }

    /// <summary>
    /// Gibt die 2 saison-einzigartigen Items zurück.
    /// </summary>
    private static List<SeasonalShopItem> GetUniqueSeasonItems(Season season, string icon) => season switch
    {
        Season.Spring =>
        [
            // Frühling: +1 Max-Worker fuer 14 Tage
            new SeasonalShopItem
            {
                Id = "spring_extra_worker",
                NameKey = "SeasonalSpringExtraWorker",
                DescriptionKey = "SeasonalSpringExtraWorkerDesc",
                Cost = 150,
                Icon = icon,
                Effect = new SeasonalItemEffect { ExtraWorkerDays = 1, EffectDurationDays = 14 }
            },
            // Frühling: +30% Research-Speed fuer 14 Tage
            new SeasonalShopItem
            {
                Id = "spring_research_speed",
                NameKey = "SeasonalSpringResearchSpeed",
                DescriptionKey = "SeasonalSpringResearchSpeedDesc",
                Cost = 80,
                Icon = icon,
                Effect = new SeasonalItemEffect { ResearchSpeedBonusPercent = 30, EffectDurationDays = 14 }
            }
        ],

        Season.Summer =>
        [
            // Sommer: 2x PP beim naechsten Prestige
            new SeasonalShopItem
            {
                Id = "summer_double_prestige",
                NameKey = "SeasonalSummerDoublePrestige",
                DescriptionKey = "SeasonalSummerDoublePrestigeDesc",
                Cost = 200,
                Icon = icon,
                Effect = new SeasonalItemEffect { DoubleNextPrestige = true }
            },
            // Sommer: +50% Offline-Earnings fuer 14 Tage
            new SeasonalShopItem
            {
                Id = "summer_offline_boost",
                NameKey = "SeasonalSummerOfflineBoost",
                DescriptionKey = "SeasonalSummerOfflineBoostDesc",
                Cost = 120,
                Icon = icon,
                Effect = new SeasonalItemEffect { OfflineEarningsBonusPercent = 50, EffectDurationDays = 14 }
            }
        ],

        Season.Autumn =>
        [
            // Herbst: 500 Goldschrauben sofort
            new SeasonalShopItem
            {
                Id = "autumn_instant_screws",
                NameKey = "SeasonalAutumnInstantScrews",
                DescriptionKey = "SeasonalAutumnInstantScrewsDesc",
                Cost = 250,
                Icon = icon,
                Effect = new SeasonalItemEffect { InstantGoldenScrews = 500 }
            },
            // Herbst: Alle Worker Mood auf 100
            new SeasonalShopItem
            {
                Id = "autumn_mood_reset",
                NameKey = "SeasonalAutumnMoodReset",
                DescriptionKey = "SeasonalAutumnMoodResetDesc",
                Cost = 60,
                Icon = icon,
                Effect = new SeasonalItemEffect { WorkerMoodResetTo = 100 }
            }
        ],

        Season.Winter =>
        [
            // Winter: 4h Speed-Boost (2x Geschwindigkeit)
            new SeasonalShopItem
            {
                Id = "winter_speed_4h",
                NameKey = "SeasonalWinterSpeed4h",
                DescriptionKey = "SeasonalWinterSpeed4hDesc",
                Cost = 100,
                Icon = icon,
                Effect = new SeasonalItemEffect { SpeedBoostHours = 4 }
            },
            // Winter: +100% Daily-Reward am naechsten Tag
            new SeasonalShopItem
            {
                Id = "winter_double_daily",
                NameKey = "SeasonalWinterDoubleDaily",
                DescriptionKey = "SeasonalWinterDoubleDailyDesc",
                Cost = 150,
                Icon = icon,
                Effect = new SeasonalItemEffect { DoubleDailyReward = true }
            }
        ],

        _ => []
    };

    /// <summary>
    /// Wendet den Effekt eines saisonalen Shop-Items an.
    /// Sofort-Effekte werden hier direkt ausgefuehrt.
    /// Temporaere/passive Effekte werden in GameLoopService/OfflineProgressService ausgewertet.
    /// </summary>
    private void ApplySeasonalItemEffect(SeasonalItemEffect effect)
    {
        if (effect.GoldenScrews > 0)
            _gameState.AddGoldenScrews(effect.GoldenScrews);

        if (effect.XpBonus > 0)
            _gameState.AddXp(effect.XpBonus);

        if (effect.SpeedBoostMinutes > 0)
        {
            var state = _gameState.State;
            var newEnd = DateTime.UtcNow.AddMinutes(effect.SpeedBoostMinutes);
            if (newEnd > state.SpeedBoostEndTime)
                state.SpeedBoostEndTime = newEnd;
        }

        // IncomeBonus wird passiv vom GameLoop berücksichtigt wenn das Item gekauft ist

        // --- Saison-einzigartige Effekte (Phase 2.9) ---

        // Sofort: Goldschrauben-Belohnung (z.B. Herbst: 500 GS)
        if (effect.InstantGoldenScrews > 0)
            _gameState.AddGoldenScrews(effect.InstantGoldenScrews);

        // Sofort: Speed-Boost in Stunden (z.B. Winter: 4h 2x-Speed)
        if (effect.SpeedBoostHours > 0)
        {
            var state = _gameState.State;
            var newEnd = DateTime.UtcNow.AddHours(effect.SpeedBoostHours);
            if (newEnd > state.SpeedBoostEndTime)
                state.SpeedBoostEndTime = newEnd;
        }

        // Sofort: Alle Worker Mood auf Zielwert setzen (z.B. Herbst: 100)
        if (effect.WorkerMoodResetTo > 0)
        {
            var state = _gameState.State;
            foreach (var workshop in state.Workshops)
            {
                foreach (var worker in workshop.Workers)
                {
                    worker.Mood = effect.WorkerMoodResetTo;
                }
            }
        }

        // Temporaere/passive Effekte (werden von anderen Services geprueft):
        // - ExtraWorkerDays + EffectDurationDays → GameLoopService prueft PurchasedItems
        // - ResearchSpeedBonusPercent + EffectDurationDays → ResearchService prueft PurchasedItems
        // - OfflineEarningsBonusPercent + EffectDurationDays → OfflineProgressService prueft PurchasedItems
        // - DoubleNextPrestige → PrestigeService prueft PurchasedItems
        // - DoubleDailyReward → DailyRewardService prueft PurchasedItems
    }
}
