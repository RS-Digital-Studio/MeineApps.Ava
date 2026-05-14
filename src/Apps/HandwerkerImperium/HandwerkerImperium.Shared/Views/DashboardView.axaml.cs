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
using HandwerkerImperium.Services;
using HandwerkerImperium.ViewModels;
using HandwerkerImperium.Helpers;
using MeineApps.UI.SkiaSharp.Shaders;
using SkiaSharp;

namespace HandwerkerImperium.Views;

public partial class DashboardView : UserControl
{
    private MainViewModel? _vm;
    private TranslateTransform? _headerTranslate;
    private Border? _headerBorder; // Gecacht statt FindControl bei jedem Scroll-Event

    // Gecachte Money-Flash-Animation (vermeidet Allokation bei jedem Aufruf)
    private static readonly Avalonia.Animation.Animation s_moneyFlashAnimation = new()
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

    // City-Skyline Rendering
    private readonly CityRenderer _cityRenderer = new();
    private readonly AnimationManager _animationManager = new();
    private readonly CoinFlyAnimation _coinFlyAnimation = new();
    private GameJuiceEngine? _juiceEngine;
    // DispatcherTimer durch IFrameClock-Subscription ersetzt.
    private Services.Interfaces.IFrameClock? _frameClock;
    private bool _renderActive;
    private SKCanvasView? _cityCanvas;
    private ScrollViewer? _dashboardScrollViewer;
    private DateTime _lastRenderTime = DateTime.UtcNow;
    private float _renderTime; // Fortlaufende Zeit für Shader-Effekte

    // Workshop-Karten (SkiaSharp 2x4 Grid)
    private SKRect _lastWorkshopCardsBounds;

    // Hold-to-Upgrade
    private DispatcherTimer? _holdTimer;
    private WorkshopType? _holdWorkshopType;
    private int _holdUpgradeCount;

    // Tap vs. Scroll Erkennung (Workshop-Karten im ScrollViewer)
    // Tap-vs-Scroll-State (reine Gesture-Logik ausgelagert in WorkshopCardHitTester)
    private Point _workshopPressPos;
    private bool _workshopIsScrolling;
    private WorkshopDisplayModel? _workshopPressedTarget;
    private bool _workshopPressedIsUpgrade;
    private float _workshopPressSkiaX, _workshopPressSkiaY;
    private DateTime _workshopPressTime;
    private double _scrollOffsetAtPress;

    // Performance: Alle Canvases während Scroll pausieren
    private bool _isScrolling;
    private DateTime _lastScrollTime;
    private bool _hasActiveEffects; // Temporärer 30fps-Boost für Game-Juice-Effekte

    /// <summary>
    /// Gibt an ob gerade gescrollt wird. Wird von MainView abgefragt um
    /// Background- und TabBar-Rendering während Scroll zu pausieren.
    /// </summary>
    public bool IsScrolling => _isScrolling;

    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        // Parallax: ScrollViewer-Event abonnieren
        _dashboardScrollViewer = this.FindControl<ScrollViewer>("DashboardScrollViewer");
        if (_dashboardScrollViewer != null)
            _dashboardScrollViewer.ScrollChanged += OnScrollChanged;

        // Tunnel-Routing für Scroll-Erkennung bei Workshop-Karten
        // Tunnel-Events feuern auch wenn der ScrollViewer den Pointer captured
        AddHandler(PointerMovedEvent, OnTunnelPointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnTunnelPointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // v2.0.35 Feature D: 1Hz-Timer fuer Live-Countdown-Aktualisierung auf sichtbaren Orders.
        // Orders sind POCOs mit INPC — der Timer feuert pro sichtbarem Live-Auftrag die
        // Countdown-PropertyChanged-Events, damit der rote LIVE-Badge seinen Text synchron haelt.
        _liveCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveCountdownTimer.Tick += OnLiveCountdownTick;
        _liveCountdownTimer.Start();
    }

    private DispatcherTimer? _liveCountdownTimer;

