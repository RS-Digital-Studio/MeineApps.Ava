using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanzRechner.Helpers;
using FinanzRechner.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MeineApps.Core.Ava.Localization;
using SkiaSharp;

namespace FinanzRechner.ViewModels.Calculators;

public partial class AmortizationViewModel : ObservableObject, IDisposable
{
    private readonly FinanceEngine _financeEngine;
    private readonly ILocalizationService _localizationService;

    // Debounce-Timer für Live-Berechnung (300ms Verzögerung)
    private Timer? _debounceTimer;

    public AmortizationViewModel(FinanceEngine financeEngine, ILocalizationService localizationService)
    {
        _financeEngine = financeEngine;
        _localizationService = localizationService;
    }

    public Action? GoBackAction { get; set; }

    #region Localized Text Properties

    public string TitleText => _localizationService.GetString("CalcAmortization") ?? "Amortization";
    public string LoanAmountText => _localizationService.GetString("LoanAmount") ?? "Loan Amount (EUR)";
    public string AnnualRateText => _localizationService.GetString("AnnualRate") ?? "Annual Rate (%)";
    public string YearsText => _localizationService.GetString("Years") ?? "Years";
    public string ResultText => _localizationService.GetString("Result") ?? "Result";
    public string MonthlyPaymentText => _localizationService.GetString("MonthlyPayment") ?? "Monthly Payment";
    public string TotalInterestText => _localizationService.GetString("TotalInterest") ?? "Total Interest";
    public string DebtReductionText => _localizationService.GetString("DebtReduction") ?? "Debt Reduction";
    public string RemainingDebtText => _localizationService.GetString("RemainingDebt") ?? "Remaining Debt";
    public string AmortizationScheduleText => _localizationService.GetString("AmortizationSchedule") ?? "Payment Schedule";
    public string PrincipalPortionText => _localizationService.GetString("PrincipalPortion") ?? "Principal";
    public string InterestPortionText => _localizationService.GetString("InterestPortion") ?? "Interest";
    public string BalanceText => _localizationService.GetString("Balance") ?? "Balance";
    public string ResetText => _localizationService.GetString("Reset") ?? "Reset";
    public string CalculateText => _localizationService.GetString("Calculate") ?? "Calculate";

    #endregion

    #region Input Properties

    [ObservableProperty]
    private double _loanAmount = 50000;

    [ObservableProperty]
    private double _annualRate = 5;

    [ObservableProperty]
    private int _years = 5;

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
    private AmortizationResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string? _errorMessage;

    public string MonthlyPaymentDisplay => Result != null ? CurrencyHelper.Format(Result.MonthlyPayment) : "";
    public string TotalInterestDisplay => Result != null ? CurrencyHelper.Format(Result.TotalInterest) : "";

    partial void OnResultChanged(AmortizationResult? value)
    {
        OnPropertyChanged(nameof(MonthlyPaymentDisplay));
        OnPropertyChanged(nameof(TotalInterestDisplay));
        UpdateChartData();
    }

    #endregion

    #region Chart Properties

    [ObservableProperty]
    private ISeries[] _chartSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _xAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _yAxes = Array.Empty<Axis>();

    private void UpdateChartData()
    {
        if (Result == null || Result.Schedule.Count == 0)
        {
            ChartSeries = Array.Empty<ISeries>();
            return;
        }

        // Tilgung und Zinsen pro Jahr aggregieren
        var principalPerYear = new List<double>();
        var interestPerYear = new List<double>();
        var labels = new List<string>();

        for (int year = 1; year <= Years; year++)
        {
            var yearEntries = Result.Schedule
                .Where(e => e.Month > (year - 1) * 12 && e.Month <= year * 12)
                .ToList();
            principalPerYear.Add(yearEntries.Sum(e => e.Principal));
            interestPerYear.Add(yearEntries.Sum(e => e.Interest));
            labels.Add(year.ToString());
        }

        ChartSeries = new ISeries[]
        {
            new StackedColumnSeries<double>
            {
                Values = principalPerYear,
                Name = _localizationService.GetString("PrincipalPortion") ?? "Principal",
                Fill = new SolidColorPaint(new SKColor(0x22, 0xC5, 0x5E)),
                Stroke = new SolidColorPaint(new SKColor(0x22, 0xC5, 0x5E)) { StrokeThickness = 0 },
                Rx = 3,
                Ry = 3,
                MaxBarWidth = 35
            },
            new StackedColumnSeries<double>
            {
                Values = interestPerYear,
                Name = _localizationService.GetString("InterestPortion") ?? "Interest",
                Fill = new SolidColorPaint(new SKColor(0xF5, 0x9E, 0x0B)),
                Stroke = new SolidColorPaint(new SKColor(0xF5, 0x9E, 0x0B)) { StrokeThickness = 0 },
                Rx = 3,
                Ry = 3,
                MaxBarWidth = 35
            }
        };

        XAxes = new Axis[]
        {
            new Axis
            {
                Name = _localizationService.GetString("ChartYears") ?? "Years",
                Labels = labels.ToArray()
            }
        };
    }

    #endregion

    #region Schedule Toggle

    [ObservableProperty]
    private bool _isScheduleExpanded;

    [RelayCommand]
    private void ToggleSchedule() => IsScheduleExpanded = !IsScheduleExpanded;

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
            Result = _financeEngine.CalculateAmortization(LoanAmount, AnnualRate, Years);
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

        LoanAmount = 50000;
        AnnualRate = 5;
        Years = 5;
        Result = null;
        HasResult = false;
        ErrorMessage = null;
        ChartSeries = Array.Empty<ISeries>();
    }

    [RelayCommand]
    private void GoBack() => GoBackAction?.Invoke();

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }
}
