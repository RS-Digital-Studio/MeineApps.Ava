using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the order detail page.
/// Shows order details and allows starting mini-games.
/// </summary>
public partial class OrderViewModel : ViewModelBase
{
    private readonly IGameStateService _gameStateService;
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localizationService;
    private readonly IPurchaseService _purchaseService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Event to request a confirmation dialog from the view.
    /// The bool result indicates if the user confirmed.
    /// </summary>
    public event Func<string, string, string, string, Task<bool>>? ConfirmationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private Order? _order;

    [ObservableProperty]
    private string _orderTitle = "";

    [ObservableProperty]
    private string _customerIcon = "HardHat";

    [ObservableProperty]
    private string _workshopIcon = "Hammer";

    [ObservableProperty]
    private string _workshopName = "";

    [ObservableProperty]
    private string _rewardText = "";

    [ObservableProperty]
    private string _xpRewardText = "";

    [ObservableProperty]
    private string _rewardHintText = "";

    [ObservableProperty]
    private string _difficultyText = "";

    [ObservableProperty]
    private string _difficultyColor = "#FFFFFF";

    [ObservableProperty]
    private int _miniGamesCompleted;

    [ObservableProperty]
    private int _miniGamesRequired;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isInProgress;

    [ObservableProperty]
    private bool _canStart;

    [ObservableProperty]
    private bool _isCooperationOrder;

    /// <summary>
    /// Indicates whether ads should be shown (not premium).
    /// </summary>
    public bool ShowAds => !_purchaseService.IsPremium;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public OrderViewModel(
        IGameStateService gameStateService,
        IAudioService audioService,
        ILocalizationService localizationService,
        IPurchaseService purchaseService)
    {
        _gameStateService = gameStateService;
        _audioService = audioService;
        _localizationService = localizationService;
        _purchaseService = purchaseService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION (replaces IQueryAttributable)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initialize with an order object.
    /// </summary>
    public void SetOrder(Order order)
    {
        LoadOrder(order);
    }

    /// <summary>
    /// Initialize from the active order in game state.
    /// </summary>
    public void LoadFromActiveOrder()
    {
        var activeOrder = _gameStateService.GetActiveOrder();
        if (activeOrder != null)
        {
            LoadOrder(activeOrder);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    private void LoadOrder(Order order)
    {
        Order = order;
        var localizedTitle = _localizationService.GetString(order.TitleKey);
        OrderTitle = string.IsNullOrEmpty(localizedTitle) ? order.TitleFallback : localizedTitle;

        // Set icons
        CustomerIcon = GetCustomerIcon(order.Difficulty);
        WorkshopIcon = GetWorkshopIcon(order.WorkshopType);
        WorkshopName = GetWorkshopName(order.WorkshopType);

        // Zentralisierte Belohnungsberechnung über Order-Methoden
        RewardText = $"~{FormatMoney(order.CalculateEstimatedReward())}";
        XpRewardText = $"~{order.CalculateEstimatedXp()} XP";

        RewardHintText = _localizationService.GetString("RewardDependsOnRating");

        // Set difficulty
        DifficultyText = GetDifficultyText(order.Difficulty);
        DifficultyColor = GetDifficultyColorHex(order.Difficulty);

        // Progress
        MiniGamesRequired = order.Tasks.Count;
        MiniGamesCompleted = order.CurrentTaskIndex;
        Progress = MiniGamesRequired > 0 ? (double)MiniGamesCompleted / MiniGamesRequired : 0;

        // State - order is "in progress" if we've started but not completed
        IsInProgress = order.CurrentTaskIndex > 0 && !order.IsCompleted;
        CanStart = order.CurrentTaskIndex == 0;
        IsCooperationOrder = order.OrderType == OrderType.Cooperation;
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
    private async Task StartOrderAsync()
    {
        if (Order == null) return;

        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Order is already marked active by MainViewModel.StartOrderAsync
        IsInProgress = true;
        CanStart = false;

        // Navigate to the appropriate mini-game
        NavigateToMiniGame();
    }

    [RelayCommand]
    private async Task ContinueOrderAsync()
    {
        if (Order == null) return;

        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Navigate to the appropriate mini-game
        NavigateToMiniGame();
    }

    [RelayCommand]
    private async Task CancelOrderAsync()
    {
        if (Order == null) return;

        // Try to get confirmation from the view
        bool confirmed = false;
        if (ConfirmationRequested != null)
        {
            confirmed = await ConfirmationRequested.Invoke(
                _localizationService.GetString("ConfirmCancelOrder"),
                _localizationService.GetString("ConfirmCancelOrderDesc"),
                _localizationService.GetString("YesCancel"),
                _localizationService.GetString("No"));
        }
        else
        {
            // Fallback: just cancel without confirmation
            confirmed = true;
        }

        if (confirmed)
        {
            _gameStateService.CancelActiveOrder();
            NavigationRequested?.Invoke("..");
        }
    }

    private void NavigateToMiniGame()
    {
        if (Order == null) return;

        // Get the mini-game route based on workshop type
        var route = GetMiniGameRoute(Order.WorkshopType);

        // Navigate with difficulty parameter
        NavigationRequested?.Invoke($"{route}?difficulty={(int)Order.Difficulty}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private static string GetCustomerIcon(OrderDifficulty difficulty) => difficulty switch
    {
        OrderDifficulty.Easy => "👵",
        OrderDifficulty.Medium => "AccountTie",
        OrderDifficulty.Hard => "OfficeBuildingOutline",
        _ => "HardHat"
    };

    private static string GetWorkshopIcon(WorkshopType type) => type switch
    {
        WorkshopType.Carpenter => "Saw",
        WorkshopType.Plumber => "Wrench",
        WorkshopType.Electrician => "LightningBolt",
        WorkshopType.Painter => "Palette",
        WorkshopType.Roofer => "Home",
        WorkshopType.Contractor => "Crane",
        _ => "Hammer"
    };

    private string GetWorkshopName(WorkshopType type) =>
        _localizationService.GetString(type.GetLocalizationKey());

    private string GetDifficultyText(OrderDifficulty difficulty) => difficulty switch
    {
        OrderDifficulty.Easy => _localizationService.GetString("DifficultyEasy"),
        OrderDifficulty.Medium => _localizationService.GetString("DifficultyMedium"),
        OrderDifficulty.Hard => _localizationService.GetString("DifficultyHard"),
        _ => _localizationService.GetString("DifficultyUnknown")
    };

    private static string GetDifficultyColorHex(OrderDifficulty difficulty) => difficulty switch
    {
        OrderDifficulty.Easy => "#06FFA5",
        OrderDifficulty.Medium => "#FFD700",
        OrderDifficulty.Hard => "#FF6B6B",
        _ => "#FFFFFF"
    };

    private static string GetMiniGameRoute(WorkshopType type) => type switch
    {
        WorkshopType.Carpenter => "minigame/sawing",
        WorkshopType.Plumber => "minigame/pipes",
        WorkshopType.Electrician => "minigame/wiring",
        WorkshopType.Painter => "minigame/painting",
        WorkshopType.Roofer => "minigame/rooftiling",
        WorkshopType.Contractor => "minigame/blueprint",
        WorkshopType.Architect => "minigame/designpuzzle",
        WorkshopType.GeneralContractor => "minigame/inspection",
        _ => "minigame/sawing"
    };

    private static string FormatMoney(decimal amount) => MoneyFormatter.Format(amount, 1);
}
