using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using RebornSaga.Services;
using RebornSaga.ViewModels;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RebornSaga.Views;

public partial class MainView : UserControl
{
    /// <summary>
    /// Ziel-Frame-Intervall des Game-Loops: ~30fps (33ms) statt 60fps (16ms).
    /// Halbiert die Render-Last verlustfrei, da das Spiel deltaTime-basiert ist
    /// (siehe StartGameLoop: deltaTime kommt aus der echten verstrichenen Stopwatch-Zeit,
    /// nicht hartkodiert) — Spielgeschwindigkeit und Animations-Timing bleiben identisch,
    /// nur die Bildwiederholrate sinkt. Spart spürbar CPU/GPU und Akku auf Android-Mid-Tier.
    /// </summary>
    private const double FrameIntervalMs = 33.0;

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

        // App-Lifecycle-Signale des VM abonnieren: Game-Loop-Timer im Hintergrund stoppen (Akku).
        // (DataContext wird vor dem Attach gesetzt → _vm ist hier bereits vorhanden.)
        if (_vm != null)
        {
            _vm.GameLoopPauseRequested += OnGameLoopPauseRequested;
            _vm.GameLoopResumeRequested += OnGameLoopResumeRequested;
        }

        // Display-Pixelhöhe ermitteln und an die Sprite-Decode-Pipeline koppeln (Akku/RAM):
        // Sprites werden beim Dekodieren auf die tatsächlich benötigte Auflösung verkleinert.
        ConfigureSpriteTargetHeight();

        // Services initialisieren (Skills, Items, Purchases laden)
        _ = InitializeServicesAsync();

        // Game-Loop starten (~33ms = ~30fps, FrameIntervalMs)
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
        {
            _vm.GameLoopPauseRequested -= OnGameLoopPauseRequested;
            _vm.GameLoopResumeRequested -= OnGameLoopResumeRequested;
            _vm = null;
        }

        DataContextChanged -= OnDataContextChanged;

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// App ging in den Hintergrund: 60fps-Timer stoppen, damit er nicht weiter aufwacht (Akku).
    /// Verlustfrei — das Spiel ist deltaTime-basiert, der Stopwatch-Stand wird beim Resume genutzt.
    /// </summary>
    private void OnGameLoopPauseRequested() => _gameLoopTimer?.Stop();

    /// <summary>
    /// App kam in den Vordergrund: Game-Loop neu starten (StartGameLoop setzt Delta-Basis zurück,
    /// kein Zeit-Sprung beim ersten Frame). Nur erreichbar solange attached — OnDetachedFromVisualTree
    /// meldet die VM-Events vorher ab.
    /// </summary>
    private void OnGameLoopResumeRequested() => StartGameLoop();

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
    /// Ermittelt die physische Display-Pixelhöhe (Portrait) und koppelt sie an die Sprite-Decode-
    /// Pipeline (SpriteCache.SetTargetDisplayHeight): Sprites werden nie höher dekodiert als sie
    /// je dargestellt werden können → spart RAM und Decode-Zeit ohne sichtbaren Qualitätsverlust.
    /// Die physische Höhe = logische Screen-Höhe × RenderScaling. Für Portrait wird die längere
    /// Kante als Höhe genommen (das Spiel läuft Hochformat). Bei Unsicherheit greift im SpriteCache
    /// der konservative Default (volle Auflösung), daher ist jeder Fehlerpfad hier unkritisch.
    /// </summary>
    private void ConfigureSpriteTargetHeight()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var scaling = topLevel.RenderScaling > 0 ? topLevel.RenderScaling : 1.0;

            // Bevorzugt die echte Bildschirmgröße (physische Pixel), Fallback auf die
            // logische Client-Größe × Scaling.
            var screenSize = topLevel.Screens?.ScreenFromVisual(this)?.Bounds;
            double physicalHeight;
            if (screenSize is { } sb && sb.Height > 0 && sb.Width > 0)
            {
                // Screen.Bounds ist bereits in physischen Pixeln. Portrait → längere Kante = Höhe.
                physicalHeight = Math.Max(sb.Width, sb.Height);
            }
            else
            {
                var logical = Math.Max(topLevel.ClientSize.Width, topLevel.ClientSize.Height);
                physicalHeight = logical * scaling;
            }

            if (physicalHeight >= 1)
                SpriteCache.SetTargetDisplayHeight((int)Math.Round(physicalHeight));
        }
        catch
        {
            // Display-Abfrage fehlgeschlagen → konservativer Default im SpriteCache greift.
        }
    }

    /// <summary>
    /// Game-Loop: Update jeden Tick (~33ms = ~30fps, FrameIntervalMs), Paint nur bei Bedarf.
    /// Die Logik (Update) läuft immer weiter; das teure InvalidateSurface wird übersprungen,
    /// wenn die aktive Szene statisch ist und keine Zustandsänderung anliegt (Bedarfs-Rendering).
    /// </summary>
    private void StartGameLoop()
    {
        // Falls bereits laufend, nur Timer stoppen (NICHT StopGameLoop - das nullt Referenzen)
        _gameLoopTimer?.Stop();

        _stopwatch.Restart();
        _lastFrameTicks = 0;

        _gameLoopTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(FrameIntervalMs)
        };
        _gameLoopTimer.Tick += (_, _) =>
        {
            // App im Hintergrund / View unsichtbar → teures Update + Render ueberspringen
            // (Akku/CPU; die Activity bleibt bei App-Switch oft im Visual-Tree, der Timer tickt sonst weiter).
            if (!IsEffectivelyVisible)
            {
                _lastFrameTicks = _stopwatch.ElapsedTicks; // kein Delta-Sprung beim Zurueckkehren
                return;
            }

            var currentTicks = _stopwatch.ElapsedTicks;
            var deltaTime = (float)(currentTicks - _lastFrameTicks) / Stopwatch.Frequency;
            _lastFrameTicks = currentTicks;

            // Delta-Time auf 50ms begrenzen (verhindert Sprünge bei Alt-Tab etc.)
            if (deltaTime > 0.05f)
                deltaTime = 0.05f;

            // Logik immer aktualisieren (Timer, Cooldowns, Animations-Zustände laufen weiter).
            _vm?.Update(deltaTime);

            // Paint nur anstoßen, wenn tatsächlich etwas Neues zu zeichnen ist (Akku).
            if (_vm == null || _vm.ShouldRender())
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
