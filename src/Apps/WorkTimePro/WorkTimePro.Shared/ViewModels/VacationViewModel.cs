using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Async;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;
using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;

namespace WorkTimePro.ViewModels;

/// <summary>
/// Type for vacation type selection (replaces ValueTuple for bindability)
/// </summary>
public class VacationTypeItem
{
    public DayStatus Status { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// ViewModel for vacation management (Premium feature)
/// </summary>
public sealed partial class VacationViewModel : ViewModelBase, INavigationSource, IMessageSource, IDisposable
{
    private readonly IVacationService _vacationService;
    private readonly IHolidayService _holidayService;
    private readonly IPurchaseService _purchaseService;
    private readonly ITrialService _trialService;
    private readonly ILocalizationService _localization;
    private readonly IRewardedAdService _rewardedAdService;

    // Rewarded Ad Overlay
    [ObservableProperty]
    private bool _showRewardedAdOverlay;

    /// <summary>Aufgeschobene Aktion nach erfolgreicher Ad-Wiedergabe</summary>
    private Func<Task>? _pendingAction;

    [ObservableProperty]
    private int _selectedYear;

    [ObservableProperty]
    private VacationQuota? _quota;

    [ObservableProperty]
    private VacationStatistics? _statistics;

    [ObservableProperty]
    private ObservableCollection<VacationEntry> _vacationEntries = new();

    [ObservableProperty]
    private ObservableCollection<HolidayEntry> _holidays = new();

    [ObservableProperty]
    private bool _isPremium;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showAds = true;

    // Quota-Edit Overlay
    [ObservableProperty]
    private bool _isEditingQuota;

    [ObservableProperty]
    private int _editTotalDays;

    [ObservableProperty]
    private int _editCarryOverDays;


    // For new vacation
    [ObservableProperty]
    private DateTime _newStartDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _newEndDate = DateTime.Today;

    [ObservableProperty]
    private string _newNote = "";

    [ObservableProperty]
    private DayStatus _newType = DayStatus.Vacation;

    [ObservableProperty]
    private int _calculatedDays;

    // Derived properties
    public string CalculatedDaysDisplay => CalculatedDays.ToString();
    public bool HasNoVacations => VacationEntries.Count == 0;
    /// <summary>True wenn ein Overlay aktiv ist → ScrollViewer deaktiviert (kein Touch-Durchfall auf Android)</summary>
    public bool IsAnyOverlayVisible => IsEditingQuota || ShowRewardedAdOverlay;

    partial void OnCalculatedDaysChanged(int value) => OnPropertyChanged(nameof(CalculatedDaysDisplay));
    partial void OnIsEditingQuotaChanged(bool value) => OnPropertyChanged(nameof(IsAnyOverlayVisible));
    partial void OnShowRewardedAdOverlayChanged(bool value) => OnPropertyChanged(nameof(IsAnyOverlayVisible));

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;

    public VacationViewModel(
        IVacationService vacationService,
        IHolidayService holidayService,
        IPurchaseService purchaseService,
        ITrialService trialService,
        ILocalizationService localization,
        IRewardedAdService rewardedAdService)
    {
        _vacationService = vacationService;
        _holidayService = holidayService;
        _purchaseService = purchaseService;
        _trialService = trialService;
        _localization = localization;
        _rewardedAdService = rewardedAdService;

        // VM-komponierte Texte (VacationTypes-ComboBox) bei Sprachwechsel auffrischen —
        // die View ist gecacht, ohne Notify bliebe die alte Sprache stehen.
        _localization.LanguageChanged += OnLanguageChanged;

        SelectedYear = DateTime.Today.Year;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(VacationTypes)));
    }

    public void Dispose()
    {
        _localization.LanguageChanged -= OnLanguageChanged;
    }

