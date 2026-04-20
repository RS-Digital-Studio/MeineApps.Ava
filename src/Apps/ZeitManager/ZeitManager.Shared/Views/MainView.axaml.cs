using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SkiaSharp;
using ZeitManager.Graphics;
using ZeitManager.ViewModels;

namespace ZeitManager.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;
    private int _onboardingStep;

    // SkiaSharp Hintergrund-Renderer
    private readonly ClockworkBackgroundRenderer _backgroundRenderer = new();
    private DispatcherTimer? _renderTimer;
    private float _renderTime;

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        StartRenderTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Render-Timer stoppen und Renderer freigeben
        if (_renderTimer != null)
        {
            _renderTimer.Stop();
            _renderTimer.Tick -= OnRenderTimerTick;
            _renderTimer = null;
        }
        _backgroundRenderer.Dispose();
    }

    // =====================================================================
    // Render-Timer (~5fps fuer animierten Hintergrund)
    // =====================================================================

    private void StartRenderTimer()
    {
        if (_renderTimer != null) return;

        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) }; // ~5fps
        _renderTimer.Tick += OnRenderTimerTick;
        _renderTimer.Start();
    }

    private void OnRenderTimerTick(object? sender, EventArgs e)
    {
        _renderTime += 0.2f;
        _backgroundRenderer.Update(0.2f);
        BackgroundCanvas?.InvalidateSurface();
    }

    // =====================================================================
    // SkiaSharp Paint-Handler
    // =====================================================================

    /// <summary>
    /// Zeichnet den animierten Clockwork-Hintergrund (5 Layer).
    /// </summary>
    private void OnBackgroundPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        _backgroundRenderer.Render(canvas, bounds, _renderTime);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
        }

        _vm = DataContext as MainViewModel;

        if (_vm != null)
        {
            _vm.FloatingTextRequested += OnFloatingText;
            _vm.CelebrationRequested += OnCelebration;
            TryStartOnboarding();
        }
    }

    private void OnFloatingText(string text, string category)
    {
        var color = category switch
        {
            "success" => Color.Parse("#22C55E"),
            "info" => Color.Parse("#3B82F6"),
            _ => Color.Parse("#3B82F6")
        };
        var w = FloatingTextCanvas.Bounds.Width;
        if (w < 10) w = 300;
        var h = FloatingTextCanvas.Bounds.Height;
        if (h < 10) h = 400;
        FloatingTextCanvas.ShowFloatingText(text, w * 0.3 + Random.Shared.NextDouble() * w * 0.4, h * 0.35, color, 18);
    }

    private void OnCelebration()
    {
        CelebrationCanvas.ShowConfetti();
    }

    /// <summary>
    /// Startet das Onboarding wenn es noch nicht abgeschlossen wurde.
    /// Texte und Completion-State kommen aus dem ViewModel.
    /// </summary>
    private async void TryStartOnboarding()
    {
        try
        {
            if (_vm == null || _vm.IsOnboardingCompleted)
                return;

            // VM-Snapshot vor Delay: Schuetzt gegen VM-Wechsel waehrend des 800ms-Warten.
            // Ohne Snapshot koennte der alte VM-Text im neuen VM-Kontext erscheinen oder
            // zwei parallele TryStartOnboarding-Coroutines Events doppelt subscriben.
            var vmSnapshot = _vm;
            await Task.Delay(800);
            if (_vm == null || !ReferenceEquals(_vm, vmSnapshot)) return;

            _onboardingStep = 0;
            // Idempotent subscriben: erst unsubscribe (falls doppelt gerufen), dann neu verdrahten
            OnboardingTooltip.Dismissed -= OnTooltipDismissed;
            OnboardingTooltip.Dismissed += OnTooltipDismissed;

            // Schritt 1: Quick-Timer Tipp (oben)
            OnboardingTooltip.Arrow = MeineApps.UI.Controls.ArrowPosition.Top;
            OnboardingTooltip.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            OnboardingTooltip.Margin = new Avalonia.Thickness(32, 80, 32, 0);
            OnboardingTooltip.Text = _vm.OnboardingQuickTimerText;
            OnboardingOverlay.IsVisible = true;
            OnboardingTooltip.Show();
        }
        catch
        {
            // Onboarding ist optional - bei Fehler einfach überspringen
        }
    }

    private void OnTooltipDismissed(object? sender, EventArgs e)
    {
        // Defensive Idempotenz: wenn Handler bereits abgearbeitet wurde (step >= 2)
        // ignorieren. Schuetzt gegen doppeltes Dismissed-Event nach einer Event-Subscribe-Race.
        if (_onboardingStep >= 2) return;

        _onboardingStep++;

        if (_onboardingStep == 1 && _vm != null)
        {
            // Schritt 2: Custom-Timer Tipp (unten)
            OnboardingTooltip.Arrow = MeineApps.UI.Controls.ArrowPosition.Bottom;
            OnboardingTooltip.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
            OnboardingTooltip.Margin = new Avalonia.Thickness(32, 0, 32, 100);
            OnboardingTooltip.Text = _vm.OnboardingCreateTimerText;

            // Kurz warten, dann zeigen
            DispatcherTimer.RunOnce(() => OnboardingTooltip.Show(), TimeSpan.FromMilliseconds(300));
        }
        else
        {
            // Onboarding abgeschlossen
            OnboardingOverlay.IsVisible = false;
            OnboardingTooltip.Dismissed -= OnTooltipDismissed;
            _vm?.MarkOnboardingCompleted();
        }
    }
}
