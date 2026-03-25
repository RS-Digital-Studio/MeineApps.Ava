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
/// ViewModel für Kontoverwaltung (Multi-Konto + Überweisungen).
/// </summary>
public sealed partial class AccountsViewModel : ViewModelBase
{
    private readonly IAccountService _accountService;
    private readonly ILocalizationService _localizationService;

    public event Action<string>? NavigationRequested;
    public event Action? DataChanged;
    public event Action<string, string>? FloatingTextRequested;

    public ObservableCollection<AccountBalance> AccountBalances { get; } = [];

    [ObservableProperty] private double _netWorth;
    [ObservableProperty] private string _netWorthDisplay = CurrencyHelper.Format(0);
    [ObservableProperty] private bool _hasAccounts;
    [ObservableProperty] private bool _showAddDialog;
    [ObservableProperty] private bool _showTransferDialog;
    [ObservableProperty] private bool _isEditing;

    // Add/Edit Formular
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editBalance = "0";
    [ObservableProperty] private int _editTypeIndex;
    [ObservableProperty] private string _editIcon = "\U0001F3E6";
    [ObservableProperty] private string _editColorHex = "#4CAF50";
    private string? _editingAccountId;

    // Transfer Formular
    [ObservableProperty] private string _transferAmount = string.Empty;
    [ObservableProperty] private string _transferDescription = string.Empty;
    [ObservableProperty] private int _transferFromIndex;
    [ObservableProperty] private int _transferToIndex = 1;

    // Lokalisierte Texte
    [ObservableProperty] private string _titleText = "Konten";
    [ObservableProperty] private string _addAccountText = "Konto hinzufügen";
    [ObservableProperty] private string _transferText = "Überweisung";
    [ObservableProperty] private string _emptyText = "Noch keine Konten vorhanden.";

    public AccountsViewModel(
        IAccountService accountService,
        ILocalizationService localizationService)
    {
        _accountService = accountService;
        _localizationService = localizationService;
        UpdateLocalizedTexts();
    }

    public async Task LoadDataAsync()
    {
        var balances = await _accountService.GetAllAccountBalancesAsync();
        AccountBalances.Clear();
        foreach (var b in balances)
            AccountBalances.Add(b);

        NetWorth = await _accountService.GetNetWorthAsync();
        NetWorthDisplay = CurrencyHelper.Format(NetWorth);
        HasAccounts = AccountBalances.Count > 0;
    }

    [RelayCommand]
    private void OpenAddDialog()
    {
        _editingAccountId = null;
        IsEditing = false;
        EditName = string.Empty;
        EditBalance = "0";
        EditTypeIndex = 0;
        EditIcon = "\U0001F3E6";
        EditColorHex = "#4CAF50";
        ShowAddDialog = true;
    }

    [RelayCommand]
    private void EditAccount(AccountBalance balance)
    {
        _editingAccountId = balance.Account.Id;
        IsEditing = true;
        EditName = balance.Account.Name;
        EditBalance = balance.Account.InitialBalance.ToString("F2");
        EditTypeIndex = (int)balance.Account.Type;
        EditIcon = balance.Account.Icon;
        EditColorHex = balance.Account.ColorHex;
        ShowAddDialog = true;
    }

