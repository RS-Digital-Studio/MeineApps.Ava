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

public partial class DesignPuzzleGameView : UserControl
{
    private DesignPuzzleGameViewModel? _vm;
    private readonly DesignPuzzleRenderer _renderer = new();
    private DispatcherTimer? _renderTimer;
    private SKCanvasView? _gameCanvas;
    private DateTime _lastRenderTime = DateTime.UtcNow;
    private SKRect _lastBounds;

    public DesignPuzzleGameView()
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

        _vm = DataContext as DesignPuzzleGameViewModel;

        // Neues ViewModel anmelden
        if (_vm != null)
            _vm.GameCompleted += OnGameCompleted;

        // Canvas-Setup und Render-Loop starten
        _gameCanvas = this.FindControl<SKCanvasView>("FloorPlanCanvas");
        if (_gameCanvas != null)
        {
            _gameCanvas.PaintSurface -= OnPaintSurface;
            _gameCanvas.PaintSurface += OnPaintSurface;

            // Touch/Click auf dem Canvas abfangen
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
    /// PaintSurface-Handler: Zeichnet den Grundriss via DesignPuzzleRenderer.
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
        _lastBounds = bounds;

        // Slot-Daten vom ViewModel konvertieren
        var slots = BuildSlotData();
        if (slots.Length == 0) return;

        // Grid-Dimensionen berechnen
        GetGridDimensions(out int cols, out int rows);

        // Korrekt gefuellte Slots zaehlen fuer Completion-Erkennung
        int filledCorrectCount = 0;
        foreach (var s in slots)
        {
            if (s.IsCorrect) filledCorrectCount++;
        }

        _renderer.Render(canvas, bounds,
            slots, cols, rows, filledCorrectCount, _vm.TotalSlots, deltaTime);
    }

    /// <summary>
    /// Touch/Klick auf dem Canvas: HitTest -> PlaceRoomCommand aufrufen.
    /// </summary>
    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null || !_vm.IsPlaying || _vm.IsResultShown || _vm.SelectedRoom == null || _gameCanvas == null) return;

        // Touch-Position relativ zum Canvas
        var point = e.GetPosition(_gameCanvas);

        // Skalierung: Avalonia-Koordinaten -> SkiaSharp-Koordinaten (mit _lastBounds vom Render)
        if (_gameCanvas.Bounds.Width <= 0 || _gameCanvas.Bounds.Height <= 0) return;

        float scaleX = _lastBounds.Width / (float)_gameCanvas.Bounds.Width;
        float scaleY = _lastBounds.Height / (float)_gameCanvas.Bounds.Height;

        float touchX = (float)point.X * scaleX;
        float touchY = (float)point.Y * scaleY;

        // Grid-Dimensionen
        GetGridDimensions(out int cols, out int rows);

        // HitTest mit gleichen Bounds wie im Renderer (LocalClipBounds)
        int slotIndex = _renderer.HitTest(
            _lastBounds,
            touchX, touchY, cols, rows, _vm.Slots.Count);

        if (slotIndex >= 0 && slotIndex < _vm.Slots.Count)
        {
            var slot = _vm.Slots[slotIndex];
            if (!slot.IsFilled)
            {
                _vm.PlaceRoomCommand.Execute(slot);
            }
        }
    }

    /// <summary>
    /// Konvertiert die ViewModel-Slots in Renderer-Daten.
    /// </summary>
    private DesignPuzzleRenderer.RoomSlotData[] BuildSlotData()
    {
        if (_vm == null) return [];

        var vmSlots = _vm.Slots;
        var data = new DesignPuzzleRenderer.RoomSlotData[vmSlots.Count];

        for (int i = 0; i < vmSlots.Count; i++)
        {
            var s = vmSlots[i];
            data[i] = new DesignPuzzleRenderer.RoomSlotData
            {
                HintColor = ParseHexColor(s.HintColor),
                DisplayLabel = s.DisplayLabel,
                FilledColor = ParseHexColor(s.FilledColor),
                BackgroundColor = ParseHexColor(s.BackgroundColor),
                BorderColor = ParseHexColor(s.BorderColor),
                IsFilled = s.IsFilled,
                IsCorrect = s.IsCorrect,
                HasError = s.HasError
            };
        }

        return data;
    }

    /// <summary>
    /// Berechnet Spalten und Zeilen basierend auf Schwierigkeit.
    /// Easy=4 Slots (2x2), Medium=6 (3x2), Hard=8 (4x2), Expert=10 (5x2).
    /// </summary>
    private void GetGridDimensions(out int cols, out int rows)
    {
        if (_vm == null) { cols = 2; rows = 2; return; }

        switch (_vm.Difficulty)
        {
            case OrderDifficulty.Easy:
                cols = 2; rows = 2;
                break;
            case OrderDifficulty.Medium:
                cols = 3; rows = 2;
                break;
            case OrderDifficulty.Hard:
                cols = 4; rows = 2;
                break;
            case OrderDifficulty.Expert:
                cols = 5; rows = 2;
                break;
            default:
                cols = 3; rows = 2;
                break;
        }
    }

    /// <summary>
    /// Hex-Farbstring (#RRGGBB oder #AARRGGBB) in ARGB uint konvertieren.
    /// </summary>
    private static uint ParseHexColor(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return 0xFF2A2A2A; // Standard-Dunkelgrau

        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                // #RRGGBB -> AARRGGBB
                return 0xFF000000 | Convert.ToUInt32(hex, 16);
            }
            if (hex.Length == 8)
            {
                // #AARRGGBB
                return Convert.ToUInt32(hex, 16);
            }
        }
        catch
        {
            // Fallback bei Parse-Fehler
        }

        return 0xFF2A2A2A;
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
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"Fehler in OnGameCompleted: {ex.Message}");
#endif
        }
    }
}
