using Avalonia.Controls;
using Avalonia.Threading;
using BomberBlast.ViewModels;
using SkiaSharp;

namespace BomberBlast.Views;

/// <summary>
/// Code-behind für LuckySpinView: SkiaSharp-Rad-Rendering + Spin-Animation per DispatcherTimer
/// </summary>
public partial class LuckySpinView : UserControl
{
    private LuckySpinViewModel? _vm;
    private DispatcherTimer? _animTimer;
    private DateTime _lastFrame;

    // Gepoolte Paints (keine per-Frame Allokationen)
    private readonly SKPaint _segmentPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Color = SKColors.White };
    private readonly SKPaint _outlinePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = SKColors.White };
    private readonly SKPaint _centerPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _pointerPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColor.Parse("#FFD700") };
    private readonly SKPaint _jackpotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };

    // Segment-Farben (9 Segmente, abwechselnd warm/kühl)
    private static readonly SKColor[] SegmentColors =
    [
        SKColor.Parse("#E53935"), // 50 Coins - Rot
        SKColor.Parse("#1E88E5"), // 100 Coins - Blau
        SKColor.Parse("#43A047"), // 250 Coins - Grün
        SKColor.Parse("#FB8C00"), // 500 Coins - Orange
        SKColor.Parse("#8E24AA"), // 100 Coins - Lila
        SKColor.Parse("#00ACC1"), // 750 Coins - Cyan
        SKColor.Parse("#3949AB"), // 250 Coins - Indigo
        SKColor.Parse("#00BCD4"), // 5 Gems - Cyan/Teal
        SKColor.Parse("#FFD700"), // 1500 Coins - Gold (Jackpot)
    ];

    public LuckySpinView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        WheelCanvas.PaintSurface += OnPaintSurface;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _vm = DataContext as LuckySpinViewModel;
        if (_vm != null)
        {
            _vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(LuckySpinViewModel.IsSpinning) && _vm.IsSpinning)
                    StartAnimationTimer();
                if (args.PropertyName == nameof(LuckySpinViewModel.CurrentAngle))
                    WheelCanvas.InvalidateSurface();
            };
        }
    }

    protected override void OnLoaded(global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        WheelCanvas.InvalidateSurface();
    }

    private void StartAnimationTimer()
    {
        _lastFrame = DateTime.UtcNow;
        if (_animTimer == null)
        {
            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _animTimer.Tick += OnAnimTick;
        }
        _animTimer.Start();
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        if (_vm == null) return;

        var now = DateTime.UtcNow;
        var dt = (float)(now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        dt = Math.Min(dt, 0.05f); // Cap bei 50ms

        if (!_vm.UpdateAnimation(dt))
        {
            _animTimer?.Stop();
            WheelCanvas.InvalidateSurface();
        }
    }

    private void OnPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        var cx = bounds.Width / 2f;
        var cy = bounds.Height / 2f;
        var radius = Math.Min(cx, cy) * 0.85f;

        if (radius < 20) return;

        var angle = _vm?.CurrentAngle ?? 0f;

        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.RotateDegrees(angle);

        DrawWheel(canvas, radius);

        canvas.Restore();

        // Zeiger oben (unbewegt)
        DrawPointer(canvas, cx, cy - radius - 4, radius * 0.12f);

        // Äußerer Glow-Ring
        DrawOuterGlow(canvas, cx, cy, radius);
    }

    private void DrawWheel(SKCanvas canvas, float radius)
    {
        var rewards = _vm?.Rewards;
        var segmentCount = rewards?.Count ?? 9;
        var segmentAngle = 360f / segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            var startAngle = i * segmentAngle - 90f; // -90 damit Segment 0 oben ist

            // Segment zeichnen
            _segmentPaint.Color = i < SegmentColors.Length ? SegmentColors[i] : SKColors.Gray;
            using var path = new SKPath();
            path.MoveTo(0, 0);
            var rect = new SKRect(-radius, -radius, radius, radius);
            path.ArcTo(rect, startAngle, segmentAngle, false);
            path.Close();
            canvas.DrawPath(path, _segmentPaint);

            // Jackpot-Shimmer (letztes Segment = Index 8)
            if (rewards != null && i < rewards.Count && rewards[i].IsJackpot)
            {
                var shimmer = (float)(Math.Sin(DateTime.UtcNow.Ticks / 5_000_000.0) * 0.3 + 0.7);
                _jackpotPaint.Color = new SKColor(255, 255, 255, (byte)(80 * shimmer));
                canvas.DrawPath(path, _jackpotPaint);
            }

            // Segment-Trennlinien
            _outlinePaint.StrokeWidth = 2;
            _outlinePaint.Color = new SKColor(255, 255, 255, 80);
            var lineAngleRad = (startAngle) * MathF.PI / 180f;
            canvas.DrawLine(0, 0,
                MathF.Cos(lineAngleRad) * radius,
                MathF.Sin(lineAngleRad) * radius,
                _outlinePaint);

            // Text im Segment
            var textAngle = startAngle + segmentAngle / 2f;
            var textAngleRad = textAngle * MathF.PI / 180f;
            var textDist = radius * 0.62f;
            var tx = MathF.Cos(textAngleRad) * textDist;
            var ty = MathF.Sin(textAngleRad) * textDist;

            canvas.Save();
            canvas.Translate(tx, ty);
            canvas.RotateDegrees(textAngle + 90f);

            // Segment-Text: Gems oder Coins
            string segmentText;
            if (rewards != null && i < rewards.Count && rewards[i].Gems > 0)
                segmentText = $"{rewards[i].Gems} Gems";
            else if (rewards != null && i < rewards.Count)
                segmentText = rewards[i].Coins.ToString("N0");
            else
                segmentText = "?";

            _textPaint.TextSize = radius * 0.10f;
            _textPaint.FakeBoldText = true;
            _textPaint.TextAlign = SKTextAlign.Center;
            // Jackpot = dunkle Schrift auf Gold, Rest = weiß
            var isJackpot = rewards != null && i < rewards.Count && rewards[i].IsJackpot;
            _textPaint.Color = isJackpot ? SKColor.Parse("#1A1A2A") : SKColors.White;
            canvas.DrawText(segmentText, 0, _textPaint.TextSize * 0.35f, _textPaint);

            canvas.Restore();
        }

        // Münz-Icon in der Mitte
        _centerPaint.Color = SKColor.Parse("#2A2A3A");
        canvas.DrawCircle(0, 0, radius * 0.15f, _centerPaint);
        _centerPaint.Color = SKColor.Parse("#FFD700");
        canvas.DrawCircle(0, 0, radius * 0.12f, _centerPaint);

        _textPaint.TextSize = radius * 0.1f;
        _textPaint.Color = SKColor.Parse("#1A1A2A");
        _textPaint.FakeBoldText = true;
        _textPaint.TextAlign = SKTextAlign.Center;
        canvas.DrawText("$", 0, _textPaint.TextSize * 0.35f, _textPaint);
    }

    private void DrawPointer(SKCanvas canvas, float cx, float cy, float size)
    {
        using var path = new SKPath();
        path.MoveTo(cx, cy - size * 0.3f);
        path.LineTo(cx - size * 0.6f, cy + size);
        path.LineTo(cx + size * 0.6f, cy + size);
        path.Close();

        _pointerPaint.Color = SKColor.Parse("#FFD700");
        canvas.DrawPath(path, _pointerPaint);

        _outlinePaint.Color = SKColor.Parse("#B8860B");
        _outlinePaint.StrokeWidth = 2;
        canvas.DrawPath(path, _outlinePaint);
    }

    private void DrawOuterGlow(SKCanvas canvas, float cx, float cy, float radius)
    {
        var outerRadius = radius + 8;
        _outlinePaint.Color = new SKColor(255, 215, 0, 60);
        _outlinePaint.StrokeWidth = 4;
        canvas.DrawCircle(cx, cy, outerRadius, _outlinePaint);

        // Äußerer Ring
        _outlinePaint.Color = new SKColor(255, 255, 255, 30);
        _outlinePaint.StrokeWidth = 2;
        canvas.DrawCircle(cx, cy, outerRadius + 4, _outlinePaint);
    }
}
