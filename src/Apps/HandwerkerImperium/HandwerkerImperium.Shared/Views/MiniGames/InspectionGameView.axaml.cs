using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.ViewModels;
using Material.Icons.Avalonia;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerImperium.Views.MiniGames;

public partial class InspectionGameView : UserControl
{
    private InspectionGameViewModel? _vm;
    private readonly InspectionGameRenderer _renderer = new();
    private DispatcherTimer? _renderTimer;
    private DateTime _lastRenderTime = DateTime.UtcNow;
    private SKRect _lastBounds;

    public InspectionGameView()
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

        _vm = DataContext as InspectionGameViewModel;

        // Neues ViewModel anmelden
        if (_vm != null)
            _vm.GameCompleted += OnGameCompleted;

        // Canvas-Setup und Render-Loop starten
        var canvas = this.FindControl<SKCanvasView>("GameCanvas");
        if (canvas != null)
        {
            canvas.PaintSurface -= OnPaintSurface;
            canvas.PaintSurface += OnPaintSurface;
            canvas.PointerPressed -= OnCanvasPointerPressed;
            canvas.PointerPressed += OnCanvasPointerPressed;
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
        _renderTimer.Tick += (_, _) =>
        {
            var canvas = this.FindControl<SKCanvasView>("GameCanvas");
            canvas?.InvalidateSurface();
        };
        _renderTimer.Start();
    }

    /// <summary>
    /// Stoppt den Render-Loop.
    /// </summary>
    private void StopRenderLoop()
    {
        _renderTimer?.Stop();
        _renderTimer = null;
    }

    /// <summary>
    /// PaintSurface-Handler: Zeichnet Inspektions-Grid via InspectionGameRenderer.
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
        _lastBounds = canvas.LocalClipBounds;

        // Zell-Daten aus dem ViewModel extrahieren
        var cells = BuildCellData();

        _renderer.Render(canvas, _lastBounds,
            cells, _vm.GridColumns, _vm.GridRows, _vm.IsPlaying, deltaTime);
    }

    /// <summary>
    /// Erstellt InspectionCellData-Array aus den ViewModel-Zellen.
    /// </summary>
    private InspectionCellData[] BuildCellData()
    {
        if (_vm == null || _vm.Cells.Count == 0)
            return [];

        var result = new InspectionCellData[_vm.Cells.Count];
        for (int i = 0; i < _vm.Cells.Count; i++)
        {
            var cell = _vm.Cells[i];
            result[i] = new InspectionCellData
            {
                Icon = cell.Icon,
                IsDefect = cell.HasDefect,
                IsDefectFound = cell.IsDefectFound,
                IsFalseAlarm = cell.IsFalseAlarm,
                IsInspected = cell.IsInspected,
                ContentOpacity = (float)cell.ContentOpacity,
                BackgroundColor = SKColor.Parse(cell.BackgroundColor)
            };
        }
        return result;
    }

    /// <summary>
    /// Touch/Klick auf das Canvas: HitTest durchfuehren und Zelle inspizieren.
    /// </summary>
    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null || !_vm.IsPlaying) return;

        var canvas = this.FindControl<SKCanvasView>("GameCanvas");
        if (canvas == null) return;

        // Touch-Position in Skia-Koordinaten umrechnen (DPI-Skalierung)
        var point = e.GetPosition(canvas);
        float scaleX = _lastBounds.Width / (float)canvas.Bounds.Width;
        float scaleY = _lastBounds.Height / (float)canvas.Bounds.Height;
        float touchX = (float)point.X * scaleX;
        float touchY = (float)point.Y * scaleY;

        int cellIndex = _renderer.HitTest(_lastBounds, touchX, touchY, _vm.GridColumns, _vm.GridRows);

        if (cellIndex >= 0 && cellIndex < _vm.Cells.Count)
        {
            var cell = _vm.Cells[cellIndex];
            if (_vm.InspectCellCommand.CanExecute(cell))
            {
                _vm.InspectCellCommand.Execute(cell);
            }
        }
    }

    // ====================================================================
    // SkiaSharp LinearProgress Handler
    // ====================================================================

    private void OnPaintInspectionProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        float progress = 0f;
        if (_vm != null)
            progress = (float)_vm.InspectionProgress;

        LinearProgressVisualization.Render(canvas, bounds, progress,
            new SKColor(0xD9, 0x77, 0x06), new SKColor(0x92, 0x40, 0x0E),
            showText: false, glowEnabled: true);
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
            System.Diagnostics.Debug.WriteLine($"Fehler in OnGameCompleted: {ex.Message}");
        }
    }
}
