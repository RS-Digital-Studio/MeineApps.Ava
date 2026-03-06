using System;
using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Zustand der Tab-Bar, wird pro Frame übergeben.
/// </summary>
public struct MedicalTabBarState
{
    /// <summary>Aktuell aktiver Tab (0-3).</summary>
    public int ActiveTab;

    /// <summary>Lokalisierte Tab-Labels (4 Einträge).</summary>
    public string[] Labels;

    /// <summary>Sekunden seit App-Start (für Animationen).</summary>
    public float Time;

    /// <summary>Zeitpunkt des letzten Tab-Wechsels in Sekunden seit Start.</summary>
    public float TabSwitchTime;
}

/// <summary>
/// SkiaSharp-basierter Tab-Bar-Renderer im holografischen Medical-Design.
/// Zeichnet ein Glas-Panel mit Cyan-Glow, 4 prozedurale Vektor-Icons,
/// aktiven Underline-Indikator, Bounce-Animation und Separator-Lines.
/// Ersetzt die XAML-Tab-Bar durch eine visuell ansprechendere Darstellung.
/// </summary>
public sealed class MedicalTabBarRenderer : IDisposable
{
    private bool _disposed;

    // Anzahl der Tabs
    private const int TabCount = 4;

    // =====================================================================
    // Gecachte Paints (keine Allokationen im Render-Loop)
    // =====================================================================

    private readonly SKPaint _bgPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    private readonly SKPaint _edgePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private readonly SKPaint _iconPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _iconStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private readonly SKPaint _labelPaint = new() { IsAntialias = true, TextAlign = SKTextAlign.Center };
    private readonly SKPaint _underlinePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _underlineGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _separatorPaint = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
    private readonly SKMaskFilter _underlineGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

    // Gecachte Pfade für Icons (Reset() statt new pro Frame)
    private readonly SKPath _heartPath = new();
    private readonly SKPath _ekgPath = new();
    private readonly SKPath _chartBarPath = new();
    private readonly SKPath _trendLinePath = new();
    private readonly SKPath _applePath = new();
    private readonly SKPath _cogPath = new();

    // =====================================================================
    // Haupt-Render-Methode
    // =====================================================================

    /// <summary>
    /// Rendert die komplette Tab-Bar auf den Canvas.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas.</param>
    /// <param name="bounds">Verfügbarer Zeichenbereich für die Tab-Bar.</param>
    /// <param name="state">Aktueller Frame-Zustand mit Tabs, Labels, Zeit.</param>
    public void Render(SKCanvas canvas, SKRect bounds, MedicalTabBarState state)
    {
        float tabWidth = bounds.Width / TabCount;

        // 1. Glas-Hintergrund
        DrawBackground(canvas, bounds);

        // 2. Obere Kante (Cyan-Gradient-Linie)
        DrawTopEdge(canvas, bounds);

        // 3. Separator-Lines zwischen Tabs
        DrawSeparators(canvas, bounds, tabWidth);

        // 4. Aktiver Tab: Underline mit Glow
        DrawActiveUnderline(canvas, bounds, tabWidth, state);

        // 5. Icons + Labels pro Tab
        for (int i = 0; i < TabCount; i++)
        {
            float tabCenterX = bounds.Left + tabWidth * i + tabWidth / 2f;
            bool isActive = i == state.ActiveTab;

            // Bounce-Animation bei Tab-Wechsel
            float scale = 1.0f;
            if (isActive)
            {
                float elapsed = state.Time - state.TabSwitchTime;
                if (elapsed < 0.2f)
                {
                    float t = Math.Clamp(elapsed / 0.2f, 0f, 1f);
                    // EaseOutBack: Überschwingt leicht über 1.0
                    float eased = 1f + (t - 1f) * (t - 1f) * (2.70158f * (t - 1f) + 1.70158f);
                    scale = 0.85f + 0.25f * eased;
                }
            }

            // Icon-Position: vertikal zentriert im oberen Bereich
            float iconCenterY = bounds.Top + bounds.Height * 0.35f;

            canvas.Save();
            canvas.Translate(tabCenterX, iconCenterY);
            canvas.Scale(scale);
            canvas.Translate(-tabCenterX, -iconCenterY);

            // Icon zeichnen
            DrawTabIcon(canvas, i, tabCenterX, iconCenterY, 24f, isActive);

            canvas.Restore();

            // Label (nur für aktiven Tab sichtbar)
            if (isActive)
            {
                float labelY = bounds.Bottom - 8f;
                DrawLabel(canvas, state.Labels != null && i < state.Labels.Length
                    ? state.Labels[i] : "", tabCenterX, labelY);
            }
        }
    }

