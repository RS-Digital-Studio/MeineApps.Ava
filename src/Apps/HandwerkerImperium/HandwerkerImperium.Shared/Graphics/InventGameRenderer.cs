using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// SkiaSharp-Renderer für das Erfinder-Puzzle-Minigame.
/// Zeichnet ein Werkstatt-/Labor-Grid mit Bauteilen, Nummern und Elektro-Partikel-Effekten.
/// Violett-Palette für Tech-/Labor-Atmosphäre.
/// </summary>
public class InventGameRenderer
{
    // Hintergrund (Dunkelviolett mit Tech-Raster)
    private static readonly SKColor LabBg = new(0x1A, 0x10, 0x40);              // Dunkelviolett
    private static readonly SKColor LabGridCoarse = new(0x2D, 0x1B, 0x69);      // Grobe Rasterlinien
    private static readonly SKColor LabGridFine = new(0x22, 0x15, 0x55);        // Feine Rasterlinien

    // Kachel-Zustände
    private static readonly SKColor CompletedBorder = new(0x4C, 0xAF, 0x50);    // Grün
    private static readonly SKColor ActiveBorderColor = new(0xBB, 0x86, 0xFC);  // Helles Violett (pulsierend)
    private static readonly SKColor DefaultBorder = new(0xFF, 0xFF, 0xFF, 0x40); // Halbtransparent weiß
    private static readonly SKColor CheckmarkBg = new(0x4C, 0xAF, 0x50);        // Grüner Hintergrund für Häkchen
    private static readonly SKColor ErrorColor = new(0xF4, 0x43, 0x36);          // Rot bei Fehler

    // Text-Farben
    private static readonly SKColor TextWhite = SKColors.White;
    private static readonly SKColor TextNumber = new(0xFF, 0xFF, 0xFF, 0xE0);
    private static readonly SKColor TextQuestion = new(0xFF, 0xFF, 0xFF, 0x80);

    // Elektro-Funken-Partikel
    private readonly List<SparkParticle> _sparkParticles = new();
    private float _animTime;

