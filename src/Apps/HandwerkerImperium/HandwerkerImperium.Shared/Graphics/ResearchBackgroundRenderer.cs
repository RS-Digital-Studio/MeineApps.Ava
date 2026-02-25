using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert einen warmen Werkstatt-Hintergrund hinter dem Forschungsbaum.
/// Dunkles Holz/Leder-Thema mit subtilen Maserungslinien und Zahnrad-Wasserzeichen.
/// Alle SKPaint-Objekte sind static readonly für GC-freie Performance im Render-Loop.
/// </summary>
public class ResearchBackgroundRenderer
{
    // ═══════════════════════════════════════════════════════════════════════
    // FARBEN (warme Werkstatt-Palette)
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly SKColor WoodBg = new(0x1C, 0x14, 0x0E);              // Dunkles Nussholz
    private static readonly SKColor WoodBgLight = new(0x24, 0x1A, 0x12);          // Leicht heller für Maserung
    private static readonly SKColor GridLineColor = new(0x3A, 0x2A, 0x1E, 0x20);  // Warme braune Grid-Linien (~12% Alpha)
    private static readonly SKColor GrainLineColor = new(0x30, 0x22, 0x16, 0x18); // Holzmaserung
    private static readonly SKColor GearColor = new(0x8B, 0x5E, 0x3C, 0x28);      // Zahnrad-Wasserzeichen (~16% Alpha)
    private static readonly SKColor GearHighlight = new(0xD9, 0x77, 0x06, 0x18);  // Leichter Craft-Orange-Hauch auf Zahnrädern

