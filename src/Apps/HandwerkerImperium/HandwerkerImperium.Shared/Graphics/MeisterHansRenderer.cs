using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// SkiaSharp-Renderer für Meister Hans NPC-Portrait (~120x120).
/// Runder Kopf, gelber Schutzhelm, grauer Bart, freundliche Augen.
/// 4 Mood-Varianten: happy, proud, concerned, excited.
/// Sanftes Idle-Wippen + Blinzel-Animation.
/// </summary>
public static class MeisterHansRenderer
{
    // Gecachte Paints (werden einmal erstellt)
    private static readonly SKPaint SkinPaint = new()
    {
        Color = new SKColor(0xF1, 0xC2, 0x7D), // Warmer Hautton
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint HelmetPaint = new()
    {
        Color = new SKColor(0xFF, 0xC1, 0x07), // Gelber Schutzhelm
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint HelmetHighlightPaint = new()
    {
        Color = new SKColor(0xFF, 0xE0, 0x82), // Helmglanz
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint HelmetShadowPaint = new()
    {
        Color = new SKColor(0xE0, 0xA0, 0x00), // Helm-Schatten
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint BeardPaint = new()
    {
        Color = new SKColor(0x9E, 0x9E, 0x9E), // Grauer Bart
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint BeardDarkPaint = new()
    {
        Color = new SKColor(0x78, 0x78, 0x78), // Dunklerer Bart-Schatten
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint EyeWhitePaint = new()
    {
        Color = SKColors.White,
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint EyePupilPaint = new()
    {
        Color = new SKColor(0x4E, 0x34, 0x2E), // Braune Pupillen
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint EyeHighlightPaint = new()
    {
        Color = new SKColor(0xFF, 0xFF, 0xFF, 0xCC),
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint EyebrowPaint = new()
    {
        Color = new SKColor(0x6D, 0x4C, 0x41), // Braune Augenbrauen
        IsAntialias = true,
        Style = SKPaintStyle.StrokeAndFill,
        StrokeWidth = 2f
    };

    private static readonly SKPaint MouthPaint = new()
    {
        Color = new SKColor(0xC6, 0x28, 0x28), // Mund-Farbe
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint MouthLinePaint = new()
    {
        Color = new SKColor(0x8D, 0x5E, 0x3C), // Mund-Linie
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f,
        StrokeCap = SKStrokeCap.Round
    };

    private static readonly SKPaint NosePaint = new()
    {
        Color = new SKColor(0xE0, 0xAC, 0x69), // Nase etwas dunkler als Haut
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint CheekPaint = new()
    {
        Color = new SKColor(0xFF, 0x8A, 0x65, 0x40), // Wangenröte
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint GlowStarPaint = new()
    {
        Color = new SKColor(0xFF, 0xD7, 0x00, 0xCC), // Gold-Stern
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint ExclamationPaint = new()
    {
        Color = new SKColor(0xFF, 0xC1, 0x07), // Gelbes Ausrufezeichen
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint ShirtPaint = new()
    {
        Color = new SKColor(0x5D, 0x40, 0x37), // Braunes Arbeitshemd
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint CollarPaint = new()
    {
        Color = new SKColor(0xE8, 0xAA, 0x00), // Goldener Kragen/Rand
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f
    };

    // Gecachter MaskFilter für Drop-Shadow
    private static readonly SKMaskFilter ShadowFilter =
        SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);

    // --- Gecachte SKPath-Objekte (GC-frei, Wiederverwendung via Reset()) ---
    private static readonly SKPath _mustachePath = new();
    private static readonly SKPath _mouthPath = new();
    private static readonly SKPath _helmPath = new();
    private static readonly SKPath _helmHighlightPath = new();
    private static readonly SKPath _dropPath = new();
    private static readonly SKPath _starPath = new();

    // --- Gecachte SKPaint-Objekte (Farbe wird vor Nutzung gesetzt) ---
    private static readonly SKPaint _earInnerPaint = new()
    {
        Color = new SKColor(0xE0, 0xAC, 0x69, 0x80),
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint _noseHighlightPaint = new()
    {
        Color = new SKColor(0xFF, 0xDB, 0xAC, 0x60),
        IsAntialias = true
    };

    private static readonly SKPaint _dynamicCheekPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint _blinkPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round
    };

    private static readonly SKPaint _browPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round
    };

    private static readonly SKPaint _teethPaint = new()
    {
        Color = SKColors.White,
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint _ridgePaint = new()
    {
        Color = new SKColor(0xE0, 0xA0, 0x00, 0x80),
        IsAntialias = true,
        Style = SKPaintStyle.Stroke
    };

    private static readonly SKPaint _dynamicFillPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    /// <summary>
    /// Rendert das Meister Hans Portrait in den gegebenen Bereich.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Verfügbarer Bereich (zentriert das Portrait darin)</param>
    /// <param name="mood">Stimmung: "happy", "proud", "concerned", "excited"</param>
    /// <param name="elapsed">Elapsed Sekunden für Animationen</param>
    /// <param name="isBlinking">True wenn Blinzel-Frame aktiv</param>
    public static void Render(SKCanvas canvas, SKRect bounds, string mood, float elapsed, bool isBlinking)
    {
        float size = Math.Min(bounds.Width, bounds.Height);
        float cx = bounds.MidX;
        float cy = bounds.MidY;

        // Idle-Wippen (sanfter Sinus, ~2s Zyklus)
        float bobOffset = MathF.Sin(elapsed * MathF.PI) * size * 0.015f;
        cy += bobOffset;

        float scale = size / 120f; // Normalisiert auf 120px Basis

        canvas.Save();

        // Schulter/Körperansatz
        DrawShoulders(canvas, cx, cy, scale);

        // Kopf (Haut)
        DrawHead(canvas, cx, cy, scale);

        // Bart
        DrawBeard(canvas, cx, cy, scale);

        // Nase
        DrawNose(canvas, cx, cy, scale);

        // Wangen
        DrawCheeks(canvas, cx, cy, scale, mood);

        // Augen (mit Blinzel)
        DrawEyes(canvas, cx, cy, scale, mood, isBlinking);

        // Augenbrauen
        DrawEyebrows(canvas, cx, cy, scale, mood);

        // Mund (mood-abhängig)
        DrawMouth(canvas, cx, cy, scale, mood);

        // Helm
        DrawHelmet(canvas, cx, cy, scale);

        // Mood-spezifische Deko
        DrawMoodDecoration(canvas, cx, cy, scale, mood, elapsed);

        canvas.Restore();
    }

    private static void DrawShoulders(SKCanvas canvas, float cx, float cy, float scale)
    {
        // Breite Schultern / Körperansatz
        float shY = cy + 42 * scale;
        float shW = 52 * scale;
        float shH = 20 * scale;
        var shRect = new SKRect(cx - shW / 2, shY, cx + shW / 2, shY + shH);
        canvas.DrawRoundRect(shRect, 8 * scale, 8 * scale, ShirtPaint);

        // Kragen
        canvas.DrawLine(cx - 18 * scale, shY + 2 * scale, cx + 18 * scale, shY + 2 * scale, CollarPaint);
    }

    private static void DrawHead(SKCanvas canvas, float cx, float cy, float scale)
    {
        // Kopf - leicht ovaler Kreis
        float headR = 32 * scale;
        canvas.DrawOval(cx, cy + 4 * scale, headR, headR * 1.05f, SkinPaint);

        // Ohren
        float earR = 7 * scale;
        canvas.DrawCircle(cx - 30 * scale, cy + 6 * scale, earR, SkinPaint);
        canvas.DrawCircle(cx + 30 * scale, cy + 6 * scale, earR, SkinPaint);
        // Ohr-Innenseite
        canvas.DrawCircle(cx - 30 * scale, cy + 6 * scale, earR * 0.5f, _earInnerPaint);
        canvas.DrawCircle(cx + 30 * scale, cy + 6 * scale, earR * 0.5f, _earInnerPaint);
    }

    private static void DrawBeard(SKCanvas canvas, float cx, float cy, float scale)
    {
        // Buschiger grauer Bart unter dem Mund
        float beardY = cy + 16 * scale;
        float beardW = 28 * scale;
        float beardH = 26 * scale;

        // Haupt-Bart (oval)
        canvas.DrawOval(cx, beardY + beardH * 0.3f, beardW, beardH, BeardPaint);

        // Bart-Schatten (untere Hälfte etwas dunkler)
        canvas.DrawOval(cx, beardY + beardH * 0.5f, beardW * 0.9f, beardH * 0.7f, BeardDarkPaint);

        // Seitenbart (Koteletten)
        canvas.DrawOval(cx - 22 * scale, cy + 10 * scale, 10 * scale, 14 * scale, BeardPaint);
        canvas.DrawOval(cx + 22 * scale, cy + 10 * scale, 10 * scale, 14 * scale, BeardPaint);

        // Schnurrbart
        float mustacheY = cy + 14 * scale;
        _mustachePath.Reset();
        _mustachePath.MoveTo(cx - 18 * scale, mustacheY);
        _mustachePath.QuadTo(cx - 6 * scale, mustacheY - 4 * scale, cx, mustacheY + 2 * scale);
        _mustachePath.QuadTo(cx + 6 * scale, mustacheY - 4 * scale, cx + 18 * scale, mustacheY);
        _mustachePath.QuadTo(cx + 6 * scale, mustacheY + 4 * scale, cx, mustacheY + 2 * scale);
        _mustachePath.QuadTo(cx - 6 * scale, mustacheY + 4 * scale, cx - 18 * scale, mustacheY);
        _mustachePath.Close();
        canvas.DrawPath(_mustachePath, BeardPaint);
    }

    private static void DrawNose(SKCanvas canvas, float cx, float cy, float scale)
    {
        // Knollige Nase
        float noseY = cy + 8 * scale;
        canvas.DrawOval(cx, noseY, 6 * scale, 5 * scale, NosePaint);
        // Nasenrücken-Highlight
        canvas.DrawCircle(cx - 1 * scale, noseY - 2 * scale, 2 * scale, _noseHighlightPaint);
    }

    private static void DrawCheeks(SKCanvas canvas, float cx, float cy, float scale, string mood)
    {
        // Wangenröte (stärker bei happy/excited)
        float cheekAlpha = mood is "happy" or "excited" ? 0.35f : 0.2f;
        _dynamicCheekPaint.Color = new SKColor(0xFF, 0x8A, 0x65, (byte)(cheekAlpha * 255));
        canvas.DrawCircle(cx - 18 * scale, cy + 10 * scale, 7 * scale, _dynamicCheekPaint);
        canvas.DrawCircle(cx + 18 * scale, cy + 10 * scale, 7 * scale, _dynamicCheekPaint);
    }

    private static void DrawEyes(SKCanvas canvas, float cx, float cy, float scale, string mood, bool isBlinking)
    {
        float eyeY = cy - 2 * scale;
        float eyeSpacing = 13 * scale;
        float eyeW, eyeH;

        if (isBlinking)
        {
            // Blinzel: Nur schmale Linie
            eyeW = 6 * scale;
            eyeH = 1.5f * scale;
            _blinkPaint.Color = EyePupilPaint.Color;
            _blinkPaint.StrokeWidth = 2 * scale;
            canvas.DrawLine(cx - eyeSpacing - eyeW / 2, eyeY, cx - eyeSpacing + eyeW / 2, eyeY, _blinkPaint);
            canvas.DrawLine(cx + eyeSpacing - eyeW / 2, eyeY, cx + eyeSpacing + eyeW / 2, eyeY, _blinkPaint);
            return;
        }

        // Augengröße je nach Mood
        switch (mood)
        {
            case "excited":
                eyeW = 8 * scale;
                eyeH = 9 * scale;
                break;
            case "proud":
                eyeW = 7 * scale;
                eyeH = 6 * scale; // Halb-geschlossen
                break;
            case "concerned":
                eyeW = 7 * scale;
                eyeH = 8 * scale;
                break;
            default: // happy
                eyeW = 7 * scale;
                eyeH = 8 * scale;
                break;
        }

        // Augenweiß
        canvas.DrawOval(cx - eyeSpacing, eyeY, eyeW, eyeH, EyeWhitePaint);
        canvas.DrawOval(cx + eyeSpacing, eyeY, eyeW, eyeH, EyeWhitePaint);

        // Pupillen
        float pupilR = 3.5f * scale;
        if (mood == "proud") pupilR = 3f * scale;
        canvas.DrawCircle(cx - eyeSpacing, eyeY + 1 * scale, pupilR, EyePupilPaint);
        canvas.DrawCircle(cx + eyeSpacing, eyeY + 1 * scale, pupilR, EyePupilPaint);

        // Glanzpunkte
        float glintR = 1.5f * scale;
        canvas.DrawCircle(cx - eyeSpacing + 1.5f * scale, eyeY - 1.5f * scale, glintR, EyeHighlightPaint);
        canvas.DrawCircle(cx + eyeSpacing + 1.5f * scale, eyeY - 1.5f * scale, glintR, EyeHighlightPaint);
    }

    private static void DrawEyebrows(SKCanvas canvas, float cx, float cy, float scale, string mood)
    {
        float browY = cy - 12 * scale;
        float eyeSpacing = 13 * scale;

        _browPaint.Color = EyebrowPaint.Color;
        _browPaint.StrokeWidth = 2.5f * scale;

        switch (mood)
        {
            case "concerned":
                // Hochgezogene innere Brauen (besorgt)
                canvas.DrawLine(cx - eyeSpacing - 6 * scale, browY - 1 * scale,
                    cx - eyeSpacing + 6 * scale, browY + 3 * scale, _browPaint);
                canvas.DrawLine(cx + eyeSpacing + 6 * scale, browY - 1 * scale,
                    cx + eyeSpacing - 6 * scale, browY + 3 * scale, _browPaint);
                break;
            case "excited":
                // Hochgezogen (überrascht)
                canvas.DrawLine(cx - eyeSpacing - 6 * scale, browY + 2 * scale,
                    cx - eyeSpacing + 6 * scale, browY - 2 * scale, _browPaint);
                canvas.DrawLine(cx + eyeSpacing - 6 * scale, browY - 2 * scale,
                    cx + eyeSpacing + 6 * scale, browY + 2 * scale, _browPaint);
                break;
            default:
                // Normal/proud: Leicht gebogen
                canvas.DrawLine(cx - eyeSpacing - 6 * scale, browY,
                    cx - eyeSpacing + 6 * scale, browY - 1 * scale, _browPaint);
                canvas.DrawLine(cx + eyeSpacing - 6 * scale, browY - 1 * scale,
                    cx + eyeSpacing + 6 * scale, browY, _browPaint);
                break;
        }
    }

    private static void DrawMouth(SKCanvas canvas, float cx, float cy, float scale, string mood)
    {
        float mouthY = cy + 20 * scale;

        // Bart verdeckt den Mund teilweise - Mund ist über dem Bart sichtbar
        mouthY = cy + 14 * scale;

        switch (mood)
        {
            case "happy":
                // Lächeln
                _mouthPath.Reset();
                _mouthPath.MoveTo(cx - 10 * scale, mouthY);
                _mouthPath.QuadTo(cx, mouthY + 6 * scale, cx + 10 * scale, mouthY);
                canvas.DrawPath(_mouthPath, MouthLinePaint);
                break;

            case "proud":
                // Breites Grinsen
                _mouthPath.Reset();
                _mouthPath.MoveTo(cx - 12 * scale, mouthY - 1 * scale);
                _mouthPath.QuadTo(cx, mouthY + 8 * scale, cx + 12 * scale, mouthY - 1 * scale);
                _mouthPath.QuadTo(cx, mouthY + 4 * scale, cx - 12 * scale, mouthY - 1 * scale);
                _mouthPath.Close();
                canvas.DrawPath(_mouthPath, MouthPaint);
                break;

            case "concerned":
                // Nach unten gezogener Mund
                _mouthPath.Reset();
                _mouthPath.MoveTo(cx - 8 * scale, mouthY + 2 * scale);
                _mouthPath.QuadTo(cx, mouthY - 4 * scale, cx + 8 * scale, mouthY + 2 * scale);
                canvas.DrawPath(_mouthPath, MouthLinePaint);
                break;

            case "excited":
                // Offener Mund (staunend)
                canvas.DrawOval(cx, mouthY + 2 * scale, 7 * scale, 6 * scale, MouthPaint);
                // Zähne oben
                canvas.DrawRect(cx - 4 * scale, mouthY - 2 * scale, 8 * scale, 3 * scale, _teethPaint);
                break;
        }
    }

    private static void DrawHelmet(SKCanvas canvas, float cx, float cy, float scale)
    {
        float helmY = cy - 18 * scale;

        // Helm-Körper (oberer Bogen)
        _helmPath.Reset();
        _helmPath.MoveTo(cx - 34 * scale, helmY + 8 * scale);
        _helmPath.QuadTo(cx - 34 * scale, helmY - 18 * scale, cx, helmY - 22 * scale);
        _helmPath.QuadTo(cx + 34 * scale, helmY - 18 * scale, cx + 34 * scale, helmY + 8 * scale);
        _helmPath.LineTo(cx - 34 * scale, helmY + 8 * scale);
        _helmPath.Close();
        canvas.DrawPath(_helmPath, HelmetPaint);

        // Helm-Rand (Schirm)
        var brimRect = new SKRect(cx - 38 * scale, helmY + 4 * scale, cx + 38 * scale, helmY + 14 * scale);
        canvas.DrawRoundRect(brimRect, 4 * scale, 4 * scale, HelmetPaint);

        // Helm-Schatten auf dem Rand
        var brimShadow = new SKRect(cx - 36 * scale, helmY + 8 * scale, cx + 36 * scale, helmY + 14 * scale);
        canvas.DrawRoundRect(brimShadow, 3 * scale, 3 * scale, HelmetShadowPaint);

        // Helm-Highlight (Glanz oben)
        _helmHighlightPath.Reset();
        _helmHighlightPath.MoveTo(cx - 16 * scale, helmY - 10 * scale);
        _helmHighlightPath.QuadTo(cx, helmY - 20 * scale, cx + 16 * scale, helmY - 10 * scale);
        _helmHighlightPath.QuadTo(cx, helmY - 14 * scale, cx - 16 * scale, helmY - 10 * scale);
        _helmHighlightPath.Close();
        canvas.DrawPath(_helmHighlightPath, HelmetHighlightPaint);

        // Mittelgrat des Helms
        _ridgePaint.StrokeWidth = 2 * scale;
        canvas.DrawLine(cx, helmY - 20 * scale, cx, helmY + 6 * scale, _ridgePaint);
    }

    private static void DrawMoodDecoration(SKCanvas canvas, float cx, float cy, float scale, string mood, float elapsed)
    {
        switch (mood)
        {
            case "proud":
                // Gold-Stern neben dem Kopf
                float starPulse = 0.8f + MathF.Sin(elapsed * 3f) * 0.2f;
                DrawStar(canvas, cx + 40 * scale, cy - 25 * scale, 8 * scale * starPulse, GlowStarPaint);
                DrawStar(canvas, cx - 38 * scale, cy - 20 * scale, 5 * scale * starPulse, GlowStarPaint);
                break;

            case "excited":
                // Ausrufezeichen
                float bounce = MathF.Abs(MathF.Sin(elapsed * 4f)) * 4 * scale;
                float exX = cx + 42 * scale;
                float exY = cy - 28 * scale - bounce;

                _dynamicFillPaint.Color = ExclamationPaint.Color;
                // Strich
                canvas.DrawRoundRect(new SKRect(exX - 2.5f * scale, exY, exX + 2.5f * scale, exY + 14 * scale),
                    2 * scale, 2 * scale, _dynamicFillPaint);
                // Punkt
                canvas.DrawCircle(exX, exY + 18 * scale, 2.5f * scale, _dynamicFillPaint);
                break;

            case "concerned":
                // Schweißtropfen
                float dropY = cy - 24 * scale + MathF.Sin(elapsed * 2f) * 3 * scale;
                _dynamicFillPaint.Color = new SKColor(0x64, 0xB5, 0xF6, 0xCC);
                float dX = cx + 36 * scale;
                _dropPath.Reset();
                _dropPath.MoveTo(dX, dropY - 4 * scale);
                _dropPath.QuadTo(dX + 3 * scale, dropY, dX, dropY + 5 * scale);
                _dropPath.QuadTo(dX - 3 * scale, dropY, dX, dropY - 4 * scale);
                _dropPath.Close();
                canvas.DrawPath(_dropPath, _dynamicFillPaint);
                break;
        }
    }

    /// <summary>
    /// Zeichnet einen 5-zackigen Stern.
    /// </summary>
    private static void DrawStar(SKCanvas canvas, float cx, float cy, float radius, SKPaint paint)
    {
        _starPath.Reset();
        float innerR = radius * 0.4f;

        for (int i = 0; i < 10; i++)
        {
            float angle = MathF.PI / 2f + i * MathF.PI / 5f;
            float r = i % 2 == 0 ? radius : innerR;
            float x = cx + MathF.Cos(angle) * r;
            float y = cy - MathF.Sin(angle) * r;

            if (i == 0)
                _starPath.MoveTo(x, y);
            else
                _starPath.LineTo(x, y);
        }
        _starPath.Close();
        canvas.DrawPath(_starPath, paint);
    }
}
