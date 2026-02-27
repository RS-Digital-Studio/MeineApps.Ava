using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// CPU-basierte Explosions-Effekte: Flammen, Heat Haze.
/// Arm-basiertes Rendering: Jeder Explosions-Arm wird als durchgehender
/// Flammenstreifen gerendert (kein Pro-Zelle-Rendering → keine sichtbaren Übergänge).
/// </summary>
public static class ExplosionShaders
{
    // Noise-Lookup-Tabelle (vorberechnet, schnell)
    private static readonly float[] _noiseLUT = new float[512];
    private static bool _noiseInitialized;

    private static void InitNoise()
    {
        if (_noiseInitialized) return;
        var rng = new Random(42); // Deterministisch
        for (int i = 0; i < 512; i++)
            _noiseLUT[i] = (float)rng.NextDouble();
        _noiseInitialized = true;
    }

    /// <summary>Schnelles Hash-Noise für Flammen-Textur</summary>
    private static float Noise(float x, float y)
    {
        int ix = ((int)MathF.Floor(x)) & 511;
        int iy = ((int)MathF.Floor(y)) & 511;
        float fx = x - MathF.Floor(x);
        float fy = y - MathF.Floor(y);

        // Smoothstep
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        float a = _noiseLUT[(ix + iy * 37) & 511];
        float b = _noiseLUT[(ix + 1 + iy * 37) & 511];
        float c = _noiseLUT[(ix + (iy + 1) * 37) & 511];
        float d = _noiseLUT[(ix + 1 + (iy + 1) * 37) & 511];

        float ab = a + (b - a) * fx;
        float cd = c + (d - c) * fx;
        return ab + (cd - ab) * fy;
    }

    /// <summary>FBM (Fraktales Rauschen) - 3 Oktaven</summary>
    private static float Fbm(float x, float y)
    {
        float v = 0f;
        float a = 0.5f;
        for (int i = 0; i < 3; i++)
        {
            v += a * Noise(x, y);
            x = x * 2f + 100f;
            y = y * 2f + 100f;
            a *= 0.5f;
        }
        return v;
    }