    // =====================================================================
    // Hit-Test
    // =====================================================================

    /// <summary>
    /// Bestimmt welcher Tab bei der gegebenen SkiaSharp-Koordinate getroffen wurde.
    /// </summary>
    /// <param name="bounds">Tab-Bar-Bereich.</param>
    /// <param name="skiaX">X-Koordinate in SkiaSharp-Einheiten.</param>
    /// <param name="skiaY">Y-Koordinate in SkiaSharp-Einheiten.</param>
    /// <returns>Tab-Index (0-3) oder -1 wenn außerhalb.</returns>
    public int HitTest(SKRect bounds, float skiaX, float skiaY)
    {
        if (skiaY < bounds.Top || skiaY > bounds.Bottom) return -1;
        if (skiaX < bounds.Left || skiaX > bounds.Right) return -1;

        float tabWidth = bounds.Width / TabCount;
        int index = (int)((skiaX - bounds.Left) / tabWidth);
        return Math.Clamp(index, 0, TabCount - 1);
    }

    // =====================================================================
    // Hintergrund (Glas-Panel)
    // =====================================================================

    /// <summary>
    /// Zeichnet den halbtransparenten Navy-Hintergrund (Glas-Panel-Effekt).
    /// </summary>
    private void DrawBackground(SKCanvas canvas, SKRect bounds)
    {
        _bgPaint.Color = MedicalColors.TabBarBg.WithAlpha(230);
        canvas.DrawRect(bounds, _bgPaint);
    }

    // =====================================================================
    // Obere Kante (Cyan-Gradient)
    // =====================================================================

