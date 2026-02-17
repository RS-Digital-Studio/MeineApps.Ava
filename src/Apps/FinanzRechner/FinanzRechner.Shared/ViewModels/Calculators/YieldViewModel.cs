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

    [ObservableProperty]
    private string? _errorMessage;

    public string TotalReturnDisplay => Result != null ? CurrencyHelper.Format(Result.TotalReturn) : "";
    public string TotalReturnPercentDisplay => Result != null ? $"{Result.TotalReturnPercent:N2} %" : "";
    public string EffectiveAnnualRateDisplay => Result != null
        ? $"{Result.EffectiveAnnualRate:N2} % {_localizationService.GetString("PerAnnum") ?? "p.a."}" : "";

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
                Value = (float)Result.InitialInvestment,
                Color = new SKColor(0x3B, 0x82, 0xF6),
                Label = _localizationService.GetString("ChartInitialValue") ?? "Initial Value",
                ValueText = CurrencyHelper.Format(Result.InitialInvestment)
            },
            new DonutChartVisualization.Segment
            {
                Value = (float)Result.TotalReturn,
                Color = new SKColor(0x22, 0xC5, 0x5E),
                Label = _localizationService.GetString("TotalReturn") ?? "Return",
                ValueText = CurrencyHelper.Format(Result.TotalReturn)
            }
        };
    }

    #endregion

    [RelayCommand]
    private void Calculate()
    {
        ErrorMessage = null;
        if (InitialInvestment <= 0 || FinalValue <= 0 || Years <= 0)
        {
            HasResult = false;
            return;
        }

        try
        {
            Result = _financeEngine.CalculateEffectiveYield(InitialInvestment, FinalValue, Years);
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

        InitialInvestment = 10000;
        FinalValue = 15000;
        Years = 5;
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
