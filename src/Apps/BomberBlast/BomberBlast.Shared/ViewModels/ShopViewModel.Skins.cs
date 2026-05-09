using Avalonia.Media;
using BomberBlast.Icons;
using BomberBlast.Models;
using BomberBlast.Models.Entities;
using CommunityToolkit.Mvvm.Input;

namespace BomberBlast.ViewModels;

// v2.0.39 (Plan Task 2.3): Aus ShopViewModel.cs extrahiert.
// Enthaelt alle Skin-bezogenen Refresh-Methoden (Player/Bomb/Explosion/Trail/Victory/Frame)
// + SelectSkin- und PurchaseSkinAsync-Commands.
public sealed partial class ShopViewModel
{
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
                PreviewIconKind = GameIconKind.Account,
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
        ReloadCollection(SkinItems, items);
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
                PreviewIconKind = GameIconKind.Bomb,
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
        ReloadCollection(BombSkinItems, items);
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
                PreviewIconKind = GameIconKind.Fire,
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
        ReloadCollection(ExplosionSkinItems, items);
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
            PreviewIconKind = GameIconKind.Trail,
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
                PreviewIconKind = GameIconKind.Trail,
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
        ReloadCollection(TrailItems, items);
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
            PreviewIconKind = GameIconKind.Celebration,
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
                PreviewIconKind = GameIconKind.Celebration,
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
        ReloadCollection(VictoryItems, items);
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
            PreviewIconKind = GameIconKind.CardFrame,
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
                PreviewIconKind = GameIconKind.CardFrame,
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
        ReloadCollection(FrameItems, items);
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
}
