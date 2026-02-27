using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Models;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für die Achievements-Ansicht
/// </summary>
public partial class AchievementsViewModel : ObservableObject, INavigable
{
    private readonly IAchievementService _achievementService;
    private readonly ILocalizationService _localizationService;

    public event Action<NavigationRequest>? NavigationRequested;

    [ObservableProperty]
    private string _titleText = "Achievements";

    [ObservableProperty]
    private string _progressText = "0/0";

    [ObservableProperty]
    private string _emptyStateText = "";

    public ObservableCollection<AchievementCategoryGroup> CategoryGroups { get; } = [];

    public AchievementsViewModel(IAchievementService achievementService, ILocalizationService localizationService)
    {
        _achievementService = achievementService;
        _localizationService = localizationService;
    }

    public void OnAppearing()
    {
        TitleText = _localizationService.GetString("AchievementsTitle") ?? "Achievements";
        ProgressText = $"{_achievementService.UnlockedCount}/{_achievementService.TotalCount}";

        CategoryGroups.Clear();

        // Nach Kategorie gruppieren
        var grouped = _achievementService.Achievements
            .GroupBy(a => a.Category)
            .OrderBy(g => (int)g.Key);

        foreach (var group in grouped)
        {
            var categoryGroup = new AchievementCategoryGroup
            {
                CategoryName = GetCategoryName(group.Key),
                CategoryIndex = (int)group.Key,
            };

            foreach (var achievement in group)
            {
                categoryGroup.Items.Add(new AchievementItem
                {
                    Name = _localizationService.GetString(achievement.NameKey) ?? achievement.Id,
                    Description = _localizationService.GetString(achievement.DescriptionKey) ?? "",
                    IsUnlocked = achievement.IsUnlocked,
                    Progress = achievement.Progress,
                    Target = achievement.Target,
                    ProgressText = achievement.Target > 1
                        ? $"{achievement.Progress}/{achievement.Target}"
                        : (achievement.IsUnlocked ? "\u2713" : ""),
                    IconName = achievement.IconName,
                    CategoryName = achievement.Category.ToString(),
                    CategoryIndex = (int)achievement.Category,
                    ProgressFraction = achievement.Target > 0
                        ? (float)achievement.Progress / achievement.Target
                        : 0f,
                    CoinReward = achievement.CoinReward,
                    HasCoinReward = achievement.CoinReward > 0,
                    CoinRewardText = achievement.CoinReward > 0
                        ? $"+{achievement.CoinReward:N0} {_localizationService.GetString("Coins") ?? "Coins"}"
                        : ""
                });
            }

            int unlocked = categoryGroup.Items.Count(i => i.IsUnlocked);
            categoryGroup.ProgressText = $"{unlocked}/{categoryGroup.Items.Count}";
            CategoryGroups.Add(categoryGroup);
        }
    }

    /// <summary>Lokalisierter Kategorie-Name</summary>
    private string GetCategoryName(AchievementCategory category) => category switch
    {
        AchievementCategory.Progress => _localizationService.GetString("CategoryProgress") ?? "Progress",
        AchievementCategory.Mastery => _localizationService.GetString("CategoryMastery") ?? "Mastery",
        AchievementCategory.Combat => _localizationService.GetString("CategoryCombat") ?? "Combat",
        AchievementCategory.Skill => _localizationService.GetString("CategorySkill") ?? "Skill",
        AchievementCategory.Challenge => _localizationService.GetString("CategoryChallenge") ?? "Challenge",
        _ => category.ToString()
    };

    [RelayCommand]
    private void Back() => NavigationRequested?.Invoke(new GoBack());
}

/// <summary>
/// Gruppierung von Achievements nach Kategorie (fuer Section-Headers)
/// </summary>
public class AchievementCategoryGroup
{
    public string CategoryName { get; set; } = "";
    public int CategoryIndex { get; set; }
    public string ProgressText { get; set; } = "";
    public ObservableCollection<AchievementItem> Items { get; } = [];

    /// <summary>Kategorie-Farbe (konsistent mit AchievementIconRenderer)</summary>
    public string CategoryColor => CategoryIndex switch
    {
        0 => "#4CAF50", // Progress - Grün
        1 => "#FFD700", // Mastery - Gold
        2 => "#F44336", // Combat - Rot
        3 => "#2196F3", // Skill - Blau
        4 => "#FF9800", // Challenge - Orange
        _ => "#888888"
    };
}

/// <summary>
/// Darstellungs-Modell für ein einzelnes Achievement in der Liste
/// </summary>
public class AchievementItem
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public bool IsUnlocked { get; init; }
    public int Progress { get; init; }
    public int Target { get; init; }
    public string ProgressText { get; init; } = "";
    public string IconName { get; init; } = "";
    public string CategoryName { get; init; } = "";
    public int CoinReward { get; init; }
    public bool HasCoinReward { get; init; }
    public string CoinRewardText { get; init; } = "";

    /// <summary>Kategorie-Index für AchievementIconRenderer (0-4)</summary>
    public int CategoryIndex { get; init; }

    /// <summary>Fortschritt als Float 0.0-1.0 für Ring-Anzeige</summary>
    public float ProgressFraction { get; init; }
}
