using Android.Content;
using Android.Graphics;
using Android.Views;
using SunSeeker.Shared.Services;
using Path = Android.Graphics.Path;

namespace SunSeeker.Android.Ar;

/// <summary>
/// Transparentes Overlay über der Kamera-Vorschau: zeichnet die Tagesbahn der Sonne, die aktuelle
/// Sonnenposition (Glow-Scheibe), Auf-/Untergangs-Marker und — wenn die Sonne außerhalb des Bildes
/// liegt — einen Rand-Pfeil in ihre Richtung. Projektion via <see cref="SunArProjection"/>
/// (testbar, in Shared). Reines Android-Canvas-Zeichnen, Paints werden gecacht.
/// </summary>
public sealed class SunArOverlayView : View
{
    // Vom Activity pro Sensor-Tick gesetzt:
    public double CameraAzimuth { get; set; }
    public double CameraElevation { get; set; }
    public double CameraRoll { get; set; }
    public double HorizontalFovDeg { get; set; } = 62.0;

    public IReadOnlyList<(double Az, double El)> ArcPoints { get; set; } = [];
    public (double Az, double El)? CurrentSun { get; set; }
    public (double Az, double El, string Label)? Sunrise { get; set; }
    public (double Az, double El, string Label)? Sunset { get; set; }
    public string HintText { get; set; } = "";

    private static readonly Color SunColor = Color.Argb(255, 255, 213, 79);
    private static readonly Color ArcColor = Color.Argb(210, 255, 179, 0);
    private static readonly Color ArcLowColor = Color.Argb(210, 255, 112, 67);
    private static readonly Color MarkerColor = Color.Argb(235, 240, 237, 230);

    private readonly Paint _arcPaint = new() { AntiAlias = true, StrokeWidth = 6f, StrokeCap = Paint.Cap.Round };
    private readonly Paint _arcDotPaint = new() { AntiAlias = true };
    private readonly Paint _sunCorePaint = new() { AntiAlias = true, Color = SunColor };
    private readonly Paint _sunGlowPaint = new() { AntiAlias = true };
    private readonly Paint _markerPaint = new() { AntiAlias = true, Color = MarkerColor, StrokeWidth = 3f };
    private readonly Paint _markerTextPaint = new() { AntiAlias = true, Color = MarkerColor, TextSize = 30f };
    private readonly Paint _arrowPaint = new() { AntiAlias = true, Color = SunColor };
    private readonly Paint _hintPaint = new() { AntiAlias = true, Color = Color.White, TextSize = 36f, TextAlign = Paint.Align.Center };
    private readonly Paint _hintBgPaint = new() { AntiAlias = true, Color = Color.Argb(140, 0, 0, 0) };

    public SunArOverlayView(Context context) : base(context)
    {
        _arcPaint.SetStyle(Paint.Style.Stroke);
        _arcDotPaint.SetStyle(Paint.Style.Fill);
        _sunCorePaint.SetStyle(Paint.Style.Fill);
        _sunGlowPaint.SetStyle(Paint.Style.Fill);
        _markerPaint.SetStyle(Paint.Style.Stroke);
        _arrowPaint.SetStyle(Paint.Style.Fill);
        _hintPaint.SetShadowLayer(4f, 0f, 0f, Color.Black);
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        float w = Width;
        float h = Height;
        if (w <= 0 || h <= 0) return;

        // Vertikales FOV aus dem horizontalen FOV + Seitenverhältnis (Hochformat → vFov größer).
        double vFov = HorizontalFovDeg * h / w;

        SunArProjection.Projected Project(double az, double el) =>
            SunArProjection.Project(az, el, CameraAzimuth, CameraElevation, CameraRoll, HorizontalFovDeg, vFov, w, h);

        DrawArc(canvas, Project);
        DrawMarker(canvas, Project, Sunrise);
        DrawMarker(canvas, Project, Sunset);
        DrawSun(canvas, Project, w, h);
        DrawHint(canvas, w, h);
    }

