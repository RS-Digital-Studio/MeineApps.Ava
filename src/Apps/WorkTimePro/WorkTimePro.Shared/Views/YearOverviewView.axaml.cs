using Avalonia.Controls;
using Avalonia.Labs.Controls;
using WorkTimePro.Graphics;
using WorkTimePro.ViewModels;

namespace WorkTimePro.Views;

public partial class YearOverviewView : UserControl
{
    private YearOverviewViewModel? _vm;

    public YearOverviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }

        if (DataContext is YearOverviewViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(YearOverviewViewModel.MonthlyWorkHoursData):
            case nameof(YearOverviewViewModel.MonthlyTargetHoursData):
            case nameof(YearOverviewViewModel.MonthLabels):
                MonthlyChartCanvas?.InvalidateSurface();
                break;
            case nameof(YearOverviewViewModel.CumulativeBalanceData):
                BalanceChartCanvas?.InvalidateSurface();
                break;
        }
    }

    // === PaintSurface Handler ===

    /// <summary>
    /// Zeichnet das monatliche Arbeitszeiten-Balkendiagramm (Ist vs. Soll).
    /// </summary>
    private void OnPaintMonthlyChart(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();
        if (_vm?.MonthLabels == null || _vm.MonthLabels.Length == 0) return;

        var bounds = canvas.LocalClipBounds;
        MonthlyBarChartVisualization.Render(canvas, bounds,
            _vm.MonthLabels, _vm.MonthlyWorkHoursData, _vm.MonthlyTargetHoursData);
    }

    /// <summary>
    /// Zeichnet den kumulativen Saldo-Trend als Spline-Kurve.
    /// </summary>
    private void OnPaintBalanceChart(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();
        if (_vm?.CumulativeBalanceData == null || _vm.CumulativeBalanceData.Length == 0) return;

        var bounds = canvas.LocalClipBounds;
        // Balance-Chart: Monatliche Balken + kumulative Linie
        MonthlyBarChartVisualization.Render(canvas, bounds,
            _vm.MonthLabels, _vm.MonthlyWorkHoursData, _vm.MonthlyTargetHoursData,
            showCumulative: true, cumulativeBalance: _vm.CumulativeBalanceData);
    }
}
