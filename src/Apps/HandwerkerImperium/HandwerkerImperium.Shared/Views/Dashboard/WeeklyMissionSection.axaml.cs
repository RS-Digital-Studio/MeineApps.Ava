using Avalonia.Controls;
using Avalonia.Labs.Controls;
using SkiaSharp;

namespace HandwerkerImperium.Views.Dashboard;

public partial class WeeklyMissionSection : UserControl
{
    public WeeklyMissionSection()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Wöchentliche Missionen Fortschritt (Amber/Gold).
    /// DataContext ist WeeklyMission (aus DataTemplate).
    /// </summary>
    private void OnPaintWeeklyMissionProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (sender is not SKCanvasView canvasView) return;
        var dc = canvasView.DataContext;
        if (dc == null) return;

        // IProgressProvider statt Reflection
        if (dc is not Models.IProgressProvider provider) return;
        var progress = (float)provider.Progress;

        MeineApps.UI.SkiaSharp.LinearProgressVisualization.Render(canvas, bounds,
            progress,
            new SKColor(0xD9, 0x77, 0x06), // Amber
            new SKColor(0xFF, 0xD7, 0x00), // Gold
            showText: false, glowEnabled: false);
    }
}
