using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the statistics page.
/// Displays comprehensive game statistics and player progress.
/// </summary>
public partial class StatisticsViewModel : ObservableObject
{
    private readonly IGameStateService _gameStateService;
    private readonly IPrestigeService _prestigeService;
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localizationService;
    private readonly IPurchaseService _purchaseService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Event to show an alert dialog. Parameters: title, message, buttonText.
    /// </summary>
    public event Action<string, string, string>? AlertRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // PLAYER STATISTICS
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _playerLevel;

    [ObservableProperty]
    private int _totalXp;

    [ObservableProperty]
    private string _totalPlayTime = "0h 0m";

    [ObservableProperty]
    private string _totalMoneyEarned = "0 €";

    [ObservableProperty]
    private string _totalMoneySpent = "0 €";

    [ObservableProperty]
    private string _currentBalance = "0 €";

    // ═══════════════════════════════════════════════════════════════════════
    // ORDERS & MINIGAMES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _totalOrdersCompleted;

    [ObservableProperty]
    private int _totalMiniGamesPlayed;

    [ObservableProperty]
    private int _perfectRatings;

    [ObservableProperty]
    private int _currentPerfectStreak;

    [ObservableProperty]
    private int _bestPerfectStreak;

    [ObservableProperty]
    private double _perfectRate;

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _prestigeLevel;

    [ObservableProperty]
    private string _prestigeMultiplier = "1.0x";

    [ObservableProperty]
    private bool _canPrestige;

    [ObservableProperty]
    private string _potentialBonus = "+0%";

    [ObservableProperty]
    private int _minimumPrestigeLevel;

    /// <summary>
    /// Localized text for minimum prestige level requirement.
    /// </summary>
    public string MinLevelRequiredText => string.Format(
        _localizationService.GetString("MinLevelRequired"),
        MinimumPrestigeLevel);

    // ═══════════════════════════════════════════════════════════════════════
    // WORKSHOPS
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private List<WorkshopStatistic> _workshopStats = [];

    [ObservableProperty]
    private int _totalWorkshops;

    [ObservableProperty]
    private int _totalWorkers;

    [ObservableProperty]
    private string _totalIncomePerSecond = "0 €/s";

    /// <summary>
    /// Whether there are any workshop stats to show.
    /// </summary>
    public bool HasWorkshopStats => WorkshopStats.Count > 0;

    partial void OnWorkshopStatsChanged(List<WorkshopStatistic> value) => OnPropertyChanged(nameof(HasWorkshopStats));

