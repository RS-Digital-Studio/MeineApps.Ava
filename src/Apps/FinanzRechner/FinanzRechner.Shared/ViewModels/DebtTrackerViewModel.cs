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
/// ViewModel für Schulden-Tracker.
/// </summary>
public sealed partial class DebtTrackerViewModel : ViewModelBase
{
    private readonly IDebtService _debtService;
    private readonly ILocalizationService _localizationService;

    public event Action<string>? NavigationRequested;
    public event Action? DataChanged;
    public event Action<string, string>? FloatingTextRequested;
    public event EventHandler? CelebrationRequested;

    public ObservableCollection<DebtEntry> Debts { get; } = [];

    [ObservableProperty] private double _totalDebt;
    [ObservableProperty] private string _totalDebtDisplay = CurrencyHelper.Format(0);
    [ObservableProperty] private double _totalPaid;
    [ObservableProperty] private string _totalPaidDisplay = CurrencyHelper.Format(0);
    [ObservableProperty] private double _overallProgress;
    [ObservableProperty] private bool _hasDebts;
    [ObservableProperty] private bool _showAddDialog;
    [ObservableProperty] private bool _showPaymentDialog;
    [ObservableProperty] private bool _isEditing;

    // Add/Edit Formular
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editOriginalAmount = string.Empty;
    [ObservableProperty] private string _editRemainingAmount = string.Empty;
    [ObservableProperty] private string _editInterestRate = "0";
    [ObservableProperty] private string _editMonthlyPayment = string.Empty;
    [ObservableProperty] private string? _editNote;
    [ObservableProperty] private string _editIcon = "\U0001F4B3";
    [ObservableProperty] private string _editColorHex = "#EF4444";
    private string? _editingDebtId;

    // Payment-Dialog
    [ObservableProperty] private string _paymentAmount = string.Empty;
    private string? _payingDebtId;

    // Lokalisierte Texte
    [ObservableProperty] private string _titleText = "Schulden";
    [ObservableProperty] private string _addDebtText = "Schuld hinzufügen";
    [ObservableProperty] private string _emptyText = "Keine Schulden - sehr gut!";

    public DebtTrackerViewModel(
        IDebtService debtService,
        ILocalizationService localizationService)
    {
        _debtService = debtService;
        _localizationService = localizationService;
        UpdateLocalizedTexts();
    }

    public async Task LoadDataAsync()
    {
        var debts = await _debtService.GetAllDebtsAsync();
        Debts.Clear();
        foreach (var d in debts)
            Debts.Add(d);

        var activeDebts = debts.Where(d => d.IsActive).ToList();
        TotalDebt = activeDebts.Sum(d => d.RemainingAmount);
        TotalDebtDisplay = CurrencyHelper.Format(TotalDebt);
        TotalPaid = debts.Sum(d => d.PaidAmount);
        TotalPaidDisplay = CurrencyHelper.Format(TotalPaid);

        var totalOriginal = debts.Sum(d => d.OriginalAmount);
        OverallProgress = totalOriginal > 0 ? (TotalPaid / totalOriginal) * 100 : 0;
        HasDebts = Debts.Count > 0;
    }

    [RelayCommand]
    private void OpenAddDialog()
    {
        _editingDebtId = null;
        IsEditing = false;
        EditName = string.Empty;
        EditOriginalAmount = string.Empty;
        EditRemainingAmount = string.Empty;
        EditInterestRate = "0";
        EditMonthlyPayment = string.Empty;
        EditNote = null;
        EditIcon = "\U0001F4B3";
        EditColorHex = "#EF4444";
        ShowAddDialog = true;
    }

    [RelayCommand]
    private void EditDebt(DebtEntry debt)
    {
        _editingDebtId = debt.Id;
        IsEditing = true;
        EditName = debt.Name;
        EditOriginalAmount = debt.OriginalAmount.ToString("F2");
        EditRemainingAmount = debt.RemainingAmount.ToString("F2");
        EditInterestRate = debt.InterestRate.ToString("F2");
        EditMonthlyPayment = debt.MonthlyPayment.ToString("F2");
        EditNote = debt.Note;
        EditIcon = debt.Icon;
        EditColorHex = debt.ColorHex;
        ShowAddDialog = true;
    }