    // Gecachte Paint-Objekte (GC-Optimierung)
    private static readonly SKPaint _flamePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKMaskFilter _softGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4);
    private static readonly SKPath _armPath = new();

    // ═══════════════════════════════════════════════════════════════════════
    // CENTER-FEUERBALL
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Center-Zelle: Kräftiger radialer Feuerball.
    /// Wird einmal pro Explosion gerendert.
    /// </summary>
    public static void DrawCenterFire(SKCanvas canvas, float cx, float cy, float cellSize,
        float time, SKColor colorOuter, SKColor colorInner, SKColor colorCore, float envelope)
    {
        InitNoise();

        if (envelope < 0.01f) return;

        float radius = cellSize * 0.6f;

        // Noise für Pulsation
        float n = Fbm(time * 1.5f, time * 1.2f);
        float pulse = 0.9f + n * 0.2f;

        // Schicht 1: Weicher äußerer Glow (dunkel-orange, groß)
        float r1 = radius * 1.1f * pulse;
        byte a1 = (byte)Math.Clamp(envelope * 200f, 0, 255);
        using (var shader = SKShader.CreateRadialGradient(
            new SKPoint(cx, cy), r1,
            new[] { colorOuter.WithAlpha(a1), colorOuter.WithAlpha((byte)(a1 * 0.4f)), SKColors.Transparent },
            new[] { 0f, 0.5f, 1f },
            SKShaderTileMode.Clamp))
        {
            _flamePaint.Shader = shader;
            _flamePaint.MaskFilter = _softGlow;
            canvas.DrawCircle(cx, cy, r1 * 1.2f, _flamePaint);
            _flamePaint.MaskFilter = null;
            _flamePaint.Shader = null;
        }

        // Schicht 2: Mittlerer Feuerball (orange→gelb, kräftig)
        float r2 = radius * 0.7f * pulse;
        byte a2 = (byte)Math.Clamp(envelope * 240f, 0, 255);
        using (var shader = SKShader.CreateRadialGradient(
            new SKPoint(cx, cy), r2,
            new[] { colorInner.WithAlpha(a2), colorOuter.WithAlpha((byte)(a2 * 0.6f)), SKColors.Transparent },
            new[] { 0f, 0.45f, 1f },
            SKShaderTileMode.Clamp))
        {
            _flamePaint.Shader = shader;
            canvas.DrawCircle(cx, cy, r2 * 1.1f, _flamePaint);
            _flamePaint.Shader = null;
        }

        // Schicht 3: Heißer Kern (weiß-gelb)
        float r3 = radius * 0.35f;
        byte a3 = (byte)Math.Clamp(envelope * 220f, 0, 255);
        using (var shader = SKShader.CreateRadialGradient(
            new SKPoint(cx, cy), r3,
            new[] { colorCore.WithAlpha(a3), colorInner.WithAlpha((byte)(a3 * 0.5f)), SKColors.Transparent },
            new[] { 0f, 0.4f, 1f },
            SKShaderTileMode.Clamp))
        {
            _flamePaint.Shader = shader;
            canvas.DrawCircle(cx, cy, r3 * 1.2f, _flamePaint);
            _flamePaint.Shader = null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ARM-BASIERTES FLAMMEN-RENDERING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Einen kompletten Explosions-Arm als durchgehenden Flammenstreifen rendern.
    /// Kein Pro-Zelle-Rendering → nahtlose Übergänge, natürliche Verjüngung.
    /// </summary>
    public static void DrawFlameArm(SKCanvas canvas, float cx, float cy,
        int armLength, int dx, int dy, float cellSize,
        float time, SKColor colorOuter, SKColor colorInner, SKColor colorCore, float envelope)
    {
        InitNoise();

        if (envelope < 0.01f || armLength <= 0) return;

        bool isHorizontal = dx != 0;

        // Gesamtlänge: Vom Center-Rand (halbe Zelle) bis zum Ende der letzten Zelle
        // armLength Zellen ab Center → letzte Zelle endet bei armLength * cellSize + cellSize/2
        // Aber wir starten ab cellSize * 0.3 (leichte Überlappung mit Center)
        float startOffset = cellSize * 0.3f;
        float endPos = armLength * cellSize + cellSize * 0.4f; // Bis knapp hinter die letzte Zelle
        float fullLength = endPos - startOffset;

        // Basisbreite der Flamme (quer zur Achse) - kräftig!
        float baseWidth = cellSize * 0.45f;

        // Noise-Seed pro Arm (damit jeder Arm anders flackert)
        float armSeed = (dx + dy * 3f + 5f) * 17f;

        // === Schicht 1: Weicher Glow (breiter, diffuser Hintergrund) ===
        DrawFlameLayerSolid(canvas, cx, cy, dx, dy, startOffset, fullLength, baseWidth * 1.8f,
            time, armSeed, colorOuter, envelope * 0.55f, _softGlow, isHorizontal, taperExponent: 1.5f);

        // === Schicht 2: Hauptflamme (kräftig, orange-gelb) ===
        DrawFlameLayerGradient(canvas, cx, cy, dx, dy, startOffset, fullLength, baseWidth,
            time, armSeed + 7f, colorOuter, colorInner, envelope * 0.9f, null, isHorizontal, taperExponent: 1.2f);

        // === Schicht 3: Heller Kern (schmal, gelb-weiß) ===
        DrawFlameLayerGradient(canvas, cx, cy, dx, dy, startOffset, fullLength, baseWidth * 0.4f,
            time, armSeed + 13f, colorInner, colorCore, envelope * 0.75f, null, isHorizontal, taperExponent: 0.9f);

        // === Flammen-Zungen entlang des Arms ===
        DrawArmFlameTongues(canvas, cx, cy, dx, dy, fullLength, startOffset, baseWidth,
            time, armSeed, colorOuter, colorInner, envelope, isHorizontal);
    }

    /// <summary>
    /// Einfarb-Flammenschicht (z.B. für Glow-Hintergrund).
    /// </summary>
    private static void DrawFlameLayerSolid(SKCanvas canvas, float cx, float cy,
        int dx, int dy, float startOffset, float length, float width,
        float time, float seed, SKColor color, float envelope,
        SKMaskFilter? maskFilter, bool isHorizontal, float taperExponent)
    {
        byte alpha = (byte)Math.Clamp(envelope * 220f, 0, 255);
        if (alpha < 3) return;

        BuildFlamePath(cx, cy, dx, dy, startOffset, length, width,
            time, seed, isHorizontal, taperExponent);

        _flamePaint.Color = color.WithAlpha(alpha);
        _flamePaint.Shader = null;
        _flamePaint.MaskFilter = maskFilter;
        canvas.DrawPath(_armPath, _flamePaint);
        _flamePaint.MaskFilter = null;
    }

    /// <summary>
    /// Zwei-Farben-Gradient Flammenschicht (quer zur Achse: Rand dunkel, Mitte hell).
    /// </summary>
    private static void DrawFlameLayerGradient(SKCanvas canvas, float cx, float cy,
        int dx, int dy, float startOffset, float length, float width,
        float time, float seed, SKColor colorOuter, SKColor colorInner, float envelope,
        SKMaskFilter? maskFilter, bool isHorizontal, float taperExponent)
    {
        byte alpha = (byte)Math.Clamp(envelope * 240f, 0, 255);
        if (alpha < 3) return;

        BuildFlamePath(cx, cy, dx, dy, startOffset, length, width,
            time, seed, isHorizontal, taperExponent);

        // Solid Color statt 5-Stop-Gradient (eliminiert native Shader-Allokation pro Arm/Layer).
        // MaskFilter-Blur auf Glow-Layer erzeugt bereits weiche Kanten,
        // und der noise-modulierte Pfad sorgt für organische Ränder.
        byte blendAlpha = (byte)Math.Clamp(alpha * 0.75f, 0, 255);
        _flamePaint.Shader = null;
        _flamePaint.Color = new SKColor(
            (byte)((colorOuter.Red + colorInner.Red) / 2),
            (byte)((colorOuter.Green + colorInner.Green) / 2),
            (byte)((colorOuter.Blue + colorInner.Blue) / 2),
            blendAlpha);
        _flamePaint.MaskFilter = maskFilter;
        canvas.DrawPath(_armPath, _flamePaint);
        _flamePaint.MaskFilter = null;
    }

    /// <summary>
    /// Flammen-Pfad mit noise-modulierten Rändern aufbauen.
    /// </summary>
    private static void BuildFlamePath(float cx, float cy,
        int dx, int dy, float startOffset, float length, float width,
        float time, float seed, bool isHorizontal, float taperExponent)
    {
        _armPath.Reset();

        const int SEGMENTS = 14;

        // Zwei Seiten des Pfads (oben/links und unten/rechts)
        Span<float> side1X = stackalloc float[SEGMENTS + 1];
        Span<float> side1Y = stackalloc float[SEGMENTS + 1];
        Span<float> side2X = stackalloc float[SEGMENTS + 1];
        Span<float> side2Y = stackalloc float[SEGMENTS + 1];

        for (int i = 0; i <= SEGMENTS; i++)
        {
            float t = i / (float)SEGMENTS; // 0→1 entlang des Arms

            // Position entlang der Achse
            float dist = startOffset + t * length;
            float posX = cx + dx * dist;
            float posY = cy + dy * dist;

            // Sanfter Start (aus dem Center-Feuerball heraus)
            float startTaper = Math.Min(1f, t * 3f); // Erste ~33% einblenden
            startTaper = startTaper * startTaper * (3f - 2f * startTaper); // Smoothstep

            // Verjüngung zum Ende
            float endTaper = 1f - MathF.Pow(t, taperExponent);
            endTaper = Math.Max(0f, endTaper);

            // Noise-modulierte Breite (organisches Wabern)
            float n1 = Fbm(time * 2.5f + seed + t * 5f, time * 1.8f + seed);
            float n2 = Fbm(time * 2.2f + seed + 50f + t * 5f, time * 2f + seed + 30f);
            float noiseModulation = 0.8f + n1 * 0.4f; // 0.8→1.2

            float halfWidth = width * startTaper * endTaper * noiseModulation;

            // Seitliche Auslenkung (Flamme "tanzt")
            float sway = (n2 - 0.5f) * width * 0.25f * t;

            if (isHorizontal)
            {
                side1X[i] = posX;
                side1Y[i] = posY - halfWidth + sway;
                side2X[i] = posX;
                side2Y[i] = posY + halfWidth + sway;
            }
            else
            {
                side1X[i] = posX - halfWidth + sway;
                side1Y[i] = posY;
                side2X[i] = posX + halfWidth + sway;
                side2Y[i] = posY;
            }
        }

        // Pfad: Seite 1 vorwärts, dann Seite 2 rückwärts
        _armPath.MoveTo(side1X[0], side1Y[0]);
        for (int i = 1; i <= SEGMENTS; i++)
        {
            float mx = (side1X[i - 1] + side1X[i]) * 0.5f;
            float my = (side1Y[i - 1] + side1Y[i]) * 0.5f;
            _armPath.QuadTo(side1X[i - 1], side1Y[i - 1], mx, my);
        }
        _armPath.LineTo(side1X[SEGMENTS], side1Y[SEGMENTS]);

        // Spitze
        _armPath.LineTo(side2X[SEGMENTS], side2Y[SEGMENTS]);

        // Seite 2 rückwärts
        for (int i = SEGMENTS - 1; i >= 0; i--)
        {
            float mx = (side2X[i + 1] + side2X[i]) * 0.5f;
            float my = (side2Y[i + 1] + side2Y[i]) * 0.5f;
            _armPath.QuadTo(side2X[i + 1], side2Y[i + 1], mx, my);
        }
        _armPath.LineTo(side2X[0], side2Y[0]);
        _armPath.Close();
    }

    /// <summary>
    /// Flammen-Zungen entlang des Arms.
    /// </summary>
    private static void DrawArmFlameTongues(SKCanvas canvas, float cx, float cy,
        int dx, int dy, float length, float startOffset, float baseWidth,
        float time, float armSeed, SKColor colorOuter, SKColor colorInner,
        float envelope, bool isHorizontal)
    {
        int tongueCount = Math.Clamp((int)(length / 15f), 1, 5);

        for (int i = 0; i < tongueCount; i++)
        {
            float n = Fbm(time * 3f + i * 30f + armSeed, time * 2f + i * 17f);
            if (n < 0.3f) continue;

            float t = (i + 0.5f) / tongueCount;
            float dist = startOffset + t * length;

            // Lokale Verjüngung
            float taper = 1f - t * 0.7f;

            float posX = cx + dx * dist;
            float posY = cy + dy * dist;

            float tongueHeight = baseWidth * taper * (0.5f + n * 0.6f);
            float tongueWidth = baseWidth * taper * 0.25f;

            float side = Fbm(time * 2.5f + i * 50f, i * 20f) > 0.5f ? -1f : 1f;
            float sideOffset = baseWidth * taper * 0.2f;

            byte a = (byte)Math.Clamp(envelope * 180f * (n - 0.2f), 0, 255);
            if (a < 5) continue;

            var tongueColor = LerpColor(colorOuter, colorInner, n * 0.5f);

            float tx, ty, tw, th;

            if (isHorizontal)
            {
                tx = posX - tongueWidth * 0.5f;
                ty = side > 0 ? cy - sideOffset - tongueHeight : cy + sideOffset;
                tw = tongueWidth;
                th = tongueHeight;
            }
            else
            {
                tx = side > 0 ? cx - sideOffset - tongueHeight : cx + sideOffset;
                ty = posY - tongueWidth * 0.5f;
                tw = tongueHeight;
                th = tongueWidth;
            }

            // Solid Color statt Gradient (eliminiert native Shader-Allokation pro Zunge)
            _flamePaint.Shader = null;
            _flamePaint.Color = tongueColor.WithAlpha((byte)(a * 0.6f));
            float cr = Math.Min(tw, th) * 0.4f;
            canvas.DrawRoundRect(tx, ty, tw, th, cr, cr, _flamePaint);
        }
    }

    /// <summary>Farbmischung zwischen zwei SKColors</summary>
    private static SKColor LerpColor(SKColor a, SKColor b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue + (b.Blue - a.Blue) * t),
            (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ENVELOPE-BERECHNUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet die Flammen-Hüllkurve (Aufflackern → Plateau → Abklingen).
    /// </summary>
    public static float CalculateEnvelope(float progress, float alpha)
    {
        float envelope;
        if (progress < 0.08f)
            envelope = progress / 0.08f;
        else if (progress < 0.5f)
            envelope = 1f;
        else
            envelope = 1f - (progress - 0.5f) / 0.5f;
        return Math.Max(0f, envelope) * alpha;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HEAT HAZE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wärme-Distortion über einen Bereich rendern.
    /// Subtiler gelb-oranger Schimmer der aufsteigt.
    /// </summary>
    public static void DrawHeatHaze(SKCanvas canvas, SKRect rect, float time,
        float intensity, SKPaint paint)
    {
        if (intensity < 0.01f) return;
        InitNoise();

        byte a = (byte)Math.Clamp(intensity * 30f, 0, 255);
        if (a < 2) return;

        // 2-Schritt Alpha statt LinearGradient (eliminiert native Shader-Allokation pro Explosion)
        paint.Shader = null;
        float halfH = rect.Height * 0.5f;
        // Untere Hälfte: kräftiger (warm-orange)
        paint.Color = new SKColor(255, 180, 80, (byte)(a * 0.7f));
        canvas.DrawRect(rect.Left, rect.MidY, rect.Width, halfH, paint);
        // Obere Hälfte: schwächer (hell-orange, aufsteigend)
        paint.Color = new SKColor(255, 220, 100, (byte)(a * 0.25f));
        canvas.DrawRect(rect.Left, rect.Top, rect.Width, halfH, paint);
    }
}
