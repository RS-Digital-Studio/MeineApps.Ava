using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using MeineApps.UI.SkiaSharp.SplashScreen;

namespace MeineApps.UI.Controls;

/// <summary>
/// Immersiver Ladebildschirm mit SkiaSharp-Rendering.
/// Zeigt Gradient-Hintergrund, schwebende Glow-Partikel, App-Name,
/// animierten Fortschrittsbalken und Status-Text.
/// Progress + StatusText werden von aussen über die Loading-Pipeline gesteuert.
/// Fade-Out nach Abschluss: 200ms Pause → 300ms Opacity 1.0→0.0.
/// </summary>
public partial class SkiaLoadingSplash : UserControl
{
    // --- Styled Properties ---

    public static readonly StyledProperty<string> AppNameProperty =
        AvaloniaProperty.Register<SkiaLoadingSplash, string>(nameof(AppName), "App");

    public static readonly StyledProperty<string> AppVersionProperty =
        AvaloniaProperty.Register<SkiaLoadingSplash, string>(nameof(AppVersion), "");

    public static readonly StyledProperty<float> ProgressProperty =
        AvaloniaProperty.Register<SkiaLoadingSplash, float>(nameof(Progress), 0f);

    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<SkiaLoadingSplash, string>(nameof(StatusText), "");

    public string AppName
    {
        get => GetValue(AppNameProperty);
        set => SetValue(AppNameProperty, value);
    }

    public string AppVersion
    {
        get => GetValue(AppVersionProperty);
        set => SetValue(AppVersionProperty, value);
    }

    /// <summary>
    /// Fortschritt 0.0–1.0, steuert den Fortschrittsbalken.
    /// </summary>
    public float Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public string StatusText
    {
        get => GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    // --- Render-State ---
    private SplashScreenRenderer? _renderer;
    private DispatcherTimer? _renderTimer;
    private bool _isFadingOut;

    public SkiaLoadingSplash()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (_renderer == null) return;

        if (change.Property == AppNameProperty)
            _renderer.AppName = change.GetNewValue<string>();
        else if (change.Property == AppVersionProperty)
            _renderer.AppVersion = change.GetNewValue<string>();
        else if (change.Property == ProgressProperty)
            _renderer.Progress = change.GetNewValue<float>();
        else if (change.Property == StatusTextProperty)
            _renderer.StatusText = change.GetNewValue<string>();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Renderer erstellen und initialisieren
        _renderer = new SplashScreenRenderer
        {
            AppName = AppName,
            AppVersion = AppVersion,
            Progress = Progress,
            StatusText = StatusText
        };

        // SkiaSharp PaintSurface verdrahten
        SplashCanvas.PaintSurface += OnPaintSurface;

        // Render-Timer starten (~60fps)
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _renderTimer.Tick += OnRenderTick;
        _renderTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopAndDispose();
    }

    /// <summary>
    /// Startet den Fade-Out: 200ms Pause → 300ms Opacity auf 0 → IsVisible=false.
    /// </summary>
    public void FadeOut()
    {
        if (_isFadingOut) return;
        _isFadingOut = true;

        // 200ms Pause, dann Fade
        DispatcherTimer.RunOnce(() =>
        {
            Opacity = 0;

            // Nach Fade-Transition (300ms) verstecken + aufräumen
            DispatcherTimer.RunOnce(() =>
            {
                IsVisible = false;
                IsHitTestVisible = false;
                StopAndDispose();
            }, TimeSpan.FromMilliseconds(350));

        }, TimeSpan.FromMilliseconds(200));
    }

    private void StopAndDispose()
    {
        _renderTimer?.Stop();
        _renderTimer = null;

        if (SplashCanvas != null)
            SplashCanvas.PaintSurface -= OnPaintSurface;

        _renderer?.Dispose();
        _renderer = null;
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        _renderer?.Update(0.016f); // ~60fps Deltatime
        SplashCanvas?.InvalidateSurface();
    }

    private void OnPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        if (_renderer == null) return;

        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear();

        _renderer.Render(canvas, bounds);
    }
}
