using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using SmartMeasure.Shared.ViewModels;

namespace SmartMeasure.Shared.Views;

public partial class GardenPlanView : UserControl
{
    private GardenPlanViewModel? _vm;
    private Avalonia.Point _lastPointer;
    private bool _isPanning;

    public GardenPlanView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (_vm != null)
                _vm.InvalidateRequested -= OnInvalidateRequested;

            _vm = DataContext as GardenPlanViewModel;
            if (_vm != null)
            {
                _vm.InvalidateRequested += OnInvalidateRequested;
                _vm.UpdateCoordinates();
            }
        };

        PlanCanvas.PointerPressed += OnPointerPressed;
        PlanCanvas.PointerMoved += OnPointerMoved;
        PlanCanvas.PointerReleased += OnPointerReleased;
        PlanCanvas.PointerWheelChanged += OnPointerWheel;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        _vm?.Renderer.Render(canvas, bounds, _vm.X, _vm.Y, _vm.Z, _vm.Labels,
            _vm.Elements.ToList(), null);
    }

    private void OnInvalidateRequested() => PlanCanvas.InvalidateSurface();

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isPanning = true;
        _lastPointer = e.GetPosition(PlanCanvas);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning || _vm == null) return;
        var current = e.GetPosition(PlanCanvas);
        _vm.HandlePan((float)(current.X - _lastPointer.X), (float)(current.Y - _lastPointer.Y));
        _lastPointer = current;
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => _isPanning = false;

    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        _vm?.HandleZoom(e.Delta.Y > 0 ? 1.1f : 0.9f);
        e.Handled = true;
    }
}
