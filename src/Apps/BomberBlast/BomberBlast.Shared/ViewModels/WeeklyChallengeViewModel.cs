using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Models;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für die wöchentlichen Herausforderungen.
/// Zeigt 5 Missionen mit Fortschritt, Bonus-Bereich und Countdown bis Reset.
/// </summary>
public partial class WeeklyChallengeViewModel : ObservableObject, INavigable, IGameJuiceEmitter
{
    private readonly IWeeklyChallengeService _weeklyService;
    private readonly IDailyMissionService _dailyMissionService;
    private readonly ICoinService _coinService;
    private readonly ILocalizationService _localizationService;
    private readonly IAchievementService _achievementService;

    public event Action<NavigationRequest>? NavigationRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    // Lokalisierte Texte
    [ObservableProperty]
    private string _weeklyTitle = "";

    [ObservableProperty]
    private string _completedCountText = "";

    [ObservableProperty]
    private string _timeRemainingText = "";

    [ObservableProperty]
    private string _bonusText = "";

    [ObservableProperty]
    private string _totalWeeksText = "";

    [ObservableProperty]
    private string _totalWeeksLabel = "";

    [ObservableProperty]
    private string _bonusLabel = "";

    [ObservableProperty]
    private string _timeRemainingLabel = "";

    [ObservableProperty]
    private bool _isAllComplete;

    [ObservableProperty]
    private bool _canClaimBonus;

    // Daily-Mission Properties
    [ObservableProperty]
    private string _dailyTitle = "";

    [ObservableProperty]
    private string _dailyCompletedCountText = "";

    [ObservableProperty]
    private string _dailyTimeRemainingText = "";

    [ObservableProperty]
    private string _dailyBonusText = "";

    [ObservableProperty]
    private string _dailyTotalDaysText = "";

    [ObservableProperty]
    private string _dailyTotalDaysLabel = "";

    [ObservableProperty]
    private string _dailyBonusLabel = "";

    [ObservableProperty]
    private string _dailyTimeRemainingLabel = "";

    [ObservableProperty]
    private bool _isDailyAllComplete;

    [ObservableProperty]
    private bool _canClaimDailyBonus;

    public ObservableCollection<WeeklyMissionDisplayItem> MissionItems { get; } = [];
    public ObservableCollection<WeeklyMissionDisplayItem> DailyMissionItems { get; } = [];

    public WeeklyChallengeViewModel(
        IWeeklyChallengeService weeklyService,
        IDailyMissionService dailyMissionService,
        ICoinService coinService,
        ILocalizationService localizationService,
        IAchievementService achievementService)
    {
        _weeklyService = weeklyService;
        _dailyMissionService = dailyMissionService;
        _coinService = coinService;
        _localizationService = localizationService;
        _achievementService = achievementService;
    }

    public void OnAppearing()
    {
        UpdateLocalizedTexts();
        UpdateMissions();
        UpdateStats();
        UpdateDailyMissions();
        UpdateDailyStats();
    }

    [RelayCommand]
    private void ClaimBonus()
    {
        int bonus = _weeklyService.ClaimAllCompleteBonus();
        if (bonus > 0)
        {
            _coinService.AddCoins(bonus);

            // Achievement: Alle 5 Weekly-Missionen einer Woche abgeschlossen
            _achievementService.OnWeeklyWeekCompleted();

            FloatingTextRequested?.Invoke($"+{bonus:N0} Bonus!", "gold");
            CelebrationRequested?.Invoke();
            UpdateStats();
        }
    }

    [RelayCommand]
    private void ClaimDailyBonus()
    {
        int bonus = _dailyMissionService.ClaimAllCompleteBonus();
        if (bonus > 0)
        {
            _coinService.AddCoins(bonus);
            FloatingTextRequested?.Invoke($"+{bonus:N0} Bonus!", "gold");
            CelebrationRequested?.Invoke();
            UpdateDailyStats();
        }
    }

    [RelayCommand]
    private void Back()
    {
        NavigationRequested?.Invoke(new GoBack());
    }

    private void UpdateMissions()
    {
        MissionItems.Clear();
        foreach (var mission in _weeklyService.Missions)
        {
            var name = _localizationService.GetString(mission.NameKey) ?? mission.NameKey;
            var descTemplate = _localizationService.GetString(mission.DescriptionKey) ?? "{0}";
            var desc = string.Format(descTemplate, mission.TargetCount);

            MissionItems.Add(new WeeklyMissionDisplayItem
            {
                Name = name,
                Description = desc,
                ProgressText = $"{mission.CurrentCount}/{mission.TargetCount}",
                Progress = mission.Progress,
                IsCompleted = mission.IsCompleted,
                RewardText = $"+{mission.CoinReward:N0}",
                IconKind = GetIconForType(mission.Type),
                AccentColor = GetColorForType(mission.Type),
            });
        }
    }

    private void UpdateStats()
    {
        IsAllComplete = _weeklyService.IsAllComplete;
        CanClaimBonus = _weeklyService.IsAllComplete && !_weeklyService.IsBonusClaimed;

        CompletedCountText = $"{_weeklyService.CompletedCount}/{_weeklyService.Missions.Count}";
        TotalWeeksText = _weeklyService.TotalWeeksCompleted.ToString();

        // Verbleibende Zeit bis Reset
        var remaining = _weeklyService.NextResetDate - DateTime.UtcNow;
        if (remaining.TotalDays >= 1)
            TimeRemainingText = $"{(int)remaining.TotalDays}d {remaining.Hours}h";
        else
            TimeRemainingText = $"{remaining.Hours}h {remaining.Minutes}m";

        BonusText = $"+{_weeklyService.AllCompleteBonusCoins:N0} Coins";
    }

