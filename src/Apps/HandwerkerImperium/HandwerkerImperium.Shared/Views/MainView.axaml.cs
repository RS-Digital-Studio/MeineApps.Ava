using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.ViewModels;
using SkiaSharp;

namespace HandwerkerImperium.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;
    private string _lastActiveTab = "";

    // Meister Hans Animation
    private DispatcherTimer? _hansTimer;
    private float _hansElapsed;
    private bool _hansBlinking;
    private float _nextBlinkTime = 3.5f;

    public MainView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes VM abmelden
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.CelebrationRequested -= OnCelebrationRequested;
            StopHansAnimation();
        }

        _vm = DataContext as MainViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            _vm.CelebrationRequested += OnCelebrationRequested;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Tab-Wechsel erkennen und Fade-Animation ausloesen
        if (e.PropertyName is "IsDashboardActive" or "IsShopActive" or "IsStatisticsActive"
            or "IsAchievementsActive" or "IsSettingsActive" or "IsWorkerMarketActive"
            or "IsResearchActive")
        {
            // Pruefen ob ein neuer Tab aktiviert wurde
            var newTab = GetActiveTab();
            if (!string.IsNullOrEmpty(newTab) && newTab != _lastActiveTab)
            {
                _lastActiveTab = newTab;
                FadeInContentPanel();
            }
        }

        // Meister Hans Animation starten/stoppen wenn Story-Dialog sichtbar wird
        if (e.PropertyName == "IsStoryDialogVisible")
        {
            if (_vm?.IsStoryDialogVisible == true)
                StartHansAnimation();
            else
                StopHansAnimation();
        }
    }

    private string GetActiveTab()
    {
        if (_vm == null) return "";
        if (_vm.IsDashboardActive) return "Dashboard";
        if (_vm.IsShopActive) return "Shop";
        if (_vm.IsStatisticsActive) return "Statistics";
        if (_vm.IsAchievementsActive) return "Achievements";
        if (_vm.IsSettingsActive) return "Settings";
        if (_vm.IsWorkerMarketActive) return "WorkerMarket";
        if (_vm.IsResearchActive) return "Research";
        return "";
    }

    /// <summary>
    /// Fade-In Animation fuer das ContentPanel bei Tab-Wechsel.
    /// Startet bei Opacity 0 und animiert auf 1 (150ms, CubicEaseOut).
    /// </summary>
    private async void FadeInContentPanel()
    {
        var panel = this.FindControl<Panel>("ContentPanel");
        if (panel == null) return;

        panel.Opacity = 0;

        var fadeIn = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(150),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(OpacityProperty, 0.0) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1.0),
                    Setters = { new Setter(OpacityProperty, 1.0) }
                }
            }
        };

        await fadeIn.RunAsync(panel);
    }

    private void OnCelebrationRequested()
    {
        CelebrationCanvas.ShowConfetti();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Meister Hans SkiaSharp-Portrait
    // ═══════════════════════════════════════════════════════════════════════

    private void OnMeisterHansPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var bounds = canvas.LocalClipBounds;
        var mood = _vm?.StoryMood ?? "happy";

        MeisterHansRenderer.Render(canvas, bounds, mood, _hansElapsed, _hansBlinking);
    }

    private void StartHansAnimation()
    {
        _hansElapsed = 0;
        _hansBlinking = false;
        _nextBlinkTime = 3f + Random.Shared.NextSingle() * 1.5f;

        _hansTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50) // 20fps
        };

        _hansTimer.Tick -= OnHansTimerTick;
        _hansTimer.Tick += OnHansTimerTick;
        _hansTimer.Start();
    }

    private void StopHansAnimation()
    {
        _hansTimer?.Stop();
    }

    private void OnHansTimerTick(object? sender, EventArgs e)
    {
        _hansElapsed += 0.05f; // 50ms pro Tick

        // Blinzel-Logik: Alle 3-4.5s für ~150ms blinzeln
        if (_hansBlinking)
        {
            // Blinzel dauert 3 Frames (150ms)
            if (_hansElapsed > _nextBlinkTime + 0.15f)
            {
                _hansBlinking = false;
                _nextBlinkTime = _hansElapsed + 3f + Random.Shared.NextSingle() * 1.5f;
            }
        }
        else if (_hansElapsed >= _nextBlinkTime)
        {
            _hansBlinking = true;
        }

        MeisterHansCanvas?.InvalidateSurface();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Dialog-Overlay Handler
    // ═══════════════════════════════════════════════════════════════════════

    // Dialog-Overlay: Klick auf dunklen Hintergrund schließt den Dialog
    private void OnAlertOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.DismissAlertDialogCommand.Execute(null);
    }

    private void OnConfirmOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.ConfirmDialogCancelCommand.Execute(null);
    }

    // Worker-Profile Bottom-Sheet: Backdrop-Klick schließt das Sheet
    private void OnWorkerProfileBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.HandleBackPressed();
    }

    // Klick auf Dialog-Inhalt: Event nicht zum Overlay durchbubblen lassen
    private void OnDialogContentPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
