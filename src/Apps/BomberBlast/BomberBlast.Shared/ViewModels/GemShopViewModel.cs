using System.Collections.ObjectModel;
using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für den Gem-Shop - ermöglicht den Kauf von Gems per In-App-Purchase.
/// 3 Pakete: Klein (100 Gems), Mittel (500 Gems), Groß (1500 Gems).
/// </summary>
public partial class GemShopViewModel : ObservableObject, INavigable, IGameJuiceEmitter
{
    private readonly IGemService _gemService;
    private readonly IPurchaseService _purchaseService;
    private readonly ILocalizationService _localizationService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<NavigationRequest>? NavigationRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    /// <summary>Bestätigungsdialog anfordern (Titel, Nachricht, Akzeptieren, Abbrechen)</summary>
    public event Func<string, string, string, string, Task<bool>>? ConfirmationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _gemBalance;

    [ObservableProperty]
    private string _gemBalanceText = "0";

    [ObservableProperty]
    private string _titleText = "";

    [ObservableProperty]
    private string _descriptionText = "";

    [ObservableProperty]
    private ObservableCollection<GemPackageItem> _packages = [];

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public GemShopViewModel(IGemService gemService, IPurchaseService purchaseService,
        ILocalizationService localizationService)
    {
        _gemService = gemService;
        _purchaseService = purchaseService;
        _localizationService = localizationService;

        // Balance-Änderungen live verfolgen
        _gemService.BalanceChanged += (_, _) => UpdateGemBalance();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert Gem-Balance und lokalisierte Texte beim Anzeigen der View.
    /// </summary>
    public void OnAppearing()
    {
        UpdateGemBalance();
        UpdateLocalizedTexts();
        BuildPackages();
    }

    public void UpdateLocalizedTexts()
    {
        TitleText = _localizationService.GetString("GemShopTitle") ?? "Gem Shop";
        DescriptionText = _localizationService.GetString("GemShopDescription") ?? "Purchase gems to unlock premium items faster";
    }

    private void UpdateGemBalance()
    {
        GemBalance = _gemService.Balance;
        GemBalanceText = _gemService.Balance.ToString("N0");
    }

    /// <summary>
    /// Erstellt die 3 Gem-Pakete mit lokalisierten Namen und Badges.
    /// </summary>
    private void BuildPackages()
    {
        var smallName = _localizationService.GetString("GemPackSmall") ?? "Small Pack";
        var mediumName = _localizationService.GetString("GemPackMedium") ?? "Medium Pack";
        var largeName = _localizationService.GetString("GemPackLarge") ?? "Large Pack";
        var popularBadge = _localizationService.GetString("GemPackPopular");
        var bestValueBadge = _localizationService.GetString("GemPackBestValue");

        Packages =
        [
            new GemPackageItem
            {
                ProductId = "gem_pack_small",
                DisplayName = smallName,
                GemAmount = 100,
                PriceText = "0,99 \u20ac",
                IconKind = MaterialIconKind.DiamondStone,
                IconColor = "#00BCD4",
                BadgeText = null,
                IsPopular = false
            },
            new GemPackageItem
            {
                ProductId = "gem_pack_medium",
                DisplayName = mediumName,
                GemAmount = 500,
                PriceText = "3,99 \u20ac",
                IconKind = MaterialIconKind.Diamond,
                IconColor = "#2196F3",
                BadgeText = popularBadge,
                IsPopular = true
            },
            new GemPackageItem
            {
                ProductId = "gem_pack_large",
                DisplayName = largeName,
                GemAmount = 1500,
                PriceText = "7,99 \u20ac",
                IconKind = MaterialIconKind.TreasureChest,
                IconColor = "#FFD700",
                BadgeText = bestValueBadge,
                IsPopular = false
            }
        ];
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kauft ein Gem-Paket per In-App-Purchase (Consumable).
    /// Bei Erfolg werden die Gems gutgeschrieben und Feedback angezeigt.
    /// </summary>
    [RelayCommand]
    private async Task PurchasePackageAsync(GemPackageItem? item)
    {
        if (item == null) return;

        // Bestätigungsdialog vor Echtgeld-Kauf
        if (ConfirmationRequested != null)
        {
            var msg = string.Format(
                _localizationService.GetString("GemPurchaseConfirm") ?? "Buy {0} Gems for {1}?",
                item.GemAmount, item.PriceText);
            var confirmed = await ConfirmationRequested.Invoke(
                _localizationService.GetString("GemShopTitle") ?? "Gem Shop",
                msg,
                _localizationService.GetString("Buy") ?? "Buy",
                _localizationService.GetString("Cancel") ?? "Cancel");
            if (!confirmed) return;
        }

        var success = await _purchaseService.PurchaseConsumableAsync(item.ProductId);
        if (success)
        {
            _gemService.AddGems(item.GemAmount);
            UpdateGemBalance();

            // Erfolgs-Feedback
            var msg = string.Format(
                _localizationService.GetString("GemPurchaseSuccess") ?? "+{0} Gems!",
                item.GemAmount);
            FloatingTextRequested?.Invoke(msg, "gold");
            CelebrationRequested?.Invoke();
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke(new GoBack());
    }
}

/// <summary>
/// Datenmodell für ein Gem-Paket im Shop.
/// </summary>
public class GemPackageItem
{
    /// <summary>Google Play Product-ID (z.B. "gem_pack_small")</summary>
    public string ProductId { get; init; } = "";

    /// <summary>Lokalisierter Anzeigename</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>Anzahl Gems im Paket</summary>
    public int GemAmount { get; init; }

    /// <summary>Preis-Text (z.B. "0,99 EUR")</summary>
    public string PriceText { get; init; } = "";

    /// <summary>MaterialIcon-Art für das Icon</summary>
    public MaterialIconKind IconKind { get; init; }

    /// <summary>Hex-Farbcode für das Icon (z.B. "#00BCD4")</summary>
    public string IconColor { get; init; } = "#FFFFFF";

    /// <summary>Optionaler Badge-Text (z.B. "Beliebt!" oder "Bester Wert!")</summary>
    public string? BadgeText { get; init; }

    /// <summary>Ob dieses Paket als "Beliebt" hervorgehoben wird</summary>
    public bool IsPopular { get; init; }

    /// <summary>Ob ein Badge angezeigt werden soll</summary>
    public bool HasBadge => !string.IsNullOrEmpty(BadgeText);
}
