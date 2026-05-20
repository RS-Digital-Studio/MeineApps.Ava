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
public sealed partial class OrderViewModel : ViewModelBase, INavigable
{
    private readonly IGameStateService _gameStateService;
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localizationService;
    private readonly IPurchaseService _purchaseService;
    private readonly IDialogService _dialogService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

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

    // ═══════════════════════════════════════════════════════════════════════
    // RISK/REWARD STRATEGY-ANZEIGE (v2.0.36)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Trefferquote-Anzeige fuer Safe-Strategie (z.B. „Trefferquote: 95%").</summary>
    [ObservableProperty]
    private string _safeWinRateText = "";

    /// <summary>Erwartungswert-Anzeige fuer Safe-Strategie (z.B. „EV: 0,75x").</summary>
    [ObservableProperty]
    private string _safeExpectedValueText = "";

    /// <summary>Trefferquote-Anzeige fuer Standard-Strategie.</summary>
    [ObservableProperty]
    private string _standardWinRateText = "";

    /// <summary>Erwartungswert-Anzeige fuer Standard-Strategie.</summary>
    [ObservableProperty]
    private string _standardExpectedValueText = "";

    /// <summary>Trefferquote-Anzeige fuer Risk-Strategie.</summary>
    [ObservableProperty]
    private string _riskWinRateText = "";

    /// <summary>Erwartungswert-Anzeige fuer Risk-Strategie.</summary>
    [ObservableProperty]
    private string _riskExpectedValueText = "";

