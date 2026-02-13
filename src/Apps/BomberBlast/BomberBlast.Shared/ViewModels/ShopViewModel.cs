using System.Collections.ObjectModel;
using BomberBlast.Models;
using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel fuer den Shop - zeigt Upgrades und Coin-Stand.
/// Implementiert IDisposable fuer BalanceChanged-Unsubscription.
/// </summary>
public partial class ShopViewModel : ObservableObject, IDisposable
{
    private readonly IShopService _shopService;
    private readonly ICoinService _coinService;
    private readonly ILocalizationService _localizationService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;

    /// <summary>Kauf erfolgreich (Upgrade-Name)</summary>
    public event Action<string>? PurchaseSucceeded;

    /// <summary>Zu wenig Coins</summary>
    public event Action? InsufficientFunds;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<ShopDisplayItem> _shopItems = [];

    [ObservableProperty]
    private string _coinsText = "0";

    [ObservableProperty]
    private int _coinBalance;

    [ObservableProperty]
    private string _shopTitleText = "";

    [ObservableProperty]
    private string _sectionStartUpgradesText = "";

    [ObservableProperty]
    private string _sectionScoreBoosterText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public ShopViewModel(IShopService shopService, ICoinService coinService, ILocalizationService localizationService)
    {
        _shopService = shopService;
        _coinService = coinService;
        _localizationService = localizationService;

        _coinService.BalanceChanged += OnBalanceChanged;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    public void OnAppearing()
    {
        UpdateLocalizedTexts();
        RefreshItems();
        UpdateCoinDisplay();
    }

    public void UpdateLocalizedTexts()
    {
        ShopTitleText = _localizationService.GetString("ShopTitle");
        SectionStartUpgradesText = _localizationService.GetString("SectionStartUpgrades");
        SectionScoreBoosterText = _localizationService.GetString("SectionScoreBooster");
    }

    private void RefreshItems()
    {
        var items = _shopService.GetShopItems();

        // Namen und Beschreibungen lokalisieren
        foreach (var item in items)
        {
            item.DisplayName = _localizationService.GetString(item.NameKey);
            item.DisplayDescription = _localizationService.GetString(item.DescriptionKey);
            item.LevelText = string.Format(
                _localizationService.GetString("UpgradeLevelFormat"),
                item.CurrentLevel, item.MaxLevel);
            item.Refresh(_coinService.Balance);
        }

        ShopItems = new ObservableCollection<ShopDisplayItem>(items);
    }

    private void UpdateCoinDisplay()
    {
        CoinBalance = _coinService.Balance;
        CoinsText = _coinService.Balance.ToString("N0");
    }

    private void OnBalanceChanged(object? sender, EventArgs e)
    {
        UpdateCoinDisplay();
        foreach (var item in ShopItems)
        {
            item.Refresh(_coinService.Balance);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void Purchase(ShopDisplayItem? item)
    {
        if (item == null || item.IsMaxed) return;

        if (!_coinService.CanAfford(item.NextPrice))
        {
            InsufficientFunds?.Invoke();
            return;
        }

        if (_shopService.TryPurchase(item.Type))
        {
            var upgradeName = _localizationService.GetString(item.NameKey);
            PurchaseSucceeded?.Invoke(upgradeName ?? item.NameKey);
            RefreshItems();
            UpdateCoinDisplay();
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    public void Dispose()
    {
        _coinService.BalanceChanged -= OnBalanceChanged;
    }
}
