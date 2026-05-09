using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel fuer den Reputation-Shop (v2.1.0).
/// Zeigt Items als Anzeige-Wrapper mit lokalisierten Texten + CanAfford-Flag.
/// </summary>
public sealed partial class ReputationShopViewModel : ViewModelBase
{
    private readonly IReputationShopService _shopService;
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localization;
    private readonly IDialogService _dialogService;

    public event Action<string, string>? FloatingTextRequested;

    public ObservableCollection<ReputationShopItemDisplay> Items { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReputationDisplay))]
    private int _currentReputation;

    [ObservableProperty]
    private bool _isUnlocked;

    public string ReputationDisplay => $"{CurrentReputation} / 100";

    public ReputationShopViewModel(
        IReputationShopService shopService,
        IGameStateService gameStateService,
        ILocalizationService localization,
        IDialogService dialogService)
    {
        _shopService = shopService;
        _gameStateService = gameStateService;
        _localization = localization;
        _dialogService = dialogService;

        _shopService.ItemPurchased += OnItemPurchased;
        Refresh();
    }

    /// <summary>Befuellt Items + IsUnlocked aus dem Service.</summary>
    public void Refresh()
    {
        var rep = _gameStateService.State.Reputation.ReputationScore;
        CurrentReputation = rep;
        IsUnlocked = _shopService.IsUnlocked;

        Items.Clear();
        foreach (var item in _shopService.AvailableItems)
        {
            var name = _localization.GetString(item.NameKey);
            if (string.IsNullOrEmpty(name)) name = item.NameFallback;
            var desc = _localization.GetString(item.DescriptionKey);
            if (string.IsNullOrEmpty(desc)) desc = item.DescriptionFallback;
            Items.Add(new ReputationShopItemDisplay
            {
                Item = item,
                Name = name,
                Description = desc,
                CostText = $"{item.ReputationCost} Rep",
                CanAfford = rep >= item.ReputationCost,
                IconKind = item.IconKind
            });
        }
    }

    [RelayCommand]
    private void Buy(ReputationShopItemDisplay? display)
    {
        if (display?.Item == null) return;

        if (!_shopService.TryBuy(display.Item.Id))
        {
            _dialogService.ShowAlertDialog(
                _localization.GetString("RepShopBuyFailedTitle") ?? "Not enough reputation",
                _localization.GetString("RepShopBuyFailedDesc")
                    ?? "You need more reputation to buy this item.",
                _localization.GetString("OK") ?? "OK");
            return;
        }
        Refresh();
    }

    private void OnItemPurchased(ReputationShopItem item)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var name = _localization.GetString(item.NameKey);
            if (string.IsNullOrEmpty(name)) name = item.NameFallback;
            FloatingTextRequested?.Invoke($"✓ {name}", "level");
            Refresh();
        });
    }
}

/// <summary>Anzeige-Wrapper fuer ein <see cref="ReputationShopItem"/>.</summary>
public sealed class ReputationShopItemDisplay
{
    public ReputationShopItem Item { get; init; } = new();
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string CostText { get; init; } = "";
    public bool CanAfford { get; init; }
    public string IconKind { get; init; } = "Star";
}