    [RelayCommand]
    private async Task SaveAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) return;
        if (!double.TryParse(EditBalance, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var balance))
            balance = 0;

        try
        {
            if (IsEditing && _editingAccountId != null)
            {
                var existing = await _accountService.GetAccountAsync(_editingAccountId);
                if (existing != null)
                {
                    existing.Name = EditName;
                    existing.InitialBalance = balance;
                    existing.Type = (AccountType)EditTypeIndex;
                    existing.Icon = EditIcon;
                    existing.ColorHex = EditColorHex;
                    await _accountService.UpdateAccountAsync(existing);
                }
            }
            else
            {
                await _accountService.CreateAccountAsync(new Account
                {
                    Name = EditName,
                    InitialBalance = balance,
                    Type = (AccountType)EditTypeIndex,
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
    private async Task DeleteAccountAsync(AccountBalance balance)
    {
        await _accountService.DeleteAccountAsync(balance.Account.Id);
        await LoadDataAsync();
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private void OpenTransferDialog()
    {
        TransferAmount = string.Empty;
        TransferDescription = string.Empty;
        TransferFromIndex = 0;
        TransferToIndex = Math.Min(1, AccountBalances.Count - 1);
        ShowTransferDialog = true;
    }

    [RelayCommand]
    private async Task ExecuteTransferAsync()
    {
        if (!double.TryParse(TransferAmount, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            return;

        if (TransferFromIndex == TransferToIndex || AccountBalances.Count < 2) return;

        var fromAccount = AccountBalances[TransferFromIndex].Account;
        var toAccount = AccountBalances[TransferToIndex].Account;
        var desc = string.IsNullOrWhiteSpace(TransferDescription)
            ? $"{fromAccount.Name} → {toAccount.Name}"
            : TransferDescription;

        try
        {
            await _accountService.CreateTransferAsync(
                fromAccount.Id, toAccount.Id, amount, desc, DateTime.Today);

            ShowTransferDialog = false;
            FloatingTextRequested?.Invoke(
                $"-{CurrencyHelper.Format(amount)} → +{CurrencyHelper.Format(amount)}", "transfer");
            await LoadDataAsync();
            DataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            FloatingTextRequested?.Invoke(ex.Message, "error");
        }
    }

    [RelayCommand]
    private void CancelDialog()
    {
        ShowAddDialog = false;
        ShowTransferDialog = false;
    }

    [RelayCommand]
    private void GoBack() => NavigationRequested?.Invoke("..");

    // Lokalisierte Standard-Texte
    public string CancelText => _localizationService.GetString("Cancel") ?? "Cancel";
    public string SaveText => _localizationService.GetString("Save") ?? "Save";
    public string EditText => _localizationService.GetString("Edit") ?? "Edit";
    public string DeleteText => _localizationService.GetString("Delete") ?? "Delete";
    public string NetWorthText => _localizationService.GetString("NetWorth") ?? "Net Worth";
    public string AccountNameText => _localizationService.GetString("AccountName") ?? "Account name";
    public string InitialBalanceText => _localizationService.GetString("InitialBalance") ?? "Initial balance";
    public string FromAccountText => _localizationService.GetString("FromAccount") ?? "From account:";
    public string ToAccountText => _localizationService.GetString("ToAccount") ?? "To account:";
    public string TransferExecuteText => _localizationService.GetString("TransferExecute") ?? "Transfer";
    public string DescriptionOptionalText => _localizationService.GetString("DescriptionOptional") ?? "Description (optional)";
    public string AmountText => _localizationService.GetString("Amount") ?? "Amount";
    public string AccountCheckingText => _localizationService.GetString("AccountChecking") ?? "Checking Account";
    public string AccountSavingsText => _localizationService.GetString("AccountSavings") ?? "Savings Account";
    public string AccountCashText => _localizationService.GetString("AccountCash") ?? "Cash";
    public string AccountCreditCardText => _localizationService.GetString("AccountCreditCard") ?? "Credit Card";
    public string AccountInvestmentText => _localizationService.GetString("AccountInvestment") ?? "Investment";
    public string AccountOtherText => _localizationService.GetString("AccountOther") ?? "Other";

    public void UpdateLocalizedTexts()
    {
        TitleText = _localizationService.GetString("AccountsTitle") ?? "Konten";
        AddAccountText = _localizationService.GetString("AddAccount") ?? "Konto hinzufügen";
        TransferText = _localizationService.GetString("Transfer") ?? "Überweisung";
        EmptyText = _localizationService.GetString("NoAccountsYet") ?? "Noch keine Konten vorhanden.";
        OnPropertyChanged(nameof(CancelText));
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(EditText));
        OnPropertyChanged(nameof(DeleteText));
        OnPropertyChanged(nameof(NetWorthText));
        OnPropertyChanged(nameof(AccountNameText));
        OnPropertyChanged(nameof(InitialBalanceText));
        OnPropertyChanged(nameof(FromAccountText));
        OnPropertyChanged(nameof(ToAccountText));
        OnPropertyChanged(nameof(TransferExecuteText));
        OnPropertyChanged(nameof(DescriptionOptionalText));
        OnPropertyChanged(nameof(AmountText));
        OnPropertyChanged(nameof(AccountCheckingText));
        OnPropertyChanged(nameof(AccountSavingsText));
        OnPropertyChanged(nameof(AccountCashText));
        OnPropertyChanged(nameof(AccountCreditCardText));
        OnPropertyChanged(nameof(AccountInvestmentText));
        OnPropertyChanged(nameof(AccountOtherText));
    }

    /// <summary>Lokalisierter Kontotyp-Name.</summary>
    public string GetAccountTypeName(AccountType type) => type switch
    {
        AccountType.Checking => _localizationService.GetString("AccountChecking") ?? "Girokonto",
        AccountType.Savings => _localizationService.GetString("AccountSavings") ?? "Sparkonto",
        AccountType.Cash => _localizationService.GetString("AccountCash") ?? "Bargeld",
        AccountType.CreditCard => _localizationService.GetString("AccountCreditCard") ?? "Kreditkarte",
        AccountType.Investment => _localizationService.GetString("AccountInvestment") ?? "Depot",
        _ => _localizationService.GetString("AccountOther") ?? "Sonstiges"
    };
}
