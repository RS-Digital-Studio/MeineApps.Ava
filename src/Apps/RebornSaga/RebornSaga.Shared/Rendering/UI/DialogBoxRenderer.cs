namespace RebornSaga.Rendering.UI;

using SkiaSharp;
using System;

/// <summary>
/// Zeichnet die Dialogbox im Visual Novel Stil: Halbtransparenter Hintergrund,
/// Sprecher-Name oben links, Text im Typewriter-Stil, Weiter-Indikator.
/// </summary>
public static class DialogBoxRenderer
{
    // Gepoolte Paints
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _nameBgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _indicatorPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Gepoolte SKFont für Text-Wrapping
    private static readonly SKFont _dialogFont = new() { LinearMetrics = true };
    private static readonly SKPaint _dialogTextPaint = new() { IsAntialias = true };

    // Gecachter Pfad für Weiter-Indikator (vermeidet new SKPath() pro Frame)
    private static readonly SKPath _indicatorPath = new();

    // Gecachtes Text-Wrapping (nur neu berechnen wenn Text sich ändert)
    private static string? _cachedWrappedSourceText;
    private static string[]? _cachedWrappedWords;

    /// <summary>
    /// Zeichnet die vollständige Dialogbox.
    /// </summary>
    /// <param name="canvas">Canvas zum Zeichnen.</param>
    /// <param name="bounds">Bildschirm-Bounds.</param>
    /// <param name="speakerName">Name des Sprechers (leer = kein Name-Tag).</param>
    /// <param name="text">Sichtbarer Text (vom TypewriterRenderer).</param>
    /// <param name="nameColor">Farbe des Sprecher-Name-Tags.</param>
    /// <param name="showIndicator">Weiter-Indikator anzeigen (pulsierendes Dreieck).</param>
    /// <param name="time">Laufende Zeit für Animationen.</param>
    public static void Render(SKCanvas canvas, SKRect bounds, string speakerName, string text,
        SKColor nameColor, bool showIndicator, float time)
    {
        // Dialogbox: Untere 25% des Bildschirms
        var boxH = bounds.Height * 0.25f;
        var boxY = bounds.Bottom - boxH - bounds.Height * 0.02f;
        var margin = bounds.Width * 0.03f;
        var boxRect = new SKRect(margin, boxY, bounds.Right - margin, bounds.Bottom - bounds.Height * 0.02f);

        // Halbtransparenter Hintergrund
        _bgPaint.Color = new SKColor(0x0D, 0x11, 0x17, 220);
        using var roundRect = new SKRoundRect(boxRect, 10f);
        canvas.DrawRoundRect(roundRect, _bgPaint);

        // Rand (System-Blau, dezent)
        _borderPaint.Color = UIRenderer.Primary.WithAlpha(80);
        canvas.DrawRoundRect(roundRect, _borderPaint);

        // Sprecher-Name (oben links, farbiges Tag)
        if (!string.IsNullOrEmpty(speakerName))
        {
            var nameSize = boxH * 0.13f;
            _dialogFont.Size = nameSize;
            var nameWidth = _dialogFont.MeasureText(speakerName);

            var nameRect = new SKRect(
                boxRect.Left + 15, boxRect.Top - nameSize * 0.6f,
                boxRect.Left + 15 + nameWidth + 20, boxRect.Top + nameSize * 0.6f);

            _nameBgPaint.Color = nameColor.WithAlpha(200);
            using var nameRoundRect = new SKRoundRect(nameRect, 5f);
            canvas.DrawRoundRect(nameRoundRect, _nameBgPaint);

            _dialogTextPaint.Color = SKColors.White;
            canvas.DrawText(speakerName, nameRect.Left + 10, nameRect.MidY + nameSize * 0.35f,
                SKTextAlign.Left, _dialogFont, _dialogTextPaint);
        }

        // Dialog-Text (mit Zeilenumbruch)
        // Ohne Sprecher-Name startet der Text höher (mehr Platz für Text)
        var textSize = boxH * 0.12f;
        var textX = boxRect.Left + 20;
        var textY = string.IsNullOrEmpty(speakerName)
            ? boxRect.Top + boxH * 0.1f  // Kein Name: Text startet weiter oben
            : boxRect.Top + boxH * 0.2f;  // Mit Name: Platz für Name-Tag
        var maxWidth = boxRect.Width - 40;

        DrawWrappedText(canvas, text, textX, textY, textSize, maxWidth, boxRect.Bottom - 15);

        // Weiter-Indikator (pulsierendes Dreieck unten rechts)
        if (showIndicator)
        {
            var indicatorAlpha = (byte)(120 + MathF.Sin(time * 4f) * 80);
            _indicatorPaint.Color = UIRenderer.Primary.WithAlpha(indicatorAlpha);

            var ix = boxRect.Right - 25;
            var iy = boxRect.Bottom - 15;
            var iSize = 5f;

            // Gecachter Pfad für Indikator-Dreieck (Rewind statt new)
            _indicatorPath.Rewind();
            _indicatorPath.MoveTo(ix - iSize, iy - iSize);
            _indicatorPath.LineTo(ix + iSize, iy - iSize);
            _indicatorPath.LineTo(ix, iy + iSize * 0.5f);
            _indicatorPath.Close();
            canvas.DrawPath(_indicatorPath, _indicatorPaint);
        }
    }

    /// <summary>
    /// Zeichnet Text mit automatischem Zeilenumbruch.
    /// </summary>
    private static void DrawWrappedText(SKCanvas canvas, string text, float x, float y,
        float fontSize, float maxWidth, float maxY)
    {
        if (string.IsNullOrEmpty(text)) return;

        _dialogFont.Size = fontSize;
        _dialogTextPaint.Color = UIRenderer.TextPrimary;
        var lineHeight = fontSize * 1.5f;

        // Gecachtes Split-Ergebnis (nur neu berechnen wenn Text sich ändert)
        if (!ReferenceEquals(text, _cachedWrappedSourceText))
        {
            _cachedWrappedSourceText = text;
            _cachedWrappedWords = text.Split(' ');
        }
        var words = _cachedWrappedWords!;
        var currentLine = "";
        var currentY = y;

        foreach (var word in words)
        {
            var testLine = currentLine.Length > 0 ? currentLine + " " + word : word;
            var testWidth = _dialogFont.MeasureText(testLine);

            if (testWidth > maxWidth && currentLine.Length > 0)
            {
                // Zeile ausgeben
                canvas.DrawText(currentLine, x, currentY, SKTextAlign.Left, _dialogFont, _dialogTextPaint);
                currentY += lineHeight;
                currentLine = word;

                // Abbruch wenn über Boxrand hinaus
                if (currentY > maxY) return;
            }
            else
            {
                currentLine = testLine;
            }
        }

        // Letzte Zeile
        if (currentLine.Length > 0 && currentY <= maxY)
            canvas.DrawText(currentLine, x, currentY, SKTextAlign.Left, _dialogFont, _dialogTextPaint);
    }

    /// <summary>
    /// Gibt statische Ressourcen frei.
    /// </summary>
    public static void Cleanup()
    {
        _bgPaint.Dispose();
        _borderPaint.Dispose();
        _nameBgPaint.Dispose();
        _indicatorPaint.Dispose();
        _indicatorPath.Dispose();
        _dialogFont.Dispose();
        _dialogTextPaint.Dispose();
    }
}
