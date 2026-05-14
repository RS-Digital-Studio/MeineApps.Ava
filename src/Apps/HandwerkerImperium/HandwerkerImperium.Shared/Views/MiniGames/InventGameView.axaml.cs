using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.ViewModels.MiniGames;
using HandwerkerImperium.Icons;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using SkiaSharp;

namespace HandwerkerImperium.Views.MiniGames;

public partial class InventGameView : UserControl
{
    private InventGameViewModel? _vm;
    private readonly InventGameRenderer _renderer = new();
    // Review-Pass (12.05.2026): FrameClockRenderLoop-Helper statt Inline-Subscribe.
    private readonly Helpers.FrameClockRenderLoop _renderLoop;
    private DateTime _lastRenderTime = DateTime.UtcNow;
    private SKRect _lastBounds;
    private SKCanvasView? _gameCanvas;
    private InventGameRenderer.InventPartData[] _cachedParts = [];
    private bool _disposed;

    public InventGameView()
    {
        InitializeComponent();
        _renderLoop = new Helpers.FrameClockRenderLoop(() => _gameCanvas?.InvalidateSurface(), Graphics.FpsProfile.MiniGame());
        DataContextChanged += OnDataContextChanged;

        // Render-Loop nur wenn sichtbar (View bleibt permanent im Visual Tree)
        PropertyChanged += (_, args) =>
        {
            if (args.Property == IsVisibleProperty)
            {
                if (IsVisible && _vm != null && _gameCanvas != null && !_renderLoop.IsActive)
                    StartRenderLoop();
                else if (!IsVisible && _renderLoop.IsActive)
                {
                    StopRenderLoop();
                }
            }
        };

        // AI-Hintergrund-Service initialisieren
        var assetService = GameAssetService.Current;
        if (assetService != null)
            _renderer.Initialize(assetService);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_disposed) return;
        _disposed = true;

        if (_vm != null)
        {
            _vm.GameCompleted -= OnGameCompleted;
            _vm.GameRestarted -= OnGameRestarted;
            _vm = null;
        }

        if (_gameCanvas != null)
        {
            _gameCanvas.PaintSurface -= OnPaintSurface;
            _gameCanvas.PointerPressed -= OnCanvasPointerPressed;
        }

        StopRenderLoop();
        _renderer.Dispose();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;

        // Altes ViewModel abmelden
        if (_vm != null)
        {
            _vm.GameCompleted -= OnGameCompleted;
            _vm.GameRestarted -= OnGameRestarted;
        }

        _vm = DataContext as InventGameViewModel;

        // Neues ViewModel anmelden
        if (_vm != null)
        {
            _vm.GameCompleted += OnGameCompleted;
            _vm.GameRestarted += OnGameRestarted;
        }

        // Canvas finden und Render-Loop starten
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

    private void StartRenderLoop() => _renderLoop.Start();
    private void StopRenderLoop() => _renderLoop.Stop();

