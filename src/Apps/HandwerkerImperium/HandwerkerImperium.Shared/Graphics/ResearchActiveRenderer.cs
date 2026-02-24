using HandwerkerImperium.Models.Enums;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert das aktive Forschungs-Banner als SkiaSharp-Grafik.
/// Animiertes Reagenzglas mit steigendem Flüssigkeitslevel,
/// blubbernde Blasen, aufsteigender Dampf, mechanische Countdown-Ziffern,
/// Funkenregen bei >90% Fortschritt, goldene Abschluss-Partikel.
/// </summary>
public class ResearchActiveRenderer
{
    private float _time;

    // Blasen-Partikel (im Reagenzglas)
    private readonly List<Bubble> _bubbles = [];
    private float _bubbleTimer;

    // Dampf-Partikel (über dem Glas)
    private readonly List<SteamParticle> _steamParticles = [];
    private float _steamTimer;

    // Funken bei >90%
    private readonly List<SparkParticle> _sparks = [];
    private float _sparkTimer;

    // Farben
    private static readonly SKColor GlassColor = new(0x80, 0xC8, 0xFF, 0x40);
    private static readonly SKColor GlassShine = new(0xFF, 0xFF, 0xFF, 0x30);
    private static readonly SKColor LiquidBase = new(0xEA, 0x58, 0x0C);         // Craft-Orange
    private static readonly SKColor LiquidTop = new(0xFF, 0x8A, 0x40);          // Helleres Orange
    private static readonly SKColor SteamColor = new(0xB0, 0xBE, 0xC5, 0x60);
    private static readonly SKColor TextColor = new(0xF5, 0xF0, 0xEB);
    private static readonly SKColor TextDim = new(0xA0, 0x90, 0x80);
    private static readonly SKColor CardBg = new(0x2A, 0x1F, 0x1A);
    private static readonly SKColor CardBorder = new(0x4E, 0x34, 0x2E);
    private static readonly SKColor CountdownBg = new(0x1A, 0x12, 0x0E);