    private void DrawArc(Canvas canvas, Func<double, double, SunArProjection.Projected> project)
    {
        if (ArcPoints.Count < 2) return;
        Path? path = null;
        for (int i = 0; i < ArcPoints.Count; i++)
        {
            var (az, el) = ArcPoints[i];
            var p = project(az, el);
            if (p.OnScreen)
            {
                _arcDotPaint.Color = el < 8 ? ArcLowColor : ArcColor;
                canvas.DrawCircle(p.X, p.Y, 4f, _arcDotPaint);
            }
            if (p.InFront)
            {
                path ??= new Path();
                if (path.IsEmpty) path.MoveTo(p.X, p.Y); else path.LineTo(p.X, p.Y);
            }
            else if (path is { IsEmpty: false })
            {
                _arcPaint.Color = ArcColor;
                canvas.DrawPath(path, _arcPaint);
                path.Reset();
            }
        }
        if (path is { IsEmpty: false })
        {
            _arcPaint.Color = ArcColor;
            canvas.DrawPath(path, _arcPaint);
        }
    }

    private void DrawMarker(Canvas canvas, Func<double, double, SunArProjection.Projected> project, (double Az, double El, string Label)? marker)
    {
        if (marker is not { } m) return;
        var p = project(m.Az, m.El);
        if (!p.OnScreen) return;
        canvas.DrawCircle(p.X, p.Y, 14f, _markerPaint);
        canvas.DrawText(m.Label, p.X + 20f, p.Y + 10f, _markerTextPaint);
    }

    private void DrawSun(Canvas canvas, Func<double, double, SunArProjection.Projected> project, float w, float h)
    {
        if (CurrentSun is not { } sun) return;
        var p = project(sun.Az, sun.El);

        if (p.OnScreen)
        {
            const float glow = 70f;
            _sunGlowPaint.SetShader(new RadialGradient(
                p.X, p.Y, glow,
                [Color.Argb(180, 255, 213, 79), Color.Argb(0, 255, 213, 79)],
                [0f, 1f], Shader.TileMode.Clamp!));
            canvas.DrawCircle(p.X, p.Y, glow, _sunGlowPaint);
            canvas.DrawCircle(p.X, p.Y, 22f, _sunCorePaint);
        }
        else
        {
            // Sonne außerhalb des Bildes → Rand-Pfeil in ihre Richtung.
            DrawEdgeArrow(canvas, p.ScreenAngleDeg, w, h);
        }
    }

    private void DrawEdgeArrow(Canvas canvas, double screenAngleDeg, float w, float h)
    {
        float cx = w / 2f, cy = h / 2f;
        double rad = screenAngleDeg * Math.PI / 180.0;
        float margin = 90f;
        float radius = Math.Min(cx, cy) - margin;
        float ax = cx + (float)(Math.Cos(rad) * radius);
        float ay = cy + (float)(Math.Sin(rad) * radius);

        using var arrow = new Path();
        float size = 26f;
        double a = rad;
        arrow.MoveTo(ax + (float)(Math.Cos(a) * size), ay + (float)(Math.Sin(a) * size));
        arrow.LineTo(ax + (float)(Math.Cos(a + 2.5) * size), ay + (float)(Math.Sin(a + 2.5) * size));
        arrow.LineTo(ax + (float)(Math.Cos(a - 2.5) * size), ay + (float)(Math.Sin(a - 2.5) * size));
        arrow.Close();
        canvas.DrawPath(arrow, _arrowPaint);
    }

    private void DrawHint(Canvas canvas, float w, float h)
    {
        if (string.IsNullOrEmpty(HintText)) return;
        float y = h - 70f;
        canvas.DrawRect(0, y - 44f, w, y + 20f, _hintBgPaint);
        canvas.DrawText(HintText, w / 2f, y, _hintPaint);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _arcPaint.Dispose();
            _arcDotPaint.Dispose();
            _sunCorePaint.Dispose();
            _sunGlowPaint.Dispose();
            _markerPaint.Dispose();
            _markerTextPaint.Dispose();
            _arrowPaint.Dispose();
            _hintPaint.Dispose();
            _hintBgPaint.Dispose();
        }
        base.Dispose(disposing);
    }
}
