using Avalonia;
using Avalonia.Labs.Controls;
using BomberBlast.Graphics;

namespace BomberBlast.Controls;

/// <summary>
/// Bindbare SKCanvasView fuer Medaillen-Anzeige (Gold/Silber/Bronze).
/// Basiert auf Sterne-Anzahl: 3=Gold, 2=Silber, 1=Bronze, 0=nichts.
/// </summary>
public class MedalCanvas : SKCanvasView
{
    public static readonly StyledProperty<int> StarsProperty =
        AvaloniaProperty.Register<MedalCanvas, int>(nameof(Stars));

    public int Stars
    {
        get => GetValue(StarsProperty);
        set => SetValue(StarsProperty, value);
    }

    // Frame-Rate-unabhängige Zeitmessung
    private readonly System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

    static MedalCanvas()
    {
        StarsProperty.Changed.AddClassHandler<MedalCanvas>((x, _) => x.InvalidateSurface());
    }

    public MedalCanvas()
    {
        PaintSurface += OnPaintSurface;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();

        if (Stars <= 0) return;

        var bounds = canvas.LocalClipBounds;
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float radius = Math.Min(bounds.Width, bounds.Height) / 2f * 0.85f;

        int rank = Stars switch
        {
            >= 3 => 1,
            2 => 2,
            _ => 3
        };

        float animTime = (float)_stopwatch.Elapsed.TotalSeconds;
        GameOverVisualization.DrawMedal(canvas, cx, cy, radius, rank, animTime);
    }
}