    /// <summary>
    /// PaintSurface-Handler: Extrahiert InventPartData aus dem ViewModel
    /// und übergibt sie an den Renderer.
    /// </summary>
    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_vm == null) return;

        var now = DateTime.UtcNow;
        float deltaTime = Math.Min((float)(now - _lastRenderTime).TotalSeconds, 0.1f);
        _lastRenderTime = now;

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // LocalClipBounds statt info.Width/Height für korrekte DPI-Skalierung
        _lastBounds = canvas.LocalClipBounds;

        // Spaltenanzahl aus GridWidth berechnen (jedes Teil = 74px im VM)
        int cols = _vm.GridWidth > 0 ? (int)Math.Round(_vm.GridWidth / 74.0) : 3;
        if (cols <= 0) cols = 3;

        // Part-Daten aus ViewModel extrahieren (gecachtes Array, keine Allokation pro Frame)
        if (_cachedParts.Length != _vm.Parts.Count)
            _cachedParts = new InventGameRenderer.InventPartData[_vm.Parts.Count];
        for (int i = 0; i < _vm.Parts.Count; i++)
        {
            var part = _vm.Parts[i];

            // BackgroundColor-String (#RRGGBB) in uint ARGB konvertieren
            uint bgColor = ParseHexColor(part.BackgroundColor);

            // Aktives Teil: Nächstes erwartetes StepNumber und noch nicht erledigt
            bool isActive = _vm.IsPlaying && part.StepNumber == _vm.NextExpectedPart && !part.IsCompleted;

            _cachedParts[i] = new InventGameRenderer.InventPartData
            {
                Icon = part.Icon,
                DisplayNumber = part.DisplayNumber,
                BackgroundColor = bgColor,
                IsCompleted = part.IsCompleted,
                IsActive = isActive,
                HasError = part.HasError,
                StepNumber = part.StepNumber
            };
        }

        _renderer.Render(canvas, _lastBounds, _cachedParts, cols, _vm.IsMemorizing, _vm.IsPlaying,
            _vm.CompletedParts, _vm.TotalParts, deltaTime);

        // Render-Loop stoppen wenn Ergebnis angezeigt wird (statisches Bild, kein 30fps nötig)
        if (_vm is { IsResultShown: true } && _renderLoop.IsActive)
        {
            StopRenderLoop();
        }
    }

    /// <summary>
    /// Touch/Klick auf das Canvas: Berechnet welche Kachel getroffen wurde
    /// und ruft SelectPartCommand auf.
    /// </summary>
    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null || !_vm.IsPlaying || _vm.IsResultShown) return;
        if (_gameCanvas == null) return;

        var pos = e.GetPosition(_gameCanvas);

        // Skalierungsfaktor: Avalonia-Koordinaten zu SkiaSharp-Koordinaten
        if (_gameCanvas.Bounds.Width <= 0 || _gameCanvas.Bounds.Height <= 0) return;

        float scaleX = _lastBounds.Width / (float)_gameCanvas.Bounds.Width;
        float scaleY = _lastBounds.Height / (float)_gameCanvas.Bounds.Height;
        float touchX = (float)pos.X * scaleX;
        float touchY = (float)pos.Y * scaleY;

        int cols = _vm.GridWidth > 0 ? (int)Math.Round(_vm.GridWidth / 74.0) : 3;
        int partIndex = _renderer.HitTest(_lastBounds, touchX, touchY, cols, _vm.Parts.Count);

        if (partIndex >= 0 && partIndex < _vm.Parts.Count)
        {
            var part = _vm.Parts[partIndex];
            _vm.SelectPartCommand.Execute(part);
        }
    }

    /// <summary>
    /// Konvertiert einen Hex-Farbstring (#RRGGBB oder #AARRGGBB) in uint ARGB.
    /// </summary>
    private static uint ParseHexColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return 0xFF2A1A40; // Standard-Dunkelviolett

        hex = hex.TrimStart('#');

        try
        {
            if (hex.Length == 6)
            {
                // #RRGGBB -> ARGB mit vollem Alpha
                uint rgb = Convert.ToUInt32(hex, 16);
                return 0xFF000000 | rgb;
            }
            else if (hex.Length == 8)
            {
                // #AARRGGBB
                return Convert.ToUInt32(hex, 16);
            }
        }
        catch
        {
            // Fallback bei ungültigem Hex-Wert
        }

        return 0xFF2A1A40;
    }

    /// <summary>
    /// Visuelle Effekte nach Spielende abspielen (Rating-Farbe, Sterne, Border-Pulse, Belohnungen).
    /// </summary>
    private async void OnGameCompleted(object? sender, int starCount)
        => await AsyncExtensions.RunHandlerSafely(() => Dispatcher.UIThread.InvokeAsync(async () =>
        {
            // 1. Rating-Text einfärben
            var ratingText = this.FindControl<TextBlock>("RatingText");
            if (ratingText != null && _vm != null)
            {
                var ratingKey = _vm.Result.GetLocalizationKey();
                ratingText.Foreground = MiniGameEffectHelper.GetRatingBrush(ratingKey);
            }

            // 2. Sterne staggered einblenden
            var star1 = this.FindControl<GameIcon>("Star1Panel");
            var star2 = this.FindControl<GameIcon>("Star2Panel");
            var star3 = this.FindControl<GameIcon>("Star3Panel");
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
                    moneyText, _vm.RewardAmountDisplay);
            }

            if (xpText != null && _vm != null)
            {
                await MiniGameEffectHelper.AnimateRewardTextAsync(
                    xpText, $"+{_vm.XpAmount} XP");
            }
        }));

    /// <summary>
    /// Startet den Render-Loop bei Task-Wechsel neu (Multi-Task-Orders).
    /// Bei IsResultShown=true wurde der Timer fuer Performance gestoppt — beim
    /// naechsten Task muss er neu aufgezogen werden, damit Canvas animiert.
    /// </summary>
    private void OnGameRestarted(object? sender, EventArgs e)
    {
        if (_disposed) return;
        if (_gameCanvas != null && !_renderLoop.IsActive)
            StartRenderLoop();
    }
}
