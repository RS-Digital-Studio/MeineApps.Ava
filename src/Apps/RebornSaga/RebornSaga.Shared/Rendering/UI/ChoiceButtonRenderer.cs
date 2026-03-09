namespace RebornSaga.Rendering.UI;

using SkiaSharp;
using System;
using System.Collections.Generic;

/// <summary>
/// Zeichnet 2-4 Antwort-Buttons für Story-Verzweigungen.
/// Vertikal angeordnet mit Hover/Tap-Feedback und optionalen Tags.
/// </summary>
public static class ChoiceButtonRenderer
{
    // Gepoolte Paints
    private static readonly SKPaint _choiceBgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _choiceBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _tagPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Gecachte Nummer-Strings (vermeidet per-Frame String-Interpolation)
    private static readonly string[] _numberLabels = { "1.", "2.", "3.", "4." };

    // Gepoolte SKFont
    private static readonly SKFont _choiceFont = new() { LinearMetrics = true };
    private static readonly SKFont _tagFont = new() { LinearMetrics = true };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };

    // Von CalculateRects gesetzt, von DrawChoice gelesen (gleiche Render-Phase)
    private static float _cachedBaseFontSize;
    private static float _cachedLineH;

    /// <summary>
    /// Berechnet die Rects für 2-4 Choice-Buttons und gibt sie zurück.
    /// Dynamische Höhe basierend auf Textlänge (mehrzeilig bei langen Texten).
    /// </summary>
    public static SKRect[] CalculateRects(SKRect bounds, int count, string[]? labels = null)
    {
        count = Math.Clamp(count, 1, 4);
        var rects = new SKRect[count];

        var btnW = bounds.Width * 0.7f;
        var minBtnH = bounds.Height * 0.055f;
        var gap = bounds.Height * 0.015f;

        // Verfügbare Textbreite (abzgl. Nummern-Label + Padding)
        var baseFontSize = minBtnH * 0.38f;
        var numFontSize = baseFontSize * 0.8f;
        _choiceFont.Size = numFontSize;
        var numWidth = _choiceFont.MeasureText("1.");
        var textPadding = 10 + numWidth + 8 + 10; // links + Nummer + Abstand + rechts
        var availTextW = btnW - textPadding;

        // Höhe pro Button berechnen (basierend auf Zeilenanzahl)
        var heights = new float[count];
        _choiceFont.Size = baseFontSize;
        var lineH = baseFontSize * 1.3f;

        // Für DrawChoice cachen (gleiche Render-Phase)
        _cachedBaseFontSize = baseFontSize;
        _cachedLineH = lineH;

        for (int i = 0; i < count; i++)
        {
            if (labels != null && i < labels.Length && !string.IsNullOrEmpty(labels[i]))
            {
                var lines = WrapText(labels[i], availTextW, baseFontSize);
                heights[i] = Math.Max(minBtnH, lineH * lines + baseFontSize * 0.8f);
            }
            else
            {
                heights[i] = minBtnH;
            }
        }

        // Gesamthöhe berechnen
        var totalH = 0f;
        for (int i = 0; i < count; i++)
            totalH += heights[i];
        totalH += (count - 1) * gap;

        // Zentriert über der Dialogbox (ab ~45% der Höhe)
        var startY = bounds.Height * 0.45f - totalH / 2f;

        var y = startY;
        for (int i = 0; i < count; i++)
        {
            rects[i] = new SKRect(
                bounds.MidX - btnW / 2, y,
                bounds.MidX + btnW / 2, y + heights[i]);
            y += heights[i] + gap;
        }

        return rects;
    }

    /// <summary>
    /// Berechnet die Anzahl Zeilen die ein Text bei gegebener Breite braucht.
    /// </summary>
    private static int WrapText(string text, float maxWidth, float fontSize)
    {
        _choiceFont.Size = fontSize;
        var totalWidth = _choiceFont.MeasureText(text);
        if (totalWidth <= maxWidth) return 1;

        // Wort-weise umbrechen
        var words = text.Split(' ');
        int lines = 1;
        float currentLineWidth = 0;
        var spaceWidth = _choiceFont.MeasureText(" ");

        foreach (var word in words)
        {
            var wordWidth = _choiceFont.MeasureText(word);
            if (currentLineWidth > 0 && currentLineWidth + spaceWidth + wordWidth > maxWidth)
            {
                lines++;
                currentLineWidth = wordWidth;
            }
            else
            {
                currentLineWidth += (currentLineWidth > 0 ? spaceWidth : 0) + wordWidth;
            }
        }

        return lines;
    }

    /// <summary>
    /// Zeichnet alle Choice-Buttons.
    /// </summary>
    /// <param name="canvas">Canvas zum Zeichnen.</param>
    /// <param name="rects">Button-Rects (von CalculateRects).</param>
    /// <param name="labels">Button-Texte.</param>
    /// <param name="tags">Optionale Tags (z.B. "[Karma+]", "[Aria +]"). Null-Einträge = kein Tag.</param>
    /// <param name="hoveredIndex">Index des gehoverten Buttons (-1 = keiner).</param>
    /// <param name="pressedIndex">Index des gedrückten Buttons (-1 = keiner).</param>
    /// <param name="disabledIndices">Indices der deaktivierten Buttons (z.B. fehlende Bedingung).</param>
    /// <param name="time">Laufende Zeit für Animationen.</param>
    public static void Render(SKCanvas canvas, SKRect[] rects, string[] labels,
        string?[]? tags, int hoveredIndex, int pressedIndex,
        HashSet<int>? disabledIndices, float time)
    {
        for (int i = 0; i < rects.Length && i < labels.Length; i++)
        {
            var rect = rects[i];
            var isHovered = i == hoveredIndex;
            var isPressed = i == pressedIndex;
            var isDisabled = disabledIndices?.Contains(i) ?? false;

            var tag = tags != null && i < tags.Length ? tags[i] : null;
            DrawChoice(canvas, rect, labels[i], tag,
                isHovered, isPressed, isDisabled, i, time);
        }
    }

    private static void DrawChoice(SKCanvas canvas, SKRect rect, string label, string? tag,
        bool isHovered, bool isPressed, bool isDisabled, int index, float time)
    {
        using var roundRect = new SKRoundRect(rect, 8f);

        // Hintergrund
        if (isDisabled)
            _choiceBgPaint.Color = new SKColor(0x15, 0x18, 0x20, 180);
        else if (isPressed)
            _choiceBgPaint.Color = UIRenderer.Primary.WithAlpha(60);
        else if (isHovered)
            _choiceBgPaint.Color = new SKColor(0x1C, 0x23, 0x33, 230);
        else
            _choiceBgPaint.Color = new SKColor(0x16, 0x1B, 0x22, 210);

        canvas.DrawRoundRect(roundRect, _choiceBgPaint);

        // Rand (leichtes Pulsieren wenn hovered)
        var borderAlpha = isHovered ? (byte)(100 + MathF.Sin(time * 3f + index) * 40) : (byte)60;
        _choiceBorderPaint.Color = isDisabled
            ? UIRenderer.TextMuted.WithAlpha(40)
            : UIRenderer.Primary.WithAlpha(borderAlpha);
        canvas.DrawRoundRect(roundRect, _choiceBorderPaint);

        // Schriftgröße aus CalculateRects (konsistent für Höhe und Zeichnung)
        var fontSize = _cachedBaseFontSize;
        var numFontSize = fontSize * 0.8f;
        var lineH = _cachedLineH;

        // Nummer-Indikator links (gecachter String)
        _choiceFont.Size = numFontSize;
        _textPaint.Color = UIRenderer.Primary.WithAlpha(isDisabled ? (byte)40 : (byte)120);
        var numLabel = index < _numberLabels.Length ? _numberLabels[index] : $"{index + 1}.";
        var numWidth = _choiceFont.MeasureText(numLabel);

        // Nummer vertikal zentriert zeichnen
        canvas.DrawText(numLabel, rect.Left + 10, rect.MidY + numFontSize * 0.35f,
            SKTextAlign.Left, _choiceFont, _textPaint);

        // Text nach der Nummer mit Wortumbruch
        _choiceFont.Size = fontSize;
        _textPaint.Color = isDisabled ? UIRenderer.TextMuted : UIRenderer.TextPrimary;
        var textX = rect.Left + 10 + numWidth + 8;
        var availWidth = rect.Right - textX - 10;

        // Clipping damit Text nicht überläuft
        canvas.Save();
        canvas.ClipRect(rect);

        // Zeilen aufteilen
        var totalWidth = _choiceFont.MeasureText(label);
        if (totalWidth <= availWidth)
        {
            // Einzeilig: vertikal zentriert
            canvas.DrawText(label, textX, rect.MidY + fontSize * 0.35f,
                SKTextAlign.Left, _choiceFont, _textPaint);
        }
        else
        {
            // Mehrzeilig: Wort-weise umbrechen
            var words = label.Split(' ');
            var lines = new List<string>();
            var currentLine = "";
            var spaceWidth = _choiceFont.MeasureText(" ");

            foreach (var word in words)
            {
                var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                if (_choiceFont.MeasureText(testLine) > availWidth && !string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }
            if (!string.IsNullOrEmpty(currentLine))
                lines.Add(currentLine);

            // Vertikal zentriert zeichnen
            var totalTextH = lines.Count * lineH;
            var startY = rect.MidY - totalTextH / 2f + fontSize * 0.85f;

            for (int l = 0; l < lines.Count; l++)
            {
                canvas.DrawText(lines[l], textX, startY + l * lineH,
                    SKTextAlign.Left, _choiceFont, _textPaint);
            }
        }

        canvas.Restore();

        // Tag rechts (z.B. "[Karma+]")
        if (!string.IsNullOrEmpty(tag))
        {
            var tagSize = fontSize * 0.75f;
            _tagFont.Size = tagSize;
            var tagWidth = _tagFont.MeasureText(tag);

            var tagRect = new SKRect(
                rect.Right - tagWidth - 25, rect.MidY - tagSize * 0.7f,
                rect.Right - 10, rect.MidY + tagSize * 0.7f);

            // Tag-Farbe basierend auf Inhalt
            var tagColor = tag.Contains("Karma") ? UIRenderer.Success
                : tag.Contains("Affinität") || tag.Contains("+") ? UIRenderer.Accent
                : tag.Contains("STR") || tag.Contains("INT") || tag.Contains("Check") ? UIRenderer.Secondary
                : UIRenderer.TextMuted;

            _tagPaint.Color = tagColor.WithAlpha(30);
            using var tagRoundRect = new SKRoundRect(tagRect, 4f);
            canvas.DrawRoundRect(tagRoundRect, _tagPaint);

            _textPaint.Color = tagColor;
            canvas.DrawText(tag, tagRect.MidX, tagRect.MidY + tagSize * 0.35f,
                SKTextAlign.Center, _tagFont, _textPaint);
        }
    }

    /// <summary>
    /// Gibt statische Ressourcen frei.
    /// </summary>
    public static void Cleanup()
    {
        _choiceBgPaint.Dispose();
        _choiceBorderPaint.Dispose();
        _tagPaint.Dispose();
        _choiceFont.Dispose();
        _tagFont.Dispose();
        _textPaint.Dispose();
    }
}
