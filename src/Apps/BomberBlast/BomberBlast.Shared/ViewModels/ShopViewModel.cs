using System.Collections.ObjectModel;
using Avalonia.Media;
using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Levels;
using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel fuer den Shop - zeigt Upgrades, PowerUp-Übersicht, Skins und Coin-Stand.
/// Implementiert IDisposable fuer BalanceChanged-Unsubscription.
/// </summary>
public partial class ShopViewModel : ObservableObject, INavigable, IDisposable
{
    private readonly IShopService _shopService;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ILocalizationService _localizationService;
    private readonly IProgressService _progressService;
    private readonly ICustomizationService _customizationService;
    private readonly IPurchaseService _purchaseService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly MeineApps.Core.Ava.Services.IPreferencesService _preferencesService;
    private readonly IRotatingDealsService _rotatingDealsService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<NavigationRequest>? NavigationRequested;
    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;

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
        IRotatingDealsService rotatingDealsService)
    {
        _shopService = shopService;
        _coinService = coinService;
        _gemService = gemService;
        _localizationService = localizationService;
        _progressService = progressService;
        _customizationService = customizationService;
        _purchaseService = purchaseService;
        _rewardedAdService = rewardedAdService;
        _preferencesService = preferencesService;
        _rotatingDealsService = rotatingDealsService;

        _coinService.BalanceChanged += OnBalanceChanged;
        _gemService.BalanceChanged += OnBalanceChanged;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
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
            item.Refresh(_coinService.Balance, _gemService.Balance);
        }

        ShopItems = new ObservableCollection<ShopDisplayItem>(items);
    }

    private void RefreshPowerUpItems()
    {
        var items = new List<PowerUpDisplayItem>();
        foreach (PowerUpType type in Enum.GetValues<PowerUpType>())
        {
            items.Add(CreateDisplayItem(
                "powerup_" + type.ToString().ToLower(),
                $"PowerUp_{type}",
                type.GetUnlockLevel(),
                GetPowerUpIcon(type),
                GetPowerUpAvaloniaColor(type)));
        }
        PowerUpItems = new ObservableCollection<PowerUpDisplayItem>(items);
    }

    private void RefreshMechanicItems()
    {
        var mechanics = new[] { WorldMechanic.Ice, WorldMechanic.Conveyor, WorldMechanic.Teleporter, WorldMechanic.LavaCrack };
        var items = new List<PowerUpDisplayItem>();
        foreach (var mech in mechanics)
        {
            items.Add(CreateDisplayItem(
                "mechanic_" + mech.ToString().ToLower(),
                $"Mechanic_{mech}",
                mech.GetUnlockLevel(),
                GetMechanicIcon(mech),
                GetMechanicColor(mech)));
        }
        MechanicItems = new ObservableCollection<PowerUpDisplayItem>(items);
    }

    /// <summary>Erstellt ein PowerUpDisplayItem mit Unlock-Status-Logik</summary>
    private PowerUpDisplayItem CreateDisplayItem(string id, string nameKey, int unlockLevel,
        MaterialIconKind icon, Color color)
    {
        int highest = _progressService.HighestCompletedLevel;
        bool isUnlocked = highest >= unlockLevel || unlockLevel <= 1;
        var unlockedFormat = _localizationService.GetString("UnlockedAt") ?? "From Level {0}";
        var unlockedText = _localizationService.GetString("Unlocked") ?? "Unlocked";

        return new PowerUpDisplayItem
        {
            Id = id,
            DisplayName = _localizationService.GetString(nameKey) ?? nameKey,
            DisplayDescription = isUnlocked
                ? (_localizationService.GetString(nameKey + "_Desc") ?? "")
                : "???",
            IconKind = icon,
            IconColor = isUnlocked ? color : Color.Parse("#666666"),
            UnlockLevel = unlockLevel,
            IsUnlocked = isUnlocked,
            UnlockText = isUnlocked ? unlockedText : string.Format(unlockedFormat, unlockLevel)
        };
    }

    private void RefreshSkinItems()
    {
        var currentSkin = _customizationService.PlayerSkin;
        bool isPremium = _purchaseService.IsPremium;
        var equippedText = _localizationService.GetString("SkinEquipped") ?? "Equipped";
        var lockedText = _localizationService.GetString("SkinLocked") ?? _localizationService.GetString("SkinPremiumOnly") ?? "Premium Only";
        var selectText = _localizationService.GetString("SkinSelect") ?? "Select";

        var items = new List<SkinDisplayItem>();
        foreach (var skin in _customizationService.AvailablePlayerSkins)
        {
            bool isEquipped = skin.Id == currentSkin.Id;
            bool isPremiumLocked = skin.IsPremiumOnly && !isPremium;
            bool isOwned = _customizationService.IsPlayerSkinOwned(skin.Id);

            items.Add(new SkinDisplayItem
            {
                Id = skin.Id,
                Category = SkinCategory.Player,
                PreviewIconKind = MaterialIconKind.Account,
                DisplayName = _localizationService.GetString(skin.NameKey) ?? skin.Id,
                PrimaryColor = Color.FromRgb(skin.PrimaryColor.Red, skin.PrimaryColor.Green, skin.PrimaryColor.Blue),
                SecondaryColor = Color.FromRgb(skin.SecondaryColor.Red, skin.SecondaryColor.Green, skin.SecondaryColor.Blue),
                IsPremiumOnly = skin.IsPremiumOnly,
                HasGlow = skin.GlowColor.HasValue,
                CoinPrice = skin.CoinPrice,
                IsOwned = isOwned,
                IsEquipped = isEquipped,
                IsLocked = isPremiumLocked,
                StatusText = isEquipped ? equippedText : (isPremiumLocked ? lockedText : (isOwned ? selectText : ""))
            });
        }
        SkinItems = new ObservableCollection<SkinDisplayItem>(items);
    }

    private void RefreshBombSkinItems()
    {
        var currentSkin = _customizationService.BombSkin;
        bool isPremium = _purchaseService.IsPremium;
        var equippedText = _localizationService.GetString("SkinEquipped") ?? "Equipped";
        var lockedText = _localizationService.GetString("SkinPremiumOnly") ?? "Premium Only";
        var selectText = _localizationService.GetString("SkinSelect") ?? "Select";

        var items = new List<SkinDisplayItem>();
        foreach (var skin in _customizationService.AvailableBombSkins)
        {
            bool isEquipped = skin.Id == currentSkin.Id;
            bool isOwned = _customizationService.IsBombSkinOwned(skin.Id);
            bool isPremiumLocked = skin.IsPremiumOnly && !isPremium;

            items.Add(new SkinDisplayItem
            {
                Id = skin.Id,
                Category = SkinCategory.Bomb,
                PreviewIconKind = MaterialIconKind.Bomb,
                DisplayName = _localizationService.GetString(skin.NameKey) ?? skin.Id,
                PrimaryColor = skin.BodyColor == SkiaSharp.SKColor.Empty
                    ? Color.Parse("#444444")
                    : Color.FromRgb(skin.BodyColor.Red, skin.BodyColor.Green, skin.BodyColor.Blue),
                SecondaryColor = skin.GlowColor == SkiaSharp.SKColor.Empty
                    ? Color.Parse("#FF6600")
                    : Color.FromRgb(skin.GlowColor.Red, skin.GlowColor.Green, skin.GlowColor.Blue),
                IsPremiumOnly = skin.IsPremiumOnly,
                CoinPrice = skin.CoinPrice,
                IsOwned = isOwned,
                IsEquipped = isEquipped,
                IsLocked = isPremiumLocked,
                StatusText = isEquipped ? equippedText : (isPremiumLocked ? lockedText : (isOwned ? selectText : ""))
            });
        }
        BombSkinItems = new ObservableCollection<SkinDisplayItem>(items);
    }

    private void RefreshExplosionSkinItems()
    {
        var currentSkin = _customizationService.ExplosionSkin;
        bool isPremium = _purchaseService.IsPremium;
        var equippedText = _localizationService.GetString("SkinEquipped") ?? "Equipped";
        var lockedText = _localizationService.GetString("SkinPremiumOnly") ?? "Premium Only";
        var selectText = _localizationService.GetString("SkinSelect") ?? "Select";

        var items = new List<SkinDisplayItem>();
        foreach (var skin in _customizationService.AvailableExplosionSkins)
        {
            bool isEquipped = skin.Id == currentSkin.Id;
            bool isOwned = _customizationService.IsExplosionSkinOwned(skin.Id);
            bool isPremiumLocked = skin.IsPremiumOnly && !isPremium;

            items.Add(new SkinDisplayItem
            {
                Id = skin.Id,
                Category = SkinCategory.Explosion,
                PreviewIconKind = MaterialIconKind.Fire,
                DisplayName = _localizationService.GetString(skin.NameKey) ?? skin.Id,
                PrimaryColor = skin.OuterColor == SkiaSharp.SKColor.Empty
                    ? Color.Parse("#FF6600")
                    : Color.FromRgb(skin.OuterColor.Red, skin.OuterColor.Green, skin.OuterColor.Blue),
                SecondaryColor = skin.CoreColor == SkiaSharp.SKColor.Empty
                    ? Color.Parse("#FFFF00")
                    : Color.FromRgb(skin.CoreColor.Red, skin.CoreColor.Green, skin.CoreColor.Blue),
                IsPremiumOnly = skin.IsPremiumOnly,
                CoinPrice = skin.CoinPrice,
                IsOwned = isOwned,
                IsEquipped = isEquipped,
                IsLocked = isPremiumLocked,
                StatusText = isEquipped ? equippedText : (isPremiumLocked ? lockedText : (isOwned ? selectText : ""))
            });
        }
        ExplosionSkinItems = new ObservableCollection<SkinDisplayItem>(items);
    }

    private void RefreshTrailItems()
    {
        var activeTrail = _customizationService.ActiveTrail;
        var equippedText = _localizationService.GetString("SkinEquipped") ?? "Equipped";
        var selectText = _localizationService.GetString("SkinSelect") ?? "Select";
        var noneText = _localizationService.GetString("TrailNone") ?? "None";

        var items = new List<SkinDisplayItem>();
        // "Kein Trail" Option
        items.Add(new SkinDisplayItem
        {
            Id = "",
            Category = SkinCategory.Trail,
            PreviewIconKind = MaterialIconKind.WavesArrowRight,
            DisplayName = noneText,
            PrimaryColor = Color.Parse("#888888"),
            SecondaryColor = Color.Parse("#666666"),
            CoinPrice = 0,
            IsOwned = true,
            IsEquipped = activeTrail == null,
            IsLocked = false,
            StatusText = activeTrail == null ? equippedText : selectText
        });

        foreach (var trail in _customizationService.AvailableTrails)
        {
            bool isEquipped = activeTrail?.Id == trail.Id;
            bool isOwned = _customizationService.IsTrailOwned(trail.Id);

            items.Add(new SkinDisplayItem
            {
                Id = trail.Id,
                Category = SkinCategory.Trail,
                PreviewIconKind = MaterialIconKind.WavesArrowRight,
                DisplayName = _localizationService.GetString(trail.NameKey) ?? trail.Id,
                PrimaryColor = Color.FromRgb(trail.PrimaryColor.Red, trail.PrimaryColor.Green, trail.PrimaryColor.Blue),
                SecondaryColor = Color.FromRgb(trail.SecondaryColor.Red, trail.SecondaryColor.Green, trail.SecondaryColor.Blue),
                CoinPrice = trail.CoinPrice,
                IsOwned = isOwned,
                IsEquipped = isEquipped,
                IsLocked = false,
                StatusText = isEquipped ? equippedText : (isOwned ? selectText : "")
            });
        }
        TrailItems = new ObservableCollection<SkinDisplayItem>(items);
    }

    private void RefreshVictoryItems()
    {
        var activeVictory = _customizationService.ActiveVictory;
        var equippedText = _localizationService.GetString("SkinEquipped") ?? "Equipped";
        var selectText = _localizationService.GetString("SkinSelect") ?? "Select";
        var noneText = _localizationService.GetString("VictoryNone") ?? "Standard";

        var items = new List<SkinDisplayItem>();
        // "Standard" Option
        items.Add(new SkinDisplayItem
        {
            Id = "",
            Category = SkinCategory.Victory,
            PreviewIconKind = MaterialIconKind.PartyPopper,
            DisplayName = noneText,
            PrimaryColor = Color.Parse("#888888"),
            SecondaryColor = Color.Parse("#666666"),
            CoinPrice = 0,
            IsOwned = true,
            IsEquipped = activeVictory == null,
            IsLocked = false,
            StatusText = activeVictory == null ? equippedText : selectText
        });

        foreach (var victory in _customizationService.AvailableVictories)
        {
            bool isEquipped = activeVictory?.Id == victory.Id;
            bool isOwned = _customizationService.IsVictoryOwned(victory.Id);
            // VictoryDefinition hat keine PrimaryColor/SecondaryColor → Raritätsfarben verwenden
            var rarityColor = victory.Rarity.GetColor();
            var rarityGlow = victory.Rarity.GetGlowColor();

            items.Add(new SkinDisplayItem
            {
                Id = victory.Id,
                Category = SkinCategory.Victory,
                PreviewIconKind = MaterialIconKind.PartyPopper,
                DisplayName = _localizationService.GetString(victory.NameKey) ?? victory.Id,
                PrimaryColor = Color.FromRgb(rarityColor.Red, rarityColor.Green, rarityColor.Blue),
                SecondaryColor = Color.FromRgb(rarityGlow.Red, rarityGlow.Green, rarityGlow.Blue),
                CoinPrice = victory.CoinPrice,
                IsOwned = isOwned,
                IsEquipped = isEquipped,
                IsLocked = false,
                StatusText = isEquipped ? equippedText : (isOwned ? selectText : "")
            });
        }
        VictoryItems = new ObservableCollection<SkinDisplayItem>(items);
    }

    private void RefreshFrameItems()
    {
        var activeFrame = _customizationService.ActiveFrame;
        var equippedText = _localizationService.GetString("SkinEquipped") ?? "Equipped";
        var selectText = _localizationService.GetString("SkinSelect") ?? "Select";
        var noneText = _localizationService.GetString("FrameNone") ?? "None";

        var items = new List<SkinDisplayItem>();
        // "Kein Rahmen" Option
        items.Add(new SkinDisplayItem
        {
            Id = "",
            Category = SkinCategory.Frame,
            PreviewIconKind = MaterialIconKind.CardAccountDetailsOutline,
            DisplayName = noneText,
            PrimaryColor = Color.Parse("#888888"),
            SecondaryColor = Color.Parse("#666666"),
            CoinPrice = 0,
            IsOwned = true,
            IsEquipped = activeFrame == null,
            IsLocked = false,
            StatusText = activeFrame == null ? equippedText : selectText
        });

        foreach (var frame in _customizationService.AvailableFrames)
        {
            bool isEquipped = activeFrame?.Id == frame.Id;
            bool isOwned = _customizationService.IsFrameOwned(frame.Id);

            items.Add(new SkinDisplayItem
            {
                Id = frame.Id,
                Category = SkinCategory.Frame,
                PreviewIconKind = MaterialIconKind.CardAccountDetailsOutline,
                DisplayName = _localizationService.GetString(frame.NameKey) ?? frame.Id,
                PrimaryColor = Color.FromRgb(frame.PrimaryColor.Red, frame.PrimaryColor.Green, frame.PrimaryColor.Blue),
                SecondaryColor = Color.FromRgb(frame.SecondaryColor.Red, frame.SecondaryColor.Green, frame.SecondaryColor.Blue),
                CoinPrice = frame.CoinPrice,
                IsOwned = isOwned,
                IsEquipped = isEquipped,
                IsLocked = false,
                StatusText = isEquipped ? equippedText : (isOwned ? selectText : "")
            });
        }
        FrameItems = new ObservableCollection<SkinDisplayItem>(items);
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
            item.Refresh(_coinService.Balance, _gemService.Balance);
        }
        // Skin-Collections NICHT neu erstellen: Kein Skin-Property hängt vom Balance ab.
        // CanBuy = !IsOwned && !IsLocked && CoinPrice > 0 (balance-unabhängig).
        // Rebuild passiert nur bei Kauf (PurchaseSkinAsync) oder Auswahl (SelectSkin).
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task PurchaseAsync(ShopDisplayItem? item)
    {
        if (item == null || item.IsMaxed) return;

        // Gratis-Upgrade per Ad: Coins werden nicht abgezogen
        if (FreeUpgradeActive)
        {
            if (_shopService.TryPurchaseFree(item.Type))
            {
                var upgradeName = _localizationService.GetString(item.NameKey);
                PurchaseSucceeded?.Invoke(upgradeName ?? item.NameKey);
                FreeUpgradeActive = false;
                RefreshItems();
                UpdateCoinDisplay();
            }
            return;
        }

        if (!_coinService.CanAfford(item.NextPrice))
        {
            // Detailliertes Fehler-Feedback: Wie viele Coins fehlen
            var detail = string.Format(
                _localizationService.GetString("PurchaseFailedDetail") ?? "Requires {0} Coins, you have {1}",
                item.NextPrice.ToString("N0"), _coinService.Balance.ToString("N0"));
            MessageRequested?.Invoke(
                _localizationService.GetString("PurchaseFailed") ?? "Purchase Failed",
                detail);
            InsufficientFunds?.Invoke();
            return;
        }

        // Bestätigungsdialog bei teureren Käufen (ab 3000 Coins)
        if (item.NextPrice >= 3000 && ConfirmationRequested != null)
        {
            var upgradeName = _localizationService.GetString(item.NameKey) ?? item.NameKey;
            var msg = string.Format(
                _localizationService.GetString("ConfirmPurchaseMessage") ?? "Spend {0} Coins on {1}?",
                item.NextPrice.ToString("N0"), upgradeName);
            var confirmed = await ConfirmationRequested.Invoke(
                _localizationService.GetString("ConfirmPurchaseTitle") ?? "Confirm Purchase",
                msg,
                _localizationService.GetString("Buy") ?? "Buy",
                _localizationService.GetString("Cancel"));
            if (!confirmed) return;
        }

        if (_shopService.TryPurchase(item.Type))
        {
            var upgradeName = _localizationService.GetString(item.NameKey);
            PurchaseSucceeded?.Invoke(upgradeName ?? item.NameKey);
            RefreshItems();
            UpdateCoinDisplay();
        }
    }

    /// <summary>
    /// Upgrade mit Gems kaufen (Alternative zu Coins, ab Level 2).
    /// </summary>
    [RelayCommand]
    private async Task PurchaseWithGemsAsync(ShopDisplayItem? item)
    {
        if (item == null || item.IsMaxed || item.GemPrice <= 0) return;

        if (!_gemService.CanAfford(item.GemPrice))
        {
            var detail = string.Format(
                _localizationService.GetString("PurchaseFailedDetailGems") ?? "Requires {0} Gems, you have {1}",
                item.GemPrice, _gemService.Balance);
            MessageRequested?.Invoke(
                _localizationService.GetString("PurchaseFailed") ?? "Purchase Failed",
                detail);
            InsufficientFunds?.Invoke();
            return;
        }

        // Bestätigungsdialog bei teureren Gem-Käufen (ab 30 Gems)
        if (item.GemPrice >= 30 && ConfirmationRequested != null)
        {
            var upgradeName = _localizationService.GetString(item.NameKey) ?? item.NameKey;
            var msg = string.Format(
                _localizationService.GetString("ConfirmGemPurchaseMessage") ?? "Spend {0} Gems on {1}?",
                item.GemPrice, upgradeName);
            var confirmed = await ConfirmationRequested.Invoke(
                _localizationService.GetString("ConfirmPurchaseTitle") ?? "Confirm Purchase",
                msg,
                _localizationService.GetString("Buy") ?? "Buy",
                _localizationService.GetString("Cancel"));
            if (!confirmed) return;
        }

        if (_shopService.TryPurchaseWithGems(item.Type))
        {
            var upgradeName = _localizationService.GetString(item.NameKey);
            PurchaseSucceeded?.Invoke(upgradeName ?? item.NameKey);
            RefreshItems();
            UpdateCoinDisplay();
        }
    }

    [RelayCommand]
    private void SelectSkin(SkinDisplayItem? item)
    {
        if (item == null || item.IsLocked || item.IsEquipped) return;

        switch (item.Category)
        {
            case SkinCategory.Player:
                _customizationService.SetPlayerSkin(item.Id);
                RefreshSkinItems();
                break;
            case SkinCategory.Bomb:
                _customizationService.SetBombSkin(item.Id);
                RefreshBombSkinItems();
                break;
            case SkinCategory.Explosion:
                _customizationService.SetExplosionSkin(item.Id);
                RefreshExplosionSkinItems();
                break;
            case SkinCategory.Trail:
                _customizationService.SetTrail(string.IsNullOrEmpty(item.Id) ? null : item.Id);
                RefreshTrailItems();
                break;
            case SkinCategory.Victory:
                _customizationService.SetVictory(string.IsNullOrEmpty(item.Id) ? null : item.Id);
                RefreshVictoryItems();
                break;
            case SkinCategory.Frame:
                _customizationService.SetFrame(string.IsNullOrEmpty(item.Id) ? null : item.Id);
                RefreshFrameItems();
                break;
        }

        PurchaseSucceeded?.Invoke(item.DisplayName);
    }

    [RelayCommand]
    private async Task PurchaseSkinAsync(SkinDisplayItem? item)
    {
        if (item == null || item.IsOwned || item.IsLocked) return;

        if (!_coinService.CanAfford(item.CoinPrice))
        {
            // Detailliertes Fehler-Feedback: Wie viele Coins fehlen
            var detail = string.Format(
                _localizationService.GetString("PurchaseFailedDetail") ?? "Requires {0} Coins, you have {1}",
                item.CoinPrice.ToString("N0"), _coinService.Balance.ToString("N0"));
            MessageRequested?.Invoke(
                _localizationService.GetString("PurchaseFailed") ?? "Purchase Failed",
                detail);
            InsufficientFunds?.Invoke();
            return;
        }

        // Bestätigungsdialog bei teureren Skins (ab 3000 Coins)
        if (item.CoinPrice >= 3000 && ConfirmationRequested != null)
        {
            var msg = string.Format(
                _localizationService.GetString("ConfirmPurchaseMessage") ?? "Spend {0} Coins on {1}?",
                item.CoinPrice.ToString("N0"), item.DisplayName);
            var confirmed = await ConfirmationRequested.Invoke(
                _localizationService.GetString("ConfirmPurchaseTitle") ?? "Confirm Purchase",
                msg,
                _localizationService.GetString("Buy") ?? "Buy",
                _localizationService.GetString("Cancel"));
            if (!confirmed) return;
        }

        bool success = item.Category switch
        {
            SkinCategory.Player => _customizationService.TryPurchasePlayerSkin(item.Id),
            SkinCategory.Bomb => _customizationService.TryPurchaseBombSkin(item.Id),
            SkinCategory.Explosion => _customizationService.TryPurchaseExplosionSkin(item.Id),
            SkinCategory.Trail => _customizationService.TryPurchaseTrail(item.Id),
            SkinCategory.Victory => _customizationService.TryPurchaseVictory(item.Id),
            SkinCategory.Frame => _customizationService.TryPurchaseFrame(item.Id),
            _ => false
        };

        if (success)
        {
            PurchaseSucceeded?.Invoke(item.DisplayName);
            switch (item.Category)
            {
                case SkinCategory.Player: RefreshSkinItems(); break;
                case SkinCategory.Bomb: RefreshBombSkinItems(); break;
                case SkinCategory.Explosion: RefreshExplosionSkinItems(); break;
                case SkinCategory.Trail: RefreshTrailItems(); break;
                case SkinCategory.Victory: RefreshVictoryItems(); break;
                case SkinCategory.Frame: RefreshFrameItems(); break;
            }
            UpdateCoinDisplay();
        }
    }

    /// <summary>
    /// Rewarded Ad für 1 Gratis-Upgrade pro Tag.
    /// Nach Erfolg wird FreeUpgradeActive gesetzt → nächster Kauf kostenlos.
    /// </summary>
    [RelayCommand]
    private async Task WatchAdForFreeUpgrade()
    {
        if (HasFreeUpgradeToday || FreeUpgradeActive) return;

        CanWatchAdForFreeUpgrade = false;

        var success = await _rewardedAdService.ShowAdAsync("free_shop_upgrade");
        if (success)
        {
            RewardedAdCooldownTracker.RecordAdShown();
            FreeUpgradeActive = true;
            HasFreeUpgradeToday = true;

            // Feedback: Gratis-Upgrade bereit
            FloatingTextRequested?.Invoke(
                _localizationService.GetString("FreeUpgradeReady") ?? "Next Upgrade FREE!",
                "gold");

            // Heutiges Datum speichern, damit es nur 1x pro Tag geht
            _preferencesService.Set("FreeUpgradeDate", DateTime.UtcNow.Date.ToString("O"));
        }
        else
        {
            CanWatchAdForFreeUpgrade = _rewardedAdService.IsAvailable && RewardedAdCooldownTracker.CanShowAd;
        }
    }

    /// <summary>Prüft ob heute bereits ein Gratis-Upgrade genutzt wurde</summary>
    private void CheckFreeUpgradeAvailability()
    {
        var savedDate = _preferencesService.Get("FreeUpgradeDate", "");
        if (!string.IsNullOrEmpty(savedDate))
        {
            if (DateTime.TryParse(savedDate, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var date))
            {
                HasFreeUpgradeToday = date.Date == DateTime.UtcNow.Date;
            }
        }
        else
        {
            HasFreeUpgradeToday = false;
        }

        CanWatchAdForFreeUpgrade = !HasFreeUpgradeToday && _rewardedAdService.IsAvailable && RewardedAdCooldownTracker.CanShowAd;
    }

    /// <summary>Rotierenden Deal kaufen</summary>
    [RelayCommand]
    private void BuyDeal(RotatingDeal? deal)
    {
        if (deal == null || deal.IsClaimed) return;

        if (_rotatingDealsService.ClaimDeal(deal.Id))
        {
            PurchaseSucceeded?.Invoke(_localizationService.GetString(deal.TitleKey) ?? deal.TitleKey);
            RefreshDailyDeals();
            UpdateCoinDisplay();
            UpdateGemDisplay();
        }
        else
        {
            InsufficientFunds?.Invoke();
        }
    }

    /// <summary>Gem-Skin kaufen (mit Gems statt Coins)</summary>
    [RelayCommand]
    private void BuyGemSkin(SkinDisplayItem? item)
    {
        if (item == null || item.IsOwned || item.IsLocked) return;

        var skin = PlayerSkins.All.FirstOrDefault(s => s.Id == item.Id);
        if (skin == null || skin.GemPrice <= 0) return;

        if (!_gemService.CanAfford(skin.GemPrice))
        {
            var detail = string.Format(
                _localizationService.GetString("PurchaseFailedDetail") ?? "Requires {0} Gems, you have {1}",
                skin.GemPrice, _gemService.Balance);
            MessageRequested?.Invoke(
                _localizationService.GetString("PurchaseFailed") ?? "Purchase Failed",
                detail);
            InsufficientFunds?.Invoke();
            return;
        }

        if (_customizationService.TryPurchasePlayerSkinWithGems(item.Id))
        {
            PurchaseSucceeded?.Invoke(item.DisplayName);
            RefreshGemSkinItems();
            UpdateGemDisplay();
        }
    }

    /// <summary>Gem-Skin auswählen (wenn bereits gekauft)</summary>
    [RelayCommand]
    private void SelectGemSkin(SkinDisplayItem? item)
    {
        if (item == null || item.IsLocked || item.IsEquipped || !item.IsOwned) return;

        _customizationService.SetPlayerSkin(item.Id);
        RefreshGemSkinItems();
        RefreshSkinItems(); // Auch normale Skin-Liste aktualisieren
        PurchaseSucceeded?.Invoke(item.DisplayName);
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

    // ═══════════════════════════════════════════════════════════════════════
    // REFRESH-METHODEN FÜR DEALS & GEM-SKINS
    // ═══════════════════════════════════════════════════════════════════════

    private void RefreshDailyDeals()
    {
        var deals = _rotatingDealsService.GetTodaysDeals();
        DailyDeals = new ObservableCollection<RotatingDeal>(deals);
        WeeklyDeal = _rotatingDealsService.GetWeeklyDeal();
    }

    private void RefreshGemSkinItems()
    {
        var currentSkin = _customizationService.PlayerSkin;
        var equippedText = _localizationService.GetString("SkinEquipped") ?? "Equipped";
        var selectText = _localizationService.GetString("SkinSelect") ?? "Select";

        var items = new List<SkinDisplayItem>();
        // Nur Skins mit GemPrice > 0 anzeigen
        foreach (var skin in PlayerSkins.All)
        {
            if (skin.GemPrice <= 0) continue;

            bool isEquipped = skin.Id == currentSkin.Id;
            bool isOwned = _customizationService.IsPlayerSkinOwned(skin.Id);

            items.Add(new SkinDisplayItem
            {
                Id = skin.Id,
                Category = SkinCategory.Player,
                PreviewIconKind = MaterialIconKind.DiamondStone,
                DisplayName = _localizationService.GetString(skin.NameKey) ?? skin.Id,
                PrimaryColor = Color.FromRgb(skin.PrimaryColor.Red, skin.PrimaryColor.Green, skin.PrimaryColor.Blue),
                SecondaryColor = Color.FromRgb(skin.SecondaryColor.Red, skin.SecondaryColor.Green, skin.SecondaryColor.Blue),
                HasGlow = skin.GlowColor.HasValue,
                CoinPrice = skin.GemPrice, // GemPrice in CoinPrice-Feld für Anzeige
                IsOwned = isOwned,
                IsEquipped = isEquipped,
                IsLocked = false,
                StatusText = isEquipped ? equippedText : (isOwned ? selectText : $"{skin.GemPrice} Gems")
            });
        }
        GemSkinItems = new ObservableCollection<SkinDisplayItem>(items);
    }

    private void UpdateGemDisplay()
    {
        GemsText = _gemService.Balance.ToString("N0");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ICON/FARB-MAPPING (konsistent mit GameRenderer/HelpIconRenderer)
    // ═══════════════════════════════════════════════════════════════════════

    private static MaterialIconKind GetPowerUpIcon(PowerUpType type) => type switch
    {
        PowerUpType.BombUp => MaterialIconKind.Bomb,
        PowerUpType.Fire => MaterialIconKind.Fire,
        PowerUpType.Speed => MaterialIconKind.FlashOutline,
        PowerUpType.Wallpass => MaterialIconKind.Ghost,
        PowerUpType.Detonator => MaterialIconKind.FlashAlert,
        PowerUpType.Bombpass => MaterialIconKind.ArrowRightBoldCircleOutline,
        PowerUpType.Flamepass => MaterialIconKind.ShieldOutline,
        PowerUpType.Mystery => MaterialIconKind.HelpCircleOutline,
        PowerUpType.Kick => MaterialIconKind.ShoeSneaker,
        PowerUpType.LineBomb => MaterialIconKind.DotsHorizontal,
        PowerUpType.PowerBomb => MaterialIconKind.StarCircle,
        PowerUpType.Skull => MaterialIconKind.SkullOutline,
        _ => MaterialIconKind.HelpCircleOutline
    };

    private static Color GetPowerUpAvaloniaColor(PowerUpType type) => type switch
    {
        PowerUpType.BombUp => Color.Parse("#5050F0"),
        PowerUpType.Fire => Color.Parse("#F05A28"),
        PowerUpType.Speed => Color.Parse("#3CDC50"),
        PowerUpType.Wallpass => Color.Parse("#966432"),
        PowerUpType.Detonator => Color.Parse("#F02828"),
        PowerUpType.Bombpass => Color.Parse("#323296"),
        PowerUpType.Flamepass => Color.Parse("#F0BE28"),
        PowerUpType.Mystery => Color.Parse("#B450F0"),
        PowerUpType.Kick => Color.Parse("#FFA500"),
        PowerUpType.LineBomb => Color.Parse("#00B4FF"),
        PowerUpType.PowerBomb => Color.Parse("#FF3232"),
        PowerUpType.Skull => Color.Parse("#640064"),
        _ => Colors.White
    };

    private static MaterialIconKind GetMechanicIcon(WorldMechanic mech) => mech switch
    {
        WorldMechanic.Ice => MaterialIconKind.Snowflake,
        WorldMechanic.Conveyor => MaterialIconKind.ArrowRightBold,
        WorldMechanic.Teleporter => MaterialIconKind.SwapHorizontalBold,
        WorldMechanic.LavaCrack => MaterialIconKind.Terrain,
        _ => MaterialIconKind.HelpCircleOutline
    };

    private static Color GetMechanicColor(WorldMechanic mech) => mech switch
    {
        WorldMechanic.Ice => Color.Parse("#64C8FF"),
        WorldMechanic.Conveyor => Color.Parse("#A0A0A0"),
        WorldMechanic.Teleporter => Color.Parse("#C864FF"),
        WorldMechanic.LavaCrack => Color.Parse("#FF5000"),
        _ => Colors.White
    };
}
