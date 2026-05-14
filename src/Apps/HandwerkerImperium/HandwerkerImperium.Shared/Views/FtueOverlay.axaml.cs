using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels;
using SkiaSharp;

namespace HandwerkerImperium.Views;

/// <summary>
/// AAA-Audit P0 FTUE-UI: Spotlight-Overlay-View. Code-Behind misst die Bounds des
/// Spotlight-Targets (per AutomationId), gibt sie ans VM weiter und zeichnet das
/// SkiaSharp-Spotlight per <see cref="FtueSpotlightRenderer"/>.
///
/// AAA-Audit P1 (12.05.2026): Render-Tick auf zentralen <see cref="IFrameClock"/>
/// migriert — kein eigener DispatcherTimer mehr.
/// </summary>
public partial class FtueOverlay : UserControl
{
    private readonly Stopwatch _stopwatch = new();
    private readonly Helpers.FrameClockRenderLoop _renderLoop;
    private string? _lastResolvedAutomationId;
    private Avalonia.Labs.Controls.SKCanvasView? _spotlightCanvas;
    private static readonly TimeSpan PulseInterval = TimeSpan.FromMilliseconds(33); // ~30fps

    public FtueOverlay()
    {
        InitializeComponent();
        _renderLoop = new Helpers.FrameClockRenderLoop(() => _spotlightCanvas?.InvalidateSurface(), PulseInterval);

        DataContextChanged += (_, _) => ResolveSpotlightBoundsAsync();
        AttachedToVisualTree += (_, _) =>
        {
            _spotlightCanvas = this.FindControl<Avalonia.Labs.Controls.SKCanvasView>("SpotlightCanvas");
            ResolveSpotlightBoundsAsync();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _renderLoop.Stop();
            _stopwatch.Stop();
        };
        LayoutUpdated += (_, _) => ResolveSpotlightBoundsAsync();

        // IsVisible-Toggle steuert Render-Loop-Lifecycle.
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty)
            {
                if (IsVisible)
                {
                    _stopwatch.Restart();
                    _renderLoop.Start();
                    ResolveSpotlightBoundsAsync();
                }
                else
                {
                    _renderLoop.Stop();
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

        // v2.1.1: LocalClipBounds statt e.Info.Width/Height — bei DPI > 1 liefert e.Info physische
        // Pixel, die groesser als der sichtbare Bereich sind. Bekanntes Rechts-Clipping-Pattern
        // aus Haupt-CLAUDE.md (Troubleshooting-Tabelle).
        var clipBounds = canvas.LocalClipBounds;
        var dpi = (float)(e.Info.Width / Math.Max(1, Bounds.Width));
        var elapsedSeconds = (float)_stopwatch.Elapsed.TotalSeconds;

        FtueSpotlightRenderer.Render(
            canvas,
            clipBounds.Width, clipBounds.Height,
            dpi,
            vm.SpotlightX, vm.SpotlightY, vm.SpotlightRadius,
            elapsedSeconds);
    }
}
