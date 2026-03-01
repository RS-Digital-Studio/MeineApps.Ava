using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert einen Pergament/Schriftrolle-Hintergrund hinter der Gilden-Forschung.
/// Dunkles Pergament-Thema mit Faserlinien, Gilden-Siegel-Wasserzeichen und goldener Bordüre.
/// Alle SKPaint-Objekte sind static readonly für GC-freie Performance im Render-Loop.
/// </summary>
public class GuildResearchBackgroundRenderer : IDisposable
{
    private bool _disposed;
    // ═══════════════════════════════════════════════════════════════════════
    // FARBEN (warme Pergament-Palette)
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly SKColor ParchmentBg = new(0x1A, 0x14, 0x10);              // Dunkles Pergament
    private static readonly SKColor FiberLineColor = new(0x3A, 0x2C, 0x1E, 0x14);     // Faser-/Faltlinien (~20 Alpha, braun-beige)
    private static readonly SKColor FiberLineLight = new(0x4A, 0x3C, 0x2A, 0x18);     // Hellere Faserlinien (~24 Alpha)
    private static readonly SKColor SealColor = new(0x8B, 0x6E, 0x50, 0x0A);          // Gilden-Siegel-Wasserzeichen (~10 Alpha)
    private static readonly SKColor SealHighlight = new(0xA0, 0x80, 0x5A, 0x0C);      // Siegel-Akzent (~12 Alpha)
    private static readonly SKColor BorderGold = new(0xD4, 0xA3, 0x73, 0x1E);         // Goldene Bordüre (~30 Alpha)
    private static readonly SKColor BorderDiamond = new(0xD4, 0xA3, 0x73, 0x28);      // Diamant-Muster in Bordüre (~40 Alpha)
    private static readonly SKColor GridLineColor = new(0x38, 0x28, 0x1A, 0x0F);      // Warme Grid-Linien (~15 Alpha)