    // ═══════════════════════════════════════════════════════════════════════
    // GECACHTE PAINTS (static readonly, keine Allokationen pro Frame)
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly SKPaint _bgPaint = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Fill,
        Color = WoodBg
    };

    private static readonly SKPaint _bgLightPaint = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Fill,
        Color = WoodBgLight
    };

    private static readonly SKPaint _gridPaint = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 0.5f,
        Color = GridLineColor
    };

    private static readonly SKPaint _grainPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 0.8f,
        Color = GrainLineColor
    };

    private static readonly SKPaint _gearPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        Color = GearColor
    };

    private static readonly SKPaint _gearHighlightPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1.2f,
        Color = GearHighlight
    };

    private static readonly SKPaint _vignettePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    // Gecachte Zahnrad-Pfade (werden einmal erstellt, bei Bounds-Änderung neu berechnet)
    private SKPath? _gear1Path;
    private SKPath? _gear2Path;
    private SKPath? _gear3Path;
    private SKPath? _gear4Path;
    private float _lastBoundsWidth;
    private float _lastBoundsHeight;

    // Gecachter Vignette-Shader (wird bei Bounds-Änderung neu erstellt)
    private SKShader? _vignetteShader;
    private float _lastVignetteW;
    private float _lastVignetteH;

    // Gecachter Maserung-Pfad
    private SKPath? _grainPath;
    private float _lastGrainW;
    private float _lastGrainH;

    // ═══════════════════════════════════════════════════════════════════════
    // KONSTANTEN
    // ═══════════════════════════════════════════════════════════════════════

    private const float GridSpacing = 24f;          // Raster (24px)
    private const int GearTeeth = 10;               // 10 Zähne pro Zahnrad
    private const int GrainLineCount = 18;          // Anzahl Maserungslinien

    /// <summary>
    /// Rendert den Werkstatt-Hintergrund.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds)
    {
        // 1. Dunkler Holz-Hintergrund
        DrawBackground(canvas, bounds);

        // 2. Holzmaserung (subtile organische Linien)
        DrawWoodGrain(canvas, bounds);

        // 3. Feines Grid-Raster
        DrawGrid(canvas, bounds);

        // 4. Zahnrad-Wasserzeichen (4 Zahnräder, größer und sichtbarer)
        DrawGearWatermarks(canvas, bounds);

        // 5. Warme Vignette für Tiefenwirkung
        DrawVignette(canvas, bounds);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HINTERGRUND
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawBackground(SKCanvas canvas, SKRect bounds)
    {
        canvas.DrawRect(bounds, _bgPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HOLZMASERUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet subtile Holzmaserungslinien (organische Sinus-Wellenlinien).
    /// Pfad wird gecacht und nur bei Bounds-Änderung neu berechnet.
    /// </summary>
    private void DrawWoodGrain(SKCanvas canvas, SKRect bounds)
    {
        float w = bounds.Width;
        float h = bounds.Height;

        if (_grainPath == null || MathF.Abs(w - _lastGrainW) > 1f || MathF.Abs(h - _lastGrainH) > 1f)
        {
            _grainPath?.Dispose();
            _grainPath = BuildGrainPath(bounds);
            _lastGrainW = w;
            _lastGrainH = h;
        }

        canvas.DrawPath(_grainPath, _grainPaint);
    }

    /// <summary>
    /// Erstellt den Maserungspfad mit Sinus-Wellenlinien.
    /// Deterministisch, kein Random.
    /// </summary>
    private static SKPath BuildGrainPath(SKRect bounds)
    {
        var path = new SKPath();
        float spacing = bounds.Height / (GrainLineCount + 1);

        for (int i = 0; i < GrainLineCount; i++)
        {
            float baseY = bounds.Top + spacing * (i + 1);
            // Verschiedene Frequenzen/Amplituden für natürlichen Look
            float freq = 0.006f + i * 0.001f;
            float amp = 3f + (i % 3) * 2f;
            float phase = i * 1.7f; // Deterministischer Versatz

            path.MoveTo(bounds.Left, baseY + MathF.Sin(phase) * amp);

            for (float x = bounds.Left + 4; x <= bounds.Right; x += 4)
            {
                float y = baseY + MathF.Sin(x * freq + phase) * amp
                                + MathF.Sin(x * freq * 2.3f + phase * 0.7f) * (amp * 0.3f);
                path.LineTo(x, y);
            }
        }

        return path;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GRID-RASTER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet ein feines Raster mit sehr subtilen warmen Linien.
    /// </summary>
    private static void DrawGrid(SKCanvas canvas, SKRect bounds)
    {
        // Vertikale Linien
        for (float x = bounds.Left; x <= bounds.Right; x += GridSpacing)
        {
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, _gridPaint);
        }

        // Horizontale Linien
        for (float y = bounds.Top; y <= bounds.Bottom; y += GridSpacing)
        {
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, _gridPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ZAHNRAD-WASSERZEICHEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet 4 Zahnrad-Silhouetten als Wasserzeichen.
    /// Größer und sichtbarer als vorher, mit warmem Craft-Orange-Akzent.
    /// </summary>
    private void DrawGearWatermarks(SKCanvas canvas, SKRect bounds)
    {
        float w = bounds.Width;
        float h = bounds.Height;

        if (_gear1Path == null || MathF.Abs(w - _lastBoundsWidth) > 1f || MathF.Abs(h - _lastBoundsHeight) > 1f)
        {
            RebuildGearPaths(bounds);
            _lastBoundsWidth = w;
            _lastBoundsHeight = h;
        }

        // Zahnräder zeichnen (Füllung + dezenter Highlight-Rand)
        if (_gear1Path != null) { canvas.DrawPath(_gear1Path, _gearPaint); canvas.DrawPath(_gear1Path, _gearHighlightPaint); }
        if (_gear2Path != null) { canvas.DrawPath(_gear2Path, _gearPaint); canvas.DrawPath(_gear2Path, _gearHighlightPaint); }
        if (_gear3Path != null) { canvas.DrawPath(_gear3Path, _gearPaint); canvas.DrawPath(_gear3Path, _gearHighlightPaint); }
        if (_gear4Path != null) { canvas.DrawPath(_gear4Path, _gearPaint); canvas.DrawPath(_gear4Path, _gearHighlightPaint); }
    }

    private void RebuildGearPaths(SKRect bounds)
    {
        _gear1Path?.Dispose();
        _gear2Path?.Dispose();
        _gear3Path?.Dispose();
        _gear4Path?.Dispose();

        float w = bounds.Width;
        float h = bounds.Height;

        // Zahnrad 1: Rechts oben, groß
        _gear1Path = CreateGearPath(
            bounds.Left + w * 0.82f, bounds.Top + h * 0.12f,
            radius: 60f, teeth: GearTeeth);

        // Zahnrad 2: Links Mitte, mittel
        _gear2Path = CreateGearPath(
            bounds.Left + w * 0.12f, bounds.Top + h * 0.45f,
            radius: 48f, teeth: GearTeeth);

        // Zahnrad 3: Rechts unten, mittel (greift in Zahnrad 1 ein)
        _gear3Path = CreateGearPath(
            bounds.Left + w * 0.72f, bounds.Top + h * 0.65f,
            radius: 42f, teeth: 8);

        // Zahnrad 4: Links unten, klein
        _gear4Path = CreateGearPath(
            bounds.Left + w * 0.25f, bounds.Top + h * 0.82f,
            radius: 35f, teeth: 8);
    }

    /// <summary>
    /// Erstellt einen Zahnrad-SKPath mit Zähnen, zentralem Loch und Speichen.
    /// </summary>
    private static SKPath CreateGearPath(float cx, float cy, float radius, int teeth)
    {
        var path = new SKPath();

        float innerRadius = radius * 0.65f;
        float outerRadius = radius;
        float toothWidth = MathF.PI / teeth;
        float holeRadius = radius * 0.22f;

        // Äußere Zahnrad-Kontur
        for (int i = 0; i < teeth; i++)
        {
            float baseAngle = i * MathF.Tau / teeth;

            float a0 = baseAngle - toothWidth * 0.8f;
            float a1 = baseAngle - toothWidth * 0.4f;
            float a2 = baseAngle + toothWidth * 0.4f;
            float a3 = baseAngle + toothWidth * 0.8f;

            float x0 = cx + MathF.Cos(a0) * innerRadius;
            float y0 = cy + MathF.Sin(a0) * innerRadius;
            float x1 = cx + MathF.Cos(a1) * outerRadius;
            float y1 = cy + MathF.Sin(a1) * outerRadius;
            float x2 = cx + MathF.Cos(a2) * outerRadius;
            float y2 = cy + MathF.Sin(a2) * outerRadius;
            float x3 = cx + MathF.Cos(a3) * innerRadius;
            float y3 = cy + MathF.Sin(a3) * innerRadius;

            if (i == 0)
                path.MoveTo(x0, y0);
            else
                path.LineTo(x0, y0);

            path.LineTo(x1, y1);
            path.LineTo(x2, y2);
            path.LineTo(x3, y3);
        }
        path.Close();

        // Zentrales Loch (Gegenrichtung für Aussparung)
        path.AddCircle(cx, cy, holeRadius, SKPathDirection.CounterClockwise);

        // 4 Speichen (Kreuz)
        float spokeWidth = 3.5f;
        for (int i = 0; i < 4; i++)
        {
            float angle = i * MathF.PI / 4f;
            float perpX = -MathF.Sin(angle) * spokeWidth / 2;
            float perpY = MathF.Cos(angle) * spokeWidth / 2;

            float startR = holeRadius + 1f;
            float endR = innerRadius - 2f;

            float sx = cx + MathF.Cos(angle) * startR;
            float sy = cy + MathF.Sin(angle) * startR;
            float ex = cx + MathF.Cos(angle) * endR;
            float ey = cy + MathF.Sin(angle) * endR;

            path.MoveTo(sx + perpX, sy + perpY);
            path.LineTo(ex + perpX, ey + perpY);
            path.LineTo(ex - perpX, ey - perpY);
            path.LineTo(sx - perpX, sy - perpY);
            path.Close();
        }

        return path;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VIGNETTE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet eine radiale Vignette: Mitte warm aufgehellt, Ränder dunkler.
    /// Stärkerer Effekt als vorher für mehr Tiefe.
    /// </summary>
    private void DrawVignette(SKCanvas canvas, SKRect bounds)
    {
        float w = bounds.Width;
        float h = bounds.Height;

        if (_vignetteShader == null || MathF.Abs(w - _lastVignetteW) > 1f || MathF.Abs(h - _lastVignetteH) > 1f)
        {
            _vignetteShader?.Dispose();

            float centerX = bounds.MidX;
            float centerY = bounds.MidY;
            float radius = MathF.Max(w, h) * 0.65f;

            _vignetteShader = SKShader.CreateRadialGradient(
                new SKPoint(centerX, centerY),
                radius,
                [
                    new SKColor(0x40, 0x2A, 0x15, 0x20), // Mitte: Warmes Braun-Aufhellen
                    SKColors.Transparent,                   // Übergang
                    new SKColor(0x00, 0x00, 0x00, 0x70)    // Ränder: Deutlich dunkler
                ],
                [0.0f, 0.35f, 1.0f],
                SKShaderTileMode.Clamp);

            _lastVignetteW = w;
            _lastVignetteH = h;
        }

        _vignettePaint.Shader = _vignetteShader;
        canvas.DrawRect(bounds, _vignettePaint);
        _vignettePaint.Shader = null;
    }
}
