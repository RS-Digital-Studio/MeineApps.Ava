using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the shop page.
/// Manages in-app purchases and premium features.
/// </summary>
public sealed partial class ShopViewModel : ViewModelBase, INavigable, IDisposable
{
    private bool _disposed;
    private bool _isBusy;
    private readonly IGameStateService _gameStateService;
    private readonly IAudioService _audioService;
    private readonly ISaveGameService _saveGameService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IPurchaseService _purchaseService;
    private readonly ILocalizationService _localizationService;
    private readonly IEquipmentService _equipmentService;
    private readonly IVipService _vipService;
    private readonly IDialogService _dialogService;
    private readonly IDailyBundleService? _dailyBundleService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isPremium;

    [ObservableProperty]
    private string _currentBalance = "0 €";

    [ObservableProperty]
    private List<ShopItem> _shopItems = [];

    [ObservableProperty]
    private List<ToolDisplayItem> _tools = [];

    [ObservableProperty]
    private string _goldenScrewsBalance = "0";

    /// <summary>
    /// Ausrüstungs-Shop-Angebote (3-4 zufällige Gegenstände).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<EquipmentShopItem> _equipmentShopItems = [];

    /// <summary>
    /// Ob Equipment-Shop-Items vorhanden sind.
    /// </summary>
    [ObservableProperty]
    private bool _hasEquipmentShop;

    /// <summary>
    /// Vergleichstext: Aktuelles vs. Premium-Einkommen (nur für Nicht-Premium-Spieler).
    /// </summary>
    [ObservableProperty]
    private string _premiumIncomeComparison = string.Empty;

    /// <summary>Aktuelles Tages-Bundle (null wenn deaktiviert).</summary>
    [ObservableProperty]
    private DailyBundleOffer? _currentDailyBundle;

    /// <summary>True wenn ein Bundle aktiv ist (für UI-Visibility-Binding).</summary>
    public bool HasDailyBundle => CurrentDailyBundle != null;

    partial void OnCurrentDailyBundleChanged(DailyBundleOffer? value)
    {
        OnPropertyChanged(nameof(HasDailyBundle));
    }

    /// <summary>
    /// Indicates whether ads should be shown (not premium).
    /// </summary>
    public bool ShowAds => !IsPremium;

    /// <summary>
    /// Localized text for restore purchases button.
    /// </summary>
    public string RestorePurchasesText => _localizationService.GetString("RestorePurchases") ?? "Restore Purchases";

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    private readonly IAnalyticsService? _analyticsService;

    /// <summary>v2.1.0: Reputation-Shop als Sub-Section im Shop (Section sichtbar ab Reputation 60).</summary>
    public ReputationShopViewModel ReputationShopVM { get; }

    public ShopViewModel(
        IGameStateService gameStateService,
        IAudioService audioService,
        ISaveGameService saveGameService,
        IRewardedAdService rewardedAdService,
        IPurchaseService purchaseService,
        ILocalizationService localizationService,
        IEquipmentService equipmentService,
        IVipService vipService,
        IDialogService dialogService,
        ReputationShopViewModel reputationShopVm,
        IAnalyticsService? analyticsService = null,
        IDailyBundleService? dailyBundleService = null)
    {
        _gameStateService = gameStateService;
        _audioService = audioService;
        _saveGameService = saveGameService;
        _rewardedAdService = rewardedAdService;
        _purchaseService = purchaseService;
        _localizationService = localizationService;
        _equipmentService = equipmentService;
        _vipService = vipService;
        _dialogService = dialogService;
        _analyticsService = analyticsService;
        _dailyBundleService = dailyBundleService;
        ReputationShopVM = reputationShopVm;

        // Subscribe to premium status changes
        _purchaseService.PremiumStatusChanged += OnPremiumStatusChanged;

        // Geld- und Goldschrauben-Anzeige live aktualisieren
        _gameStateService.MoneyChanged += OnMoneyChanged;
        _gameStateService.GoldenScrewsChanged += OnGoldenScrewsChanged;

        // Bundle live aktualisieren bei Tageswechsel
        if (_dailyBundleService != null)
            _dailyBundleService.BundleRotated += OnDailyBundleRotated;

        LoadShopData();
        LoadTools();
        LoadEquipmentShop();
        RefreshDailyBundle();
    }

