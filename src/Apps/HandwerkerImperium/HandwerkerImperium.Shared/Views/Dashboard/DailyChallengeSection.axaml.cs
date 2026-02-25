using Avalonia.Controls;
using Avalonia.Labs.Controls;
using SkiaSharp;

namespace HandwerkerImperium.Views.Dashboard;

public partial class DailyChallengeSection : UserControl
{
    public DailyChallengeSection()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Tages-Challenge Fortschritt (CraftPrimary orange).
    /// DataContext ist DailyChallenge (aus DataTemplate).
    /// </summary>
    private void OnPaintChallengeProgress(object? sender, SKPaintSurfaceEventArgs e)
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
            new SKColor(0xD9, 0x77, 0x06), // CraftPrimary
            new SKColor(0xF5, 0x9E, 0x0B), // CraftPrimaryLight
            showText: false, glowEnabled: false);
    }
}