    partial void OnSelectedYearChanged(int value)
    {
        LoadDataAsync().Forget(ex =>
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorLoading, ex.Message)));
    }

    partial void OnNewStartDateChanged(DateTime value)
    {
        if (value > NewEndDate)
            NewEndDate = value;
        CalculateWorkDaysAsync().Forget(ex =>
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorLoading, ex.Message)));
    }

    partial void OnNewEndDateChanged(DateTime value)
    {
        if (value < NewStartDate)
            NewStartDate = value;
        CalculateWorkDaysAsync().Forget(ex =>
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorLoading, ex.Message)));
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            IsPremium = _purchaseService.IsPremium || _trialService.IsTrialActive;
            ShowAds = !IsPremium;

            // Load statistics
            Statistics = await _vacationService.GetStatisticsAsync(SelectedYear);

            // Quota wird als SkiaSharp-Ring-Gauge (VacationQuotaGaugeVisualization) aus
            // Statistics gerendert — kein separater Display-String/ProgressBar mehr nötig.

            // Load vacation entries
            var entries = await _vacationService.GetVacationEntriesAsync(SelectedYear);
            VacationEntries = new ObservableCollection<VacationEntry>(entries);
            OnPropertyChanged(nameof(HasNoVacations));

            // Load holidays
            var holidays = await _holidayService.GetHolidaysAsync(SelectedYear);
            Holidays = new ObservableCollection<HolidayEntry>(holidays);
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
    private async Task CalculateWorkDaysAsync()
    {
        CalculatedDays = await _vacationService.CalculateWorkDaysAsync(NewStartDate, NewEndDate);
    }

    [RelayCommand]
    private async Task AddVacationAsync(bool skipPremiumCheck = false)
    {
        if (!skipPremiumCheck && !IsPremium)
        {
            // Soft-Paywall: Ad-Overlay anzeigen statt hart blockieren
            _pendingAction = () => AddVacationAsync(skipPremiumCheck: true);
            ShowRewardedAdOverlay = true;
            return;
        }

        if (CalculatedDays <= 0)
        {
            MessageRequested?.Invoke(AppStrings.Info, AppStrings.NoWorkDaysInPeriod);
            return;
        }

        var entry = new VacationEntry
        {
            Year = NewStartDate.Year,
            StartDate = NewStartDate,
            EndDate = NewEndDate,
            Days = CalculatedDays,
            Type = NewType,
            Note = string.IsNullOrWhiteSpace(NewNote) ? null : NewNote
        };

        await _vacationService.SaveVacationEntryAsync(entry);

        // Reset
        NewStartDate = DateTime.Today;
        NewEndDate = DateTime.Today;
        NewNote = "";
        NewType = DayStatus.Vacation;

        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task DeleteVacationAsync(VacationEntry entry)
    {
        await _vacationService.DeleteVacationEntryAsync(entry.Id);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task EditQuotaAsync(bool skipPremiumCheck = false)
    {
        if (!skipPremiumCheck && !IsPremium)
        {
            _pendingAction = () => EditQuotaAsync(skipPremiumCheck: true);
            ShowRewardedAdOverlay = true;
            return;
        }

        try
        {
            var quota = await _vacationService.GetQuotaAsync(SelectedYear);
            EditTotalDays = quota.TotalDays;
            EditCarryOverDays = quota.CarryOverDays;
            IsEditingQuota = true;
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorLoading, ex.Message));
        }
    }

    [RelayCommand]
    private async Task SaveQuotaAsync()
    {
        try
        {
            var quota = await _vacationService.GetQuotaAsync(SelectedYear);
            quota.TotalDays = Math.Max(0, EditTotalDays);
            quota.CarryOverDays = Math.Max(0, EditCarryOverDays);
            await _vacationService.SaveQuotaAsync(quota);
            IsEditingQuota = false;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorSaving, ex.Message));
        }
    }

    [RelayCommand]
    private void CancelEditQuota()
    {
        IsEditingQuota = false;
    }

    [RelayCommand]
    private async Task CarryOverDaysAsync(bool skipPremiumCheck = false)
    {
        if (!skipPremiumCheck && !IsPremium)
        {
            _pendingAction = () => CarryOverDaysAsync(skipPremiumCheck: true);
            ShowRewardedAdOverlay = true;
            return;
        }

        var previousYear = SelectedYear - 1;
        var transferred = await _vacationService.CarryOverRemainingDaysAsync(previousYear, SelectedYear);

        if (transferred > 0)
        {
            MessageRequested?.Invoke(AppStrings.Info, string.Format(AppStrings.DaysCarriedOver, transferred, previousYear));
            await LoadDataAsync();
        }
        else
        {
            MessageRequested?.Invoke(AppStrings.Info, string.Format(AppStrings.NoDaysToCarryOver, previousYear));
        }
    }

    [RelayCommand]
    private void PreviousYear()
    {
        SelectedYear--;
    }

    [RelayCommand]
    private void NextYear()
    {
        SelectedYear++;
    }

    [RelayCommand]
    private void GoToCurrentYear()
    {
        SelectedYear = DateTime.Today.Year;
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    // === Rewarded Ad Commands ===

    [RelayCommand]
    private async Task WatchAdAsync()
    {
        ShowRewardedAdOverlay = false;
        var success = await _rewardedAdService.ShowAdAsync("vacation_entry");
        if (success && _pendingAction != null)
        {
            await _pendingAction();
        }
        _pendingAction = null;
    }

    [RelayCommand]
    private void CancelAdOverlay()
    {
        ShowRewardedAdOverlay = false;
        _pendingAction = null;
    }

    public List<VacationTypeItem> VacationTypes => new()
    {
        new() { Status = DayStatus.Vacation, Name = AppStrings.Vacation },
        new() { Status = DayStatus.Sick, Name = AppStrings.Illness },
        new() { Status = DayStatus.SpecialLeave, Name = AppStrings.SpecialLeave },
        new() { Status = DayStatus.UnpaidLeave, Name = AppStrings.UnpaidLeave },
        new() { Status = DayStatus.OvertimeCompensation, Name = AppStrings.OvertimeCompensation }
    };
}
