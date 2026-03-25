using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using SkiaSharp;
using SmartMeasure.Shared.ViewModels;

namespace SmartMeasure.Shared.Views;

public partial class TerrainView : UserControl
{
    private TerrainViewModel? _vm;
    private Avalonia.Point _lastPointer;
    private bool _isDragging;

    public TerrainView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (_vm != null)
                _vm.InvalidateRequested -= OnInvalidateRequested;

            _vm = DataContext as TerrainViewModel;
            if (_vm != null)
                _vm.InvalidateRequested += OnInvalidateRequested;
        };

        TerrainCanvas.PointerPressed += OnPointerPressed;
        TerrainCanvas.PointerMoved += OnPointerMoved;
        TerrainCanvas.PointerReleased += OnPointerReleased;
        TerrainCanvas.PointerWheelChanged += OnPointerWheel;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        _vm?.Renderer.Render(canvas, bounds, _vm.Mesh, _vm.ContourLines, _vm.Labels);
    }

    private void OnInvalidateRequested()
    {
        TerrainCanvas.InvalidateSurface();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDragging = true;
        _lastPointer = e.GetPosition(TerrainCanvas);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _vm == null) return;

        var current = e.GetPosition(TerrainCanvas);
        var dx = (float)(current.X - _lastPointer.X);
        var dy = (float)(current.Y - _lastPointer.Y);

        _vm.HandleDrag(dx, dy);
        _lastPointer = current;
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
    }

    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (_vm == null) return;
        var factor = e.Delta.Y > 0 ? 1.1f : 0.9f;
        _vm.HandleZoom(factor);
        e.Handled = true;
    }
}
