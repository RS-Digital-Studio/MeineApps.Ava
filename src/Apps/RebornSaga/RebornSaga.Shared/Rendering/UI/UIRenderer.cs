namespace RebornSaga.Rendering.UI;

using SkiaSharp;

/// <summary>
/// Zentrale UI-Rendering-Hilfsklasse. Alle Methoden statisch, alle SKPaint/SKFont gepooled.
/// Farben aus dem "Isekai System Blue" Design (AppPalette.axaml).
/// </summary>
public static class UIRenderer
{
    // Farb-Konstanten aus Design-Dokument
    public static readonly SKColor Primary = new(0x4A, 0x90, 0xD9);         // System-Blau
    public static readonly SKColor PrimaryGlow = new(0x58, 0xA6, 0xFF);     // Leuchtendes Blau
    public static readonly SKColor Secondary = new(0x9B, 0x59, 0xB6);       // Mystisch-Lila
    public static readonly SKColor Accent = new(0xF3, 0x9C, 0x12);         // Gold
    public static readonly SKColor Danger = new(0xE7, 0x4C, 0x3C);         // Rot
    public static readonly SKColor Success = new(0x2E, 0xCC, 0x71);        // Grün
    public static readonly SKColor DarkBg = new(0x0D, 0x11, 0x17);         // Haupthintergrund
    public static readonly SKColor PanelBg = new(0x16, 0x1B, 0x22);        // Panel-Hintergrund
    public static readonly SKColor CardBg = new(0x1C, 0x23, 0x33);         // Karten-Hintergrund
    public static readonly SKColor TextPrimary = new(0xE6, 0xED, 0xF3);    // Heller Text
    public static readonly SKColor TextSecondary = new(0x8B, 0x94, 0x9E);  // Gedämpfter Text
    public static readonly SKColor TextMuted = new(0x6E, 0x76, 0x81);      // Sehr gedämpft
    public static readonly SKColor Border = new(0x30, 0x36, 0x3D);         // Rand-Farbe

