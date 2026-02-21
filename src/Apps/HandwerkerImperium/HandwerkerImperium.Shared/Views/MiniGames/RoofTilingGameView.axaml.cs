using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.ViewModels;
using SkiaSharp;

namespace HandwerkerImperium.Views.MiniGames;

public partial class RoofTilingGameView : UserControl
{
    private RoofTilingGameViewModel? _vm;
    private readonly RoofTilingRenderer _renderer = new();
    private DispatcherTimer? _renderTimer;
    private SKCanvasView? _gameCanvas;
    private DateTime _lastRenderTime = DateTime.UtcNow;

    public RoofTilingGameView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => StopRenderLoop();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes ViewModel abmelden
        if (_vm != null)
        {
            _vm.GameCompleted -= OnGameCompleted;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = DataContext as RoofTilingGameViewModel;

        // Neues ViewModel anmelden
        if (_vm != null)
        {
            _vm.GameCompleted += OnGameCompleted;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }

        // Canvas-Setup: PaintSurface + Touch-Events
        _gameCanvas = this.FindControl<SKCanvasView>("GameCanvas");
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
        StopRenderLoop();
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
    /// PaintSurface-Handler: Konvertiert ViewModel-Daten in RoofTileData und rendert.
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

        // ViewModel-Tiles in RoofTileData konvertieren
        var tiles = _vm.Tiles;
        var tileData = new RoofTileData[tiles.Count];

        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            tileData[i] = new RoofTileData
            {
                TargetColor = ParseColorToArgb(t.CorrectColor),
                DisplayColor = ParseColorToArgb(t.DisplayColor),
                IsPlaced = t.IsPlaced,
                IsHint = t.IsHint,
                HasError = t.HasError,
                IsEmpty = !t.IsPlaced && !t.IsHint && string.IsNullOrEmpty(t.CurrentColor)
            };
        }

        // LocalClipBounds statt info.Width/Height fuer korrekte DPI-Skalierung
        var bounds = canvas.LocalClipBounds;
        _lastCanvasWidth = bounds.Width;
        _lastCanvasHeight = bounds.Height;

        _renderer.Render(canvas, bounds, tileData, _vm.GridColumns, _vm.GridRows, deltaTime);
    }

    /// <summary>
    /// Touch/Klick auf das SkiaSharp-Canvas: HitTest ermittelt Ziegel-Index,
    /// PlaceTileCommand wird mit dem entsprechenden RoofTile aufgerufen.
    /// </summary>
    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null || !_vm.IsPlaying || _vm.IsResultShown) return;

        var canvas = sender as SKCanvasView;
        if (canvas == null) return;

        // Touch-Position in Canvas-Koordinaten holen
        var point = e.GetPosition(canvas);

        // DPI-Skalierung: Render-Bounds vs. Control-Bounds
        float scaleX = (float)(canvas.Bounds.Width > 0
            ? _lastCanvasWidth / canvas.Bounds.Width
            : 1.0);
        float scaleY = (float)(canvas.Bounds.Height > 0
            ? _lastCanvasHeight / canvas.Bounds.Height
            : 1.0);

        float touchX = (float)point.X * scaleX;
        float touchY = (float)point.Y * scaleY;

        var bounds = SKRect.Create(0, 0, _lastCanvasWidth, _lastCanvasHeight);
        int tileIndex = _renderer.HitTest(bounds, touchX, touchY, _vm.GridColumns, _vm.GridRows);

        if (tileIndex >= 0 && tileIndex < _vm.Tiles.Count)
        {
            var tile = _vm.Tiles[tileIndex];
            if (_vm.PlaceTileCommand.CanExecute(tile))
            {
                _vm.PlaceTileCommand.Execute(tile);
            }
        }
    }

    // Canvas-Groesse aus letztem PaintSurface fuer DPI-Skalierung
    private float _lastCanvasWidth = 1;
    private float _lastCanvasHeight = 1;

    /// <summary>
    /// Hex-String (#RRGGBB) in uint ARGB konvertieren fuer SkiaSharp.
    /// </summary>
    private static uint ParseColorToArgb(string hexColor)
    {
        if (string.IsNullOrEmpty(hexColor))
            return 0xFF3A3A3A; // EmptySlotColor als Fallback

        try
        {
            var skColor = SKColor.Parse(hexColor);
            return (uint)skColor;
        }
        catch
        {
            return 0xFF3A3A3A;
        }
    }

    /// <summary>
    /// Reagiert auf SelectColorHint-Änderung → Farbpalette pulsieren.
    /// </summary>
    private async void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RoofTilingGameViewModel.SelectColorHint)) return;
        if (_vm?.SelectColorHint != true) return;

        var border = this.FindControl<Border>("ColorPaletteBorder");
        if (border == null) return;

        // Schnelles Opacity-Pulsieren (2x blinken)
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            for (int i = 0; i < 2; i++)
            {
                border.Opacity = 0.4;
                await Task.Delay(150);
                border.Opacity = 1.0;
                await Task.Delay(150);
            }
        });
    }

    /// <summary>
    /// Visuelle Effekte nach Spielende abspielen (Rating-Farbe, Sterne, Border-Pulse).
    /// </summary>
    private async void OnGameCompleted(object? sender, int rating)
    {
        try
        {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            // 1. Rating-Text Farbe setzen
            var ratingText = this.FindControl<TextBlock>("RatingText");
            if (ratingText != null)
            {
                string ratingStr = rating switch { 3 => "Perfect", 2 => "Good", 1 => "Ok", _ => "Miss" };
                ratingText.Foreground = MiniGameEffectHelper.GetRatingBrush(ratingStr);
            }

            // 2. Sterne staggered anzeigen
            var star1 = this.FindControl<Control>("Star1Panel");
            var star2 = this.FindControl<Control>("Star2Panel");
            var star3 = this.FindControl<Control>("Star3Panel");
            if (star1 != null && star2 != null && star3 != null)
            {
                await MiniGameEffectHelper.ShowStarsStaggeredAsync(star1, star2, star3, rating);
            }

            // 3. Result-Border pulsieren
            var resultBorder = this.FindControl<Border>("ResultBorder");
            if (resultBorder != null)
            {
                await MiniGameEffectHelper.PulseResultBorderAsync(resultBorder, rating);
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
