using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MeineApps.CalcLib;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using RechnerPlus.ViewModels;
using System;
using System.Diagnostics;
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

    // Onboarding
    private int _onboardingStep;
    private string[] _onboardingTexts = [];
    private VerticalAlignment[] _onboardingPositions = [];

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

        var loc = App.Services.GetService<ILocalizationService>();

        // Preloading-Tasks im SplashOverlay konfigurieren
        Splash.PreloadAction = async (reportProgress) =>
        {
            // Schritt 1: SkSL-Shader vorab kompilieren (auf ThreadPool)
            reportProgress(0.0f, loc?.GetString("LoadingShaders") ?? "Grafik-Engine wird vorbereitet...");
            await Task.Run(() => ShaderPreloader.PreloadAll());

            // Schritt 2: Calculator-Engine warm machen (erster Parse-Durchlauf)
            reportProgress(0.60f, loc?.GetString("LoadingCalculator") ?? "Rechner wird initialisiert...");
            await Task.Run(() =>
            {
                try
                {
                    var parser = App.Services.GetService<ExpressionParser>();
                    // Einmal parsen um JIT/Caches aufzuwärmen
                    parser?.Evaluate("1+1");
                }
                catch (Exception ex) { Debug.WriteLine($"[SplashPreload] Engine: {ex.Message}"); }
            });

            // History ist bereits im Constructor geladen (LoadHistory() in CalculatorVM)
            reportProgress(1.0f, "");
        };

        // Benannter Handler für sauberes Unsubscribe (HOCH-2)
        Splash.PreloadCompleted += OnSplashPreloadCompleted;
    }

    private void OnSplashPreloadCompleted(object? sender, EventArgs e)
    {
        Splash.PreloadCompleted -= OnSplashPreloadCompleted;
        Dispatcher.UIThread.Post(TryStartOnboarding);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes ViewModel abmelden
        if (_vm != null)
            _vm.FloatingTextRequested -= OnFloatingText;

        _vm = DataContext as MainViewModel;

        // Neues ViewModel anmelden
        if (_vm != null)
            _vm.FloatingTextRequested += OnFloatingText;
    }

    private void OnFloatingText(string text, string category)
    {
        var color = Color.Parse("#6366F1");
        var w = FloatingTextCanvas.Bounds.Width;
        if (w < 10) w = 300;
        var h = FloatingTextCanvas.Bounds.Height;
        if (h < 10) h = 400;
        FloatingTextCanvas.ShowFloatingText(text, w * 0.3, h * 0.3, color, 14);
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

    #region Onboarding

    private void TryStartOnboarding()
    {
        try
        {
            var prefs = App.Services.GetService<IPreferencesService>();
            if (prefs == null) return;

            if (prefs.Get("onboarding_shown_v2", false)) return;

            // Lokalisierte Texte laden
            var loc = App.Services.GetService<ILocalizationService>();
            _onboardingTexts =
            [
                loc?.GetString("OnboardingSwipeDelete") ?? "Wische nach links zum Löschen",
                loc?.GetString("OnboardingSwipeHistory") ?? "Wische hoch für den Verlauf",
                loc?.GetString("OnboardingScientific") ?? "Drehe dein Gerät für den Wissenschaftsmodus"
            ];
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

            try
            {
                var prefs = App.Services.GetService<IPreferencesService>();
                prefs?.Set("onboarding_shown_v2", true);
            }
            catch { /* nicht kritisch */ }

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
