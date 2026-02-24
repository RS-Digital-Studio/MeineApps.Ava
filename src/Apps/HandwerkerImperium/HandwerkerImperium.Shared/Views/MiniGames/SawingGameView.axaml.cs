using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.ViewModels;
using Material.Icons.Avalonia;
using SkiaSharp;

namespace HandwerkerImperium.Views.MiniGames;

public partial class SawingGameView : UserControl
{
    private SawingGameViewModel? _vm;
    private readonly SawingGameRenderer _renderer = new();
    private DispatcherTimer? _renderTimer;
    private SKCanvasView? _gameCanvas;
    private DateTime _lastRenderTime = DateTime.UtcNow;

    public SawingGameView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => StopRenderLoop();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Alte Events abhaengen
        if (_vm != null)
        {
            _vm.GameStarted -= OnGameStarted;
            _vm.GameCompleted -= OnGameCompleted;
            _vm.ZoneHit -= OnZoneHit;
        }

        _vm = DataContext as SawingGameViewModel;

        // Neue Events anhaengen
        if (_vm != null)
        {
            _vm.GameStarted += OnGameStarted;
            _vm.GameCompleted += OnGameCompleted;
            _vm.ZoneHit += OnZoneHit;
        }

        // Canvas-Setup und Render-Loop starten
        _gameCanvas = this.FindControl<SKCanvasView>("GameCanvas");
        if (_gameCanvas != null)
        {
            _gameCanvas.PaintSurface -= OnPaintSurface;
            _gameCanvas.PaintSurface += OnPaintSurface;
            StartRenderLoop();
        }
        else
        {
            StopRenderLoop();
        }
    }

    /// <summary>
    /// Startet den 20fps Render-Loop fuer die SkiaSharp-Darstellung.
    /// </summary>
    private void StartRenderLoop()
    {
        _renderTimer?.Stop();
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) }; // 20fps
        _renderTimer.Tick += (_, _) => _gameCanvas?.InvalidateSurface();
        _renderTimer.Start();
    }

    /// <summary>
    /// Stoppt den Render-Loop und gibt Canvas-Referenz frei.
    /// </summary>
    private void StopRenderLoop()
    {
        _renderTimer?.Stop();
        _renderTimer = null;
        _gameCanvas = null;
    }

    /// <summary>
    /// PaintSurface-Handler: Zeichnet Spielfeld via SawingGameRenderer.
    /// </summary>
    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_vm == null) return;

        var now = DateTime.UtcNow;
        float deltaTime = (float)(now - _lastRenderTime).TotalSeconds;
        _lastRenderTime = now;
        deltaTime = Math.Min(deltaTime, 0.1f); // Cap bei 100ms

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // LocalClipBounds statt info.Width/Height fuer korrekte DPI-Skalierung
        var bounds = canvas.LocalClipBounds;

        _renderer.Render(canvas, bounds,
            _vm.MarkerPosition,
            _vm.PerfectZoneStart, _vm.PerfectZoneWidth,
            _vm.GoodZoneStart, _vm.GoodZoneWidth,
            _vm.OkZoneStart, _vm.OkZoneWidth,
            _vm.IsPlaying, _vm.IsResultShown,
            deltaTime);
    }

    /// <summary>
    /// Countdown-Animation beim Spielstart.
    /// Die ViewModel-Logik setzt IsCountdownActive/CountdownText,
    /// wir pulsieren den Text zusaetzlich fuer visuellen Effekt.
    /// </summary>
    private async void OnGameStarted(object? sender, EventArgs e)
    {
        var countdownText = this.FindControl<TextBlock>("CountdownTextBlock");
        if (countdownText == null) return;

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await AnimationHelper.PulseAsync(countdownText, TimeSpan.FromMilliseconds(200));
        });
    }

    /// <summary>
    /// Result-Effekte: Rating-Farbe, Sterne staggered, Border-Pulse, Belohnungs-Texte.
    /// </summary>
    private async void OnGameCompleted(object? sender, int starCount)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // 1. Rating-Text einfaerben
                var ratingText = this.FindControl<TextBlock>("RatingText");
                if (ratingText != null && _vm != null)
                {
                    var ratingKey = _vm.Result.GetLocalizationKey();
                    ratingText.Foreground = MiniGameEffectHelper.GetRatingBrush(ratingKey);
                }

                // 2. Sterne staggered einblenden
                var star1 = this.FindControl<MaterialIcon>("Star1Panel");
                var star2 = this.FindControl<MaterialIcon>("Star2Panel");
                var star3 = this.FindControl<MaterialIcon>("Star3Panel");
                if (star1 != null && star2 != null && star3 != null)
                {
                    await MiniGameEffectHelper.ShowStarsStaggeredAsync(star1, star2, star3, starCount);
                }

                // 3. Result-Border pulsen
                var resultBorder = this.FindControl<Border>("ResultBorder");
                if (resultBorder != null)
                {
                    await MiniGameEffectHelper.PulseResultBorderAsync(resultBorder, starCount);
                }

                // 4. Belohnungs-Texte animiert einblenden
                var moneyText = this.FindControl<TextBlock>("RewardMoneyText");
                var xpText = this.FindControl<TextBlock>("RewardXpText");

                if (moneyText != null && _vm != null)
                {
                    await MiniGameEffectHelper.AnimateRewardTextAsync(
                        moneyText, $"+{_vm.RewardAmount:N0} \u20ac");
                }

                if (xpText != null && _vm != null)
                {
                    await MiniGameEffectHelper.AnimateRewardTextAsync(
                        xpText, $"+{_vm.XpAmount} XP");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in OnGameCompleted: {ex.Message}");
        }
    }

    /// <summary>
    /// Zone-Hit Feedback. Da das Spielfeld jetzt SkiaSharp ist,
    /// ist ein XAML-Zone-Flash nicht mehr noetig. Der visuelle Effekt
    /// kommt vom SkiaSharp-Renderer selbst (Marker-Position + Zonen-Glow).
    /// </summary>
    private void OnZoneHit(object? sender, string zoneName)
    {
        // Visuelles Feedback laeuft ueber den SkiaSharp-Renderer
        // (Perfect-Zone Glow-Puls, Saegemehl-Partikel etc.)
    }
}
