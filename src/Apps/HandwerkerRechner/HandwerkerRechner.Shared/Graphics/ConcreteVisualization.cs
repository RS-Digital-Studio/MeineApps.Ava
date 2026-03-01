using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// 3 Sub-Visualisierungen: Platte (Isometrie-Quader), Fundament (Seiten-Schnitt), Säule (Zylinder).
/// </summary>
public static class ConcreteVisualization
{
    // Einschwing-Animation
    private static readonly AnimatedVisualizationBase _animation = new()
    {
        AnimationDurationMs = 500f,
        EasingFunction = EasingFunctions.EaseOutCubic
    };

    /// <summary>Startet die Einschwing-Animation.</summary>
    public static void StartAnimation() => _animation.StartAnimation();

    /// <summary>True wenn noch animiert wird (für InvalidateSurface-Loop).</summary>
    public static bool NeedsRedraw => _animation.IsAnimating;

    private static readonly SKPaint _concreteFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _concreteStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _topFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _sideFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private static readonly SKPaint _mixPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _mixStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private static readonly SKFont _mixFont = new() { Size = 9f };
    private static readonly SKPaint _mixTextPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _layerPaint = new() { IsAntialias = false };

    // Mischverhältnis-Farben
    private static readonly SKColor _cementColor = new(0x78, 0x85, 0x8C); // Grau (Zement)
    private static readonly SKColor _sandMixColor = new(0xD4, 0xBE, 0x8C); // Sand
    private static readonly SKColor _gravelMixColor = new(0x9C, 0x8C, 0x7C); // Kies
    private static readonly SKColor _waterMixColor = new(0x38, 0xBD, 0xF8); // Wasser

