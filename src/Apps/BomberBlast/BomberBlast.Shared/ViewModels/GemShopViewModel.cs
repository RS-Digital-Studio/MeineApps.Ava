using System.Collections.ObjectModel;
using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using BomberBlast.Icons;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für den Gem-Shop - ermöglicht den Kauf von Gems per In-App-Purchase.
/// 4 Pakete: Small (100G/0,99€), Medium (600G/3,99€), Large (1500G/7,99€), Mega (5000G/14,99€).
/// </summary>
public sealed partial class GemShopViewModel : ViewModelBase, INavigable, IGameJuiceEmitter, ILocalizable, IDisposable
{
    private readonly IGemService _gemService;
    private readonly IPurchaseService _purchaseService;
    private readonly ILocalizationService _localizationService;
    /// <summary>Sprint 2.2 AAA-Audit #2: IAP-Funnel-Telemetrie (Start/Success/Cancel/Fail).</summary>
    private readonly IAnalyticsService _analytics;

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
        ILocalizationService localizationService, IAnalyticsService analytics)
    {
        _gemService = gemService;
        _purchaseService = purchaseService;
        _localizationService = localizationService;
        _analytics = analytics;

        // Balance-Änderungen live verfolgen
        _gemService.BalanceChanged += OnGemBalanceChanged;
    }

    private void OnGemBalanceChanged(object? sender, EventArgs e) => UpdateGemBalance();
    private bool _disposed;

    /// <summary>Audit (Event-Subscription-Lücke): Unsubscribe beim App-Shutdown.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gemService.BalanceChanged -= OnGemBalanceChanged;
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
        var megaName = _localizationService.GetString("GemPackMega") ?? "Mega Pack";
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
                PriceCents = 99,
                IconKind = GameIconKind.DiamondStone,
                IconColor = "#00BCD4",
                BadgeText = null,
                IsPopular = false,
                ButtonSeed = 110
            },
            new GemPackageItem
            {
                // BAL-33 (20.04.2026): 500G -> 600G. Bei 500G war G/€-Ratio (125) deutlich
                // unter Large (188) und Mega (334), Conversion-Paket war unattraktiv.
                // 600G gibt 150 G/€ = 1,5x Basis, klarer Vorteil gegenueber Small ohne Large zu kannibalisieren.
                ProductId = "gem_pack_medium",
                DisplayName = mediumName,
                GemAmount = 600,
                PriceText = "3,99 \u20ac",
                PriceCents = 399,
                IconKind = GameIconKind.Diamond,
                IconColor = "#2196F3",
                BadgeText = popularBadge,
                IsPopular = true,
                ButtonSeed = 111
            },
            new GemPackageItem
            {
                ProductId = "gem_pack_large",
                DisplayName = largeName,
                GemAmount = 1500,
                PriceText = "7,99 \u20ac",
                PriceCents = 799,
                IconKind = GameIconKind.TreasureChest,
                IconColor = "#FFD700",
                BadgeText = bestValueBadge,
                IsPopular = false,
                ButtonSeed = 112
            },
            new GemPackageItem
            {
                ProductId = "gem_pack_mega",
                DisplayName = megaName,
                GemAmount = 5000,
                PriceText = "14,99 \u20ac",
                PriceCents = 1499,
                IconKind = GameIconKind.Crown,
                IconColor = "#FF6B35",
                BadgeText = _localizationService.GetString("GemPackWhale") ?? "3.3x Value!",
                IsPopular = false,
                ButtonSeed = 113
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

        // Sprint 2.2 AAA-Audit #2: Funnel-Start — der User hat Kauf-Intent gezeigt.
        _analytics?.LogEvent(AnalyticsEvents.PurchaseFlowStart, new Dictionary<string, object>
        {
            [AnalyticsParams.Sku] = item.ProductId,
            [AnalyticsParams.PriceCents] = item.PriceCents,
            [AnalyticsParams.Currency] = "EUR",
        });

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
            if (!confirmed)
            {
                // Funnel-Drop-off: User bricht im Bestätigungsdialog ab.
                _analytics?.LogEvent(AnalyticsEvents.PurchaseCancel, new Dictionary<string, object>
                {
                    [AnalyticsParams.Sku] = item.ProductId,
                });
                return;
            }
        }

        var success = await _purchaseService.PurchaseConsumableAsync(item.ProductId);
        if (success)
        {
            _gemService.AddGems(item.GemAmount);
            UpdateGemBalance();

            // Funnel-Conversion: Echtgeld-Kauf abgeschlossen.
            _analytics?.LogEvent(AnalyticsEvents.PurchaseSuccess, new Dictionary<string, object>
            {
                [AnalyticsParams.Sku] = item.ProductId,
                [AnalyticsParams.PriceCents] = item.PriceCents,
                [AnalyticsParams.Currency] = "EUR",
            });

            // Erfolgs-Feedback
            var msg = string.Format(
                _localizationService.GetString("GemPurchaseSuccess") ?? "+{0} Gems!",
                item.GemAmount);
            FloatingTextRequested?.Invoke(msg, "gold");
            CelebrationRequested?.Invoke();
        }
        else
        {
            // Funnel-Drop-off: Google-Play-Kauf fehlgeschlagen oder vom User im
            // Play-Billing-Sheet abgebrochen (PurchaseConsumableAsync unterscheidet das nicht).
            _analytics?.LogEvent(AnalyticsEvents.PurchaseFail, new Dictionary<string, object>
            {
                [AnalyticsParams.Sku] = item.ProductId,
            });
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

    /// <summary>Preis in Cents (z.B. 99 fuer 0,99 EUR) — fuer Funnel-Telemetrie.</summary>
    public int PriceCents { get; init; }

    /// <summary>GameIcon-Art fuer das Icon</summary>
    public GameIconKind IconKind { get; init; }

    /// <summary>Hex-Farbcode für das Icon (z.B. "#00BCD4")</summary>
    public string IconColor { get; init; } = "#FFFFFF";

    /// <summary>Optionaler Badge-Text (z.B. "Beliebt!" oder "Bester Wert!")</summary>
    public string? BadgeText { get; init; }

    /// <summary>Ob dieses Paket als "Beliebt" hervorgehoben wird</summary>
    public bool IsPopular { get; init; }

    /// <summary>Ob ein Badge angezeigt werden soll</summary>
    public bool HasBadge => !string.IsNullOrEmpty(BadgeText);

    /// <summary>Einzigartiger Seed fuer GameButtonCanvas (prozedurale Textur)</summary>
    public int ButtonSeed { get; init; }
}
