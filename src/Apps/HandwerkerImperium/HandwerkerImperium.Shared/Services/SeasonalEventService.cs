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

    // SP-Belohnungen pro Auftragsabschluss
    private const int BaseSpPerOrder = 5;
    private const int GoodBonusSp = 3;
    private const int PerfectBonusSp = 5;

    public event Action? SeasonalEventChanged;

    public SeasonalEventService(IGameStateService gameState)
    {
        _gameState = gameState;
        _gameState.OrderCompleted += OnOrderCompleted;
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

    public void Dispose()
    {
        _gameState.OrderCompleted -= OnOrderCompleted;
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

        return
        [
            new SeasonalShopItem
            {
                Id = $"{prefix}_income_boost",
                NameKey = $"Seasonal{season}IncomeBoost",
                DescriptionKey = $"Seasonal{season}IncomeBoostDesc",
                Cost = 50,
                Icon = icon,
                Effect = new SeasonalItemEffect { IncomeBonus = 0.10m }
            },
            new SeasonalShopItem
            {
                Id = $"{prefix}_xp_pack",
                NameKey = $"Seasonal{season}XpPack",
                DescriptionKey = $"Seasonal{season}XpPackDesc",
                Cost = 30,
                Icon = icon,
                Effect = new SeasonalItemEffect { XpBonus = 500 }
            },
            new SeasonalShopItem
            {
                Id = $"{prefix}_screw_bundle",
                NameKey = $"Seasonal{season}ScrewBundle",
                DescriptionKey = $"Seasonal{season}ScrewBundleDesc",
                Cost = 75,
                Icon = icon,
                Effect = new SeasonalItemEffect { GoldenScrews = 15 }
            },
            new SeasonalShopItem
            {
                Id = $"{prefix}_speed_boost",
                NameKey = $"Seasonal{season}SpeedBoost",
                DescriptionKey = $"Seasonal{season}SpeedBoostDesc",
                Cost = 100,
                Icon = icon,
                Effect = new SeasonalItemEffect { SpeedBoostMinutes = 120 }
            }
        ];
    }

    /// <summary>
    /// Wendet den Effekt eines saisonalen Shop-Items an.
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
    }
}
