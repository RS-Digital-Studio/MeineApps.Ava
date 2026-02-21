using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.ViewModels;
using SkiaSharp;

namespace HandwerkerImperium.Views;

public partial class DashboardView : UserControl
{
    private MainViewModel? _vm;
    private readonly Random _random = new();
    private TranslateTransform? _headerTranslate;

    // City-Skyline Rendering
    private readonly CityRenderer _cityRenderer = new();
    private readonly AnimationManager _animationManager = new();
    private DispatcherTimer? _renderTimer;
    private SKCanvasView? _cityCanvas;
    private DateTime _lastRenderTime = DateTime.UtcNow;

    // Hold-to-Upgrade
    private DispatcherTimer? _holdTimer;
    private WorkshopType? _holdWorkshopType;
    private int _holdUpgradeCount;

    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Parallax: ScrollViewer-Event abonnieren
        var scrollViewer = this.FindControl<ScrollViewer>("DashboardScrollViewer");
        if (scrollViewer != null)
            scrollViewer.ScrollChanged += OnScrollChanged;

        // Hold-to-Upgrade: Tunneling-Events auf alle Buttons im visuellen Baum
        AddHandler(InputElement.PointerPressedEvent, OnUpgradePointerPressed, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerReleasedEvent, OnUpgradePointerReleased, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Parallax-Effekt: Header-Background verschiebt sich leicht beim Scrollen.
    /// translateY = -scrollOffset * 0.3, maximal 20px.
    /// </summary>
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        var header = this.FindControl<Border>("HeaderBorder");
        var scrollViewer = sender as ScrollViewer;
        if (header == null || scrollViewer == null) return;

        _headerTranslate ??= new TranslateTransform();
        header.RenderTransform = _headerTranslate;

        var offset = Math.Min(scrollViewer.Offset.Y * 0.3, 20);
        _headerTranslate.Y = -offset;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes VM abmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingTextRequested;
            _vm.FloatingTextRequested -= OnFloatingTextForParticles;
            _vm = null;
        }

        // Timer stoppen wenn kein neues VM kommt
        _renderTimer?.Stop();