    private void OnDailyBundleRotated()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(RefreshDailyBundle);
    }

    /// <summary>Aktualisiert <see cref="CurrentDailyBundle"/> aus dem Service.</summary>
    public void RefreshDailyBundle()
    {
        CurrentDailyBundle = _dailyBundleService?.GetCurrentBundle();
    }

    /// <summary>Kauft das aktuelle Bundle (Service verbucht Bonus-Items).</summary>
    [RelayCommand]
    private async Task PurchaseDailyBundleAsync()
    {
        if (_dailyBundleService is null || _isBusy) return;
        _isBusy = true;
        try
        {
            var ok = await _dailyBundleService.PurchaseCurrentBundleAsync();
            if (ok) RefreshDailyBundle();
        }
        finally { _isBusy = false; }
    }

    private void OnPremiumStatusChanged(object? sender, EventArgs e)
    {
        IsPremium = _purchaseService.IsPremium;
        _gameStateService.State.IsPremium = _purchaseService.IsPremium;
        _rewardedAdService.Disable();
        OnPropertyChanged(nameof(ShowAds));
        LoadShopData();
        LoadTools();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    public void LoadShopData()
    {
        var state = _gameStateService.State;
        IsPremium = state.IsPremium;
        CurrentBalance = FormatMoney(state.Money);

        // Premium-Einkommensvergleich nur für Nicht-Premium-Spieler berechnen
        UpdatePremiumIncomeComparison();

        // Create shop items with localized texts
        ShopItems =
        [
            new ShopItem
            {
                Id = "premium",
                // V7 (, Section 10): Premium-Kauf wird zum "Imperium-Pass"
                // repositioniert — bestehender Preis 4,99 €, klare Bundle-Boni-Liste in der Detail-Card.
                Name = _localizationService.GetString("ImperiumPassName") ?? _localizationService.GetString("ShopPremiumName"),
                Description = _localizationService.GetString("ImperiumPassDesc") ?? _localizationService.GetString("ShopPremiumDesc"),
                Icon = "Crown",
                Price = "4,99 €",
                IsPremiumItem = true,
                IsPurchased = state.IsPremium
            },
            new ShopItem
            {
                Id = "booster_2x_30min",
                Name = _localizationService.GetString("ShopBooster30MinName"),
                Description = _localizationService.GetString("ShopBooster30MinDesc"),
                Icon = "RocketLaunch",
                Price = _localizationService.GetString("WatchVideo"),
                IsAdReward = true
            },
            new ShopItem
            {
                Id = "booster_2x_2h",
                Name = _localizationService.GetString("ShopBooster2hName"),
                Description = _localizationService.GetString("ShopBooster2hDesc"),
                Icon = "DiamondStone",
                Price = "1,99 €",
                IsPremiumItem = true
            },
            new ShopItem
            {
                Id = "instant_cash_small",
                Name = _localizationService.GetString("ShopCashSmallName"),
                Description = string.Format(_localizationService.GetString("ShopCashSmallDescScaled") ?? "{0}", MoneyFormatter.FormatCompact(GetInstantCashAmount("instant_cash_small"))),
                Icon = "Cash",
                Price = _localizationService.GetString("WatchVideo"),
                IsAdReward = true
            },
            new ShopItem
            {
                Id = "instant_cash_large",
                Name = _localizationService.GetString("ShopCashLargeName"),
                Description = string.Format(_localizationService.GetString("ShopCashLargeDescScaled") ?? "{0}", MoneyFormatter.FormatCompact(GetInstantCashAmount("instant_cash_large"))),
                Icon = "CashMultiple",
                Price = "0,99 €",
                IsPremiumItem = true
            },
            new ShopItem
            {
                Id = "instant_cash_huge",
                Name = _localizationService.GetString("ShopCashHugeName"),
                Description = string.Format(_localizationService.GetString("ShopCashHugeDesc") ?? "{0}", MoneyFormatter.FormatCompact(GetInstantCashAmount("instant_cash_huge"))),
                Icon = "DiamondStone",
                Price = "2,49 €",
                IsPremiumItem = true
            },
            new ShopItem
            {
                Id = "instant_cash_mega",
                Name = _localizationService.GetString("ShopCashMegaName"),
                Description = string.Format(_localizationService.GetString("ShopCashMegaDesc") ?? "{0}", MoneyFormatter.FormatCompact(GetInstantCashAmount("instant_cash_mega"))),
                Icon = "Crown",
                Price = "3,99 €",
                IsPremiumItem = true
            },
            new ShopItem
            {
                Id = "skip_time_1h",
                Name = _localizationService.GetString("ShopTimeSkipName") ?? _localizationService.GetString("ShopSkipTimeName"),
                Description = _localizationService.GetString("ShopTimeSkipDesc") ?? _localizationService.GetString("ShopSkipTimeDesc"),
                Icon = "TimerOutline",
                Price = _localizationService.GetString("WatchVideo"),
                IsAdReward = true
            },
            // Goldschrauben: Video-Ad
            new ShopItem
            {
                Id = "golden_screws_ad",
                Name = _localizationService.GetString("ShopGoldenScrewsAdName"),
                Description = _localizationService.GetString("ShopGoldenScrewsAdDesc"),
                Icon = "ScrewFlatTop",
                Price = _localizationService.GetString("WatchVideo"),
                IsAdReward = true
            },
            // Goldschrauben: IAP-Pakete
            new ShopItem
            {
                Id = "golden_screws_50",
                Name = _localizationService.GetString("ShopGoldenScrews50Name"),
                Description = _localizationService.GetString("ShopGoldenScrews50Desc"),
                Icon = "ScrewFlatTop",
                Price = "0,99 €",
                IsPremiumItem = true
            },
            new ShopItem
            {
                Id = "golden_screws_150",
                Name = _localizationService.GetString("ShopGoldenScrews150Name"),
                Description = _localizationService.GetString("ShopGoldenScrews150Desc"),
                Icon = "ScrewFlatTop",
                Price = "2,49 €",
                IsPremiumItem = true
            },
            new ShopItem
            {
                Id = "golden_screws_450",
                Name = _localizationService.GetString("ShopGoldenScrews450Name"),
                Description = _localizationService.GetString("ShopGoldenScrews450Desc"),
                Icon = "ScrewFlatTop",
                Price = "4,99 €",
                IsPremiumItem = true
            },
            // ── Whale-Tier-Bundles (— Revenue-Hardcap durchbrechen) ──
            // Zielgruppe: Top-10%-Spieler, die aktuell maximal ~24 EUR ausgeben koennen.
            // Erwartung: ARPU +30-60% in der Whale-Cohort.
            new ShopItem
            {
                Id = "bundle_mid",
                Name = _localizationService.GetString("ShopBundleMidName") ?? "Imperium-Paket",
                Description = _localizationService.GetString("ShopBundleMidDesc") ?? "1500 Goldschrauben + 8h Speed-Boost",
                Icon = "PackageVariantClosed",
                Price = "9,99 €",
                IsPremiumItem = true
            },
            new ShopItem
            {
                Id = "bundle_big",
                Name = _localizationService.GetString("ShopBundleBigName") ?? "Meister-Paket",
                Description = _localizationService.GetString("ShopBundleBigDesc") ?? "4000 Goldschrauben + 48h Speed-Boost + 25 Mio. EUR",
                Icon = "TrophyVariant",
                Price = "19,99 €",
                IsPremiumItem = true
            },
            new ShopItem
            {
                Id = "bundle_mega",
                Name = _localizationService.GetString("ShopBundleMegaName") ?? "Legenden-Paket",
                Description = _localizationService.GetString("ShopBundleMegaDesc") ?? "12000 Goldschrauben + 7 Tage Speed-Boost + 200 Mio. EUR + Premium",
                Icon = "Crown",
                Price = "49,99 €",
                IsPremiumItem = true
            }
        ];
    }

    public void LoadTools()
    {
        var state = _gameStateService.State;

        // Tools initialisieren falls leer (alte Spielstaende)
        if (state.Tools.Count == 0)
            state.Tools = Tool.CreateDefaults();

        GoldenScrewsBalance = state.GoldenScrews.ToString("N0");

        var toolItems = new List<ToolDisplayItem>();
        foreach (var tool in state.Tools)
        {
            var name = _localizationService.GetString(tool.NameKey) ?? tool.NameKey;
            var effect = tool.Type == ToolType.Saw
                ? string.Format(_localizationService.GetString("ToolEffectZone") ?? "+{0}% target zone",
                    tool.CanUpgrade ? $"{(tool.ZoneBonus + 0.05) * 100:N0}" : $"{tool.ZoneBonus * 100:N0}")
                : string.Format(_localizationService.GetString("ToolEffectTime") ?? "+{0}s time bonus",
                    tool.CanUpgrade ? tool.TimeBonus + (tool.Level == 0 ? 5 : 2) : tool.TimeBonus);

            var iconKind = tool.Type switch
            {
                ToolType.Saw => "HandSaw",
                ToolType.PipeWrench => "Wrench",
                ToolType.Screwdriver => "Screwdriver",
                ToolType.Paintbrush => "FormatPaint",
                ToolType.Hammer => "Hammer",
                ToolType.SpiritLevel => "Draw",
                ToolType.Magnifier => "MagnifyPlus",
                ToolType.Compass => "Compass",
                _ => "HammerWrench"
            };

            toolItems.Add(new ToolDisplayItem
            {
                Type = tool.Type,
                Name = name,
                Level = tool.Level,
                LevelDisplay = $"Lv. {tool.Level}",
                UpgradeCostScrews = tool.UpgradeCostScrews,
                UpgradeCostDisplay = $"{tool.UpgradeCostScrews}",
                CanUpgrade = tool.CanUpgrade,
                CanAfford = _gameStateService.CanAffordGoldenScrews(tool.UpgradeCostScrews) && tool.CanUpgrade,
                EffectDescription = effect,
                IconKind = iconKind,
                IsMaxLevel = !tool.CanUpgrade
            });
        }

        Tools = toolItems;
    }

    /// <summary>
    /// Lädt die Equipment-Shop-Rotation (3-4 zufällige Ausrüstungsgegenstände).
    /// </summary>
    public void LoadEquipmentShop()
    {
        var items = _equipmentService.GetShopItems();

        var displayItems = new ObservableCollection<EquipmentShopItem>();
        foreach (var eq in items)
        {
            var name = _localizationService.GetString(eq.NameKey) ?? eq.NameKey;
            var iconKind = eq.Type switch
            {
                EquipmentType.Helmet => "HardHat",
                EquipmentType.Gloves => "HandWave",
                EquipmentType.Boots => "ShoeFormal",
                EquipmentType.Belt => "Wrench",
                _ => "Shield"
            };

            // Bonus-Beschreibung zusammensetzen
            var bonusParts = new List<string>();
            if (eq.EfficiencyBonus > 0)
                bonusParts.Add($"+{eq.EfficiencyBonus * 100m:F0}% Eff.");
            if (eq.FatigueReduction > 0)
                bonusParts.Add($"-{eq.FatigueReduction * 100m:F0}% Erm.");
            if (eq.MoodBonus > 0)
                bonusParts.Add($"+{eq.MoodBonus * 100m:F0}% Stim.");
            var bonusText = string.Join(", ", bonusParts);

            displayItems.Add(new EquipmentShopItem
            {
                Equipment = eq,
                Name = name,
                IconKind = iconKind,
                RarityColor = eq.RarityColor,
                BonusDescription = bonusText,
                PriceDisplay = eq.ShopPrice.ToString("N0"),
                CanAfford = _gameStateService.CanAffordGoldenScrews(eq.ShopPrice)
            });
        }

        EquipmentShopItems = displayItems;
        HasEquipmentShop = displayItems.Count > 0;
    }

    /// <summary>
    /// Aktualisiert nur CanAfford auf bestehenden Equipment-Items,
    /// ohne den Shop neu zu randomisieren.
    /// </summary>
    private void RefreshEquipmentCanAfford()
    {
        foreach (var item in EquipmentShopItems)
        {
            item.CanAfford = _gameStateService.CanAffordGoldenScrews(item.Equipment.ShopPrice);
        }
        // ObservableCollection Items haben kein INotifyPropertyChanged →
        // Collection neu zuweisen damit UI aktualisiert
        EquipmentShopItems = new ObservableCollection<EquipmentShopItem>(EquipmentShopItems);
    }

    [RelayCommand]
    private void BuyEquipment(EquipmentShopItem? item)
    {
        if (item?.Equipment == null) return;

        if (!_gameStateService.CanAffordGoldenScrews(item.Equipment.ShopPrice))
        {
            ShowAlert(
                _localizationService.GetString("NotEnoughScrews"),
                string.Format(_localizationService.GetString("NotEnoughScrewsDesc"), item.Equipment.ShopPrice),
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        _equipmentService.BuyEquipment(item.Equipment);

        var name = _localizationService.GetString(item.Equipment.NameKey) ?? item.Equipment.NameKey;
        ShowAlert(
            _localizationService.GetString("EquipmentTitle"),
            $"{name} ({_localizationService.GetString("EquipmentBonus")})",
            _localizationService.GetString("Great"));

        // Shop neu laden (Item entfernen, Balance aktualisieren)
        GoldenScrewsBalance = _gameStateService.State.GoldenScrews.ToString("N0");
        LoadEquipmentShop();
        LoadTools(); // Für CanAfford-Aktualisierung
    }

    [RelayCommand]
    private void UpgradeTool(ToolDisplayItem? item)
    {
        if (item == null || !item.CanUpgrade) return;

        var tool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == item.Type);
        if (tool == null) return;

        if (!_gameStateService.TrySpendGoldenScrews(tool.UpgradeCostScrews))
        {
            ShowAlert(
                _localizationService.GetString("NotEnoughScrews"),
                string.Format(_localizationService.GetString("NotEnoughScrewsDesc"), tool.UpgradeCostScrews),
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        tool.Level++;
        LoadTools();

        var name = _localizationService.GetString(tool.NameKey) ?? tool.NameKey;
        ShowAlert(
            _localizationService.GetString("ToolUpgrade") ?? "Upgrade",
            $"{name} → Lv. {tool.Level}",
            _localizationService.GetString("OK") ?? "OK");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    [RelayCommand]
    private async Task PurchaseItemAsync(ShopItem? item)
    {
        if (item == null) return;
        if (_isBusy) return;
        _isBusy = true;
        try
        {
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Telemetrie: Shop-Item angetippt (Conversion-Funnel-Einstieg).
        _analyticsService?.TrackEvent(Models.AnalyticsEvents.IapItemViewed, new Dictionary<string, object?>
        {
            ["item_id"] = item.Id,
            ["is_premium_item"] = item.IsPremiumItem,
            ["is_ad_reward"] = item.IsAdReward
        });

        if (item.IsPurchased)
        {
            ShowAlert(
                _localizationService.GetString("AlreadyPurchased"),
                _localizationService.GetString("AlreadyPurchasedDesc"),
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        if (item.IsAdReward)
        {
            if (_purchaseService.IsPremium)
            {
                // Premium: 3x pro Tag gratis (kein Video)
                var state = _gameStateService.State;
                // Tages-Reset prüfen
                if (state.LastPremiumAdRewardReset.Date < DateTime.UtcNow.Date)
                {
                    state.PremiumAdRewardsUsedToday = 0;
                    state.LastPremiumAdRewardReset = DateTime.UtcNow;
                }

                if (state.PremiumAdRewardsUsedToday >= 3)
                {
                    ShowAlert(
                        _localizationService.GetString("PremiumDailyLimitTitle") ?? "Daily Limit",
                        _localizationService.GetString("PremiumDailyLimitMessage") ?? "Premium rewards used up today. Come back tomorrow!",
                        _localizationService.GetString("OK") ?? "OK");
                    return;
                }

                state.PremiumAdRewardsUsedToday++;
                await ApplyReward(item);
                return;
            }

            // Free-User: Cooldown prüfen - GS-Ad hat eigenen 4h-Cooldown, Rest teilt 3h
            var gameState = _gameStateService.State;
            bool isGoldenScrewsAd = item.Id == "golden_screws_ad";
            var cooldownEnd = isGoldenScrewsAd
                ? gameState.LastGoldenScrewsAdTime.AddHours(4)
                : gameState.LastShopAdRewardTime.AddHours(3);

            if (DateTime.UtcNow < cooldownEnd)
            {
                var remaining = cooldownEnd - DateTime.UtcNow;
                ShowAlert(
                    _localizationService.GetString("AdCooldownTitle") ?? "Cooldown Active",
                    string.Format(
                        _localizationService.GetString("AdCooldownMessage") ?? "Next video in {0}h {1}min",
                        (int)remaining.TotalHours, remaining.Minutes),
                    _localizationService.GetString("OK") ?? "OK");
                return;
            }

            if (!_rewardedAdService.IsAvailable)
            {
                ShowAlert(
                    _localizationService.GetString("AdVideoNotAvailableTitle") ?? "No video available",
                    _localizationService.GetString("AdVideoNotAvailableMessage") ?? "No ad video is currently available. Please try again later.",
                    _localizationService.GetString("OK") ?? "OK");
                return;
            }

            var watchAd = await _dialogService.ShowConfirmDialog(
                item.Name,
                $"{item.Description}\n\n{_localizationService.GetString("WatchVideoQuestion")}",
                _localizationService.GetString("WatchVideo"),
                _localizationService.GetString("Cancel"));

            if (watchAd)
            {
                string placement = isGoldenScrewsAd ? "golden_screws" : "shop_reward";
                bool success = await _rewardedAdService.ShowAdAsync(placement);
                if (success)
                {
                    if (isGoldenScrewsAd)
                        gameState.LastGoldenScrewsAdTime = DateTime.UtcNow;
                    else
                        gameState.LastShopAdRewardTime = DateTime.UtcNow;
                    await ApplyReward(item);
                }
            }
        }
        else if (item.IsPremiumItem)
        {
            var confirm = await _dialogService.ShowConfirmDialog(
                item.Name,
                $"{item.Description}\n\n{_localizationService.GetString("Price")}: {item.Price}",
                _localizationService.GetString("Buy"),
                _localizationService.GetString("Cancel"));

            if (confirm)
            {
                _analyticsService?.TrackEvent(Models.AnalyticsEvents.IapPurchaseStarted, new Dictionary<string, object?>
                {
                    ["item_id"] = item.Id
                });

                bool success = false;

                if (item.Id == "premium")
                {
                    success = await _purchaseService.PurchaseRemoveAdsAsync();
                    if (success)
                    {
                        _gameStateService.State.IsPremium = true;
                        _gameStateService.State.InvalidateMaxOfflineHoursCache();
                        await _saveGameService.SaveAsync();
                        await _audioService.PlaySoundAsync(GameSound.LevelUp);
                        ShowAlert(
                            _localizationService.GetString("ThankYou"),
                            _localizationService.GetString("ThankYouPremiumDesc"),
                            _localizationService.GetString("Great"));
                        LoadShopData();
                    }
                }
                else if (item.Id == "booster_2x_2h")
                {
                    success = await _purchaseService.PurchaseConsumableAsync(item.Id);
                    if (success)
                    {
                        _gameStateService.State.SpeedBoostEndTime = DateTime.UtcNow.AddHours(2);
                        await _saveGameService.SaveAsync();
                        ShowAlert(
                            _localizationService.GetString("BoosterActivated"),
                            _localizationService.GetString("BoosterActivatedDesc"),
                            _localizationService.GetString("Great"));
                    }
                }
                else if (item.Id is "instant_cash_large" or "instant_cash_huge" or "instant_cash_mega")
                {
                    success = await _purchaseService.PurchaseConsumableAsync(item.Id);
                    if (success)
                    {
                        var cashAmount = GetInstantCashAmount(item.Id);
                        if (cashAmount > 0)
                        {
                            _gameStateService.AddMoney(cashAmount);
                            CurrentBalance = FormatMoney(_gameStateService.State.Money);
                            await _audioService.PlaySoundAsync(GameSound.MoneyEarned);
                            ShowAlert(
                                _localizationService.GetString("MoneyReceived"),
                                string.Format(_localizationService.GetString("MoneyReceivedFormat"), MoneyFormatter.FormatCompact(cashAmount)),
                                _localizationService.GetString("Great"));
                        }
                    }
                }
                else if (item.Id.StartsWith("golden_screws_"))
                {
                    success = await _purchaseService.PurchaseConsumableAsync(item.Id);
                    if (success)
                    {
                        int screwAmount = item.Id switch
                        {
                            "golden_screws_50" => 50,
                            "golden_screws_150" => 150,
                            "golden_screws_450" => 450,
                            _ => 0
                        };
                        if (screwAmount > 0)
                        {
                            _gameStateService.AddGoldenScrews(screwAmount, fromPurchase: true);
                            GoldenScrewsBalance = _gameStateService.State.GoldenScrews.ToString("N0");
                            await _audioService.PlaySoundAsync(GameSound.LevelUp);
                            ShowAlert(
                                _localizationService.GetString("GoldenScrews"),
                                string.Format(_localizationService.GetString("GoldenScrewsReceivedFormat"), screwAmount),
                                _localizationService.GetString("Great"));
                            LoadTools();
                        }
                    }
                }
                // ── Whale-Bundles (P0): Mid/Big/Mega-Tier (9,99/19,99/49,99 EUR) ──
                else if (item.Id is "bundle_mid" or "bundle_big" or "bundle_mega")
                {
                    success = await _purchaseService.PurchaseConsumableAsync(item.Id);
                    if (success)
                    {
                        var (gs, boostHours, cash, grantsPremium) = item.Id switch
                        {
                            "bundle_mid"  => (1500,   8m,         0m, false),
                            "bundle_big"  => (4000,  48m,  25_000_000m, false),
                            "bundle_mega" => (12000, 7m * 24m, 200_000_000m, true),
                            _ => (0, 0m, 0m, false)
                        };

                        if (gs > 0)
                            _gameStateService.AddGoldenScrews(gs, fromPurchase: true);
                        if (boostHours > 0)
                            _gameStateService.State.SpeedBoostEndTime = DateTime.UtcNow.AddHours((double)boostHours);
                        if (cash > 0)
                            _gameStateService.AddMoney(cash);
                        if (grantsPremium && !_gameStateService.State.IsPremium)
                        {
                            // SetPremiumStatus setzt den is_premium-Preference-Key — sonst waere das
                            // aus bundle_mega (49,99 EUR Consumable) erworbene Premium nach
                            // Reinstall/Geraetewechsel unwiederbringlich weg (Restore findet Consumables nicht).
                            _purchaseService.SetPremiumStatus(true);
                            _gameStateService.State.IsPremium = true;
                            _gameStateService.State.InvalidateMaxOfflineHoursCache();
                            _rewardedAdService.Disable();
                            OnPropertyChanged(nameof(ShowAds));
                        }

                        GoldenScrewsBalance = _gameStateService.State.GoldenScrews.ToString("N0");
                        CurrentBalance = FormatMoney(_gameStateService.State.Money);

                        await _audioService.PlaySoundAsync(GameSound.LevelUp);
                        ShowAlert(
                            _localizationService.GetString("ThankYou") ?? "Vielen Dank!",
                            _localizationService.GetString("BundleReceivedDesc")
                                ?? "Dein Paket wurde aktiviert. Viel Erfolg!",
                            _localizationService.GetString("Great") ?? "Super");

                        LoadShopData();
                        LoadTools();
                    }
                }

                // VIP-System: Echtgeld-Kauf registrieren
                if (success)
                {
                    // Sofort persistieren: Consumables sind bei Google bereits verbraucht, die
                    // Gutschrift haengt sonst bis zum naechsten AutoSave (<=30s) ungesichert im RAM
                    // — bei App-Kill/Crash in diesem Fenster ist der bezahlte Kauf verloren.
                    await _saveGameService.SaveAsync();
                    RecordVipPurchase(item.Id);
                    _analyticsService?.TrackEvent(Models.AnalyticsEvents.IapPurchaseSuccess, new Dictionary<string, object?>
                    {
                        ["item_id"] = item.Id
                    });
                }
                else
                {
                    _analyticsService?.TrackEvent(Models.AnalyticsEvents.IapPurchaseFailed, new Dictionary<string, object?>
                    {
                        ["item_id"] = item.Id
                    });
                }
            }
        }
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestorePurchasesAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            await _audioService.PlaySoundAsync(GameSound.ButtonTap);

            bool restored = await _purchaseService.RestorePurchasesAsync();

            if (restored)
            {
                _gameStateService.State.IsPremium = _purchaseService.IsPremium;
                _gameStateService.State.InvalidateMaxOfflineHoursCache();
                await _saveGameService.SaveAsync();
                await _audioService.PlaySoundAsync(GameSound.LevelUp);
                ShowAlert(
                    _localizationService.GetString("PurchasesRestored"),
                    _localizationService.GetString("PurchasesRestoredDesc"),
                    _localizationService.GetString("Great"));
                LoadShopData();
            }
            else
            {
                ShowAlert(
                    _localizationService.GetString("NoPurchasesFound"),
                    _localizationService.GetString("NoPurchasesFoundDesc"),
                    _localizationService.GetString("OK") ?? "OK");
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void ShowAlert(string title, string message, string buttonText)
    {
        _dialogService.ShowAlertDialog(title, message, buttonText);
    }

    /// <summary>
    /// Registriert einen Echtgeld-Kauf für das VIP-System.
    /// Preise basierend auf Item-ID.
    /// </summary>
    private void RecordVipPurchase(string itemId)
    {
        decimal amount = itemId switch
        {
            "premium" => 4.99m,
            "prestige_pass" => 2.99m,
            "battle_pass_season" => 1.99m,
            "booster_2x_2h" => 1.99m,
            "instant_cash_large" => 0.99m,
            "instant_cash_huge" => 2.49m,
            "instant_cash_mega" => 3.99m,
            "golden_screws_50" => 0.99m,
            "golden_screws_150" => 2.49m,
            "golden_screws_450" => 4.99m,
            "starter_pack" => 2.99m,
            // Whale-Bundles (P0): Top-VIP-Tier-Schub.
            "bundle_mid" => 9.99m,
            "bundle_big" => 19.99m,
            "bundle_mega" => 49.99m,
            _ => 0m
        };
        if (amount > 0)
            _vipService.RecordPurchase(amount);
    }

    private async Task ApplyReward(ShopItem item)
    {
        switch (item.Id)
        {
            case "booster_2x_30min":
                _gameStateService.State.SpeedBoostEndTime = DateTime.UtcNow.AddMinutes(30);
                ShowAlert(
                    _localizationService.GetString("BoosterActivated"),
                    _localizationService.GetString("BoosterActivatedDesc"),
                    _localizationService.GetString("Great"));
                break;

            case "instant_cash_small":
                var cashSmall = GetInstantCashAmount("instant_cash_small");
                _gameStateService.AddMoney(cashSmall);
                CurrentBalance = FormatMoney(_gameStateService.State.Money);
                await _audioService.PlaySoundAsync(GameSound.MoneyEarned);
                ShowAlert(
                    _localizationService.GetString("MoneyReceived"),
                    string.Format(_localizationService.GetString("MoneyReceivedFormat"), MoneyFormatter.FormatCompact(cashSmall)),
                    _localizationService.GetString("Great"));
                break;

            case "skip_time_1h":
                // BAL-AD-2: Echter Zeitsprung - 2h Netto-Einkommen + Worker-Erholung + Forschung
                var skipHours = 2m;
                var timeSkipEarnings = Math.Max(0m, _gameStateService.State.NetIncomePerSecond * 3600m * skipHours);
                _gameStateService.AddMoney(timeSkipEarnings);
                var skipState = _gameStateService.State;
                foreach (var ws in skipState.Workshops)
                    foreach (var worker in ws.Workers)
                    {
                        if (worker.IsResting)
                        {
                            worker.Fatigue = Math.Max(0m, worker.Fatigue - 25m * skipHours);
                            worker.Mood = Math.Min(100m, worker.Mood + 5m * skipHours);
                        }
                        else
                            worker.Mood = Math.Min(100m, worker.Mood + 3m * skipHours);
                    }
                if (skipState.ActiveResearchId != null)
                {
                    var research = skipState.Researches.FirstOrDefault(r => r.Id == skipState.ActiveResearchId);
                    if (research?.StartedAt != null)
                        research.BonusSeconds += (double)(skipHours * 3600m);
                }
                CurrentBalance = FormatMoney(skipState.Money);
                await _audioService.PlaySoundAsync(GameSound.MoneyEarned);
                ShowAlert(
                    _localizationService.GetString("TimeSkipped"),
                    string.Format(
                        _localizationService.GetString("TimeSkipFullDesc") ?? "+{0} + Recovery & research accelerated",
                        FormatMoney(timeSkipEarnings)),
                    _localizationService.GetString("Great"));
                break;

            case "golden_screws_ad":
                // v2.0.37: 8 → 12 GS (Goldschrauben-Oekonomie-Anpassung).
                // F2P-Vollabschluss-Zeit reduziert sich um ~30% bei Spielern, die Werbung schauen.
                _gameStateService.AddGoldenScrews(12);
                GoldenScrewsBalance = _gameStateService.State.GoldenScrews.ToString("N0");
                await _audioService.PlaySoundAsync(GameSound.MoneyEarned);
                ShowAlert(
                    _localizationService.GetString("GoldenScrews"),
                    string.Format(_localizationService.GetString("GoldenScrewsReceivedFormat"), 12),
                    _localizationService.GetString("Great"));
                break;
        }

        await _saveGameService.SaveAsync();
    }

    /// <summary>
    /// Berechnet den Instant-Cash-Betrag basierend auf stündlichem Einkommen.
    /// Mindestens Level-basierter Fallback für Spieler ohne Workshops.
    /// </summary>
    private decimal GetInstantCashAmount(string itemId)
    {
        var state = _gameStateService.State;
        // Basis: Stündliches Brutto-Einkommen (oder Fallback auf Level * 100)
        var hourlyIncome = Math.Max(state.TotalIncomePerSecond * 3600m, state.PlayerLevel * 100m);

        return itemId switch
        {
            "instant_cash_small" => Math.Max(500m, hourlyIncome * 4m),      // ~4h Einkommen (Video-Ad)
            "instant_cash_large" => Math.Max(2_000m, hourlyIncome * 8m),    // ~8h Einkommen (0,99€)
            "instant_cash_huge" => Math.Max(10_000m, hourlyIncome * 24m),   // ~24h Einkommen (2,49€)
            "instant_cash_mega" => Math.Max(50_000m, hourlyIncome * 48m),   // ~48h Einkommen (3,99€)
            _ => 0m
        };
    }

    /// <summary>
    /// Berechnet den Premium-Vergleich für Nicht-Premium-Spieler.
    /// Zeile 1: Aktuelles Netto-Einkommen und Wert mit Premium (+50%).
    /// Zeile 2: Goldschrauben-Verdopplung (ECON-2 20.04.2026: war im Compare unsichtbar,
    /// ist aber der psychologisch staerkste Kaufgrund fuer Mid-Game-Spieler mit Rebirth-Ziel).
    /// </summary>
    private void UpdatePremiumIncomeComparison()
    {
        if (IsPremium)
        {
            PremiumIncomeComparison = string.Empty;
            return;
        }

        var netIncome = _gameStateService.State.NetIncomePerSecond;
        var premiumIncome = netIncome * 1.5m;
        var currentFormatted = MoneyFormatter.FormatPerSecond(netIncome);
        var premiumFormatted = MoneyFormatter.FormatPerSecond(premiumIncome);

        var incomeTemplate = _localizationService.GetString("PremiumIncomeCompare")
                       ?? "Your income: {0} \u2192 With Premium: {1}";
        var gsBenefit = _localizationService.GetString("PremiumBenefitGoldenScrews")
                       ?? "+100% Golden Screws from Mini-Games";

        PremiumIncomeComparison = string.Format(incomeTemplate, currentFormatted, premiumFormatted)
                                  + "\n" + gsBenefit;
    }

    private static string FormatMoney(decimal amount) => MoneyFormatter.Format(amount, 2);

    private void OnMoneyChanged(object? sender, MoneyChangedEventArgs e)
    {
        CurrentBalance = FormatMoney(e.NewAmount);
        // Einkommensvergleich aktualisieren (NetIncome kann sich geändert haben)
        UpdatePremiumIncomeComparison();
    }

    private void OnGoldenScrewsChanged(object? sender, GoldenScrewsChangedEventArgs e)
    {
        GoldenScrewsBalance = e.NewAmount.ToString("N0");
        // CanAfford in Tools aktualisieren (neu generieren, da CanAfford statisch ist)
        LoadTools();
        // Equipment-Shop: Nur CanAfford aktualisieren, NICHT neu generieren
        RefreshEquipmentCanAfford();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _purchaseService.PremiumStatusChanged -= OnPremiumStatusChanged;
        if (_dailyBundleService != null)
            _dailyBundleService.BundleRotated -= OnDailyBundleRotated;
        _gameStateService.MoneyChanged -= OnMoneyChanged;
        _gameStateService.GoldenScrewsChanged -= OnGoldenScrewsChanged;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Represents an item in the shop.
/// </summary>
public class ShopItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Price { get; set; } = "";
    public bool IsPremiumItem { get; set; }
    public bool IsAdReward { get; set; }
    public bool IsPurchased { get; set; }
}

/// <summary>
/// Display-Model fuer Werkzeuge im Shop.
/// </summary>
public class ToolDisplayItem
{
    public ToolType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public string LevelDisplay { get; set; } = string.Empty;
    public int UpgradeCostScrews { get; set; }
    public string UpgradeCostDisplay { get; set; } = string.Empty;
    public bool CanUpgrade { get; set; }
    public bool CanAfford { get; set; }
    public string EffectDescription { get; set; } = string.Empty;
    public string IconKind { get; set; } = string.Empty;
    public bool IsMaxLevel { get; set; }
}

/// <summary>
/// Display-Model für Ausrüstung im Shop.
/// </summary>
public class EquipmentShopItem
{
    public Equipment Equipment { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string IconKind { get; set; } = string.Empty;
    public string RarityColor { get; set; } = "#9E9E9E";
    public string BonusDescription { get; set; } = string.Empty;
    public string PriceDisplay { get; set; } = "0";
    public bool CanAfford { get; set; }
}
