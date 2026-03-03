using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.ViewModels;
using SkiaSharp;

namespace HandwerkerImperium.Views;

public partial class LuckySpinView : UserControl
{
    private readonly LuckySpinWheelRenderer _wheelRenderer = new();
    private SKCanvasView? _canvasView;
    private LuckySpinViewModel? _subscribedVm;
    private bool _disposed;

    public LuckySpinView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_disposed) return;
        _disposed = true;

        // Event-Subscription abmelden
        if (_subscribedVm != null)
        {
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedVm = null;
        }

        _wheelRenderer.Dispose();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;

        // Altes VM abmelden
        if (_subscribedVm != null)
        {
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedVm = null;
        }

        // Bei neuem ViewModel PropertyChanged subscriben
        if (DataContext is LuckySpinViewModel vm)
        {
            _subscribedVm = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    /// <summary>
    /// Bei SpinAngle-Änderung Canvas invalidieren → Rad dreht sich visuell.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LuckySpinViewModel.SpinAngle)
                           or nameof(LuckySpinViewModel.ShowPrize))
        {
            _canvasView?.InvalidateSurface();
        }
    }

    /// <summary>
    /// Zeichnet das Glücksrad via LuckySpinWheelRenderer.
    /// </summary>
    private void OnPaintWheel(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_disposed) return;

        // Canvas-Referenz speichern für spätere Invalidierung
        if (_canvasView == null && sender is SKCanvasView cv)
            _canvasView = cv;

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not LuckySpinViewModel vm) return;

        // Hervorgehobenes Segment nur wenn Spin fertig UND Gewinn angezeigt
        int? highlightedSegment = null;
        if (vm.ShowPrize && vm.LastPrizeType != null)
        {
            highlightedSegment = (int)vm.LastPrizeType.Value;
        }

        _wheelRenderer.Render(canvas, bounds, vm.SpinAngle, highlightedSegment);
    }
}
