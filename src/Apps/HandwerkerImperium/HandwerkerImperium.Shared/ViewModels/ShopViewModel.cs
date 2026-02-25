using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the shop page.
/// Manages in-app purchases and premium features.
/// </summary>
public partial class ShopViewModel : ObservableObject, IDisposable
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

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Event to show an alert dialog. Parameters: title, message, buttonText.
    /// </summary>
    public event Action<string, string, string>? AlertRequested;

    /// <summary>
    /// Event to request a confirmation dialog.
    /// Parameters: title, message, acceptText, cancelText. Returns bool.
    /// </summary>
    public event Func<string, string, string, string, Task<bool>>? ConfirmationRequested;

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
    /// Indicates whether ads should be shown (not premium).
    /// </summary>
    public bool ShowAds => !IsPremium;

    /// <summary>
    /// Localized text for restore purchases button.
    /// </summary>
    public string RestorePurchasesText => _localizationService.GetString("RestorePurchases") ?? "Käufe wiederherstellen";

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public ShopViewModel(
        IGameStateService gameStateService,
        IAudioService audioService,
        ISaveGameService saveGameService,
        IRewardedAdService rewardedAdService,
        IPurchaseService purchaseService,
        ILocalizationService localizationService,
        IEquipmentService equipmentService)
    {
        _gameStateService = gameStateService;
        _audioService = audioService;
        _saveGameService = saveGameService;
        _rewardedAdService = rewardedAdService;
        _purchaseService = purchaseService;
        _localizationService = localizationService;
        _equipmentService = equipmentService;

        // Subscribe to premium status changes
        _purchaseService.PremiumStatusChanged += OnPremiumStatusChanged;

        // Geld- und Goldschrauben-Anzeige live aktualisieren
        _gameStateService.MoneyChanged += OnMoneyChanged;
        _gameStateService.GoldenScrewsChanged += OnGoldenScrewsChanged;

        LoadShopData();
        LoadTools();
        LoadEquipmentShop();
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

        // Create shop items with localized texts
        ShopItems =
        [
            new ShopItem
            {
                Id = "premium",
                Name = _localizationService.GetString("ShopPremiumName"),
                Description = _localizationService.GetString("ShopPremiumDesc"),
                Icon = "Star",
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
                Name = _localizationService.GetString("ShopSkipTimeName"),
                Description = _localizationService.GetString("ShopSkipTimeDesc"),
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
                Icon = "Screwdriver",
                Price = _localizationService.GetString("WatchVideo"),
                IsAdReward = true
            },
            // Goldschrauben: IAP-Pakete
            new ShopItem
            {
                Id = "golden_screws_50",
                Name = _localizationService.GetString("ShopGoldenScrews50Name"),
                Description = _localizationService.GetString("ShopGoldenScrews50Desc"),
                Icon = "Screwdriver",
                Price = "0,99 €",
                IsPremiumItem = true
            },
            new ShopItem
            {
                Id = "golden_screws_150",
                Name = _localizationService.GetString("ShopGoldenScrews150Name"),
                Description = _localizationService.GetString("ShopGoldenScrews150Desc"),
                Icon = "Screwdriver",
                Price = "2,49 €",
                IsPremiumItem = true
            },
            new ShopItem
            {
                Id = "golden_screws_450",
                Name = _localizationService.GetString("ShopGoldenScrews450Name"),
                Description = _localizationService.GetString("ShopGoldenScrews450Desc"),
                Icon = "Screwdriver",
                Price = "4,99 €",
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
                ToolType.Saw => "Saw",
                ToolType.PipeWrench => "Pipe",
                ToolType.Screwdriver => "Screwdriver",
                ToolType.Paintbrush => "Brush",
                _ => "Wrench"
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
                PriceDisplay = eq.ShopPrice.ToString(),
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
        _gameStateService.MarkDirty();
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
                        _localizationService.GetString("PremiumDailyLimitTitle") ?? "Tageslimit",
                        _localizationService.GetString("PremiumDailyLimitMessage") ?? "Premium-Belohnungen für heute aufgebraucht.",
                        _localizationService.GetString("OK") ?? "OK");
                    return;
                }

                state.PremiumAdRewardsUsedToday++;
                _gameStateService.MarkDirty();
                await ApplyReward(item);
                return;
            }

            // Free-User: Cooldown prüfen (1x, dann 3h Cooldown)
            var gameState = _gameStateService.State;
            var cooldownEnd = gameState.LastShopAdRewardTime.AddHours(3);
            if (DateTime.UtcNow < cooldownEnd)
            {
                var remaining = cooldownEnd - DateTime.UtcNow;
                ShowAlert(
                    _localizationService.GetString("AdCooldownTitle") ?? "Wartezeit aktiv",
                    string.Format(
                        _localizationService.GetString("AdCooldownMessage") ?? "Nächstes Video in {0}h {1}min",
                        (int)remaining.TotalHours, remaining.Minutes),
                    _localizationService.GetString("OK") ?? "OK");
                return;
            }

            if (!_rewardedAdService.IsAvailable)
            {
                ShowAlert(
                    _localizationService.GetString("AdVideoNotAvailableTitle") ?? "Video nicht verfügbar",
                    _localizationService.GetString("AdVideoNotAvailableMessage") ?? "Bitte versuche es später.",
                    _localizationService.GetString("OK") ?? "OK");
                return;
            }

            bool watchAd = false;
            if (ConfirmationRequested != null)
            {
                watchAd = await ConfirmationRequested.Invoke(
                    item.Name,
                    $"{item.Description}\n\n{_localizationService.GetString("WatchVideoQuestion")}",
                    _localizationService.GetString("WatchVideo"),
                    _localizationService.GetString("Cancel"));
            }
            else
            {
                watchAd = true;
            }

            if (watchAd)
            {
                bool success = await _rewardedAdService.ShowAdAsync("golden_screws");
                if (success)
                {
                    gameState.LastShopAdRewardTime = DateTime.UtcNow;
                    _gameStateService.MarkDirty();
                    await ApplyReward(item);
                }
            }
        }
        else if (item.IsPremiumItem)
        {
            bool confirm = false;
            if (ConfirmationRequested != null)
            {
                confirm = await ConfirmationRequested.Invoke(
                    item.Name,
                    $"{item.Description}\n\n{_localizationService.GetString("Price")}: {item.Price}",
                    _localizationService.GetString("Buy"),
                    _localizationService.GetString("Cancel"));
            }
            else
            {
                confirm = true;
            }

            if (confirm)
            {
                bool success = false;

                if (item.Id == "premium")
                {
                    success = await _purchaseService.PurchaseRemoveAdsAsync();
                    if (success)
                    {
                        _gameStateService.State.IsPremium = true;
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
                            _gameStateService.AddGoldenScrews(screwAmount);
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
        AlertRequested?.Invoke(title, message, buttonText);
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
                // Netto-Einkommen statt Brutto (Kosten abziehen)
                var hourlyEarnings = Math.Max(0m, _gameStateService.State.NetIncomePerSecond * 3600);
                _gameStateService.AddMoney(hourlyEarnings);
                CurrentBalance = FormatMoney(_gameStateService.State.Money);
                await _audioService.PlaySoundAsync(GameSound.MoneyEarned);
                ShowAlert(
                    _localizationService.GetString("TimeSkipped"),
                    string.Format(_localizationService.GetString("MoneyReceivedFormat"), FormatMoney(hourlyEarnings)),
                    _localizationService.GetString("Great"));
                break;

            case "golden_screws_ad":
                _gameStateService.AddGoldenScrews(5);
                GoldenScrewsBalance = _gameStateService.State.GoldenScrews.ToString("N0");
                await _audioService.PlaySoundAsync(GameSound.MoneyEarned);
                ShowAlert(
                    _localizationService.GetString("GoldenScrews"),
                    string.Format(_localizationService.GetString("GoldenScrewsReceivedFormat"), 5),
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

    private static string FormatMoney(decimal amount) => MoneyFormatter.Format(amount, 2);

    private void OnMoneyChanged(object? sender, MoneyChangedEventArgs e)
    {
        CurrentBalance = FormatMoney(e.NewAmount);
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
