using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Visuelle Darstellung benötigter Materialmengen als Icon-Reihe.
/// Zeigt z.B. 3 Farbeimer, 5 Zementsäcke, 4 Tapetenrollen als stilisierte Icons.
/// Klein genug für Inline-Darstellung (~32x32dp pro Icon).
/// </summary>
public static class MaterialStackVisualization
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

    private static readonly SKPaint _iconFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _iconStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _labelPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _countFont = new() { Size = 10f };
    private static readonly SKFont _labelFont = new() { Size = 9f };

    /// <summary>
    /// Material-Typ für Icon-Darstellung.
    /// </summary>
    public enum MaterialType
    {
        PaintBucket,    // Farbeimer (zylindrisch)
        CementBag,      // Zementsack (rechteckig)
        WallpaperRoll,  // Tapeterolle (Zylinder liegend)
        TilePack,       // Fliesenpaket (flach rechteckig)
        BoardPack,      // Dielenbündel (gestapelt)
        SoilBag,        // Erdsack (wie Zementsack, braun)
        ScrewBox,       // Schraubenschachtel (klein)
        Panel,          // Platte (dünn, groß)
        Cable,          // Kabelrolle (Ring)
        MetalBar,       // Metallstange (länglich)
    }

    /// <summary>
    /// Ein Material-Icon mit Menge.
    /// </summary>
    public struct MaterialIcon
    {
        /// <summary>Art des Materials.</summary>
        public MaterialType Type;

        /// <summary>Anzahl (wird als "×N" angezeigt).</summary>
        public int Count;

        /// <summary>Bezeichnung (z.B. "Farbe", "Zement").</summary>
        public string Label;

        /// <summary>Farbe des Icons (optional).</summary>
        public SKColor Color;
    }

    /// <summary>
    /// Rendert eine Reihe von Material-Icons mit Mengenangaben.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="items">Material-Icons mit Menge</param>
    public static void Render(SKCanvas canvas, SKRect bounds, MaterialIcon[] items)
    {
        if (items.Length == 0) return;

        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

        // Animation: Icons erscheinen sequentiell
        int visibleItems = (int)Math.Ceiling(items.Length * progress);

        float iconSize = 28f;
        float spacing = 8f;
        float labelH = 14f;
        float totalH = iconSize + labelH + 4f;

        // Zentriert horizontal
        float totalW = items.Length * iconSize + (items.Length - 1) * spacing;
        float startX = bounds.MidX - totalW / 2f;
        float iconY = bounds.MidY - totalH / 2f;

        for (int i = 0; i < visibleItems; i++)
        {
            float cx = startX + i * (iconSize + spacing) + iconSize / 2f;
            float cy = iconY + iconSize / 2f;

            var color = items[i].Color.Alpha > 0
                ? items[i].Color
                : GetDefaultColor(items[i].Type);

            // Icon zeichnen
            DrawMaterialIcon(canvas, cx, cy, iconSize / 2f, items[i].Type, color);

            // Mengenangabe (oben rechts, wenn > 1)
            if (items[i].Count > 1)
            {
                string countText = $"×{items[i].Count}";
                _textPaint.Color = SkiaThemeHelper.TextPrimary;
                _countFont.Size = 10f;
                canvas.DrawText(countText, cx + iconSize / 2f - 2f, iconY + 10f,
                    SKTextAlign.Right, _countFont, _textPaint);
            }

            // Label darunter
            if (!string.IsNullOrEmpty(items[i].Label))
            {
                _labelPaint.Color = SkiaThemeHelper.TextMuted;
                _labelFont.Size = 9f;
                canvas.DrawText(items[i].Label, cx, iconY + iconSize + labelH,
                    SKTextAlign.Center, _labelFont, _labelPaint);
            }
        }
    }

    /// <summary>
    /// Zeichnet ein stilisiertes Material-Icon.
    /// </summary>
    private static void DrawMaterialIcon(SKCanvas canvas, float cx, float cy, float r,
        MaterialType type, SKColor color)
    {
        _iconStroke.Color = SkiaThemeHelper.AdjustBrightness(color, 0.7f);

        switch (type)
        {
            case MaterialType.PaintBucket:
                DrawBucket(canvas, cx, cy, r, color);
                break;
            case MaterialType.CementBag:
            case MaterialType.SoilBag:
                DrawBag(canvas, cx, cy, r, color);
                break;
            case MaterialType.WallpaperRoll:
                DrawRoll(canvas, cx, cy, r, color);
                break;
            case MaterialType.TilePack:
            case MaterialType.BoardPack:
                DrawPack(canvas, cx, cy, r, color);
                break;
            case MaterialType.ScrewBox:
                DrawBox(canvas, cx, cy, r, color);
                break;
            case MaterialType.Panel:
                DrawPanel(canvas, cx, cy, r, color);
                break;
            case MaterialType.Cable:
                DrawCableRoll(canvas, cx, cy, r, color);
                break;
            case MaterialType.MetalBar:
                DrawBar(canvas, cx, cy, r, color);
                break;
        }
    }

    // --- Icon-Zeichenfunktionen ---

    /// <summary>Farbeimer: Trapez mit Henkel.</summary>
    private static void DrawBucket(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float w = r * 1.4f;
        float h = r * 1.6f;
        float topW = w * 0.85f;

        // Eimer-Körper (Trapez)
        using var bucketPath = new SKPath();
        bucketPath.MoveTo(cx - topW / 2f, cy - h / 2f);
        bucketPath.LineTo(cx + topW / 2f, cy - h / 2f);
        bucketPath.LineTo(cx + w / 2f, cy + h / 2f);
        bucketPath.LineTo(cx - w / 2f, cy + h / 2f);
        bucketPath.Close();

        _iconFill.Color = color;
        canvas.DrawPath(bucketPath, _iconFill);
        canvas.DrawPath(bucketPath, _iconStroke);

        // Henkel (Halbkreis oben)
        _iconStroke.StrokeWidth = 1.5f;
        var henkelRect = new SKRect(cx - topW * 0.3f, cy - h / 2f - r * 0.5f,
            cx + topW * 0.3f, cy - h / 2f + r * 0.2f);
        using var henkelPath = new SKPath();
        henkelPath.AddArc(henkelRect, 180f, 180f);
        canvas.DrawPath(henkelPath, _iconStroke);

        // Farb-Highlight (obere Hälfte heller)
        _iconFill.Color = SkiaThemeHelper.AdjustBrightness(color, 1.3f).WithAlpha(80);
        canvas.DrawRect(cx - topW / 2f + 1f, cy - h / 2f + 1f, topW - 2f, h * 0.3f, _iconFill);
    }

    /// <summary>Sack: Rechteck mit gewölbter Oberseite.</summary>
    private static void DrawBag(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float w = r * 1.5f;
        float h = r * 1.6f;

        // Sack-Körper
        var bagRect = new SKRect(cx - w / 2f, cy - h / 2f + r * 0.15f, cx + w / 2f, cy + h / 2f);
        _iconFill.Color = color;
        canvas.DrawRoundRect(bagRect, 3f, 3f, _iconFill);
        canvas.DrawRoundRect(bagRect, 3f, 3f, _iconStroke);

        // Gewölbte Oberseite
        using var topPath = new SKPath();
        topPath.MoveTo(cx - w / 2f, cy - h / 2f + r * 0.15f);
        topPath.QuadTo(cx, cy - h / 2f - r * 0.1f, cx + w / 2f, cy - h / 2f + r * 0.15f);
        _iconFill.Color = SkiaThemeHelper.AdjustBrightness(color, 1.1f);
        canvas.DrawPath(topPath, _iconFill);
        canvas.DrawPath(topPath, _iconStroke);
    }

    /// <summary>Rolle: Zylinder liegend.</summary>
    private static void DrawRoll(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float w = r * 1.6f;
        float h = r * 1.2f;

        // Rollen-Körper (Rechteck)
        var bodyRect = new SKRect(cx - w / 2f, cy - h / 2f, cx + w / 2f - r * 0.3f, cy + h / 2f);
        _iconFill.Color = color;
        canvas.DrawRect(bodyRect, _iconFill);
        canvas.DrawRect(bodyRect, _iconStroke);

        // Rechte Ellipse (sichtbares Ende)
        var endRect = new SKRect(cx + w / 2f - r * 0.6f, cy - h / 2f, cx + w / 2f, cy + h / 2f);
        _iconFill.Color = SkiaThemeHelper.AdjustBrightness(color, 0.85f);
        canvas.DrawOval(endRect, _iconFill);
        canvas.DrawOval(endRect, _iconStroke);
    }

    /// <summary>Paket: Gestapelte flache Rechtecke.</summary>
    private static void DrawPack(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float w = r * 1.5f;
        float layerH = r * 0.4f;
        float gap = 2f;

        for (int i = 0; i < 3; i++)
        {
            float y = cy - r * 0.5f + i * (layerH + gap);
            var layerRect = new SKRect(cx - w / 2f, y, cx + w / 2f, y + layerH);

            _iconFill.Color = i % 2 == 0 ? color : SkiaThemeHelper.AdjustBrightness(color, 1.15f);
            canvas.DrawRoundRect(layerRect, 2f, 2f, _iconFill);
            canvas.DrawRoundRect(layerRect, 2f, 2f, _iconStroke);
        }
    }

    /// <summary>Box: Kleines Rechteck mit Deckel-Linie.</summary>
    private static void DrawBox(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float w = r * 1.3f;
        float h = r * 1.2f;

        var boxRect = new SKRect(cx - w / 2f, cy - h / 2f, cx + w / 2f, cy + h / 2f);
        _iconFill.Color = color;
        canvas.DrawRoundRect(boxRect, 2f, 2f, _iconFill);
        canvas.DrawRoundRect(boxRect, 2f, 2f, _iconStroke);

        // Deckel-Linie
        canvas.DrawLine(cx - w / 2f, cy - h / 2f + h * 0.25f,
            cx + w / 2f, cy - h / 2f + h * 0.25f, _iconStroke);
    }

    /// <summary>Platte: Dünnes breites Rechteck.</summary>
    private static void DrawPanel(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float w = r * 1.8f;
        float h = r * 0.6f;

        var panelRect = new SKRect(cx - w / 2f, cy - h / 2f, cx + w / 2f, cy + h / 2f);
        _iconFill.Color = color;
        canvas.DrawRoundRect(panelRect, 1f, 1f, _iconFill);
        canvas.DrawRoundRect(panelRect, 1f, 1f, _iconStroke);

        // Dicke-Kante (3D-Effekt)
        _iconFill.Color = SkiaThemeHelper.AdjustBrightness(color, 0.8f);
        canvas.DrawRect(cx - w / 2f, cy + h / 2f, w, 3f, _iconFill);
    }

    /// <summary>Kabelrolle: Ring/Donut.</summary>
    private static void DrawCableRoll(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float outerR = r * 0.8f;
        float innerR = outerR * 0.45f;

        // Äußerer Ring
        _iconFill.Color = color;
        canvas.DrawCircle(cx, cy, outerR, _iconFill);
        canvas.DrawCircle(cx, cy, outerR, _iconStroke);

        // Inneres Loch
        _iconFill.Color = SkiaThemeHelper.Background;
        canvas.DrawCircle(cx, cy, innerR, _iconFill);
        canvas.DrawCircle(cx, cy, innerR, _iconStroke);
    }

    /// <summary>Metallstange: Längliches Rechteck.</summary>
    private static void DrawBar(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float w = r * 1.8f;
        float h = r * 0.4f;

        var barRect = new SKRect(cx - w / 2f, cy - h / 2f, cx + w / 2f, cy + h / 2f);

        // Gradient für metallischen Look
        _iconFill.Shader = SKShader.CreateLinearGradient(
            new SKPoint(cx, cy - h / 2f),
            new SKPoint(cx, cy + h / 2f),
            new[] { SkiaThemeHelper.AdjustBrightness(color, 1.4f), color },
            null, SKShaderTileMode.Clamp);
        canvas.DrawRoundRect(barRect, h / 2f, h / 2f, _iconFill);
        _iconFill.Shader = null;
        canvas.DrawRoundRect(barRect, h / 2f, h / 2f, _iconStroke);
    }

    /// <summary>
    /// Standard-Farbe pro Material-Typ.
    /// </summary>
    private static SKColor GetDefaultColor(MaterialType type)
    {
        return type switch
        {
            MaterialType.PaintBucket => new SKColor(0x3B, 0x82, 0xF6), // Blau
            MaterialType.CementBag => new SKColor(0x94, 0xA3, 0xB8), // Grau
            MaterialType.WallpaperRoll => new SKColor(0xF5, 0x9E, 0x0B), // Amber
            MaterialType.TilePack => new SKColor(0xD9, 0x77, 0x06), // Orange
            MaterialType.BoardPack => new SKColor(0x92, 0x40, 0x0E), // Braun
            MaterialType.SoilBag => new SKColor(0x78, 0x35, 0x0F), // Dunkelbraun
            MaterialType.ScrewBox => new SKColor(0x64, 0x74, 0x8B), // Stahl
            MaterialType.Panel => new SKColor(0xE2, 0xE8, 0xF0), // Hellgrau
            MaterialType.Cable => new SKColor(0xEF, 0x44, 0x44), // Rot
            MaterialType.MetalBar => new SKColor(0x71, 0x71, 0x7A), // Zink
            _ => new SKColor(0x94, 0xA3, 0xB8)
        };
    }
}
