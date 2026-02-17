using System;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanzRechner.Helpers;
using FinanzRechner.Models;
using MeineApps.Core.Ava.Localization;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FinanzRechner.ViewModels.Calculators;

public partial class LoanViewModel : ObservableObject, IDisposable
{
    private readonly FinanceEngine _financeEngine;
    private readonly ILocalizationService _localizationService;

    // Debounce-Timer für Live-Berechnung (300ms Verzögerung)
    private Timer? _debounceTimer;

    public LoanViewModel(FinanceEngine financeEngine, ILocalizationService localizationService)
    {
        _financeEngine = financeEngine;
        _localizationService = localizationService;
    }

    public Action? GoBackAction { get; set; }

    #region Localized Text Properties

    public string TitleText => _localizationService.GetString("CalcLoan") ?? "Loan";
    public string LoanAmountText => _localizationService.GetString("LoanAmount") ?? "Loan Amount (EUR)";
    public string AnnualRateText => _localizationService.GetString("AnnualRate") ?? "Annual Rate (%)";
    public string YearsText => _localizationService.GetString("Years") ?? "Years";
    public string ResultText => _localizationService.GetString("Result") ?? "Result";
    public string MonthlyPaymentText => _localizationService.GetString("MonthlyPayment") ?? "Monthly Payment";
    public string TotalPaymentText => _localizationService.GetString("TotalPayment") ?? "Total Payment";
    public string TotalInterestText => _localizationService.GetString("TotalInterest") ?? "Total Interest";
    public string CostBreakdownText => _localizationService.GetString("CostBreakdown") ?? "Cost Breakdown";
    public string PrincipalPortionText => _localizationService.GetString("PrincipalPortion") ?? "Principal";
    public string InterestPortionText => _localizationService.GetString("InterestPortion") ?? "Interest";
    public string ResetText => _localizationService.GetString("Reset") ?? "Reset";
    public string CalculateText => _localizationService.GetString("Calculate") ?? "Calculate";

    #endregion

    #region Input Properties

    [ObservableProperty]
    private double _loanAmount = 100000;

    [ObservableProperty]
    private double _annualRate = 4;

    [ObservableProperty]
    private int _years = 20;

    // Live-Berechnung mit Debouncing auslösen
    partial void OnLoanAmountChanged(double value) => ScheduleAutoCalculate();
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
    private LoanResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string? _errorMessage;

    public string MonthlyPaymentDisplay => Result != null ? CurrencyHelper.Format(Result.MonthlyPayment) : "";
    public string TotalPaymentDisplay => Result != null ? CurrencyHelper.Format(Result.TotalPayment) : "";
    public string TotalInterestDisplay => Result != null ? CurrencyHelper.Format(Result.TotalInterest) : "";

    partial void OnResultChanged(LoanResult? value)
    {
        OnPropertyChanged(nameof(MonthlyPaymentDisplay));
        OnPropertyChanged(nameof(TotalPaymentDisplay));
        OnPropertyChanged(nameof(TotalInterestDisplay));
        UpdateChartData();
    }

    #endregion

    #region Chart Properties

    [ObservableProperty]
    private DonutChartVisualization.Segment[]? _donutSegments;

    private void UpdateChartData()
    {
        if (Result == null)
        {
            DonutSegments = null;
            return;
        }

        DonutSegments = new[]
        {
            new DonutChartVisualization.Segment
            {
                Value = (float)Result.LoanAmount,
                Color = new SKColor(0x22, 0xC5, 0x5E),
                Label = _localizationService.GetString("ChartRepayment") ?? "Repayment",
                ValueText = CurrencyHelper.Format(Result.LoanAmount)
            },
            new DonutChartVisualization.Segment
            {
                Value = (float)Result.TotalInterest,
                Color = new SKColor(0xF5, 0x9E, 0x0B),
                Label = _localizationService.GetString("ChartInterest") ?? "Interest",
                ValueText = CurrencyHelper.Format(Result.TotalInterest)
            }
        };
    }

    #endregion

    [RelayCommand]
    private void Calculate()
    {
        ErrorMessage = null;
        if (LoanAmount <= 0 || AnnualRate < 0 || Years <= 0)
        {
            HasResult = false;
            return;
        }

        try
        {
            Result = _financeEngine.CalculateLoan(LoanAmount, AnnualRate, Years);
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

        LoanAmount = 100000;
        AnnualRate = 4;
        Years = 20;
        Result = null;
        HasResult = false;
        ErrorMessage = null;
        DonutSegments = null;
    }

    [RelayCommand]
    private void GoBack() => GoBackAction?.Invoke();

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }
}