    [RelayCommand]
    private async Task SaveDebtAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) return;
        if (!double.TryParse(EditOriginalAmount, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var original) || original <= 0)
            return;

        double.TryParse(EditRemainingAmount, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var remaining);
        double.TryParse(EditInterestRate, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var rate);
        double.TryParse(EditMonthlyPayment, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var monthly);

        if (remaining <= 0) remaining = original;

        try
        {
            if (IsEditing && _editingDebtId != null)
            {
                var existing = await _debtService.GetDebtAsync(_editingDebtId);
                if (existing != null)
                {
                    existing.Name = EditName;
                    existing.OriginalAmount = original;
                    existing.RemainingAmount = remaining;
                    existing.InterestRate = rate;
                    existing.MonthlyPayment = monthly;
                    existing.Note = EditNote;
                    existing.Icon = EditIcon;
                    existing.ColorHex = EditColorHex;
                    await _debtService.UpdateDebtAsync(existing);
                }
            }
            else
            {
                await _debtService.CreateDebtAsync(new DebtEntry
                {
                    Name = EditName,
                    OriginalAmount = original,
                    RemainingAmount = remaining,
                    InterestRate = rate,
                    MonthlyPayment = monthly,
                    Note = EditNote,
                    Icon = EditIcon,
                    ColorHex = EditColorHex
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
    private void OpenPaymentDialog(DebtEntry debt)
    {
        _payingDebtId = debt.Id;
        PaymentAmount = debt.MonthlyPayment > 0 ? debt.MonthlyPayment.ToString("F2") : string.Empty;
        ShowPaymentDialog = true;
    }

    [RelayCommand]
    private async Task ConfirmPaymentAsync()
    {
        if (_payingDebtId == null) return;
        if (!double.TryParse(PaymentAmount, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            return;

        await _debtService.MakePaymentAsync(_payingDebtId, amount);

        ShowPaymentDialog = false;
        FloatingTextRequested?.Invoke(
            $"-{CurrencyHelper.Format(amount)}", "expense");

        // Prüfen ob Schuld abbezahlt
        var debt = await _debtService.GetDebtAsync(_payingDebtId);
        if (debt is { IsActive: false })
            CelebrationRequested?.Invoke(this, EventArgs.Empty);

        await LoadDataAsync();
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private async Task DeleteDebtAsync(DebtEntry debt)
    {
        await _debtService.DeleteDebtAsync(debt.Id);
        await LoadDataAsync();
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private void CancelDialog()
    {
        ShowAddDialog = false;
        ShowPaymentDialog = false;
    }

    [RelayCommand]
    private void GoBack() => NavigationRequested?.Invoke("..");

    public string CancelText => _localizationService.GetString("Cancel") ?? "Cancel";
    public string SaveText => _localizationService.GetString("Save") ?? "Save";
    public string EditText => _localizationService.GetString("Edit") ?? "Edit";
    public string DeleteText => _localizationService.GetString("Delete") ?? "Delete";
    public string StillOpenText => _localizationService.GetString("StillOpen") ?? "Still open";
    public string AlreadyPaidText => _localizationService.GetString("AlreadyPaid") ?? "Already paid";
    public string MonthlyRateText => _localizationService.GetString("MonthlyRate") ?? "Rate/month";
    public string InterestRateLabelText => _localizationService.GetString("InterestRateLabel") ?? "Interest rate";
    public string RemainingTermText => _localizationService.GetString("RemainingTerm") ?? "Remaining term";
    public string MonthsAbbrevText => _localizationService.GetString("MonthsAbbrev") ?? "mo.";
    public string MakePaymentText => _localizationService.GetString("MakePayment") ?? "Make Payment";
    public string BookPaymentText => _localizationService.GetString("BookPayment") ?? "Book";
    public string PaymentAmountText => _localizationService.GetString("PaymentAmount") ?? "Payment amount";
    public string AmountText => _localizationService.GetString("Amount") ?? "Amount";

    public void UpdateLocalizedTexts()
    {
        TitleText = _localizationService.GetString("DebtTrackerTitle") ?? "Schulden";
        AddDebtText = _localizationService.GetString("AddDebt") ?? "Schuld hinzufügen";
        EmptyText = _localizationService.GetString("NoDebtsYet") ?? "Keine Schulden - sehr gut!";
        OnPropertyChanged(nameof(CancelText));
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(EditText));
        OnPropertyChanged(nameof(DeleteText));
        OnPropertyChanged(nameof(StillOpenText));
        OnPropertyChanged(nameof(AlreadyPaidText));
        OnPropertyChanged(nameof(MakePaymentText));
    }
}
