using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Game-Over Bildschirm-Elemente: Großes Score-Display mit animierten Segmenten,
/// Medaillen (Gold/Silber/Bronze), Score-Breakdown Bars, Coin-Counter.
/// </summary>
public static class GameOverVisualization
{
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _barPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _barBg = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _medalPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _medalStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKFont _scoreFont = new() { Size = 42f };
    private static readonly SKFont _labelFont = new() { Size = 12f };
    private static readonly SKFont _valueFont = new() { Size = 14f };
    private static readonly SKFont _medalFont = new() { Size = 10f };

    // Medaillen-Farben
    private static readonly SKColor _gold = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor _silver = new(0xC0, 0xC0, 0xC0);
    private static readonly SKColor _bronze = new(0xCD, 0x7F, 0x32);

    // Score-Breakdown Farben
    private static readonly SKColor _enemyColor = new(0xEF, 0x44, 0x44);   // Rot
    private static readonly SKColor _timeColor = new(0x4C, 0xAF, 0x50);    // Grün
    private static readonly SKColor _effColor = new(0xFF, 0x98, 0x00);      // Orange
    private static readonly SKColor _multColor = new(0x7B, 0x1F, 0xA2);    // Violett
    private static readonly SKColor _coinColor = new(0xFF, 0xD7, 0x00);    // Gold

    /// <summary>
    /// Score-Breakdown Element.
    /// </summary>
    public struct ScoreSegment
    {
        /// <summary>Label (z.B. "Gegner-Punkte").</summary>
        public string Label;

        /// <summary>Punkte-Wert.</summary>
        public int Points;

        /// <summary>Farbe.</summary>
        public SKColor Color;
    }

    /// <summary>
    /// Rendert den großen Score mit Glow-Effekt und optionalem "NEW HIGH SCORE" Label.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="cx">Zentrierte X-Position</param>
    /// <param name="y">Y-Position (Baseline)</param>
    /// <param name="score">Gesamtscore</param>
    /// <param name="isHighScore">Ob neuer Highscore</param>
    /// <param name="animTime">Animations-Zeit für Glow</param>
    public static void DrawBigScore(SKCanvas canvas, float cx, float y,
        int score, bool isHighScore, float animTime, string? newHighScoreText = null)
    {
        string scoreStr = score.ToString("N0");

        // Glow (pulsierend bei Highscore)
        if (isHighScore)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin(animTime * 4f);
            _glowPaint.Color = _gold.WithAlpha((byte)(pulse * 80));
            _glowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 12f);
            _scoreFont.Size = 42f;
            canvas.DrawText(scoreStr, cx, y, SKTextAlign.Center, _scoreFont, _glowPaint);
            _glowPaint.MaskFilter = null;