        // Neues VM abonnieren
        if (DataContext is MainViewModel vm)
        {
            _vm = vm;
            _vm.FloatingTextRequested += OnFloatingTextRequested;
            _vm.FloatingTextRequested += OnFloatingTextForParticles;

            // City-Canvas finden und Render-Loop starten
            _cityCanvas = this.FindControl<SKCanvasView>("CityCanvas");
            if (_cityCanvas != null)
            {
                _cityCanvas.PaintSurface -= OnCityPaintSurface;
                _cityCanvas.PaintSurface += OnCityPaintSurface;
                StartCityRenderLoop();
            }
        }
    }

    private void OnFloatingTextRequested(string text, string category)
    {
        // Farbe je nach Kategorie bestimmen
        var color = category switch
        {
            "money" => Color.Parse("#22C55E"),          // Gruen fuer Geld
            "xp" => Color.Parse("#FFD700"),             // Gold fuer XP
            "golden_screws" => Color.Parse("#FFD700"),   // Gold fuer Goldschrauben
            "level" => Color.Parse("#D97706"),            // Craft-Primaer fuer Level
            _ => Color.Parse("#FFFFFF")
        };

        // FontSize je nach Kategorie
        var fontSize = category switch
        {
            "level" => 20.0,
            "golden_screws" => 18.0,
            _ => 16.0
        };

        // X-Position: zufaellig im sichtbaren Bereich (20-80% der Breite)
        var canvasWidth = FloatingTextCanvas.Bounds.Width;
        if (canvasWidth < 10) canvasWidth = 300; // Fallback
        var x = canvasWidth * (0.2 + _random.NextDouble() * 0.6);

        // Y-Position: ~40% der Hoehe (Mitte-oben)
        var canvasHeight = FloatingTextCanvas.Bounds.Height;
        if (canvasHeight < 10) canvasHeight = 400; // Fallback
        var y = canvasHeight * 0.4;

        FloatingTextCanvas.ShowFloatingText(text, x, y, color, fontSize);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GAME JUICE: Muenz-Partikel, Money-Flash, Confetti bei Events
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zusaetzlicher FloatingText-Handler fuer Partikel-Effekte und Money-Flash.
    /// Wird parallel zum bestehenden OnFloatingTextRequested aufgerufen.
    /// </summary>
    private void OnFloatingTextForParticles(string text, string category)
    {
        // Muenz-Partikel bei Geld-Einnahmen
        if (category == "money" && _cityCanvas != null)
        {
            var bounds = _cityCanvas.Bounds;
            if (bounds.Width >= 10)
            {
                var centerX = (float)(bounds.Width * 0.5);
                var topY = (float)(bounds.Height * 0.3);

                for (int i = 0; i < 3; i++)
                {
                    _animationManager.AddCoinParticle(
                        centerX + _random.Next(-30, 30),
                        topY + _random.Next(-5, 10));
                }
            }

            // Kurzer Highlight-Flash auf dem Geld-Display
            var moneyText = this.FindControl<TextBlock>("MoneyText");
            if (moneyText != null)
            {
                _ = AnimateMoneyFlash(moneyText);
            }
        }

        // Confetti bei Level-Up oder Goldschrauben-Belohnung
        if (category is "level" or "golden_screws")
        {
            if (_cityCanvas != null)
            {
                var bounds = _cityCanvas.Bounds;
                if (bounds.Width >= 10)
                {
                    _animationManager.AddLevelUpConfetti(
                        (float)(bounds.Width / 2),
                        (float)(bounds.Height * 0.5));
                }
            }
        }
    }

    /// <summary>
    /// Kurzer Opacity-Flash auf dem Geld-Display bei Einnahmen.
    /// </summary>
    private static async Task AnimateMoneyFlash(TextBlock text)
    {
        var animation = new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromMilliseconds(400),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(Visual.OpacityProperty, 1.0) }
                },
                new KeyFrame
                {
                    Cue = new Cue(0.3),
                    Setters = { new Setter(Visual.OpacityProperty, 0.6) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1.0),
                    Setters = { new Setter(Visual.OpacityProperty, 1.0) }
                }
            }
        };
        await animation.RunAsync(text);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HOLD-TO-UPGRADE: Gedrückthalten = schnelles Hochleveln
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft ob das Source-Element oder ein Parent ein UpgradeBtn-Button ist.
    /// </summary>
    private static Button? FindUpgradeButton(object? source)
    {
        var visual = source as Avalonia.Visual;
        while (visual != null)
        {
            if (visual is Button btn && btn.Classes.Contains("UpgradeBtn"))
                return btn;
            visual = visual.GetVisualParent();
        }
        return null;
    }

    private void OnUpgradePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var btn = FindUpgradeButton(e.Source);
        if (btn?.DataContext is WorkshopDisplayModel workshop && workshop.IsUnlocked)
        {
            StartHoldUpgrade(workshop.Type);
        }
    }

    private void OnUpgradePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_holdTimer != null)
        {
            StopHoldUpgrade();
        }
    }

    /// <summary>
    /// Startet Hold-to-Upgrade Timer wenn Upgrade-Button gedrückt gehalten wird.
    /// </summary>
    private void StartHoldUpgrade(WorkshopType type)
    {
        _holdWorkshopType = type;
        _holdUpgradeCount = 0;

        // Dialoge während Hold unterdrücken
        if (_vm != null) _vm.IsHoldingUpgrade = true;

        _holdTimer?.Stop();
        _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _holdTimer.Tick += OnHoldTick;
        _holdTimer.Start();
    }

    /// <summary>
    /// Stoppt Hold-to-Upgrade und zeigt Gesamt-Ergebnis.
    /// </summary>
    private void StopHoldUpgrade()
    {
        _holdTimer?.Stop();
        _holdTimer = null;

        // Dialoge wieder erlauben
        if (_vm != null) _vm.IsHoldingUpgrade = false;

        if (_holdUpgradeCount > 1 && _vm != null)
        {
            // Sound nur einmal am Ende
            _vm.PlayUpgradeSound();
            OnFloatingTextRequested($"+{_holdUpgradeCount} Level!", "level");

            // Confetti-Burst bei großen Hold-Upgrades (5+ Level auf einmal)
            if (_holdUpgradeCount >= 5 && _cityCanvas != null)
            {
                var bounds = _cityCanvas.Bounds;
                _animationManager.AddLevelUpConfetti((float)bounds.Width / 2, (float)bounds.Height);
            }
        }

        _holdWorkshopType = null;
        _holdUpgradeCount = 0;
    }

    private void OnHoldTick(object? sender, EventArgs e)
    {
        if (_vm == null || _holdWorkshopType == null) return;

        if (_vm.UpgradeWorkshopSilent(_holdWorkshopType.Value))
        {
            _holdUpgradeCount++;
            _vm.RefreshSingleWorkshopPublic(_holdWorkshopType.Value);
        }
        else
        {
            // Kein Geld mehr → Timer stoppen
            StopHoldUpgrade();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CITY-SKYLINE: SkiaSharp Render-Loop für Header-Hintergrund
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Startet den Render-Timer für die City-Skyline (20 fps).
    /// </summary>
    private void StartCityRenderLoop()
    {
        _renderTimer?.Stop();
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) }; // 20 fps
        _renderTimer.Tick += (_, _) =>
        {
            _cityCanvas?.InvalidateSurface();
        };
        _renderTimer.Start();
    }

    /// <summary>
    /// PaintSurface-Handler: Zeichnet City-Skyline + Partikel-Effekte.
    /// </summary>
    private void OnCityPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        // Delta-Zeit berechnen
        var now = DateTime.UtcNow;
        var delta = (now - _lastRenderTime).TotalSeconds;
        _lastRenderTime = now;

        // GameState für CityRenderer holen
        if (_vm != null)
        {
            var gameState = _vm.GetGameStateForRendering();
            if (gameState != null)
            {
                _cityRenderer.Render(canvas, bounds, gameState, gameState.Buildings, (float)delta);
            }
        }

        // AnimationManager Update + Render
        _animationManager.Update(delta);
        _animationManager.Render(canvas);
    }

    #region ProgressBar Paint-Handler

    /// <summary>
    /// Spieler XP-Level-Fortschritt (amber/gold Gradient).
    /// </summary>
    private void OnPaintLevelProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        if (_vm == null) return;

        MeineApps.UI.SkiaSharp.LinearProgressVisualization.Render(canvas, bounds,
            (float)_vm.LevelProgress,
            new SKColor(0xF5, 0x9E, 0x0B), // Amber Start
            new SKColor(0xFF, 0xD7, 0x00), // Gold End
            showText: false, glowEnabled: true);
    }

    /// <summary>
    /// Tages-Challenge Fortschritt (CraftPrimary orange).
    /// DataContext ist DailyChallenge (aus DataTemplate).
    /// </summary>
    private void OnPaintChallengeProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (sender is not SKCanvasView canvasView) return;
        var dc = canvasView.DataContext;
        if (dc == null) return;

        // Progress-Property per Reflection
        var progressProp = dc.GetType().GetProperty("Progress");
        if (progressProp == null) return;
        var progress = (float)(double)(progressProp.GetValue(dc) ?? 0.0);

        MeineApps.UI.SkiaSharp.LinearProgressVisualization.Render(canvas, bounds,
            progress,
            new SKColor(0xD9, 0x77, 0x06), // CraftPrimary
            new SKColor(0xF5, 0x9E, 0x0B), // CraftPrimaryLight
            showText: false, glowEnabled: false);
    }

    /// <summary>
    /// Wöchentliche Missionen Fortschritt (Amber/Gold).
    /// DataContext ist WeeklyMission (aus DataTemplate).
    /// </summary>
    private void OnPaintWeeklyMissionProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (sender is not SKCanvasView canvasView) return;
        var dc = canvasView.DataContext;
        if (dc == null) return;

        // Progress-Property per Reflection (decimal → float)
        var progressProp = dc.GetType().GetProperty("Progress");
        if (progressProp == null) return;
        var progress = (float)Convert.ToDouble(progressProp.GetValue(dc) ?? 0.0m);

        MeineApps.UI.SkiaSharp.LinearProgressVisualization.Render(canvas, bounds,
            progress,
            new SKColor(0xD9, 0x77, 0x06), // Amber
            new SKColor(0xFF, 0xD7, 0x00), // Gold
            showText: false, glowEnabled: false);
    }

    /// <summary>
    /// Workshop-Level Fortschritt (Farbe abhängig vom Workshop-Typ).
    /// DataContext ist WorkshopDisplayModel (aus DataTemplate).
    /// </summary>
    private void OnPaintWorkshopLevelProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (sender is not SKCanvasView canvasView) return;
        var dc = canvasView.DataContext;
        if (dc == null) return;

        var progressProp = dc.GetType().GetProperty("LevelProgress");
        if (progressProp == null) return;
        var progress = (float)(double)(progressProp.GetValue(dc) ?? 0.0);

        // Workshop-Farbe: CraftPrimary als Default
        MeineApps.UI.SkiaSharp.LinearProgressVisualization.Render(canvas, bounds,
            progress,
            new SKColor(0xD9, 0x77, 0x06), // CraftPrimary dunkel
            new SKColor(0xF5, 0x9E, 0x0B), // CraftPrimaryLight
            showText: false, glowEnabled: false);
    }

    /// <summary>
    /// Milestone-Fortschritt (Gold, dezent).
    /// DataContext ist WorkshopDisplayModel (aus DataTemplate).
    /// </summary>
    private void OnPaintMilestoneProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (sender is not SKCanvasView canvasView) return;
        var dc = canvasView.DataContext;
        if (dc == null) return;

        var progressProp = dc.GetType().GetProperty("MilestoneProgress");
        if (progressProp == null) return;
        var progress = (float)(double)(progressProp.GetValue(dc) ?? 0.0);

        MeineApps.UI.SkiaSharp.LinearProgressVisualization.Render(canvas, bounds,
            progress,
            new SKColor(0xFF, 0xD7, 0x00), // Gold Start
            new SKColor(0xFF, 0xB3, 0x00), // Gold End
            showText: false, glowEnabled: false);
    }

    #endregion
}
