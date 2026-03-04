using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using FitnessRechner.Graphics;
using FitnessRechner.ViewModels;
using SkiaSharp;

namespace FitnessRechner.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;
    private readonly Random _rng = new();

    // SkiaSharp Renderer
    private readonly MedicalBackgroundRenderer _backgroundRenderer = new();
    private readonly MedicalTabBarRenderer _tabBarRenderer = new();
    private DispatcherTimer? _renderTimer;
    private float _renderTime;
    private float _lastTabSwitchTime;
    private SKRect _lastTabBarBounds;
    private string[] _tabLabels = ["Home", "Progress", "Food", "Settings"];

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Render-Timer stoppen und Renderer freigeben
        _renderTimer?.Stop();
        _renderTimer = null;
        _backgroundRenderer.Dispose();
        _tabBarRenderer.Dispose();

        // Events sauber abmelden bei Entfernung aus dem Visual Tree
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
            _vm = null;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Alte Events abmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
        }

        _vm = DataContext as MainViewModel;

        // Neue Events abonnieren
        if (_vm != null)
        {
            _vm.FloatingTextRequested += OnFloatingText;
            _vm.CelebrationRequested += OnCelebration;

            // Render-Timer einmalig starten wenn VM verfügbar
            StartRenderTimer();
        }
    }

    // =====================================================================
    // Render-Timer (20fps für animierten Hintergrund + Tab-Bar)
    // =====================================================================

    private void StartRenderTimer()
    {
        if (_renderTimer != null) return;

        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) }; // 20fps
        _renderTimer.Tick += OnRenderTimerTick;
        _renderTimer.Start();
    }

    private void OnRenderTimerTick(object? sender, EventArgs e)
    {
        _renderTime += 0.05f;
        _backgroundRenderer.Update(0.05f);
        BackgroundCanvas?.InvalidateSurface();
        TabBarCanvas?.InvalidateSurface();

        // HomeView animierte Canvases aktualisieren (VitalSignsHero)
        if (_vm?.IsHomeActive == true)
        {
            HomeViewControl?.OnRenderTick(_renderTime);
        }
    }

    // =====================================================================
    // SkiaSharp Paint-Handler
    // =====================================================================

    /// <summary>
    /// Zeichnet den animierten medizinischen Hintergrund (5 Layer).
    /// </summary>
    private void OnBackgroundPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        _backgroundRenderer.Render(canvas, bounds, _renderTime);
    }

    /// <summary>
    /// Zeichnet die holografische Tab-Bar mit Icons, Labels und Glow.
    /// </summary>
    private void OnTabBarPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        _lastTabBarBounds = bounds;

        // Lokalisierte Tab-Labels vom ViewModel holen
        if (_vm != null)
        {
            _tabLabels =
            [
                _vm.NavHomeText ?? "Home",
                _vm.NavProgressText ?? "Progress",
                _vm.NavFoodText ?? "Food",
                _vm.NavSettingsText ?? "Settings"
            ];
        }

        var state = new MedicalTabBarState
        {
            ActiveTab = GetActiveTabIndex(),
            Labels = _tabLabels,
            Time = _renderTime,
            TabSwitchTime = _lastTabSwitchTime
        };

        _tabBarRenderer.Render(canvas, bounds, state);
    }

    // =====================================================================
    // Tab-Bar Touch-Handling
    // =====================================================================

    /// <summary>
    /// Verarbeitet Touch/Klick auf die SkiaSharp-Tab-Bar.
    /// DPI-Skalierung wird berücksichtigt.
    /// </summary>
    private void OnTabBarPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (_vm == null || sender is not SKCanvasView canvas) return;

        var pos = e.GetPosition(canvas);

        // Avalonia-Koordinaten in SkiaSharp-Koordinaten umrechnen (DPI-Skalierung)
        float scaleX = _lastTabBarBounds.Width / (float)canvas.Bounds.Width;
        float scaleY = _lastTabBarBounds.Height / (float)canvas.Bounds.Height;
        float skiaX = (float)pos.X * scaleX;
        float skiaY = (float)pos.Y * scaleY;

        int tabIndex = _tabBarRenderer.HitTest(_lastTabBarBounds, skiaX, skiaY);
        if (tabIndex >= 0 && tabIndex != GetActiveTabIndex())
        {
            ExecuteTabCommand(tabIndex);
            _lastTabSwitchTime = _renderTime;
        }

        e.Handled = true;
    }

    // =====================================================================
    // Hilfsmethoden für Tab-Navigation
    // =====================================================================

    /// <summary>
    /// Ermittelt den Index des aktuell aktiven Tabs (0-3).
    /// </summary>
    private int GetActiveTabIndex()
    {
        if (_vm == null) return 0;
        if (_vm.IsHomeActive) return 0;
        if (_vm.IsProgressActive) return 1;
        if (_vm.IsFoodActive) return 2;
        if (_vm.IsSettingsActive) return 3;
        return 0;
    }

    /// <summary>
    /// Führt das Tab-Wechsel-Command für den gegebenen Index aus.
    /// </summary>
    private void ExecuteTabCommand(int index)
    {
        switch (index)
        {
            case 0: _vm?.SelectHomeTabCommand.Execute(null); break;
            case 1: _vm?.SelectProgressTabCommand.Execute(null); break;
            case 2: _vm?.SelectFoodTabCommand.Execute(null); break;
            case 3: _vm?.SelectSettingsTabCommand.Execute(null); break;
        }
    }

    // =====================================================================
    // Game Juice Events (FloatingText + Celebration)
    // =====================================================================

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
        FloatingTextCanvas.ShowFloatingText(text, w * (0.2 + _rng.NextDouble() * 0.6), h * 0.35, color, 16);
    }

    private void OnCelebration()
    {
        CelebrationCanvas.ShowConfetti();
    }
}
