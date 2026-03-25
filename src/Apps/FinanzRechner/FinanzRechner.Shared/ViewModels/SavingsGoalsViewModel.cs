using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanzRechner.Helpers;
using FinanzRechner.Models;
using FinanzRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;

namespace FinanzRechner.ViewModels;

/// <summary>
/// ViewModel für Sparziel-Verwaltung.
/// </summary>
public sealed partial class SavingsGoalsViewModel : ViewModelBase
{
    private readonly ISavingsGoalService _savingsGoalService;
    private readonly ILocalizationService _localizationService;

    public event Action<string>? NavigationRequested;
    public event Action? DataChanged;
    public event Action<string, string>? FloatingTextRequested;
    public event EventHandler? CelebrationRequested;

    public ObservableCollection<SavingsGoal> Goals { get; } = [];

    [ObservableProperty] private double _totalSaved;
    [ObservableProperty] private string _totalSavedDisplay = CurrencyHelper.Format(0);
    [ObservableProperty] private bool _hasGoals;
    [ObservableProperty] private bool _showAddDialog;
    [ObservableProperty] private bool _showAdjustDialog;
    [ObservableProperty] private bool _isEditing;

    // Add/Edit Formular
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editTargetAmount = string.Empty;
    [ObservableProperty] private string _editCurrentAmount = "0";
    [ObservableProperty] private DateTimeOffset? _editDeadline;
    [ObservableProperty] private string _editIcon = "\U0001F3AF";
    [ObservableProperty] private string _editColorHex = "#10B981";
    [ObservableProperty] private string? _editNote;
    private string? _editingGoalId;

    // Adjust-Dialog
    [ObservableProperty] private string _adjustAmount = string.Empty;
    [ObservableProperty] private bool _adjustIsDeposit = true;
    private string? _adjustingGoalId;

    // Lokalisierte Texte
    [ObservableProperty] private string _titleText = "Sparziele";
    [ObservableProperty] private string _addGoalText = "Sparziel hinzufügen";
    [ObservableProperty] private string _emptyText = "Noch keine Sparziele vorhanden.";

    public SavingsGoalsViewModel(
        ISavingsGoalService savingsGoalService,
        ILocalizationService localizationService)
    {
        _savingsGoalService = savingsGoalService;
        _localizationService = localizationService;
        UpdateLocalizedTexts();
    }

    public async Task LoadDataAsync()
    {
        var goals = await _savingsGoalService.GetAllGoalsAsync();
        Goals.Clear();
        foreach (var g in goals)
            Goals.Add(g);

        TotalSaved = goals.Sum(g => g.CurrentAmount);
        TotalSavedDisplay = CurrencyHelper.Format(TotalSaved);
        HasGoals = Goals.Count > 0;
    }

    [RelayCommand]
    private void OpenAddDialog()
    {
        _editingGoalId = null;
        IsEditing = false;
        EditName = string.Empty;
        EditTargetAmount = string.Empty;
        EditCurrentAmount = "0";
        EditDeadline = null;
        EditIcon = "\U0001F3AF";
        EditColorHex = "#10B981";
        EditNote = null;
        ShowAddDialog = true;
    }

    [RelayCommand]
    private void EditGoal(SavingsGoal goal)
    {
        _editingGoalId = goal.Id;
        IsEditing = true;
        EditName = goal.Name;
        EditTargetAmount = goal.TargetAmount.ToString("F2");
        EditCurrentAmount = goal.CurrentAmount.ToString("F2");
        EditDeadline = goal.Deadline.HasValue ? new DateTimeOffset(goal.Deadline.Value) : null;
        EditIcon = goal.Icon;
        EditColorHex = goal.ColorHex;
        EditNote = goal.Note;
        ShowAddDialog = true;
    }

