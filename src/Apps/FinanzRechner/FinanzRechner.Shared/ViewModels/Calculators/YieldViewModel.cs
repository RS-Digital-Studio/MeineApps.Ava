using System;
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

public partial class YieldViewModel : ObservableObject, IDisposable
{
    private readonly FinanceEngine _financeEngine;
    private readonly ILocalizationService _localizationService;

    // Debounce-Timer für Live-Berechnung (300ms Verzögerung)
    private Timer? _debounceTimer;

    public YieldViewModel(FinanceEngine financeEngine, ILocalizationService localizationService)
    {
        _financeEngine = financeEngine;
        _localizationService = localizationService;
    }

    public Action? GoBackAction { get; set; }

    #region Localized Text Properties

    public string TitleText => _localizationService.GetString("CalcYield") ?? "Yield";
    public string InitialInvestmentText => _localizationService.GetString("InitialInvestment") ?? "Initial Investment (EUR)";
    public string FinalValueText => _localizationService.GetString("FinalValue") ?? "Final Value (EUR)";
    public string YearsText => _localizationService.GetString("Years") ?? "Years";
    public string ResultText => _localizationService.GetString("Result") ?? "Result";
    public string EffectiveAnnualRateText => _localizationService.GetString("EffectiveAnnualRate") ?? "Effective Annual Rate";
    public string TotalReturnText => _localizationService.GetString("TotalReturn") ?? "Total Return";
    public string TotalReturnPercentText => _localizationService.GetString("TotalReturnPercent") ?? "Total Return (%)";
    public string InvestmentComparisonText => _localizationService.GetString("InvestmentComparison") ?? "Investment Comparison";
    public string InitialLegendText => _localizationService.GetString("Initial") ?? "Initial";
    public string FinalLegendText => _localizationService.GetString("Final") ?? "Final";
    public string ResetText => _localizationService.GetString("Reset") ?? "Reset";
    public string CalculateText => _localizationService.GetString("Calculate") ?? "Calculate";

    #endregion

    #region Input Properties

    [ObservableProperty]
    private double _initialInvestment = 10000;

    [ObservableProperty]
    private double _finalValue = 15000;

    [ObservableProperty]
    private int _years = 5;

    // Live-Berechnung mit Debouncing auslösen
    partial void OnInitialInvestmentChanged(double value) => ScheduleAutoCalculate();
    partial void OnFinalValueChanged(double value) => ScheduleAutoCalculate();
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
    private YieldResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    public string TotalReturnDisplay => Result != null ? CurrencyHelper.Format(Result.TotalReturn) : "";
    public string TotalReturnPercentDisplay => Result != null ? $"{Result.TotalReturnPercent:N2} %" : "";
    public string EffectiveAnnualRateDisplay => Result != null ? $"{Result.EffectiveAnnualRate:N2} % p.a." : "";

    partial void OnResultChanged(YieldResult? value)
    {
        OnPropertyChanged(nameof(TotalReturnDisplay));
        OnPropertyChanged(nameof(TotalReturnPercentDisplay));
        OnPropertyChanged(nameof(EffectiveAnnualRateDisplay));
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
        if (Result == null)
        {
            ChartSeries = Array.Empty<ISeries>();
            return;
        }

        ChartSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = new[] { Result.InitialInvestment },
                Name = _localizationService.GetString("ChartInitialValue") ?? "Initial Value",
                Fill = new SolidColorPaint(SKColor.Parse("#2196F3")),
                MaxBarWidth = 60
            },
            new ColumnSeries<double>
            {
                Values = new[] { Result.FinalValue },
                Name = _localizationService.GetString("ChartFinalValue") ?? "Final Value",
                Fill = new SolidColorPaint(SKColor.Parse("#4CAF50")),
                MaxBarWidth = 60
            }
        };

        XAxes = new Axis[]
        {
            new Axis
            {
                Labels = new[] { "" },
                ShowSeparatorLines = false
            }
        };
    }

    #endregion

    [RelayCommand]
    private void Calculate()
    {
        if (InitialInvestment <= 0 || FinalValue <= 0 || Years <= 0)
        {
            HasResult = false;
            return;
        }

        Result = _financeEngine.CalculateEffectiveYield(InitialInvestment, FinalValue, Years);
        HasResult = true;
    }

    [RelayCommand]
    private void Reset()
    {
        // Timer stoppen um keine verzögerte Berechnung nach Reset auszulösen
        _debounceTimer?.Dispose();
        _debounceTimer = null;

        InitialInvestment = 10000;
        FinalValue = 15000;
        Years = 5;
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
