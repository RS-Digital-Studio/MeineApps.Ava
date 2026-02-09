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
/// ViewModel for month overview (Premium)
/// </summary>
public partial class MonthOverviewViewModel : ObservableObject
{
    private readonly ICalculationService _calculation;
    private readonly IDatabaseService _database;
    private readonly IPurchaseService _purchaseService;
    private readonly ITrialService _trialService;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;

    public MonthOverviewViewModel(
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
    private DateTime _selectedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private WorkMonth? _currentMonth;

    [ObservableProperty]
    private ObservableCollection<WorkWeek> _weeks = new();

    [ObservableProperty]
    private string _monthDisplay = "";

    [ObservableProperty]
    private string _workTimeDisplay = "0:00";

    [ObservableProperty]
    private string _targetTimeDisplay = "0:00";

    [ObservableProperty]
    private string _balanceDisplay = "+0:00";

    [ObservableProperty]
    private string _balanceColor = "#4CAF50";

    [ObservableProperty]
    private string _cumulativeBalanceDisplay = "+0:00";

    [ObservableProperty]
    private string _cumulativeBalanceColor = "#4CAF50";

    [ObservableProperty]
    private int _workedDays;

    [ObservableProperty]
    private int _targetDays;

    [ObservableProperty]
    private int _vacationDays;

    [ObservableProperty]
    private int _sickDays;

    [ObservableProperty]
    private int _holidayDays;

    [ObservableProperty]
    private bool _isLocked;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showAds = true;

    public string LockMonthButtonText => $"{Icons.Lock} {AppStrings.CloseMonth}";
    public string UnlockMonthButtonText => $"{Icons.LockOpen} {AppStrings.UnlockMonth}";

    // === Commands ===

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;

            CurrentMonth = await _calculation.CalculateMonthAsync(SelectedMonth.Year, SelectedMonth.Month);

            MonthDisplay = CurrentMonth.MonthDisplay;
            WorkTimeDisplay = CurrentMonth.ActualWorkDisplay;
            TargetTimeDisplay = CurrentMonth.TargetWorkDisplay;
            BalanceDisplay = CurrentMonth.BalanceDisplay;
            BalanceColor = CurrentMonth.BalanceColor;
            CumulativeBalanceDisplay = CurrentMonth.CumulativeBalanceDisplay;
            CumulativeBalanceColor = CurrentMonth.CumulativeBalanceColor;
            WorkedDays = CurrentMonth.WorkedDays;
            TargetDays = CurrentMonth.TargetWorkDays;
            VacationDays = CurrentMonth.VacationDays;
            SickDays = CurrentMonth.SickDays;
            HolidayDays = CurrentMonth.HolidayDays;
            IsLocked = CurrentMonth.IsLocked;

            // Generate weeks
            var weeksList = new List<WorkWeek>();
            var firstDay = new DateTime(SelectedMonth.Year, SelectedMonth.Month, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);

            var currentDate = firstDay;
            while (currentDate <= lastDay)
            {
                var week = await _calculation.CalculateWeekAsync(currentDate);
                if (!weeksList.Any(w => w.WeekNumber == week.WeekNumber && w.Year == week.Year))
                {
                    weeksList.Add(week);
                }
                currentDate = currentDate.AddDays(7);
            }

            Weeks = new ObservableCollection<WorkWeek>(weeksList);

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
    private async Task PreviousMonthAsync()
    {
        SelectedMonth = SelectedMonth.AddMonths(-1);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        SelectedMonth = SelectedMonth.AddMonths(1);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        SelectedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task LockMonthAsync()
    {
        if (CurrentMonth == null) return;

        await _database.LockMonthAsync(SelectedMonth.Year, SelectedMonth.Month);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task UnlockMonthAsync()
    {
        if (CurrentMonth == null) return;

        await _database.UnlockMonthAsync(SelectedMonth.Year, SelectedMonth.Month);
        await LoadDataAsync();
    }

    [RelayCommand]
    private void SelectWeek(WorkWeek? week)
    {
        if (week == null) return;
        // Navigate to week overview
        NavigationRequested?.Invoke("WeekOverviewPage");
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }
}
