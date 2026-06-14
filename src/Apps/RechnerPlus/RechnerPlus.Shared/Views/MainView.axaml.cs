using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using RechnerPlus.Graphics;
using RechnerPlus.ViewModels;
using SkiaSharp;
using System;
using System.Threading.Tasks;

namespace RechnerPlus.Views;

public partial class MainView : UserControl
{
    private Point _swipeStart;
    private DateTime _swipeStartTime;
    private DateTime _lastHistoryToggle;
    private bool _isSwiping;
    private const double SwipeThreshold = 120;
    private const int MinSwipeMs = 200;
    private const int HistoryToggleCooldownMs = 500;
    private MainViewModel? _vm;

    // Animierter Hintergrund
    private readonly CalculatorBackgroundRenderer _backgroundRenderer = new();
    private DispatcherTimer? _bgTimer;
    private float _bgTime;

    // Onboarding
    private int _onboardingStep;
    private string[] _onboardingTexts = [];
    private VerticalAlignment[] _onboardingPositions = [];
    private bool _onboardingArmed;

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Tunnel-Routing fuer Swipe-Erkennung auch ueber Buttons
        AddHandler(PointerPressedEvent, OnPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Hintergrund-Render-Loop starten (~5fps)
        _bgTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _bgTimer.Tick += OnBackgroundTimerTick;
        _bgTimer.Start();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes VM abmelden um Memory Leak zu vermeiden
        if (_vm != null)
            _vm.PauseStateChanged -= OnPauseStateChanged;

        _vm = DataContext as MainViewModel;

        if (_vm != null)
        {
            _vm.PauseStateChanged += OnPauseStateChanged;

            // Onboarding-Hook: Das DataContext wird von App.RunLoadingAsync NACH der Loading-Pipeline
            // gesetzt (zeitgleich mit splash.FadeOut()). Früher hing der Trigger an
            // SplashOverlay.PreloadCompleted — diese zweite Splash + ihr zweiter ShaderPreloader-Lauf
            // sind entfernt (die Pipeline + SkiaLoadingSplash sind die einzige Splash). Wir warten auf
            // das erste LayoutUpdated nach VM-Zuweisung, damit der Visual-Tree steht (Tooltip-Position).
            if (!_onboardingArmed)
            {
                _onboardingArmed = true;
                LayoutUpdated += OnFirstLayoutUpdatedForOnboarding;
            }
        }
    }

    private void OnFirstLayoutUpdatedForOnboarding(object? sender, EventArgs e)
    {
        LayoutUpdated -= OnFirstLayoutUpdatedForOnboarding;
        Dispatcher.UIThread.Post(TryStartOnboarding);
    }

    /// <summary>
    /// App-Pause/Resume (Android-Lifecycle via MainViewModel): den ~5fps-Hintergrund-Render-Timer
    /// im Hintergrund anhalten — er ist rein dekorativ und niemand sieht ihn (Akku). Avalonia
    /// detacht die View beim App-Backgrounding nicht, daher greift OnDetachedFromVisualTree hier nicht.
    /// </summary>
    private void OnPauseStateChanged(bool isPaused)
    {
        if (isPaused)
            _bgTimer?.Stop();
        else
            _bgTimer?.Start();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Swipe-Tracking nur auf Calculator-Tab (nicht auf Converter/Settings)
        var vm = DataContext as MainViewModel;
        if (vm == null || !vm.IsCalculatorActive) return;

        _swipeStart = e.GetPosition(this);
        _swipeStartTime = DateTime.UtcNow;
        _isSwiping = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isSwiping) return;
        _isSwiping = false;

        // Zu schnelle Gesten ignorieren (versehentliche Swipes bei Button-Klicks)
        if ((DateTime.UtcNow - _swipeStartTime).TotalMilliseconds < MinSwipeMs) return;

        var vm = DataContext as MainViewModel;
        if (vm == null || !vm.IsCalculatorActive) return;

        // Cooldown: Verlauf nicht sofort wieder öffnen/schließen
        if ((DateTime.UtcNow - _lastHistoryToggle).TotalMilliseconds < HistoryToggleCooldownMs) return;

        var end = e.GetPosition(this);
        var deltaX = end.X - _swipeStart.X;
        var deltaY = end.Y - _swipeStart.Y;