    /// <summary>
    /// Indicates whether ads should be shown (not premium).
    /// </summary>
    public bool ShowAds => !_purchaseService.IsPremium;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public StatisticsViewModel(
        IGameStateService gameStateService,
        IPrestigeService prestigeService,
        IAudioService audioService,
        ILocalizationService localizationService,
        IPurchaseService purchaseService)
    {
        _gameStateService = gameStateService;
        _prestigeService = prestigeService;
        _audioService = audioService;
        _localizationService = localizationService;
        _purchaseService = purchaseService;

        LoadStatistics();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    private void LoadStatistics()
    {
        var state = _gameStateService.State;

        // Player stats
        PlayerLevel = state.PlayerLevel;
        TotalXp = state.TotalXp;
        TotalPlayTime = FormatPlayTime(state.TotalPlayTimeSeconds);
        TotalMoneyEarned = FormatMoney(state.TotalMoneyEarned);
        TotalMoneySpent = FormatMoney(state.TotalMoneySpent);
        CurrentBalance = FormatMoney(state.Money);

        // Orders & Mini-games
        TotalOrdersCompleted = state.TotalOrdersCompleted;
        TotalMiniGamesPlayed = state.TotalMiniGamesPlayed;
        PerfectRatings = state.PerfectRatings;
        CurrentPerfectStreak = state.PerfectStreak;
        BestPerfectStreak = state.BestPerfectStreak;
        PerfectRate = state.TotalMiniGamesPlayed > 0
            ? (double)state.PerfectRatings / state.TotalMiniGamesPlayed * 100
            : 0;

        // Prestige (3-Tier System)
        PrestigeLevel = state.Prestige.TotalPrestigeCount;
        PrestigeMultiplier = $"{_prestigeService.GetPermanentMultiplier():F1}x";
        var highestTier = state.Prestige.GetHighestAvailableTier(state.PlayerLevel);
        CanPrestige = highestTier != PrestigeTier.None;
        MinimumPrestigeLevel = PrestigeTier.Bronze.GetRequiredLevel();
        var potentialPoints = _prestigeService.GetPrestigePoints(state.TotalMoneyEarned);
        PotentialBonus = $"+{potentialPoints} PP";

        // Workshops
        TotalWorkshops = state.Workshops.Count;
        TotalWorkers = state.Workshops.Sum(w => w.Workers.Count);
        TotalIncomePerSecond = FormatMoneyPerSecond(state.TotalIncomePerSecond);

        WorkshopStats = state.Workshops
            .OrderBy(w => w.Type.GetUnlockLevel())
            .Select(w => new WorkshopStatistic
            {
                Name = GetWorkshopName(w.Type),
                Icon = GetWorkshopIcon(w.Type),
                Level = w.Level,
                Workers = w.Workers.Count,
                IncomePerSecond = FormatMoneyPerSecond(w.IncomePerSecond)
            })
            .ToList();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    [RelayCommand]
    private void RefreshStatistics()
    {
        LoadStatistics();
    }

    /// <summary>
    /// Event to show the prestige dialog.
    /// </summary>
    public event EventHandler? ShowPrestigeDialog;

    [RelayCommand]
    private async Task PrestigeAsync()
    {
        var state = _gameStateService.State;
        var highestTier = state.Prestige.GetHighestAvailableTier(state.PlayerLevel);

        if (highestTier == PrestigeTier.None)
        {
            var title = _localizationService.GetString("PrestigeNotAvailable");
            var message = string.Format(
                _localizationService.GetString("PrestigeNotAvailableDesc"),
                PrestigeTier.Bronze.GetRequiredLevel(),
                state.PlayerLevel);

            AlertRequested?.Invoke(title, message, "OK");
            return;
        }

        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Fire event to show dialog - the page handles the actual dialog
        ShowPrestigeDialog?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Called after prestige is completed to refresh statistics.
    /// </summary>
    public async Task OnPrestigeCompletedAsync()
    {
        await _audioService.PlaySoundAsync(GameSound.LevelUp);
        LoadStatistics();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private static string FormatPlayTime(long seconds)
    {
        var timeSpan = TimeSpan.FromSeconds(seconds);
        if (timeSpan.TotalDays >= 1)
            return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h {timeSpan.Minutes}m";
        if (timeSpan.TotalHours >= 1)
            return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
        return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
    }

    private static string FormatMoney(decimal amount) => MoneyFormatter.Format(amount, 2);

    private static string FormatMoneyPerSecond(decimal amount) => MoneyFormatter.FormatPerSecond(amount);

    private string GetWorkshopName(WorkshopType type) =>
        _localizationService.GetString(type.GetLocalizationKey());

    private static string GetWorkshopIcon(WorkshopType type) => type switch
    {
        WorkshopType.Carpenter => "🪚",
        WorkshopType.Plumber => "🔧",
        WorkshopType.Electrician => "⚡",
        WorkshopType.Painter => "🎨",
        WorkshopType.Roofer => "🏠",
        WorkshopType.Contractor => "🏗️",
        _ => "🔨"
    };
}

// ═══════════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Represents statistics for a single workshop.
/// </summary>
public class WorkshopStatistic
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public int Level { get; set; }
    public int Workers { get; set; }
    public string IncomePerSecond { get; set; } = "";
}
