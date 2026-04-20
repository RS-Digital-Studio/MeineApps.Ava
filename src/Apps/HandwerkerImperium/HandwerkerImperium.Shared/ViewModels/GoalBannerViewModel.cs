using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel fuer das dynamische "Naechstes Ziel"-Banner auf dem Dashboard.
/// Liest den aktuellen GoalService-State und stellt ihn via Properties bereit.
/// Navigation erfolgt ueber den <see cref="INavigationService"/>.
/// Extrahiert aus MainViewModel (17.04.2026, Phase 3 Schritt 9).
/// </summary>
public sealed partial class GoalBannerViewModel : ViewModelBase
{
    private readonly IGoalService _goalService;
    private readonly INavigationService _navigationService;
    private string? _currentGoalRoute;

    [ObservableProperty]
    private string _currentGoalDescription = "";

    [ObservableProperty]
    private string _currentGoalReward = "";

    [ObservableProperty]
    private double _currentGoalProgress;

    [ObservableProperty]
    private string _currentGoalIcon = "TrendingUp";

    [ObservableProperty]
    private bool _hasCurrentGoal;

    public GoalBannerViewModel(IGoalService goalService, INavigationService navigationService)
    {
        _goalService = goalService;
        _navigationService = navigationService;
    }

    /// <summary>
    /// Aktualisiert das Banner vom GoalService. Wird von MainViewModel bei Zustandsaenderungen aufgerufen.
    /// </summary>
    public void Refresh()
    {
        var goal = _goalService.GetCurrentGoal();
        HasCurrentGoal = goal != null;
        if (goal != null)
        {
            CurrentGoalDescription = goal.Description;
            CurrentGoalReward = goal.RewardHint;
            CurrentGoalProgress = goal.Progress;
            CurrentGoalIcon = goal.IconKind;
            _currentGoalRoute = goal.NavigationRoute;
        }
    }

    /// <summary>Navigation zum Ziel (Banner-Klick).</summary>
    [RelayCommand]
    private void NavigateToGoal()
    {
        if (!string.IsNullOrEmpty(_currentGoalRoute))
            _navigationService.NavigateToRoute(_currentGoalRoute);
    }
}