        // Nur vertikale Swipes erkennen (mindestens 2x so viel vertikal wie horizontal)
        if (Math.Abs(deltaY) < Math.Abs(deltaX) * 2) return;

        if (deltaY < -SwipeThreshold && !vm.CalculatorViewModel.IsHistoryVisible)
        {
            // Swipe hoch -> Verlauf anzeigen (nur wenn geschlossen)
            vm.CalculatorViewModel.ShowHistoryCommand.Execute(null);
            _lastHistoryToggle = DateTime.UtcNow;
        }
        else if (deltaY > SwipeThreshold && vm.CalculatorViewModel.IsHistoryVisible)
        {
            // Swipe runter -> Verlauf ausblenden
            vm.CalculatorViewModel.HideHistoryCommand.Execute(null);
            _lastHistoryToggle = DateTime.UtcNow;
        }
    }

    private void OnHistoryBackdropTapped(object? sender, PointerPressedEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        if (vm?.CalculatorViewModel == null) return;
        vm.CalculatorViewModel.HideHistoryCommand.Execute(null);
        _lastHistoryToggle = DateTime.UtcNow;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Pause-Event abmelden (VM ist Singleton — sonst dangling Handler bei View-Neuerstellung)
        if (_vm != null)
            _vm.PauseStateChanged -= OnPauseStateChanged;

        // Hintergrund-Render-Loop stoppen und Renderer freigeben
        if (_bgTimer != null)
        {
            _bgTimer.Stop();
            _bgTimer.Tick -= OnBackgroundTimerTick;
            _bgTimer = null;
        }
        _backgroundRenderer.Dispose();
    }

    private void OnBackgroundTimerTick(object? sender, EventArgs e)
    {
        const float deltaTime = 0.2f; // 200ms Intervall
        _bgTime += deltaTime;
        _backgroundRenderer.Update(deltaTime);
        BackgroundCanvas?.InvalidateSurface();
    }

    private void OnBackgroundPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();
        _backgroundRenderer.Render(canvas, canvas.LocalClipBounds, _bgTime);
    }

    #region Onboarding

    private void TryStartOnboarding()
    {
        try
        {
            if (_vm == null || _vm.IsOnboardingCompleted) return;

            _onboardingTexts = _vm.GetOnboardingTexts();
            _onboardingPositions =
            [
                VerticalAlignment.Top,      // Display-Bereich (oben)
                VerticalAlignment.Center,    // Button-Grid (Mitte)
                VerticalAlignment.Top        // Mode-Selector (oben)
            ];

            _onboardingStep = 0;

            // 500ms Delay nach Splash-Ende
            DispatcherTimer.RunOnce(ShowNextOnboardingStep, TimeSpan.FromMilliseconds(500));
        }
        catch
        {
            // Onboarding nicht kritisch - Fehler ignorieren
        }
    }

    private void ShowNextOnboardingStep()
    {
        if (_onboardingStep >= _onboardingTexts.Length)
        {
            // Alle Schritte abgeschlossen
            OnboardingOverlay.IsVisible = false;
            _vm?.MarkOnboardingCompleted();
            return;
        }

        OnboardingOverlay.IsVisible = true;
        OnboardingTooltip.Text = _onboardingTexts[_onboardingStep];
        OnboardingTooltip.VerticalAlignment = _onboardingPositions[_onboardingStep];

        // Tooltip-Position anpassen
        OnboardingTooltip.Margin = _onboardingStep switch
        {
            0 => new Thickness(32, 120, 32, 0), // Unter dem Display
            1 => new Thickness(32, 0, 32, 80),   // Über der Tab-Bar
            2 => new Thickness(32, 60, 32, 0),   // Am Mode-Selector
            _ => new Thickness(32, 0)
        };

        // Dismissed-Event einmalig registrieren
        OnboardingTooltip.Dismissed -= OnTooltipDismissed;
        OnboardingTooltip.Dismissed += OnTooltipDismissed;

        OnboardingTooltip.Show();
    }

    private void OnTooltipDismissed(object? sender, EventArgs e)
    {
        OnboardingTooltip.Dismissed -= OnTooltipDismissed;
        _onboardingStep++;

        // Nächster Tooltip nach 300ms
        DispatcherTimer.RunOnce(ShowNextOnboardingStep, TimeSpan.FromMilliseconds(300));
    }

    #endregion
}
