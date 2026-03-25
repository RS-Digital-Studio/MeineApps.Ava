namespace RebornSaga.Rendering.Backgrounds.Layers;

using SkiaSharp;
using System;

/// <summary>
/// Zeichnet Elemente ÜBER den Charakteren für Tiefe.
/// Safezone: nur unterhalb von MaxY, max Alpha 40%, nie im Dialog-Box-Bereich.
/// </summary>
public static class ForegroundRenderer
{
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPath _path = new();

    // Fog-Shader-Cache: Nur bei Positionsaenderung >2px neu erstellen
    private static SKShader? _cachedFogShader1;
    private static SKShader? _cachedFogShader2;
    private static int _cachedFogY1;
    private static int _cachedFogY2;
    private static int _cachedFogH;
    private static uint _cachedFogColor;
    private static byte _cachedFogAlpha;

    public static void Render(SKCanvas canvas, SKRect bounds, ForegroundDef[] foreground, float time)
    {
        foreach (var fg in foreground)
        {
            // Safezone: Clip auf erlaubten Bereich (nur unterhalb MaxY)
            var clipTop = bounds.Top + bounds.Height * fg.MaxY;
            canvas.Save();
            canvas.ClipRect(new SKRect(bounds.Left, clipTop, bounds.Right, bounds.Bottom));

            switch (fg.Style)
            {
                case ForegroundStyle.GrassBlade: DrawGrassBlades(canvas, bounds, fg, time); break;
                case ForegroundStyle.Fog: DrawFog(canvas, bounds, fg, time); break;
                case ForegroundStyle.Branch: DrawBranches(canvas, bounds, fg, time); break;
                case ForegroundStyle.Cobweb: DrawCobwebs(canvas, bounds, fg); break;
                case ForegroundStyle.LightRay: DrawLightRays(canvas, bounds, fg, time); break;
            }

            canvas.Restore();
        }
    }

    /// <summary>Hohe Grashalme vom unteren Rand, leichter Wind-Sway.</summary>
    private static void DrawGrassBlades(SKCanvas canvas, SKRect bounds, ForegroundDef fg, float time)
    {
        _strokePaint.Color = fg.Color.WithAlpha(fg.Alpha);
        _strokePaint.StrokeWidth = 2f;

        var count = 20;
        for (int i = 0; i < count; i++)
        {
            var x = bounds.Left + bounds.Width * (i + 0.3f) / count;
            var h = 20f + MathF.Sin(i * 1.7f) * 12f;
            var sway = MathF.Sin(time * 1.2f + i * 0.5f) * 4f;

            // Halm als Bezier
            _path.Rewind();
            _path.MoveTo(x, bounds.Bottom);
            _path.QuadTo(x + sway * 0.5f, bounds.Bottom - h * 0.5f,
                         x + sway, bounds.Bottom - h);
            canvas.DrawPath(_path, _strokePaint);
        }
    }

    /// <summary>Halbtransparenter Gradient-Streifen, langsam wandernd. Shader gecacht (2px Toleranz).</summary>
    private static void DrawFog(SKCanvas canvas, SKRect bounds, ForegroundDef fg, float time)
    {
        var fogH = bounds.Height * 0.12f;
        var fogY = bounds.Bottom - bounds.Height * 0.25f +
                   MathF.Sin(time * 0.3f) * bounds.Height * 0.03f;

        // Zweiter Nebel-Streifen (versetzt, duenner)
        var fogY2 = fogY - bounds.Height * 0.08f + MathF.Sin(time * 0.2f + 1.5f) * bounds.Height * 0.02f;

        // Quantisierte Werte fuer Cache-Vergleich (2px Toleranz)
        int qFogY1 = (int)(fogY / 2f) * 2;
        int qFogY2 = (int)(fogY2 / 2f) * 2;
        int qFogH = (int)(fogH / 2f) * 2;
        uint colorKey = (uint)((fg.Color.Red << 16) | (fg.Color.Green << 8) | fg.Color.Blue);

        bool needsRebuild = _cachedFogShader1 == null ||
            qFogY1 != _cachedFogY1 || qFogY2 != _cachedFogY2 ||
            qFogH != _cachedFogH || colorKey != _cachedFogColor || fg.Alpha != _cachedFogAlpha;

        if (needsRebuild)
        {
            _cachedFogShader1?.Dispose();
            _cachedFogShader2?.Dispose();

            _cachedFogShader1 = SKShader.CreateLinearGradient(
                new SKPoint(bounds.MidX, fogY - fogH),
                new SKPoint(bounds.MidX, fogY + fogH),
                new[] { fg.Color.WithAlpha(0), fg.Color.WithAlpha(fg.Alpha), fg.Color.WithAlpha(0) },
                new[] { 0f, 0.5f, 1f },
                SKShaderTileMode.Clamp);

            _cachedFogShader2 = SKShader.CreateLinearGradient(
                new SKPoint(bounds.MidX, fogY2 - fogH * 0.5f),
                new SKPoint(bounds.MidX, fogY2 + fogH * 0.5f),
                new[] { fg.Color.WithAlpha(0), fg.Color.WithAlpha((byte)(fg.Alpha * 0.5f)), fg.Color.WithAlpha(0) },
                new[] { 0f, 0.5f, 1f },
                SKShaderTileMode.Clamp);

            _cachedFogY1 = qFogY1;
            _cachedFogY2 = qFogY2;
            _cachedFogH = qFogH;
            _cachedFogColor = colorKey;
            _cachedFogAlpha = fg.Alpha;
        }

        _fillPaint.Shader = _cachedFogShader1;
        canvas.DrawRect(bounds.Left, fogY - fogH, bounds.Width, fogH * 2, _fillPaint);
        _fillPaint.Shader = null;

        _fillPaint.Shader = _cachedFogShader2;
        canvas.DrawRect(bounds.Left, fogY2 - fogH * 0.5f, bounds.Width, fogH, _fillPaint);
        _fillPaint.Shader = null;
    }

