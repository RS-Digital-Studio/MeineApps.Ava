using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using RebornSaga.ViewModels;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RebornSaga.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;
    private DispatcherTimer? _gameLoopTimer;
    private readonly Stopwatch _stopwatch = new();
    private long _lastFrameTicks;

    // Gespeicherte Bounds für DPI-skalierte Touch-Koordinaten
    private SKRect _lastBounds;
    private double _controlWidth, _controlHeight;

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Focusable = true damit KeyDown-Events ankommen (Desktop)
        Focusable = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // SKCanvasView-Events verdrahten
        GameCanvas.PaintSurface += OnPaintSurface;

        // Touch/Maus-Events
        GameCanvas.PointerPressed += OnPointerPressed;
        GameCanvas.PointerMoved += OnPointerMoved;
        GameCanvas.PointerReleased += OnPointerReleased;

        // KeyDown symmetrisch anmelden (wird in OnDetachedFromVisualTree abgemeldet)
        KeyDown += OnKeyDown;

        // Services initialisieren (Skills, Items, Purchases laden)
        _ = InitializeServicesAsync();

        // Game-Loop starten (16ms = ~60fps)
        StartGameLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Game-Loop stoppen
        StopGameLoop();

        // Events abmelden (Memory-Leak verhindern)
        GameCanvas.PaintSurface -= OnPaintSurface;
        GameCanvas.PointerPressed -= OnPointerPressed;
        GameCanvas.PointerMoved -= OnPointerMoved;
        GameCanvas.PointerReleased -= OnPointerReleased;
        KeyDown -= OnKeyDown;

        if (_vm != null)
            _vm = null;

        DataContextChanged -= OnDataContextChanged;

        base.OnDetachedFromVisualTree(e);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _vm = DataContext as MainViewModel;
    }

    /// <summary>
    /// Initialisiert Services asynchron nach dem Attach.
    /// Fire-and-forget: Fehler werden verschluckt da TitleScene keine geladenen Daten braucht.
    /// </summary>
    private async Task InitializeServicesAsync()
    {
        try
        {
            if (_vm != null)
                await _vm.InitializeAsync();
        }
        catch
        {
            // Service-Initialisierung fehlgeschlagen - SaveGame-Load wird Fehler werfen,
            // TitleScene funktioniert aber weiterhin
        }
    }

    /// <summary>
    /// Game-Loop: Update + InvalidateSurface alle 16ms (~60fps).
    /// </summary>
    private void StartGameLoop()
    {
        // Falls bereits laufend, nur Timer stoppen (NICHT StopGameLoop - das nullt Referenzen)
        _gameLoopTimer?.Stop();

        _stopwatch.Restart();
        _lastFrameTicks = 0;

        _gameLoopTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _gameLoopTimer.Tick += (_, _) =>
        {
            var currentTicks = _stopwatch.ElapsedTicks;
            var deltaTime = (float)(currentTicks - _lastFrameTicks) / Stopwatch.Frequency;
            _lastFrameTicks = currentTicks;

            // Delta-Time auf 50ms begrenzen (verhindert Sprünge bei Alt-Tab etc.)
            if (deltaTime > 0.05f)
                deltaTime = 0.05f;

            _vm?.Update(deltaTime);
            GameCanvas.InvalidateSurface();
        };
        _gameLoopTimer.Start();
    }

    private void StopGameLoop()
    {
        _gameLoopTimer?.Stop();
        _gameLoopTimer = null;
        _stopwatch.Stop();
    }

    /// <summary>
    /// SkiaSharp PaintSurface - verwendet canvas.LocalClipBounds fuer korrekte Bounds bei DPI-Skalierung.
    /// NICHT e.Info.Width/Height verwenden (physische Pixel, nicht sichtbarer Bereich)!
    /// </summary>
    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;

        // Bounds und Control-Größe für DPI-skalierte Touch-Koordinaten speichern
        _lastBounds = bounds;
        _controlWidth = GameCanvas.Bounds.Width;
        _controlHeight = GameCanvas.Bounds.Height;

        _vm?.Render(canvas, bounds);
    }

    /// <summary>
    /// Berechnet die SkiaSharp-Koordinaten aus dem Pointer-Event.
    /// Skaliert proportional von Control-Bounds auf Render-Bounds.
    /// </summary>
    private SKPoint GetSkiaPoint(PointerEventArgs e)
    {
        var pos = e.GetPosition(GameCanvas);

        if (_controlWidth < 1 || _controlHeight < 1)
            return new SKPoint((float)pos.X, (float)pos.Y);

        // Proportionale Skalierung von Avalonia-Koordinaten auf SkiaSharp-Koordinaten
        // (Control-Bounds können von Canvas-Bounds abweichen bei DPI-Skalierung)
        var scaleX = _lastBounds.Width / (float)_controlWidth;
        var scaleY = _lastBounds.Height / (float)_controlHeight;
        return new SKPoint((float)pos.X * scaleX, (float)pos.Y * scaleY);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _vm?.HandlePointerPressed(GetSkiaPoint(e));
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        _vm?.HandlePointerMoved(GetSkiaPoint(e));
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _vm?.HandlePointerReleased(GetSkiaPoint(e));
    }

    /// <summary>
    /// Keyboard-Events an InputManager weiterleiten (Desktop).
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _vm?.HandleKeyDown(e.Key);
    }
}
