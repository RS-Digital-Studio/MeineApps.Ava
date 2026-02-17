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

public partial class CompoundInterestViewModel : ObservableObject, IDisposable
{
    private readonly FinanceEngine _financeEngine;
    private readonly ILocalizationService _localizationService;

    // Debounce-Timer für Live-Berechnung (300ms Verzögerung)
    private Timer? _debounceTimer;

    public CompoundInterestViewModel(FinanceEngine financeEngine, ILocalizationService localizationService)
    {
        _financeEngine = financeEngine;
        _localizationService = localizationService;
    }

    public Action? GoBackAction { get; set; }

    #region Localized Text Properties

    public string TitleText => _localizationService.GetString("CalcCompoundInterest") ?? "Compound Interest";
    public string PrincipalText => _localizationService.GetString("Principal") ?? "Principal (EUR)";
    public string AnnualRateText => _localizationService.GetString("AnnualRate") ?? "Annual Rate (%)";
    public string YearsText => _localizationService.GetString("Years") ?? "Years";
    public string CompoundingsPerYearText => _localizationService.GetString("CompoundingsPerYear") ?? "Compoundings per Year";
    public string ResultText => _localizationService.GetString("Result") ?? "Result";
    public string FinalAmountText => _localizationService.GetString("FinalAmount") ?? "Final Amount";
    public string InterestEarnedText => _localizationService.GetString("InterestEarned") ?? "Interest Earned";
    public string CapitalGrowthText => _localizationService.GetString("CapitalGrowth") ?? "Capital Growth";
    public string ResetText => _localizationService.GetString("Reset") ?? "Reset";
    public string CalculateText => _localizationService.GetString("Calculate") ?? "Calculate";

    #endregion

    #region Input Properties

    [ObservableProperty]
    private double _principal = 10000;

    [ObservableProperty]
    private double _annualRate = 5;

    [ObservableProperty]
    private int _years = 10;

    [ObservableProperty]
    private int _compoundingsPerYear = 1;

    // Live-Berechnung mit Debouncing auslösen
    partial void OnPrincipalChanged(double value) => ScheduleAutoCalculate();
    partial void OnAnnualRateChanged(double value) => ScheduleAutoCalculate();
    partial void OnYearsChanged(int value) => ScheduleAutoCalculate();
    partial void OnCompoundingsPerYearChanged(int value) => ScheduleAutoCalculate();

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
    private CompoundInterestResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string? _errorMessage;

    public string FinalAmountDisplay => Result != null ? CurrencyHelper.Format(Result.FinalAmount) : "";
    public string InterestEarnedDisplay => Result != null ? CurrencyHelper.Format(Result.InterestEarned) : "";

    partial void OnResultChanged(CompoundInterestResult? value)
    {
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
        var principalValues = new float[Years + 1];
        var interestValues = new float[Years + 1];
        var rate = AnnualRate / 100;
        var n = CompoundingsPerYear;

        for (int year = 0; year <= Years; year++)
        {
            labels[year] = year.ToString();
            principalValues[year] = (float)Principal;
            var total = Principal * Math.Pow(1 + rate / n, n * year);
            interestValues[year] = (float)Math.Max(0, total - Principal);
        }

        ChartXLabels = labels;
        ChartArea1Data = principalValues;
        ChartArea2Data = interestValues;
    }

    #endregion

    [RelayCommand]
    private void Calculate()
    {
        ErrorMessage = null;
        if (Principal <= 0 || Years <= 0 || CompoundingsPerYear <= 0)
        {
            HasResult = false;
            return;
        }

        try
        {
            Result = _financeEngine.CalculateCompoundInterest(Principal, AnnualRate, Years, CompoundingsPerYear);
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

        Principal = 10000;
        AnnualRate = 5;
        Years = 10;
        CompoundingsPerYear = 1;
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
