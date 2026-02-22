using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Achievement-Icons: Stilisierte Pixel-Art-Symbole für Achievement-Kacheln.
/// Freigeschaltete Icons haben Glow-Rand + volle Farbe.
/// Gesperrte Icons sind grau + Schloss-Overlay.
/// </summary>
public static class AchievementIconRenderer
{
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _iconPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _progressFont = new() { Size = 9f };

    // Kategorie-Farben
    private static readonly SKColor _progressColor = new(0x22, 0xC5, 0x5E);  // Grün (Fortschritt)
    private static readonly SKColor _masteryColor = new(0xFF, 0xD7, 0x00);   // Gold (Meisterschaft)
    private static readonly SKColor _combatColor = new(0xEF, 0x44, 0x44);    // Rot (Kampf)
    private static readonly SKColor _skillColor = new(0x38, 0xBD, 0xF8);     // Blau (Geschick)
    private static readonly SKColor _challengeColor = new(0xA7, 0x8B, 0xFA);  // Violett (Herausforderung)
    private static readonly SKColor _lockedColor = new(0x55, 0x55, 0x55);    // Grau

    /// <summary>
    /// Rendert ein Achievement-Icon.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="cx">Center X</param>
    /// <param name="cy">Center Y</param>
    /// <param name="size">Icon-Größe (Durchmesser)</param>
    /// <param name="categoryIndex">Kategorie: 0=Progress, 1=Mastery, 2=Combat, 3=Skill, 4=Challenge</param>
    /// <param name="isUnlocked">Ob freigeschaltet</param>
    /// <param name="progress">Fortschritt 0.0-1.0 (für Ring-Anzeige)</param>
    /// <param name="animTime">Animation für Glow</param>
    public static void Render(SKCanvas canvas, float cx, float cy, float size,
        int categoryIndex, bool isUnlocked, float progress = 0, float animTime = 0)
    {
        float r = size / 2f;
        var catColor = GetCategoryColor(categoryIndex);

        if (isUnlocked)
        {
            // === Freigeschaltet: Volle Farbe + Glow ===

            // Glow-Aura (pulsierend)
            float pulse = 0.6f + 0.4f * MathF.Sin(animTime * 3f);
            _glowPaint.Color = catColor.WithAlpha((byte)(pulse * 40));
            _glowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, r * 0.3f);
            canvas.DrawCircle(cx, cy, r * 1.15f, _glowPaint);
            _glowPaint.MaskFilter = null;

            // Hintergrund-Kreis
            _bgPaint.Color = catColor.WithAlpha(200);
            canvas.DrawCircle(cx, cy, r, _bgPaint);

            // Heller Ring
            _strokePaint.Color = catColor.WithAlpha((byte)(120 + pulse * 80));
            _strokePaint.StrokeWidth = 2f;
            canvas.DrawCircle(cx, cy, r, _strokePaint);

            // Highlight (obere Hälfte)
            _iconPaint.Color = SKColors.White.WithAlpha(35);
            canvas.Save();
            canvas.ClipRect(new SKRect(cx - r, cy - r, cx + r, cy));
            canvas.DrawCircle(cx, cy - r * 0.15f, r * 0.8f, _iconPaint);
            canvas.Restore();

            // Trophy-Symbol (vereinfacht)
            DrawTrophy(canvas, cx, cy, r * 0.55f, SKColors.White.WithAlpha(230));
        }
        else
        {
            // === Gesperrt: Grau + Fortschrittsring + Schloss ===

            // Grauer Hintergrund
            _bgPaint.Color = _lockedColor.WithAlpha(100);
            canvas.DrawCircle(cx, cy, r, _bgPaint);

            // Fortschrittsring (wenn progress > 0)
            if (progress > 0)
            {
                // Track
                _strokePaint.Color = _lockedColor.WithAlpha(60);
                _strokePaint.StrokeWidth = 2.5f;
                _strokePaint.StrokeCap = SKStrokeCap.Round;
                var arcRect = new SKRect(cx - r, cy - r, cx + r, cy + r);
                canvas.DrawArc(arcRect, -90, 360, false, _strokePaint);

                // Fortschritt
                _strokePaint.Color = catColor.WithAlpha(140);
                float sweep = progress * 360f;
                canvas.DrawArc(arcRect, -90, sweep, false, _strokePaint);
                _strokePaint.StrokeWidth = 2f;
            }

            // Rand
            _strokePaint.Color = _lockedColor.WithAlpha(80);
            _strokePaint.StrokeWidth = 1.5f;
            canvas.DrawCircle(cx, cy, r, _strokePaint);
            _strokePaint.StrokeWidth = 2f;

            // Schloss-Symbol
            DrawLock(canvas, cx, cy, r * 0.45f);

            // Fortschritts-Text unter dem Icon
            if (progress > 0 && progress < 1f)
            {
                _textPaint.Color = catColor.WithAlpha(180);
                _progressFont.Size = r * 0.4f;
                string pctText = $"{(int)(progress * 100)}%";
                canvas.DrawText(pctText, cx, cy + r + _progressFont.Size + 2f,
                    SKTextAlign.Center, _progressFont, _textPaint);
            }
        }
    }

    /// <summary>
    /// Zeichnet ein vereinfachtes Trophy-Symbol.
    /// </summary>
    private static void DrawTrophy(SKCanvas canvas, float cx, float cy, float size, SKColor color)
    {
        _iconPaint.Color = color;

        // Pokal-Körper (umgekehrtes Trapez)
        float bodyW = size * 1.2f;
        float bodyH = size;
        float topW = bodyW;
        float botW = bodyW * 0.5f;

        using var bodyPath = new SKPath();
        bodyPath.MoveTo(cx - topW / 2f, cy - bodyH / 2f);
        bodyPath.LineTo(cx + topW / 2f, cy - bodyH / 2f);
        bodyPath.LineTo(cx + botW / 2f, cy + bodyH * 0.2f);
        bodyPath.LineTo(cx - botW / 2f, cy + bodyH * 0.2f);
        bodyPath.Close();
        canvas.DrawPath(bodyPath, _iconPaint);

        // Fuß (Dreieck/Trapez)
        float footW = bodyW * 0.7f;
        float footY = cy + bodyH * 0.2f;
        canvas.DrawRect(cx - 2f, footY, 4f, size * 0.3f, _iconPaint);
        canvas.DrawRect(cx - footW / 2f, footY + size * 0.3f, footW, 2f, _iconPaint);

        // Henkel (zwei kleine Bögen)
        _strokePaint.Color = color;
        _strokePaint.StrokeWidth = 1.5f;
        var leftArc = new SKRect(cx - topW / 2f - size * 0.25f, cy - bodyH * 0.3f,
            cx - topW / 2f + size * 0.1f, cy + bodyH * 0.05f);
        using var leftPath = new SKPath();
        leftPath.AddArc(leftArc, 90f, 180f);
        canvas.DrawPath(leftPath, _strokePaint);

        var rightArc = new SKRect(cx + topW / 2f - size * 0.1f, cy - bodyH * 0.3f,
            cx + topW / 2f + size * 0.25f, cy + bodyH * 0.05f);
        using var rightPath = new SKPath();
        rightPath.AddArc(rightArc, -90f, 180f);
        canvas.DrawPath(rightPath, _strokePaint);
        _strokePaint.StrokeWidth = 2f;
    }

    /// <summary>
    /// Zeichnet ein Schloss-Symbol.
    /// </summary>
    private static void DrawLock(SKCanvas canvas, float cx, float cy, float size)
    {
        _iconPaint.Color = _lockedColor;

        // Körper
        float bodyW = size * 1.2f;
        float bodyH = size * 0.8f;
        float bodyY = cy;
        canvas.DrawRoundRect(new SKRect(cx - bodyW / 2f, bodyY, cx + bodyW / 2f, bodyY + bodyH),
            2f, 2f, _iconPaint);

        // Bügel
        _strokePaint.Color = _lockedColor;
        _strokePaint.StrokeWidth = 2f;
        float arcW = bodyW * 0.6f;
        float arcH = size * 0.7f;
        var arcRect = new SKRect(cx - arcW / 2f, bodyY - arcH, cx + arcW / 2f, bodyY);
        using var arcPath = new SKPath();
        arcPath.AddArc(arcRect, 180f, 180f);
        canvas.DrawPath(arcPath, _strokePaint);
    }

    /// <summary>
    /// Farbe pro Achievement-Kategorie.
    /// </summary>
    private static SKColor GetCategoryColor(int categoryIndex)
    {
        return categoryIndex switch
        {
            0 => _progressColor,
            1 => _masteryColor,
            2 => _combatColor,
            3 => _skillColor,
            4 => _challengeColor,
            _ => _progressColor
        };
    }
}
