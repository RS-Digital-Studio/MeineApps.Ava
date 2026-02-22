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
public partial class ShopViewModel : ObservableObject, IDisposable
{
    private readonly IShopService _shopService;
    private readonly ICoinService _coinService;
    private readonly ILocalizationService _localizationService;
    private readonly IProgressService _progressService;
    private readonly ICustomizationService _customizationService;
    private readonly IPurchaseService _purchaseService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;

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

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public ShopViewModel(IShopService shopService, ICoinService coinService,
        ILocalizationService localizationService, IProgressService progressService,
        ICustomizationService customizationService, IPurchaseService purchaseService)
    {
        _shopService = shopService;
        _coinService = coinService;
        _localizationService = localizationService;
        _progressService = progressService;
        _customizationService = customizationService;
        _purchaseService = purchaseService;

        _coinService.BalanceChanged += OnBalanceChanged;
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
        UpdateCoinDisplay();
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
        var unlockedFormat = _localizationService.GetString("UnlockedAt") ?? "Ab Level {0}";
        var unlockedText = _localizationService.GetString("Unlocked") ?? "Freigeschaltet";

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
            item.Refresh(_coinService.Balance);
        }
        // Skins aktualisieren (CanBuy Status kann sich aendern)
        RefreshSkinItems();
        RefreshBombSkinItems();
        RefreshExplosionSkinItems();
        RefreshTrailItems();
        RefreshVictoryItems();
        RefreshFrameItems();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task PurchaseAsync(ShopDisplayItem? item)
    {
        if (item == null || item.IsMaxed) return;

        if (!_coinService.CanAfford(item.NextPrice))
        {
            InsufficientFunds?.Invoke();
            return;
        }

        // Bestätigungsdialog bei teureren Käufen (ab 3000 Coins)
        if (item.NextPrice >= 3000 && ConfirmationRequested != null)
        {
            var upgradeName = _localizationService.GetString(item.NameKey) ?? item.NameKey;
            var msg = string.Format(
                _localizationService.GetString("ConfirmPurchaseMessage") ?? "{0} Coins für {1} ausgeben?",
                item.NextPrice.ToString("N0"), upgradeName);
            var confirmed = await ConfirmationRequested.Invoke(
                _localizationService.GetString("ConfirmPurchaseTitle") ?? "Kauf bestätigen",
                msg,
                _localizationService.GetString("Buy") ?? "Kaufen",
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
            InsufficientFunds?.Invoke();
            return;
        }

        // Bestätigungsdialog bei teureren Skins (ab 3000 Coins)
        if (item.CoinPrice >= 3000 && ConfirmationRequested != null)
        {
            var msg = string.Format(
                _localizationService.GetString("ConfirmPurchaseMessage") ?? "{0} Coins für {1} ausgeben?",
                item.CoinPrice.ToString("N0"), item.DisplayName);
            var confirmed = await ConfirmationRequested.Invoke(
                _localizationService.GetString("ConfirmPurchaseTitle") ?? "Kauf bestätigen",
                msg,
                _localizationService.GetString("Buy") ?? "Kaufen",
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

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    public void Dispose()
    {
        _coinService.BalanceChanged -= OnBalanceChanged;
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
