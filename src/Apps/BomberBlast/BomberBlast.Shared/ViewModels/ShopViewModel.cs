using System.Collections.ObjectModel;
using BomberBlast.Models;
using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel fuer den Shop - zeigt Upgrades, PowerUp-Übersicht, Skins und Coin-Stand.
/// Implementiert IDisposable fuer BalanceChanged-Unsubscription.
///
/// <para>v2.0.39 (Plan Task 2.3): Partial-Class-Split — Methoden auf 4 Files aufgeteilt:
/// <list type="bullet">
///   <item>ShopViewModel.cs — Header + Lifecycle + Localization + Balance-Updates</item>
///   <item>ShopViewModel.Upgrades.cs — Permanent-Upgrades + PowerUp/Mechanic + Mappings</item>
///   <item>ShopViewModel.Skins.cs — alle 6 Skin-Kategorien (Player/Bomb/Explosion/Trail/Victory/Frame)</item>
///   <item>ShopViewModel.Deals.cs — Rotating-Deals + Gem-Skin-Logik</item>
/// </list>
/// </para>
/// </summary>
public sealed partial class ShopViewModel : ViewModelBase, INavigable, IGameJuiceEmitter, IDisposable, ILocalizable
{
    private readonly IShopService _shopService;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ILocalizationService _localizationService;
    private readonly IProgressService _progressService;
    private readonly ICustomizationService _customizationService;
    private readonly IPurchaseService _purchaseService;
    private readonly IRewardedAdService _rewardedAdService;
    /// <summary>Sprint 2.2 AAA-Audit #2: Funnel-Telemetrie fuer Rewarded-Ad-Placements.</summary>
    private readonly IAnalyticsService _analytics;
    private readonly MeineApps.Core.Ava.Services.IPreferencesService _preferencesService;
    private readonly IRotatingDealsService _rotatingDealsService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<NavigationRequest>? NavigationRequested;
    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;

    // IGameJuiceEmitter-Pflichtevent. Shop emittiert aktuell keine Celebration
    // (Kauefe zeigen bereits PurchaseSucceeded mit Floating-Text) — CS0067 bewusst unterdrueckt.
#pragma warning disable CS0067
    public event Action? CelebrationRequested;
#pragma warning restore CS0067

    /// <summary>Kauf erfolgreich (Upgrade-Name)</summary>
    public event Action<string>? PurchaseSucceeded;

    /// <summary>Zu wenig Coins</summary>
    public event Action? InsufficientFunds;

    /// <summary>Bestätigungsdialog anfordern (Titel, Nachricht, Akzeptieren, Abbrechen)</summary>
    public event Func<string, string, string, string, Task<bool>>? ConfirmationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<ShopDisplayItem> _shopItems = [];

    [ObservableProperty]
    private ObservableCollection<PowerUpDisplayItem> _powerUpItems = [];

    [ObservableProperty]
    private ObservableCollection<PowerUpDisplayItem> _mechanicItems = [];

    [ObservableProperty]
    private ObservableCollection<SkinDisplayItem> _skinItems = [];

    [ObservableProperty]
    private ObservableCollection<SkinDisplayItem> _bombSkinItems = [];

    [ObservableProperty]
    private ObservableCollection<SkinDisplayItem> _explosionSkinItems = [];

    [ObservableProperty]
    private ObservableCollection<SkinDisplayItem> _trailItems = [];

    [ObservableProperty]
    private ObservableCollection<SkinDisplayItem> _victoryItems = [];

    [ObservableProperty]
    private ObservableCollection<SkinDisplayItem> _frameItems = [];

    [ObservableProperty]
    private string _coinsText = "0";

    [ObservableProperty]
    private int _coinBalance;

    /// <summary>v2.0.48 — Currency-Pulse-Animation bei Coin-Update (analog MainMenu)</summary>
    [ObservableProperty]
    private bool _isCoinsPulse;

    [ObservableProperty]
    private bool _isGemsPulse;

