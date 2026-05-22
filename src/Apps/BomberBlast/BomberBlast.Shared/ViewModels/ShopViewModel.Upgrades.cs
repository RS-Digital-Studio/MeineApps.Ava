using Avalonia.Media;
using BomberBlast.Icons;
using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Levels;
using BomberBlast.Services;
using CommunityToolkit.Mvvm.Input;

namespace BomberBlast.ViewModels;

// v2.0.39 (Plan Task 2.3): Aus ShopViewModel.cs extrahiert.
// Enthaelt Permanent-Upgrade-Refresh + PurchaseAsync (Coins/Gems) + WatchAdForFreeUpgrade
// + PowerUp/Mechanic-Refresh + Icon/Color-Mappings.
public sealed partial class ShopViewModel
{
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

        ReloadCollection(ShopItems, items);
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
        ReloadCollection(PowerUpItems, items);
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
        ReloadCollection(MechanicItems, items);
    }

    /// <summary>Erstellt ein PowerUpDisplayItem mit Unlock-Status-Logik</summary>
    private PowerUpDisplayItem CreateDisplayItem(string id, string nameKey, int unlockLevel,
        GameIconKind icon, Color color)
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

    // Audit M10: Gate gegen Double-Tap waehrend Confirm-Dialog offen ist.
    // Stacking-Dialoge waren Telemetrie-Muell + UX-Bug.
    private bool _purchaseInFlight;

    [RelayCommand]
    private async Task PurchaseAsync(ShopDisplayItem? item)
    {
        if (item == null || item.IsMaxed) return;
        if (_purchaseInFlight) return;

        _purchaseInFlight = true;
        try
        {
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
        finally
        {
            _purchaseInFlight = false;
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

    // ═══════════════════════════════════════════════════════════════════════
    // ICON/FARB-MAPPING (konsistent mit GameRenderer/HelpIconRenderer)
    // ═══════════════════════════════════════════════════════════════════════

    private static GameIconKind GetPowerUpIcon(PowerUpType type) => type switch
    {
        PowerUpType.BombUp => GameIconKind.Bomb,
        PowerUpType.Fire => GameIconKind.Fire,
        PowerUpType.Speed => GameIconKind.FlashOutline,
        PowerUpType.Wallpass => GameIconKind.Ghost,
        PowerUpType.Detonator => GameIconKind.FlashAlert,
        PowerUpType.Bombpass => GameIconKind.ArrowRightCircle,
        PowerUpType.Flamepass => GameIconKind.ShieldOutline,
        PowerUpType.Mystery => GameIconKind.HelpCircleOutline,
        PowerUpType.Kick => GameIconKind.Shoe,
        PowerUpType.LineBomb => GameIconKind.DotsHorizontal,
        PowerUpType.PowerBomb => GameIconKind.StarCircle,
        PowerUpType.Skull => GameIconKind.SkullOutline,
        _ => GameIconKind.HelpCircleOutline
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

    private static GameIconKind GetMechanicIcon(WorldMechanic mech) => mech switch
    {
        WorldMechanic.Ice => GameIconKind.Snowflake,
        WorldMechanic.Conveyor => GameIconKind.ArrowRightBold,
        WorldMechanic.Teleporter => GameIconKind.SwapHorizontal,
        WorldMechanic.LavaCrack => GameIconKind.Terrain,
        _ => GameIconKind.HelpCircleOutline
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
