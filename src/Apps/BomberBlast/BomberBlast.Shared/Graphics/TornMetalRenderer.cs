using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Statischer Renderer fuer "Torn Metal" Button-Hintergruende.
/// Erzeugt prozedurale beschaedigte Metall-Optik: eingerissene Raender, Risse,
/// abgeplatzte Ecken, Nieten, Kratzer und metallischer Glanz.
/// Deterministisch per Seed - jeder Button sieht anders aber konsistent aus.
/// Gepoolte SKPaint-Objekte fuer GC-Optimierung.
/// </summary>
public static class TornMetalRenderer
{
    // === Gepoolte Paint-Objekte ===
    private static readonly SKPaint MetalFillPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint MetalStrokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke
    };

    private static readonly SKPaint GlowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint ScratchPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round
    };

    // Wiederverwendbarer Path
    private static readonly SKPath _metalPath = new();
    private static readonly SKPath _crackPath = new();

    /// <summary>
    /// Statische Felder vorinitialisieren (SKPaint, SKPath).
    /// Wird im SplashOverlay-Preloader aufgerufen um Jank beim ersten Button-Render zu vermeiden.
    /// </summary>
    public static void Preload()
    {
        // Statische readonly-Felder werden durch diesen Methodenaufruf
        // vom CLR-Klassen-Initializer angelegt
    }

    /// <summary>
    /// Einfacher deterministischer Pseudo-Zufallsgenerator.
    /// Gleicher Seed + Index → immer gleicher Wert (0..1).
    /// </summary>
    private static float Hash(int seed, int index)
    {
        int h = seed * 374761393 + index * 668265263;
        h = (h ^ (h >> 13)) * 1274126177;
        h ^= h >> 16;
        return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
    }

    /// <summary>
    /// Hauptmethode: Zeichnet einen Torn-Metal Button-Hintergrund.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="width">Button-Breite</param>
    /// <param name="height">Button-Hoehe</param>
    /// <param name="baseColor">Basis-Metallfarbe (z.B. Cyan, Gold, Rot)</param>
    /// <param name="seed">Deterministischer Seed (z.B. Hash des Button-Textes)</param>
    /// <param name="damageLevel">Schadens-Intensitaet 0.0-1.0 (0=subtil, 1=stark beschaedigt)</param>
    public static void Render(SKCanvas canvas, float width, float height,
        SKColor baseColor, int seed, float damageLevel = 0.5f)
    {
        if (width < 4 || height < 4) return;

        damageLevel = Math.Clamp(damageLevel, 0f, 1f);

        // 1. Metallischer Hintergrund mit unregelmaeßigem Rand
        DrawMetalBody(canvas, width, height, baseColor, seed, damageLevel);

        // Alles Weitere auf den Torn-Metal-Pfad clippen,
        // damit nichts ueber die eingerissenen Kanten hinausragt
        canvas.Save();
        canvas.ClipPath(_metalPath);

        // 2. Riss-Linien
        DrawCracks(canvas, width, height, baseColor, seed, damageLevel);

        // 3. Kratzer
        DrawScratches(canvas, width, height, baseColor, seed, damageLevel);

        // 4. Nieten an intakten Ecken
        DrawRivets(canvas, width, height, baseColor, seed, damageLevel);

        // 5. Metallischer Glanz-Highlight (oben)
        DrawHighlight(canvas, width, height, baseColor);

        // 6. Subtiler innerer Rand-Glow
        DrawEdgeGlow(canvas, width, height, baseColor, damageLevel);

        canvas.Restore();
    }

    /// <summary>
    /// Zeichnet den Metall-Koerper mit unregelmaeßigen (eingerissenen) Kanten.
    /// </summary>
    private static void DrawMetalBody(SKCanvas canvas, float w, float h,
        SKColor baseColor, int seed, float damage)
    {
        _metalPath.Reset();

        float cornerRadius = Math.Min(w, h) * 0.14f;
        float tearSize = Math.Min(w, h) * 0.28f * damage;
        int segments = 10; // Mehr Segmente = detailliertere Risse

        // Stärkerer Kontrast: hellere Oberseite, dunklere Unterseite
        var darkColor = DarkenColor(baseColor, 0.55f);
        var midColor = baseColor;

        // Metallischer Gradient (oben hell → unten dunkel)
        MetalFillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, h),
            new[] { midColor, darkColor },
            SKShaderTileMode.Clamp);

        // Pfad mit eingerissenen Kanten erstellen
        BuildTornEdgePath(_metalPath, w, h, cornerRadius, tearSize, segments, seed);

        canvas.DrawPath(_metalPath, MetalFillPaint);
        MetalFillPaint.Shader = null;

        // Kräftiger Rand um das Metall
        MetalStrokePaint.Color = DarkenColor(baseColor, 0.6f).WithAlpha(240);
        MetalStrokePaint.StrokeWidth = 2.5f;
        canvas.DrawPath(_metalPath, MetalStrokePaint);

        // Heller innerer Rand (Bevel-Effekt oben)
        MetalStrokePaint.Color = LightenColor(baseColor, 0.35f).WithAlpha(120);
        MetalStrokePaint.StrokeWidth = 1.0f;
        canvas.Save();
        canvas.Translate(0, 1);
        canvas.DrawPath(_metalPath, MetalStrokePaint);
        canvas.Restore();
    }

    /// <summary>
    /// Baut den Path mit Zickzack-Kanten (eingerissene Raender).
    /// Ecken werden teils "abgeplatzt" (Diagonalschnitt statt Rundung).
    /// </summary>
    private static void BuildTornEdgePath(SKPath path, float w, float h,
        float cornerR, float tearSize, int segments, int seed)
    {
        // Bestimme welche Ecken "abgeplatzt" sind - häufiger + größer
        bool chipTopLeft = Hash(seed, 100) > 0.35f;
        bool chipTopRight = Hash(seed, 101) > 0.4f;
        bool chipBottomRight = Hash(seed, 102) > 0.4f;
        bool chipBottomLeft = Hash(seed, 103) > 0.35f;

        float chipSize = cornerR * (1.8f + Hash(seed, 104) * 1.5f);

        // Startpunkt: oben-links (nach Corner)
        float startX = chipTopLeft ? chipSize : cornerR;
        path.MoveTo(startX, 0);

        // === Obere Kante (links → rechts) ===
        float endX = chipTopRight ? w - chipSize : w - cornerR;
        AddTornEdge(path, startX, 0, endX, 0, tearSize, segments, seed, 0, true);

        // Oben-rechts Ecke
        if (chipTopRight)
        {
            // Diagonaler Schnitt
            path.LineTo(w, chipSize);
        }
        else
        {
            path.ArcTo(new SKRect(w - cornerR * 2, 0, w, cornerR * 2), -90, 90, false);
        }

        // === Rechte Kante (oben → unten) ===
        float startY = chipTopRight ? chipSize : cornerR;
        float endY = chipBottomRight ? h - chipSize : h - cornerR;
        AddTornEdge(path, w, startY, w, endY, tearSize, segments, seed, 1, false);

        // Unten-rechts Ecke
        if (chipBottomRight)
        {
            path.LineTo(w - chipSize, h);
        }
        else
        {
            path.ArcTo(new SKRect(w - cornerR * 2, h - cornerR * 2, w, h), 0, 90, false);
        }

        // === Untere Kante (rechts → links) ===
        endX = chipBottomLeft ? chipSize : cornerR;
        startX = chipBottomRight ? w - chipSize : w - cornerR;
        AddTornEdge(path, startX, h, endX, h, tearSize, segments, seed, 2, true);

        // Unten-links Ecke
        if (chipBottomLeft)
        {
            path.LineTo(0, h - chipSize);
        }
        else
        {
            path.ArcTo(new SKRect(0, h - cornerR * 2, cornerR * 2, h), 90, 90, false);
        }

        // === Linke Kante (unten → oben) ===
        endY = chipTopLeft ? chipSize : cornerR;
        startY = chipBottomLeft ? h - chipSize : h - cornerR;
        AddTornEdge(path, 0, startY, 0, endY, tearSize, segments, seed, 3, false);

        // Oben-links Ecke
        if (chipTopLeft)
        {
            path.LineTo(chipSize, 0);
        }
        else
        {
            path.ArcTo(new SKRect(0, 0, cornerR * 2, cornerR * 2), 180, 90, false);
        }

        path.Close();
    }

    /// <summary>
    /// Fuegt eine Kante mit Zickzack-Einrissen zum Path hinzu.
    /// </summary>
    private static void AddTornEdge(SKPath path, float x1, float y1, float x2, float y2,
        float tearSize, int segments, int seed, int edgeIndex, bool horizontal)
    {
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float x = x1 + (x2 - x1) * t;
            float y = y1 + (y2 - y1) * t;

            // Zickzack-Versatz (senkrecht zur Kante)
            float tearOffset = (Hash(seed, edgeIndex * 100 + i) - 0.5f) * 2f * tearSize;

            if (horizontal)
                y += tearOffset;
            else
                x += tearOffset;

            path.LineTo(x, y);
        }
    }

    /// <summary>
    /// Zeichnet Riss-Linien durch den Button-Koerper.
    /// Anzahl skaliert mit Schadens-Level.
    /// </summary>
    private static void DrawCracks(SKCanvas canvas, float w, float h,
        SKColor baseColor, int seed, float damage)
    {
        int crackCount = (int)(3 + damage * 5); // 3-8 Risse

        var crackColor = LightenColor(baseColor, 0.5f).WithAlpha((byte)(130 + damage * 120));

        for (int i = 0; i < crackCount; i++)
        {
            if (Hash(seed, 200 + i) > 0.6f + (1f - damage) * 0.3f) continue;

            _crackPath.Reset();

            // Riss-Startpunkt (am Rand oder nahe dem Rand)
            float startX = w * (0.15f + Hash(seed, 210 + i) * 0.7f);
            float startY = h * (0.1f + Hash(seed, 220 + i) * 0.8f);

            _crackPath.MoveTo(startX, startY);

            // 4-7 Segmente pro Riss (länger + detaillierter)
            int rissSegmente = 4 + (int)(Hash(seed, 230 + i) * 4);
            float crackLength = Math.Min(w, h) * (0.25f + damage * 0.35f);

            float cx = startX;
            float cy = startY;
            float angle = Hash(seed, 240 + i) * MathF.PI * 2; // Zufaellige Richtung

            for (int j = 0; j < rissSegmente; j++)
            {
                float segLen = crackLength / rissSegmente;
                // Richtung leicht aendern (Zickzack)
                angle += (Hash(seed, 250 + i * 10 + j) - 0.5f) * 1.2f;

                cx += MathF.Cos(angle) * segLen;
                cy += MathF.Sin(angle) * segLen;

                // Innerhalb der Bounds halten
                cx = Math.Clamp(cx, 2, w - 2);
                cy = Math.Clamp(cy, 2, h - 2);

                _crackPath.LineTo(cx, cy);
            }

            // Riss-Linie zeichnen (kräftig, gut sichtbar)
            ScratchPaint.Color = crackColor;
            ScratchPaint.StrokeWidth = 2.0f + damage * 2.0f;
            canvas.DrawPath(_crackPath, ScratchPaint);

            // Dunkler Schatten unter dem Riss (Tiefe)
            ScratchPaint.Color = DarkenColor(baseColor, 0.7f).WithAlpha((byte)(80 + damage * 80));
            ScratchPaint.StrokeWidth = 2.0f + damage * 1.0f;
            canvas.Save();
            canvas.Translate(0.5f, 1f);
            canvas.DrawPath(_crackPath, ScratchPaint);
            canvas.Restore();

            // Helle Reflexion im Riss (Kante fängt Licht)
            ScratchPaint.Color = LightenColor(baseColor, 0.7f).WithAlpha((byte)(50 + damage * 60));
            ScratchPaint.StrokeWidth = 0.6f;

            // Leicht versetzt zeichnen
            canvas.Save();
            canvas.Translate(0.5f, 0.5f);
            canvas.DrawPath(_crackPath, ScratchPaint);
            canvas.Restore();
        }
    }

    /// <summary>
    /// Zeichnet feine Kratzer ueber die Metall-Oberflaeche.
    /// </summary>
    private static void DrawScratches(SKCanvas canvas, float w, float h,
        SKColor baseColor, int seed, float damage)
    {
        int scratchCount = 4 + (int)(damage * 7); // 4-11 Kratzer

        for (int i = 0; i < scratchCount; i++)
        {
            float sx = w * Hash(seed, 300 + i * 2);
            float sy = h * Hash(seed, 301 + i * 2);

            // Kratzer-Richtung (meist diagonal)
            float angle = Hash(seed, 310 + i) * MathF.PI;
            float len = Math.Min(w, h) * (0.15f + Hash(seed, 320 + i) * 0.35f * damage);

            float ex = sx + MathF.Cos(angle) * len;
            float ey = sy + MathF.Sin(angle) * len;

            // Innerhalb der Bounds halten
            ex = Math.Clamp(ex, 0, w);
            ey = Math.Clamp(ey, 0, h);

            // Dunkle Kratzer-Linie (Vertiefung)
            ScratchPaint.Color = DarkenColor(baseColor, 0.4f).WithAlpha((byte)(80 + damage * 80));
            ScratchPaint.StrokeWidth = 1.0f + Hash(seed, 330 + i) * 1.2f;
            canvas.DrawLine(sx, sy, ex, ey, ScratchPaint);

            // Helle Licht-Kante am Kratzer
            ScratchPaint.Color = LightenColor(baseColor, 0.5f).WithAlpha((byte)(60 + damage * 60));
            ScratchPaint.StrokeWidth = 0.5f + Hash(seed, 330 + i) * 0.5f;
            canvas.DrawLine(sx, sy + 0.8f, ex, ey + 0.8f, ScratchPaint);
        }
    }

    /// <summary>
    /// Zeichnet Nieten an den intakten Ecken.
    /// </summary>
    private static void DrawRivets(SKCanvas canvas, float w, float h,
        SKColor baseColor, int seed, float damage)
    {
        float rivetRadius = Math.Min(w, h) * 0.05f;
        if (rivetRadius < 3.0f) rivetRadius = 3.0f;
        float margin = rivetRadius * 2.5f;

        // 4 Eckpositionen
        (float x, float y)[] corners =
        [
            (margin, margin),                // oben-links
            (w - margin, margin),            // oben-rechts
            (w - margin, h - margin),        // unten-rechts
            (margin, h - margin)             // unten-links
        ];

        for (int i = 0; i < 4; i++)
        {
            // Abgeplatzte Ecken bekommen keine Niete
            bool chipped = Hash(seed, 100 + i) > (i is 0 or 3 ? 0.45f : 0.5f);
            if (chipped) continue;

            var (cx, cy) = corners[i];

            // Nieten-Schatten (3D-Effekt)
            MetalFillPaint.Shader = null;
            MetalFillPaint.Color = DarkenColor(baseColor, 0.7f).WithAlpha(100);
            canvas.DrawCircle(cx + 0.5f, cy + 1f, rivetRadius + 0.5f, MetalFillPaint);

            // Nieten-Koerper (Gradient: oben hell → unten dunkel)
            MetalFillPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx, cy - rivetRadius),
                new SKPoint(cx, cy + rivetRadius),
                new[] { LightenColor(baseColor, 0.1f), DarkenColor(baseColor, 0.5f) },
                SKShaderTileMode.Clamp);
            canvas.DrawCircle(cx, cy, rivetRadius, MetalFillPaint);
            MetalFillPaint.Shader = null;

            // Nieten-Highlight (oben-links reflektiert Licht, kräftiger)
            MetalFillPaint.Color = LightenColor(baseColor, 0.6f).WithAlpha(220);
            canvas.DrawCircle(cx - rivetRadius * 0.3f, cy - rivetRadius * 0.3f,
                rivetRadius * 0.35f, MetalFillPaint);

            // Nieten-Rand (kräftiger)
            MetalStrokePaint.Color = DarkenColor(baseColor, 0.65f).WithAlpha(220);
            MetalStrokePaint.StrokeWidth = 1.0f;
            canvas.DrawCircle(cx, cy, rivetRadius, MetalStrokePaint);
        }
    }

    /// <summary>
    /// Zeichnet einen metallischen Glanz-Streifen am oberen Rand.
    /// </summary>
    private static void DrawHighlight(SKCanvas canvas, float w, float h, SKColor baseColor)
    {
        var highlightColor = LightenColor(baseColor, 0.6f);
        float highlightHeight = h * 0.4f;

        GlowPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, highlightHeight),
            new[] { highlightColor.WithAlpha(100), SKColors.Transparent },
            SKShaderTileMode.Clamp);

        canvas.DrawRect(0, 0, w, highlightHeight, GlowPaint);
        GlowPaint.Shader = null;

        // Helle Kante ganz oben (Metall-Bevel)
        ScratchPaint.Color = LightenColor(baseColor, 0.7f).WithAlpha(85);
        ScratchPaint.StrokeWidth = 1.2f;
        canvas.DrawLine(6, 1.5f, w - 6, 1.5f, ScratchPaint);
    }

    /// <summary>
    /// Zeichnet einen subtilen Glow am inneren Rand (Kantenbeleuchtung).
    /// </summary>
    private static void DrawEdgeGlow(SKCanvas canvas, float w, float h,
        SKColor baseColor, float damage)
    {
        float inset = 2f;
        byte glowAlpha = (byte)(65 + (1f - damage) * 50);

        MetalStrokePaint.Color = LightenColor(baseColor, 0.5f).WithAlpha(glowAlpha);
        MetalStrokePaint.StrokeWidth = 1.5f;
        MetalStrokePaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3.5f);

        float cr = Math.Min(w, h) * 0.1f;
        canvas.DrawRoundRect(inset, inset, w - inset * 2, h - inset * 2, cr, cr, MetalStrokePaint);
        MetalStrokePaint.MaskFilter = null;

        // Dunkle untere Kante (Schatten, Tiefe)
        ScratchPaint.Color = DarkenColor(baseColor, 0.5f).WithAlpha(45);
        ScratchPaint.StrokeWidth = 1.5f;
        canvas.DrawLine(8, h - 2, w - 8, h - 2, ScratchPaint);
    }

    // === Farb-Helfer ===

    /// <summary>Farbe aufhellen (factor 0..1)</summary>
    private static SKColor LightenColor(SKColor color, float factor)
    {
        return new SKColor(
            (byte)Math.Min(255, color.Red + (255 - color.Red) * factor),
            (byte)Math.Min(255, color.Green + (255 - color.Green) * factor),
            (byte)Math.Min(255, color.Blue + (255 - color.Blue) * factor),
            color.Alpha);
    }

    /// <summary>Farbe abdunkeln (factor 0..1)</summary>
    private static SKColor DarkenColor(SKColor color, float factor)
    {
        return new SKColor(
            (byte)(color.Red * (1f - factor)),
            (byte)(color.Green * (1f - factor)),
            (byte)(color.Blue * (1f - factor)),
            color.Alpha);
    }
}
