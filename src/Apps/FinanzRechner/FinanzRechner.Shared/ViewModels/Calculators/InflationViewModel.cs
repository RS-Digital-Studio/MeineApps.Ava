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

/// <summary>
/// ViewModel fuer den Inflationsrechner.
/// Berechnet Kaufkraftverlust ueber die Jahre.
/// </summary>
public partial class InflationViewModel : ObservableObject, IDisposable
{
    private readonly FinanceEngine _financeEngine;
    private readonly ILocalizationService _localizationService;

    // Debounce-Timer für Live-Berechnung (300ms Verzögerung)
    private Timer? _debounceTimer;

    public InflationViewModel(FinanceEngine financeEngine, ILocalizationService localizationService)
    {
        _financeEngine = financeEngine;
        _localizationService = localizationService;
    }

    public Action? GoBackAction { get; set; }

    #region Localized Text Properties

    public string TitleText => _localizationService.GetString("CalcInflation") ?? "Inflation";
    public string CurrentAmountText => _localizationService.GetString("CurrentAmount") ?? "Current Amount (EUR)";
    public string AnnualInflationRateText => _localizationService.GetString("AnnualInflationRate") ?? "Inflation Rate (%)";
    public string YearsText => _localizationService.GetString("Years") ?? "Years";
    public string ResultText => _localizationService.GetString("Result") ?? "Result";
    public string FutureValueText => _localizationService.GetString("FutureValue") ?? "Future Value";
    public string PurchasingPowerText => _localizationService.GetString("PurchasingPower") ?? "Purchasing Power";
    public string PurchasingPowerLossText => _localizationService.GetString("PurchasingPowerLoss") ?? "Purchasing Power Loss";
    public string LossPercentText => _localizationService.GetString("LossPercent") ?? "Loss %";
    public string PurchasingPowerLossChartText => _localizationService.GetString("ChartPurchasingPower") ?? "Purchasing Power";
    public string ResetText => _localizationService.GetString("Reset") ?? "Reset";
    public string CalculateText => _localizationService.GetString("Calculate") ?? "Calculate";

    #endregion

    #region Input Properties

    [ObservableProperty]
    private double _currentAmount = 10000;

    [ObservableProperty]
    private double _annualInflationRate = 3;

    [ObservableProperty]
    private int _years = 10;

    // Live-Berechnung mit Debouncing auslösen
    partial void OnCurrentAmountChanged(double value) => ScheduleAutoCalculate();
    partial void OnAnnualInflationRateChanged(double value) => ScheduleAutoCalculate();
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
    private InflationResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string? _errorMessage;

    public string PurchasingPowerDisplay => Result != null ? CurrencyHelper.Format(Result.PurchasingPower) : "";
    public string PurchasingPowerLossDisplay => Result != null ? CurrencyHelper.Format(Result.PurchasingPowerLoss) : "";
    public string FutureValueDisplay => Result != null ? CurrencyHelper.Format(Result.FutureValue) : "";
    public string LossPercentDisplay => Result != null ? $"{Result.LossPercent:F1} %" : "";

    partial void OnResultChanged(InflationResult? value)
    {
        OnPropertyChanged(nameof(PurchasingPowerDisplay));
        OnPropertyChanged(nameof(PurchasingPowerLossDisplay));
        OnPropertyChanged(nameof(FutureValueDisplay));
        OnPropertyChanged(nameof(LossPercentDisplay));
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
        var remainingValues = new float[Years + 1];
        var lostValues = new float[Years + 1];
        var rate = AnnualInflationRate / 100;

        for (int year = 0; year <= Years; year++)
        {
            labels[year] = year.ToString();
            var remaining = CurrentAmount / Math.Pow(1 + rate, year);
            remainingValues[year] = (float)remaining;
            lostValues[year] = (float)(CurrentAmount - remaining);
        }

        ChartXLabels = labels;
        ChartArea1Data = remainingValues;
        ChartArea2Data = lostValues;
    }

    #endregion

    [RelayCommand]
    private void Calculate()
    {
        ErrorMessage = null;
        if (CurrentAmount <= 0 || Years <= 0)
        {
            HasResult = false;
            return;
        }

        try
        {
            Result = _financeEngine.CalculateInflation(CurrentAmount, AnnualInflationRate, Years);
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

        CurrentAmount = 10000;
        AnnualInflationRate = 3;
        Years = 10;
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