    /// <summary>True wenn Risk-Strategie statistisch schlechter als Standard — UI zeigt rote Border.</summary>
    [ObservableProperty]
    private bool _isRiskWorseThanStandard;

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
        IPurchaseService purchaseService,
        IDialogService dialogService)
    {
        _gameStateService = gameStateService;
        _audioService = audioService;
        _localizationService = localizationService;
        _purchaseService = purchaseService;
        _dialogService = dialogService;
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

        // Belohnungsberechnung inkl. aller aktiven Multiplikatoren (Research, Gebäude, Reputation, Events, Stammkunden)
        var rewardMultiplier = _gameStateService.GetOrderRewardMultiplier(order);
        RewardText = $"~{FormatMoney(order.CalculateEstimatedReward() * rewardMultiplier)}";
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

        // v2.0.36: Strategy-Quoten + Erwartungswert berechnen
        UpdateStrategyStats(order);
    }

    /// <summary>
    /// Befuellt die Trefferquote- und Erwartungswert-Anzeigen pro Strategie (v2.0.36).
    /// Basis: <see cref="IGameStateService.GetMiniGameSuccessRate"/> auf dem Typ des
    /// naechsten ausstehenden Tasks. Bei weniger als 5 Plays wird ein „~?"-Hinweis gezeigt
    /// statt einer konkreten Zahl.
    ///
    /// Erwartungswert-Modell (vereinfacht, lineare Belohnungs-Skalierung):
    ///   Safe: ~95% Trefferquote × 0,75x Reward (kleines Risiko, kleiner Reward)
    ///   Standard: persoenliche Trefferquote × 1,0x Reward (Miss → 50% Teil-Reward)
    ///   Risk: persoenliche Trefferquote × 0,7 × 2,0x Reward (Miss → 0)
    /// </summary>
    private void UpdateStrategyStats(Order order)
    {
        var nextTaskIndex = Math.Min(order.CurrentTaskIndex, order.Tasks.Count - 1);
        var miniGameType = order.Tasks.Count > 0
            ? order.Tasks[Math.Max(0, nextTaskIndex)].GameType
            : (MiniGameType?)null;

        if (miniGameType == null)
        {
            SafeWinRateText = StandardWinRateText = RiskWinRateText = "";
            SafeExpectedValueText = StandardExpectedValueText = RiskExpectedValueText = "";
            IsRiskWorseThanStandard = false;
            return;
        }

        var personalRate = _gameStateService.GetMiniGameSuccessRate(miniGameType.Value);
        bool tooFewPlays = personalRate < 0;
        var standardRate = tooFewPlays ? 0.65 : personalRate;
        var safeRate = Math.Min(0.95, 0.30 + standardRate * 0.65); // Safe-Schiene zieht hoch
        var riskRate = Math.Max(0.0, standardRate * 0.7);

        var winRateLabelFormat = _localizationService.GetString("StrategyWinRateLabel") ?? "Hit rate: {0}%";
        var unknownText = _localizationService.GetString("StrategyWinRateUnknown") ?? "Hit rate: ~?";
        var evFormat = _localizationService.GetString("StrategyExpectedValue") ?? "EV: {0}x";

        SafeWinRateText = tooFewPlays ? unknownText : string.Format(winRateLabelFormat, (int)Math.Round(safeRate * 100));
        StandardWinRateText = tooFewPlays ? unknownText : string.Format(winRateLabelFormat, (int)Math.Round(standardRate * 100));
        RiskWinRateText = tooFewPlays ? unknownText : string.Format(winRateLabelFormat, (int)Math.Round(riskRate * 100));

        var safeEv = safeRate * 0.75;
        var standardEv = standardRate * 1.0 + (1 - standardRate) * 0.5;
        var riskEv = riskRate * 2.0; // Miss → 0
        SafeExpectedValueText = string.Format(evFormat, safeEv.ToString("0.00"));
        StandardExpectedValueText = string.Format(evFormat, standardEv.ToString("0.00"));
        RiskExpectedValueText = string.Format(evFormat, riskEv.ToString("0.00"));

        IsRiskWorseThanStandard = !tooFewPlays && riskEv < standardEv;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void GoBack()
    {
        // v2.0.35 Feature A: Back aus OrderDetail pausiert den aktuellen Vordergrund-Auftrag.
        // Der Auftrag bleibt in ParallelOrdersByWorkshop erhalten — Spieler kann spaeter
        // via Workshop-Card oder Banner zurueckkehren ("Plate-Spinning").
        if (_gameStateService.GetActiveOrder() != null)
        {
            _gameStateService.PauseActiveOrder();
        }
        NavigationRequested?.Invoke("..");
    }

    [RelayCommand]
    private async Task StartOrderAsync()
    {
        // Legacy: Standard-Strategy verwenden wenn kein Strategy-Button geklickt wurde.
        await StartWithStrategyAsync(OrderStrategy.Standard.ToString());
    }

    /// <summary>
    /// Startet den Auftrag mit einer vom Spieler gewaehlten Strategie (v2.0.35).
    /// Safe/Standard/Risk wirken auf MiniGame-Schwierigkeit und Reward-Multiplikator.
    /// Risk-Wahl loest einen Bestaetigungs-Dialog aus wegen Hard-Fail-Risiko.
    /// </summary>
    [RelayCommand]
    private async Task StartWithStrategyAsync(string strategyName)
    {
        if (Order == null) return;
        if (!Enum.TryParse<OrderStrategy>(strategyName, ignoreCase: true, out var strat)) return;

        // Risk-Strategie: Bestaetigungs-Dialog wegen Hard-Fail-Gefahr
        if (strat == OrderStrategy.Risk)
        {
            var confirmed = await _dialogService.ShowConfirmDialog(
                _localizationService.GetString("OrderStrategyRiskConfirmTitle") ?? "Really take the risk?",
                _localizationService.GetString("OrderStrategyRiskConfirmMessage")
                    ?? "Miss = no reward + reputation loss. Really risk it?",
                _localizationService.GetString("OrderStrategyRiskConfirmYes") ?? "Yes, risk it",
                _localizationService.GetString("Cancel") ?? "Cancel");
            if (!confirmed) return;
        }

        Order.Strategy = strat;
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        IsInProgress = true;
        CanStart = false;

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

    /// <summary>
    /// F-12 / F-26: Risk-Strategy-Sticky — pinnt die uebergebene Strategie als
    /// <see cref="Workshop.DefaultRiskStrategy"/> dieses Workshops. Reduziert Choice-Fatigue
    /// bei 30-60 Auftraegen/Session. Wird per Long-Press oder dediziertem Pin-Button getriggert.
    /// </summary>
    [RelayCommand]
    private async Task PinDefaultStrategyAsync(string strategyName)
    {
        if (Order == null) return;
        if (!Enum.TryParse<OrderStrategy>(strategyName, ignoreCase: true, out var strat)) return;

        var workshop = _gameStateService.State.Workshops.FirstOrDefault(w => w.Type == Order.WorkshopType);
        if (workshop == null) return;

        workshop.DefaultRiskStrategy = strat;
        Order.Strategy = strat; // Aktueller Auftrag sofort uebernehmen
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);
    }

    [RelayCommand]
    private async Task CancelOrderAsync()
    {
        if (Order == null) return;

        // Bestaetigungsdialog via DialogService
        var confirmed = await _dialogService.ShowConfirmDialog(
            _localizationService.GetString("ConfirmCancelOrder"),
            _localizationService.GetString("ConfirmCancelOrderDesc"),
            _localizationService.GetString("YesCancel"),
            _localizationService.GetString("No"));

        if (confirmed)
        {
            _gameStateService.CancelActiveOrder();
            NavigationRequested?.Invoke("..");
        }
    }

    private void NavigateToMiniGame()
    {
        if (Order == null) return;

        // Aktuellen Task-Typ verwenden (korrekt für Cooperation, MasterSmith, InnovationLab)
        var currentTask = Order.CurrentTask;
        if (currentTask == null) return;

        var route = currentTask.GameType.GetRoute();
        NavigationRequested?.Invoke($"{route}?orderId={Order.Id}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private static string GetCustomerIcon(OrderDifficulty difficulty) => difficulty switch
    {
        OrderDifficulty.Easy => "Account",
        OrderDifficulty.Medium => "AccountTie",
        OrderDifficulty.Hard => "OfficeBuildingOutline",
        _ => "HardHat"
    };

    private static string GetWorkshopIcon(WorkshopType type) => type.GetIconKind();

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

    private static string FormatMoney(decimal amount) => MoneyFormatter.Format(amount, 1);
}
