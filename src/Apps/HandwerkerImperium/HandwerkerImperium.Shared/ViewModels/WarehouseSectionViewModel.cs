using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Icons;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// V7 (Phase 1 Ressourcen-Plan): ViewModel fuer den Lager-Sub-Tab im Imperium.
/// Zeigt Slot-Belegung, Lagerwert, alle Materialien mit Anzahl/Preis/Auto-Verkauf-Toggle
/// und den Slot-Upgrade-Button.
/// </summary>
public sealed partial class WarehouseSectionViewModel : ViewModelBase, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly IWarehouseService _warehouseService;
    private readonly ICraftingService _craftingService;
    private readonly ILocalizationService _localizationService;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _subtitle = "";

    /// <summary>Header-Anzeige: "12 / 20 Slots".</summary>
    [ObservableProperty]
    private string _slotDisplay = "";

    /// <summary>Header-Anzeige: "Stack max. 50".</summary>
    [ObservableProperty]
    private string _stackLimitDisplay = "";

    /// <summary>Header-Anzeige: "Gesamtwert: 12.5K €".</summary>
    [ObservableProperty]
    private string _totalValueDisplay = "";

    /// <summary>Header-Anzeige: "Lager-Slots erweitern (+5) — 50.000 €".</summary>
    [ObservableProperty]
    private string _upgradeButtonText = "";

    [ObservableProperty]
    private bool _canUpgradeSlots;

    [ObservableProperty]
    private bool _isMaxSlots;

    [ObservableProperty]
    private ObservableCollection<WarehouseSlotDisplay> _slots = [];

    /// <summary>True wenn keine einzige Material-Sorte gelagert wird (Empty-State).</summary>
    public bool HasItems => Slots.Count > 0;

    partial void OnSlotsChanged(ObservableCollection<WarehouseSlotDisplay> value)
        => OnPropertyChanged(nameof(HasItems));

    public WarehouseSectionViewModel(
        IGameStateService gameStateService,
        IWarehouseService warehouseService,
        ICraftingService craftingService,
        ILocalizationService localizationService)
    {
        _gameStateService = gameStateService;
        _warehouseService = warehouseService;
        _craftingService = craftingService;
        _localizationService = localizationService;

        _warehouseService.InventoryChanged += OnInventoryChanged;
        _gameStateService.MoneyChanged += OnMoneyChanged;

        UpdateLocalizedTexts();
        Refresh();
    }

    private void OnInventoryChanged() =>
        Dispatcher.UIThread.Post(Refresh);

    private void OnMoneyChanged(object? sender, Models.Events.MoneyChangedEventArgs e) =>
        Dispatcher.UIThread.Post(RefreshUpgradeState);

    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("WarehouseTitle") ?? "Warehouse";
        Subtitle = _localizationService.GetString("WarehouseSubtitle") ?? "Manage your raw materials and crafted goods.";
        Refresh();
    }

    public void Refresh()
    {
        var state = _gameStateService.State;
        var allProducts = CraftingProduct.GetAllProducts();

        // Header
        int used = _warehouseService.UsedSlotCount;
        int max = state.WarehouseSlotCount;
        SlotDisplay = string.Format(
            _localizationService.GetString("WarehouseSlotsFormat") ?? "{0} / {1} slots",
            used, max);
        StackLimitDisplay = string.Format(
            _localizationService.GetString("WarehouseStackLimitFormat") ?? "Max. {0} per slot",
            state.WarehouseStackLimit);
        TotalValueDisplay = MoneyFormatter.Format(_warehouseService.GetTotalWarehouseValue(), 0);

        // Slot-Liste
        var slots = new ObservableCollection<WarehouseSlotDisplay>();
        foreach (var (productId, count) in state.CraftingInventory)
        {
            if (count <= 0) continue;

            string name = allProducts.TryGetValue(productId, out var product)
                ? _localizationService.GetString(product.NameKey) ?? product.NameKey
                : productId;

            int reserved = state.ReservedInventory.GetValueOrDefault(productId, 0);
            decimal unitPrice = _craftingService.GetSellPrice(productId);
            var rule = _warehouseService.GetAutoSellRule(productId);

            int tier = product?.Tier ?? 0;
            double stackFillRatio = state.WarehouseStackLimit > 0
                ? Math.Clamp((double)count / state.WarehouseStackLimit, 0.0, 1.0)
                : 0.0;

            slots.Add(new WarehouseSlotDisplay
            {
                ProductId = productId,
                Name = name,
                Icon = GetProductIcon(productId),
                Tier = tier,
                TierLabel = tier > 0 ? $"T{tier}" : "",
                Quantity = count,
                Reserved = reserved,
                QuantityDisplay = reserved > 0 ? $"{count - reserved} / {count}" : $"{count}",
                StackFillRatio = stackFillRatio,
                StackFillPercent = $"{stackFillRatio * 100:F0}%",
                StackLimit = state.WarehouseStackLimit,
                UnitValue = unitPrice,
                UnitValueDisplay = MoneyFormatter.Format(unitPrice, 0),
                TotalValueDisplay = MoneyFormatter.Format(unitPrice * count, 0),
                AutoSellEnabled = rule.Enabled,
                CanSell = (count - reserved) > 0
            });
        }
        // Sortierung: Tier absteigend, dann Wert absteigend
        var sorted = slots.OrderByDescending(s => s.Tier).ThenByDescending(s => s.UnitValue).ToList();
        Slots = new ObservableCollection<WarehouseSlotDisplay>(sorted);

        RefreshUpgradeState();
    }

    private void RefreshUpgradeState()
    {
        IsMaxSlots = _gameStateService.State.WarehouseSlotCount >= _warehouseService.MaxSlotCount;
        if (IsMaxSlots)
        {
            UpgradeButtonText = _localizationService.GetString("WarehouseMaxSlots") ?? "Max. slots reached";
            CanUpgradeSlots = false;
        }
        else
        {
            decimal cost = _warehouseService.GetNextSlotUpgradeCost();
            UpgradeButtonText = string.Format(
                _localizationService.GetString("WarehouseUpgradeButton") ?? "+{0} slots — {1}",
                WarehouseService.SlotsPerUpgrade,
                MoneyFormatter.Format(cost, 0));
            CanUpgradeSlots = _warehouseService.CanUpgradeSlots();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void SellOne(WarehouseSlotDisplay? slot)
    {
        if (slot == null || string.IsNullOrEmpty(slot.ProductId)) return;
        _craftingService.SellProducts(slot.ProductId, 1);
    }

    [RelayCommand]
    private void SellTen(WarehouseSlotDisplay? slot)
    {
        if (slot == null || string.IsNullOrEmpty(slot.ProductId)) return;
        _craftingService.SellProducts(slot.ProductId, 10);
    }

    [RelayCommand]
    private void SellAllOfSlot(WarehouseSlotDisplay? slot)
    {
        if (slot == null || string.IsNullOrEmpty(slot.ProductId)) return;
        int sellable = slot.Quantity - slot.Reserved;
        if (sellable > 0)
            _craftingService.SellProducts(slot.ProductId, sellable);
    }

    [RelayCommand]
    private void ToggleAutoSell(WarehouseSlotDisplay? slot)
    {
        if (slot == null || string.IsNullOrEmpty(slot.ProductId)) return;
        bool newState = !slot.AutoSellEnabled;
        _warehouseService.SetAutoSellEnabled(slot.ProductId, newState);
    }

    [RelayCommand(CanExecute = nameof(CanUpgradeSlots))]
    private void UpgradeSlots()
    {
        _warehouseService.TryUpgradeSlots();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private static GameIconKind GetProductIcon(string productId) => productId switch
    {
        // Schreiner
        "planks" => GameIconKind.Forest,
        "furniture" => GameIconKind.SeatOutline,
        "luxury_furniture" => GameIconKind.Crown,
        // Klempner
        "pipes" => GameIconKind.Pipe,
        "plumbing_system" => GameIconKind.Water,
        "bathroom_installation" => GameIconKind.ShowerHead,
        // Elektriker
        "cables" => GameIconKind.CableData,
        "circuit" => GameIconKind.Chip,
        "smart_home" => GameIconKind.HomeAutomation,
        // Maler
        "paint_mix" => GameIconKind.Palette,
        "wall_design" => GameIconKind.FormatPaint,
        "artwork" => GameIconKind.Palette,
        // Dachdecker
        "roof_tiles" => GameIconKind.ViewGrid,
        "roofing_system" => GameIconKind.HomeRoof,
        "roof_structure" => GameIconKind.HomeRoof,
        // Bauunternehmer
        "concrete" => GameIconKind.Wall,
        "concrete_foundation" => GameIconKind.OfficeBuildingOutline,
        "skyscraper_frame" => GameIconKind.OfficeBuilding,
        // Architekt
        "blueprint" => GameIconKind.Compass,
        "framework" => GameIconKind.DomainPlus,
        "master_blueprint" => GameIconKind.City,
        // Generalunternehmer
        "contract" => GameIconKind.FileDocumentCheck,
        "contract_complex" => GameIconKind.FileDocumentCheck,
        "general_contract" => GameIconKind.Bank,
        // Meisterschmiede
        "fittings" => GameIconKind.Anvil,
        "master_fittings" => GameIconKind.HammerWrench,
        "masterpiece_fittings" => GameIconKind.Trophy,
        // Innovationslabor
        "prototype" => GameIconKind.LightbulbOnOutline,
        "innovation" => GameIconKind.LightbulbOn,
        "patent" => GameIconKind.StarFourPoints,
        _ => GameIconKind.PackageVariant
    };

    public void Dispose()
    {
        _warehouseService.InventoryChanged -= OnInventoryChanged;
        _gameStateService.MoneyChanged -= OnMoneyChanged;
    }
}

/// <summary>
/// Anzeige-Modell fuer einen Lager-Slot.
/// </summary>
public class WarehouseSlotDisplay
{
    public string ProductId { get; set; } = "";
    public string Name { get; set; } = "";
    public GameIconKind Icon { get; set; } = GameIconKind.PackageVariant;
    public int Tier { get; set; }
    public string TierLabel { get; set; } = "";
    public int Quantity { get; set; }
    public int Reserved { get; set; }
    public string QuantityDisplay { get; set; } = "";
    /// <summary>Stack-Fuell-Verhaeltnis 0..1 fuer Fortschrittsbalken.</summary>
    public double StackFillRatio { get; set; }
    public string StackFillPercent { get; set; } = "";
    public int StackLimit { get; set; }
    public decimal UnitValue { get; set; }
    public string UnitValueDisplay { get; set; } = "";
    public string TotalValueDisplay { get; set; } = "";
    public bool AutoSellEnabled { get; set; }
    public bool CanSell { get; set; }
}
