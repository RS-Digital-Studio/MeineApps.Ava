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

public partial class WiringGameView : UserControl
{
    private WiringGameViewModel? _vm;
    private readonly WiringGameRenderer _renderer = new();
    private DispatcherTimer? _renderTimer;
    private SKCanvasView? _gameCanvas;
    private DateTime _lastRenderTime = DateTime.UtcNow;
    private SKRect _lastBounds;

    public WiringGameView()
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

        _vm = DataContext as WiringGameViewModel;

        // Neues ViewModel anmelden
        if (_vm != null)
            _vm.GameCompleted += OnGameCompleted;

        // Canvas-Setup: PaintSurface + Touch-Events + Render-Loop
        _gameCanvas = this.FindControl<SKCanvasView>("WiringCanvas");
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
    /// PaintSurface-Handler: Extrahiert Kabel-Daten aus dem ViewModel
    /// und delegiert das Zeichnen an den WiringGameRenderer.
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

        // Kabel-Daten aus ViewModel extrahieren (Wire -> WireRenderData)
        var leftData = _vm.LeftWires.Select(w => new WireRenderData
        {
            ColorIndex = (int)w.WireColor,
            IsSelected = w.IsSelected,
            IsConnected = w.IsConnected,
            HasError = w.HasError
        }).ToArray();

        var rightData = _vm.RightWires.Select(w => new WireRenderData
        {
            ColorIndex = (int)w.WireColor,
            IsSelected = w.IsSelected,
            IsConnected = w.IsConnected,
            HasError = w.HasError
        }).ToArray();

        // Selektiertes linkes Kabel als Index ermitteln
        int? selectedLeft = null;
        if (_vm.SelectedLeftWire != null)
        {
            var idx = _vm.LeftWires.IndexOf(_vm.SelectedLeftWire);
            if (idx >= 0) selectedLeft = idx;
        }

        _renderer.Render(canvas, _lastBounds, leftData, rightData, selectedLeft, deltaTime);
    }

    /// <summary>
    /// Touch-Handler: Berechnet welches Kabel getroffen wurde und
    /// delegiert an die ViewModel-Commands (SelectLeftWire/SelectRightWire).
    /// </summary>
    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null || !_vm.IsPlaying || _vm.IsResultShown || _gameCanvas == null) return;

        // Touch-Position in SkiaSharp-Koordinaten umrechnen
        var pos = e.GetPosition(_gameCanvas);
        float scaleX = _lastBounds.Width / (float)_gameCanvas.Bounds.Width;
        float scaleY = _lastBounds.Height / (float)_gameCanvas.Bounds.Height;
        float touchX = (float)pos.X * scaleX;
        float touchY = (float)pos.Y * scaleY;

        var (isLeft, index) = _renderer.HitTest(_lastBounds, touchX, touchY, _vm.WireCount);

        if (index >= 0)
        {
            if (isLeft && index < _vm.LeftWires.Count)
            {
                _vm.SelectLeftWireCommand.Execute(_vm.LeftWires[index]);
            }
            else if (!isLeft && index < _vm.RightWires.Count)
            {
                _vm.SelectRightWireCommand.Execute(_vm.RightWires[index]);
            }
        }
    }

    /// <summary>
    /// Result-Effekte nach Spielende: Rating-Farbe, Sterne staggered, Border-Pulse.
    /// Gleiche MiniGameEffectHelper-Pattern wie SawingGameView.
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
            System.Diagnostics.Debug.WriteLine($"Fehler in OnGameCompleted: {ex.Message}");
        }
    }
}