    // ═══════════════════════════════════════════════════════════════════════
    // GECACHTE PAINTS (static readonly, keine Allokationen pro Frame)
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly SKPaint _bgPaint = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Fill,
        Color = ParchmentBg
    };

    private static readonly SKPaint _fiberPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 0.7f,
        Color = FiberLineColor
    };

    private static readonly SKPaint _fiberLightPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 0.5f,
        Color = FiberLineLight
    };

    private static readonly SKPaint _sealPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        Color = SealColor
    };

    private static readonly SKPaint _sealHighlightPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1.0f,
        Color = SealHighlight
    };

    private static readonly SKPaint _borderPaint = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Fill,
        Color = BorderGold
    };

    private static readonly SKPaint _borderDiamondPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        Color = BorderDiamond
    };

    private static readonly SKPaint _gridPaint = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 0.5f,
        Color = GridLineColor
    };

    private static readonly SKPaint _vignettePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    // ═══════════════════════════════════════════════════════════════════════
    // GECACHTE INSTANZ-FELDER (bei Bounds-Änderung neu erstellt)
    // ═══════════════════════════════════════════════════════════════════════

    // Gecachter Siegel-Pfad (Schild mit Hammer+Zahnrad)
    private SKPath? _sealPath;
    private float _lastSealW;
    private float _lastSealH;

    // Gecachter Faserlinien-Pfad
    private SKPath? _fiberPath;
    private SKPath? _fiberLightPath;
    private float _lastFiberW;
    private float _lastFiberH;

    // Gecachter Vignette-Shader
    private SKShader? _vignetteShader;
    private float _lastVignetteW;
    private float _lastVignetteH;

    // Gecachter Bordüre-Diamant-Pfad
    private SKPath? _borderDiamondPath;
    private float _lastBorderW;

    // Bounds-Tracking
    private float _lastBoundsWidth;
    private float _lastBoundsHeight;

    // ═══════════════════════════════════════════════════════════════════════
    // KONSTANTEN
    // ═══════════════════════════════════════════════════════════════════════

    private const float GridSpacing = 28f;          // Raster-Abstand (28px)
    private const int FiberLineCount = 14;          // Anzahl Faserlinien
    private const float BorderHeight = 4f;          // Bordüre-Höhe
    private const float DiamondSpacing = 18f;       // Abstand zwischen Diamant-Mustern

    /// <summary>
    /// Rendert den Gilden-Forschungshintergrund.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds)
    {
        // 1. Dunkler Pergament-Hintergrund
        DrawBackground(canvas, bounds);

        // 2. Faser-/Faltlinien (subtile Sinus-Wellenlinien)
        DrawFiberLines(canvas, bounds);

        // 3. Feines Grid-Raster
        DrawGrid(canvas, bounds);

        // 4. Gilden-Siegel-Wasserzeichen (Schild mit Hammer+Zahnrad)
        DrawSealWatermark(canvas, bounds);

        // 5. Goldene Bordüre oben und unten
        DrawGoldenBorders(canvas, bounds);

        // 6. Warme Vignette für Tiefenwirkung
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
    // FASERLINIEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet subtile Faser-/Faltlinien (organische Sinus-Wellenlinien, braun-beige Töne).
    /// Pfade werden gecacht und nur bei Bounds-Änderung neu berechnet.
    /// </summary>
    private void DrawFiberLines(SKCanvas canvas, SKRect bounds)
    {
        float w = bounds.Width;
        float h = bounds.Height;

        if (_fiberPath == null || _fiberLightPath == null
            || MathF.Abs(w - _lastFiberW) > 1f || MathF.Abs(h - _lastFiberH) > 1f)
        {
            _fiberPath?.Dispose();
            _fiberLightPath?.Dispose();
            BuildFiberPaths(bounds, out _fiberPath, out _fiberLightPath);
            _lastFiberW = w;
            _lastFiberH = h;
        }

        canvas.DrawPath(_fiberPath, _fiberPaint);
        canvas.DrawPath(_fiberLightPath, _fiberLightPaint);
    }

    /// <summary>
    /// Erstellt die Faserlinien-Pfade (dunkel + hell) mit Sinus-Wellenlinien.
    /// Deterministisch, kein Random.
    /// </summary>
    private static void BuildFiberPaths(SKRect bounds, out SKPath darkPath, out SKPath lightPath)
    {
        darkPath = new SKPath();
        lightPath = new SKPath();
        float spacing = bounds.Height / (FiberLineCount + 1);

        for (int i = 0; i < FiberLineCount; i++)
        {
            float baseY = bounds.Top + spacing * (i + 1);
            // Verschiedene Frequenzen/Amplituden für natürlichen Pergament-Look
            float freq = 0.005f + i * 0.0008f;
            float amp = 2.5f + (i % 4) * 1.5f;
            float phase = i * 2.1f; // Deterministischer Versatz

            // Abwechselnd dunkle und helle Fasern
            var path = (i % 3 == 0) ? lightPath : darkPath;

            path.MoveTo(bounds.Left, baseY + MathF.Sin(phase) * amp);

            for (float x = bounds.Left + 4; x <= bounds.Right; x += 4)
            {
                float y = baseY + MathF.Sin(x * freq + phase) * amp
                                + MathF.Sin(x * freq * 1.8f + phase * 0.5f) * (amp * 0.25f);
                path.LineTo(x, y);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GRID-RASTER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet ein sehr subtiles warmes Raster (28px Abstand, braune Linien).
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
    // GILDEN-SIEGEL-WASSERZEICHEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet ein großes blasses Gilden-Emblem in der Mitte (Schild mit Hammer+Zahnrad).
    /// Pfad wird gecacht und nur bei Bounds-Änderung neu berechnet.
    /// </summary>
    private void DrawSealWatermark(SKCanvas canvas, SKRect bounds)
    {
        float w = bounds.Width;
        float h = bounds.Height;

        if (_sealPath == null || MathF.Abs(w - _lastSealW) > 1f || MathF.Abs(h - _lastSealH) > 1f)
        {
            _sealPath?.Dispose();
            _sealPath = BuildSealPath(bounds);
            _lastSealW = w;
            _lastSealH = h;
        }

        canvas.DrawPath(_sealPath, _sealPaint);
        canvas.DrawPath(_sealPath, _sealHighlightPaint);
    }

    /// <summary>
    /// Erstellt den Gilden-Siegel-Pfad: Schildform mit Hammer und Zahnrad im Inneren.
    /// Groß und zentral platziert.
    /// </summary>
    private static SKPath BuildSealPath(SKRect bounds)
    {
        var path = new SKPath();
        float cx = bounds.MidX;
        float cy = bounds.MidY;

        // Schildgröße: ~30% der kleineren Dimension
        float size = MathF.Min(bounds.Width, bounds.Height) * 0.3f;

        // Schildform (Wappen-Schild)
        float shieldTop = cy - size * 0.55f;
        float shieldBottom = cy + size * 0.55f;
        float shieldLeft = cx - size * 0.45f;
        float shieldRight = cx + size * 0.45f;

        // Oberer Teil: Abgerundetes Rechteck
        path.MoveTo(shieldLeft, shieldTop + size * 0.1f);
        path.QuadTo(shieldLeft, shieldTop, cx - size * 0.2f, shieldTop);
        path.LineTo(cx + size * 0.2f, shieldTop);
        path.QuadTo(shieldRight, shieldTop, shieldRight, shieldTop + size * 0.1f);

        // Rechte Seite: Gerade nach unten, dann zur Spitze
        path.LineTo(shieldRight, cy);
        path.QuadTo(shieldRight, cy + size * 0.25f, cx, shieldBottom);

        // Linke Seite: Spiegelbildlich zurück
        path.QuadTo(shieldLeft, cy + size * 0.25f, shieldLeft, cy);
        path.Close();

        // Zahnrad im Schild (oberer Bereich)
        float gearCx = cx;
        float gearCy = cy - size * 0.1f;
        float gearRadius = size * 0.18f;
        float innerRadius = gearRadius * 0.65f;
        int teeth = 8;
        float toothWidth = MathF.PI / teeth;

        for (int i = 0; i < teeth; i++)
        {
            float baseAngle = i * MathF.Tau / teeth;
            float a0 = baseAngle - toothWidth * 0.7f;
            float a1 = baseAngle - toothWidth * 0.35f;
            float a2 = baseAngle + toothWidth * 0.35f;
            float a3 = baseAngle + toothWidth * 0.7f;

            float x0 = gearCx + MathF.Cos(a0) * innerRadius;
            float y0 = gearCy + MathF.Sin(a0) * innerRadius;
            float x1 = gearCx + MathF.Cos(a1) * gearRadius;
            float y1 = gearCy + MathF.Sin(a1) * gearRadius;
            float x2 = gearCx + MathF.Cos(a2) * gearRadius;
            float y2 = gearCy + MathF.Sin(a2) * gearRadius;
            float x3 = gearCx + MathF.Cos(a3) * innerRadius;
            float y3 = gearCy + MathF.Sin(a3) * innerRadius;

            if (i == 0)
                path.MoveTo(x0, y0);
            else
                path.LineTo(x0, y0);

            path.LineTo(x1, y1);
            path.LineTo(x2, y2);
            path.LineTo(x3, y3);
        }
        path.Close();

        // Zahnrad-Loch
        path.AddCircle(gearCx, gearCy, gearRadius * 0.25f, SKPathDirection.CounterClockwise);

        // Hammer im Schild (unterer Bereich, unter dem Zahnrad)
        float hammerCx = cx;
        float hammerCy = cy + size * 0.18f;
        float hammerScale = size * 0.12f;

        // Hammer-Stiel (vertikal)
        float stielBreite = hammerScale * 0.2f;
        float stielHoehe = hammerScale * 1.4f;
        path.AddRect(new SKRect(
            hammerCx - stielBreite / 2, hammerCy - stielHoehe * 0.3f,
            hammerCx + stielBreite / 2, hammerCy + stielHoehe * 0.7f));

        // Hammer-Kopf (horizontal oben am Stiel)
        float kopfBreite = hammerScale * 1.2f;
        float kopfHoehe = hammerScale * 0.4f;
        path.AddRect(new SKRect(
            hammerCx - kopfBreite / 2, hammerCy - stielHoehe * 0.3f - kopfHoehe,
            hammerCx + kopfBreite / 2, hammerCy - stielHoehe * 0.3f));

        return path;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GOLDENE BORDÜRE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet dekorative goldene Bordüren am oberen und unteren Rand.
    /// Mit kleinen Diamant-/Punkt-Mustern entlang der Bordüre.
    /// </summary>
    private void DrawGoldenBorders(SKCanvas canvas, SKRect bounds)
    {
        // Obere Bordüre
        canvas.DrawRect(new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + BorderHeight), _borderPaint);

        // Untere Bordüre
        canvas.DrawRect(new SKRect(bounds.Left, bounds.Bottom - BorderHeight, bounds.Right, bounds.Bottom), _borderPaint);

        // Diamant-Muster entlang der Bordüren
        DrawBorderDiamonds(canvas, bounds);
    }

    /// <summary>
    /// Zeichnet kleine Diamant-Formen entlang der oberen und unteren Bordüre.
    /// Pfad wird gecacht und bei Breiten-Änderung neu erstellt.
    /// </summary>
    private void DrawBorderDiamonds(SKCanvas canvas, SKRect bounds)
    {
        float w = bounds.Width;

        if (_borderDiamondPath == null || MathF.Abs(w - _lastBorderW) > 1f
            || MathF.Abs(bounds.Height - _lastBoundsHeight) > 1f)
        {
            _borderDiamondPath?.Dispose();
            _borderDiamondPath = BuildBorderDiamondPath(bounds);
            _lastBorderW = w;
            _lastBoundsWidth = w;
            _lastBoundsHeight = bounds.Height;
        }

        canvas.DrawPath(_borderDiamondPath, _borderDiamondPaint);
    }

    /// <summary>
    /// Erstellt den Pfad für alle Diamant-Muster in beiden Bordüren.
    /// </summary>
    private static SKPath BuildBorderDiamondPath(SKRect bounds)
    {
        var path = new SKPath();
        float diamondSize = BorderHeight * 0.35f;

        // Obere Bordüre: Diamanten
        float topCenterY = bounds.Top + BorderHeight / 2;
        for (float x = bounds.Left + DiamondSpacing; x < bounds.Right; x += DiamondSpacing)
        {
            AddDiamond(path, x, topCenterY, diamondSize);
        }

        // Untere Bordüre: Diamanten
        float bottomCenterY = bounds.Bottom - BorderHeight / 2;
        for (float x = bounds.Left + DiamondSpacing; x < bounds.Right; x += DiamondSpacing)
        {
            AddDiamond(path, x, bottomCenterY, diamondSize);
        }

        return path;
    }

    /// <summary>
    /// Fügt eine einzelne Rauten-/Diamantform zum Pfad hinzu.
    /// </summary>
    private static void AddDiamond(SKPath path, float cx, float cy, float size)
    {
        path.MoveTo(cx, cy - size);       // Oben
        path.LineTo(cx + size, cy);       // Rechts
        path.LineTo(cx, cy + size);       // Unten
        path.LineTo(cx - size, cy);       // Links
        path.Close();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VIGNETTE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet eine warme radiale Vignette: Mitte leicht aufgehellt, Ränder dunkles Warmbraun.
    /// Shader wird gecacht und bei Bounds-Änderung neu erstellt.
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
                    new SKColor(0x35, 0x28, 0x18, 0x18), // Mitte: Dezenter warmer Highlight
                    SKColors.Transparent,                   // Übergang
                    new SKColor(0x0D, 0x0A, 0x06, 0x80)    // Ränder: Dunkles Warmbraun #0D0A06
                ],
                [0.0f, 0.3f, 1.0f],
                SKShaderTileMode.Clamp);

            _lastVignetteW = w;
            _lastVignetteH = h;
        }

        _vignettePaint.Shader = _vignetteShader;
        canvas.DrawRect(bounds, _vignettePaint);
        _vignettePaint.Shader = null;
    }

    /// <summary>
    /// Gibt native SkiaSharp-Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _vignetteShader?.Dispose();
        _sealPath?.Dispose();
        _fiberPath?.Dispose();
        _fiberLightPath?.Dispose();
        _borderDiamondPath?.Dispose();
    }
}