    private void OnLiveCountdownTick(object? sender, EventArgs e)
    {
        if (_vm == null) return;
        // Fuer alle sichtbaren Orders (AvailableOrders + ParallelOrders) die Live-sind:
        // Countdown-Binding aktualisieren.
        for (int i = 0; i < _vm.AvailableOrders.Count; i++)
        {
            if (_vm.AvailableOrders[i].IsLive)
                _vm.AvailableOrders[i].RaiseLiveCountdownChanged();
        }
        for (int i = 0; i < _vm.ParallelOrders.Count; i++)
        {
            if (_vm.ParallelOrders[i].IsLive)
                _vm.ParallelOrders[i].RaiseLiveCountdownChanged();
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        StopCityRenderLoop();
        _holdTimer?.Stop();
        _holdTimer = null;
        if (_liveCountdownTimer != null)
        {
            _liveCountdownTimer.Stop();
            _liveCountdownTimer.Tick -= OnLiveCountdownTick;
            _liveCountdownTimer = null;
        }

        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingTextRequested;
            _vm.FloatingTextRequested -= OnFloatingTextForParticles;
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }

        _cityRenderer.Dispose();
        _coinFlyAnimation.Dispose();
        _juiceEngine?.Dispose();
    }

    /// <summary>
    /// Parallax-Effekt: Header-Background verschiebt sich leicht beim Scrollen.
    /// translateY = -scrollOffset * 0.3, maximal 20px.
    /// </summary>
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // HeaderBorder nur einmal suchen und danach aus dem Cache verwenden
        _headerBorder ??= this.FindControl<Border>("HeaderBorder");
        var scrollViewer = sender as ScrollViewer;
        if (_headerBorder == null || scrollViewer == null) return;

        _headerTranslate ??= new TranslateTransform();
        _headerBorder.RenderTransform = _headerTranslate;

        var offset = Math.Min(scrollViewer.Offset.Y * 0.3, 20);
        _headerTranslate.Y = -offset;

