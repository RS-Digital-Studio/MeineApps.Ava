using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanzRechner.Helpers;
using FinanzRechner.Models;
using FinanzRechner.Services;
using MeineApps.Core.Ava.Localization;

namespace FinanzRechner.ViewModels;

public partial class BudgetsViewModel : ObservableObject, IDisposable
{
    private readonly IExpenseService _expenseService;
    private readonly ILocalizationService _localizationService;
    private readonly INotificationService _notificationService;

    // Thread-safe Collection zum Tracken benachrichtigter Budgets
    private static readonly ConcurrentDictionary<string, DateTime> _notifiedBudgets = new();
    private static readonly object _cleanupLock = new();

    public event Action<string, string>? MessageRequested;

    public BudgetsViewModel(IExpenseService expenseService, ILocalizationService localizationService, INotificationService notificationService)
    {
        _expenseService = expenseService;
        _localizationService = localizationService;
        _notificationService = notificationService;
    }

    #region Localized Text Properties

    public string BudgetLimitsText => _localizationService.GetString("BudgetLimits") ?? "Budget Limits";
    public string NoBudgetsText => _localizationService.GetString("EmptyBudgetsTitle") ?? "No Budgets";
    public string NoBudgetsHintText => _localizationService.GetString("EmptyBudgetsDesc") ?? "Set monthly spending limits for your categories";
    public string ExceededText => _localizationService.GetString("Exceeded") ?? "Exceeded";
    public string WarningLabelText => _localizationService.GetString("Warning") ?? "Warning";
    public string SpentText => _localizationService.GetString("Spent") ?? "Spent";
    public string RemainingText => _localizationService.GetString("Remaining") ?? "Remaining";
    public string MonthlyLimitText => _localizationService.GetString("MonthlyLimit") ?? "Monthly Limit";
    public string SetBudgetText => _localizationService.GetString("SetBudget") ?? "Set Budget";
    public string CategoryText => _localizationService.GetString("Category") ?? "Category";
    public string MonthlyLimitEuroText => _localizationService.GetString("MonthlyLimitEuro") ?? "Monthly Limit (\u20ac)";
    public string WarningThresholdText => _localizationService.GetString("WarningThreshold") ?? "Warning Threshold (%)";
    public string WarningThresholdHintText => _localizationService.GetString("WarningThresholdHint") ?? "A warning will be displayed at this percentage.";
    public string CancelText => _localizationService.GetString("Cancel") ?? "Cancel";
    public string SaveText => _localizationService.GetString("Save") ?? "Save";
    public string UndoText => _localizationService.GetString("Undo") ?? "Undo";
    public string TotalBudgetText => _localizationService.GetString("TotalBudget") ?? "Total Budget";
    public string TotalBudgetSpentDisplay => CurrencyHelper.Format(TotalBudgetSpent);
    public string TotalBudgetLimitDisplay => CurrencyHelper.Format(TotalBudgetLimit);

