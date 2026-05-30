using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

        // Re-Subscribe bei Wiedereintritt (Singleton-VM: DataContextChanged feuert nicht erneut)
        AttachedToVisualTree += OnAttachedToVisualTree;

        // Loaded als zusätzliche Subscription-Chance (ViewLocator kann DataContext verzögert setzen)
        Loaded += OnLoaded;
    }


    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        StopRenderTimer();

        // ViewModel-Events abmelden
        if (_subscribedVm != null)
        {
            _subscribedVm.InvalidateCanvasRequested -= OnInvalidateRequested;
            _subscribedVm = null;
        }
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        // Bei Singleton-VM feuert DataContextChanged nicht erneut beim Wieder-Sichtbar-Werden.
        // Deshalb hier manuell re-subscriben wenn _subscribedVm null ist (durch Detach geloescht).
        TrySubscribeToViewModel();
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // ViewLocator/ContentPresenter kann DataContext NACH AttachedToVisualTree setzen.
        // Loaded feuert zuverlässig nachdem DataContext + Visual Tree stehen.
        TrySubscribeToViewModel();
    }

    /// <summary>
    /// Zentrale Methode: VM-Events abonnieren falls noch nicht geschehen.
    /// Wird aus OnDataContextChanged, OnAttachedToVisualTree UND OnLoaded aufgerufen.
    /// </summary>
    private void TrySubscribeToViewModel()
    {
        if (_subscribedVm != null) return; // Bereits subscribed
        if (DataContext is not GameViewModel vm) return;

        _subscribedVm = vm;
        vm.InvalidateCanvasRequested += OnInvalidateRequested;

        // Falls Game-Loop bereits laeuft, sofort rendern + Timer starten
        if (vm.IsGameLoopRunning)
        {
            GameCanvas.InvalidateSurface();
            StartRenderTimer();
        }
    }

    private GameViewModel? ViewModel => DataContext as GameViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes ViewModel abmelden
        if (_subscribedVm != null)
        {
            _subscribedVm.InvalidateCanvasRequested -= OnInvalidateRequested;
            _subscribedVm = null;
        }

        // Neues ViewModel abonnieren (via zentrale Methode)
        TrySubscribeToViewModel();
    }

    private void OnInvalidateRequested()
    {
        // Initialen Frame rendern + Render-Timer starten
        GameCanvas.InvalidateSurface();
        StartRenderTimer();
    }

    // HINWEIS: Die Hit-Test-Steuerung des GameCanvas liegt ausschliesslich im XAML-Binding
    // IsHitTestVisible="{Binding !IsAnyOverlayOpen}" (GameView.axaml). IsAnyOverlayOpen deckt
    // Pause + ScoreDouble + ContextHelp + Loading vollstaendig ab. Eine fruehere Code-Behind-
    // Variante setzte die Property zusaetzlich per LocalValue-Setter — das verdraengt das Binding
    // dauerhaft (Avalonia-Value-Precedence) und liess ContextHelp/Loading aussen vor (Taps gingen
    // unter dem Overlay durch). Daher bewusst KEIN Code-Behind-Hit-Test mehr.

    // ═══════════════════════════════════════════════════════════════════════
    // RENDER-TIMER (~60fps, selbes Pattern wie CelebrationOverlay)
    // ═══════════════════════════════════════════════════════════════════════

    private void StartRenderTimer()
    {
        if (_renderTimer != null) return;

        // Tick-Intervall aus GameLoopSettings (User-konfigurierbar in Settings: 30/60 FPS).
        // Default 30 FPS halbiert CPU/GPU-Last auf Android (Akku, Geräte-Erwärmung).
        // GameEngine.Update nutzt deltaTime, reagiert also automatisch auf die neue Rate.
        _renderTimer = new DispatcherTimer { Interval = BomberBlast.Core.GameLoopSettings.TickInterval };
        _renderTimer.Tick += OnRenderTick;
        _renderTimer.Start();

        // Reagiert auf Frame-Rate-Toggle in Settings — Timer wird neu mit aktuellem Intervall gestartet.
        BomberBlast.Core.GameLoopSettings.TargetFpsChanged += OnTargetFpsChanged;
    }

    private void OnTargetFpsChanged(object? sender, int newFps)
    {
        if (_renderTimer != null)
        {
            _renderTimer.Stop();
            _renderTimer.Interval = BomberBlast.Core.GameLoopSettings.TickInterval;
            _renderTimer.Start();
        }
    }

    private void StopRenderTimer()
    {
        // Static-Event-Subscription aufheben — sonst hält das Event den GameView am Leben (Memory Leak).
        BomberBlast.Core.GameLoopSettings.TargetFpsChanged -= OnTargetFpsChanged;
        if (_renderTimer == null) return;
        _renderTimer.Stop();
        _renderTimer.Tick -= OnRenderTick;
        _renderTimer = null;
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        if (ViewModel?.IsGameLoopRunning != true)
        {
            // Game-Loop gestoppt → Timer stoppen
            StopRenderTimer();
            return;
        }

        // Skippen wenn Parent-Container (PageView-Border) unsichtbar ist.
        // Beim Tab-Wechsel zum Menü bleibt GameView im Visual Tree aber der Border
        // wird auf IsVisible=false gesetzt. Ohne diesen Check würde die GameEngine
        // weiter bei 60fps rendern + Update laufen → Android-Gerät erwärmt sich unnötig.
        if (!IsEffectivelyVisible)
            return;

        GameCanvas.InvalidateSurface();
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

        // Selbstheilend: Falls Game-Loop laeuft aber kein Render-Timer aktiv ist
        // (z.B. weil InvalidateCanvasRequested keinen Subscriber hatte beim StartGameLoop),
        // Timer hier nachholen damit Update(deltaTime) auf folgenden Frames aufgerufen wird.
        if (ViewModel?.IsGameLoopRunning == true && _renderTimer == null)
        {
            StartRenderTimer();
        }
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
