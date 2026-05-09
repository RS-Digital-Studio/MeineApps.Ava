using Avalonia;
using Avalonia.Labs.Controls;
using BomberBlast.Graphics;
using SkiaSharp;

namespace BomberBlast.Controls;

/// <summary>
/// Avalonia-Wrapper für <see cref="EmptyStateRenderer"/> (Phase 28c-Hookup).
///
/// <para>Verwendet als Empty-State-Anzeige in Shop, Achievements, Collection-Listen.
/// Setze <see cref="StateType"/> über Binding oder XAML-Property:</para>
/// <code>
/// &lt;controls:EmptyStateCanvas StateType="Shop" Width="200" Height="200" /&gt;
/// </code>
/// </summary>
public class EmptyStateCanvas : SKCanvasView
{
    public static readonly StyledProperty<EmptyStateRenderer.EmptyStateType> StateTypeProperty =
        AvaloniaProperty.Register<EmptyStateCanvas, EmptyStateRenderer.EmptyStateType>(
            nameof(StateType),
            defaultValue: EmptyStateRenderer.EmptyStateType.Generic);

    public static readonly StyledProperty<SKColor> PrimaryColorProperty =
        AvaloniaProperty.Register<EmptyStateCanvas, SKColor>(
            nameof(PrimaryColor),
            defaultValue: new SKColor(255, 140, 60));

    public static readonly StyledProperty<SKColor> SecondaryColorProperty =
        AvaloniaProperty.Register<EmptyStateCanvas, SKColor>(
            nameof(SecondaryColor),
            defaultValue: new SKColor(80, 200, 220));

    public EmptyStateRenderer.EmptyStateType StateType
    {
        get => GetValue(StateTypeProperty);
        set => SetValue(StateTypeProperty, value);
    }

    public SKColor PrimaryColor
    {
        get => GetValue(PrimaryColorProperty);
        set => SetValue(PrimaryColorProperty, value);
    }

    public SKColor SecondaryColor
    {
        get => GetValue(SecondaryColorProperty);
        set => SetValue(SecondaryColorProperty, value);
    }

    private readonly System.Diagnostics.Stopwatch _timer = System.Diagnostics.Stopwatch.StartNew();
    private readonly Avalonia.Threading.DispatcherTimer _renderTimer;

    static EmptyStateCanvas()
    {
        StateTypeProperty.Changed.AddClassHandler<EmptyStateCanvas>((x, _) => x.InvalidateSurface());
        PrimaryColorProperty.Changed.AddClassHandler<EmptyStateCanvas>((x, _) => x.InvalidateSurface());
        SecondaryColorProperty.Changed.AddClassHandler<EmptyStateCanvas>((x, _) => x.InvalidateSurface());
    }

    public EmptyStateCanvas()
    {
        PaintSurface += OnPaintSurface;

        // Idle-Animation: 30 FPS Re-Render damit Sway/Pulse sichtbar werden
        _renderTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33),
        };
        _renderTimer.Tick += (_, _) => InvalidateSurface();
        AttachedToVisualTree += (_, _) => _renderTimer.Start();
        DetachedFromVisualTree += (_, _) => _renderTimer.Stop();
    }

    private void OnPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var bounds = canvas.LocalClipBounds;
        var time = (float)_timer.Elapsed.TotalSeconds;

        EmptyStateRenderer.Draw(canvas, bounds, StateType, PrimaryColor, SecondaryColor, time);
    }
}
