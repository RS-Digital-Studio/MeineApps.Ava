using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Icons;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// V7 (Phase 3 Ressourcen-Plan): Markt-Sub-Tab im Shop.
/// Zeigt alle Materialien mit aktuellem Kauf-/Verkaufspreis, Trend und Buttons fuer Kauf/Verkauf.
/// </summary>
public sealed partial class MarketViewModel : ViewModelBase, IDisposable
{
    private readonly IGameStateService _gameState;
    private readonly IMarketService _market;
    private readonly ILocalizationService _localization;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _emptyMessage = "";

    [ObservableProperty]
    private bool _isMarketAvailable;

    [ObservableProperty]
    private ObservableCollection<MarketEntryDisplay> _entries = [];

    public MarketViewModel(
        IGameStateService gameState,
        IMarketService market,
        ILocalizationService localization)
    {
        _gameState = gameState;
        _market = market;
        _localization = localization;

        _market.MarketChanged += OnMarketChanged;
        _gameState.MoneyChanged += OnMoneyChanged;

        UpdateLocalizedTexts();
        Refresh();
    }

    private void OnMarketChanged() => Dispatcher.UIThread.Post(Refresh);

    private void OnMoneyChanged(object? sender, Models.Events.MoneyChangedEventArgs e)
        => Dispatcher.UIThread.Post(Refresh);

    public void UpdateLocalizedTexts()
    {
        Title = _localization.GetString("MarketTitle") ?? "Market";
        EmptyMessage = _localization.GetString("MarketLockedHint")
            ?? "Research 'Material Market Access' to unlock the market.";
        Refresh();
    }

    public void Refresh()
    {
        IsMarketAvailable = _market.IsMarketAvailable;
        if (!IsMarketAvailable)
        {
            Entries = new ObservableCollection<MarketEntryDisplay>();
            return;
        }

        var allProducts = CraftingProduct.GetAllProducts();
        var entries = new List<MarketEntryDisplay>();
        foreach (var (productId, product) in allProducts)
        {
            decimal buyPrice = _market.GetBuyPrice(productId);
            decimal sellPrice = _market.GetSellPrice(productId);
            double trend = _market.GetPriceTrend(productId);
            int inStock = _gameState.State.CraftingInventory.GetValueOrDefault(productId, 0);

            string name = _localization.GetString(product.NameKey) ?? product.NameKey;
            entries.Add(new MarketEntryDisplay
            {
                ProductId = productId,
                Name = name,
                Icon = GetProductIcon(productId),
                Tier = product.Tier,
                TierLabel = $"T{product.Tier}",
                BuyPrice = buyPrice,
                BuyPriceDisplay = MoneyFormatter.Format(buyPrice, 0),
                SellPrice = sellPrice,
                SellPriceDisplay = MoneyFormatter.Format(sellPrice, 0),
                InStock = inStock,
                StockDisplay = $"x{inStock}",
                Trend = trend,
                TrendDisplay = trend > 0.1 ? "↑" : trend < -0.1 ? "↓" : "→",
                TrendColor = trend > 0.1 ? "#22C55E" : trend < -0.1 ? "#EF4444" : "#9CA3AF",
                CanAffordOne = _gameState.State.Money >= buyPrice,
                HasStock = inStock > 0
            });
        }
        // Tier absteigend, dann Name
        var sorted = entries.OrderBy(e => e.Tier).ThenBy(e => e.Name).ToList();
        Entries = new ObservableCollection<MarketEntryDisplay>(sorted);
    }

    [RelayCommand]
    private void Buy1(MarketEntryDisplay? entry)
    {
        if (entry == null) return;
        _market.TryBuy(entry.ProductId, 1);
    }

    [RelayCommand]
    private void Buy10(MarketEntryDisplay? entry)
    {
        if (entry == null) return;
        _market.TryBuy(entry.ProductId, 10);
    }

    [RelayCommand]
    private void Sell1(MarketEntryDisplay? entry)
    {
        if (entry == null) return;
        _market.TrySell(entry.ProductId, 1);
    }

    [RelayCommand]
    private void Sell10(MarketEntryDisplay? entry)
    {
        if (entry == null) return;
        _market.TrySell(entry.ProductId, 10);
    }

    private static GameIconKind GetProductIcon(string productId) => productId switch
    {
        "planks" => GameIconKind.Forest,
        "furniture" => GameIconKind.SeatOutline,
        "luxury_furniture" => GameIconKind.Crown,
        "pipes" => GameIconKind.Pipe,
        "plumbing_system" => GameIconKind.Water,
        "bathroom_installation" => GameIconKind.ShowerHead,
        "cables" => GameIconKind.CableData,
        "circuit" => GameIconKind.Chip,
        "smart_home" => GameIconKind.HomeAutomation,
        "paint_mix" => GameIconKind.Palette,
        "wall_design" => GameIconKind.FormatPaint,
        "artwork" => GameIconKind.Palette,
        "roof_tiles" => GameIconKind.ViewGrid,
        "roofing_system" => GameIconKind.HomeRoof,
        "roof_structure" => GameIconKind.HomeRoof,
        "concrete" => GameIconKind.Wall,
        "concrete_foundation" => GameIconKind.OfficeBuildingOutline,
        "skyscraper_frame" => GameIconKind.OfficeBuilding,
        "blueprint" => GameIconKind.Compass,
        "framework" => GameIconKind.DomainPlus,
        "master_blueprint" => GameIconKind.City,
        "contract" => GameIconKind.FileDocumentCheck,
        "contract_complex" => GameIconKind.FileDocumentCheck,
        "general_contract" => GameIconKind.Bank,
        "fittings" => GameIconKind.Anvil,
        "master_fittings" => GameIconKind.HammerWrench,
        "masterpiece_fittings" => GameIconKind.Trophy,
        "prototype" => GameIconKind.LightbulbOnOutline,
        "innovation" => GameIconKind.LightbulbOn,
        "patent" => GameIconKind.StarFourPoints,
        _ => GameIconKind.PackageVariant
    };

    public void Dispose()
    {
        _market.MarketChanged -= OnMarketChanged;
        _gameState.MoneyChanged -= OnMoneyChanged;
    }
}

public class MarketEntryDisplay
{
    public string ProductId { get; set; } = "";
    public string Name { get; set; } = "";
    public GameIconKind Icon { get; set; } = GameIconKind.PackageVariant;
    public int Tier { get; set; }
    public string TierLabel { get; set; } = "";
    public decimal BuyPrice { get; set; }
    public string BuyPriceDisplay { get; set; } = "";
    public decimal SellPrice { get; set; }
    public string SellPriceDisplay { get; set; } = "";
    public int InStock { get; set; }
    public string StockDisplay { get; set; } = "";
    public double Trend { get; set; }
    public string TrendDisplay { get; set; } = "";
    public string TrendColor { get; set; } = "";
    public bool CanAffordOne { get; set; }
    public bool HasStock { get; set; }
}
