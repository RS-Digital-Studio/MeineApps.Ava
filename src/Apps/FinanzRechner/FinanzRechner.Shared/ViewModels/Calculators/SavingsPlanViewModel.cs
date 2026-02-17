using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanzRechner.Helpers;
using FinanzRechner.Models;
using MeineApps.Core.Ava.Localization;

namespace FinanzRechner.ViewModels.Calculators;

public partial class SavingsPlanViewModel : ObservableObject, IDisposable
{
    private readonly FinanceEngine _financeEngine;
    private readonly ILocalizationService _localizationService;

    // Debounce-Timer für Live-Berechnung (300ms Verzögerung)
    private Timer? _debounceTimer;

    public SavingsPlanViewModel(FinanceEngine financeEngine, ILocalizationService localizationService)
    {
        _financeEngine = financeEngine;
        _localizationService = localizationService;
    }

    public Action? GoBackAction { get; set; }

    #region Localized Text Properties

    public string TitleText => _localizationService.GetString("CalcSavingsPlan") ?? "Savings Plan";
    public string MonthlyDepositText => _localizationService.GetString("MonthlyDeposit") ?? "Monthly Deposit (EUR)";
    public string InitialDepositText => _localizationService.GetString("InitialDeposit") ?? "Initial Deposit (EUR)";
    public string AnnualRateText => _localizationService.GetString("AnnualRate") ?? "Annual Rate (%)";
    public string YearsText => _localizationService.GetString("Years") ?? "Years";
    public string ResultText => _localizationService.GetString("Result") ?? "Result";
    public string FinalAmountText => _localizationService.GetString("FinalAmount") ?? "Final Amount";
    public string TotalDepositsText => _localizationService.GetString("TotalDeposits") ?? "Total Deposits";
    public string InterestEarnedText => _localizationService.GetString("InterestEarned") ?? "Interest Earned";
    public string SavingsGrowthText => _localizationService.GetString("SavingsGrowth") ?? "Savings Growth";
    public string DepositsLegendText => _localizationService.GetString("Deposits") ?? "Deposits";
    public string CapitalLegendText => _localizationService.GetString("Capital") ?? "Capital";
    public string ResetText => _localizationService.GetString("Reset") ?? "Reset";
    public string CalculateText => _localizationService.GetString("Calculate") ?? "Calculate";

    #endregion

    #region Input Properties

    [ObservableProperty]
    private double _monthlyDeposit = 200;

    [ObservableProperty]
    private double _initialDeposit = 0;

    [ObservableProperty]
    private double _annualRate = 5;

    [ObservableProperty]
    private int _years = 20;

    // Live-Berechnung mit Debouncing auslösen
    partial void OnMonthlyDepositChanged(double value) => ScheduleAutoCalculate();
    partial void OnInitialDepositChanged(double value) => ScheduleAutoCalculate();
    partial void OnAnnualRateChanged(double value) => ScheduleAutoCalculate();
    partial void OnYearsChanged(int value) => ScheduleAutoCalculate();

    /// <summary>
    /// Startet den Debounce-Timer neu. Nach 300ms wird Calculate() auf dem UI-Thread aufgerufen.
    /// </summary>
    private void ScheduleAutoCalculate()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ =>
            Dispatcher.UIThread.Post(() => Calculate()),
            null, 300, Timeout.Infinite);
    }

    #endregion

    #region Result Properties

    [ObservableProperty]
    private SavingsPlanResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string? _errorMessage;

    public string TotalDepositsDisplay => Result != null ? CurrencyHelper.Format(Result.TotalDeposits) : "";
    public string FinalAmountDisplay => Result != null ? CurrencyHelper.Format(Result.FinalAmount) : "";
    public string InterestEarnedDisplay => Result != null ? CurrencyHelper.Format(Result.InterestEarned) : "";

    partial void OnResultChanged(SavingsPlanResult? value)
    {
        OnPropertyChanged(nameof(TotalDepositsDisplay));
        OnPropertyChanged(nameof(FinalAmountDisplay));
        OnPropertyChanged(nameof(InterestEarnedDisplay));
        UpdateChartData();
    }

    #endregion

    #region Chart Properties

    [ObservableProperty]
    private string[]? _chartXLabels;

    [ObservableProperty]
    private float[]? _chartArea1Data;

    [ObservableProperty]
    private float[]? _chartArea2Data;

    private void UpdateChartData()
    {
        if (Result == null || Years <= 0)
        {
            ChartXLabels = null;
            ChartArea1Data = null;
            ChartArea2Data = null;
            return;
        }

        var labels = new string[Years + 1];
        var depositValues = new float[Years + 1];
        var interestValues = new float[Years + 1];
        var monthlyRate = (AnnualRate / 100) / 12;

        for (int year = 0; year <= Years; year++)
        {
            labels[year] = year.ToString();
            var months = year * 12;
            var deposits = InitialDeposit + (MonthlyDeposit * months);
            depositValues[year] = (float)deposits;

            // Gesamtwert für dieses Jahr berechnen
            double total;
            if (monthlyRate > 0)
            {
                var initialGrowth = InitialDeposit * Math.Pow(1 + monthlyRate, months);
                var savingsGrowth = MonthlyDeposit * ((Math.Pow(1 + monthlyRate, months) - 1) / monthlyRate);
                total = initialGrowth + savingsGrowth;
            }
            else
            {
                total = deposits;
            }
            interestValues[year] = (float)Math.Max(0, total - deposits);
        }

        ChartXLabels = labels;
        ChartArea1Data = depositValues;
        ChartArea2Data = interestValues;
    }

    #endregion

    [RelayCommand]
    private void Calculate()
    {
        ErrorMessage = null;
        if (MonthlyDeposit < 0 || Years <= 0)
        {
            HasResult = false;
            return;
        }

        try
        {
            Result = _financeEngine.CalculateSavingsPlan(MonthlyDeposit, AnnualRate, Years, InitialDeposit);
            HasResult = true;
        }
        catch (OverflowException)
        {
            HasResult = false;
            ErrorMessage = _localizationService.GetString("ErrorOverflow") ?? "The input values lead to unrealistic results.";
        }
    }

    [RelayCommand]
    private void Reset()
    {
        // Timer stoppen um keine verzögerte Berechnung nach Reset auszulösen
        _debounceTimer?.Dispose();
        _debounceTimer = null;

        MonthlyDeposit = 200;
        InitialDeposit = 0;
        AnnualRate = 5;
        Years = 20;
        Result = null;
        HasResult = false;
        ErrorMessage = null;
        ChartXLabels = null;
        ChartArea1Data = null;
        ChartArea2Data = null;
    }

    [RelayCommand]
    private void GoBack() => GoBackAction?.Invoke();

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }
}