        // Performance: Scroll-Zustand tracken für Render-Timer-Drosselung
        _isScrolling = true;
        _lastScrollTime = DateTime.UtcNow;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes VM abmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingTextRequested;
            _vm.FloatingTextRequested -= OnFloatingTextForParticles;
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }

        // Render-Loop stoppen wenn kein neues VM kommt
        StopCityRenderLoop();

        // Neues VM abonnieren
        if (DataContext is MainViewModel vm)
        {
            _vm = vm;
            _vm.FloatingTextRequested += OnFloatingTextRequested;
            _vm.FloatingTextRequested += OnFloatingTextForParticles;
            _vm.PropertyChanged += OnVmPropertyChanged;

            // GameJuiceEngine über MainViewModel (kein Service Locator)
            _juiceEngine = vm.GameJuiceEngine;
            _juiceEngine?.SetVignette(0.25f); // Subtile Vignette für Tiefe

            // AI-Hintergrund-Service für CityRenderer initialisieren
            var assetService = GameAssetService.Current;
            if (assetService != null)
                _cityRenderer.Initialize(assetService);

            // City-Canvas finden und Render-Loop nur starten wenn Dashboard aktiv
            _cityCanvas = this.FindControl<SKCanvasView>("CityCanvas");
            if (_cityCanvas != null)
            {
                _cityCanvas.PaintSurface -= OnCityPaintSurface;
                _cityCanvas.PaintSurface += OnCityPaintSurface;

                if (_vm.IsDashboardActive)
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
                AnimateMoneyFlash(moneyText).SafeFireAndForget();
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
    /// Nutzt gecachte statische Animation (keine Allokation pro Aufruf).
    /// </summary>
    private static async Task AnimateMoneyFlash(TextBlock text)
    {
        await s_moneyFlashAnimation.RunAsync(text);
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
    /// Touch auf Workshop-Karten: Position und Ziel merken, aber NICHT sofort ausführen.
    /// Ausführung erst bei PointerReleased wenn kein Scroll erkannt wurde.
    /// </summary>
    private void OnWorkshopCardsPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm?.Workshops == null || sender is not Avalonia.Labs.Controls.SKCanvasView canvasView) return;

        // Scroll-Tracking zurücksetzen
        _workshopPressPos = e.GetPosition(this);
        _workshopPressTime = DateTime.UtcNow;
        _scrollOffsetAtPress = _dashboardScrollViewer?.Offset.Y ?? 0;
        _workshopIsScrolling = false;
        _workshopPressedTarget = null;
        _workshopPressedIsUpgrade = false;

        // Hit-Test via Helper — kapselt Koordinaten-Konvertierung + Grid-Berechnung
        var hit = WorkshopCardHitTester.HitTest(
            e.GetPosition(canvasView),
            canvasView.Bounds.Width,
            canvasView.Bounds.Height,
            _lastWorkshopCardsBounds,
            _vm.Workshops.Count);

        if (hit.WorkshopIndex < 0) return;

        var workshop = _vm.Workshops[hit.WorkshopIndex];
        _workshopPressSkiaX = hit.SkiaX;
        _workshopPressSkiaY = hit.SkiaY;

        // Upgrade-Button getroffen + Karte aufgewertet? → Hold-to-Upgrade starten
        if (hit.IsUpgradeButton && workshop.IsUnlocked && !workshop.IsMaxLevel)
        {
            _workshopPressedTarget = workshop;
            _workshopPressedIsUpgrade = true;
            StartHoldUpgrade(workshop.Type);
            return;
        }

        // Karten-Body: Nur merken, Ausfuehrung bei PointerReleased.
        _workshopPressedTarget = workshop;
        _workshopPressedIsUpgrade = false;
    }

    /// <summary>
    /// PointerReleased auf Workshop-Karten: Aktion ausführen wenn kein Scroll erkannt wurde.
    /// </summary>
    private void OnWorkshopCardsPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var target = _workshopPressedTarget;
        var isUpgrade = _workshopPressedIsUpgrade;
        var wasScrolling = _workshopIsScrolling;

        // Hold-Upgrade stoppen und Count sichern
        int holdCount = _holdUpgradeCount;
        if (_holdTimer != null)
            StopHoldUpgrade();

        // State zurücksetzen
        _workshopPressedTarget = null;
        _workshopPressedIsUpgrade = false;
        _workshopIsScrolling = false;

        // Wenn gescrollt wurde → keine Aktion
        if (wasScrolling || target == null || _vm == null) return;

        // Zusaetzliche Scroll-Erkennung: ScrollViewer-Offset hat sich veraendert
        var currentScrollOffset = _dashboardScrollViewer?.Offset.Y ?? 0;
        if (WorkshopCardHitTester.HasScrollViewerMoved(_scrollOffsetAtPress, currentScrollOffset)) return;

        // Zeitbasierte Erkennung: Tap muss schnell sein (nicht fuer Hold-Upgrade)
        if (!isUpgrade && !WorkshopCardHitTester.IsTapDuration(_workshopPressTime)) return;

        if (isUpgrade)
        {
            // Erstes Upgrade ausführen falls Hold-Timer noch nicht gefeuert hat
            if (holdCount == 0)
            {
                _vm.UpgradeWorkshopCommand.Execute(target);
            }
            // Sonst hat StopHoldUpgrade() bereits die Zusammenfassung gezeigt
        }
        else
        {
            // Karten-Body: Navigation zum Workshop / Freischalten
            _vm.SelectWorkshopCommand.Execute(target);

            // RadialBurst-Effekt am Tap-Punkt
            _juiceEngine?.RadialBurst(_workshopPressSkiaX, _workshopPressSkiaY,
                new SKColor(0xD9, 0x77, 0x06), maxRadius: 40f);
        }
    }

    /// <summary>
    /// Direkte PointerMoved auf dem Workshop-Canvas: Erkennt Scroll-Geste zusätzlich zum Tunnel.
    /// Auf Android wird der Tunnel-Event nicht immer zuverlässig weitergeleitet.
    /// </summary>
    private void OnWorkshopCardsPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_workshopPressedTarget == null || _workshopIsScrolling) return;
        if (!WorkshopCardHitTester.IsScrollDistance(_workshopPressPos, e.GetPosition(this))) return;

        _workshopIsScrolling = true;
        CancelHoldUpgradeOnScroll();
    }

    /// <summary>
    /// Tunnel-PointerMoved: Erkennt Scroll-Geste (>15px Bewegung) und bricht Hold-Upgrade ab.
    /// Feuert auch wenn der ScrollViewer den Pointer captured hat.
    /// </summary>
    private void OnTunnelPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_workshopPressedTarget == null || _workshopIsScrolling) return;
        if (!WorkshopCardHitTester.IsScrollDistance(_workshopPressPos, e.GetPosition(this))) return;

        _workshopIsScrolling = true;
        CancelHoldUpgradeOnScroll();
    }

    /// <summary>
    /// Bricht Hold-Upgrade ab wenn eine Scroll-Geste erkannt wurde.
    /// </summary>
    private void CancelHoldUpgradeOnScroll()
    {
        if (_holdTimer != null)
        {
            _holdTimer.Stop();
            _holdTimer = null;
            if (_vm != null) _vm.IsHoldingUpgrade = false;
            _holdWorkshopType = null;
            _holdUpgradeCount = 0;
        }
    }

    /// <summary>
    /// Tunnel-PointerReleased: Aufräumen wenn ScrollViewer den Pointer captured hat
    /// und das direkte PointerReleased auf dem Canvas nicht mehr feuert.
    /// </summary>
    private void OnTunnelPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Wenn wir noch ein Ziel haben und gescrollt wurde, aufräumen
        if (_workshopPressedTarget != null && _workshopIsScrolling)
        {
            CancelHoldUpgradeOnScroll();
            _workshopPressedTarget = null;
            _workshopPressedIsUpgrade = false;
            _workshopIsScrolling = false;
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
            Name = model.Name,
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
            UnlockLevel = model.UnlockLevel,
            TimeToUpgrade = model.TimeToUpgrade,
            RebirthStars = model.RebirthStars
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

        // Scroll erkannt → Hold-Upgrade sofort abbrechen
        if (_workshopIsScrolling) { StopHoldUpgrade(); return; }

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
    /// Startet den Render-Timer für die City-Skyline.
    /// 10fps Basis (AI-Hintergrund statisch, animierte WebP bei 8fps).
    /// Temporär 30fps wenn Game-Juice-Effekte aktiv (CoinFly, Confetti, ScreenShake).
    /// </summary>
    private void StartCityRenderLoop()
    {
        if (_renderActive) return;
        _frameClock ??= App.Services?.GetService(typeof(Services.Interfaces.IFrameClock)) as Services.Interfaces.IFrameClock;
        _frameClock?.Subscribe(OnCityRenderTick, Graphics.FpsProfile.DashboardIdle()); // 5/10fps je nach Quality
        _renderActive = true;
    }

    /// <summary>
    /// Render-Tick: City + Workshop-Karten invalidieren.
    /// Adaptives FPS: 10fps Basis, automatisch 30fps bei aktiven Effekten.
    /// Während Scroll werden ALLE Canvases pausiert (spart ~10 Repaints/s).
    /// </summary>
    private void OnCityRenderTick(object? sender, Services.Interfaces.FrameTickEventArgs e)
    {
        // Scroll-Ende erkennen (250ms ohne ScrollChanged - längere Ruhezeit für flüssiges Scroll)
        if (_isScrolling && (DateTime.UtcNow - _lastScrollTime).TotalMilliseconds > 250)
            _isScrolling = false;

        // Während Scroll: Alle Canvases pausieren (City + Workshop-Karten)
        // Der Header bewegt sich per RenderTransform (GPU), braucht kein Canvas-Repaint
        if (_isScrolling) return;

        // Adaptives FPS: 30fps wenn Effekte aktiv, sonst 10fps
        bool effectsActive = _coinFlyAnimation.IsActive || _animationManager.HasActiveParticles ||
                             (_juiceEngine?.HasActiveEffects == true);
        if (effectsActive != _hasActiveEffects)
        {
            _hasActiveEffects = effectsActive;
            _frameClock?.UpdateInterval(OnCityRenderTick,
                effectsActive ? Graphics.FpsProfile.DashboardActive() : Graphics.FpsProfile.DashboardIdle());
        }

        _cityCanvas?.InvalidateSurface();
        WorkshopCardsCanvas?.InvalidateSurface();
    }

    private void StopCityRenderLoop()
    {
        if (!_renderActive) return;
        _frameClock?.Unsubscribe(OnCityRenderTick);
        _renderActive = false;
    }

    /// <summary>
    /// Render-Timer nur laufen lassen wenn Dashboard sichtbar ist.
    /// Spart ~40 InvalidateSurface-Aufrufe/Sek wenn andere Tabs aktiv sind.
    /// </summary>
    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDashboardActive))
        {
            if (_vm?.IsDashboardActive == true)
            {
                _lastRenderTime = DateTime.UtcNow;
                StartCityRenderLoop();
            }
            else
            {
                StopCityRenderLoop();
            }
        }
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
            }

            // Gold-Shimmer auf Goldschrauben-Bereich (oben links)
            if (_vm.HeaderVM.GoldenScrewsDisplay != "0")
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

    // City-Tap-Handler entfernt: AI-Hintergrundbild hat keine interaktiven Gebäude-Zonen.
    // Navigation zu Workshops/Gebäuden erfolgt über Workshop-Karten und Imperium-Tab.

    // OnPaintLevelProgress → Views/Dashboard/DashboardHeader.axaml.cs
    // OnPaintChallengeProgress → Views/Dashboard/DailyChallengeSection.axaml.cs
    // OnPaintWeeklyMissionProgress → Views/Dashboard/WeeklyMissionSection.axaml.cs
    // Workshop-Level- und Milestone-ProgressBars werden direkt im WorkshopGameCardRenderer gezeichnet.
}
