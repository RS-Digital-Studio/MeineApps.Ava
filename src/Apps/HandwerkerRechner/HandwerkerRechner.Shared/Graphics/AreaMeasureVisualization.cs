using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Visualisierung für den Aufmaß-Rechner: Schematische 2D-Zeichnung der gewählten Form.
/// Zeigt die Form mit Bemaßungspfeilen und Flächenangabe.
/// </summary>
public static class AreaMeasureVisualization
{
    private static readonly SKPaint FillPaint = new() { Color = new SKColor(59, 130, 246, 60), IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint StrokePaint = new() { Color = new SKColor(59, 130, 246), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f };
    private static readonly SKPaint DimPaint = new() { Color = new SKColor(245, 158, 11), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint TextPaint = new() { Color = SKColors.White, IsAntialias = true };
    private static readonly SKPaint AreaTextPaint = new() { Color = new SKColor(34, 197, 94), IsAntialias = true };
    private static readonly SKPaint BgPaint = new() { Color = new SKColor(30, 30, 30, 120), IsAntialias = true };

    // SKFont-Objekte für Text-Rendering (nicht-veraltete API)
    private static readonly SKFont TextFont = new() { Size = 12f, Embolden = true };
    private static readonly SKFont AreaTextFont = new() { Size = 16f, Embolden = true };

    /// <summary>Zeichnet die aktuelle Form</summary>
    /// <param name="shapeIndex">0=Rechteck, 1=L, 2=T, 3=Trapez, 4=Dreieck, 5=Kreis</param>
    public static void Render(SKCanvas canvas, SKRect bounds, int shapeIndex,
        double dim1, double dim2, double dim3, double dim4, double dim5,
        double area, float alpha = 1f)
    {
        canvas.DrawRoundRect(bounds.Left, bounds.Top, bounds.Width, bounds.Height, 12, 12, BgPaint);

        var drawArea = new SKRect(
            bounds.Left + 30, bounds.Top + 20,
            bounds.Right - 30, bounds.Bottom - 40);

        switch (shapeIndex)
        {
            case 0: DrawRectangle(canvas, drawArea, dim1, dim2, area, alpha); break;
            case 1: DrawLShape(canvas, drawArea, dim1, dim2, dim3, dim4, area, alpha); break;
            case 2: DrawTShape(canvas, drawArea, dim1, dim2, dim3, dim4, area, alpha); break;
            case 3: DrawTrapezoid(canvas, drawArea, dim1, dim2, dim5, area, alpha); break;
            case 4: DrawTriangle(canvas, drawArea, dim1, dim2, area, alpha); break;
            case 5: DrawCircle(canvas, drawArea, dim1, area, alpha); break;
        }

        // Flächenangabe unten
        var areaText = $"{area:F2} m²";
        var areaWidth = AreaTextFont.MeasureText(areaText, AreaTextPaint);
        canvas.DrawText(areaText,
            bounds.Left + (bounds.Width - areaWidth) / 2,
            bounds.Bottom - 12, SKTextAlign.Left, AreaTextFont, AreaTextPaint);
    }

    private static void DrawRectangle(SKCanvas canvas, SKRect area, double w, double h, double sqm, float alpha)
    {
        var scale = Math.Min(area.Width / (float)w, area.Height / (float)h) * 0.7f;
        var rw = (float)w * scale;
        var rh = (float)h * scale;
        var left = area.Left + (area.Width - rw) / 2;
        var top = area.Top + (area.Height - rh) / 2;

        var rect = new SKRect(left, top, left + rw, top + rh);
        canvas.DrawRect(rect, FillPaint);
        canvas.DrawRect(rect, StrokePaint);

        // Bemaßung
        DrawDimension(canvas, left, top + rh + 12, left + rw, top + rh + 12, $"{w:F1}m");
        DrawDimension(canvas, left - 12, top, left - 12, top + rh, $"{h:F1}m");
    }

    private static void DrawLShape(SKCanvas canvas, SKRect area, double w, double h, double cw, double ch, double sqm, float alpha)
    {
        var scale = Math.Min(area.Width / (float)w, area.Height / (float)h) * 0.65f;
        var sw = (float)w * scale;
        var sh = (float)h * scale;
        var scw = (float)cw * scale;
        var sch = (float)ch * scale;
        var left = area.Left + (area.Width - sw) / 2;
        var top = area.Top + (area.Height - sh) / 2;

        using var path = new SKPath();
        path.MoveTo(left, top);
        path.LineTo(left + sw, top);
        path.LineTo(left + sw, top + sh);
        path.LineTo(left + scw, top + sh);
        path.LineTo(left + scw, top + sch);
        path.LineTo(left, top + sch);
        path.Close();

        canvas.DrawPath(path, FillPaint);
        canvas.DrawPath(path, StrokePaint);
    }

    private static void DrawTShape(SKCanvas canvas, SKRect area, double stemW, double stemH, double crossW, double crossH, double sqm, float alpha)
    {
        var maxW = Math.Max(stemW, crossW);
        var totalH = stemH + crossH;
        var scale = Math.Min(area.Width / (float)maxW, area.Height / (float)totalH) * 0.65f;

        var sw = (float)stemW * scale;
        var sh = (float)stemH * scale;
        var cw = (float)crossW * scale;
        var ch = (float)crossH * scale;
        var cx = area.Left + (area.Width - cw) / 2;
        var stemX = area.Left + (area.Width - sw) / 2;
        var top = area.Top + (area.Height - sh - ch) / 2;

        using var path = new SKPath();
        path.MoveTo(cx, top);
        path.LineTo(cx + cw, top);
        path.LineTo(cx + cw, top + ch);
        path.LineTo(stemX + sw, top + ch);
        path.LineTo(stemX + sw, top + ch + sh);
        path.LineTo(stemX, top + ch + sh);
        path.LineTo(stemX, top + ch);
        path.LineTo(cx, top + ch);
        path.Close();

        canvas.DrawPath(path, FillPaint);
        canvas.DrawPath(path, StrokePaint);
    }

    private static void DrawTrapezoid(SKCanvas canvas, SKRect area, double a, double h, double b, double sqm, float alpha)
    {
        var maxW = (float)Math.Max(a, b);
        var scale = Math.Min(area.Width / maxW, area.Height / (float)h) * 0.7f;
        var aw = (float)a * scale;
        var bw = (float)b * scale;
        var th = (float)h * scale;
        var cx = area.Left + area.Width / 2;
        var top = area.Top + (area.Height - th) / 2;

        using var path = new SKPath();
        path.MoveTo(cx - aw / 2, top);
        path.LineTo(cx + aw / 2, top);
        path.LineTo(cx + bw / 2, top + th);
        path.LineTo(cx - bw / 2, top + th);
        path.Close();

        canvas.DrawPath(path, FillPaint);
        canvas.DrawPath(path, StrokePaint);

        DrawDimension(canvas, cx - aw / 2, top - 10, cx + aw / 2, top - 10, $"a={a:F1}m");
        DrawDimension(canvas, cx - bw / 2, top + th + 14, cx + bw / 2, top + th + 14, $"b={b:F1}m");
    }

    private static void DrawTriangle(SKCanvas canvas, SKRect area, double baseW, double h, double sqm, float alpha)
    {
        var scale = Math.Min(area.Width / (float)baseW, area.Height / (float)h) * 0.7f;
        var bw = (float)baseW * scale;
        var th = (float)h * scale;
        var cx = area.Left + area.Width / 2;
        var bottom = area.Top + (area.Height + th) / 2;

        using var path = new SKPath();
        path.MoveTo(cx, bottom - th);
        path.LineTo(cx + bw / 2, bottom);
        path.LineTo(cx - bw / 2, bottom);
        path.Close();

        canvas.DrawPath(path, FillPaint);
        canvas.DrawPath(path, StrokePaint);
    }

    private static void DrawCircle(SKCanvas canvas, SKRect area, double diameter, double sqm, float alpha)
    {
        var maxDim = Math.Min(area.Width, area.Height) * 0.7f;
        var radius = maxDim / 2;
        var cx = area.Left + area.Width / 2;
        var cy = area.Top + area.Height / 2;

        canvas.DrawCircle(cx, cy, radius, FillPaint);
        canvas.DrawCircle(cx, cy, radius, StrokePaint);

        // Durchmesser-Linie
        canvas.DrawLine(cx - radius, cy, cx + radius, cy, DimPaint);
        var dimText = $"d={diameter:F1}m";
        canvas.DrawText(dimText, cx, cy - 8, SKTextAlign.Center, TextFont, TextPaint);
    }

    private static void DrawDimension(SKCanvas canvas, float x1, float y1, float x2, float y2, string text)
    {
        canvas.DrawLine(x1, y1, x2, y2, DimPaint);

        // Pfeile
        var arrowSize = 4f;
        if (Math.Abs(y1 - y2) < 1) // Horizontal
        {
            canvas.DrawLine(x1, y1 - arrowSize, x1, y1 + arrowSize, DimPaint);
            canvas.DrawLine(x2, y2 - arrowSize, x2, y2 + arrowSize, DimPaint);
        }
        else // Vertikal
        {
            canvas.DrawLine(x1 - arrowSize, y1, x1 + arrowSize, y1, DimPaint);
            canvas.DrawLine(x2 - arrowSize, y2, x2 + arrowSize, y2, DimPaint);
        }

        var tx = (x1 + x2) / 2;
        var ty = (y1 + y2) / 2 - 4;
        canvas.DrawText(text, tx, ty, SKTextAlign.Center, TextFont, TextPaint);
    }
}
