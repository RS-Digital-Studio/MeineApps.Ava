using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using BomberBlast.ViewModels;
using SkiaSharp;
using Avalonia.Labs.Controls;

namespace BomberBlast.Views;

public partial class GameView : UserControl
{
    private int _renderWidth, _renderHeight;
    private float _touchScaleX = 1f, _touchScaleY = 1f; // Gecacht für Touch-Koordinaten
    private GameViewModel? _subscribedVm;
    private DispatcherTimer? _renderTimer;

    public GameView()
    {
        InitializeComponent();

        GameCanvas.PaintSurface += OnPaintSurface;
        GameCanvas.PointerPressed += OnPointerPressed;
        GameCanvas.PointerMoved += OnPointerMoved;
        GameCanvas.PointerReleased += OnPointerReleased;

        // Keyboard input for desktop
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;

        // InvalidateCanvasRequested bei DataContext-Wechsel abonnieren
        DataContextChanged += OnDataContextChanged;

        // Cleanup bei Entfernung aus Visual Tree (verhindert DispatcherTimer-Speicherleck)
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        StopRenderTimer();

        // ViewModel-Events abmelden
        if (_subscribedVm != null)
        {
            _subscribedVm.InvalidateCanvasRequested -= OnInvalidateRequested;
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedVm = null;
        }
    }

    private GameViewModel? ViewModel => DataContext as GameViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes ViewModel abmelden
        if (_subscribedVm != null)
        {
            _subscribedVm.InvalidateCanvasRequested -= OnInvalidateRequested;
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedVm = null;
        }

        // Neues ViewModel abonnieren
        if (DataContext is GameViewModel vm)
        {
            _subscribedVm = vm;
            vm.InvalidateCanvasRequested += OnInvalidateRequested;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnInvalidateRequested()
    {
        // Initialen Frame rendern + Render-Timer starten
        GameCanvas.InvalidateSurface();
        StartRenderTimer();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // OVERLAY HIT-TEST STEUERUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Deaktiviert Hit-Testing auf dem GameCanvas wenn Pause- oder Score-Double-Overlay sichtbar ist,
    /// damit Overlay-Buttons auf Android Touch-Events empfangen können.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameViewModel.IsPaused) or nameof(GameViewModel.ShowScoreDoubleOverlay))
        {
            UpdateCanvasHitTest();
        }
    }

    private void UpdateCanvasHitTest()
    {
        GameCanvas.IsHitTestVisible = ViewModel is { IsPaused: false, ShowScoreDoubleOverlay: false };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RENDER-TIMER (~60fps, selbes Pattern wie CelebrationOverlay)
    // ═══════════════════════════════════════════════════════════════════════

    private void StartRenderTimer()
    {
        if (_renderTimer != null) return;

        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _renderTimer.Tick += OnRenderTick;
        _renderTimer.Start();
    }

    private void StopRenderTimer()
    {
        if (_renderTimer == null) return;
        _renderTimer.Stop();
        _renderTimer.Tick -= OnRenderTick;
        _renderTimer = null;
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        if (ViewModel?.IsGameLoopRunning == true)
        {
            GameCanvas.InvalidateSurface();
        }
        else
        {
            // Game-Loop gestoppt → Timer stoppen
            StopRenderTimer();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RENDERING
    // ═══════════════════════════════════════════════════════════════════════

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        // canvas.LocalClipBounds statt e.Info fuer korrekte DPI-Dimensionen
        var bounds = canvas.LocalClipBounds;
        int width = (int)bounds.Width;
        int height = (int)bounds.Height;

        if (width <= 0 || height <= 0) return;

        // Fuer Touch-Koordinaten-Konvertierung speichern + Scale-Faktoren cachen
        _renderWidth = width;
        _renderHeight = height;
        var bw = GameCanvas.Bounds.Width;
        var bh = GameCanvas.Bounds.Height;
        _touchScaleX = bw > 0 ? width / (float)bw : 1f;
        _touchScaleY = bh > 0 ? height / (float)bh : 1f;

        ViewModel?.OnPaintSurface(canvas, width, height);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INPUT
    // ═══════════════════════════════════════════════════════════════════════

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is null) return;

        var point = e.GetPosition(GameCanvas);
        float x = (float)(point.X * _touchScaleX);
        float y = (float)(point.Y * _touchScaleY);
        long pointerId = e.Pointer.Id;

        ViewModel.OnPointerPressed(x, y, _renderWidth, _renderHeight, pointerId);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (ViewModel is null) return;

        var point = e.GetPosition(GameCanvas);
        float x = (float)(point.X * _touchScaleX);
        float y = (float)(point.Y * _touchScaleY);
        long pointerId = e.Pointer.Id;

        ViewModel.OnPointerMoved(x, y, pointerId);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (ViewModel is null) return;
        long pointerId = e.Pointer.Id;

        ViewModel.OnPointerReleased(pointerId);
        e.Handled = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.OnKeyDown(e.Key);
        e.Handled = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.OnKeyUp(e.Key);
        e.Handled = true;
    }
}
