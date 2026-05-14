using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Partial Class: Achievement-Dialog (v2.1.0 Aufspaltung ).
/// </summary>
public sealed partial class DialogViewModel
{
    [ObservableProperty]
    private bool _isAchievementDialogVisible;

    [ObservableProperty]
    private string _achievementName = "";

    [ObservableProperty]
    private string _achievementDescription = "";

    [RelayCommand]
    private void DismissAchievementDialog()
    {
        IsAchievementDialogVisible = false;
    }
}