    public void UpdateLocalizedTexts()
    {
        OnPropertyChanged(nameof(BudgetLimitsText));
        OnPropertyChanged(nameof(NoBudgetsText));
        OnPropertyChanged(nameof(NoBudgetsHintText));
        OnPropertyChanged(nameof(ExceededText));
        OnPropertyChanged(nameof(WarningLabelText));
        OnPropertyChanged(nameof(SpentText));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(MonthlyLimitText));
        OnPropertyChanged(nameof(SetBudgetText));
        OnPropertyChanged(nameof(CategoryText));
        OnPropertyChanged(nameof(MonthlyLimitEuroText));
        OnPropertyChanged(nameof(WarningThresholdText));
        OnPropertyChanged(nameof(WarningThresholdHintText));
        OnPropertyChanged(nameof(CancelText));
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(UndoText));
        OnPropertyChanged(nameof(TotalBudgetText));
        OnPropertyChanged(nameof(TotalBudgetSpentDisplay));
        OnPropertyChanged(nameof(TotalBudgetLimitDisplay));
    }

    #endregion

    #region Navigation Events

    public event Action<string>? NavigationRequested;
    private void NavigateTo(string route) => NavigationRequested?.Invoke(route);

    #endregion

    [ObservableProperty] private ObservableCollection<BudgetStatus> _budgetStatuses = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasBudgets;

    // Undo-Löschen
    [ObservableProperty] private bool _showUndoDelete;
    [ObservableProperty] private string _undoMessage = string.Empty;
    private Budget? _deletedBudget;
    private CancellationTokenSource? _undoCancellation;

    // Für neues/editiertes Budget
    [ObservableProperty] private bool _showAddBudget;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private ExpenseCategory _selectedCategory;
    [ObservableProperty] private double _monthlyLimit = 500;
    [ObservableProperty] private double _warningThreshold = 80;

    // Gesamt-Monatsbudget
    [ObservableProperty] private double _totalBudgetLimit;
    [ObservableProperty] private double _totalBudgetSpent;
    [ObservableProperty] private double _totalBudgetPercentage;
    [ObservableProperty] private bool _hasTotalBudget;

    partial void OnTotalBudgetSpentChanged(double value)
        => OnPropertyChanged(nameof(TotalBudgetSpentDisplay));

    partial void OnTotalBudgetLimitChanged(double value)
        => OnPropertyChanged(nameof(TotalBudgetLimitDisplay));

    public List<ExpenseCategory> AvailableCategories { get; } = new()
    {
        ExpenseCategory.Food,
        ExpenseCategory.Transport,
        ExpenseCategory.Housing,
        ExpenseCategory.Entertainment,
        ExpenseCategory.Shopping,
        ExpenseCategory.Health,
        ExpenseCategory.Education,
        ExpenseCategory.Bills,
        ExpenseCategory.Other
    };

    [RelayCommand]
    private async Task LoadBudgetsAsync()
    {
        IsLoading = true;

        // ExpenseService sicherstellen dass initialisiert
        await _expenseService.InitializeAsync();

        var statuses = await _expenseService.GetAllBudgetStatusAsync();
        BudgetStatuses.Clear();
        foreach (var status in statuses.OrderByDescending(s => s.PercentageUsed))
        {
            BudgetStatuses.Add(status);
        }

        HasBudgets = BudgetStatuses.Count > 0;

        // Gesamt-Monatsbudget berechnen
        if (HasBudgets)
        {
            TotalBudgetLimit = statuses.Sum(s => s.Limit);
            TotalBudgetSpent = statuses.Sum(s => s.Spent);
            TotalBudgetPercentage = TotalBudgetLimit > 0
                ? Math.Min(100, TotalBudgetSpent / TotalBudgetLimit * 100)
                : 0;
            HasTotalBudget = true;
        }
        else
        {
            TotalBudgetLimit = 0;
            TotalBudgetSpent = 0;
            TotalBudgetPercentage = 0;
            HasTotalBudget = false;
        }

        IsLoading = false;

        // Budget-Warnungen prüfen (80% oder 100%)
        await CheckBudgetAlertsAsync(statuses);
    }

    /// <summary>
    /// Prüft Budgets und sendet Benachrichtigungen bei 80%- oder 100%-Schwellwerten
    /// </summary>
    private async Task CheckBudgetAlertsAsync(IEnumerable<BudgetStatus> statuses)
    {
        // Prüfen ob Benachrichtigungen erlaubt sind
        if (!await _notificationService.AreNotificationsAllowedAsync())
            return;

        // Alte Benachrichtigungen bereinigen (einmal pro Sitzung)
        CleanupOldNotifications();

        var today = DateTime.Today;
        var currentMonthKey = $"{today.Year}-{today.Month}";

        foreach (var status in statuses)
        {
            var percentage = status.PercentageUsed;
            if (percentage < 80) continue;

            // Eindeutiger Schlüssel für diesen Budget-Alert (Kategorie + Monat + Schwellwert)
            var threshold = percentage >= 100 ? "100" : "80";
            var alertKey = $"{status.Category}_{currentMonthKey}_{threshold}";

            // Überspringen wenn bereits in diesem Monat benachrichtigt (thread-safe)
            if (_notifiedBudgets.ContainsKey(alertKey))
                continue;

            // Benachrichtigung senden
            var categoryName = GetLocalizedCategoryName(status.Category);
            await _notificationService.SendBudgetAlertAsync(
                categoryName,
                percentage,
                status.Spent,
                status.Limit);

            // Als benachrichtigt markieren (thread-safe)
            _notifiedBudgets.TryAdd(alertKey, DateTime.UtcNow);
        }
    }

    /// <summary>
    /// Entfernt alte Benachrichtigungs-Einträge (älter als aktueller Monat)
    /// </summary>
    private static void CleanupOldNotifications()
    {
        lock (_cleanupLock)
        {
            var today = DateTime.Today;
            var currentMonthKey = $"{today.Year}-{today.Month}";
            var keysToRemove = _notifiedBudgets.Keys
                .Where(k => !k.Contains(currentMonthKey))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _notifiedBudgets.TryRemove(key, out _);
            }
        }
    }

    [RelayCommand]
    private void ShowAddBudgetDialog()
    {
        IsEditing = false;
        SelectedCategory = ExpenseCategory.Food;
        MonthlyLimit = 500;
        WarningThreshold = 80;
        ShowAddBudget = true;
    }

    [RelayCommand]
    private void CancelAddBudget()
    {
        ShowAddBudget = false;
    }

    [RelayCommand]
    private async Task SaveBudgetAsync()
    {
        if (MonthlyLimit <= 0)
        {
            var title = _localizationService.GetString("Error") ?? "Error";
            var message = _localizationService.GetString("ErrorInvalidBudget") ?? "Please enter a valid budget amount.";
            MessageRequested?.Invoke(title, message);
            return;
        }

        try
        {
            var budget = new Budget
            {
                Category = SelectedCategory,
                MonthlyLimit = MonthlyLimit,
                WarningThreshold = WarningThreshold,
                IsEnabled = true
            };

            await _expenseService.SetBudgetAsync(budget);
            ShowAddBudget = false;
            await LoadBudgetsAsync();
        }
        catch (Exception)
        {
            var title = _localizationService.GetString("Error") ?? "Error";
            var message = _localizationService.GetString("SaveError") ?? "Failed to save budget. Please try again.";
            MessageRequested?.Invoke(title, message);
        }
    }

    [RelayCommand]
    private async Task EditBudgetAsync(BudgetStatus status)
    {
        var budget = await _expenseService.GetBudgetAsync(status.Category);
        if (budget == null) return;

        IsEditing = true;
        SelectedCategory = budget.Category;
        MonthlyLimit = budget.MonthlyLimit;
        WarningThreshold = budget.WarningThreshold;
        ShowAddBudget = true;
    }

    [RelayCommand]
    private async Task DeleteBudgetAsync(BudgetStatus status)
    {
        CancellationTokenSource? cts = null;
        try
        {
            // Budget für Undo sichern
            _deletedBudget = await _expenseService.GetBudgetAsync(status.Category);
            if (_deletedBudget == null) return;

            // Aus UI entfernen
            var itemToRemove = BudgetStatuses.FirstOrDefault(b => b.Category == status.Category);
            if (itemToRemove != null)
            {
                BudgetStatuses.Remove(itemToRemove);
                HasBudgets = BudgetStatuses.Count > 0;
            }

            // Undo-Benachrichtigung anzeigen
            var categoryName = GetLocalizedCategoryName(status.Category);
            UndoMessage = $"{_localizationService.GetString("BudgetDeleted") ?? "Budget deleted"} - {categoryName}";
            ShowUndoDelete = true;

            // Timer für permanente Löschung starten (5 Sekunden)
            _undoCancellation?.Cancel();
            _undoCancellation?.Dispose();
            cts = _undoCancellation = new CancellationTokenSource();

            await Task.Delay(5000, cts.Token);

            // Permanente Löschung nach 5 Sekunden
            if (_deletedBudget != null)
            {
                await _expenseService.DeleteBudgetAsync(_deletedBudget.Category);
                _deletedBudget = null;
                ShowUndoDelete = false;
            }
        }
        catch (TaskCanceledException)
        {
            // Undo wurde ausgelöst - nichts tun
        }
        catch (OperationCanceledException)
        {
            // Undo wurde ausgelöst - nichts tun
        }
        catch (Exception)
        {
            var title = _localizationService.GetString("Error") ?? "Error";
            var message = _localizationService.GetString("DeleteError") ?? "Failed to delete budget. Please try again.";
            MessageRequested?.Invoke(title, message);
        }
    }

    [RelayCommand]
    private async Task UndoDeleteAsync()
    {
        // Timer stoppen
        _undoCancellation?.Cancel();
        _undoCancellation?.Dispose();

        // Gelöschtes Budget wiederherstellen
        if (_deletedBudget != null)
        {
            await _expenseService.SetBudgetAsync(_deletedBudget);
            await LoadBudgetsAsync();
            _deletedBudget = null;
        }

        ShowUndoDelete = false;
    }

    [RelayCommand]
    private void DismissUndo()
    {
        ShowUndoDelete = false;
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigateTo("..");
    }

    private string GetLocalizedCategoryName(ExpenseCategory category)
        => CategoryLocalizationHelper.GetLocalizedName(category, _localizationService);

    #region IDisposable

    public void Dispose()
    {
        _undoCancellation?.Cancel();
        _undoCancellation?.Dispose();
        _undoCancellation = null;
    }

    #endregion
}
