using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using SkiaSharp;
using FinanzRechner.Graphics;
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
}
