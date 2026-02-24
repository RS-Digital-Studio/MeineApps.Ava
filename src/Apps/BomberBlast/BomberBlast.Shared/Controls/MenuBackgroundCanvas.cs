using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BomberBlast.Graphics;

namespace BomberBlast.Controls;

/// <summary>
/// Wiederverwendbares UserControl mit animiertem Bomberman-Menü-Hintergrund.
/// Nutzt MenuBackgroundRenderer für SkiaSharp-Rendering mit ~30fps Timer-Animation.
/// Startet automatisch bei Einfügen in den Visual Tree, stoppt beim Entfernen.
/// </summary>
public class MenuBackgroundCanvas : UserControl
{
    private SKCanvasView? _canvas;
    private DispatcherTimer? _timer;
    private readonly Stopwatch _stopwatch = new();
    private bool _initialized;

    public MenuBackgroundCanvas()
    {
        // Keine Touch-/Klick-Interaktion (reiner Hintergrund)
        IsHitTestVisible = false;

        // Canvas füllt das gesamte Control
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        _canvas = new SKCanvasView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _canvas.PaintSurface += OnPaintSurface;

        Content = _canvas;

        // Automatisch starten/stoppen bei Visual-Tree-Änderungen
        AttachedToVisualTree += (_, _) => Start();
        DetachedFromVisualTree += (_, _) => Stop();
    }

    /// <summary>
    /// Startet die Animations-Schleife (~30fps).
    /// Initialisiert den Renderer beim ersten Aufruf.
    /// </summary>
    public void Start()
    {
        if (this.GetVisualRoot() == null)
            return;

        // Renderer einmalig initialisieren
        if (!_initialized)
        {
            MenuBackgroundRenderer.Initialize(42);
            _initialized = true;
        }

        // Bestehenden Timer stoppen (kein doppelter Timer)
        _timer?.Stop();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => _canvas?.InvalidateSurface();
        _timer.Start();
        _stopwatch.Restart();
    }

    /// <summary>
    /// Stoppt die Animations-Schleife und den Timer.
    /// </summary>
    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
        _stopwatch.Stop();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear();

        var time = (float)_stopwatch.Elapsed.TotalSeconds;
        MenuBackgroundRenderer.Render(canvas, bounds.Width, bounds.Height, time);
    }
}
