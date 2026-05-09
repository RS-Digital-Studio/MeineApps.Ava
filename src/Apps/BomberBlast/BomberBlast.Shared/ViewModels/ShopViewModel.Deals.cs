using Avalonia.Media;
using BomberBlast.Icons;
using BomberBlast.Models;
using BomberBlast.Models.Entities;
using CommunityToolkit.Mvvm.Input;

namespace BomberBlast.ViewModels;

// v2.0.39 (Plan Task 2.3): Aus ShopViewModel.cs extrahiert.
// Enthaelt RotatingDeals + Gem-Skin-Logik (BuyDeal / RefreshGemSkinItems / BuyGemSkin / SelectGemSkin).
public sealed partial class ShopViewModel
{
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

    private void RefreshDailyDeals()
    {
        var deals = _rotatingDealsService.GetTodaysDeals();
        ReloadCollection(DailyDeals, deals);
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
                PreviewIconKind = GameIconKind.DiamondStone,
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
        ReloadCollection(GemSkinItems, items);
    }
}
