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

        var purchasingPowerValues = new List<double>();
        var rate = AnnualInflationRate / 100;

        for (int year = 0; year <= Years; year++)
        {
            // Kaufkraft des heutigen Betrags in jedem Jahr
            var value = CurrentAmount / Math.Pow(1 + rate, year);
            purchasingPowerValues.Add(value);
        }

        ChartSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = purchasingPowerValues,
                Name = _localizationService.GetString("ChartPurchasingPower") ?? "Purchasing Power",
                Fill = new SolidColorPaint(SKColor.Parse("#EF4444").WithAlpha(50)),
                Stroke = new SolidColorPaint(SKColor.Parse("#EF4444")) { StrokeThickness = 3 },
                GeometryFill = new SolidColorPaint(SKColor.Parse("#EF4444")),
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
                MinStep = 1,
                Labels = Enumerable.Range(0, Years + 1).Select(x => x.ToString()).ToArray()
            }
        };
    }

    #endregion

    [RelayCommand]
    private void Calculate()
    {
        if (CurrentAmount <= 0 || Years <= 0)
        {
            HasResult = false;
            return;
        }

        Result = _financeEngine.CalculateInflation(CurrentAmount, AnnualInflationRate, Years);
        HasResult = true;
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
