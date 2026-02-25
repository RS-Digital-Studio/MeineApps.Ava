using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.ViewModels;
using MeineApps.UI.SkiaSharp.Shaders;
using SkiaSharp;

namespace HandwerkerImperium.Views;

public partial class DashboardView : UserControl
{
    private MainViewModel? _vm;
    private TranslateTransform? _headerTranslate;

    // City-Skyline Rendering
    private readonly CityRenderer _cityRenderer = new();
    private readonly CityWeatherSystem _weatherSystem = new();
    private readonly AnimationManager _animationManager = new();
    private readonly CoinFlyAnimation _coinFlyAnimation = new();
    private GameJuiceEngine? _juiceEngine;
    private DispatcherTimer? _renderTimer;
    private SKCanvasView? _cityCanvas;
    private DateTime _lastRenderTime = DateTime.UtcNow;
    private float _renderTime; // Fortlaufende Zeit für Shader-Effekte

    // Workshop-Karten (SkiaSharp 2x4 Grid)
    private SKRect _lastWorkshopCardsBounds;

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

        // Hold-to-Upgrade wird jetzt direkt im WorkshopCardsCanvas behandelt (PointerPressed/Released)
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

        // Parallax auf CityRenderer (5-Layer)
        _cityRenderer.ScrollOffset = (float)scrollViewer.Offset.Y;
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

            // GameJuiceEngine über MainViewModel (kein Service Locator)
            _juiceEngine = vm.GameJuiceEngine;
            _juiceEngine?.SetVignette(0.25f); // Subtile Vignette für Tiefe

            // Wetter-System nach aktuellem Monat initialisieren
            _weatherSystem.SetWeatherByMonth();

