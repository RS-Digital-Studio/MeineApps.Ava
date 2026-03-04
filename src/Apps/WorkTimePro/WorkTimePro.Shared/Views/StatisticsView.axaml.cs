using Avalonia.Controls;
using Avalonia.Labs.Controls;
using MeineApps.UI.SkiaSharp;
using WorkTimePro.Graphics;
using WorkTimePro.ViewModels;

namespace WorkTimePro.Views;

public partial class StatisticsView : UserControl
{
    private StatisticsViewModel? _vm;

    public StatisticsView()
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

        if (DataContext is StatisticsViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Bei Datenänderungen alle Canvas invalidieren
        switch (e.PropertyName)
        {
            case nameof(StatisticsViewModel.PauseSegments):
                PauseDonutCanvas?.InvalidateSurface();
                break;
            case nameof(StatisticsViewModel.WeeklyHoursData):
            case nameof(StatisticsViewModel.WeeklyLabels):
                WeeklyChartCanvas?.InvalidateSurface();
                break;
            case nameof(StatisticsViewModel.OvertimeDailyBalance):
            case nameof(StatisticsViewModel.OvertimeCumulativeBalance):
                OvertimeChartCanvas?.InvalidateSurface();
                break;
            case nameof(StatisticsViewModel.WeekdayAvgHours):
            case nameof(StatisticsViewModel.WeekdayLabels):
                WeekdayRadialCanvas?.InvalidateSurface();
                break;
            case nameof(StatisticsViewModel.ProjectSegments):
                ProjectDonutCanvas?.InvalidateSurface();
                break;
            case nameof(StatisticsViewModel.EmployerSegments):
                EmployerDonutCanvas?.InvalidateSurface();
                break;
            case nameof(StatisticsViewModel.ShowTable):
            // Beim Wechsel zu Charts alle Canvas invalidieren
                InvalidateAllCanvases();
                break;
        }
    }

    private void InvalidateAllCanvases()
    {
        PauseDonutCanvas?.InvalidateSurface();
        WeeklyChartCanvas?.InvalidateSurface();
        OvertimeChartCanvas?.InvalidateSurface();
        WeekdayRadialCanvas?.InvalidateSurface();
        ProjectDonutCanvas?.InvalidateSurface();
        EmployerDonutCanvas?.InvalidateSurface();
    }

    // === PaintSurface Handler ===

    private void OnPaintPauseDonut(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();
        if (_vm?.PauseSegments == null || _vm.PauseSegments.Length == 0) return;

        var bounds = canvas.LocalClipBounds;
        DonutChartVisualization.Render(canvas, bounds, _vm.PauseSegments,
            innerRadiusFraction: 0.5f, showLabels: true, showLegend: true);
    }

    private void OnPaintWeeklyChart(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();
        if (_vm?.WeeklyLabels == null || _vm.WeeklyLabels.Length == 0) return;

        var bounds = canvas.LocalClipBounds;
        WeeklyWorkChartVisualization.Render(canvas, bounds,
            _vm.WeeklyLabels, _vm.WeeklyHoursData, _vm.WeeklyTargetHours);
    }

    private void OnPaintOvertimeChart(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();
        if (_vm?.OvertimeDailyBalance == null || _vm.OvertimeDailyBalance.Length == 0) return;

        var bounds = canvas.LocalClipBounds;
        OvertimeSplineVisualization.Render(canvas, bounds,
            _vm.OvertimeDailyBalance, _vm.OvertimeCumulativeBalance, _vm.OvertimeDateLabels);
    }

    private void OnPaintWeekdayRadial(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();
        if (_vm?.WeekdayLabels == null || _vm.WeekdayLabels.Length == 0) return;

        var bounds = canvas.LocalClipBounds;
        WeekdayRadialVisualization.Render(canvas, bounds,
            _vm.WeekdayLabels, _vm.WeekdayAvgHours, _vm.WeekdayTargetPerDay,
            centerLabel: "\u00d8/Tag");
    }

    private void OnPaintProjectDonut(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();
        if (_vm?.ProjectSegments == null || _vm.ProjectSegments.Length == 0) return;

        var bounds = canvas.LocalClipBounds;
        DonutChartVisualization.Render(canvas, bounds, _vm.ProjectSegments,
            innerRadiusFraction: 0.5f, showLabels: true, showLegend: true);
    }

    private void OnPaintEmployerDonut(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();
        if (_vm?.EmployerSegments == null || _vm.EmployerSegments.Length == 0) return;

        var bounds = canvas.LocalClipBounds;
        DonutChartVisualization.Render(canvas, bounds, _vm.EmployerSegments,
            innerRadiusFraction: 0.5f, showLabels: true, showLegend: true);
    }
}