    private void UpdateLocalizedTexts()
    {
        WeeklyTitle = _localizationService.GetString("WeeklyChallengeTitle") ?? "Weekly Challenges";
        TotalWeeksLabel = _localizationService.GetString("WeeklyTotalWeeks") ?? "Completed Weeks";
        BonusLabel = _localizationService.GetString("WeeklyBonusLabel") ?? "All Complete Bonus";
        TimeRemainingLabel = _localizationService.GetString("WeeklyTimeRemaining") ?? "Time Remaining";
        DailyTitle = _localizationService.GetString("DailyMissionsTitle") ?? "Daily Missions";
        DailyTotalDaysLabel = _localizationService.GetString("DailyMissionsCompleted") ?? "Completed Days";
        DailyBonusLabel = _localizationService.GetString("DailyBonusLabel") ?? "All Complete Bonus";
        DailyTimeRemainingLabel = _localizationService.GetString("DailyTimeRemaining") ?? "Reset in";
    }

    private static string GetIconForType(WeeklyMissionType type) => type switch
    {
        WeeklyMissionType.CompleteLevels => "FlagCheckered",
        WeeklyMissionType.DefeatEnemies => "Sword",
        WeeklyMissionType.CollectPowerUps => "Lightning",
        WeeklyMissionType.EarnCoins => "CircleMultiple",
        WeeklyMissionType.SurvivalKills => "Skull",
        WeeklyMissionType.UseSpecialBombs => "Bomb",
        WeeklyMissionType.AchieveCombo => "Fire",
        WeeklyMissionType.WinBossFights => "Crown",
        WeeklyMissionType.CompleteDungeonFloors => "ShieldSword",
        WeeklyMissionType.CollectCards => "CardsPlaying",
        WeeklyMissionType.EarnGems => "DiamondStone",
        WeeklyMissionType.PlayQuickPlay => "FlashAlert",
        WeeklyMissionType.SpinLuckyWheel => "Sync",
        WeeklyMissionType.UpgradeCards => "ArrowUpBold",
        _ => "Star"
    };

    private static string GetColorForType(WeeklyMissionType type) => type switch
    {
        WeeklyMissionType.CompleteLevels => "#4CAF50",
        WeeklyMissionType.DefeatEnemies => "#F44336",
        WeeklyMissionType.CollectPowerUps => "#FF9800",
        WeeklyMissionType.EarnCoins => "#FFD700",
        WeeklyMissionType.SurvivalKills => "#B71C1C",
        WeeklyMissionType.UseSpecialBombs => "#2196F3",
        WeeklyMissionType.AchieveCombo => "#FF6B00",
        WeeklyMissionType.WinBossFights => "#9C27B0",
        WeeklyMissionType.CompleteDungeonFloors => "#4A148C",
        WeeklyMissionType.CollectCards => "#1565C0",
        WeeklyMissionType.EarnGems => "#00BCD4",
        WeeklyMissionType.PlayQuickPlay => "#E91E63",
        WeeklyMissionType.SpinLuckyWheel => "#FF5722",
        WeeklyMissionType.UpgradeCards => "#00897B",
        _ => "#607D8B"
    };

    private void UpdateDailyMissions()
    {
        DailyMissionItems.Clear();
        foreach (var mission in _dailyMissionService.Missions)
        {
            var name = _localizationService.GetString(mission.NameKey) ?? mission.NameKey;
            var descTemplate = _localizationService.GetString(mission.DescriptionKey) ?? "{0}";
            var desc = string.Format(descTemplate, mission.TargetCount);

            DailyMissionItems.Add(new WeeklyMissionDisplayItem
            {
                Name = name,
                Description = desc,
                ProgressText = $"{mission.CurrentCount}/{mission.TargetCount}",
                Progress = mission.Progress,
                IsCompleted = mission.IsCompleted,
                RewardText = $"+{mission.CoinReward:N0}",
                IconKind = GetIconForType(mission.Type),
                AccentColor = GetColorForType(mission.Type),
            });
        }
    }

    private void UpdateDailyStats()
    {
        IsDailyAllComplete = _dailyMissionService.IsAllComplete;
        CanClaimDailyBonus = _dailyMissionService.IsAllComplete && !_dailyMissionService.IsBonusClaimed;

        DailyCompletedCountText = $"{_dailyMissionService.CompletedCount}/{_dailyMissionService.Missions.Count}";
        DailyTotalDaysText = _dailyMissionService.TotalDaysCompleted.ToString();

        // Verbleibende Zeit bis Reset
        var remaining = _dailyMissionService.NextResetDate - DateTime.UtcNow;
        DailyTimeRemainingText = $"{remaining.Hours}h {remaining.Minutes}m";

        DailyBonusText = $"+{_dailyMissionService.AllCompleteBonusCoins:N0} Coins";
    }
}

/// <summary>
/// Display-Item für eine wöchentliche Mission in der View
/// </summary>
public class WeeklyMissionDisplayItem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ProgressText { get; set; } = "";
    public float Progress { get; set; }
    public bool IsCompleted { get; set; }
    public string RewardText { get; set; } = "";
    public string IconKind { get; set; } = "Star";
    public string AccentColor { get; set; } = "#607D8B";

    /// <summary>Fortschrittsbalken-Breite als Prozent-String für Width-Binding</summary>
    public double ProgressBarWidth => Progress * 100;
    public bool IsNotCompleted => !IsCompleted;
}
