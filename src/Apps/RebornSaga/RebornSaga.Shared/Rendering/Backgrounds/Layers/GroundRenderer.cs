namespace RebornSaga.Rendering.Backgrounds.Layers;

using SkiaSharp;
using System;

/// <summary>Zeichnet den Boden am unteren Rand mit texturierten Details.</summary>
public static class GroundRenderer
{
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _detailPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _fadePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPath _path = new();

    // Gecachter Fade-Shader (nur bei Szenen-/Bounds-Wechsel neu erstellen)
    private static SKShader? _fadeShader;
    private static SKColor _cachedFadeColor;
    private static float _cachedFadeTop;

    public static void Render(SKCanvas canvas, SKRect bounds, GroundDef ground)
    {
        var groundTop = bounds.Bottom - bounds.Height * ground.Height;

        // Basis-Füllung
        _fillPaint.Color = ground.Color;
        canvas.DrawRect(bounds.Left, groundTop, bounds.Width, bounds.Height * ground.Height, _fillPaint);

        // Oberkante: weicher Gradient-Übergang (gecachter Shader, nur bei Wechsel neu)
        var fadeH = bounds.Height * 0.03f;
        if (_fadeShader == null || _cachedFadeColor != ground.Color
            || Math.Abs(_cachedFadeTop - groundTop) > 0.5f)
        {
            _fadeShader?.Dispose();
            _fadeShader = SKShader.CreateLinearGradient(
                new SKPoint(bounds.MidX, groundTop - fadeH),
                new SKPoint(bounds.MidX, groundTop + fadeH),
                new[] { ground.Color.WithAlpha(0), ground.Color },
                SKShaderTileMode.Clamp);
            _cachedFadeColor = ground.Color;
            _cachedFadeTop = groundTop;
        }
        _fadePaint.Shader = _fadeShader;
        canvas.DrawRect(bounds.Left, groundTop - fadeH, bounds.Width, fadeH * 2, _fadePaint);
        _fadePaint.Shader = null;

        // Detail-Textur je nach Typ
        var accent = ground.AccentColor ?? DarkenColor(ground.Color, 0.7f);
        _detailPaint.Color = accent.WithAlpha(60);
        _detailPaint.StrokeWidth = 1f;

        switch (ground.Type)
        {
            case GroundType.Grass:
                DrawGrassDetails(canvas, bounds, groundTop);
                break;
            case GroundType.Stone:
                DrawStoneDetails(canvas, bounds, groundTop, ground.Height);
                break;
            case GroundType.Wood:
                DrawWoodDetails(canvas, bounds, groundTop);
                break;
            case GroundType.Sand:
                DrawSandDetails(canvas, bounds, groundTop, accent);
                break;
            case GroundType.Snow:
                DrawSnowDetails(canvas, bounds, groundTop, ground.Height);
                break;
            case GroundType.Water:
                DrawWaterDetails(canvas, bounds, groundTop);
                break;
        }
    }

    private static void DrawGrassDetails(SKCanvas canvas, SKRect bounds, float groundTop)
    {
        // Grashalme an der Oberkante
        for (float x = bounds.Left; x < bounds.Right; x += 8f)
        {
            var h = 4f + MathF.Sin(x * 0.3f) * 3f;
            canvas.DrawLine(x, groundTop, x + 2f, groundTop - h, _detailPaint);
        }
    }

    private static void DrawStoneDetails(SKCanvas canvas, SKRect bounds, float groundTop, float height)
    {
        // Horizontale Fugen
        for (float y = groundTop + 12f; y < bounds.Bottom; y += 18f)
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, _detailPaint);
        // Vertikale Fugen (versetzt)
        for (float x = bounds.Left + 30f; x < bounds.Right; x += 60f)
        {
            var row = (int)((x - bounds.Left) / 60f);
            var yOff = row % 2 == 0 ? 0f : 9f;
            canvas.DrawLine(x, groundTop + yOff, x, groundTop + yOff + 18f, _detailPaint);
        }
    }

    private static void DrawWoodDetails(SKCanvas canvas, SKRect bounds, float groundTop)
    {
        // Planken-Linien
        for (float y = groundTop + 8f; y < bounds.Bottom; y += 14f)
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, _detailPaint);
    }

    private static void DrawSandDetails(SKCanvas canvas, SKRect bounds, float groundTop, SKColor accent)
    {
        // Subtile Punkte
        _fillPaint.Color = accent.WithAlpha(30);
        for (float x = bounds.Left + 5f; x < bounds.Right; x += 12f)
            for (float y = groundTop + 5f; y < bounds.Bottom; y += 10f)
                canvas.DrawCircle(x + MathF.Sin(y) * 3f, y, 1f, _fillPaint);
    }

    private static void DrawSnowDetails(SKCanvas canvas, SKRect bounds, float groundTop, float height)
    {
        // Blaue Schatten-Flecken
        _fillPaint.Color = new SKColor(0x80, 0x90, 0xC0, 20);
        for (int i = 0; i < 6; i++)
        {
            var sx = bounds.Left + bounds.Width * (i * 0.18f + 0.02f);
            canvas.DrawOval(sx, groundTop + bounds.Height * height * 0.5f,
                bounds.Width * 0.08f, bounds.Height * height * 0.3f, _fillPaint);
        }
    }

    private static void DrawWaterDetails(SKCanvas canvas, SKRect bounds, float groundTop)
    {
        // Wellenlinien
        _detailPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 25);
        _detailPaint.StrokeWidth = 1.5f;
        _path.Rewind();
        _path.MoveTo(bounds.Left, groundTop + 3f);
        for (float x = bounds.Left; x < bounds.Right; x += 20f)
            _path.QuadTo(x + 10f, groundTop, x + 20f, groundTop + 3f);
        canvas.DrawPath(_path, _detailPaint);
    }

    public static void Cleanup()
    {
        _fillPaint.Dispose();
        _detailPaint.Dispose();
        _fadePaint.Dispose();
        _path.Dispose();
        _fadeShader?.Dispose();
        _fadeShader = null;
    }

    private static SKColor DarkenColor(SKColor c, float f) => new(
        (byte)(c.Red * f), (byte)(c.Green * f), (byte)(c.Blue * f), c.Alpha);
}
