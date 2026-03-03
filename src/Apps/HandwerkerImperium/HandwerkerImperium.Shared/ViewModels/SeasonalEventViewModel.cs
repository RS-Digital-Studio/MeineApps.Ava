using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel für saisonale Events (4x pro Jahr, jeweils 2 Wochen).
/// Zeigt Event-Info, Saison-Währung und den saisonalen Shop.
/// </summary>
public partial class SeasonalEventViewModel : ViewModelBase
{
    private readonly IGameStateService _gameStateService;
    private readonly ISeasonalEventService _seasonalEventService;
    private readonly ILocalizationService _localizationService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private ObservableCollection<SeasonalShopItemDisplay> _shopItems = [];

    [ObservableProperty]
    private string _eventName = "";

    [ObservableProperty]
    private string _seasonCurrency = "0";

    [ObservableProperty]
    private string _timeRemaining = "";

    [ObservableProperty]
    private bool _isEventActive;

    [ObservableProperty]
    private string _eventColor = "#D97706";

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public SeasonalEventViewModel(
        IGameStateService gameStateService,
        ISeasonalEventService seasonalEventService,
        ILocalizationService localizationService)
    {
        _gameStateService = gameStateService;
        _seasonalEventService = seasonalEventService;
        _localizationService = localizationService;

        UpdateLocalizedTexts();
        RefreshEvent();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void BuyItem(string? itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        _seasonalEventService.BuySeasonalItem(itemId);
        RefreshEvent();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert alle Event-Daten aus dem State.
    /// </summary>
    public void RefreshEvent()
    {
        var state = _gameStateService.State;
        var seasonalEvent = state.CurrentSeasonalEvent;

        IsEventActive = seasonalEvent != null && seasonalEvent.IsActive;

        if (seasonalEvent != null && IsEventActive)
        {
            // Event-Name aus Saison
            EventName = GetSeasonName(seasonalEvent.Season);
            EventColor = seasonalEvent.SeasonColor;
            SeasonCurrency = seasonalEvent.Currency.ToString();

            // Verbleibende Zeit
            var remaining = seasonalEvent.TimeRemaining;
            TimeRemaining = remaining.TotalHours >= 24
                ? $"{(int)remaining.TotalDays}d {remaining.Hours}h"
                : $"{(int)remaining.TotalHours}h {remaining.Minutes}m";

            // Shop-Items aufbauen
            BuildShopItems(seasonalEvent);
        }
        else
        {
            EventName = _localizationService.GetString("NoEventActive") ?? "Kein Event aktiv";
            EventColor = "#808080";
            SeasonCurrency = "0";
            TimeRemaining = "-";
            ShopItems.Clear();
        }
    }

    /// <summary>
    /// Lokalisierte Texte aktualisieren.
    /// </summary>
    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("SeasonalEvent") ?? "Saison-Event";
        RefreshEvent();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildShopItems(SeasonalEvent seasonalEvent)
    {
        var items = new ObservableCollection<SeasonalShopItemDisplay>();

        // Statische Shop-Items pro Saison generieren
        var shopItemDefs = GetSeasonalShopItems(seasonalEvent.Season);
        foreach (var def in shopItemDefs)
        {
            bool isPurchased = seasonalEvent.PurchasedItems.Contains(def.Id);
            bool canAfford = !isPurchased && seasonalEvent.Currency >= def.Cost;

            items.Add(new SeasonalShopItemDisplay
            {
                Id = def.Id,
                Name = _localizationService.GetString(def.NameKey) ?? def.NameKey,
                Description = _localizationService.GetString(def.DescriptionKey) ?? def.DescriptionKey,
                Cost = def.Cost,
                IsPurchased = isPurchased,
                CanAfford = canAfford
            });
        }

        ShopItems = items;
    }

    private string GetSeasonName(Season season) => season switch
    {
        Season.Spring => _localizationService.GetString("SeasonSpring") ?? "Frühling",
        Season.Summer => _localizationService.GetString("SeasonSummer") ?? "Sommer",
        Season.Autumn => _localizationService.GetString("SeasonAutumn") ?? "Herbst",
        Season.Winter => _localizationService.GetString("SeasonWinter") ?? "Winter",
        _ => season.ToString()
    };

    /// <summary>
    /// Gibt die statischen Shop-Item-Definitionen für eine Saison zurück.
    /// </summary>
    private static List<SeasonalShopItem> GetSeasonalShopItems(Season season)
    {
        string prefix = season.ToString().ToLower();
        return
        [
            new() { Id = $"{prefix}_boost_income", NameKey = $"Seasonal{season}Income", DescriptionKey = $"Seasonal{season}IncomeDesc", Cost = 50, Icon = "CurrencyEur" },
            new() { Id = $"{prefix}_boost_xp", NameKey = $"Seasonal{season}Xp", DescriptionKey = $"Seasonal{season}XpDesc", Cost = 30, Icon = "Star" },
            new() { Id = $"{prefix}_screws", NameKey = $"Seasonal{season}Screws", DescriptionKey = $"Seasonal{season}ScrewsDesc", Cost = 80, Icon = "Cog" },
            new() { Id = $"{prefix}_speed", NameKey = $"Seasonal{season}Speed", DescriptionKey = $"Seasonal{season}SpeedDesc", Cost = 100, Icon = "RocketLaunch" },
        ];
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// DISPLAY MODEL
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Anzeige-Modell für ein saisonales Shop-Item im UI.
/// </summary>
public class SeasonalShopItemDisplay
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Cost { get; set; }
    public bool IsPurchased { get; set; }
    public bool CanAfford { get; set; }

    /// <summary>
    /// Kosten-Anzeige mit Saison-Währungs-Symbol.
    /// </summary>
    public string CostDisplay => $"{Cost} SP";

    /// <summary>
    /// Kosten-Farbe: Grün wenn leistbar, Rot wenn nicht, Grau wenn gekauft.
    /// </summary>
    public string CostColor => IsPurchased ? "#808080" : CanAfford ? "#22C55E" : "#EF4444";

    /// <summary>
    /// Opacity: Gekaufte Items leicht gedimmt.
    /// </summary>
    public double DisplayOpacity => IsPurchased ? 0.6 : 1.0;
}
