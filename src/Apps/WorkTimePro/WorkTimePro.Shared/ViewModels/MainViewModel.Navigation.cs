using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Async;
using MeineApps.Core.Ava.ViewModels;
using WorkTimePro.Resources.Strings;

namespace WorkTimePro.ViewModels;

/// <summary>
/// MainViewModel — Tab- und Sub-Page-Navigation (extrahiert aus MainViewModel.cs).
/// Hält die Wiring-Liste für typed Navigation/Message-Sources, die Tab-Indizes
/// und die Sub-Page-Routenbehandlung. Tab-Datenladen weiterhin in MainViewModel.cs.
/// </summary>
public sealed partial class MainViewModel
{
    // === Tab Navigation ===

    [ObservableProperty]
    private int _currentTab;

    public bool IsTodayActive => CurrentTab == 0;
    public bool IsWeekActive => CurrentTab == 1;
    public bool IsCalendarActive => CurrentTab == 2;
    public bool IsStatisticsActive => CurrentTab == 3;
    public bool IsSettingsActive => CurrentTab == 4;

    partial void OnCurrentTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsTodayActive));
        OnPropertyChanged(nameof(IsWeekActive));
        OnPropertyChanged(nameof(IsCalendarActive));
        OnPropertyChanged(nameof(IsStatisticsActive));
        OnPropertyChanged(nameof(IsSettingsActive));

        // Daten für den jeweiligen Tab neu laden (zentraler Forget-Helper)
        LoadTabDataAsync(value).Forget(ex =>
            MessageRequested?.Invoke(
                AppStrings.Error,
                ex.Message ?? AppStrings.ErrorLoading ?? "Tab load failed"));
    }

    [RelayCommand]
    private void SelectTodayTab() => CurrentTab = 0;

    [RelayCommand]
    private void SelectWeekTab() => CurrentTab = 1;

    [RelayCommand]
    private void SelectCalendarTab() => CurrentTab = 2;

    [RelayCommand]
    private void SelectStatisticsTab() => CurrentTab = 3;

    [RelayCommand]
    private void SelectSettingsTab() => CurrentTab = 4;

    // === Sub-Page Wiring (typed, kein Reflection) ===

    private void WireSubPageNavigation(ObservableObject vm)
    {
        // Typed Wiring statt Reflection: Compile-time-sicher, Refactor-fest, kein GetEvent-Overhead.
        if (vm is INavigationSource navSource)
        {
            Action<string> handler = HandleNavigation;
            navSource.NavigationRequested += handler;
            _navHandlers.Add((navSource, handler));
        }

        if (vm is IMessageSource msgSource)
        {
            Action<string, string> handler = (title, msg) => MessageRequested?.Invoke(title, msg);
            msgSource.MessageRequested += handler;
            _msgHandlers.Add((msgSource, handler));
        }
    }

    // === Route-Behandlung ===

    /// <summary>
    /// Zentrale Navigations-Behandlung für alle Sub-Page-Routes.
    /// async void ist hier erforderlich (Event-Signatur), Fehler werden zentral gemeldet.
    /// </summary>
    private async void HandleNavigation(string route)
    {
        try
        {
            // Zurück-Navigation
            if (route == ".." || route.Contains("back", StringComparison.OrdinalIgnoreCase))
            {
                GoBack();
                return;
            }

            // DayDetail-Navigation (z.B. "DayDetailPage?date=2026-02-13")
            if (route.StartsWith("DayDetailPage", StringComparison.OrdinalIgnoreCase))
            {
                var dateParam = route.Split("date=", StringSplitOptions.RemoveEmptyEntries);
                // InvariantCulture + RoundtripKind: ISO-Routen sind locale-unabhängig (Pflicht laut Haupt-CLAUDE.md)
                if (dateParam.Length > 1 && DateTime.TryParse(dateParam[1], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var date))
                {
                    DayDetailVm.SelectedDate = date;
                }
                CloseAllSubPages();
                IsDayDetailActive = true;
                OnPropertyChanged(nameof(IsSubPageActive));
                await DayDetailVm.LoadDataAsync();
                return;
            }

            // MonthOverview-Navigation (z.B. "month?date=2026-02-01" von YearOverview)
            if (route.StartsWith("month", StringComparison.OrdinalIgnoreCase))
            {
                var dateParam = route.Split("date=", StringSplitOptions.RemoveEmptyEntries);
                // InvariantCulture + RoundtripKind: ISO-Routen sind locale-unabhängig (Pflicht laut Haupt-CLAUDE.md)
                if (dateParam.Length > 1 && DateTime.TryParse(dateParam[1], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var monthDate))
                {
                    MonthVm.SelectedMonth = new DateTime(monthDate.Year, monthDate.Month, 1);
                }
                CloseAllSubPages();
                IsMonthActive = true;
                OnPropertyChanged(nameof(IsSubPageActive));
                await MonthVm.LoadDataAsync();
                return;
            }

            // WeekOverview-Navigation (von MonthOverview)
            if (route.StartsWith("WeekOverviewPage", StringComparison.OrdinalIgnoreCase))
            {
                // Zur Wochen-Ansicht wechseln (Tab 1)
                CloseAllSubPages();
                CurrentTab = 1;
                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in HandleNavigation: {ex}");
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
        }
    }

    // === Sub-Page Active-Flags + Navigation-Commands ===

    [ObservableProperty]
    private bool _isDayDetailActive;

    [ObservableProperty]
    private bool _isMonthActive;

    [ObservableProperty]
    private bool _isYearActive;

    [ObservableProperty]
    private bool _isVacationActive;

    [ObservableProperty]
    private bool _isShiftPlanActive;

    public bool IsSubPageActive => IsDayDetailActive || IsMonthActive || IsYearActive || IsVacationActive || IsShiftPlanActive;

    [RelayCommand]
    private async Task NavigateToDayDetailAsync()
    {
        // Immer den heutigen Tag anzeigen wenn von TodayView aus navigiert
        DayDetailVm.SelectedDate = DateTime.Today;
        CloseAllSubPages();
        IsDayDetailActive = true;
        OnPropertyChanged(nameof(IsSubPageActive));
        await DayDetailVm.LoadDataAsync();
    }

    [RelayCommand]
    private async Task NavigateToMonthAsync()
    {
        CloseAllSubPages();
        IsMonthActive = true;
        OnPropertyChanged(nameof(IsSubPageActive));
        await MonthVm.LoadDataAsync();
    }

    [RelayCommand]
    private async Task NavigateToYearAsync()
    {
        CloseAllSubPages();
        IsYearActive = true;
        OnPropertyChanged(nameof(IsSubPageActive));
        await YearVm.LoadDataAsync();
    }

    [RelayCommand]
    private async Task NavigateToVacationAsync()
    {
        CloseAllSubPages();
        IsVacationActive = true;
        OnPropertyChanged(nameof(IsSubPageActive));
        await VacationVm.LoadDataAsync();
    }

    [RelayCommand]
    private async Task NavigateToShiftPlanAsync()
    {
        CloseAllSubPages();
        IsShiftPlanActive = true;
        OnPropertyChanged(nameof(IsSubPageActive));
        await ShiftPlanVm.LoadDataAsync();
    }

    [RelayCommand]
    public void GoBack()
    {
        CloseAllSubPages();
    }

    private void CloseAllSubPages()
    {
        IsDayDetailActive = false;
        IsMonthActive = false;
        IsYearActive = false;
        IsVacationActive = false;
        IsShiftPlanActive = false;
        OnPropertyChanged(nameof(IsSubPageActive));
    }
}