    [RelayCommand]
    private async Task SaveGoalAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) return;
        if (!double.TryParse(EditTargetAmount, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var target) || target <= 0)
            return;
        double.TryParse(EditCurrentAmount, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var current);

        try
        {
            if (IsEditing && _editingGoalId != null)
            {
                var existing = await _savingsGoalService.GetGoalAsync(_editingGoalId);
                if (existing != null)
                {
                    existing.Name = EditName;
                    existing.TargetAmount = target;
                    existing.CurrentAmount = current;
                    existing.Deadline = EditDeadline?.DateTime;
                    existing.Icon = EditIcon;
                    existing.ColorHex = EditColorHex;
                    existing.Note = EditNote;
                    await _savingsGoalService.UpdateGoalAsync(existing);
                }
            }
            else
            {
                await _savingsGoalService.CreateGoalAsync(new SavingsGoal
                {
                    Name = EditName,
                    TargetAmount = target,
                    CurrentAmount = current,
                    Deadline = EditDeadline?.DateTime,
                    Icon = EditIcon,
                    ColorHex = EditColorHex,
                    Note = EditNote
                });
            }

            ShowAddDialog = false;
            await LoadDataAsync();
            DataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            FloatingTextRequested?.Invoke(ex.Message, "error");
        }
    }

    [RelayCommand]
    private void OpenAdjustDialog(SavingsGoal goal)
    {
        _adjustingGoalId = goal.Id;
        AdjustAmount = string.Empty;
        AdjustIsDeposit = true;
        ShowAdjustDialog = true;
    }

    [RelayCommand]
    private async Task ConfirmAdjustAsync()
    {
        if (_adjustingGoalId == null) return;
        if (!double.TryParse(AdjustAmount, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            return;

        var adjustedAmount = AdjustIsDeposit ? amount : -amount;
        await _savingsGoalService.AdjustAmountAsync(_adjustingGoalId, adjustedAmount);

        ShowAdjustDialog = false;
        FloatingTextRequested?.Invoke(
            CurrencyHelper.FormatCompactSigned(adjustedAmount),
            AdjustIsDeposit ? "income" : "expense");

        // Prüfen ob Ziel erreicht
        var goal = await _savingsGoalService.GetGoalAsync(_adjustingGoalId);
        if (goal is { IsCompleted: true })
            CelebrationRequested?.Invoke(this, EventArgs.Empty);

        await LoadDataAsync();
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private async Task DeleteGoalAsync(SavingsGoal goal)
    {
        await _savingsGoalService.DeleteGoalAsync(goal.Id);
        await LoadDataAsync();
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private void CancelDialog()
    {
        ShowAddDialog = false;
        ShowAdjustDialog = false;
    }

    [RelayCommand]
    private void GoBack() => NavigationRequested?.Invoke("..");

    public string CancelText => _localizationService.GetString("Cancel") ?? "Cancel";
    public string SaveText => _localizationService.GetString("Save") ?? "Save";
    public string EditText => _localizationService.GetString("Edit") ?? "Edit";
    public string DeleteText => _localizationService.GetString("Delete") ?? "Delete";
    public string ConfirmText => _localizationService.GetString("Confirm") ?? "Confirm";
    public string AdjustAmountText => _localizationService.GetString("AdjustAmount") ?? "Adjust amount";
    public string DepositText => _localizationService.GetString("Deposit") ?? "Deposit";
    public string WithdrawalText => _localizationService.GetString("Withdrawal") ?? "Withdrawal";
    public string AmountText => _localizationService.GetString("Amount") ?? "Amount";
    public string GoalNameHintText => _localizationService.GetString("GoalNameHint") ?? "Name (e.g. Vacation)";
    public string TargetAmountText => _localizationService.GetString("TargetAmount") ?? "Target amount";
    public string AlreadySavedText => _localizationService.GetString("AlreadySaved") ?? "Already saved";
    public string DeadlineOptionalText => _localizationService.GetString("DeadlineOptional") ?? "Deadline (optional)";
    public string TotalSavedLabelText => _localizationService.GetString("TotalSavedLabel") ?? "Total saved";

    public void UpdateLocalizedTexts()
    {
        TitleText = _localizationService.GetString("SavingsGoalsTitle") ?? "Sparziele";
        AddGoalText = _localizationService.GetString("AddSavingsGoal") ?? "Sparziel hinzufügen";
        EmptyText = _localizationService.GetString("NoSavingsGoalsYet") ?? "Noch keine Sparziele vorhanden.";
        OnPropertyChanged(nameof(CancelText));
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(EditText));
        OnPropertyChanged(nameof(DeleteText));
        OnPropertyChanged(nameof(ConfirmText));
        OnPropertyChanged(nameof(AdjustAmountText));
        OnPropertyChanged(nameof(DepositText));
        OnPropertyChanged(nameof(WithdrawalText));
        OnPropertyChanged(nameof(AmountText));
        OnPropertyChanged(nameof(TotalSavedLabelText));
    }
}
