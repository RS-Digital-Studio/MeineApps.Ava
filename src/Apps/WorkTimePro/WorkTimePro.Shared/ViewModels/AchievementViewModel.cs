using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;

namespace WorkTimePro.ViewModels;

/// <summary>
/// ViewModel für die Achievement/Badge-Übersicht.
/// Zeigt alle Achievements mit Fortschritt und Unlock-Status an.
/// </summary>
public partial class AchievementViewModel : ObservableObject
{
    private readonly IAchievementService _achievementService;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;

    public AchievementViewModel(IAchievementService achievementService)
    {
        _achievementService = achievementService;
    }

    // === Properties ===

    [ObservableProperty]
    private ObservableCollection<Achievement> _achievements = new();

    [ObservableProperty]
    private int _unlockedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private string _summaryText = "";

    [ObservableProperty]
    private bool _isLoading;

    // === Commands ===

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;

            var all = await _achievementService.GetAllAsync();
            Achievements = new ObservableCollection<Achievement>(all);

            TotalCount = all.Count;
            UnlockedCount = all.Count(a => a.IsUnlocked);
            SummaryText = $"{UnlockedCount}/{TotalCount}";
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorLoading, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }
}