    // Gecachte Paints
    private static readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };

    /// <summary>
    /// Rendert das aktive Forschungs-Banner.
    /// </summary>
    /// <param name="canvas">Canvas zum Zeichnen.</param>
    /// <param name="bounds">Verfügbarer Bereich.</param>
    /// <param name="researchName">Name der aktiven Forschung.</param>
    /// <param name="timeRemaining">Verbleibende Zeit als formatierter String.</param>
    /// <param name="progress">Fortschritt 0.0 bis 1.0.</param>
    /// <param name="branch">Branch der aktiven Forschung.</param>
    /// <param name="deltaTime">Zeitdelta in Sekunden.</param>
    public void Render(SKCanvas canvas, SKRect bounds, string researchName,
        string timeRemaining, float progress, ResearchBranch branch, float deltaTime,
        string? runningLabel = null)
    {
        _time += deltaTime;

        float x = bounds.Left;
        float y = bounds.Top;
        float w = bounds.Width;
        float h = bounds.Height;
        var branchColor = ResearchItemRenderer.GetBranchColor(branch);

        // Karten-Hintergrund
        DrawBackground(canvas, x, y, w, h, branchColor);

        // Reagenzglas (links)
        float glassX = x + 30;
        float glassW = 28;
        float glassH = h * 0.65f;
        float glassY = y + (h - glassH) / 2;
        DrawFlask(canvas, glassX, glassY, glassW, glassH, progress, branchColor, deltaTime);

        // Forschungsname + Countdown (rechts vom Glas)
        float infoX = glassX + glassW + 20;
        float infoW = w - (infoX - x) - 16;
        DrawResearchInfo(canvas, infoX, y, infoW, h, researchName, timeRemaining, progress, branchColor,
            runningLabel ?? "Forschung l\u00e4uft...");

        // Fortschrittsbalken (unten)
        float barY = y + h - 10;
        DrawProgressBar(canvas, x + 10, barY, w - 20, 5, progress, branchColor);

        // Funkenregen bei >90%
        if (progress > 0.9f)
        {
            UpdateAndDrawSparks(canvas, x, y, w, h, branchColor, deltaTime);
        }
        else
        {
            _sparks.Clear();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HINTERGRUND
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawBackground(SKCanvas canvas, float x, float y, float w, float h, SKColor branchColor)
    {
        // Dunkler Karten-Hintergrund
        var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 12);
        _fill.Color = CardBg;
        canvas.DrawRoundRect(rect, _fill);

        // Dezenter Branch-Schimmer
        _fill.Color = branchColor.WithAlpha(10);
        canvas.DrawRoundRect(rect, _fill);

        // Rahmen
        _stroke.Color = branchColor.WithAlpha(80);
        _stroke.StrokeWidth = 1;
        canvas.DrawRoundRect(rect, _stroke);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // REAGENZGLAS
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawFlask(SKCanvas canvas, float x, float y, float w, float h,
        float progress, SKColor branchColor, float deltaTime)
    {
        float cx = x + w / 2;
        float neckW = w * 0.4f;
        float neckH = h * 0.15f;
        float bodyTop = y + neckH;
        float bodyH = h - neckH;

        // Glas-Hals (schmaler oberer Teil)
        _stroke.Color = GlassColor;
        _stroke.StrokeWidth = 2;
        canvas.DrawRect(cx - neckW / 2, y, neckW, neckH, _stroke);

        // Glas-Körper (breiter unterer Teil, abgerundet)
        var bodyRect = new SKRoundRect(new SKRect(x, bodyTop, x + w, y + h), 4, 8);
        canvas.DrawRoundRect(bodyRect, _stroke);

        // Flüssigkeit (Füllstand basierend auf Fortschritt)
        float liquidH = bodyH * Math.Clamp(progress, 0.02f, 0.95f);
        float liquidTop = y + h - liquidH;

        // Leichte Pulsation
        float pulse = MathF.Sin(_time * 1.5f) * 2;
        liquidTop += pulse;

        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(new SKRect(x + 1, bodyTop + 1, x + w - 1, y + h - 1), 3, 7));

        // Flüssigkeits-Gradient (unten dunkler, oben heller)
        _fill.Color = branchColor.WithAlpha(200);
        canvas.DrawRect(x + 1, liquidTop, w - 2, y + h - liquidTop, _fill);

        // Hellere Oberfläche
        _fill.Color = branchColor.WithAlpha(120);
        canvas.DrawRect(x + 1, liquidTop, w - 2, 3, _fill);

        canvas.Restore();

        // Glanzlicht (weißer Streifen links)
        _fill.Color = GlassShine;
        canvas.DrawRect(x + 2, bodyTop + 3, 2, bodyH - 8, _fill);

        // Blubbernde Blasen in der Flüssigkeit
        UpdateAndDrawBubbles(canvas, x, liquidTop, w, y + h - liquidTop, branchColor, deltaTime);

        // Dampf über dem Glas
        UpdateAndDrawSteam(canvas, cx, y, deltaTime);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BLASEN
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateAndDrawBubbles(SKCanvas canvas, float x, float topY, float w,
        float liquidH, SKColor branchColor, float deltaTime)
    {
        _bubbleTimer += deltaTime;

        // Neue Blasen erzeugen
        if (_bubbleTimer >= 0.25f && _bubbles.Count < 8)
        {
            _bubbleTimer = 0;
            _bubbles.Add(new Bubble
            {
                X = x + w * 0.2f + Random.Shared.NextSingle() * w * 0.6f,
                Y = topY + liquidH - 2,
                Size = 1.5f + Random.Shared.NextSingle() * 2.5f,
                Speed = 8 + Random.Shared.NextSingle() * 12,
                WobbleOffset = Random.Shared.NextSingle() * MathF.Tau,
                Life = 1.0f
            });
        }

        // Aktualisieren und zeichnen
        for (int i = _bubbles.Count - 1; i >= 0; i--)
        {
            var b = _bubbles[i];
            b.Y -= b.Speed * deltaTime;
            b.X += MathF.Sin(_time * 3 + b.WobbleOffset) * 0.3f;
            b.Life -= deltaTime * 0.8f;

            if (b.Life <= 0 || b.Y < topY)
            {
                _bubbles.RemoveAt(i);
                continue;
            }

            byte alpha = (byte)(b.Life * 180);
            _fill.Color = SKColors.White.WithAlpha(alpha);
            canvas.DrawCircle(b.X, b.Y, b.Size, _fill);

            // Highlight-Punkt
            _fill.Color = SKColors.White.WithAlpha((byte)(alpha / 2));
            canvas.DrawCircle(b.X - b.Size * 0.3f, b.Y - b.Size * 0.3f, b.Size * 0.3f, _fill);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DAMPF
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateAndDrawSteam(SKCanvas canvas, float cx, float topY, float deltaTime)
    {
        _steamTimer += deltaTime;

        if (_steamTimer >= 0.2f && _steamParticles.Count < 6)
        {
            _steamTimer = 0;
            _steamParticles.Add(new SteamParticle
            {
                X = cx + (Random.Shared.NextSingle() - 0.5f) * 8,
                Y = topY - 2,
                Size = 2 + Random.Shared.NextSingle() * 3,
                Life = 1.0f
            });
        }

        for (int i = _steamParticles.Count - 1; i >= 0; i--)
        {
            var s = _steamParticles[i];
            s.Y -= 12 * deltaTime;
            s.X += MathF.Sin(_time * 2 + i) * 0.5f;
            s.Size += 3 * deltaTime;
            s.Life -= deltaTime * 0.7f;

            if (s.Life <= 0)
            {
                _steamParticles.RemoveAt(i);
                continue;
            }

            _fill.Color = SteamColor.WithAlpha((byte)(s.Life * 80));
            canvas.DrawCircle(s.X, s.Y, s.Size, _fill);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FORSCHUNGS-INFO
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawResearchInfo(SKCanvas canvas, float x, float y, float w, float h,
        string name, string timeRemaining, float progress, SKColor branchColor, string runningLabel)
    {
        // "Aktive Forschung" Label
        using var labelFont = new SKFont { Size = 10 };
        _textPaint.Color = TextDim;
        canvas.DrawText(runningLabel, x, y + 16, labelFont, _textPaint);

        // Forschungsname
        using var nameFont = new SKFont { Size = 15, Embolden = true };
        _textPaint.Color = branchColor;
        string displayName = TruncateText(name, nameFont, w);
        canvas.DrawText(displayName, x, y + 34, nameFont, _textPaint);

        // Countdown-Display (mechanische Ziffern)
        DrawCountdown(canvas, x, y + 42, timeRemaining, branchColor);

        // Prozent-Anzeige
        using var percentFont = new SKFont { Size = 12, Embolden = true };
        _textPaint.Color = branchColor;
        string percentText = $"{(int)(progress * 100)}%";
        canvas.DrawText(percentText, x + w - percentFont.MeasureText(percentText) - 4, y + h - 16, percentFont, _textPaint);
    }

    /// <summary>
    /// Zeichnet den Countdown als mechanische Flip-Clock-Ziffern.
    /// </summary>
    private static void DrawCountdown(SKCanvas canvas, float x, float y, string timeText, SKColor branchColor)
    {
        if (string.IsNullOrEmpty(timeText)) return;

        using var digitFont = new SKFont { Size = 18, Embolden = true };
        float charW = 14;
        float charH = 22;

        float cx = x;
        foreach (char c in timeText)
        {
            if (c == ':')
            {
                // Trennzeichen
                _textPaint.Color = branchColor.WithAlpha(150);
                canvas.DrawText(":", cx + 2, y + 17, digitFont, _textPaint);
                cx += 10;
            }
            else
            {
                // Ziffern-Hintergrund
                _fill.Color = CountdownBg;
                var digitRect = new SKRoundRect(new SKRect(cx, y, cx + charW, y + charH), 3);
                canvas.DrawRoundRect(digitRect, _fill);

                // Trennlinie (Flip-Effekt)
                _stroke.Color = new SKColor(0x30, 0x22, 0x1A);
                _stroke.StrokeWidth = 0.5f;
                canvas.DrawLine(cx, y + charH / 2, cx + charW, y + charH / 2, _stroke);

                // Ziffer
                _textPaint.Color = branchColor;
                canvas.DrawText(c.ToString(), cx + charW / 2, y + 17, SKTextAlign.Center, digitFont, _textPaint);

                cx += charW + 2;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FORTSCHRITTSBALKEN
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawProgressBar(SKCanvas canvas, float x, float y, float w, float h,
        float progress, SKColor branchColor)
    {
        // Hintergrund
        _fill.Color = new SKColor(0x18, 0x10, 0x0C);
        var bgRect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 2);
        canvas.DrawRoundRect(bgRect, _fill);

        // Fortschritt
        float fillW = w * Math.Clamp(progress, 0, 1);
        if (fillW > 1)
        {
            _fill.Color = branchColor;
            var fillRect = new SKRoundRect(new SKRect(x, y, x + fillW, y + h), 2);
            canvas.DrawRoundRect(fillRect, _fill);

            // Glow am Ende
            float glow = 0.5f + MathF.Sin(_time * 4f) * 0.5f;
            _fill.Color = branchColor.WithAlpha((byte)(glow * 150));
            canvas.DrawCircle(x + fillW, y + h / 2, h + 3, _fill);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FUNKEN BEI >90%
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateAndDrawSparks(SKCanvas canvas, float x, float y, float w, float h,
        SKColor branchColor, float deltaTime)
    {
        _sparkTimer += deltaTime;

        if (_sparkTimer >= 0.08f && _sparks.Count < 20)
        {
            _sparkTimer = 0;
            _sparks.Add(new SparkParticle
            {
                X = x + Random.Shared.NextSingle() * w,
                Y = y + h,
                VX = (Random.Shared.NextSingle() - 0.5f) * 40,
                VY = -(30 + Random.Shared.NextSingle() * 50),
                Life = 1.0f,
                Size = 1 + Random.Shared.NextSingle() * 2
            });
        }

        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var p = _sparks[i];
            p.X += p.VX * deltaTime;
            p.Y += p.VY * deltaTime;
            p.VY += 30 * deltaTime;
            p.Life -= deltaTime * 1.5f;

            if (p.Life <= 0)
            {
                _sparks.RemoveAt(i);
                continue;
            }

            // Orange → Gold → verblassend
            byte alpha = (byte)(p.Life * 255);
            byte green = (byte)(0x8B + (1.0f - p.Life) * 0x4C);
            _fill.Color = new SKColor(0xFF, green, 0x00, alpha);
            canvas.DrawCircle(p.X, p.Y, p.Size * p.Life, _fill);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════════

    private static string TruncateText(string text, SKFont font, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (font.MeasureText(text) <= maxWidth) return text;

        for (int i = text.Length - 1; i > 0; i--)
        {
            if (font.MeasureText(text[..i] + "...") <= maxWidth)
                return text[..i] + "...";
        }
        return "...";
    }

    // Partikel-Klassen
    private class Bubble
    {
        public float X, Y, Size, Speed, WobbleOffset, Life;
    }

    private class SteamParticle
    {
        public float X, Y, Size, Life;
    }

    private class SparkParticle
    {
        public float X, Y, VX, VY, Life, Size;
    }
}
