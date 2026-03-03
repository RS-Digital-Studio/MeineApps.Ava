using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the achievements page.
/// </summary>
public partial class AchievementsViewModel : ViewModelBase
{
    private readonly IAchievementService _achievementService;
    private readonly ILocalizationService _localizationService;
    private readonly IAudioService _audioService;
    private readonly IRewardedAdService _rewardedAdService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Event to show an alert dialog. Parameters: title, message, buttonText.
    /// </summary>
    public event Action<string, string, string>? AlertRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<AchievementDisplayModel> _achievements = [];

    [ObservableProperty]
    private int _unlockedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private string _achievementsCompletedText = "";

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private AchievementCategory _selectedCategory = AchievementCategory.Orders;

    public AchievementsViewModel(
        IAchievementService achievementService,
        ILocalizationService localizationService,
        IAudioService audioService,
        IRewardedAdService rewardedAdService)
    {
        _achievementService = achievementService;
        _localizationService = localizationService;
        _audioService = audioService;
        _rewardedAdService = rewardedAdService;

        LoadAchievements();
    }

    public void LoadAchievements()
    {
        var allAchievements = _achievementService.GetAllAchievements();

        UnlockedCount = _achievementService.UnlockedCount;
        TotalCount = _achievementService.TotalCount;
        ProgressText = $"{UnlockedCount}/{TotalCount}";
        AchievementsCompletedText = string.Format(
            _localizationService.GetString("AchievementsCompleted") ?? "{0} completed",
            ProgressText);
        OverallProgress = TotalCount > 0 ? (double)UnlockedCount / TotalCount : 0;

        Achievements.Clear();
        foreach (var achievement in allAchievements)
        {
            Achievements.Add(new AchievementDisplayModel
            {
                Id = achievement.Id,
                Icon = achievement.Icon,
                Title = _localizationService.GetString(achievement.TitleKey) ?? achievement.TitleFallback,
                Description = _localizationService.GetString(achievement.DescriptionKey) ?? achievement.DescriptionFallback,
                Category = achievement.Category,
                Progress = achievement.Progress,
                ProgressFraction = achievement.ProgressFraction,
                ProgressText = $"{achievement.CurrentValue}/{achievement.TargetValue}",
                MoneyReward = achievement.MoneyReward,
                XpReward = achievement.XpReward,
                IsUnlocked = achievement.IsUnlocked,
                IsCloseToUnlock = achievement.IsCloseToUnlock,
                HasUsedAdBoost = achievement.HasUsedAdBoost
            });
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    [RelayCommand]
    private void FilterByCategory(AchievementCategory category)
    {
        SelectedCategory = category;
        // Filtering can be done in the UI with ItemsRepeater/ListBox
    }

    [RelayCommand]
    private async Task BoostAchievementAsync(AchievementDisplayModel? achievement)
    {
        if (achievement == null || !achievement.CanBoost) return;

        var success = await _rewardedAdService.ShowAdAsync("achievement_boost");
        if (success)
        {
            _achievementService.BoostAchievement(achievement.Id, 0.20);
            LoadAchievements();

            AlertRequested?.Invoke(
                _localizationService.GetString("AchievementBoostedFormat"),
                achievement.Title,
                _localizationService.GetString("Great"));
        }
    }
}

/// <summary>
/// Display model for achievements in the UI.
/// </summary>
public class AchievementDisplayModel
{
    public string Id { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public AchievementCategory Category { get; set; }
    public double Progress { get; set; }
    public double ProgressFraction { get; set; }
    public string ProgressText { get; set; } = "";
    public decimal MoneyReward { get; set; }
    public int XpReward { get; set; }
    public bool IsUnlocked { get; set; }
    public bool IsCloseToUnlock { get; set; }
    public bool HasUsedAdBoost { get; set; }

    /// <summary>
    /// Ob ein Ad-Boost moeglich ist: Nicht freigeschaltet, noch kein Boost genutzt, Fortschritt > 0.
    /// </summary>
    public bool CanBoost => !IsUnlocked && !HasUsedAdBoost && Progress > 0;

    public string RewardText => MoneyReward > 0 && XpReward > 0
        ? $"+{MoneyReward:N0}€ +{XpReward} XP"
        : MoneyReward > 0
            ? $"+{MoneyReward:N0}€"
            : $"+{XpReward} XP";

    /// <summary>
    /// Background color for the achievement icon area.
    /// Gold for unlocked, grey for locked.
    /// </summary>
    public string IconBackground => IsUnlocked ? "#40FFD700" : "#20808080";

    /// <summary>
    /// Color for the achievement title.
    /// Gold for unlocked, default for locked.
    /// </summary>
    public string TitleColor => IsUnlocked ? "#FFD700" : "#AAAAAA";
}
