using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using SmartMeasure.Shared.ViewModels;

namespace SmartMeasure.Shared.Views;

public partial class GardenPlanView : UserControl
{
    private GardenPlanViewModel? _vm;
    private Avalonia.Point _lastPointer;
    private Avalonia.Point _pressPoint;
    private bool _isPanning;
    private bool _hasMoved;

    /// <summary>Maximale Entfernung in Pixel, die als Tap gilt (kein Pan)</summary>
    private const double TapThreshold = 10.0;

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
        if (_vm == null) return;
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;

        // Zeichnungs-Vorschau uebergeben (aktuelle Punkte + Werkzeug-Typ)
        var previewPoints = _vm.IsDrawing ? _vm.CurrentDrawingPoints : null;
        _vm.Renderer.Render(canvas, bounds, _vm.X, _vm.Y, _vm.Z, _vm.Labels,
            _vm.Elements.ToList(), null, previewPoints, _vm.SelectedTool);
    }

    private void OnInvalidateRequested() => PlanCanvas.InvalidateSurface();

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressPoint = e.GetPosition(PlanCanvas);
        _lastPointer = _pressPoint;
        _isPanning = true;
        _hasMoved = false;
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning || _vm == null) return;
        var current = e.GetPosition(PlanCanvas);

        var dx = current.X - _pressPoint.X;
        var dy = current.Y - _pressPoint.Y;
        var distFromPress = Math.Sqrt(dx * dx + dy * dy);

        if (distFromPress > TapThreshold)
            _hasMoved = true;

        if (_hasMoved)
        {
            _vm.HandlePan((float)(current.X - _lastPointer.X), (float)(current.Y - _lastPointer.Y));
            _lastPointer = current;
        }

        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_hasMoved && _vm != null)
        {
            // Tap erkannt: Punkt zum Zeichnungselement hinzufuegen
            // Canvas-Position relativ zur Viewport-Mitte berechnen
            // Der Renderer verschiebt um (MidX + PanX, MidY + PanY)
            var pos = e.GetPosition(PlanCanvas);
            var canvasBounds = PlanCanvas.Bounds;
            var relX = (float)(pos.X - canvasBounds.Width / 2 - _vm.Renderer.PanX);
            var relY = (float)(pos.Y - canvasBounds.Height / 2 - _vm.Renderer.PanY);
            _vm.OnCanvasTapped(relX, relY);
        }

        _isPanning = false;
        _hasMoved = false;
    }

    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        _vm?.HandleZoom(e.Delta.Y > 0 ? 1.1f : 0.9f);
        e.Handled = true;
    }
}