    /// <summary>
    /// Zeichnet eine 1px Gradient-Linie am oberen Rand der Tab-Bar.
    /// Cyan Alpha 80 links → Transparent Mitte → Cyan Alpha 80 rechts.
    /// </summary>
    private void DrawTopEdge(SKCanvas canvas, SKRect bounds)
    {
        var cyanEdge = MedicalColors.Cyan.WithAlpha(80);
        var transparent = MedicalColors.Cyan.WithAlpha(0);

        _edgePaint.Shader?.Dispose();
        _edgePaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.Left, bounds.Top),
            new SKPoint(bounds.Right, bounds.Top),
            new[] { cyanEdge, transparent, cyanEdge },
            new[] { 0f, 0.5f, 1f },
            SKShaderTileMode.Clamp);

        canvas.DrawLine(bounds.Left, bounds.Top, bounds.Right, bounds.Top, _edgePaint);

        // Shader freigeben (wird pro Frame neu erstellt - unvermeidbar bei LinearGradient)
        _edgePaint.Shader?.Dispose();
        _edgePaint.Shader = null;
    }

    // =====================================================================
    // Separator-Lines
    // =====================================================================

    /// <summary>
    /// Zeichnet vertikale Trennlinien zwischen den Tabs.
    /// Höhe 60% der Tab-Bar, vertikal zentriert.
    /// </summary>
    private void DrawSeparators(SKCanvas canvas, SKRect bounds, float tabWidth)
    {
        _separatorPaint.Color = MedicalColors.Cyan.WithAlpha(13);

        float lineHeight = bounds.Height * 0.6f;
        float lineTop = bounds.Top + (bounds.Height - lineHeight) / 2f;
        float lineBottom = lineTop + lineHeight;

        for (int i = 1; i < TabCount; i++)
        {
            float x = bounds.Left + tabWidth * i;
            canvas.DrawLine(x, lineTop, x, lineBottom, _separatorPaint);
        }
    }

    // =====================================================================
    // Aktiver Tab Underline + Glow
    // =====================================================================

    /// <summary>
    /// Zeichnet den Cyan-Underline mit Blur-Glow unter dem aktiven Tab-Icon.
    /// Breite: 40% der Tab-Breite, 3px hoch, CornerRadius 1.5.
    /// </summary>
    private void DrawActiveUnderline(SKCanvas canvas, SKRect bounds, float tabWidth,
        MedicalTabBarState state)
    {
        float underlineWidth = tabWidth * 0.4f;
        float underlineHeight = 3f;
        float centerX = bounds.Left + tabWidth * state.ActiveTab + tabWidth / 2f;
        float underlineY = bounds.Top + bounds.Height * 0.6f;

        var underlineRect = new SKRect(
            centerX - underlineWidth / 2f,
            underlineY,
            centerX + underlineWidth / 2f,
            underlineY + underlineHeight);

        var rrect = new SKRoundRect(underlineRect, 1.5f);

        // Glow (breiterer Blur unter der Linie)
        _underlineGlowPaint.Color = MedicalColors.Cyan.WithAlpha(100);
        _underlineGlowPaint.MaskFilter = _underlineGlow;
        canvas.DrawRoundRect(rrect, _underlineGlowPaint);

        // Solide Linie
        _underlinePaint.Color = MedicalColors.Cyan;
        canvas.DrawRoundRect(rrect, _underlinePaint);
    }

    // =====================================================================
    // Label
    // =====================================================================

    /// <summary>
    /// Zeichnet das Textlabel des aktiven Tabs (Cyan, 11pt).
    /// </summary>
    private void DrawLabel(SKCanvas canvas, string text, float centerX, float y)
    {
        if (string.IsNullOrEmpty(text)) return;

        _labelPaint.Color = MedicalColors.Cyan;
        _labelPaint.TextSize = 11f;
        canvas.DrawText(text, centerX, y, _labelPaint);
    }

    // =====================================================================
    // Icon Dispatcher
    // =====================================================================

    /// <summary>
    /// Zeichnet das passende Icon für den gegebenen Tab-Index.
    /// </summary>
    private void DrawTabIcon(SKCanvas canvas, int tabIndex, float cx, float cy,
        float size, bool isActive)
    {
        switch (tabIndex)
        {
            case 0: DrawHeartPulseIcon(canvas, cx, cy, size, isActive); break;
            case 1: DrawChartIcon(canvas, cx, cy, size, isActive); break;
            case 2: DrawFoodIcon(canvas, cx, cy, size, isActive); break;
            case 3: DrawCogIcon(canvas, cx, cy, size, isActive); break;
        }
    }

    // =====================================================================
    // Icon: HeartPulse (Home) - Herz mit EKG-Linie
    // =====================================================================

    /// <summary>
    /// Zeichnet ein Herz mit einer EKG-Zick-Zack-Linie durch die Mitte.
    /// Herz: 2 Bezier-Kurven für obere Hälften, Spitze unten.
    /// EKG: Horizontale Zick-Zack-Linie durch die Herz-Mitte.
    /// </summary>
    private void DrawHeartPulseIcon(SKCanvas canvas, float cx, float cy, float size,
        bool isActive)
    {
        float s = size * 0.5f;
        var color = isActive ? MedicalColors.Cyan : MedicalColors.TextDimmed;

        // Herz-Form via Bezier-Kurven
        _heartPath.Reset();
        _heartPath.MoveTo(cx, cy + s * 0.55f); // Untere Spitze

        // Linke Herz-Hälfte (Bezier nach oben links)
        _heartPath.CubicTo(
            cx - s * 0.05f, cy + s * 0.2f,   // Kontrollpunkt 1
            cx - s * 0.65f, cy + s * 0.15f,  // Kontrollpunkt 2
            cx - s * 0.65f, cy - s * 0.15f); // Endpunkt (oberer Bogen links)

        _heartPath.CubicTo(
            cx - s * 0.65f, cy - s * 0.55f,  // Kontrollpunkt 1
            cx - s * 0.1f, cy - s * 0.6f,    // Kontrollpunkt 2
            cx, cy - s * 0.3f);               // Mitte oben (Kerbe)

        // Rechte Herz-Hälfte (Bezier nach oben rechts, gespiegelt)
        _heartPath.CubicTo(
            cx + s * 0.1f, cy - s * 0.6f,    // Kontrollpunkt 1
            cx + s * 0.65f, cy - s * 0.55f,  // Kontrollpunkt 2
            cx + s * 0.65f, cy - s * 0.15f); // Endpunkt (oberer Bogen rechts)

        _heartPath.CubicTo(
            cx + s * 0.65f, cy + s * 0.15f,  // Kontrollpunkt 1
            cx + s * 0.05f, cy + s * 0.2f,   // Kontrollpunkt 2
            cx, cy + s * 0.55f);              // Zurück zur Spitze

        _heartPath.Close();

        _iconPaint.Color = color;
        canvas.DrawPath(_heartPath, _iconPaint);

        // EKG-Linie durch die Mitte des Herzens
        _ekgPath.Reset();
        float ekgY = cy;
        float left = cx - s * 0.45f;
        float right = cx + s * 0.45f;
        float span = right - left;

        _ekgPath.MoveTo(left, ekgY);
        _ekgPath.LineTo(left + span * 0.25f, ekgY);
        _ekgPath.LineTo(left + span * 0.35f, ekgY - s * 0.25f); // Zacke hoch
        _ekgPath.LineTo(left + span * 0.45f, ekgY + s * 0.35f);  // Zacke runter (QRS)
        _ekgPath.LineTo(left + span * 0.55f, ekgY - s * 0.15f);  // Kleine Zacke
        _ekgPath.LineTo(left + span * 0.65f, ekgY);
        _ekgPath.LineTo(right, ekgY);

        // EKG-Linie in kontrastierender Farbe (dunkler Hintergrund des Herzens)
        var ekgColor = isActive ? MedicalColors.BgDeep : MedicalColors.Surface;
        _iconStrokePaint.Color = ekgColor;
        _iconStrokePaint.StrokeWidth = 1.5f;
        _iconStrokePaint.StrokeCap = SKStrokeCap.Round;
        _iconStrokePaint.StrokeJoin = SKStrokeJoin.Round;
        canvas.DrawPath(_ekgPath, _iconStrokePaint);
    }

    // =====================================================================
    // Icon: Chart (Progress) - 3 Balken + Trendlinie
    // =====================================================================

    /// <summary>
    /// Zeichnet 3 vertikale Balken unterschiedlicher Höhe mit einer
    /// aufsteigenden Trendlinie darüber.
    /// </summary>
    private void DrawChartIcon(SKCanvas canvas, float cx, float cy, float size,
        bool isActive)
    {
        float s = size * 0.5f;
        var color = isActive ? MedicalColors.Cyan : MedicalColors.TextDimmed;

        float baseY = cy + s * 0.5f;
        float barWidth = s * 0.25f;
        float gap = s * 0.1f;

        // 3 Balken (links kurz, mitte mittel, rechts hoch)
        float[] heights = { s * 0.4f, s * 0.65f, s * 0.9f };
        float totalWidth = 3 * barWidth + 2 * gap;
        float startX = cx - totalWidth / 2f;

        _chartBarPath.Reset();
        for (int i = 0; i < 3; i++)
        {
            float barLeft = startX + i * (barWidth + gap);
            float barTop = baseY - heights[i];

            _chartBarPath.AddRoundRect(
                new SKRoundRect(new SKRect(barLeft, barTop, barLeft + barWidth, baseY), 2f));
        }

        _iconPaint.Color = color;
        canvas.DrawPath(_chartBarPath, _iconPaint);

        // Aufsteigende Trendlinie über den Balken
        _trendLinePath.Reset();
        float lineStartX = startX + barWidth * 0.5f;
        float lineEndX = startX + 2 * (barWidth + gap) + barWidth * 0.5f;

        _trendLinePath.MoveTo(lineStartX, baseY - heights[0] - s * 0.12f);
        _trendLinePath.LineTo(startX + (barWidth + gap) + barWidth * 0.5f,
            baseY - heights[1] - s * 0.12f);
        _trendLinePath.LineTo(lineEndX, baseY - heights[2] - s * 0.12f);

        _iconStrokePaint.Color = color;
        _iconStrokePaint.StrokeWidth = 1.5f;
        _iconStrokePaint.StrokeCap = SKStrokeCap.Round;
        _iconStrokePaint.StrokeJoin = SKStrokeJoin.Round;
        canvas.DrawPath(_trendLinePath, _iconStrokePaint);
    }

    // =====================================================================
    // Icon: Food (Apfel-Silhouette)
    // =====================================================================

    /// <summary>
    /// Zeichnet eine Apfel-Silhouette: Runde Form + Stiel oben + kleines Blatt.
    /// </summary>
    private void DrawFoodIcon(SKCanvas canvas, float cx, float cy, float size,
        bool isActive)
    {
        float s = size * 0.5f;
        var color = isActive ? MedicalColors.Cyan : MedicalColors.TextDimmed;

        // Apfel-Körper (2 überlappende Kreise für die typische Apfelform)
        _applePath.Reset();

        // Unterer Teil: Breiter Oval
        float appleBottom = cy + s * 0.45f;
        float appleTop = cy - s * 0.2f;

        // Apfel via Bezier (birnenförmiger Kreis)
        _applePath.MoveTo(cx, appleBottom); // Unten Mitte

        // Rechte Seite
        _applePath.CubicTo(
            cx + s * 0.55f, appleBottom,
            cx + s * 0.6f, cy - s * 0.1f,
            cx + s * 0.35f, appleTop);

        // Obere Kerbe rechts → Mitte
        _applePath.CubicTo(
            cx + s * 0.15f, cy - s * 0.35f,
            cx + s * 0.05f, cy - s * 0.35f,
            cx, cy - s * 0.25f);

        // Obere Kerbe Mitte → links
        _applePath.CubicTo(
            cx - s * 0.05f, cy - s * 0.35f,
            cx - s * 0.15f, cy - s * 0.35f,
            cx - s * 0.35f, appleTop);

        // Linke Seite
        _applePath.CubicTo(
            cx - s * 0.6f, cy - s * 0.1f,
            cx - s * 0.55f, appleBottom,
            cx, appleBottom);

        _applePath.Close();

        _iconPaint.Color = color;
        canvas.DrawPath(_applePath, _iconPaint);

        // Stiel (kleine Linie oben Mitte)
        _iconStrokePaint.Color = color;
        _iconStrokePaint.StrokeWidth = 1.5f;
        _iconStrokePaint.StrokeCap = SKStrokeCap.Round;
        canvas.DrawLine(cx, cy - s * 0.25f, cx + s * 0.05f, cy - s * 0.5f, _iconStrokePaint);

        // Kleines Blatt (schräg rechts vom Stiel)
        _iconStrokePaint.StrokeWidth = 1.2f;

        // Blatt als kleine Bezier-Form
        using var leafPath = new SKPath();
        float leafStartX = cx + s * 0.05f;
        float leafStartY = cy - s * 0.48f;

        leafPath.MoveTo(leafStartX, leafStartY);
        leafPath.CubicTo(
            leafStartX + s * 0.2f, leafStartY - s * 0.15f,
            leafStartX + s * 0.3f, leafStartY - s * 0.05f,
            leafStartX + s * 0.25f, leafStartY + s * 0.1f);

        canvas.DrawPath(leafPath, _iconStrokePaint);
    }

    // =====================================================================
    // Icon: Cog (Zahnrad mit 6 Zähnen)
    // =====================================================================

    /// <summary>
    /// Zeichnet ein Zahnrad: Innerer Kreis + 6 Zähne (rotierte Rechtecke um den Kreis).
    /// </summary>
    private void DrawCogIcon(SKCanvas canvas, float cx, float cy, float size,
        bool isActive)
    {
        float s = size * 0.5f;
        var color = isActive ? MedicalColors.Cyan : MedicalColors.TextDimmed;

        int teeth = 6;
        float outerR = s * 0.55f;
        float innerR = s * 0.38f;
        float toothWidth = MathF.PI * 2f / teeth;
        float toothHalf = toothWidth * 0.3f;

        // Zahnrad-Pfad (äußere Kontur mit Zähnen)
        _cogPath.Reset();

        for (int i = 0; i < teeth; i++)
        {
            float baseAngle = i * toothWidth - MathF.PI / 2f;

            float a1 = baseAngle - toothHalf;
            float a2 = baseAngle + toothHalf;
            float a3 = baseAngle + toothWidth / 2f - toothHalf;
            float a4 = baseAngle + toothWidth / 2f + toothHalf;

            float ox1 = cx + outerR * MathF.Cos(a1);
            float oy1 = cy + outerR * MathF.Sin(a1);
            float ox2 = cx + outerR * MathF.Cos(a2);
            float oy2 = cy + outerR * MathF.Sin(a2);
            float ix1 = cx + innerR * MathF.Cos(a3);
            float iy1 = cy + innerR * MathF.Sin(a3);
            float ix2 = cx + innerR * MathF.Cos(a4);
            float iy2 = cy + innerR * MathF.Sin(a4);

            if (i == 0)
                _cogPath.MoveTo(ox1, oy1);
            else
                _cogPath.LineTo(ox1, oy1);

            _cogPath.LineTo(ox2, oy2);
            _cogPath.LineTo(ix1, iy1);
            _cogPath.LineTo(ix2, iy2);
        }

        _cogPath.Close();

        _iconPaint.Color = color;
        canvas.DrawPath(_cogPath, _iconPaint);

        // Innerer Kreis (Achsen-Loch)
        float holeR = s * 0.15f;
        _iconPaint.Color = MedicalColors.TabBarBg.WithAlpha(230);
        canvas.DrawCircle(cx, cy, holeR, _iconPaint);
    }

    // =====================================================================
    // Dispose (alle nativen Ressourcen freigeben)
    // =====================================================================

    /// <summary>
    /// Gibt alle SkiaSharp-Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Paints
        _bgPaint.Dispose();
        _edgePaint.Dispose();
        _iconPaint.Dispose();
        _iconStrokePaint.Dispose();
        _labelPaint.Dispose();
        _underlinePaint.Dispose();
        _underlineGlowPaint.Dispose();
        _separatorPaint.Dispose();

        // MaskFilter
        _underlineGlow.Dispose();

        // Pfade
        _heartPath.Dispose();
        _ekgPath.Dispose();
        _chartBarPath.Dispose();
        _trendLinePath.Dispose();
        _applePath.Dispose();
        _cogPath.Dispose();
    }
}