    /// <summary>Blätter/Äste von den oberen Ecken (nur am Rand, nie mittig).</summary>
    private static void DrawBranches(SKCanvas canvas, SKRect bounds, ForegroundDef fg, float time)
    {
        _strokePaint.Color = fg.Color.WithAlpha(fg.Alpha);
        _strokePaint.StrokeWidth = 2.5f;

        // Linke obere Ecke
        var sway = MathF.Sin(time * 0.8f) * 3f;
        for (int i = 0; i < 3; i++)
        {
            var startX = bounds.Left;
            var startY = bounds.Top + bounds.Height * fg.MaxY + i * 15f;
            var endX = bounds.Left + 40f + i * 12f + sway;
            var endY = startY + 20f + sway;

            _path.Rewind();
            _path.MoveTo(startX, startY);
            _path.QuadTo(startX + 20f + sway, startY + 5f, endX, endY);
            canvas.DrawPath(_path, _strokePaint);

            // Blättchen am Ende
            _fillPaint.Color = fg.Color.WithAlpha((byte)(fg.Alpha * 0.8f));
            canvas.DrawOval(endX, endY, 5f, 3f, _fillPaint);
        }

        // Rechte obere Ecke
        for (int i = 0; i < 2; i++)
        {
            var startX = bounds.Right;
            var startY = bounds.Top + bounds.Height * fg.MaxY + i * 18f + 5f;
            var endX = bounds.Right - 35f - i * 10f - sway;
            var endY = startY + 18f + sway;

            _path.Rewind();
            _path.MoveTo(startX, startY);
            _path.QuadTo(startX - 18f - sway, startY + 4f, endX, endY);
            canvas.DrawPath(_path, _strokePaint);

            _fillPaint.Color = fg.Color.WithAlpha((byte)(fg.Alpha * 0.8f));
            canvas.DrawOval(endX, endY, 4f, 2.5f, _fillPaint);
        }
    }

    /// <summary>Feine Linien in Ecken (Dungeon).</summary>
    private static void DrawCobwebs(SKCanvas canvas, SKRect bounds, ForegroundDef fg)
    {
        _strokePaint.Color = fg.Color.WithAlpha(fg.Alpha);
        _strokePaint.StrokeWidth = 0.8f;

        // Linke obere Ecke — Spinnennetz
        var cx = bounds.Left;
        var cy = bounds.Top + bounds.Height * fg.MaxY;
        var webSize = 40f;

        // Strahlen
        for (int i = 0; i < 5; i++)
        {
            var angle = i * MathF.PI * 0.5f / 4f;
            canvas.DrawLine(cx, cy,
                cx + MathF.Cos(angle) * webSize,
                cy + MathF.Sin(angle) * webSize, _strokePaint);
        }
        // Bögen (2 Ringe)
        for (int ring = 1; ring <= 2; ring++)
        {
            var r = webSize * ring * 0.4f;
            _path.Rewind();
            _path.MoveTo(cx + r, cy);
            _path.QuadTo(cx + r * 0.7f, cy + r * 0.7f, cx, cy + r);
            canvas.DrawPath(_path, _strokePaint);
        }

        // Rechte obere Ecke (gespiegelt)
        cx = bounds.Right;
        for (int i = 0; i < 4; i++)
        {
            var angle = MathF.PI * 0.5f + i * MathF.PI * 0.5f / 3f;
            canvas.DrawLine(cx, cy,
                cx + MathF.Cos(angle) * webSize * 0.7f,
                cy + MathF.Sin(angle) * webSize * 0.7f, _strokePaint);
        }
    }

    /// <summary>Diagonale Lichtstrahlen von oben (durch Blätterdach/Fenster).</summary>
    private static void DrawLightRays(SKCanvas canvas, SKRect bounds, ForegroundDef fg, float time)
    {
        var rayCount = 3;
        var pulse = 0.7f + MathF.Sin(time * 0.5f) * 0.3f;

        for (int i = 0; i < rayCount; i++)
        {
            var startX = bounds.Left + bounds.Width * (0.2f + i * 0.3f);
            var rayW = bounds.Width * 0.06f;
            var alpha = (byte)(fg.Alpha * pulse * (0.6f + MathF.Sin(time * 0.3f + i * 1.5f) * 0.4f));

            // Schräger Strahl als Gradient-Trapez
            _path.Rewind();
            _path.MoveTo(startX, bounds.Top + bounds.Height * fg.MaxY);
            _path.LineTo(startX + rayW, bounds.Top + bounds.Height * fg.MaxY);
            _path.LineTo(startX + rayW * 2.5f, bounds.Bottom);
            _path.LineTo(startX - rayW * 0.5f, bounds.Bottom);
            _path.Close();

            _fillPaint.Color = fg.Color.WithAlpha(alpha);
            canvas.DrawPath(_path, _fillPaint);
        }
    }

    public static void Cleanup()
    {
        _cachedFogShader1?.Dispose();
        _cachedFogShader2?.Dispose();
        _cachedFogShader1 = null;
        _cachedFogShader2 = null;
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _path.Dispose();
    }
}