    partial void OnCoinsTextChanged(string value)
    {
        IsCoinsPulse = true;
        _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Task.Delay(280);
            IsCoinsPulse = false;
        });
    }

    partial void OnGemsTextChanged(string value)
    {
        IsGemsPulse = true;
        _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Task.Delay(280);
            IsGemsPulse = false;
        });
    }

    [ObservableProperty]
    private string _shopTitleText = "";

    [ObservableProperty]
    private string _sectionStartUpgradesText = "";

    [ObservableProperty]
    private string _sectionScoreBoosterText = "";

    [ObservableProperty]
    private string _sectionPowerUpsText = "";

    [ObservableProperty]
    private string _sectionMechanicsText = "";

    [ObservableProperty]
    private string _sectionSkinsText = "";

    [ObservableProperty]
    private string _sectionBombSkinsText = "";

    [ObservableProperty]
    private string _sectionExplosionSkinsText = "";

    [ObservableProperty]
    private string _sectionTrailsText = "";

    [ObservableProperty]
    private string _sectionVictoriesText = "";

    [ObservableProperty]
    private string _sectionFramesText = "";

    // Gratis-Upgrade per Ad (1x pro Tag)
    /// <summary>Ob der Button "Gratis-Upgrade per Werbung" angezeigt wird</summary>
    [ObservableProperty]
    private bool _canWatchAdForFreeUpgrade;

    /// <summary>Ob heute bereits ein Gratis-Upgrade per Ad genutzt wurde</summary>
    [ObservableProperty]
    private bool _hasFreeUpgradeToday;

    /// <summary>Ob das nächste Upgrade kostenlos ist (Flag nach erfolgreicher Ad)</summary>
    [ObservableProperty]
    private bool _freeUpgradeActive;

    [ObservableProperty]
    private string _freeUpgradeButtonText = "";

    [ObservableProperty]
    private string _freeUpgradeActiveText = "";

    // Rotierende Deals
    [ObservableProperty]
    private ObservableCollection<RotatingDeal> _dailyDeals = [];

    [ObservableProperty]
    private RotatingDeal? _weeklyDeal;

    [ObservableProperty]
    private string _sectionDailyDealsText = "";

    [ObservableProperty]
    private string _sectionWeeklyDealText = "";

    // Gem-exklusive Skins
    [ObservableProperty]
    private ObservableCollection<SkinDisplayItem> _gemSkinItems = [];

    [ObservableProperty]
    private string _sectionGemSkinsText = "";

    [ObservableProperty]
    private string _gemsText = "0";

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public ShopViewModel(IShopService shopService, ICoinService coinService, IGemService gemService,
        ILocalizationService localizationService, IProgressService progressService,
        ICustomizationService customizationService, IPurchaseService purchaseService,
        IRewardedAdService rewardedAdService, MeineApps.Core.Ava.Services.IPreferencesService preferencesService,
        IRotatingDealsService rotatingDealsService, IAnalyticsService analytics)
    {
        _shopService = shopService;
        _coinService = coinService;
        _gemService = gemService;
        _localizationService = localizationService;
        _progressService = progressService;
        _customizationService = customizationService;
        _purchaseService = purchaseService;
        _rewardedAdService = rewardedAdService;
        _analytics = analytics;
        _preferencesService = preferencesService;
        _rotatingDealsService = rotatingDealsService;

        _coinService.BalanceChanged += OnBalanceChanged;
        _gemService.BalanceChanged += OnBalanceChanged;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    public void OnAppearing()
    {
        UpdateLocalizedTexts();
        RefreshItems();
        RefreshPowerUpItems();
        RefreshMechanicItems();
        RefreshSkinItems();
        RefreshBombSkinItems();
        RefreshExplosionSkinItems();
        RefreshTrailItems();
        RefreshVictoryItems();
        RefreshFrameItems();
        RefreshDailyDeals();
        RefreshGemSkinItems();
        UpdateCoinDisplay();
        UpdateGemDisplay();
        CheckFreeUpgradeAvailability();
    }

    public void UpdateLocalizedTexts()
    {
        ShopTitleText = _localizationService.GetString("ShopTitle");
        SectionStartUpgradesText = _localizationService.GetString("SectionStartUpgrades");
        SectionScoreBoosterText = _localizationService.GetString("SectionScoreBooster");
        SectionPowerUpsText = _localizationService.GetString("SectionPowerUps");
        SectionMechanicsText = _localizationService.GetString("SectionMechanics");
        SectionSkinsText = _localizationService.GetString("SectionSkins") ?? _localizationService.GetString("SkinsTitle");
        SectionBombSkinsText = _localizationService.GetString("SectionBombSkins") ?? "Bomb Skins";
        SectionExplosionSkinsText = _localizationService.GetString("SectionExplosionSkins") ?? "Explosion Skins";
        SectionTrailsText = _localizationService.GetString("SectionTrails") ?? "Trails";
        SectionVictoriesText = _localizationService.GetString("SectionVictories") ?? "Victory Animations";
        SectionFramesText = _localizationService.GetString("SectionFrames") ?? "Profile Frames";
        FreeUpgradeButtonText = _localizationService.GetString("WatchAdFreeUpgrade") ?? "Free Upgrade via Ad";
        FreeUpgradeActiveText = _localizationService.GetString("FreeUpgradeActive") ?? "Next upgrade is free!";
        SectionDailyDealsText = _localizationService.GetString("DailyDealsTitle") ?? "Daily Deals";
        SectionWeeklyDealText = _localizationService.GetString("WeeklyDealTitle") ?? "Weekly Deal";
        SectionGemSkinsText = _localizationService.GetString("GemSkinsTitle") ?? "Exclusive Gem Skins";
    }

    private void UpdateCoinDisplay()
    {
        CoinBalance = _coinService.Balance;
        CoinsText = _coinService.Balance.ToString("N0");
    }

    private void UpdateGemDisplay()
    {
        GemsText = _gemService.Balance.ToString("N0");
    }

    private void OnBalanceChanged(object? sender, EventArgs e)
    {
        UpdateCoinDisplay();
        foreach (var item in ShopItems)
        {
            item.Refresh(_coinService.Balance, _gemService.Balance);
        }
        // Skin-Collections NICHT neu erstellen: Kein Skin-Property hängt vom Balance ab.
        // CanBuy = !IsOwned && !IsLocked && CoinPrice > 0 (balance-unabhängig).
        // Rebuild passiert nur bei Kauf (PurchaseSkinAsync) oder Auswahl (SelectSkin).
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke(new GoBack());
    }

    public void Dispose()
    {
        _coinService.BalanceChanged -= OnBalanceChanged;
        _gemService.BalanceChanged -= OnBalanceChanged;
    }

    /// <summary>
    /// Collection in-place neuladen statt via Property-Assignment.
    /// Vermeidet PropertyChanged-Event → Binding-komplett-Rebind (~50-150ms pro Tab-Wechsel).
    /// Löst nur CollectionChanged-Events aus, ItemsControl re-nutzt vorhandene Container.
    /// </summary>
    private static void ReloadCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }
}
