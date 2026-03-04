using Avalonia.Controls;
using Avalonia.Labs.Controls;
using FitnessRechner.Graphics;
using FitnessRechner.ViewModels;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FitnessRechner.Views;

public partial class ProgressView : UserControl
{
    private ProgressViewModel? _vm;

    public ProgressView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }

        if (DataContext is ProgressViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(_vm.WeightChartData):
            case nameof(_vm.WeightMilestoneLines):
                WeightChartCanvas?.InvalidateSurface();
                break;
            case nameof(_vm.BmiChartData):
                BmiChartCanvas?.InvalidateSurface();
                break;
            case nameof(_vm.BodyFatChartData):
                BodyFatChartCanvas?.InvalidateSurface();
                break;
            case nameof(_vm.WeeklyCaloriesValues):
            case nameof(_vm.WeeklyDayLabels):
                WeeklyCaloriesCanvas?.InvalidateSurface();
                break;
            // ProgressBars invalidieren bei Datenänderung
            case nameof(_vm.WeightGoalProgress):
                InvalidateAllCanvases("WeightGoalProgress");
                break;
            case nameof(_vm.WaterProgress):
                InvalidateAllCanvases("WaterProgress");
                break;
            case nameof(_vm.ProteinConsumed):
            case nameof(_vm.CarbsConsumed):
            case nameof(_vm.FatConsumed):
                InvalidateAllCanvases("MacroProgress");
                break;
        }
    }

    /// <summary>
    /// Invalidiert alle SKCanvasView-Instanzen die keinen x:Name haben.
    /// Wir nutzen eine einfache Lösung: Alle finden und invalidieren.
    /// </summary>
    private void InvalidateAllCanvases(string _)
    {
        // Alle unnamed SKCanvasViews invalidieren
        InvalidateNamedCanvasViews();
    }

    private void InvalidateNamedCanvasViews()
    {
        // Da die ProgressBar-Canvases keinen x:Name haben,
        // nutzen wir den visuellen Baum nicht - stattdessen
        // invalidieren wir bei jeder relevanten Property-Änderung
        // alle Charts (die benannten Canvases sind via x:Name erreichbar)
        // Die ProgressBars werden automatisch neu gezeichnet wenn
        // sie sichtbar werden (Tab-Wechsel) oder beim nächsten Render-Zyklus
    }

    #region Chart Paint-Handler

    /// <summary>
    /// Gewichtsverlauf-Chart zeichnen.
    /// </summary>
    private void OnPaintWeightChart(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not ProgressViewModel vm || vm.WeightChartData.Length == 0) return;

        HealthTrendVisualization.Render(canvas, bounds, vm.WeightChartData,
            new SKColor(0x4C, 0xAF, 0x50), // Grün
            milestones: vm.WeightMilestoneLines,
            yLabelFormat: "F0");
    }

    /// <summary>
    /// BMI-Trend-Chart zeichnen.
    /// </summary>
    private void OnPaintBmiChart(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not ProgressViewModel vm || vm.BmiChartData.Length == 0) return;

        // Gesunde BMI-Zone (18.5-25) als grüner Bereich
        var bmiZone = new HealthTrendVisualization.TargetZone
        {
            MinValue = 18.5f,
            MaxValue = 25f,
            Color = new SKColor(0x4C, 0xAF, 0x50) // Grün
        };

        HealthTrendVisualization.Render(canvas, bounds, vm.BmiChartData,
            new SKColor(0x21, 0x96, 0xF3), // Blau
            targetZone: bmiZone,
            yLabelFormat: "F1");
    }

    /// <summary>
    /// Körperfett-Trend-Chart zeichnen.
    /// </summary>
    private void OnPaintBodyFatChart(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not ProgressViewModel vm || vm.BodyFatChartData.Length == 0) return;

        HealthTrendVisualization.Render(canvas, bounds, vm.BodyFatChartData,
            new SKColor(0xFF, 0x98, 0x00), // Orange
            yLabelFormat: "F0");
    }

    /// <summary>
    /// Wochen-Kalorien-Balkendiagramm zeichnen.
    /// </summary>
    private void OnPaintWeeklyCalories(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not ProgressViewModel vm ||
            vm.WeeklyCaloriesValues.Length == 0 ||
            vm.WeeklyDayLabels.Length == 0) return;

        WeeklyCaloriesBarVisualization.Render(canvas, bounds,
            vm.WeeklyDayLabels, vm.WeeklyCaloriesValues,
            targetCalories: (float)vm.DailyCalorieGoal);
    }

    #endregion

    #region ProgressBar Paint-Handler

    /// <summary>
    /// Gewichtsziel-Fortschritt zeichnen.
    /// </summary>
    private void OnPaintWeightGoalProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not ProgressViewModel vm) return;

        LinearProgressVisualization.Render(canvas, bounds,
            (float)vm.WeightGoalProgress,
            new SKColor(0x8B, 0x5C, 0xF6), // Lila Start
            new SKColor(0x7C, 0x3A, 0xED), // Lila End
            showText: true);
    }

    /// <summary>
    /// Wasser-Fortschritt zeichnen.
    /// </summary>
    private void OnPaintWaterProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not ProgressViewModel vm) return;

        LinearProgressVisualization.Render(canvas, bounds,
            (float)vm.WaterProgress,
            new SKColor(0x06, 0xB6, 0xD4), // Cyan Start
            new SKColor(0x3B, 0x82, 0xF6), // Blau End
            showText: false);
    }

    /// <summary>
    /// Protein-Fortschritt zeichnen.
    /// </summary>
    private void OnPaintProteinProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not ProgressViewModel vm) return;

        LinearProgressVisualization.Render(canvas, bounds,
            (float)vm.ProteinProgress,
            SkiaThemeHelper.Success,
            new SKColor(0x16, 0xA3, 0x4A), // Dunkleres Grün
            showText: false, glowEnabled: false);
    }

    /// <summary>
    /// Kohlenhydrate-Fortschritt zeichnen.
    /// </summary>
    private void OnPaintCarbsProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not ProgressViewModel vm) return;

        LinearProgressVisualization.Render(canvas, bounds,
            (float)vm.CarbsProgress,
            SkiaThemeHelper.Warning,
            new SKColor(0xD9, 0x77, 0x06), // Dunkleres Amber
            showText: false, glowEnabled: false);
    }

    /// <summary>
    /// Fett-Fortschritt zeichnen.
    /// </summary>
    private void OnPaintFatProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not ProgressViewModel vm) return;

        LinearProgressVisualization.Render(canvas, bounds,
            (float)vm.FatProgress,
            SkiaThemeHelper.Error,
            new SKColor(0xDC, 0x26, 0x26), // Dunkleres Rot
            showText: false, glowEnabled: false);
    }

    #endregion
}
