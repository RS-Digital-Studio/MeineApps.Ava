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
    private ISeries[] _chartSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _xAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _yAxes = Array.Empty<Axis>();

    private void UpdateChartData()
    {
        if (Result == null || Years <= 0)
        {
            ChartSeries = Array.Empty<ISeries>();
            return;
        }

        var depositValues = new List<double>();
        var totalValues = new List<double>();
        var monthlyRate = (AnnualRate / 100) / 12;

        for (int year = 0; year <= Years; year++)
        {
            var months = year * 12;
            var deposits = InitialDeposit + (MonthlyDeposit * months);
            depositValues.Add(deposits);

            // Calculate total value for this year
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
            totalValues.Add(total);
        }

        ChartSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = depositValues,
                Name = _localizationService.GetString("ChartDeposits") ?? "Deposits",
                Fill = null,
                Stroke = new SolidColorPaint(SKColor.Parse("#2196F3")) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColor.Parse("#2196F3")),
                GeometryStroke = null,
                GeometrySize = 6
            },
            new LineSeries<double>
            {
                Values = totalValues,
                Name = _localizationService.GetString("ChartCapital") ?? "Capital",
                Fill = new SolidColorPaint(SKColor.Parse("#4CAF50").WithAlpha(50)),
                Stroke = new SolidColorPaint(SKColor.Parse("#4CAF50")) { StrokeThickness = 3 },
                GeometryFill = new SolidColorPaint(SKColor.Parse("#4CAF50")),
                GeometryStroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                GeometrySize = 8
            }
        };

        XAxes = new Axis[]
        {
            new Axis
            {
                Name = _localizationService.GetString("ChartYears") ?? "Years",
                MinLimit = 0,
                MaxLimit = Years,
                MinStep = Years > 10 ? 5 : 1,
                Labels = Enumerable.Range(0, Years + 1).Where(x => Years <= 10 || x % 5 == 0).Select(x => x.ToString()).ToArray()
            }
        };
    }

    #endregion

    [RelayCommand]
    private void Calculate()
    {
        if (MonthlyDeposit < 0 || Years <= 0)
        {
            HasResult = false;
            return;
        }

        Result = _financeEngine.CalculateSavingsPlan(MonthlyDeposit, AnnualRate, Years, InitialDeposit);
        HasResult = true;
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