            // Lokalisierter "NEW HIGH SCORE!" Text darüber
            string highScoreLabel = newHighScoreText ?? "NEW HIGH SCORE!";
            _textPaint.Color = _gold.WithAlpha((byte)(180 + pulse * 75));
            _labelFont.Size = 12f;
            canvas.DrawText(highScoreLabel, cx, y - 36f,
                SKTextAlign.Center, _labelFont, _textPaint);
        }

        // Score-Text
        _textPaint.Color = isHighScore ? _gold : SKColors.White;
        _scoreFont.Size = 42f;
        canvas.DrawText(scoreStr, cx, y, SKTextAlign.Center, _scoreFont, _textPaint);
    }

    /// <summary>
    /// Rendert Score-Breakdown als horizontale Mini-Balken mit Labels.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="segments">Score-Segmente (z.B. Gegner, Zeit, Effizienz, Multiplikator)</param>
    /// <param name="totalScore">Gesamtscore (für Proportions-Berechnung)</param>
    public static void DrawScoreBreakdown(SKCanvas canvas, SKRect bounds,
        ScoreSegment[] segments, int totalScore)
    {
        if (segments.Length == 0 || totalScore <= 0) return;

        float rowH = 22f;
        float barH = 6f;
        float labelW = bounds.Width * 0.45f;
        float barLeft = bounds.Left + labelW;
        float barW = bounds.Width * 0.35f;
        float valueX = barLeft + barW + 8f;

        for (int i = 0; i < segments.Length; i++)
        {
            float y = bounds.Top + i * rowH;
            if (y + rowH > bounds.Bottom) break;

            var seg = segments[i];

            // Label (links)
            _textPaint.Color = seg.Color;
            _labelFont.Size = 11f;
            canvas.DrawText(seg.Label, bounds.Left, y + rowH / 2f + 4f,
                SKTextAlign.Left, _labelFont, _textPaint);

            // Balken-Track
            float barY = y + (rowH - barH) / 2f;
            _barBg.Color = SKColors.White.WithAlpha(15);
            canvas.DrawRoundRect(new SKRect(barLeft, barY, barLeft + barW, barY + barH),
                barH / 2f, barH / 2f, _barBg);

            // Balken-Fill
            float frac = Math.Min((float)seg.Points / totalScore, 1f);
            if (frac > 0)
            {
                float fillW = frac * barW;
                _barPaint.Color = seg.Color;
                canvas.DrawRoundRect(new SKRect(barLeft, barY, barLeft + fillW, barY + barH),
                    barH / 2f, barH / 2f, _barPaint);
            }

            // Wert (rechts)
            _textPaint.Color = SKColors.White.WithAlpha(200);
            _valueFont.Size = 12f;
            canvas.DrawText($"+{seg.Points:N0}", valueX, y + rowH / 2f + 4f,
                SKTextAlign.Left, _valueFont, _textPaint);
        }
    }

    /// <summary>
    /// Rendert eine Medaille (Gold/Silber/Bronze) als Kreis mit Stern.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="cx">Center X</param>
    /// <param name="cy">Center Y</param>
    /// <param name="radius">Medaillen-Radius</param>
    /// <param name="rank">Rang: 1=Gold, 2=Silber, 3=Bronze, sonst keine Medaille</param>
    /// <param name="animTime">Animations-Zeit für Shimmer</param>
    public static void DrawMedal(SKCanvas canvas, float cx, float cy,
        float radius, int rank, float animTime)
    {
        if (rank < 1 || rank > 3) return;

        SKColor color = rank switch
        {
            1 => _gold,
            2 => _silver,
            _ => _bronze
        };

        float shimmer = 0.85f + 0.15f * MathF.Sin(animTime * 3f + rank);

        // Glow
        _glowPaint.Color = color.WithAlpha(40);
        _glowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f);
        canvas.DrawCircle(cx, cy, radius * 1.2f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Medaillen-Körper
        byte mr = (byte)Math.Min(255, (int)(color.Red * shimmer));
        byte mg = (byte)Math.Min(255, (int)(color.Green * shimmer));
        byte mb = (byte)Math.Min(255, (int)(color.Blue * shimmer));
        _medalPaint.Color = new SKColor(mr, mg, mb);
        canvas.DrawCircle(cx, cy, radius, _medalPaint);

        // Rand
        _medalStroke.Color = SKColors.White.WithAlpha(100);
        canvas.DrawCircle(cx, cy, radius, _medalStroke);

        // Highlight (obere Hälfte)
        _medalPaint.Color = SKColors.White.WithAlpha(40);
        canvas.Save();
        canvas.ClipRect(new SKRect(cx - radius, cy - radius, cx + radius, cy));
        canvas.DrawCircle(cx, cy - radius * 0.2f, radius * 0.75f, _medalPaint);
        canvas.Restore();

        // Rang-Text
        _textPaint.Color = SKColors.White.WithAlpha(220);
        _medalFont.Size = radius * 0.8f;
        string rankText = rank switch { 1 => "1st", 2 => "2nd", _ => "3rd" };
        canvas.DrawText(rankText, cx, cy + _medalFont.Size * 0.3f,
            SKTextAlign.Center, _medalFont, _textPaint);
    }

    /// <summary>
    /// Rendert einen animierten Coin-Counter (zählt von 0 hoch).
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="cx">Center X</param>
    /// <param name="y">Y-Position (Baseline)</param>
    /// <param name="coins">Ziel-Coins</param>
    /// <param name="progress">Animations-Fortschritt 0.0-1.0</param>
    public static void DrawCoinCounter(SKCanvas canvas, float cx, float y,
        int coins, float progress)
    {
        int displayCoins = (int)(coins * Math.Clamp(progress, 0f, 1f));
        string coinStr = $"+{displayCoins:N0}";

        // Coin-Icon (kleiner Kreis links vom Text)
        float iconR = 8f;
        float textW = coinStr.Length * 8f; // Ungefähre Textbreite
        float iconX = cx - textW / 2f - iconR - 4f;

        _medalPaint.Color = _coinColor;
        canvas.DrawCircle(iconX, y - 6f, iconR, _medalPaint);
        _medalStroke.Color = new SKColor(0xCC, 0xA0, 0x00);
        canvas.DrawCircle(iconX, y - 6f, iconR, _medalStroke);

        // "C" Symbol in der Münze
        _textPaint.Color = new SKColor(0xCC, 0xA0, 0x00);
        _medalFont.Size = 10f;
        canvas.DrawText("C", iconX, y - 2f, SKTextAlign.Center, _medalFont, _textPaint);

        // Coin-Wert
        _textPaint.Color = _coinColor;
        _valueFont.Size = 16f;
        canvas.DrawText(coinStr, cx + 4f, y, SKTextAlign.Center, _valueFont, _textPaint);
    }
}
