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
using MeineApps.Core.Premium.Ava.Services;

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
    // V7 (Phase 3 Ressourcen-Plan): Stock-Anzeige im Markt soll auch bei Auto-Production
    // und Lager-Verkauf live aktualisieren (nicht nur bei Markt-Trades).
    private readonly IWarehouseService? _warehouse;
    private readonly ICraftingService? _crafting;
    // V7 (Phase 4 Ressourcen-Plan, Imperium-Pass): Premium schaltet Markt sofort frei
    // (Bypass auf logi_05). Nach Premium-Kauf muss die UI sofort den Locked-State verlassen.
    private readonly IPurchaseService? _purchase;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _emptyMessage = "";

    [ObservableProperty]
    private bool _isMarketAvailable;

    [ObservableProperty]
    private ObservableCollection<MarketEntryDisplay> _entries = [];

    /// <summary>
    /// V7 (Phase 3 Ressourcen-Plan): Aktuell im Detail-Panel ausgewaehlter Markt-Eintrag
    /// (zeigt 24h-Heatmap). Default = null, kein Detail.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedEntry))]
    [NotifyPropertyChangedFor(nameof(SelectedEntryPriceSeries))]
    [NotifyPropertyChangedFor(nameof(SelectedEntryCurrentHour))]
    private MarketEntryDisplay? _selectedEntry;

    public bool HasSelectedEntry => SelectedEntry != null;

    /// <summary>24-Werte-Array fuer den ausgewaehlten Eintrag (UTC-Stunden 0-23).</summary>
    public decimal[]? SelectedEntryPriceSeries =>
        SelectedEntry != null ? _market.Get24hPriceSeries(SelectedEntry.ProductId) : null;

    /// <summary>Aktuelle UTC-Stunde (fuer Now-Indikator im Chart).</summary>
    public int SelectedEntryCurrentHour => DateTime.UtcNow.Hour;

    /// <summary>
    /// Wird vom Chart-Control beim DataContextChanged gesetzt, damit der Renderer ueber
    /// Refreshes informiert wird. Nicht persistiert.
    /// </summary>
    public event Action? ChartInvalidated;

    public MarketViewModel(
        IGameStateService gameState,
        IMarketService market,
        ILocalizationService localization,
        IWarehouseService? warehouse = null,
        ICraftingService? crafting = null,
        IPurchaseService? purchase = null)
    {
        _gameState = gameState;
        _market = market;
        _localization = localization;
        _warehouse = warehouse;
        _crafting = crafting;
        _purchase = purchase;

        _market.MarketChanged += OnMarketChanged;
        _gameState.MoneyChanged += OnMoneyChanged;
        // V7 (Phase 3 Ressourcen-Plan): Auch Lager-Mutationen (Auto-Production, Verkauf
        // ausserhalb des Markts) und Crafting-Complete refresht die InStock-Anzeige.
        if (_warehouse != null) _warehouse.InventoryChanged += OnMarketChanged;
        if (_crafting != null) _crafting.CraftingUpdated += OnMarketChanged;
        // V7 (Phase 4 Imperium-Pass): Premium-Kauf schaltet Markt sofort frei — UI muss
        // sofort den Locked-State verlassen.
        if (_purchase != null) _purchase.PremiumStatusChanged += OnPremiumStatusChanged;

        UpdateLocalizedTexts();
        Refresh();
    }

    private void OnPremiumStatusChanged(object? sender, EventArgs e)
        => Dispatcher.UIThread.Post(Refresh);

    private void OnMarketChanged() => Dispatcher.UIThread.Post(Refresh);

    private void OnMoneyChanged(object? sender, Models.Events.MoneyChangedEventArgs e)
        => Dispatcher.UIThread.Post(Refresh);

    /// <summary>
    /// V7 (Phase 3 Ressourcen-Plan): Spieler hat im Detail-Panel auf einen Eintrag getippt
    /// — Heatmap-Detail oeffnet sich. Tippen auf den gleichen Eintrag schliesst das Detail.
    /// </summary>
    [RelayCommand]
    private void SelectEntry(MarketEntryDisplay? entry)
    {
        SelectedEntry = (SelectedEntry == entry) ? null : entry;
        ChartInvalidated?.Invoke();
    }

    [RelayCommand]
    private void CloseDetail() => SelectedEntry = null;

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

        // SelectedEntry resynchronisieren (Refresh ersetzt die Objekte, alter Ref-Pointer ist tot)
        if (SelectedEntry != null)
        {
            var refreshed = sorted.FirstOrDefault(e => e.ProductId == SelectedEntry.ProductId);
            if (refreshed != null && !ReferenceEquals(refreshed, SelectedEntry))
                SelectedEntry = refreshed;
            else if (refreshed == null)
                SelectedEntry = null;
        }
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
        if (_warehouse != null) _warehouse.InventoryChanged -= OnMarketChanged;
        if (_crafting != null) _crafting.CraftingUpdated -= OnMarketChanged;
        if (_purchase != null) _purchase.PremiumStatusChanged -= OnPremiumStatusChanged;
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
