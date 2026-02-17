using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;
using FinanzRechner.Graphics;
using FinanzRechner.Helpers;
using FinanzRechner.Models;
using FinanzRechner.ViewModels;

namespace FinanzRechner.Views;

public partial class BudgetsView : UserControl
{
    public BudgetsView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is BudgetsViewModel vm)
        {
            // Bei Budget-Änderungen Gauge invalidieren
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(vm.TotalBudgetPercentage)
                    or nameof(vm.IsTotalBudgetOverLimit) or nameof(vm.BudgetStatuses))
                {
                    BudgetGaugeCanvas?.InvalidateSurface();
                }
            };
        }
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is BudgetsViewModel vm)
            await vm.LoadBudgetsCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Zeichnet den Budget-Halbkreis-Tachometer mit Spent/Limit-Anzeige.
    /// </summary>
    private void OnPaintBudgetGauge(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not BudgetsViewModel vm || !vm.HasTotalBudget) return;

        BudgetGaugeVisualization.Render(canvas, bounds,
            vm.TotalBudgetPercentage,
            vm.TotalBudgetSpentDisplay,
            vm.TotalBudgetLimitDisplay,
            vm.IsTotalBudgetOverLimit);
    }

    /// <summary>
    /// Zeichnet den Budget-Fortschrittsbalken pro Kategorie (im DataTemplate).
    /// </summary>
    private void OnPaintBudgetProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (sender is not SKCanvasView canvasView) return;
        if (canvasView.DataContext is not BudgetStatus budget) return;

        float progress = (float)(budget.PercentageUsed / 100.0);

        // Farbe je nach Warnstufe: Grün → Gelb → Rot
        SKColor startColor, endColor;
        switch (budget.AlertLevel)
        {
            case BudgetAlertLevel.Exceeded:
                startColor = SKColor.Parse("#EF4444");
                endColor = SKColor.Parse("#DC2626");
                break;
            case BudgetAlertLevel.Warning:
                startColor = SKColor.Parse("#F59E0B");
                endColor = SKColor.Parse("#D97706");
                break;
            default:
                // Kategorie-Farbe als Gradient verwenden
                var catColor = CategoryLocalizationHelper.GetCategoryColor(budget.Category);
                startColor = catColor;
                endColor = SkiaThemeHelper.AdjustBrightness(catColor, 0.85f);
                break;
        }

        LinearProgressVisualization.Render(canvas, bounds, progress,
            startColor, endColor, showText: true, glowEnabled: true);
    }
}
