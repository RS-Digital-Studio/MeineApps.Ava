using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using SkiaSharp;
using SmartMeasure.Shared.ViewModels;

namespace SmartMeasure.Shared.Views;

/// <summary>
/// 3D-Terrain-Viewport mit Touch/Mouse-Interaktion:
/// - Linksklick / Einzel-Finger: Rotation (Azimut + Elevation)
/// - Rechtsklick / Middle-Click / Zwei-Finger: Pan
/// - Mausrad / Pinch: Zoom
/// </summary>
public partial class TerrainView : UserControl
{
    private TerrainViewModel? _vm;
    private Avalonia.Point _lastPointer;
    private bool _isDragging;
    private bool _isPanning;
    private Action? _invalidateHandler;

    public TerrainView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        TerrainCanvas.PointerPressed += OnPointerPressed;
        TerrainCanvas.PointerMoved += OnPointerMoved;
        TerrainCanvas.PointerReleased += OnPointerReleased;
        TerrainCanvas.PointerWheelChanged += OnPointerWheel;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_vm != null && _invalidateHandler != null)
            _vm.InvalidateRequested -= _invalidateHandler;

        _vm = DataContext as TerrainViewModel;
        _invalidateHandler = null;

        if (_vm != null)
        {
            _invalidateHandler = OnInvalidateRequested;
            _vm.InvalidateRequested += _invalidateHandler;
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        _vm?.Renderer.Render(canvas, bounds, _vm.Mesh, _vm.ContourLines, _vm.Labels);
    }

    private void OnInvalidateRequested() => TerrainCanvas.InvalidateSurface();

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(TerrainCanvas).Properties;
        _lastPointer = e.GetPosition(TerrainCanvas);

        // Rechts-/Mitte-Klick = Pan, Links = Rotate (Standard)
        _isPanning = props.IsRightButtonPressed || props.IsMiddleButtonPressed;
        _isDragging = !_isPanning;

        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_vm == null || (!_isDragging && !_isPanning)) return;

        var current = e.GetPosition(TerrainCanvas);
        var dx = (float)(current.X - _lastPointer.X);
        var dy = (float)(current.Y - _lastPointer.Y);

        if (_isPanning)
            _vm.HandlePan(dx, dy);
        else
            _vm.HandleDrag(dx, dy);

        _lastPointer = current;
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        _isPanning = false;
    }

    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (_vm == null) return;
        var factor = e.Delta.Y > 0 ? 1.1f : 0.9f;
        _vm.HandleZoom(factor);
        e.Handled = true;
    }
}