    /// <summary>
    /// subType: 0=Platte, 1=Streifenfundament, 2=Säule
    /// mixRatio: Optional Zement:Sand:Kies Verhältnis (z.B. 1:2:3). Null = kein Mischverhältnis.
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds, int subType,
        float dim1Cm, float dim2Cm, float dim3Cm, float volumeM3, bool hasResult,
        float cementParts = 1, float sandParts = 2, float gravelParts = 3)
    {
        if (!hasResult) return;

        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

        SkiaBlueprintCanvas.DrawGrid(canvas, bounds, 20f);

        // Global Alpha Fade-In
        _layerPaint.Color = SKColors.White.WithAlpha((byte)(255 * progress));
        canvas.SaveLayer(_layerPaint);

        // Hauptvisualisierung in oberen ~75% des Bereichs
        var mainBounds = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom - 40f);

        switch (subType)
        {
            case 0: RenderSlab(canvas, mainBounds, dim1Cm, dim2Cm, dim3Cm, volumeM3); break;
            case 1: RenderStrip(canvas, mainBounds, dim1Cm, dim2Cm, dim3Cm, volumeM3); break;
            case 2: RenderColumn(canvas, mainBounds, dim1Cm, dim2Cm, volumeM3); break;
        }

        // Mischverhältnis-Leiste am unteren Rand
        if (cementParts > 0 || sandParts > 0 || gravelParts > 0)
        {
            DrawMixRatioBar(canvas, bounds, cementParts, sandParts, gravelParts);
        }

        canvas.Restore();
    }

    /// <summary>
    /// Platte: Isometrischer Quader mit L×B×H Maßlinien.
    /// </summary>
    private static void RenderSlab(SKCanvas canvas, SKRect bounds,
        float lengthCm, float widthCm, float heightCm, float volumeM3)
    {
        if (lengthCm <= 0 || widthCm <= 0 || heightCm <= 0) return;

        float cx = bounds.MidX;
        float cy = bounds.MidY;

        // Isometrie-Projektionsfaktoren
        float maxDim = Math.Max(lengthCm, Math.Max(widthCm, heightCm));
        float availH = bounds.Height - 80f;
        float isoScale = Math.Min(availH / (maxDim * 1.5f), (bounds.Width - 80f) / (maxDim * 1.5f));
        isoScale = Math.Min(isoScale, 1.2f);

        float l = lengthCm * isoScale;
        float w = widthCm * isoScale * 0.5f; // Tiefe in Iso
        float h = heightCm * isoScale;

        // Isometrische Offsets
        float isoX = w * 0.7f;
        float isoY = w * 0.4f;

        // Basisposition (vorne-unten-links)
        float baseX = cx - l / 2f;
        float baseY = cy + h / 2f;

        // Vorderseite
        using var frontPath = new SKPath();
        frontPath.MoveTo(baseX, baseY);
        frontPath.LineTo(baseX + l, baseY);
        frontPath.LineTo(baseX + l, baseY - h);
        frontPath.LineTo(baseX, baseY - h);
        frontPath.Close();

        _concreteFill.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Card, 200);
        canvas.DrawPath(frontPath, _concreteFill);
        _concreteStroke.Color = SkiaThemeHelper.TextSecondary;
        canvas.DrawPath(frontPath, _concreteStroke);

        // Oberseite
        using var topPath = new SKPath();
        topPath.MoveTo(baseX, baseY - h);
        topPath.LineTo(baseX + l, baseY - h);
        topPath.LineTo(baseX + l + isoX, baseY - h - isoY);
        topPath.LineTo(baseX + isoX, baseY - h - isoY);
        topPath.Close();

        _topFill.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Accent, 60);
        canvas.DrawPath(topPath, _topFill);
        _concreteStroke.Color = SkiaThemeHelper.TextSecondary;
        canvas.DrawPath(topPath, _concreteStroke);

        // Rechte Seite
        using var sidePath = new SKPath();
        sidePath.MoveTo(baseX + l, baseY);
        sidePath.LineTo(baseX + l + isoX, baseY - isoY);
        sidePath.LineTo(baseX + l + isoX, baseY - h - isoY);
        sidePath.LineTo(baseX + l, baseY - h);
        sidePath.Close();

        _sideFill.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Card, 140);
        canvas.DrawPath(sidePath, _sideFill);
        canvas.DrawPath(sidePath, _concreteStroke);

        // Maßlinien
        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(baseX, baseY + 4f), new SKPoint(baseX + l, baseY + 4f),
            $"{lengthCm:F0} cm", offset: 14f);

        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(baseX - 4f, baseY), new SKPoint(baseX - 4f, baseY - h),
            $"{heightCm:F0} cm", offset: -14f);

        // Volumen
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"{volumeM3:F3} m³",
            new SKPoint(cx, baseY + 28f),
            SkiaThemeHelper.Accent, 11f);
    }

    /// <summary>
    /// Streifenfundament: Seiten-Schnitt als Rechteck im Boden.
    /// </summary>
    private static void RenderStrip(SKCanvas canvas, SKRect bounds,
        float lengthCm, float widthCm, float depthCm, float volumeM3)
    {
        if (lengthCm <= 0 || widthCm <= 0 || depthCm <= 0) return;

        // Seiten-Schnitt: Breite × Tiefe
        var fit = SkiaBlueprintCanvas.FitToCanvas(bounds, widthCm, depthCm + 20f, 40f);
        float scale = fit.Scale;
        float ox = fit.OffsetX;
        float oy = fit.OffsetY + 20f * scale;

        float fw = widthCm * scale;
        float fh = depthCm * scale;

        // Erdreich-Hintergrund
        _concreteFill.Color = new SKColor(0x8B, 0x6C, 0x42, 40);
        canvas.DrawRect(bounds.Left, oy - 4f, bounds.Width, fh + 10f, _concreteFill);

        // Boden-Linie
        _concreteStroke.Color = new SKColor(0x8B, 0x6C, 0x42, 150);
        _concreteStroke.StrokeWidth = 1.5f;
        canvas.DrawLine(bounds.Left, oy - 2f, bounds.Right, oy - 2f, _concreteStroke);
        _concreteStroke.StrokeWidth = 2f;

        // Fundament-Querschnitt
        _concreteFill.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Card, 200);
        canvas.DrawRect(ox, oy, fw, fh, _concreteFill);

        // Schraffur im Beton
        SkiaBlueprintCanvas.DrawHatch(canvas, new SKRect(ox, oy, ox + fw, oy + fh), 45f,
            SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 40), 8f);

        _concreteStroke.Color = SkiaThemeHelper.TextSecondary;
        canvas.DrawRect(ox, oy, fw, fh, _concreteStroke);

        // Maßlinien
        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox, oy + fh + 4f), new SKPoint(ox + fw, oy + fh + 4f),
            $"{widthCm:F0} cm", offset: 14f);

        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox - 4f, oy), new SKPoint(ox - 4f, oy + fh),
            $"{depthCm:F0} cm", offset: -14f);

        // Länge + Volumen
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"L={lengthCm / 100f:F1} m | {volumeM3:F3} m³",
            new SKPoint(bounds.MidX, oy - 14f),
            SkiaThemeHelper.Accent, 10f);
    }

    /// <summary>
    /// Säule: Zylinder-Darstellung (2 Ellipsen + 2 Linien).
    /// </summary>
    private static void RenderColumn(SKCanvas canvas, SKRect bounds,
        float diameterCm, float heightCm, float volumeM3)
    {
        if (diameterCm <= 0 || heightCm <= 0) return;

        var fit = SkiaBlueprintCanvas.FitToCanvas(bounds, diameterCm, heightCm, 40f);
        float scale = fit.Scale;
        float cx = fit.OffsetX + diameterCm * scale / 2f;
        float topY = fit.OffsetY;

        float dw = diameterCm * scale;
        float colH = heightCm * scale;
        float ellH = dw * 0.2f; // Ellipsen-Höhe

        // Säulenkörper (Rechteck + halbe Ellipsen)
        _concreteFill.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Card, 180);

        // Seitliche Linien
        canvas.DrawRect(cx - dw / 2f, topY + ellH / 2f, dw, colH - ellH, _concreteFill);

        // Schraffur
        SkiaBlueprintCanvas.DrawHatch(canvas,
            new SKRect(cx - dw / 2f, topY + ellH / 2f, cx + dw / 2f, topY + colH - ellH / 2f),
            60f, SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 30), 8f);

        // Obere Ellipse
        _concreteFill.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Accent, 50);
        var topEllipse = new SKRect(cx - dw / 2f, topY, cx + dw / 2f, topY + ellH);
        canvas.DrawOval(topEllipse, _concreteFill);

        // Umriss
        _concreteStroke.Color = SkiaThemeHelper.TextSecondary;
        canvas.DrawOval(topEllipse, _concreteStroke);
        canvas.DrawLine(cx - dw / 2f, topY + ellH / 2f, cx - dw / 2f, topY + colH - ellH / 2f, _concreteStroke);
        canvas.DrawLine(cx + dw / 2f, topY + ellH / 2f, cx + dw / 2f, topY + colH - ellH / 2f, _concreteStroke);

        // Untere Ellipse (nur untere Hälfte sichtbar)
        var bottomEllipse = new SKRect(cx - dw / 2f, topY + colH - ellH, cx + dw / 2f, topY + colH);
        canvas.Save();
        canvas.ClipRect(new SKRect(cx - dw / 2f, topY + colH - ellH / 2f, cx + dw / 2f, topY + colH + 1f));
        canvas.DrawOval(bottomEllipse, _concreteStroke);
        canvas.Restore();

        // Maßlinien
        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(cx - dw / 2f, topY + colH + 4f), new SKPoint(cx + dw / 2f, topY + colH + 4f),
            $"Ø {diameterCm:F0} cm", offset: 14f);

        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(cx + dw / 2f + 4f, topY + ellH / 2f),
            new SKPoint(cx + dw / 2f + 4f, topY + colH - ellH / 2f),
            $"{heightCm:F0} cm", offset: 14f);

        // Volumen
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"{volumeM3:F3} m³",
            new SKPoint(cx, topY + colH + 28f),
            SkiaThemeHelper.Accent, 11f);
    }

    /// <summary>
    /// Zeichnet eine horizontale Mischverhältnis-Leiste am unteren Rand.
    /// Zeigt Zement : Sand : Kies als farbige Segmente mit Labels.
    /// </summary>
    private static void DrawMixRatioBar(SKCanvas canvas, SKRect bounds,
        float cementParts, float sandParts, float gravelParts)
    {
        float total = cementParts + sandParts + gravelParts;
        if (total <= 0) return;

        float barH = 14f;
        float barLeft = bounds.Left + 20f;
        float barRight = bounds.Right - 20f;
        float barW = barRight - barLeft;
        float barY = bounds.Bottom - 32f;
        float cornerR = barH / 2f;

        var barRect = new SKRect(barLeft, barY, barRight, barY + barH);

        // Clip auf abgerundetes Rechteck
        canvas.Save();
        using var clipPath = new SKPath();
        clipPath.AddRoundRect(barRect, cornerR, cornerR);
        canvas.ClipPath(clipPath);

        float currentX = barLeft;

        // Zement-Segment
        float cW = (cementParts / total) * barW;
        _mixPaint.Color = _cementColor;
        canvas.DrawRect(currentX, barY, cW, barH, _mixPaint);
        currentX += cW;

        // Sand-Segment
        float sW = (sandParts / total) * barW;
        _mixPaint.Color = _sandMixColor;
        canvas.DrawRect(currentX, barY, sW, barH, _mixPaint);
        currentX += sW;

        // Kies-Segment
        float gW = (gravelParts / total) * barW;
        _mixPaint.Color = _gravelMixColor;
        canvas.DrawRect(currentX, barY, gW, barH, _mixPaint);

        canvas.Restore();

        // Rahmen
        _mixStroke.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 100);
        canvas.DrawRoundRect(barRect, cornerR, cornerR, _mixStroke);

        // Labels über den Segmenten
        _mixTextPaint.Color = SkiaThemeHelper.TextSecondary;
        _mixFont.Size = 8f;

        float cCx = barLeft + cW / 2f;
        float sCx = barLeft + cW + sW / 2f;
        float gCx = barLeft + cW + sW + gW / 2f;

        // Verhältnis-Text in den Segmenten (wenn breit genug)
        _mixTextPaint.Color = SKColors.White.WithAlpha(200);
        if (cW > 25f)
            canvas.DrawText("Zement", cCx, barY + barH / 2f + 3f, SKTextAlign.Center, _mixFont, _mixTextPaint);
        if (sW > 25f)
            canvas.DrawText("Sand", sCx, barY + barH / 2f + 3f, SKTextAlign.Center, _mixFont, _mixTextPaint);
        if (gW > 25f)
            canvas.DrawText("Kies", gCx, barY + barH / 2f + 3f, SKTextAlign.Center, _mixFont, _mixTextPaint);

        // Verhältnis-Angabe darunter
        _mixTextPaint.Color = SkiaThemeHelper.TextMuted;
        _mixFont.Size = 9f;
        string ratioText = $"Mischverhältnis {cementParts:F0} : {sandParts:F0} : {gravelParts:F0}";
        canvas.DrawText(ratioText, bounds.MidX, barY + barH + 12f,
            SKTextAlign.Center, _mixFont, _mixTextPaint);
    }
}
