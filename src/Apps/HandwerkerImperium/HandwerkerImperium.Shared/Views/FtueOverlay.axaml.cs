using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.ViewModels;
using SkiaSharp;

namespace HandwerkerImperium.Views;

/// <summary>
/// AAA-Audit P0 FTUE-UI: Spotlight-Overlay-View. Code-Behind misst die Bounds des
/// Spotlight-Targets (per AutomationId), gibt sie ans VM weiter und zeichnet das
/// SkiaSharp-Spotlight per <see cref="FtueSpotlightRenderer"/>.
/// </summary>
public partial class FtueOverlay : UserControl
{
    private readonly DispatcherTimer _renderTimer;
    private readonly Stopwatch _stopwatch = new();
    private string? _lastResolvedAutomationId;
    private Avalonia.Labs.Controls.SKCanvasView? _spotlightCanvas;

    public FtueOverlay()
    {
        InitializeComponent();

        // 30fps Pulse-Animation. Timer laeuft nur wenn Overlay sichtbar ist.
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _renderTimer.Tick += (_, _) => _spotlightCanvas?.InvalidateSurface();

        DataContextChanged += (_, _) => ResolveSpotlightBoundsAsync();
        AttachedToVisualTree += (_, _) =>
        {
            _spotlightCanvas = this.FindControl<Avalonia.Labs.Controls.SKCanvasView>("SpotlightCanvas");
            ResolveSpotlightBoundsAsync();
        };
        LayoutUpdated += (_, _) => ResolveSpotlightBoundsAsync();

        // IsVisible-Toggle steuert Timer-Lifecycle. PropertyChanged-Listener fuer IsVisible.
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty)
            {
                if (IsVisible)
                {
                    _stopwatch.Restart();
                    _renderTimer.Start();
                    ResolveSpotlightBoundsAsync();
                }
                else
                {
                    _renderTimer.Stop();
                }
            }
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Sucht im Visual-Tree nach dem Element mit AutomationId == VM.SpotlightAutomationId
    /// und gibt seine Center-Position + Radius an das VM weiter.
    /// </summary>
    private void ResolveSpotlightBoundsAsync()
    {
        if (DataContext is not FtueOverlayViewModel vm) return;
        if (string.IsNullOrEmpty(vm.SpotlightAutomationId)) return;
        if (_lastResolvedAutomationId == vm.SpotlightAutomationId) return; // Idempotent

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (TopLevel.GetTopLevel(this) is not Control root) return;
                var target = FindByAutomationId(root, vm.SpotlightAutomationId!);
                if (target == null) return;

                var bounds = target.Bounds;
                var topLeftInRoot = target.TranslatePoint(new Point(0, 0), this);
                if (topLeftInRoot == null) return;

                var centerX = topLeftInRoot.Value.X + bounds.Width / 2;
                var centerY = topLeftInRoot.Value.Y + bounds.Height / 2;
                var radius = (float)Math.Max(bounds.Width, bounds.Height) / 2f + 16f;
                vm.SetSpotlightBounds((float)centerX, (float)centerY, radius);
                _lastResolvedAutomationId = vm.SpotlightAutomationId;
            }
            catch
            {
                // Visual-Tree-Walk darf niemals crashen.
            }
        }, DispatcherPriority.Background);
    }

    private static Control? FindByAutomationId(Control parent, string id)
    {
        if (AutomationProperties.GetAutomationId(parent) == id) return parent;
        foreach (var child in parent.GetVisualChildren())
        {
            if (child is Control c)
            {
                var found = FindByAutomationId(c, id);
                if (found != null) return found;
            }
        }
        return null;
    }

    private void OnSpotlightPaint(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        if (DataContext is not FtueOverlayViewModel vm) return;

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var dpi = (float)(e.Info.Width / Math.Max(1, Bounds.Width));
        var elapsedSeconds = (float)_stopwatch.Elapsed.TotalSeconds;

        FtueSpotlightRenderer.Render(
            canvas,
            e.Info.Width, e.Info.Height,
            dpi,
            vm.SpotlightX, vm.SpotlightY, vm.SpotlightRadius,
            elapsedSeconds);
    }
}