            // City-Canvas finden und Render-Loop starten
            _cityCanvas = this.FindControl<SKCanvasView>("CityCanvas");
            if (_cityCanvas != null)
            {
                _cityCanvas.PaintSurface -= OnCityPaintSurface;
                _cityCanvas.PaintSurface += OnCityPaintSurface;

                // Touch-Handler: Tap auf Workshop → Navigation
                _cityCanvas.PointerPressed -= OnCityCanvasTapped;
                _cityCanvas.PointerPressed += OnCityCanvasTapped;

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
        var x = canvasWidth * (0.2 + Random.Shared.NextDouble() * 0.6);

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
                        centerX + Random.Shared.Next(-30, 30),
                        topY + Random.Shared.Next(-5, 10));
                }

                // CoinFly: Münzen fliegen zum MoneyText (oben links)
                _coinFlyAnimation.Start(
                    centerX, topY,
                    60, 20,
                    count: 6, coinSize: 6f);
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

                    // GameJuice: ConfettiBurst + FlashOverlay bei Level-Up
                    _juiceEngine?.ConfettiBurst((float)(bounds.Width / 2), (float)(bounds.Height * 0.5), count: 25);
                    _juiceEngine?.FlashOverlay(new SKColor(0xFF, 0xD7, 0x00, 60), 0.12f);
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
    // WORKSHOP-KARTEN: SkiaSharp Rendering + Touch-Handling
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Rendert alle 8 Workshop-Karten als 2x4 Grid in einem einzelnen Draw-Call.
    /// </summary>
    private void OnWorkshopCardsPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        _lastWorkshopCardsBounds = bounds;

        if (_vm?.Workshops == null || _vm.Workshops.Count == 0) return;

        int cols = 2;
        float gap = 8f;
        float cardW = (bounds.Width - (cols - 1) * gap) / cols;
        int rows = (int)Math.Ceiling(_vm.Workshops.Count / (double)cols);
        float cardH = (bounds.Height - (rows - 1) * gap) / rows;

        for (int i = 0; i < _vm.Workshops.Count; i++)
        {
            int col = i % cols;
            int row = i / cols;

            float x = col * (cardW + gap);
            float y = row * (cardH + gap);
            var cardBounds = new SKRect(x, y, x + cardW, y + cardH);

            var data = MapToCardData(_vm.Workshops[i]);
            WorkshopGameCardRenderer.Render(canvas, cardBounds, data, _renderTime);
        }
    }

    /// <summary>
    /// Touch auf Workshop-Karten: HitTest für Karten-Body (Navigation) oder
    /// Upgrade-Button (Upgrade + Hold-to-Upgrade starten).
    /// </summary>
    private void OnWorkshopCardsPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm?.Workshops == null || sender is not Avalonia.Labs.Controls.SKCanvasView canvasView) return;

        var pos = e.GetPosition(canvasView);

        // Avalonia → SkiaSharp Koordinaten (DPI-Skalierung)
        float scaleX = _lastWorkshopCardsBounds.Width / (float)canvasView.Bounds.Width;
        float scaleY = _lastWorkshopCardsBounds.Height / (float)canvasView.Bounds.Height;
        float skiaX = (float)pos.X * scaleX;
        float skiaY = (float)pos.Y * scaleY;

        // Grid-Layout berechnen
        int cols = 2;
        float gap = 8f;
        float cardW = (_lastWorkshopCardsBounds.Width - (cols - 1) * gap) / cols;
        int rows = (int)Math.Ceiling(_vm.Workshops.Count / (double)cols);
        float cardH = (_lastWorkshopCardsBounds.Height - (rows - 1) * gap) / rows;

        // Welche Karte wurde getroffen?
        int hitCol = (int)(skiaX / (cardW + gap));
        int hitRow = (int)(skiaY / (cardH + gap));

        if (hitCol < 0 || hitCol >= cols || hitRow < 0 || hitRow >= rows) return;

        int index = hitRow * cols + hitCol;
        if (index >= _vm.Workshops.Count) return;

        // Prüfen ob der Tap innerhalb der Karte liegt (nicht im Gap)
        float cardX = hitCol * (cardW + gap);
        float cardY = hitRow * (cardH + gap);
        var cardBounds = new SKRect(cardX, cardY, cardX + cardW, cardY + cardH);
        if (!cardBounds.Contains(skiaX, skiaY)) return;

        var workshop = _vm.Workshops[index];

        // Prüfen ob der Tap auf dem Upgrade-Button liegt
        if (workshop.IsUnlocked && !workshop.IsMaxLevel)
        {
            var upgradeBounds = WorkshopGameCardRenderer.GetUpgradeButtonBounds(cardBounds);
            if (upgradeBounds.Contains(skiaX, skiaY))
            {
                // Erstes Upgrade sofort ausführen (mit UI-Feedback)
                _vm.UpgradeWorkshopCommand.Execute(workshop);

                // Hold-to-Upgrade starten für schnelles Hochleveln
                StartHoldUpgrade(workshop.Type);
                e.Handled = true;
                return;
            }
        }

        // Karten-Body: Navigation zum Workshop / Freischalten
        _vm.SelectWorkshopCommand.Execute(workshop);

        // RadialBurst-Effekt am Tap-Punkt
        _juiceEngine?.RadialBurst(skiaX, skiaY, new SKColor(0xD9, 0x77, 0x06), maxRadius: 40f);

        e.Handled = true;
    }

    /// <summary>
    /// PointerReleased auf Workshop-Karten: Hold-to-Upgrade stoppen.
    /// </summary>
    private void OnWorkshopCardsPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_holdTimer != null)
        {
            StopHoldUpgrade();
        }
    }

    /// <summary>
    /// Mappt ein WorkshopDisplayModel auf die WorkshopCardData-Struktur für den Renderer.
    /// </summary>
    private static WorkshopCardData MapToCardData(WorkshopDisplayModel model)
    {
        return new WorkshopCardData
        {
            Type = model.Type,
            Level = model.Level,
            WorkerCount = model.WorkerCount,
            MaxWorkers = model.MaxWorkers,
            IsUnlocked = model.IsUnlocked,
            CanBuyUnlock = model.CanBuyUnlock,
            CanAffordUpgrade = model.CanAffordUpgrade,
            CanAffordUnlock = model.CanAffordUnlock,
            IsMaxLevel = model.IsMaxLevel,
            LevelProgress = (float)model.LevelProgress,
            MilestoneProgress = (float)model.MilestoneProgress,
            NextMilestone = model.NextMilestone,
            ShowMilestone = model.ShowMilestone,
            IncomeText = model.IncomeDisplay,
            UpgradeCostText = model.UpgradeCostDisplay,
            NetIncomeText = model.NetIncomeDisplay,
            IsNetNegative = model.IsNetNegative,
            UnlockLevel = model.UnlockLevel
        };
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

                // GameJuice: ScreenShake + RadialBurst bei großem Upgrade
                _juiceEngine?.ScreenShake(3f, 0.25f);
                _juiceEngine?.RadialBurst(
                    (float)bounds.Width / 2, (float)bounds.Height * 0.7f,
                    new SKColor(0xD9, 0x77, 0x06), maxRadius: 80f);
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
            WorkshopCardsCanvas?.InvalidateSurface(); // Workshop-Karten mit animierten Headern
        };
        _renderTimer.Start();
    }

    /// <summary>
    /// PaintSurface-Handler: Zeichnet City-Skyline + Shader-Effekte + Partikel.
    /// </summary>
    private void OnCityPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        // Delta-Zeit berechnen
        var now = DateTime.UtcNow;
        var deltaSeconds = (float)(now - _lastRenderTime).TotalSeconds;
        _lastRenderTime = now;
        _renderTime += deltaSeconds;

        // ScreenShake-Offset anwenden (falls aktiv)
        if (_juiceEngine != null)
        {
            _juiceEngine.Update(deltaSeconds);
            float shakeX = _juiceEngine.ShakeOffsetX;
            float shakeY = _juiceEngine.ShakeOffsetY;
            if (MathF.Abs(shakeX) > 0.1f || MathF.Abs(shakeY) > 0.1f)
            {
                canvas.Save();
                canvas.Translate(shakeX, shakeY);
            }
        }

        // GameState für CityRenderer holen
        if (_vm != null)
        {
            var gameState = _vm.GetGameStateForRendering();
            if (gameState != null)
            {
                _cityRenderer.Render(canvas, bounds, gameState, gameState.Buildings, deltaSeconds);

                // Wetter-Overlay (Regen/Schnee/Blätter/Sonnenstrahlen)
                _weatherSystem.Update(deltaSeconds);
                _weatherSystem.Render(canvas, bounds);
            }

            // Gold-Shimmer auf Goldschrauben-Bereich (oben links)
            if (_vm.GoldenScrewsDisplay != "0")
            {
                var screwBounds = new SKRect(bounds.Left + 8, bounds.Top + 6, bounds.Left + 100, bounds.Top + 32);
                SkiaShimmerEffect.DrawGoldShimmer(canvas, screwBounds, _renderTime);
            }
        }

        // CoinFly-Animation
        _coinFlyAnimation.Update(deltaSeconds);
        _coinFlyAnimation.Render(canvas);

        // AnimationManager (bestehende Partikel)
        _animationManager.Update(deltaSeconds);
        _animationManager.Render(canvas);

        // GameJuiceEngine Effekte (über allem)
        _juiceEngine?.Render(canvas, bounds);

        // ScreenShake Restore
        if (_juiceEngine != null &&
            (MathF.Abs(_juiceEngine.ShakeOffsetX) > 0.1f || MathF.Abs(_juiceEngine.ShakeOffsetY) > 0.1f))
        {
            canvas.Restore();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CITY-CANVAS: Touch-Handler für Workshop-Tap
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tap auf City-Canvas: HitTest auf Workshop-Gebäude → Navigation + RadialBurst.
    /// </summary>
    private void OnCityCanvasTapped(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null || _cityCanvas == null) return;

        var point = e.GetPosition(_cityCanvas);
        var bounds = _cityCanvas.Bounds;
        if (bounds.Width < 10 || bounds.Height < 10) return;

        // Nur Tap in der oberen Hälfte (Workshop-Bereich) berücksichtigen
        float tapY = (float)point.Y;
        float canvasHeight = (float)bounds.Height;
        if (tapY > canvasHeight * 0.7f) return; // Unterer Bereich ignorieren

        // Workshop-HitTest: X-Position auf Workshop-Index mappen
        var allTypes = Enum.GetValues<WorkshopType>();
        int count = allTypes.Length;
        if (count == 0) return;

        float totalWidth = (float)bounds.Width - 16f;
        float gap = 5f;
        float buildingWidth = Math.Max(22f, (totalWidth - (count - 1) * gap) / count);

        float tapX = (float)point.X;
        float x = 8f;
        for (int i = 0; i < count; i++)
        {
            if (tapX >= x && tapX <= x + buildingWidth)
            {
                var type = allTypes[i];
                var gameState = _vm.GetGameStateForRendering();
                if (gameState != null && gameState.IsWorkshopUnlocked(type))
                {
                    // RadialBurst-Effekt am Tap-Punkt
                    _juiceEngine?.RadialBurst(
                        (float)point.X, (float)point.Y,
                        new SKColor(0xD9, 0x77, 0x06), maxRadius: 50f);

                    // Zum Workshop navigieren
                    _vm.NavigateToWorkshopFromCity(type);
                }
                break;
            }
            x += buildingWidth + gap;
        }
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

    // OnPaintChallengeProgress → Views/Dashboard/DailyChallengeSection.axaml.cs
    // OnPaintWeeklyMissionProgress → Views/Dashboard/WeeklyMissionSection.axaml.cs

    // Workshop-Level- und Milestone-ProgressBars werden jetzt direkt
    // im WorkshopGameCardRenderer gezeichnet (kein separater Handler nötig).

    #endregion
}
