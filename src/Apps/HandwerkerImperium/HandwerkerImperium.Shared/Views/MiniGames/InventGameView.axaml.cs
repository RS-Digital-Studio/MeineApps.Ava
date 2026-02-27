using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.ViewModels;
using Material.Icons.Avalonia;
using SkiaSharp;

namespace HandwerkerImperium.Views.MiniGames;

public partial class InventGameView : UserControl
{
    private InventGameViewModel? _vm;
    private readonly InventGameRenderer _renderer = new();
    private DispatcherTimer? _renderTimer;
    private DateTime _lastRenderTime = DateTime.UtcNow;
    private SKRect _lastBounds;
    private SKCanvasView? _gameCanvas;

    public InventGameView()
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

        _vm = DataContext as InventGameViewModel;

        // Neues ViewModel anmelden
        if (_vm != null)
            _vm.GameCompleted += OnGameCompleted;

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

    /// <summary>
    /// Startet den 20fps Render-Loop für Erfinder-Puzzle-Animationen.
    /// </summary>
    private void StartRenderLoop()
    {
        // NUR Timer stoppen, NICHT StopRenderLoop() aufrufen (das nullt _gameCanvas)
        _renderTimer?.Stop();
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) }; // 20 fps
        _renderTimer.Tick += (_, _) =>
        {
            _gameCanvas?.InvalidateSurface();
        };
        _renderTimer.Start();
    }

    private void StopRenderLoop()
    {
        _renderTimer?.Stop();
        _renderTimer = null;
    }

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

        // Part-Daten aus ViewModel extrahieren (kein direkter VM-Zugriff im Renderer)
        var parts = new InventGameRenderer.InventPartData[_vm.Parts.Count];
        for (int i = 0; i < _vm.Parts.Count; i++)
        {
            var part = _vm.Parts[i];

            // BackgroundColor-String (#RRGGBB) in uint ARGB konvertieren
            uint bgColor = ParseHexColor(part.BackgroundColor);

            // Aktives Teil: Nächstes erwartetes StepNumber und noch nicht erledigt
            bool isActive = _vm.IsPlaying && part.StepNumber == _vm.NextExpectedPart && !part.IsCompleted;

            parts[i] = new InventGameRenderer.InventPartData
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

        _renderer.Render(canvas, _lastBounds, parts, cols, _vm.IsMemorizing, _vm.IsPlaying,
            _vm.CompletedParts, _vm.TotalParts, deltaTime);
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
    {
        try
        {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            // 1. Rating-Text einfärben
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
}