    // Gepoolte Paints (keine Allokation pro Frame)
    private static readonly SKPaint _panelPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };
    private static readonly SKPaint _barBgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _barFgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _buttonPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Gepoolte SKFont für Text-Rendering (SkiaSharp 3.x API, keine Deprecation-Warnings)
    private static readonly SKFont _font = new() { LinearMetrics = true };

    // Gecachter MaskFilter für Glow-Effekte
    private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

    /// <summary>
    /// Zeichnet ein Panel mit optionalem Glow-Rand (Solo Leveling Stil).
    /// </summary>
    public static void DrawPanel(SKCanvas canvas, SKRect rect, SKColor? bgColor = null, float cornerRadius = 8f, SKColor? glowColor = null)
    {
        var bg = bgColor ?? PanelBg;
        using var roundRect = new SKRoundRect(rect, cornerRadius);

        // Hintergrund
        _panelPaint.Color = bg;
        canvas.DrawRoundRect(roundRect, _panelPaint);

        // Glow-Rand (Solo Leveling Stil)
        if (glowColor.HasValue)
        {
            _glowPaint.Color = glowColor.Value.WithAlpha(100);
            _glowPaint.MaskFilter = _glowFilter;
            canvas.DrawRoundRect(roundRect, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Normaler Rand
        _borderPaint.Color = glowColor ?? Border;
        canvas.DrawRoundRect(roundRect, _borderPaint);
    }

    /// <summary>
    /// Zeichnet einen interaktiven Button mit Hover- und Press-Zuständen.
    /// </summary>
    public static void DrawButton(SKCanvas canvas, SKRect rect, string text, bool isHovered = false, bool isPressed = false, SKColor? color = null, bool disabled = false)
    {
        var btnColor = color ?? Primary;

        if (disabled)
        {
            // Ausgegraut: Sättigung entfernen, Transparenz erhöhen
            var gray = (byte)((btnColor.Red + btnColor.Green + btnColor.Blue) / 3 * 0.4f);
            btnColor = new SKColor(gray, gray, gray, 120);
        }
        else if (isPressed)
            btnColor = new SKColor(
                (byte)(btnColor.Red * 0.7f),
                (byte)(btnColor.Green * 0.7f),
                (byte)(btnColor.Blue * 0.7f));
        else if (isHovered)
            btnColor = new SKColor(
                (byte)Math.Min(255, btnColor.Red + 20),
                (byte)Math.Min(255, btnColor.Green + 20),
                (byte)Math.Min(255, btnColor.Blue + 20));

        using var roundRect = new SKRoundRect(rect, 6f);

        // Button-Hintergrund
        _buttonPaint.Color = btnColor;
        canvas.DrawRoundRect(roundRect, _buttonPaint);

        // Hover-Glow (nicht bei disabled)
        if (isHovered && !disabled)
        {
            _glowPaint.Color = btnColor.WithAlpha(60);
            _glowPaint.MaskFilter = _glowFilter;
            canvas.DrawRoundRect(roundRect, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Text zentriert (bei disabled halbtransparent)
        var textColor = disabled ? new SKColor(255, 255, 255, 80) : SKColors.White;
        DrawText(canvas, text, rect.MidX, rect.MidY, rect.Height * 0.4f, textColor, SKTextAlign.Center, true);
    }

    /// <summary>
    /// Zeichnet Text mit optionaler vertikaler Zentrierung.
    /// Verwendet SKFont API (SkiaSharp 3.x, keine Deprecation-Warnings).
    /// </summary>
    public static void DrawText(SKCanvas canvas, string text, float x, float y, float fontSize,
        SKColor? color = null, SKTextAlign align = SKTextAlign.Left, bool verticalCenter = false)
    {
        _textPaint.Color = color ?? TextPrimary;
        _font.Size = fontSize;

        if (verticalCenter)
        {
            var metrics = _font.Metrics;
            y -= (metrics.Ascent + metrics.Descent) / 2f;
        }

        canvas.DrawText(text, x, y, align, _font, _textPaint);
    }

    /// <summary>
    /// Zeichnet eine Fortschrittsleiste (HP, MP, EXP etc.).
    /// </summary>
    public static void DrawProgressBar(SKCanvas canvas, SKRect rect, float value, float maxValue,
        SKColor fgColor, SKColor? bgColor = null)
    {
        var ratio = maxValue > 0 ? Math.Clamp(value / maxValue, 0f, 1f) : 0f;
        var cornerRadius = rect.Height / 2f;
        using var roundRect = new SKRoundRect(rect, cornerRadius);

        // Hintergrund
        _barBgPaint.Color = bgColor ?? new SKColor(0x20, 0x25, 0x30);
        canvas.DrawRoundRect(roundRect, _barBgPaint);

        // Gefüllter Bereich
        if (ratio > 0)
        {
            var fillRect = new SKRect(rect.Left, rect.Top, rect.Left + rect.Width * ratio, rect.Bottom);
            using var fillRoundRect = new SKRoundRect(fillRect, cornerRadius);
            _barFgPaint.Color = fgColor;
            canvas.DrawRoundRect(fillRoundRect, _barFgPaint);
        }

        // Rand
        _borderPaint.Color = fgColor.WithAlpha(80);
        canvas.DrawRoundRect(roundRect, _borderPaint);
    }

    /// <summary>
    /// Zeichnet zentrierten Text mit Schatten (für Titel etc.).
    /// </summary>
    public static void DrawTextWithShadow(SKCanvas canvas, string text, float x, float y,
        float fontSize, SKColor color, float shadowOffset = 2f)
    {
        _font.Size = fontSize;

        // Schatten
        _textPaint.Color = SKColors.Black.WithAlpha(128);
        canvas.DrawText(text, x + shadowOffset, y + shadowOffset, SKTextAlign.Center, _font, _textPaint);

        // Text
        _textPaint.Color = color;
        canvas.DrawText(text, x, y, SKTextAlign.Center, _font, _textPaint);
    }

    /// <summary>
    /// Prüft ob ein Punkt innerhalb eines Rechtecks liegt (Hit-Test für Touch/Maus).
    /// </summary>
    public static bool HitTest(SKRect rect, SKPoint point) => rect.Contains(point.X, point.Y);

    /// <summary>
    /// Gibt alle statischen nativen Ressourcen frei (Font, MaskFilter, Paints).
    /// Beim Beenden aufrufen um Memory Leaks zu verhindern.
    /// </summary>
    public static void Cleanup()
    {
        _font.Dispose();
        _panelPaint.Dispose();
        _borderPaint.Dispose();
        _glowPaint.Dispose();
        _textPaint.Dispose();
        _barBgPaint.Dispose();
        _barFgPaint.Dispose();
        _buttonPaint.Dispose();
        // _glowFilter ist static readonly — NICHT disposen
    }
}
