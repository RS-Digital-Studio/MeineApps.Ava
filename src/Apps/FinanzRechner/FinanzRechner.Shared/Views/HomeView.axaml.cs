using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using SkiaSharp;
using FinanzRechner.Graphics;
using FinanzRechner.ViewModels;

namespace FinanzRechner.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainViewModel vm)
        {
            // Bei Budget-Änderungen Gauge invalidieren
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(vm.OverallBudgetPercentage)
                    or nameof(vm.HasBudgets) or nameof(vm.TopBudgets))
                {
                    BudgetGaugeCanvas?.InvalidateSurface();
                }
            };
        }
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            await vm.OnAppearingAsync();
    }

    /// <summary>
    /// Zeichnet den Budget-Halbkreis-Tachometer.
    /// </summary>
    private void OnPaintBudgetGauge(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not MainViewModel vm || !vm.HasBudgets) return;

        BudgetGaugeVisualization.Render(canvas, bounds,
            vm.OverallBudgetPercentage, "", "",
            vm.OverallBudgetPercentage > 100);
    }
}