    private struct SparkParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Size;
    }

    /// <summary>
    /// Daten-Struct für ein einzelnes Bauteil (View-optimiert, keine VM-Referenz).
    /// </summary>
    public struct InventPartData
    {
        public string Icon;
        public string DisplayNumber;
        public uint BackgroundColor; // ARGB
        public bool IsCompleted;
        public bool IsActive;
        public bool HasError;
    }

    // Zwischengespeicherte Kachel-Positionen für HitTest
    private SKRect[] _tileRects = Array.Empty<SKRect>();

    /// <summary>
    /// Rendert das gesamte Erfinder-Puzzle-Spielfeld.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds,
        InventPartData[] parts, int cols,
        bool isMemorizing, bool isPlaying,
        float deltaTime)
    {
        _animTime += deltaTime;

        // Labor-Hintergrund zeichnen
        DrawLabBackground(canvas, bounds);

        if (parts.Length == 0 || cols <= 0) return;

        // Grid-Layout berechnen
        int rows = (int)Math.Ceiling((double)parts.Length / cols);
        float padding = 16;
        float tileSpacing = 8;

        float availableWidth = bounds.Width - 2 * padding;
        float availableHeight = bounds.Height - 2 * padding;

        // Kachelgröße berechnen (quadratisch, passt in verfügbaren Platz)
        float maxTileWidth = (availableWidth - (cols - 1) * tileSpacing) / cols;
        float maxTileHeight = (availableHeight - (rows - 1) * tileSpacing) / rows;
        float tileSize = Math.Min(maxTileWidth, maxTileHeight);

        // Grid zentrieren (horizontal), oben ausrichten (vertikal)
        float gridWidth = cols * tileSize + (cols - 1) * tileSpacing;
        float startX = bounds.Left + (bounds.Width - gridWidth) / 2;
        float startY = bounds.Top + padding;

        // Kachel-Positionen speichern für HitTest
        if (_tileRects.Length != parts.Length)
            _tileRects = new SKRect[parts.Length];

        // Kacheln zeichnen
        for (int i = 0; i < parts.Length; i++)
        {
            int col = i % cols;
            int row = i / cols;

            float x = startX + col * (tileSize + tileSpacing);
            float y = startY + row * (tileSize + tileSpacing);

            var tileRect = SKRect.Create(x, y, tileSize, tileSize);
            _tileRects[i] = tileRect;

            DrawTile(canvas, tileRect, parts[i], isMemorizing, isPlaying);
        }

        // Elektro-Funken-Partikel (atmosphärisch)
        UpdateAndDrawSparkParticles(canvas, bounds, deltaTime);
    }

    /// <summary>
    /// HitTest: Gibt den Index des getroffenen Bauteils zurück, oder -1.
    /// </summary>
    public int HitTest(SKRect bounds, float touchX, float touchY, int cols, int totalParts)
    {
        for (int i = 0; i < _tileRects.Length && i < totalParts; i++)
        {
            if (_tileRects[i].Contains(touchX, touchY))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Zeichnet den Labor-Hintergrund mit Tech-Rasterlinien.
    /// </summary>
    private void DrawLabBackground(SKCanvas canvas, SKRect bounds)
    {
        // Dunkelvioletter Hintergrund
        using var bgPaint = new SKPaint { Color = LabBg, IsAntialias = false };
        canvas.DrawRect(bounds, bgPaint);

        // Feine Rasterlinien (20px Abstand, Tech-Look)
        using var fineGridPaint = new SKPaint
        {
            Color = LabGridFine,
            IsAntialias = false,
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke
        };

        for (float x = bounds.Left; x < bounds.Right; x += 20)
        {
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, fineGridPaint);
        }
        for (float y = bounds.Top; y < bounds.Bottom; y += 20)
        {
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, fineGridPaint);
        }

        // Grobe Rasterlinien (80px Abstand)
        using var gridPaint = new SKPaint
        {
            Color = LabGridCoarse,
            IsAntialias = false,
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke
        };

        for (float x = bounds.Left; x < bounds.Right; x += 80)
        {
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, gridPaint);
        }
        for (float y = bounds.Top; y < bounds.Bottom; y += 80)
        {
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, gridPaint);
        }

        // Eck-Markierungen (Labor-Stil, diagonal)
        using var cornerPaint = new SKPaint
        {
            Color = LabGridCoarse.WithAlpha(80),
            IsAntialias = false,
            StrokeWidth = 1
        };

        float cornerSize = 30;
        canvas.DrawLine(bounds.Left, bounds.Top + cornerSize, bounds.Left + cornerSize, bounds.Top, cornerPaint);
        canvas.DrawLine(bounds.Right - cornerSize, bounds.Top, bounds.Right, bounds.Top + cornerSize, cornerPaint);
        canvas.DrawLine(bounds.Left, bounds.Bottom - cornerSize, bounds.Left + cornerSize, bounds.Bottom, cornerPaint);
        canvas.DrawLine(bounds.Right - cornerSize, bounds.Bottom, bounds.Right, bounds.Bottom - cornerSize, cornerPaint);
    }

    /// <summary>
    /// Zeichnet eine einzelne Bauteil-Kachel mit Icon, Nummer und Zustandsanzeige.
    /// </summary>
    private void DrawTile(SKCanvas canvas, SKRect rect, InventPartData part, bool isMemorizing, bool isPlaying)
    {
        float cornerRadius = 8;

        // Hintergrundfarbe der Kachel
        var bgColor = new SKColor(
            (byte)((part.BackgroundColor >> 16) & 0xFF),
            (byte)((part.BackgroundColor >> 8) & 0xFF),
            (byte)(part.BackgroundColor & 0xFF),
            (byte)((part.BackgroundColor >> 24) & 0xFF)
        );

        // Kachel-Hintergrund (abgerundetes Rechteck)
        using var bgPaint = new SKPaint { Color = bgColor, IsAntialias = true };
        canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, bgPaint);

        // Leichter Gradient-Effekt (obere Hälfte heller)
        using var highlightPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x12),
            IsAntialias = true
        };
        var highlightRect = SKRect.Create(rect.Left, rect.Top, rect.Width, rect.Height * 0.45f);
        canvas.DrawRoundRect(highlightRect, cornerRadius, cornerRadius, highlightPaint);

        // Rahmen basierend auf Zustand
        DrawTileBorder(canvas, rect, part, cornerRadius);

        // Icon (Vektor-Icon oben in der Kachel)
        DrawTileIcon(canvas, rect, part.Icon);

        // Nummer oder Fragezeichen (unten in der Kachel)
        DrawTileNumber(canvas, rect, part.DisplayNumber, isMemorizing);

        // Häkchen bei abgeschlossenen Teilen (oben rechts)
        if (part.IsCompleted)
        {
            DrawCheckmark(canvas, rect);
        }

        // Fehler-Overlay (rotes Blinken)
        if (part.HasError)
        {
            using var errorPaint = new SKPaint
            {
                Color = ErrorColor.WithAlpha(100),
                IsAntialias = true
            };
            canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, errorPaint);
        }
    }

    /// <summary>
    /// Zeichnet den Kachel-Rahmen je nach Zustand (aktiv=pulsierend violett, erledigt=grün, normal=weiß).
    /// </summary>
    private void DrawTileBorder(SKCanvas canvas, SKRect rect, InventPartData part, float cornerRadius)
    {
        SKColor borderColor;
        float borderWidth;

        if (part.IsActive && !part.IsCompleted)
        {
            // Pulsierender violetter Rand für aktives Bauteil
            float pulse = (float)(0.5 + 0.5 * Math.Sin(_animTime * 5));
            byte alpha = (byte)(150 + 105 * pulse);
            borderColor = ActiveBorderColor.WithAlpha(alpha);
            borderWidth = 3;

            // Äußerer Glow-Effekt
            using var glowPaint = new SKPaint
            {
                Color = ActiveBorderColor.WithAlpha((byte)(40 * pulse)),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 6
            };
            var glowRect = SKRect.Create(rect.Left - 2, rect.Top - 2, rect.Width + 4, rect.Height + 4);
            canvas.DrawRoundRect(glowRect, cornerRadius + 2, cornerRadius + 2, glowPaint);
        }
        else if (part.IsCompleted)
        {
            borderColor = CompletedBorder;
            borderWidth = 3;
        }
        else if (part.HasError)
        {
            borderColor = ErrorColor;
            borderWidth = 3;
        }
        else
        {
            borderColor = DefaultBorder;
            borderWidth = 2;
        }

        using var borderPaint = new SKPaint
        {
            Color = borderColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = borderWidth
        };
        canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, borderPaint);
    }

    /// <summary>
    /// Zeichnet ein Vektor-Icon in der oberen Hälfte der Kachel basierend auf dem Icon-Identifier.
    /// </summary>
    private static void DrawTileIcon(SKCanvas canvas, SKRect rect, string iconId)
    {
        if (string.IsNullOrEmpty(iconId)) return;

        float iconSize = rect.Height * 0.32f;
        float cx = rect.MidX;
        float cy = rect.Top + rect.Height * 0.38f;

        switch (iconId)
        {
            case "gear": DrawGearIcon(canvas, cx, cy, iconSize); break;
            case "piston": DrawPistonIcon(canvas, cx, cy, iconSize); break;
            case "wire": DrawWireIcon(canvas, cx, cy, iconSize); break;
            case "board": DrawBoardIcon(canvas, cx, cy, iconSize); break;
            case "screw": DrawScrewIcon(canvas, cx, cy, iconSize); break;
            case "housing": DrawHousingIcon(canvas, cx, cy, iconSize); break;
            case "spring": DrawSpringIcon(canvas, cx, cy, iconSize); break;
            case "lens": DrawLensIcon(canvas, cx, cy, iconSize); break;
            case "motor": DrawMotorIcon(canvas, cx, cy, iconSize); break;
            case "battery": DrawBatteryIcon(canvas, cx, cy, iconSize); break;
            case "switch": DrawSwitchIcon(canvas, cx, cy, iconSize); break;
            case "antenna": DrawAntennaIcon(canvas, cx, cy, iconSize); break;
            default: DrawDefaultIcon(canvas, cx, cy, iconSize); break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 12 BAUTEIL-ICONS (SkiaSharp-Zeichnungen)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Zahnrad: Kreis mit 8 Zähnen.</summary>
    private static void DrawGearIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;
        float outerR = half * 0.85f;
        float innerR = half * 0.55f;
        float toothW = half * 0.22f;
        int teeth = 8;

        // Zahnrad-Kontur mit Zähnen
        using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xB0, 0xBE, 0xC5) };
        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x78, 0x90, 0x9C), StrokeWidth = 1.5f
        };

        // Äußerer Kreis (Basis)
        canvas.DrawCircle(cx, cy, outerR, fillPaint);

        // Zähne als kleine Rechtecke
        using var toothPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x90, 0xA4, 0xAE) };
        for (int i = 0; i < teeth; i++)
        {
            float angle = i * 360f / teeth;
            float rad = angle * MathF.PI / 180f;
            float tx = cx + MathF.Cos(rad) * outerR;
            float ty = cy + MathF.Sin(rad) * outerR;

            canvas.Save();
            canvas.Translate(tx, ty);
            canvas.RotateDegrees(angle);
            canvas.DrawRect(-toothW / 2, -toothW / 2, toothW, toothW, toothPaint);
            canvas.Restore();
        }

        canvas.DrawCircle(cx, cy, outerR, borderPaint);

        // Innerer Kreis (Achsloch)
        using var holePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x2A, 0x1A, 0x40) };
        canvas.DrawCircle(cx, cy, innerR * 0.45f, holePaint);
        canvas.DrawCircle(cx, cy, innerR * 0.45f, borderPaint);
    }

    /// <summary>Kolben: Rechteck mit Stange nach unten.</summary>
    private static void DrawPistonIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        // Kolbenkopf (breites Rechteck oben)
        using var headPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x9E, 0x9E, 0x9E) };
        var headRect = SKRect.Create(cx - half * 0.6f, cy - half * 0.7f, half * 1.2f, half * 0.7f);
        canvas.DrawRoundRect(headRect, 3, 3, headPaint);

        // Kolbenring (dünne Linie oben)
        using var ringPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x61, 0x61, 0x61), StrokeWidth = 2
        };
        canvas.DrawLine(headRect.Left + 3, headRect.Top + half * 0.15f, headRect.Right - 3, headRect.Top + half * 0.15f, ringPaint);

        // Pleuelstange (schmal nach unten)
        using var rodPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x78, 0x90, 0x9C) };
        var rodRect = SKRect.Create(cx - half * 0.15f, cy, half * 0.3f, half * 0.8f);
        canvas.DrawRect(rodRect, rodPaint);

        // Bolzen unten (Kreis)
        using var boltPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xB0, 0xBE, 0xC5) };
        canvas.DrawCircle(cx, cy + half * 0.8f, half * 0.18f, boltPaint);

        // Rand
        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x54, 0x6E, 0x7A), StrokeWidth = 1.5f
        };
        canvas.DrawRoundRect(headRect, 3, 3, borderPaint);
    }

    /// <summary>Kabel: Wellenlinie mit Steckern an beiden Enden.</summary>
    private static void DrawWireIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var wirePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xEF, 0x53, 0x50), StrokeWidth = 3, StrokeCap = SKStrokeCap.Round
        };

        // Wellenlinie (3 Bögen)
        using var path = new SKPath();
        path.MoveTo(cx - half, cy);
        float segW = half * 2f / 3f;
        for (int i = 0; i < 3; i++)
        {
            float startX = cx - half + i * segW;
            float controlY = (i % 2 == 0) ? cy - half * 0.5f : cy + half * 0.5f;
            path.QuadTo(startX + segW / 2, controlY, startX + segW, cy);
        }
        canvas.DrawPath(path, wirePaint);

        // Stecker links
        using var plugPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xFF, 0xCA, 0x28) };
        canvas.DrawCircle(cx - half, cy, half * 0.15f, plugPaint);

        // Stecker rechts
        canvas.DrawCircle(cx + half, cy, half * 0.15f, plugPaint);
    }

    /// <summary>Platine: Rechteck mit Leiterbahnen und Bauteilen.</summary>
    private static void DrawBoardIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        // Platinen-Rechteck (grün)
        using var boardPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x2E, 0x7D, 0x32) };
        var boardRect = SKRect.Create(cx - half * 0.8f, cy - half * 0.6f, half * 1.6f, half * 1.2f);
        canvas.DrawRoundRect(boardRect, 3, 3, boardPaint);

        // Leiterbahnen (goldene Linien)
        using var tracePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0xD5, 0x4F, 0xC0), StrokeWidth = 1.5f
        };
        // Horizontal
        canvas.DrawLine(boardRect.Left + 4, cy - half * 0.2f, boardRect.Right - 4, cy - half * 0.2f, tracePaint);
        canvas.DrawLine(boardRect.Left + 4, cy + half * 0.2f, boardRect.Right - 4, cy + half * 0.2f, tracePaint);
        // Vertikal
        canvas.DrawLine(cx - half * 0.3f, boardRect.Top + 4, cx - half * 0.3f, boardRect.Bottom - 4, tracePaint);
        canvas.DrawLine(cx + half * 0.3f, boardRect.Top + 4, cx + half * 0.3f, boardRect.Bottom - 4, tracePaint);

        // Chip (kleines Quadrat in der Mitte)
        using var chipPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x21, 0x21, 0x21) };
        canvas.DrawRect(cx - half * 0.2f, cy - half * 0.15f, half * 0.4f, half * 0.3f, chipPaint);

        // Rand
        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x1B, 0x5E, 0x20), StrokeWidth = 1.5f
        };
        canvas.DrawRoundRect(boardRect, 3, 3, borderPaint);
    }

    /// <summary>Schraube: Kreuz mit Kreis (Kreuzschlitz).</summary>
    private static void DrawScrewIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;
        float radius = half * 0.7f;

        // Schraubenkopf (Kreis)
        using var headPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xB0, 0xBE, 0xC5) };
        canvas.DrawCircle(cx, cy, radius, headPaint);

        // Rand
        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x78, 0x90, 0x9C), StrokeWidth = 2
        };
        canvas.DrawCircle(cx, cy, radius, borderPaint);

        // Kreuzschlitz
        using var slotPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x54, 0x6E, 0x7A), StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round
        };
        float slotLen = radius * 0.6f;
        canvas.DrawLine(cx - slotLen, cy, cx + slotLen, cy, slotPaint);
        canvas.DrawLine(cx, cy - slotLen, cx, cy + slotLen, slotPaint);
    }

    /// <summary>Gehäuse: Abgerundetes Rechteck mit Schrauben in Ecken.</summary>
    private static void DrawHousingIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        // Gehäuse
        using var housingPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x60, 0x7D, 0x8B) };
        var housingRect = SKRect.Create(cx - half * 0.8f, cy - half * 0.7f, half * 1.6f, half * 1.4f);
        canvas.DrawRoundRect(housingRect, 6, 6, housingPaint);

        // Rand
        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x45, 0x5A, 0x64), StrokeWidth = 2
        };
        canvas.DrawRoundRect(housingRect, 6, 6, borderPaint);

        // 4 Schrauben in Ecken
        using var screwPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xB0, 0xBE, 0xC5) };
        float screwR = half * 0.08f;
        float inset = half * 0.2f;
        canvas.DrawCircle(housingRect.Left + inset, housingRect.Top + inset, screwR, screwPaint);
        canvas.DrawCircle(housingRect.Right - inset, housingRect.Top + inset, screwR, screwPaint);
        canvas.DrawCircle(housingRect.Left + inset, housingRect.Bottom - inset, screwR, screwPaint);
        canvas.DrawCircle(housingRect.Right - inset, housingRect.Bottom - inset, screwR, screwPaint);

        // Ventilationsschlitze
        using var slitPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x45, 0x5A, 0x64, 0x80), StrokeWidth = 1.5f
        };
        for (int i = 0; i < 3; i++)
        {
            float ly = cy - half * 0.2f + i * half * 0.2f;
            canvas.DrawLine(cx - half * 0.3f, ly, cx + half * 0.3f, ly, slitPaint);
        }
    }

    /// <summary>Feder: Zickzack-Linie (Spiralfeder).</summary>
    private static void DrawSpringIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var springPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xE0, 0xE0, 0xE0), StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round
        };

        // Zickzack-Feder (7 Segmente)
        using var path = new SKPath();
        int segments = 7;
        float segHeight = (half * 1.6f) / segments;
        float zigWidth = half * 0.5f;

        // Obere Endkappe
        canvas.DrawLine(cx - half * 0.15f, cy - half * 0.8f, cx + half * 0.15f, cy - half * 0.8f, springPaint);

        path.MoveTo(cx, cy - half * 0.8f);
        for (int i = 0; i < segments; i++)
        {
            float y = cy - half * 0.8f + (i + 1) * segHeight;
            float x = (i % 2 == 0) ? cx + zigWidth : cx - zigWidth;
            path.LineTo(x, y);
        }
        canvas.DrawPath(path, springPaint);

        // Untere Endkappe
        float bottomY = cy - half * 0.8f + segments * segHeight;
        float lastX = (segments % 2 != 0) ? cx + zigWidth : cx - zigWidth;
        canvas.DrawLine(lastX, bottomY, cx, bottomY, springPaint);
        canvas.DrawLine(cx - half * 0.15f, bottomY, cx + half * 0.15f, bottomY, springPaint);
    }

    /// <summary>Linse: Doppelkonvexe Form.</summary>
    private static void DrawLensIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        // Linke konvexe Seite
        using var lensPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x42, 0xA5, 0xF5, 0xA0) };
        using var path = new SKPath();

        float lensHeight = half * 1.4f;
        float bulgeFactor = half * 0.6f;

        // Linke Seite
        path.MoveTo(cx, cy - lensHeight / 2);
        path.QuadTo(cx - bulgeFactor, cy, cx, cy + lensHeight / 2);
        // Rechte Seite
        path.QuadTo(cx + bulgeFactor, cy, cx, cy - lensHeight / 2);
        path.Close();

        canvas.DrawPath(path, lensPaint);

        // Rand
        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x1E, 0x88, 0xE5), StrokeWidth = 2
        };
        canvas.DrawPath(path, borderPaint);

        // Lichtreflex (kleiner heller Bogen oben links)
        using var reflexPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x80), StrokeWidth = 1.5f, StrokeCap = SKStrokeCap.Round
        };
        using var reflexPath = new SKPath();
        reflexPath.MoveTo(cx - half * 0.2f, cy - half * 0.35f);
        reflexPath.QuadTo(cx - half * 0.1f, cy - half * 0.45f, cx + half * 0.05f, cy - half * 0.35f);
        canvas.DrawPath(reflexPath, reflexPaint);
    }

    /// <summary>Motor: Rechteck mit Achse und Wicklungs-Andeutung.</summary>
    private static void DrawMotorIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        // Motor-Gehäuse
        using var bodyPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x78, 0x90, 0x9C) };
        var bodyRect = SKRect.Create(cx - half * 0.6f, cy - half * 0.5f, half * 1.2f, half);
        canvas.DrawRoundRect(bodyRect, 4, 4, bodyPaint);

        // Wicklungs-Streifen (2 kupferfarbene Linien)
        using var coilPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0x8F, 0x00), StrokeWidth = 2
        };
        canvas.DrawLine(bodyRect.Left + 4, cy - half * 0.15f, bodyRect.Right - 4, cy - half * 0.15f, coilPaint);
        canvas.DrawLine(bodyRect.Left + 4, cy + half * 0.15f, bodyRect.Right - 4, cy + half * 0.15f, coilPaint);

        // Achse (rechts herausragend)
        using var axlePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xB0, 0xBE, 0xC5), StrokeWidth = 3, StrokeCap = SKStrokeCap.Round
        };
        canvas.DrawLine(bodyRect.Right, cy, bodyRect.Right + half * 0.35f, cy, axlePaint);

        // Achsenlager (Kreis am Ende)
        using var bearingPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xE0, 0xE0, 0xE0) };
        canvas.DrawCircle(bodyRect.Right + half * 0.35f, cy, half * 0.1f, bearingPaint);

        // Rand
        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x54, 0x6E, 0x7A), StrokeWidth = 1.5f
        };
        canvas.DrawRoundRect(bodyRect, 4, 4, borderPaint);
    }

    /// <summary>Batterie: Rechteck mit +/- Symbolen.</summary>
    private static void DrawBatteryIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        // Batterie-Körper
        using var bodyPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x43, 0xA0, 0x47) };
        var bodyRect = SKRect.Create(cx - half * 0.5f, cy - half * 0.7f, half, half * 1.4f);
        canvas.DrawRoundRect(bodyRect, 3, 3, bodyPaint);

        // Batterie-Kappe (oben, schmaler)
        using var capPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xB0, 0xBE, 0xC5) };
        canvas.DrawRect(cx - half * 0.2f, bodyRect.Top - half * 0.15f, half * 0.4f, half * 0.15f, capPaint);

        // "+" Symbol oben
        using var symbolPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = SKColors.White, StrokeWidth = 2, StrokeCap = SKStrokeCap.Round
        };
        float plusY = cy - half * 0.3f;
        canvas.DrawLine(cx - half * 0.15f, plusY, cx + half * 0.15f, plusY, symbolPaint);
        canvas.DrawLine(cx, plusY - half * 0.15f, cx, plusY + half * 0.15f, symbolPaint);

        // "-" Symbol unten
        float minusY = cy + half * 0.3f;
        canvas.DrawLine(cx - half * 0.15f, minusY, cx + half * 0.15f, minusY, symbolPaint);

        // Rand
        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x2E, 0x7D, 0x32), StrokeWidth = 1.5f
        };
        canvas.DrawRoundRect(bodyRect, 3, 3, borderPaint);
    }

    /// <summary>Schalter: Hebel mit Basis.</summary>
    private static void DrawSwitchIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        // Basis (flaches Rechteck unten)
        using var basePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x78, 0x90, 0x9C) };
        var baseRect = SKRect.Create(cx - half * 0.7f, cy + half * 0.1f, half * 1.4f, half * 0.4f);
        canvas.DrawRoundRect(baseRect, 4, 4, basePaint);

        // Drehpunkt (Kreis in der Mitte der Basis)
        using var pivotPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xB0, 0xBE, 0xC5) };
        float pivotY = baseRect.Top;
        canvas.DrawCircle(cx, pivotY, half * 0.12f, pivotPaint);

        // Hebel (diagonal nach oben rechts)
        using var leverPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0xCA, 0x28), StrokeWidth = 3, StrokeCap = SKStrokeCap.Round
        };
        canvas.DrawLine(cx, pivotY, cx + half * 0.5f, cy - half * 0.6f, leverPaint);

        // Griff (Kreis am Hebelende)
        using var handlePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xFF, 0xD5, 0x4F) };
        canvas.DrawCircle(cx + half * 0.5f, cy - half * 0.6f, half * 0.15f, handlePaint);

        // Kontaktpunkte (links und rechts auf Basis)
        using var contactPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xE0, 0xE0, 0xE0) };
        canvas.DrawCircle(baseRect.Left + half * 0.2f, pivotY, half * 0.08f, contactPaint);
        canvas.DrawCircle(baseRect.Right - half * 0.2f, pivotY, half * 0.08f, contactPaint);

        // Rand
        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x54, 0x6E, 0x7A), StrokeWidth = 1.5f
        };
        canvas.DrawRoundRect(baseRect, 4, 4, borderPaint);
    }

    /// <summary>Antenne: Vertikale Linie mit Kreisen (Signalwellen).</summary>
    private static void DrawAntennaIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        // Antennenstab (vertikal)
        using var stickPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xB0, 0xBE, 0xC5), StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round
        };
        canvas.DrawLine(cx, cy + half * 0.5f, cx, cy - half * 0.5f, stickPaint);

        // Basis (kleines Dreieck unten)
        using var basePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x78, 0x90, 0x9C) };
        using var basePath = new SKPath();
        basePath.MoveTo(cx, cy + half * 0.5f);
        basePath.LineTo(cx - half * 0.3f, cy + half * 0.85f);
        basePath.LineTo(cx + half * 0.3f, cy + half * 0.85f);
        basePath.Close();
        canvas.DrawPath(basePath, basePaint);

        // Signalwellen (3 konzentrische Halbkreise oben)
        using var wavePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xBB, 0x86, 0xFC, 0xA0), StrokeWidth = 1.5f
        };
        for (int i = 1; i <= 3; i++)
        {
            float waveR = half * 0.2f * i;
            byte alpha = (byte)(160 - i * 40);
            wavePaint.Color = new SKColor(0xBB, 0x86, 0xFC, alpha);
            canvas.DrawArc(
                SKRect.Create(cx - waveR, cy - half * 0.5f - waveR, waveR * 2, waveR * 2),
                -150, 120, false, wavePaint);
        }
    }

    /// <summary>Fallback-Icon: Einfacher Kreis mit Fragezeichen.</summary>
    private static void DrawDefaultIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var circlePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x80), StrokeWidth = 2
        };
        canvas.DrawCircle(cx, cy, half * 0.6f, circlePaint);

        using var textPaint = new SKPaint
        {
            IsAntialias = true, TextSize = size * 0.5f,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x80), TextAlign = SKTextAlign.Center
        };
        canvas.DrawText("?", cx, cy + size * 0.15f, textPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NUMMER + HÄKCHEN + PARTIKEL
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet die Nummer oder das Fragezeichen in der unteren Hälfte der Kachel.
    /// </summary>
    private static void DrawTileNumber(SKCanvas canvas, SKRect rect, string displayNumber, bool isMemorizing)
    {
        if (string.IsNullOrEmpty(displayNumber)) return;

        bool isQuestion = displayNumber == "?";
        float fontSize = rect.Height * 0.28f;

        using var numberPaint = new SKPaint
        {
            Color = isQuestion ? TextQuestion : TextNumber,
            TextSize = fontSize,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            FakeBoldText = !isQuestion
        };

        float x = rect.MidX;
        float y = rect.Bottom - rect.Height * 0.12f;

        // Schatten unter der Nummer
        if (!isQuestion)
        {
            using var shadowPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 80),
                TextSize = fontSize,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                FakeBoldText = true
            };
            canvas.DrawText(displayNumber, x + 1, y + 1, shadowPaint);
        }

        canvas.DrawText(displayNumber, x, y, numberPaint);

        // Memorisierungsphase: Nummer pulsiert leicht
        if (isMemorizing && !isQuestion)
        {
            using var glowPaint = new SKPaint
            {
                Color = TextWhite.WithAlpha(30),
                TextSize = fontSize + 2,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                FakeBoldText = true
            };
            canvas.DrawText(displayNumber, x, y, glowPaint);
        }
    }

    /// <summary>
    /// Zeichnet ein grünes Häkchen oben rechts in der Kachel.
    /// </summary>
    private static void DrawCheckmark(SKCanvas canvas, SKRect rect)
    {
        float badgeSize = 16;
        float badgeX = rect.Right - badgeSize - 3;
        float badgeY = rect.Top + 3;

        // Grüner Kreis-Hintergrund
        using var circlePaint = new SKPaint
        {
            Color = CheckmarkBg,
            IsAntialias = true
        };
        canvas.DrawCircle(badgeX + badgeSize / 2, badgeY + badgeSize / 2, badgeSize / 2, circlePaint);

        // Häkchen (zwei Linien)
        using var checkPaint = new SKPaint
        {
            Color = TextWhite,
            IsAntialias = true,
            StrokeWidth = 2,
            StrokeCap = SKStrokeCap.Round,
            Style = SKPaintStyle.Stroke
        };

        float checkCx = badgeX + badgeSize / 2;
        float checkCy = badgeY + badgeSize / 2;
        float s = badgeSize * 0.22f;

        // Kurzer Strich nach unten-links
        canvas.DrawLine(checkCx - s * 1.2f, checkCy, checkCx - s * 0.1f, checkCy + s, checkPaint);
        // Langer Strich nach oben-rechts
        canvas.DrawLine(checkCx - s * 0.1f, checkCy + s, checkCx + s * 1.5f, checkCy - s * 0.8f, checkPaint);
    }

    /// <summary>
    /// Aktualisiert und zeichnet die Elektro-Funken-Partikel.
    /// Kleine violette/weiße Lichtpunkte die über den Hintergrund schweben.
    /// </summary>
    private void UpdateAndDrawSparkParticles(SKCanvas canvas, SKRect bounds, float deltaTime)
    {
        var random = Random.Shared;

        // Neue Partikel erzeugen (maximal 15 gleichzeitig)
        while (_sparkParticles.Count < 15)
        {
            _sparkParticles.Add(new SparkParticle
            {
                X = bounds.Left + (float)random.NextDouble() * bounds.Width,
                Y = bounds.Top + (float)random.NextDouble() * bounds.Height,
                VelocityX = ((float)random.NextDouble() - 0.5f) * 20,
                VelocityY = -8 - (float)random.NextDouble() * 12,
                Life = 0,
                MaxLife = 2f + (float)random.NextDouble() * 3f,
                Size = 1 + (float)random.NextDouble() * 2.5f
            });
        }

        // Partikel aktualisieren und zeichnen
        using var sparkPaint = new SKPaint { IsAntialias = false };
        for (int i = _sparkParticles.Count - 1; i >= 0; i--)
        {
            var p = _sparkParticles[i];
            p.Life += deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;

            // Leichtes Flackern (Sinus + Noise)
            p.X += (float)Math.Sin(p.Life * 2.5f + i * 0.7f) * 4 * deltaTime;

            if (p.Life >= p.MaxLife || p.Y < bounds.Top - 10 || p.X < bounds.Left - 10 || p.X > bounds.Right + 10)
            {
                _sparkParticles.RemoveAt(i);
                continue;
            }

            _sparkParticles[i] = p;

            // Alpha: Einblenden -> Halten -> Ausblenden
            float lifeRatio = p.Life / p.MaxLife;
            float alpha;
            if (lifeRatio < 0.15f)
                alpha = lifeRatio / 0.15f;
            else if (lifeRatio > 0.65f)
                alpha = (1 - lifeRatio) / 0.35f;
            else
                alpha = 1;

            // Farbe: Violett-Weiß-Mix (Labor-Atmosphäre / Elektro-Funken)
            byte r = (byte)(180 + random.Next(0, 76));
            byte g = (byte)(130 + random.Next(0, 70));
            byte b = (byte)(220 + random.Next(0, 36));
            sparkPaint.Color = new SKColor(r, g, b, (byte)(alpha * 70));

            canvas.DrawCircle(p.X, p.Y, p.Size, sparkPaint);
        }
    }
}
