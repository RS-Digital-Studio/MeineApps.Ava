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
    private float _hansElapsed;
    private bool _hansBlinking;
    private float _nextBlinkTime = 3.5f;

    // SkiaSharp Tab-Bar + Screen-Transitions + Background (Phase 3+4)
    private readonly GameTabBarRenderer _tabBarRenderer = new();
    private readonly ScreenTransitionRenderer _transitionRenderer = new();
    private readonly GameBackgroundRenderer _backgroundRenderer = new();

    // Full-Screen Reward-Zeremonie (Phase 7)
    private readonly RewardCeremonyRenderer _ceremonyRenderer = new();

    // Animierter Loading-Screen (Phase 10)
    private readonly LoadingScreenRenderer _loadingRenderer = new();
    private bool _loadingTipsInitialized;

    private DispatcherTimer? _renderTimer;
    private float _renderTime;
    private float _lastTabSwitchTime;
    private SKRect _lastTabBarBounds;
    private SKRect _lastBackgroundBounds;
    private GameScreenType _currentScreenType = GameScreenType.Dashboard;
    private string[] _tabLabels = ["Home", "Gebäude", "Gilde", "Shop", "Einstellungen"];

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
            _vm.CeremonyRequested -= OnCeremonyRequested;
        }

        _vm = DataContext as MainViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            _vm.CelebrationRequested += OnCelebrationRequested;
            _vm.CeremonyRequested += OnCeremonyRequested;

            // Tab-Labels initialisieren
            RefreshTabLabels();

            // Render-Timer starten (für Tab-Bar + Transition + Hans)
            StartRenderTimer();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Render-Timer (20fps - Tab-Bar, Transition, Hans)
    // ═══════════════════════════════════════════════════════════════════════

    private void StartRenderTimer()
    {
        if (_renderTimer != null) return;

        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50) // 20fps
        };
        _renderTimer.Tick += OnRenderTimerTick;
        _renderTimer.Start();
    }

    private void OnRenderTimerTick(object? sender, EventArgs e)
    {
        _renderTime += 0.05f;

        // Animierten Hintergrund aktualisieren (Partikel + Repaint)
        _backgroundRenderer.UpdateParticles(0.05f, _currentScreenType, _lastBackgroundBounds);
        BackgroundCanvas?.InvalidateSurface();

        // Tab-Bar aktualisieren
        TabBarCanvas?.InvalidateSurface();

        // Screen-Transition aktualisieren (wenn aktiv)
        if (_transitionRenderer.IsActive)
        {
            _transitionRenderer.Update(0.05f);
            TransitionCanvas?.InvalidateSurface();
        }

        // Reward-Zeremonie aktualisieren
        if (_ceremonyRenderer.IsActive)
        {
            _ceremonyRenderer.Update(0.05f);
            CeremonyCanvas?.InvalidateSurface();

            if (!_ceremonyRenderer.IsActive)
            {
                CeremonyCanvas.IsVisible = false;
            }
        }

        // Loading-Screen aktualisieren (solange sichtbar)
        if (_vm?.IsLoading == true)
        {
            LoadingCanvas?.InvalidateSurface();
        }

        // Meister Hans aktualisieren (nur wenn Story-Dialog sichtbar)
        if (_vm?.IsStoryDialogVisible == true)
        {
            UpdateHansAnimation();
            MeisterHansCanvas?.InvalidateSurface();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Animierter Hintergrund (Phase 4)
    // ═══════════════════════════════════════════════════════════════════════

    private void OnBackgroundPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var bounds = canvas.LocalClipBounds;
        _lastBackgroundBounds = bounds;

        _backgroundRenderer.Render(canvas, bounds, _currentScreenType, _renderTime);
    }

    /// <summary>
    /// Ermittelt den GameScreenType basierend auf dem aktiven Tab/View.
    /// </summary>
    private GameScreenType GetCurrentScreenType()
    {
        if (_vm == null) return GameScreenType.Dashboard;
        if (_vm.IsDashboardActive) return GameScreenType.Dashboard;
        if (_vm.IsBuildingsActive) return GameScreenType.Buildings;
        if (_vm.IsGuildActive) return GameScreenType.Guild;
        if (_vm.IsShopActive) return GameScreenType.Shop;
        if (_vm.IsSettingsActive) return GameScreenType.Settings;
        if (_vm.IsWorkerMarketActive) return GameScreenType.Workers;
        if (_vm.IsResearchActive) return GameScreenType.Research;
        // Workshop-Detail, MiniGames etc. → Workshop-Hintergrund
        return GameScreenType.Workshop;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SkiaSharp Tab-Bar
    // ═══════════════════════════════════════════════════════════════════════

    private void OnTabBarPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var bounds = canvas.LocalClipBounds;
        _lastTabBarBounds = bounds;

        var state = new TabBarState
        {
            ActiveTab = GetActiveTabIndex(),
            BadgeCounts = new int[5],
            Labels = _tabLabels,
            Time = _renderTime,
            TabSwitchTime = _lastTabSwitchTime
        };

        _tabBarRenderer.Render(canvas, bounds, state);
    }

    private void OnTabBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null || sender is not Avalonia.Labs.Controls.SKCanvasView canvas) return;

        var pos = e.GetPosition(canvas);

        // Avalonia-Koordinaten → SkiaSharp-Koordinaten (DPI-Skalierung)
        float scaleX = _lastTabBarBounds.Width / (float)canvas.Bounds.Width;
        float scaleY = _lastTabBarBounds.Height / (float)canvas.Bounds.Height;
        float skiaX = (float)pos.X * scaleX;
        float skiaY = (float)pos.Y * scaleY;

        int tabIndex = _tabBarRenderer.HitTest(_lastTabBarBounds, skiaX, skiaY);
        if (tabIndex >= 0 && tabIndex != GetActiveTabIndex())
        {
            // Screen-Transition starten
            _transitionRenderer.StartTransition(TransitionType.Dissolve);
            TransitionCanvas?.InvalidateSurface();

            // Tab-Wechsel ausführen
            ExecuteTabCommand(tabIndex);
            _lastTabSwitchTime = _renderTime;
        }

        e.Handled = true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Screen-Transition Overlay
    // ═══════════════════════════════════════════════════════════════════════

    private void OnTransitionPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (!_transitionRenderer.IsActive && _transitionRenderer.Progress < 0.01f) return;

        var bounds = canvas.LocalClipBounds;
        _transitionRenderer.Render(canvas, bounds);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tab-Verwaltung
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt den Index des aktiven Haupt-Tabs zurück (0-4), oder -1 bei Detail-Views.
    /// </summary>
    private int GetActiveTabIndex()
    {
        if (_vm == null) return 0;
        if (_vm.IsDashboardActive) return 0;
        if (_vm.IsBuildingsActive) return 1;
        if (_vm.IsGuildActive) return 2;
        if (_vm.IsShopActive) return 3;
        if (_vm.IsSettingsActive) return 4;
        return -1;
    }

    /// <summary>
    /// Führt den passenden SelectXxxTabCommand für den Tab-Index aus.
    /// </summary>
    private void ExecuteTabCommand(int tabIndex)
    {
        if (_vm == null) return;
        switch (tabIndex)
        {
            case 0: _vm.SelectDashboardTabCommand.Execute(null); break;
            case 1: _vm.SelectBuildingsTabCommand.Execute(null); break;
            case 2: _vm.SelectGuildTabCommand.Execute(null); break;
            case 3: _vm.SelectShopTabCommand.Execute(null); break;
            case 4: _vm.SelectSettingsTabCommand.Execute(null); break;
        }
    }

    /// <summary>
    /// Lokalisierte Tab-Labels vom ViewModel holen.
    /// </summary>
    private void RefreshTabLabels()
    {
        _tabLabels = _vm?.GetTabLabels() ?? ["Home", "Gebäude", "Gilde", "Shop", "Einstellungen"];
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Property-Changed + Content-Animation
    // ═══════════════════════════════════════════════════════════════════════

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Tab-Wechsel erkennen und Fade-Animation + Hintergrund-Wechsel auslösen
        if (e.PropertyName is "IsDashboardActive" or "IsShopActive" or "IsStatisticsActive"
            or "IsAchievementsActive" or "IsSettingsActive" or "IsWorkerMarketActive"
            or "IsResearchActive" or "IsBuildingsActive" or "IsGuildActive")
        {
            // Hintergrund-Typ aktualisieren
            _currentScreenType = GetCurrentScreenType();

            // Prüfen ob ein neuer Tab aktiviert wurde
            var newTab = GetActiveTab();
            if (!string.IsNullOrEmpty(newTab) && newTab != _lastActiveTab)
            {
                _lastActiveTab = newTab;
                FadeInContentPanel();
            }
        }

        // Meister Hans Animation: Reset bei Story-Dialog-Öffnung
        if (e.PropertyName == "IsStoryDialogVisible")
        {
            if (_vm?.IsStoryDialogVisible == true)
            {
                _hansElapsed = 0;
                _hansBlinking = false;
                _nextBlinkTime = 3f + Random.Shared.NextSingle() * 1.5f;
            }
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
        if (_vm.IsBuildingsActive) return "Buildings";
        if (_vm.IsGuildActive) return "Guild";
        return "";
    }

    /// <summary>
    /// Fade-In Animation für das ContentPanel bei Tab-Wechsel.
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

    private void OnCeremonyRequested(CeremonyType type, string title, string subtitle)
    {
        _ceremonyRenderer.Start(type, title, subtitle);
        CeremonyCanvas.IsVisible = true;
        CeremonyCanvas.InvalidateSurface();
    }

    private void OnCeremonyPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (!_ceremonyRenderer.IsActive) return;
        var bounds = canvas.LocalClipBounds;
        _ceremonyRenderer.Render(canvas, bounds);
    }

    private void OnCeremonyTapped(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        _ceremonyRenderer.Dismiss();
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

    /// <summary>
    /// Aktualisiert die Hans-Blinzel-Animation (wird vom Render-Timer aufgerufen).
    /// </summary>
    private void UpdateHansAnimation()
    {
        _hansElapsed += 0.05f; // 50ms pro Tick

        // Blinzel-Logik: Alle 3-4.5s für ~150ms blinzeln
        if (_hansBlinking)
        {
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
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Loading-Screen (Phase 10)
    // ═══════════════════════════════════════════════════════════════════════

    private void OnLoadingPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // Tipps einmalig setzen
        if (!_loadingTipsInitialized)
        {
            _loadingRenderer.SetTips([
                "Tipp: Halte den Upgrade-Button gedrückt für schnelles Leveln!",
                "Tipp: Höhere Worker-Tiers verdienen deutlich mehr!",
                "Tipp: Schau täglich vorbei für Login-Belohnungen!",
                "Tipp: Prestige schaltet neue Boni und Workshops frei!",
                "Tipp: Reputation über 70 bringt Extra-Aufträge!",
                "Tipp: Meisterwerkzeuge geben permanente Einkommens-Boni!"
            ]);
            _loadingTipsInitialized = true;
        }

        var bounds = canvas.LocalClipBounds;
        _loadingRenderer.Render(canvas, bounds, _renderTime);
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
