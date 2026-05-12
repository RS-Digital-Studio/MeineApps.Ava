using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerImperium.ViewModels;
using SkiaSharp;

namespace HandwerkerImperium.Views.Dashboard;

public partial class DashboardHeader : UserControl
{
    public DashboardHeader()
    {
        InitializeComponent();
    }

    /// <summary>
    /// XP-Level-Fortschritt-Bar (amber/gold Gradient) — wurde aus DashboardView.axaml.cs
    /// extrahiert da der Canvas jetzt im DashboardHeader-UserControl lebt.
    /// </summary>
    private void OnPaintLevelProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        if (DataContext is not MainViewModel vm) return;

        MeineApps.UI.SkiaSharp.LinearProgressVisualization.Render(canvas, bounds,
            (float)vm.HeaderVM.LevelProgress,
            new SKColor(0xF5, 0x9E, 0x0B), // Amber Start
            new SKColor(0xFF, 0xD7, 0x00), // Gold End
            showText: false, glowEnabled: true);
    }
}
