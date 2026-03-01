using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.ViewModels;
using SkiaSharp;

namespace HandwerkerImperium.Views.MiniGames;

public partial class PipePuzzleView : UserControl
{
    private PipePuzzleViewModel? _vm;
    private readonly PipePuzzleRenderer _renderer = new();
    private DispatcherTimer? _renderTimer;
    private DateTime _lastRenderTime = DateTime.UtcNow;
    private SKRect _lastBounds;
    private SKCanvasView? _puzzleCanvas;

    public PipePuzzleView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => StopRenderLoop();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes ViewModel abmelden
        if (_vm != null)
            _vm.GameCompleted -= OnGameCompleted;

        _vm = DataContext as PipePuzzleViewModel;

        // Neues ViewModel anmelden
        if (_vm != null)
            _vm.GameCompleted += OnGameCompleted;

        // Canvas finden und Render-Loop starten
        _puzzleCanvas = this.FindControl<SKCanvasView>("PuzzleCanvas");
        if (_puzzleCanvas != null)
        {
            _puzzleCanvas.PaintSurface -= OnPaintSurface;
            _puzzleCanvas.PaintSurface += OnPaintSurface;
            _puzzleCanvas.PointerPressed -= OnCanvasPointerPressed;
            _puzzleCanvas.PointerPressed += OnCanvasPointerPressed;
            StartRenderLoop();
        }
        else
        {
            StopRenderLoop();
        }
    }

    /// <summary>
    /// Startet den 20fps Render-Loop fuer Wasser-Animationen.
    /// </summary>
    private void StartRenderLoop()
    {
        StopRenderLoop();
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) }; // 20 fps
        _renderTimer.Tick += (_, _) =>
        {
            _puzzleCanvas?.InvalidateSurface();
        };
        _renderTimer.Start();
    }

    private void StopRenderLoop()
    {
        _renderTimer?.Stop();
        _renderTimer = null;
    }

    /// <summary>
    /// PaintSurface-Handler: Zeichnet das Puzzle-Grid via SkiaSharp-Renderer.
    /// </summary>
    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_vm == null) return;

        var now = DateTime.UtcNow;
        float deltaTime = Math.Min((float)(now - _lastRenderTime).TotalSeconds, 0.1f);
        _lastRenderTime = now;

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // LocalClipBounds statt info.Width/Height fuer korrekte DPI-Skalierung
        _lastBounds = canvas.LocalClipBounds;

        // Tile-Daten aus ViewModel extrahieren
        var tiles = new PipeTileData[_vm.Tiles.Count];
        for (int i = 0; i < _vm.Tiles.Count; i++)
        {
            var t = _vm.Tiles[i];
            tiles[i] = new PipeTileData
            {
                PipeType = (int)t.PipeType,
                Rotation = t.Rotation,
                IsSource = t.IsSource,
                IsDrain = t.IsDrain,
                IsLocked = t.IsLocked,
                IsConnected = t.IsConnected,
                ConnectionDistance = t.ConnectionDistance
            };
        }

        _renderer.Render(canvas, _lastBounds, tiles, _vm.GridCols, _vm.GridRows,
            _vm.IsPuzzleSolved, _vm.MaxConnectionDistance, deltaTime);
    }

    /// <summary>
    /// Touch/Klick auf das Canvas: Berechnet welche Kachel getroffen wurde
    /// und ruft RotateTileCommand auf.
    /// </summary>
    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null || !_vm.IsPlaying || _vm.IsResultShown) return;
        if (_puzzleCanvas == null) return;

        var pos = e.GetPosition(_puzzleCanvas);

        // Skalierungsfaktor: Avalonia-Koordinaten zu SkiaSharp-Koordinaten
        if (_puzzleCanvas.Bounds.Width <= 0 || _puzzleCanvas.Bounds.Height <= 0) return;

        float scaleX = _lastBounds.Width / (float)_puzzleCanvas.Bounds.Width;
        float scaleY = _lastBounds.Height / (float)_puzzleCanvas.Bounds.Height;
        float touchX = (float)pos.X * scaleX;
        float touchY = (float)pos.Y * scaleY;

        int tileIndex = _renderer.HitTest(_lastBounds, touchX, touchY, _vm.GridCols, _vm.GridRows);
        if (tileIndex >= 0 && tileIndex < _vm.Tiles.Count)
        {
            var tile = _vm.Tiles[tileIndex];
            _vm.RotateTileCommand.Execute(tile);
        }
    }

    /// <summary>
    /// Visuelle Effekte nach Spielende abspielen (Rating-Farbe, Sterne, Border-Pulse).
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
            var star1 = this.FindControl<Control>("Star1Panel");
            var star2 = this.FindControl<Control>("Star2Panel");
            var star3 = this.FindControl<Control>("Star3Panel");
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
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"Fehler in OnGameCompleted: {ex.Message}");
#endif
        }
    }
}
