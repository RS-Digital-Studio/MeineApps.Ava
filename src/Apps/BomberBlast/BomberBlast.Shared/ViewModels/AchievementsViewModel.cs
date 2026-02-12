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
public partial class AchievementsViewModel : ObservableObject
{
    private readonly IAchievementService _achievementService;
    private readonly ILocalizationService _localizationService;

    public event Action<string>? NavigationRequested;

    [ObservableProperty]
    private string _titleText = "Achievements";

    [ObservableProperty]
    private string _progressText = "0/0";

    public ObservableCollection<AchievementItem> Items { get; } = [];

    public AchievementsViewModel(IAchievementService achievementService, ILocalizationService localizationService)
    {
        _achievementService = achievementService;
        _localizationService = localizationService;
    }

    public void OnAppearing()
    {
        TitleText = _localizationService.GetString("AchievementsTitle") ?? "Achievements";
        ProgressText = $"{_achievementService.UnlockedCount}/{_achievementService.TotalCount}";

        Items.Clear();
        foreach (var achievement in _achievementService.Achievements)
        {
            Items.Add(new AchievementItem
            {
                Name = _localizationService.GetString(achievement.NameKey) ?? achievement.Id,
                Description = _localizationService.GetString(achievement.DescriptionKey) ?? "",
                IsUnlocked = achievement.IsUnlocked,
                Progress = achievement.Progress,
                Target = achievement.Target,
                ProgressText = achievement.Target > 1
                    ? $"{achievement.Progress}/{achievement.Target}"
                    : (achievement.IsUnlocked ? "✓" : ""),
                IconName = achievement.IconName,
                CategoryName = achievement.Category.ToString()
            });
        }
    }

    [RelayCommand]
    private void Back() => NavigationRequested?.Invoke("..");
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
}
