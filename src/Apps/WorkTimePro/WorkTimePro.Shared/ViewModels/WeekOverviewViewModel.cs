using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Premium.Ava.Services;
using WorkTimePro.Models;
using WorkTimePro.Helpers;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;

namespace WorkTimePro.ViewModels;

/// <summary>
/// ViewModel for week overview
/// </summary>
public partial class WeekOverviewViewModel : ObservableObject
{
    private readonly ICalculationService _calculation;
    private readonly IDatabaseService _database;
    private readonly IPurchaseService _purchaseService;
    private readonly ITrialService _trialService;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;

    public WeekOverviewViewModel(
        ICalculationService calculation,
        IDatabaseService database,
        IPurchaseService purchaseService,
        ITrialService trialService)
    {
        _calculation = calculation;
        _database = database;
        _purchaseService = purchaseService;
        _trialService = trialService;
    }

    // === Properties ===

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private WorkWeek? _currentWeek;

    [ObservableProperty]
    private WorkWeek? _previousWeek;

    [ObservableProperty]
    private ObservableCollection<WorkDay> _days = new();

    [ObservableProperty]
    private string _weekDisplay = "";

    [ObservableProperty]
    private string _dateRangeDisplay = "";

    [ObservableProperty]
    private string _workTimeDisplay = "0:00";

    [ObservableProperty]
    private string _targetTimeDisplay = "40:00";

    [ObservableProperty]
    private string _balanceDisplay = "+0:00";

    [ObservableProperty]
    private string _balanceColor = "#4CAF50";

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressText = "0%";

    [ObservableProperty]
    private int _workedDays;

    [ObservableProperty]
    private int _vacationDays;

    [ObservableProperty]
    private int _sickDays;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showAds = true;

    // Derived properties
    public bool HasVacationDays => VacationDays > 0;
    public bool HasSickDays => SickDays > 0;

    partial void OnVacationDaysChanged(int value) => OnPropertyChanged(nameof(HasVacationDays));
    partial void OnSickDaysChanged(int value) => OnPropertyChanged(nameof(HasSickDays));

    // Localized texts
    public string TodayButtonText => $"{Icons.CalendarToday} {AppStrings.Today}";

    // === Commands ===

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;

            // Load current week
            CurrentWeek = await _calculation.CalculateWeekAsync(SelectedDate);

            // Load previous week
            PreviousWeek = await _calculation.CalculateWeekAsync(SelectedDate.AddDays(-7));

            // Update UI
            WeekDisplay = CurrentWeek.WeekDisplay;
            DateRangeDisplay = CurrentWeek.DateRangeDisplay;
            WorkTimeDisplay = CurrentWeek.ActualWorkDisplay;
            TargetTimeDisplay = CurrentWeek.TargetWorkDisplay;
            BalanceDisplay = CurrentWeek.BalanceDisplay;
            BalanceColor = CurrentWeek.BalanceColor;
            ProgressPercent = CurrentWeek.ProgressPercent;
            ProgressText = $"{ProgressPercent:F0}%";
            WorkedDays = CurrentWeek.WorkedDays;
            VacationDays = CurrentWeek.VacationDays;
            SickDays = CurrentWeek.SickDays;

            Days = new ObservableCollection<WorkDay>(CurrentWeek.Days);

            // Premium status
            ShowAds = !_purchaseService.IsPremium && !_trialService.IsTrialActive;
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
    private async Task PreviousWeekAsync()
    {
        SelectedDate = SelectedDate.AddDays(-7);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task NextWeekAsync()
    {
        SelectedDate = SelectedDate.AddDays(7);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        SelectedDate = DateTime.Today;
        await LoadDataAsync();
    }

    [RelayCommand]
    private void SelectDay(WorkDay? day)
    {
        if (day == null) return;
        NavigationRequested?.Invoke($"DayDetailPage?date={day.Date:yyyy-MM-dd}");
    }
}
