using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MeineApps.UI.Controls;

/// <summary>
/// App-Startup Splash mit Icon, Ladebalken und Status-Text.
/// Unterstützt echtes Preloading über PreloadAction oder Timer-Fallback.
/// </summary>
public partial class SplashOverlay : UserControl
{
    private const double BarMaxWidth = 220;
    private bool _isDetached;

    public static readonly StyledProperty<string> AppNameProperty =
        AvaloniaProperty.Register<SplashOverlay, string>(nameof(AppName), "App");

    public static readonly StyledProperty<IImage?> IconSourceProperty =
        AvaloniaProperty.Register<SplashOverlay, IImage?>(nameof(IconSource));

    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<SplashOverlay, string>(nameof(StatusText), "");

    public string AppName
    {
        get => GetValue(AppNameProperty);
        set => SetValue(AppNameProperty, value);
    }

    public IImage? IconSource
    {
        get => GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    public string StatusText
    {
        get => GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    /// <summary>
    /// Async Preload-Aktion. Bekommt einen Callback für Progress (0.0-1.0) und Status-Text.
    /// Wenn null, wird Timer-Fallback (1.5s) verwendet.
    /// </summary>
    public Func<Action<float, string>, Task>? PreloadAction { get; set; }

    /// <summary>
    /// Wird gefeuert wenn Preloading + Fade-Out abgeschlossen sind.
    /// </summary>
    public event EventHandler? PreloadCompleted;

    public SplashOverlay()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == AppNameProperty)
            AppNameText.Text = change.GetNewValue<string>();
        else if (change.Property == IconSourceProperty)
            AppIconImage.Source = change.GetNewValue<IImage?>();
        else if (change.Property == StatusTextProperty)
            StatusTextBlock.Text = change.GetNewValue<string>();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isDetached = false;

        AppNameText.Text = AppName;
        AppIconImage.Source = IconSource;

        if (PreloadAction != null)
        {
            // Echtes Preloading mit Progress-Updates
            _ = RunPreloadAsync();
        }
        else
        {
            // Fallback: Timer-basiert (1.5s)
            DispatcherTimer.RunOnce(() => { if (!_isDetached) LoadingBar.Width = BarMaxWidth; },
                TimeSpan.FromMilliseconds(100));
            DispatcherTimer.RunOnce(() => { if (!_isDetached) Opacity = 0; },
                TimeSpan.FromMilliseconds(1500));
            DispatcherTimer.RunOnce(() => { if (!_isDetached) CompleteSplash(); },
                TimeSpan.FromMilliseconds(2000));
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isDetached = true;
        base.OnDetachedFromVisualTree(e);
    }

    private async Task RunPreloadAsync()
    {
        // Lokale Kopie um Race Condition zu vermeiden (KRIT-2)
        var preloadAction = PreloadAction;
        if (preloadAction == null) return;

        try
        {
            // Progress-Callback: Aktualisiert Ladebalken + Status-Text auf dem UI-Thread
            void OnProgress(float progress, string statusText)
            {
                if (_isDetached) return;
                Dispatcher.UIThread.Post(() =>
                {
                    if (_isDetached) return;
                    var clampedProgress = Math.Clamp(progress, 0f, 1f);
                    LoadingBar.Width = clampedProgress * BarMaxWidth;
                    StatusTextBlock.Text = statusText;
                });
            }

            // Preload-Task ausführen
            await preloadAction(OnProgress);

            if (_isDetached) return;

            // 100% setzen + kurz anzeigen
            Dispatcher.UIThread.Post(() =>
            {
                if (_isDetached) return;
                LoadingBar.Width = BarMaxWidth;
                StatusTextBlock.Text = "";
            });

            // Kurze Pause damit 100% sichtbar ist
            await Task.Delay(200);
            if (_isDetached) return;

            // Fade-Out
            Dispatcher.UIThread.Post(() => { if (!_isDetached) Opacity = 0; });
            await Task.Delay(450); // Etwas länger als die 400ms Opacity-Transition

            // Splash ausblenden + Delegate-Referenz freigeben
            if (!_isDetached)
                Dispatcher.UIThread.Post(CompleteSplash);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SplashOverlay] Preload-Fehler: {ex.Message}");
            if (!_isDetached)
                Dispatcher.UIThread.Post(CompleteSplash);
        }
    }

    private void CompleteSplash()
    {
        IsVisible = false;
        IsHitTestVisible = false;
        PreloadAction = null; // Delegate-Referenz freigeben (MITTEL-5)
        PreloadCompleted?.Invoke(this, EventArgs.Empty);
    }
}
