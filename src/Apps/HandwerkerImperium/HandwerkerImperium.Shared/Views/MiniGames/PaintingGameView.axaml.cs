using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.ViewModels;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerImperium.Views.MiniGames;

public partial class PaintingGameView : UserControl
{
    private PaintingGameViewModel? _vm;
    private readonly PaintingGameRenderer _renderer = new();
    private DispatcherTimer? _renderTimer;
    private SKCanvasView? _gameCanvas;
    private DateTime _lastRenderTime = DateTime.UtcNow;
    private SKRect _lastBounds;

    public PaintingGameView()
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
            _vm.ComboIncreased -= OnComboIncreased;
            _vm.GameCompleted -= OnGameCompleted;
        }

        _vm = DataContext as PaintingGameViewModel;

        // Neue Events anhaengen
        if (_vm != null)
        {
            _vm.ComboIncreased += OnComboIncreased;
            _vm.GameCompleted += OnGameCompleted;
        }

        // Canvas-Setup: PaintSurface + Touch-Handler + Render-Loop starten
        _gameCanvas = this.FindControl<SKCanvasView>("PaintCanvas");
        if (_gameCanvas != null)
        {
            _gameCanvas.PaintSurface -= OnPaintSurface;
            _gameCanvas.PaintSurface += OnPaintSurface;
            _gameCanvas.PointerPressed -= OnCanvasPointerPressed;
            _gameCanvas.PointerPressed += OnCanvasPointerPressed;
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
    /// PaintSurface-Handler: Zeichnet Spielfeld via PaintingGameRenderer.
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

        // LocalClipBounds statt e.Info.Width/Height fuer korrekte DPI-Skalierung
        _lastBounds = canvas.LocalClipBounds;

        // Zell-Daten aus ViewModel extrahieren
        var cells = _vm.Cells.Select(c => new PaintCellData
        {
            IsTarget = c.IsTarget,
            IsPainted = c.IsPainted,
            IsCorrect = c.IsPaintedCorrectly,
            HasError = c.HasError,
            PaintedAge = c.IsPainted ? (float)(now - c.PaintedAt).TotalSeconds : 0f
        }).ToArray();

        // Ausgewaehlte Farbe parsen (Fallback: CraftOrange)
        var paintColor = SKColor.Parse(_vm.SelectedColor ?? "#E8A00E");

        _renderer.Render(canvas, _lastBounds, cells, _vm.GridSize, paintColor, _vm.IsPlaying, deltaTime);
    }

    /// <summary>
    /// Touch-Handler: Berechnet getroffene Zelle und fuehrt PaintCellCommand aus.
    /// DPI-Skalierung wird beruecksichtigt (Render-Bounds vs. Control-Bounds).
    /// </summary>
    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null || !_vm.IsPlaying || _vm.IsResultShown || _gameCanvas == null) return;

        var pos = e.GetPosition(_gameCanvas);

        // DPI-Skalierung: Render-Bounds (Pixel) / Control-Bounds (logische Einheiten)
        float scaleX = _lastBounds.Width / (float)_gameCanvas.Bounds.Width;
        float scaleY = _lastBounds.Height / (float)_gameCanvas.Bounds.Height;

        int cellIndex = _renderer.HitTest(_lastBounds, (float)pos.X * scaleX, (float)pos.Y * scaleY, _vm.GridSize);
        if (cellIndex >= 0 && cellIndex < _vm.Cells.Count)
        {
            var cell = _vm.Cells[cellIndex];

            // Farbspritzer-Effekt wenn Zielzelle getroffen (bevor Command ausgefuehrt wird)
            if (cell.IsTarget && !cell.IsPainted)
            {
                var paintColor = SKColor.Parse(_vm.SelectedColor ?? "#E8A00E");
                _renderer.AddSplatter(_lastBounds, cellIndex, _vm.GridSize, paintColor);
            }

            _vm.PaintCellCommand.Execute(cell);
        }
    }

    // ====================================================================
    // SkiaSharp LinearProgress Handler
    // ====================================================================

    private void OnPaintPaintProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        float progress = 0f;
        if (_vm != null)
            progress = (float)_vm.PaintProgress;

        LinearProgressVisualization.Render(canvas, bounds, progress,
            new SKColor(0xF5, 0x9E, 0x0B), new SKColor(0xD9, 0x77, 0x06),
            showText: false, glowEnabled: true);
    }

    /// <summary>
    /// Combo-Badge Scale-Animation bei Combo >= 3.
    /// </summary>
    private async void OnComboIncreased(object? sender, EventArgs e)
    {
        var badge = this.FindControl<Border>("ComboBadge");
        if (badge != null)
            await AnimationHelper.ScaleUpDownAsync(badge, 1.0, 1.3, TimeSpan.FromMilliseconds(250));
    }

    /// <summary>
    /// Visuelle Effekte nach Spielende abspielen (Rating-Farbe, Sterne, Border-Pulse, Belohnungs-Texte).
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
            var star1 = this.FindControl<Panel>("Star1Panel");
            var star2 = this.FindControl<Panel>("Star2Panel");
            var star3 = this.FindControl<Panel>("Star3Panel");
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
}
