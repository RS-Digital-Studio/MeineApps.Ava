using Avalonia.Controls;
using Avalonia.Labs.Controls;
using BingXBot.Graphics;
using BingXBot.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace BingXBot.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();

        // DataContext aus DI holen
        DataContext = App.Services.GetRequiredService<DashboardViewModel>();
    }

    private void OnEquityPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (DataContext is not DashboardViewModel vm) return;

        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds; // NICHT e.Info.Width/Height (DPI-Problem!)
        var data = vm.EquityData.ToList();

        EquityChartRenderer.Render(canvas, bounds, data, vm.Balance > 0 ? vm.Balance : 10000m);
    }
}
