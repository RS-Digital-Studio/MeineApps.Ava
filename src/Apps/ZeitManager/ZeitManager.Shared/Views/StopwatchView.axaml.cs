using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using SkiaSharp;
using ZeitManager.Graphics;
using ZeitManager.ViewModels;

namespace ZeitManager.Views;

public partial class StopwatchView : UserControl
{
    private DispatcherTimer? _animTimer;
    private float _animTime;

    public StopwatchView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is StopwatchViewModel vm)
        {
            // Bei Property-Änderungen Canvas invalidieren
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(vm.ElapsedTimeFormatted) or nameof(vm.IsRunning) or nameof(vm.Laps))
                {
                    StopwatchCanvas?.InvalidateSurface();
                    UpdateAnimation(vm.IsRunning);
                }
            };
        }
    }

    private void UpdateAnimation(bool isRunning)
    {
        if (isRunning && _animTimer == null)
        {
            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _animTimer.Tick += (_, _) =>
            {
                _animTime += 0.033f;
                StopwatchCanvas?.InvalidateSurface();
            };
            _animTimer.Start();
        }
        else if (!isRunning && _animTimer != null)
        {
            _animTimer.Stop();
            _animTimer = null;
        }
    }

    private void OnPaintStopwatch(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not StopwatchViewModel vm) return;

        // ElapsedTimeFormatted parsen → Sekunden (Format: "mm:ss.cc")
        double elapsedSeconds = ParseElapsedTime(vm.ElapsedTimeFormatted);

        StopwatchVisualization.Render(canvas, bounds,
            elapsedSeconds, vm.IsRunning, vm.Laps.Count, _animTime);
    }

    /// <summary>
    /// Parst "mm:ss.cc" zu Sekunden.
    /// </summary>
    private static double ParseElapsedTime(string formatted)
    {
        try
        {
            // Format: "00:12.45" oder "01:30.00"
            var parts = formatted.Split(':');
            if (parts.Length != 2) return 0;

            int minutes = int.Parse(parts[0]);
            var secParts = parts[1].Split('.');
            int seconds = int.Parse(secParts[0]);
            int centis = secParts.Length > 1 ? int.Parse(secParts[1]) : 0;

            return minutes * 60.0 + seconds + centis / 100.0;
        }
        catch
        {
            return 0;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animTimer?.Stop();
        _animTimer = null;
    }
}
