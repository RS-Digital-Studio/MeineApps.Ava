using CommunityToolkit.Mvvm.ComponentModel;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Dashboard-Header ViewModel: Geld, Einkommen, Netto, GoldenScrews, Level/XP, Prestige-Badge,
/// Boost-Indikator, Rush, Delivery, Reputation, Soft-Cap, Worker-Warning.
/// Extrahiert aus MainViewModel (17.04.2026, Phase 3 Schritt 7).
/// Wird ueber <see cref="MainViewModel.HeaderVM"/> in den Views referenziert.
/// </summary>
public sealed partial class HeaderViewModel : ViewModelBase
{
    // Geld + Einkommen
    [ObservableProperty] private decimal _money;
    [ObservableProperty] private string _moneyDisplay = "0 €";
    [ObservableProperty] private decimal _incomePerSecond;
    [ObservableProperty] private string _incomeDisplay = "0 €/s";
    [ObservableProperty] private string _netIncomeHeaderDisplay = "";
    [ObservableProperty] private bool _isNetIncomeNegative;
    [ObservableProperty] private string _netIncomeColor = "#FFFFFFAA";

    // Worker-Warning + Soft-Cap
    [ObservableProperty] private string _workerWarningText = "";
    [ObservableProperty] private bool _hasWorkerWarning;
    [ObservableProperty] private bool _isSoftCapActive;
    [ObservableProperty] private string _softCapText = "";

    // Level + XP
    [ObservableProperty] private int _playerLevel = 1;
    [ObservableProperty] private int _currentXp;
    [ObservableProperty] private int _xpForNextLevel = 100;
    [ObservableProperty] private double _levelProgress;

    // GoldenScrews + Premium
    [ObservableProperty] private int _goldenScrews;
    [ObservableProperty] private string _goldenScrewsDisplay = "0";
    [ObservableProperty] private bool _isPremium;

    // Prestige-Badge
    [ObservableProperty] private bool _hasPrestige;
    [ObservableProperty] private string _prestigeTierName = "";
    [ObservableProperty] private string _prestigeBadgeColor = "#CD7F32";

    // Boost + Rush + Delivery
    [ObservableProperty] private bool _isBoostActive;
    [ObservableProperty] private string _boostTimeRemaining = "";
    [ObservableProperty] private bool _isRushActive;
    [ObservableProperty] private string _rushTimeRemaining = "";
    [ObservableProperty] private bool _hasPendingDelivery;
    [ObservableProperty] private string _deliveryTimeRemaining = "";
}
