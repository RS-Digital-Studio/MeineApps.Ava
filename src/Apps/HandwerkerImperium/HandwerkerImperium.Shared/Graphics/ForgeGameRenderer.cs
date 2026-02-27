using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// AAA SkiaSharp-Renderer fuer das Schmiede-Minigame.
/// Metallischer Amboss mit Gradienten, gluehnendes Werkstueck mit Heat-Shimmer,
/// Schmiedehammer mit Impact-Schockwelle, Esse mit Glut-Partikeln und mehrstufigen Flammen,
/// zonenabhaengige Funkenfarben (Gold/Orange/Gelb/Rot), Dampf vom heissen Werkstueck,
/// Completion-Celebration mit goldenem Glow.
/// Struct-basierte Partikel-Arrays fuer GC-freie Android-Performance.
/// </summary>
public class ForgeGameRenderer
{
    // ═══════════════════════════════════════════════════════════════════════
    // Partikel-System (Struct-basiert, kein GC)
    // ═══════════════════════════════════════════════════════════════════════

    private const int MAX_SPARKS = 80;
    private const int MAX_SMOKE = 20;
    private const int MAX_EMBERS = 15;

    private struct SparkParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Size;
        public byte R, G, B;
        public bool IsGolden; // Goldene Funken bei Perfect-Hit
    }

    private struct SmokeParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Size;
        public byte Brightness;
    }

    private struct EmberParticle
    {
        public float X, Y, Life, MaxLife, Size;
        public float PulseOffset;
    }

    private readonly SparkParticle[] _sparks = new SparkParticle[MAX_SPARKS];
    private int _sparkCount;
    private readonly SmokeParticle[] _smoke = new SmokeParticle[MAX_SMOKE];
    private int _smokeCount;
    private readonly EmberParticle[] _embers = new EmberParticle[MAX_EMBERS];
    private int _emberCount;

    // ═══════════════════════════════════════════════════════════════════════
    // State-Tracking
    // ═══════════════════════════════════════════════════════════════════════

    private float _animTime;
    private float _hammerAnimTime;
    private bool _wasHammering;
    private int _prevHitsCompleted;
    private bool _completionDetected;
    private float _completionTime;
    private float _shockwaveTime = -1; // < 0 = inaktiv
    private float _shockwaveCenterX, _shockwaveCenterY;
    private float _missFlashTime = -1;
    private float _perfectFlashTime = -1;
    private int _lastHitZone; // 0=Miss, 1=Ok, 2=Good, 3=Perfect

    // ═══════════════════════════════════════════════════════════════════════
    // Gecachte Paints (statisch fuer feste Farben)
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly SKPaint AnvilBodyPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };
    private static readonly SKPaint AnvilEdgePaint = new()
    {
        Color = new SKColor(0x25, 0x25, 0x25),
        IsAntialias = true,
        StrokeWidth = 2,
        Style = SKPaintStyle.Stroke
    };
    private static readonly SKPaint HammerHandlePaint = new()
    {
        IsAntialias = true
    };
    private static readonly SKPaint FramePaint = new()
    {
        Color = new SKColor(0x5D, 0x5D, 0x5D),
        IsAntialias = true,
        StrokeWidth = 2,
        Style = SKPaintStyle.Stroke
    };
    private static readonly SKPaint TickPaint = new()
    {
        Color = new SKColor(255, 255, 255, 40),
        IsAntialias = false,
        StrokeWidth = 1
    };

    // Dynamische Paints (mutierbar)
    private readonly SKPaint _glowPaint = new() { IsAntialias = true };
    private readonly SKPaint _fillPaint = new() { IsAntialias = true };
    private readonly SKPaint _sparkPaint = new() { IsAntialias = true };
    private readonly SKPaint _smokePaint = new() { IsAntialias = true };
    private readonly SKPaint _textPaint = new() { IsAntialias = true, TextSize = 10 };

    // Gecachte Blur-Filter
    private static readonly SKMaskFilter Blur4 = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4);
    private static readonly SKMaskFilter Blur6 = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6);
    private static readonly SKMaskFilter Blur10 = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10);
    private static readonly SKMaskFilter Blur16 = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 16);

    // ═══════════════════════════════════════════════════════════════════════
    // Farb-Konstanten
    // ═══════════════════════════════════════════════════════════════════════

    // Metall-Farben (temperaturabhaengig)
    private static readonly SKColor MetalCold = new(0x6B, 0x3A, 0x10);
    private static readonly SKColor MetalWarm = new(0xE8, 0x5C, 0x00);
    private static readonly SKColor MetalHot = new(0xFF, 0xC4, 0x00);
    private static readonly SKColor MetalWhiteHot = new(0xFF, 0xF8, 0xE0);

    // Zonen-Farben
    private static readonly SKColor PerfectColor = new(0xFF, 0x6B, 0x00);
    private static readonly SKColor GoodColor = new(0xE8, 0xA0, 0x0E);
    private static readonly SKColor OkColor = new(0xFF, 0xC1, 0x07);
    private static readonly SKColor ColdColor = new(0x42, 0x72, 0xB0);
    private static readonly SKColor TooHotColor = new(0xEF, 0x44, 0x44);

    // Feuer-Farben
    private static readonly SKColor FireDark = new(0xFF, 0x5A, 0x00);
    private static readonly SKColor FireMedium = new(0xFF, 0xA0, 0x00);
    private static readonly SKColor FireLight = new(0xFF, 0xE8, 0x60);
    private static readonly SKColor FireWhite = new(0xFF, 0xFF, 0xD0);

    // ═══════════════════════════════════════════════════════════════════════
    // Hauptmethode
    // ═══════════════════════════════════════════════════════════════════════

    public void Render(SKCanvas canvas, SKRect bounds,
        double temperature,
        double targetStart, double targetWidth,
        double goodStart, double goodWidth,
        double okStart, double okWidth,
        int hitsCompleted, int hitsRequired,
        bool isPlaying, bool isResultShown,
        bool isHammering,
        bool isAllComplete,
        float deltaTime)
    {
        _animTime += deltaTime;

        // Hammer-Animation und Effekte tracken
        if (isHammering && !_wasHammering)
        {
            _hammerAnimTime = 0;
            // Zonenbestimmung aus aktueller Temperatur
            _lastHitZone = DetermineZone(temperature, targetStart, targetWidth,
                goodStart, goodWidth, okStart, okWidth);
            SpawnHitSparks(bounds, temperature, _lastHitZone);
            SpawnHitSteam(bounds);

            // Effekte je nach Zone
            float anvilCenterX = bounds.Left + bounds.Width * 0.5f;
            float anvilCenterY = bounds.Top + 16 + (bounds.Height - 32) * 0.32f;
            if (_lastHitZone == 3) // Perfect
            {
                _perfectFlashTime = 0;
                _shockwaveTime = 0;
                _shockwaveCenterX = anvilCenterX;
                _shockwaveCenterY = anvilCenterY;
            }
            else if (_lastHitZone == 0) // Miss
            {
                _missFlashTime = 0;
            }
        }
        _wasHammering = isHammering;
        if (isHammering) _hammerAnimTime += deltaTime;

        // Completion-Erkennung
        if (hitsCompleted >= hitsRequired && hitsRequired > 0 && !_completionDetected && _prevHitsCompleted < hitsRequired)
        {
            _completionDetected = true;
            _completionTime = 0;
            SpawnCompletionSparks(bounds);
        }
        if (_completionDetected) _completionTime += deltaTime;
        _prevHitsCompleted = hitsCompleted;

        // Reset bei neuem Spiel
        if (hitsCompleted == 0 && _prevHitsCompleted > 0)
        {
            _completionDetected = false;
            _completionTime = 0;
            _sparkCount = 0;
            _smokeCount = 0;
        }

        float padding = 14;
        float areaTop = bounds.Top + padding;
        float areaBottom = bounds.Bottom - padding;
        float areaHeight = areaBottom - areaTop;

        // Hintergrund: Esse/Schmiedefeuer links
        DrawForge(canvas, bounds, padding, areaTop, areaHeight, temperature, isPlaying);

        // Amboss mit Werkstueck
        float anvilTop = areaTop + areaHeight * 0.15f;
        float anvilHeight = areaHeight * 0.32f;
        float centerX = bounds.Left + bounds.Width * 0.5f;

        // Completion-Glow hinter Amboss
        if (_completionDetected && _completionTime < 3.0f)
        {
            DrawCompletionGlow(canvas, centerX, anvilTop + anvilHeight * 0.5f, _completionTime);
        }

        DrawAnvil(canvas, centerX, anvilTop, anvilHeight);
        DrawWorkpiece(canvas, centerX, anvilTop, anvilHeight, temperature);

        // Hammer
        float hammerBaseY = anvilTop - 8;
        DrawHammer(canvas, centerX, hammerBaseY, isPlaying, isHammering);

        // Schlag-Fortschritt
        DrawHitProgress(canvas, bounds, anvilTop + anvilHeight + 10, hitsCompleted, hitsRequired);

        // Temperatur-Bar
        float barTop = anvilTop + anvilHeight + 32;
        float barHeight = Math.Min(46, areaHeight * 0.16f);
        float barLeft = bounds.Left + padding + 6;
        float barWidth = bounds.Width - 2 * padding - 12;
        DrawTemperatureBar(canvas, barLeft, barTop, barWidth, barHeight,
            temperature, targetStart, targetWidth, goodStart, goodWidth, okStart, okWidth, isPlaying);

        // Partikel-Systeme
        UpdateAndDrawSparks(canvas, deltaTime);
        UpdateAndDrawSmoke(canvas, deltaTime);
        UpdateAndDrawEmbers(canvas, deltaTime);

        // Schockwelle bei Perfect-Hit
        if (_shockwaveTime >= 0 && _shockwaveTime < 0.5f)
        {
            DrawShockwave(canvas);
            _shockwaveTime += deltaTime;
            if (_shockwaveTime >= 0.5f) _shockwaveTime = -1;
        }

        // Perfect-Hit Flash (weiss-blauer Blitz auf Amboss)
        if (_perfectFlashTime >= 0 && _perfectFlashTime < 0.3f)
        {
            DrawPerfectFlash(canvas, centerX, anvilTop, anvilHeight);
            _perfectFlashTime += deltaTime;
            if (_perfectFlashTime >= 0.3f) _perfectFlashTime = -1;
        }

        // Miss Flash (rot)
        if (_missFlashTime >= 0 && _missFlashTime < 0.25f)
        {
            DrawMissFlash(canvas, bounds);
            _missFlashTime += deltaTime;
            if (_missFlashTime >= 0.25f) _missFlashTime = -1;
        }

        // Esse-Glut-Partikel spawnen (kontinuierlich)
        if (isPlaying && _animTime % 0.4f < deltaTime)
        {
            SpawnEmber(bounds, padding, areaTop, areaHeight);
        }
    }

    /// <summary>
    /// Prueft ob ein Touch den Amboss-Bereich getroffen hat.
    /// </summary>
    public bool HitTest(SKRect bounds, float touchX, float touchY)
    {
        float padding = 14;
        float areaTop = bounds.Top + padding;
        float areaHeight = (bounds.Bottom - padding) - areaTop;
        float anvilTop = areaTop + areaHeight * 0.15f;
        float anvilHeight = areaHeight * 0.32f;
        float centerX = bounds.Left + bounds.Width * 0.5f;

        // Grosszuegiger Trefferbereich (ganzer Amboss + Hammer + etwas drumherum)
        float hitLeft = centerX - 90;
        float hitRight = centerX + 90;
        float hitTop = anvilTop - 60;
        float hitBottom = anvilTop + anvilHeight + 30;

        return touchX >= hitLeft && touchX <= hitRight && touchY >= hitTop && touchY <= hitBottom;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Amboss (metallisch, 3D mit Gradienten)
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawAnvil(SKCanvas canvas, float cx, float top, float height)
    {
        float baseW = 110;
        float topW = 75;
        float hornLen = 35;

        // Amboss-Koerper (Trapez) mit vertikalem Metall-Gradient
        using var bodyPath = new SKPath();
        bodyPath.MoveTo(cx - topW / 2, top);
        bodyPath.LineTo(cx + topW / 2, top);
        bodyPath.LineTo(cx + baseW / 2, top + height);
        bodyPath.LineTo(cx - baseW / 2, top + height);
        bodyPath.Close();

        AnvilBodyPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(cx, top),
            new SKPoint(cx, top + height),
            new[] { new SKColor(0x78, 0x78, 0x78), new SKColor(0x3A, 0x3A, 0x3A) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawPath(bodyPath, AnvilBodyPaint);
        AnvilBodyPaint.Shader = null;

        // Metallglanz-Highlight oben
        _fillPaint.Color = new SKColor(0xA0, 0xA0, 0xA0, 180);
        canvas.DrawRect(cx - topW / 2 + 6, top + 1, topW - 12, 3, _fillPaint);

        // Seiten-Reflexion (links heller)
        _fillPaint.Color = new SKColor(0x90, 0x90, 0x90, 50);
        using var leftReflect = new SKPath();
        leftReflect.MoveTo(cx - topW / 2, top);
        leftReflect.LineTo(cx - topW / 2 + 12, top);
        leftReflect.LineTo(cx - baseW / 2 + 12, top + height);
        leftReflect.LineTo(cx - baseW / 2, top + height);
        leftReflect.Close();
        canvas.DrawPath(leftReflect, _fillPaint);

        // Kanten
        canvas.DrawPath(bodyPath, AnvilEdgePaint);

        // Horn (links, konisch mit Gradient)
        float hornY = top + height * 0.28f;
        using var hornPath = new SKPath();
        hornPath.MoveTo(cx - topW / 2 - 2, hornY);
        hornPath.LineTo(cx - topW / 2 - hornLen, hornY + 6);
        hornPath.LineTo(cx - topW / 2 - 2, hornY + 14);
        hornPath.Close();
        _fillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(cx - topW / 2, hornY),
            new SKPoint(cx - topW / 2 - hornLen, hornY + 7),
            new[] { new SKColor(0x70, 0x70, 0x70), new SKColor(0x55, 0x55, 0x55) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawPath(hornPath, _fillPaint);
        _fillPaint.Shader = null;

        // Sockel (2-stufig, dunkel)
        _fillPaint.Color = new SKColor(0x30, 0x30, 0x30);
        canvas.DrawRect(cx - baseW / 2 - 5, top + height, baseW + 10, 10, _fillPaint);
        _fillPaint.Color = new SKColor(0x28, 0x28, 0x28);
        canvas.DrawRect(cx - baseW / 2 - 10, top + height + 10, baseW + 20, 8, _fillPaint);

        // Sockel-Highlight-Linie
        _fillPaint.Color = new SKColor(0x50, 0x50, 0x50);
        canvas.DrawRect(cx - baseW / 2 - 4, top + height, baseW + 8, 2, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Werkstueck (temperaturabhaengig mit Glow + Shimmer)
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawWorkpiece(SKCanvas canvas, float cx, float anvilTop, float anvilH, double temp)
    {
        float pw = 44;
        float ph = 10;
        float py = anvilTop - ph + 2;

        SKColor metalColor = InterpolateMetalColor(temp);

        // Werkstueck-Koerper mit horizontalem Gradient
        _fillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(cx - pw / 2, py),
            new SKPoint(cx + pw / 2, py),
            new[] { Darken(metalColor, 0.85f), metalColor, Brighten(metalColor, 1.15f) },
            new[] { 0f, 0.5f, 1f }, SKShaderTileMode.Clamp);
        canvas.DrawRect(cx - pw / 2, py, pw, ph, _fillPaint);
        _fillPaint.Shader = null;

        // Oberkanten-Glanz
        _fillPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(temp > 0.5 ? 80 : 30));
        canvas.DrawRect(cx - pw / 2 + 2, py, pw - 4, 2, _fillPaint);

        // Glueh-Effekt (ab 30% Temperatur)
        if (temp > 0.3)
        {
            byte glowAlpha = (byte)(Math.Min(1.0, (temp - 0.3) / 0.7) * 140);
            _glowPaint.Color = new SKColor(0xFF, 0xC4, 0x00, glowAlpha);
            _glowPaint.MaskFilter = Blur6;
            canvas.DrawRect(cx - pw / 2 - 4, py - 4, pw + 8, ph + 8, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Weissglut-Aura (ab 70%)
        if (temp > 0.7)
        {
            byte whiteAlpha = (byte)(Math.Min(1.0, (temp - 0.7) / 0.3) * 100);
            _glowPaint.Color = new SKColor(0xFF, 0xFF, 0xE0, whiteAlpha);
            _glowPaint.MaskFilter = Blur10;
            canvas.DrawRect(cx - pw / 2 - 6, py - 6, pw + 12, ph + 12, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Heat-Shimmer (subtile Verzerrung simuliert durch pulsierende Linie)
        if (temp > 0.5)
        {
            float shimmerAlpha = (float)((temp - 0.5) / 0.5) * 0.4f;
            float shimmerY = py - 8 - (float)Math.Sin(_animTime * 8) * 3;
            _fillPaint.Color = new SKColor(0xFF, 0xD0, 0x60, (byte)(shimmerAlpha * 80));
            canvas.DrawRect(cx - pw / 3, shimmerY, pw * 2 / 3, 1.5f, _fillPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Hammer (metallisch mit Schlag-Animation + Bounce)
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawHammer(SKCanvas canvas, float cx, float baseY, bool isPlaying, bool isHammering)
    {
        float offsetY = 0;
        float rotation = 0;

        if (isHammering)
        {
            float t = Math.Min(_hammerAnimTime / 0.12f, 1.0f);
            if (t < 0.4f)
            {
                // Schneller Schlag nach unten
                float ease = t / 0.4f;
                offsetY = ease * ease * 30;
                rotation = ease * -5; // Leichte Neigung beim Schlag
            }
            else
            {
                // Bounce zurueck (EaseOutBack)
                float bounce = (t - 0.4f) / 0.6f;
                float easeBack = 1.0f - (1.0f - bounce) * (1.0f - bounce);
                offsetY = (1 - easeBack) * 30;
                rotation = (1 - easeBack) * -5;
            }
        }
        else if (isPlaying)
        {
            // Leichtes Schweben waehrend des Spiels
            offsetY = (float)Math.Sin(_animTime * 5) * 3;
        }

        float hammerY = baseY - 55 + offsetY;
        float hammerX = cx + 16;

        canvas.Save();
        canvas.RotateDegrees(rotation, hammerX, hammerY + 20);

        // Stiel mit Holz-Gradient
        HammerHandlePaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(hammerX - 4, hammerY),
            new SKPoint(hammerX + 4, hammerY),
            new[] { new SKColor(0xA6, 0x7C, 0x52), new SKColor(0x8B, 0x5A, 0x2B), new SKColor(0x6D, 0x44, 0x1E) },
            new[] { 0f, 0.5f, 1f }, SKShaderTileMode.Clamp);
        canvas.DrawRoundRect(hammerX - 3.5f, hammerY + 2, 7, 42, 2, 2, HammerHandlePaint);
        HammerHandlePaint.Shader = null;

        // Stiel-Glanz (duenne helle Linie links)
        _fillPaint.Color = new SKColor(0xC8, 0xA0, 0x70, 120);
        canvas.DrawRect(hammerX - 2, hammerY + 4, 1.5f, 38, _fillPaint);

        // Hammerkopf mit Metall-Gradient
        _fillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(hammerX - 18, hammerY - 10),
            new SKPoint(hammerX - 18, hammerY + 4),
            new[] { new SKColor(0x80, 0x80, 0x80), new SKColor(0x50, 0x50, 0x50), new SKColor(0x3A, 0x3A, 0x3A) },
            new[] { 0f, 0.5f, 1f }, SKShaderTileMode.Clamp);
        canvas.DrawRoundRect(hammerX - 18, hammerY - 10, 36, 14, 2, 2, _fillPaint);
        _fillPaint.Shader = null;

        // Kopf-Highlight (Metallglanz oben)
        _fillPaint.Color = new SKColor(0xB0, 0xB0, 0xB0, 160);
        canvas.DrawRect(hammerX - 16, hammerY - 9, 32, 3, _fillPaint);

        // Kopf-Schatten (unten)
        _fillPaint.Color = new SKColor(0x25, 0x25, 0x25);
        canvas.DrawRect(hammerX - 16, hammerY + 2, 32, 2, _fillPaint);

        canvas.Restore();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Esse / Schmiede-Feuer (mehrstufige Flammen + Glut)
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawForge(SKCanvas canvas, SKRect bounds, float padding, float areaTop, float areaH,
        double temperature, bool isPlaying)
    {
        float fLeft = bounds.Left + padding;
        float fTop = areaTop + 8;
        float fW = 54;
        float fH = areaH * 0.42f;

        // Steinrahmen mit Gradient (warme Toene)
        _fillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(fLeft, fTop),
            new SKPoint(fLeft, fTop + fH),
            new[] { new SKColor(0x6D, 0x56, 0x44), new SKColor(0x4A, 0x38, 0x2A) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawRoundRect(fLeft, fTop, fW, fH, 4, 4, _fillPaint);
        _fillPaint.Shader = null;

        // Steinfugen (horizontal)
        _fillPaint.Color = new SKColor(0x3A, 0x2E, 0x22, 100);
        for (float fy = fTop + 12; fy < fTop + fH - 5; fy += 14)
        {
            canvas.DrawRect(fLeft + 3, fy, fW - 6, 1, _fillPaint);
        }

        // Innerer dunkler Bereich (Feuerstelle)
        _fillPaint.Color = new SKColor(0x1A, 0x0E, 0x06);
        canvas.DrawRoundRect(fLeft + 5, fTop + 5, fW - 10, fH - 10, 2, 2, _fillPaint);

        // Glut-Bett am Boden (rote Kohlen)
        float coalY = fTop + fH - 14;
        _fillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(fLeft + 8, coalY),
            new SKPoint(fLeft + 8, coalY + 8),
            new[] { new SKColor(0xFF, 0x30, 0x00, 200), new SKColor(0x80, 0x10, 0x00, 150) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawRect(fLeft + 8, coalY, fW - 16, 8, _fillPaint);
        _fillPaint.Shader = null;

        // Feuer (animiert, pulsierend, mehrere Schichten)
        if (isPlaying || temperature > 0.05)
        {
            float tempFactor = (float)Math.Max(0.3, temperature);
            float firePulse = 0.7f + 0.3f * (float)Math.Sin(_animTime * 5);
            float fireH = (fH - 24) * firePulse * tempFactor;
            float fireX = fLeft + 10;
            float fireBase = fTop + fH - 14;

            // Aeussere Flamme (dunkelrot→orange)
            _fillPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(fireX, fireBase - fireH),
                new SKPoint(fireX, fireBase),
                new[] { FireDark.WithAlpha(0), FireDark.WithAlpha(200) },
                null, SKShaderTileMode.Clamp);
            canvas.DrawRect(fireX, fireBase - fireH, fW - 20, fireH, _fillPaint);

            // Mittlere Flamme (orange→gelb)
            float midW = (fW - 20) * 0.7f;
            float midH = fireH * 0.75f;
            float midX = fireX + ((fW - 20) - midW) / 2;
            _fillPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(midX, fireBase - midH),
                new SKPoint(midX, fireBase),
                new[] { FireMedium.WithAlpha(0), FireMedium.WithAlpha(220) },
                null, SKShaderTileMode.Clamp);
            canvas.DrawRect(midX, fireBase - midH, midW, midH, _fillPaint);

            // Innere Flamme (gelb→weiss, bei hoher Temp)
            float innerW = (fW - 20) * 0.35f;
            float innerH = fireH * 0.45f;
            float innerX = fireX + ((fW - 20) - innerW) / 2;
            byte innerAlpha = (byte)(tempFactor * 200);
            _fillPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(innerX, fireBase - innerH),
                new SKPoint(innerX, fireBase),
                new[] { FireLight.WithAlpha(0), FireLight.WithAlpha(innerAlpha) },
                null, SKShaderTileMode.Clamp);
            canvas.DrawRect(innerX, fireBase - innerH, innerW, innerH, _fillPaint);
            _fillPaint.Shader = null;

            // Weiss-heisser Kern bei sehr hoher Temperatur
            if (temperature > 0.7)
            {
                float coreW = (fW - 20) * 0.2f;
                float coreH = fireH * 0.2f;
                float coreX = fireX + ((fW - 20) - coreW) / 2;
                byte coreAlpha = (byte)((temperature - 0.7) / 0.3 * 150);
                _fillPaint.Color = FireWhite.WithAlpha(coreAlpha);
                canvas.DrawRect(coreX, fireBase - coreH, coreW, coreH, _fillPaint);
            }

            // Glueh-Schein auf Umgebung (weicher, temperaturabhaengig)
            byte glowAlpha = (byte)(firePulse * 50 * tempFactor);
            _glowPaint.Color = new SKColor(0xFF, 0x70, 0x00, glowAlpha);
            _glowPaint.MaskFilter = Blur16;
            canvas.DrawRect(fLeft - 15, fTop - 8, fW + 30, fH + 16, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Rauch ueber der Esse (4 Rauchwoelkchen)
        if (isPlaying)
        {
            for (int i = 0; i < 4; i++)
            {
                float sTime = _animTime + i * 1.1f;
                float sProg = (sTime % 2.5f) / 2.5f;
                float sX = fLeft + fW / 2 + (float)Math.Sin(sTime * 1.8f) * 8;
                float sY = fTop - 4 - sProg * 35;
                float sSize = 3 + sProg * 3;
                byte sAlpha = (byte)((1 - sProg) * 60);
                _smokePaint.Color = new SKColor(0x80, 0x80, 0x80, sAlpha);
                canvas.DrawCircle(sX, sY, sSize, _smokePaint);
            }
        }

        // Blasebalg rechts (dekorativ)
        float bX = bounds.Right - padding - 42;
        float bY = areaTop + areaH * 0.28f;

        // Blasebalg-Koerper
        _fillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(bX, bY),
            new SKPoint(bX, bY + 22),
            new[] { new SKColor(0x7D, 0x5C, 0x4B), new SKColor(0x5D, 0x3C, 0x2B) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawRoundRect(bX, bY, 32, 22, 3, 3, _fillPaint);
        _fillPaint.Shader = null;

        // Blasebalg-Griff
        _fillPaint.Color = new SKColor(0x8B, 0x5A, 0x2B);
        canvas.DrawRoundRect(bX + 10, bY - 10, 12, 12, 2, 2, _fillPaint);

        // Blasebalg-Duese (Metall)
        _fillPaint.Color = new SKColor(0x80, 0x80, 0x80);
        canvas.DrawRect(bX - 6, bY + 8, 9, 6, _fillPaint);
        // Highlight
        _fillPaint.Color = new SKColor(0xA0, 0xA0, 0xA0, 100);
        canvas.DrawRect(bX - 5, bY + 8, 7, 2, _fillPaint);

        // Faltenlinien am Blasebalg
        _fillPaint.Color = new SKColor(0x4A, 0x30, 0x20, 100);
        canvas.DrawRect(bX + 4, bY + 5, 24, 1, _fillPaint);
        canvas.DrawRect(bX + 4, bY + 12, 24, 1, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Temperatur-Bar (Zonen + Marker + Labels)
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawTemperatureBar(SKCanvas canvas, float x, float y, float w, float h,
        double temp, double pStart, double pWidth, double gStart, double gWidth,
        double oStart, double oWidth, bool isPlaying)
    {
        // Bar-Hintergrund (dunkel mit Gradient)
        _fillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(x, y), new SKPoint(x, y + h),
            new[] { new SKColor(0x20, 0x14, 0x08), new SKColor(0x30, 0x1E, 0x10) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawRoundRect(x, y, w, h, 4, 4, _fillPaint);
        _fillPaint.Shader = null;

        // Rahmen
        canvas.DrawRoundRect(x, y, w, h, 4, 4, FramePaint);

        float innerX = x + 2;
        float innerW = w - 4;
        float innerY = y + 2;
        float innerH = h - 4;

        // Kalt-Zone (links, blau)
        float coldEnd = innerX + (float)(oStart * innerW);
        _fillPaint.Color = ColdColor.WithAlpha(70);
        canvas.DrawRect(innerX, innerY, coldEnd - innerX, innerH, _fillPaint);

        // Heiss-Zone (rechts, rot)
        float hotStart = innerX + (float)((oStart + oWidth) * innerW);
        _fillPaint.Color = TooHotColor.WithAlpha(70);
        canvas.DrawRect(hotStart, innerY, innerX + innerW - hotStart, innerH, _fillPaint);

        // TooHot Warning-Puls (wenn Temperatur in der heissen Zone)
        if (isPlaying && temp > oStart + oWidth)
        {
            float warnPulse = (float)(0.5 + 0.5 * Math.Sin(_animTime * 8));
            _fillPaint.Color = TooHotColor.WithAlpha((byte)(40 + warnPulse * 60));
            canvas.DrawRect(hotStart, innerY, innerX + innerW - hotStart, innerH, _fillPaint);
        }

        // Ok-Zone (gelb)
        float okLeft = innerX + (float)(oStart * innerW);
        float okW = (float)(oWidth * innerW);
        _fillPaint.Color = OkColor.WithAlpha(90);
        canvas.DrawRect(okLeft, innerY, okW, innerH, _fillPaint);

        // Good-Zone (orange) mit leichtem Gradient
        float goodLeft = innerX + (float)(gStart * innerW);
        float goodW = (float)(gWidth * innerW);
        _fillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(goodLeft, innerY), new SKPoint(goodLeft, innerY + innerH),
            new[] { GoodColor.WithAlpha(160), GoodColor.WithAlpha(120) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawRect(goodLeft, innerY, goodW, innerH, _fillPaint);
        _fillPaint.Shader = null;

        // Perfect-Zone (glueh-orange, hell) mit Gradient
        float perfLeft = innerX + (float)(pStart * innerW);
        float perfW = (float)(pWidth * innerW);
        _fillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(perfLeft, innerY), new SKPoint(perfLeft, innerY + innerH),
            new[] { PerfectColor.WithAlpha(220), PerfectColor.WithAlpha(180) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawRect(perfLeft, innerY, perfW, innerH, _fillPaint);
        _fillPaint.Shader = null;

        // Perfect-Zone Glow-Puls
        if (isPlaying)
        {
            float pulse = (float)(0.5 + 0.5 * Math.Sin(_animTime * 4));
            _glowPaint.Color = PerfectColor.WithAlpha((byte)(40 * pulse));
            _glowPaint.MaskFilter = Blur4;
            canvas.DrawRect(perfLeft - 3, y - 3, perfW + 6, h + 6, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Tick-Markierungen
        for (float t = 0.1f; t < 1.0f; t += 0.1f)
        {
            float tickX = innerX + t * innerW;
            canvas.DrawLine(tickX, y, tickX, y + 4, TickPaint);
            canvas.DrawLine(tickX, y + h - 4, tickX, y + h, TickPaint);
        }

        // Temperatur-Labels
        _textPaint.Color = new SKColor(255, 255, 255, 90);
        _textPaint.TextSize = 9;
        canvas.DrawText("KALT", x + 4, y + h + 13, _textPaint);

        float heissWidth = _textPaint.MeasureText("HEISS");
        canvas.DrawText("HEISS", x + w - heissWidth - 4, y + h + 13, _textPaint);

        // Temperatur-Marker (dreieckiger Zeiger mit Glow)
        float markerX = innerX + (float)(temp * innerW);
        SKColor tempColor = InterpolateMetalColor(temp);

        // Marker-Glow (temperaturfarben)
        _glowPaint.Color = tempColor.WithAlpha(80);
        _glowPaint.MaskFilter = Blur4;
        canvas.DrawRect(markerX - 4, y - 5, 8, h + 10, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Marker-Schatten
        _fillPaint.Color = new SKColor(0, 0, 0, 60);
        canvas.DrawRect(markerX - 2, y - 3, 6, h + 6, _fillPaint);

        // Marker-Koerper (weiss mit Temperatur-Overlay)
        _fillPaint.Color = SKColors.White;
        canvas.DrawRect(markerX - 3, y - 4, 6, h + 8, _fillPaint);

        // Temperatur-Farbe im Marker
        _fillPaint.Color = tempColor.WithAlpha(180);
        canvas.DrawRect(markerX - 2, y, 4, h, _fillPaint);

        // Pfeil-Spitze oben
        using var arrowPath = new SKPath();
        arrowPath.MoveTo(markerX - 5, y - 5);
        arrowPath.LineTo(markerX, y - 10);
        arrowPath.LineTo(markerX + 5, y - 5);
        arrowPath.Close();
        _fillPaint.Color = SKColors.White;
        canvas.DrawPath(arrowPath, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Schlag-Fortschritt (farbige Punkte je nach Zone)
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawHitProgress(SKCanvas canvas, SKRect bounds, float top, int completed, int required)
    {
        if (required <= 0) return;

        float dotR = 4;
        float spacing = 12;
        float totalW = required * dotR * 2 + (required - 1) * (spacing - dotR * 2);
        float startX = bounds.Left + (bounds.Width - totalW) / 2;

        for (int i = 0; i < required; i++)
        {
            float cx = startX + i * spacing + dotR;
            float cy = top + dotR;

            if (i < completed)
            {
                // Ausgefuellter Punkt (Farbe der letzten Hit-Zone oder Orange)
                SKColor dotColor = (i == completed - 1) ? GetZoneColor(_lastHitZone) : PerfectColor;
                _fillPaint.Color = dotColor;
                canvas.DrawCircle(cx, cy, dotR, _fillPaint);

                // Kleiner Glanz
                _fillPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 80);
                canvas.DrawCircle(cx - 1, cy - 1, 1.5f, _fillPaint);
            }
            else
            {
                // Leerer Punkt (dunkler Ring)
                _fillPaint.Color = new SKColor(0x60, 0x60, 0x60, 80);
                _fillPaint.Style = SKPaintStyle.Stroke;
                _fillPaint.StrokeWidth = 1.5f;
                canvas.DrawCircle(cx, cy, dotR, _fillPaint);
                _fillPaint.Style = SKPaintStyle.Fill;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Effekte: Perfect-Flash, Miss-Flash, Schockwelle, Completion-Glow
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawPerfectFlash(SKCanvas canvas, float cx, float anvilTop, float anvilH)
    {
        float t = _perfectFlashTime / 0.3f;
        byte alpha = (byte)((1 - t) * 180);
        _glowPaint.Color = new SKColor(0xCC, 0xDD, 0xFF, alpha);
        _glowPaint.MaskFilter = Blur10;
        canvas.DrawRect(cx - 70, anvilTop - 20, 140, anvilH + 40, _glowPaint);
        _glowPaint.MaskFilter = null;
    }

    private void DrawMissFlash(SKCanvas canvas, SKRect bounds)
    {
        float t = _missFlashTime / 0.25f;
        float pulse = (float)(Math.Sin(t * Math.PI * 3) * 0.5 + 0.5); // Schnelles Blinken
        byte alpha = (byte)(pulse * (1 - t) * 60);
        _fillPaint.Color = new SKColor(0xFF, 0x20, 0x20, alpha);
        canvas.DrawRect(bounds, _fillPaint);
    }

    private void DrawShockwave(SKCanvas canvas)
    {
        float t = _shockwaveTime / 0.5f;
        float radius = t * 80;
        byte alpha = (byte)((1 - t) * 150);
        _fillPaint.Color = new SKColor(0xFF, 0xD0, 0x60, alpha);
        _fillPaint.Style = SKPaintStyle.Stroke;
        _fillPaint.StrokeWidth = 3 * (1 - t);
        canvas.DrawCircle(_shockwaveCenterX, _shockwaveCenterY, radius, _fillPaint);
        _fillPaint.Style = SKPaintStyle.Fill;
    }

    private void DrawCompletionGlow(SKCanvas canvas, float cx, float cy, float time)
    {
        float pulse = (float)(0.6 + 0.4 * Math.Sin(time * 3));
        float fadeOut = Math.Max(0, 1 - time / 3.0f);
        byte alpha = (byte)(pulse * fadeOut * 100);

        // Goldener Glow um den Amboss
        _glowPaint.Color = new SKColor(0xFF, 0xD7, 0x00, alpha);
        _glowPaint.MaskFilter = Blur16;
        canvas.DrawCircle(cx, cy, 80 + pulse * 20, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Innerer weisser Kern
        byte innerAlpha = (byte)(pulse * fadeOut * 50);
        _glowPaint.Color = new SKColor(0xFF, 0xFF, 0xE0, innerAlpha);
        _glowPaint.MaskFilter = Blur10;
        canvas.DrawCircle(cx, cy, 40, _glowPaint);
        _glowPaint.MaskFilter = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Partikel: Funken
    // ═══════════════════════════════════════════════════════════════════════

    private void SpawnHitSparks(SKRect bounds, double temperature, int zone)
    {
        float cx = bounds.Left + bounds.Width * 0.5f;
        float cy = bounds.Top + (bounds.Height - 28) * 0.3f;

        // Mehr Funken bei besserer Zone + hoeherer Temperatur
        int count = zone switch
        {
            3 => 35 + (int)(temperature * 15), // Perfect: viele goldene
            2 => 20 + (int)(temperature * 8),  // Good: mittelviele orange
            1 => 12,                             // Ok: wenige gelbe
            _ => 8                               // Miss: wenige dunkle
        };

        var random = Random.Shared;
        for (int i = 0; i < count && _sparkCount < MAX_SPARKS; i++)
        {
            float angle = (float)(random.NextDouble() * Math.PI * 2);
            float speed = 50 + random.Next(0, 100);

            // Farbe zonenabhaengig
            SKColor sparkColor = zone switch
            {
                3 => random.NextDouble() < 0.4
                    ? new SKColor(0xFF, 0xFF, 0xE0) // Weiss-gold
                    : new SKColor(0xFF, 0xD7, 0x00), // Gold
                2 => random.NextDouble() < 0.3
                    ? new SKColor(0xFF, 0xD7, 0x00) // Gold
                    : new SKColor(0xFF, 0x8C, 0x00), // Orange
                1 => new SKColor(0xFF, 0xC1, 0x07),  // Gelb
                _ => random.NextDouble() < 0.5
                    ? new SKColor(0xCC, 0x44, 0x00)  // Dunkelrot
                    : new SKColor(0xFF, 0x66, 0x00)   // Dunkles Orange
            };

            _sparks[_sparkCount++] = new SparkParticle
            {
                X = cx + random.Next(-12, 13),
                Y = cy + random.Next(-6, 7),
                VelocityX = (float)Math.Cos(angle) * speed,
                VelocityY = (float)Math.Sin(angle) * speed - 40,
                Life = 0,
                MaxLife = 0.3f + (float)random.NextDouble() * 0.6f,
                Size = zone >= 2 ? 2 + random.Next(0, 3) : 1 + random.Next(0, 2),
                R = sparkColor.Red,
                G = sparkColor.Green,
                B = sparkColor.Blue,
                IsGolden = zone == 3
            };
        }
    }

    private void SpawnCompletionSparks(SKRect bounds)
    {
        float cx = bounds.Left + bounds.Width * 0.5f;
        float cy = bounds.Top + bounds.Height * 0.35f;

        var random = Random.Shared;
        for (int i = 0; i < 50 && _sparkCount < MAX_SPARKS; i++)
        {
            float angle = (float)(random.NextDouble() * Math.PI * 2);
            float speed = 60 + random.Next(0, 120);

            _sparks[_sparkCount++] = new SparkParticle
            {
                X = cx + random.Next(-20, 21),
                Y = cy + random.Next(-10, 11),
                VelocityX = (float)Math.Cos(angle) * speed,
                VelocityY = (float)Math.Sin(angle) * speed - 50,
                Life = 0,
                MaxLife = 0.6f + (float)random.NextDouble() * 1.0f,
                Size = 2 + random.Next(0, 4),
                R = 0xFF, G = 0xD7, B = 0x00,
                IsGolden = true
            };
        }
    }

    private void UpdateAndDrawSparks(SKCanvas canvas, float deltaTime)
    {
        for (int i = 0; i < _sparkCount; i++)
        {
            ref var p = ref _sparks[i];
            p.Life += deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            p.VelocityY += 90 * deltaTime; // Schwerkraft
            p.VelocityX *= 0.97f; // Luftwiderstand

            if (p.Life >= p.MaxLife)
            {
                // Entfernen durch Tauschen mit letztem
                _sparks[i] = _sparks[--_sparkCount];
                i--;
                continue;
            }

            float alpha = 1 - (p.Life / p.MaxLife);
            _sparkPaint.Color = new SKColor(p.R, p.G, p.B, (byte)(alpha * 255));

            if (p.IsGolden && p.Size >= 2)
            {
                // Goldene Funken mit Glow
                _sparkPaint.MaskFilter = Blur4;
                canvas.DrawCircle(p.X, p.Y, p.Size + 1, _sparkPaint);
                _sparkPaint.MaskFilter = null;
            }

            canvas.DrawCircle(p.X, p.Y, p.Size * 0.5f, _sparkPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Partikel: Dampf/Steam vom Werkstueck
    // ═══════════════════════════════════════════════════════════════════════

    private void SpawnHitSteam(SKRect bounds)
    {
        float cx = bounds.Left + bounds.Width * 0.5f;
        float cy = bounds.Top + (bounds.Height - 28) * 0.28f;

        var random = Random.Shared;
        for (int i = 0; i < 6 && _smokeCount < MAX_SMOKE; i++)
        {
            _smoke[_smokeCount++] = new SmokeParticle
            {
                X = cx + random.Next(-15, 16),
                Y = cy + random.Next(-4, 5),
                VelocityX = (float)(random.NextDouble() - 0.5) * 20,
                VelocityY = -25 - random.Next(0, 20),
                Life = 0,
                MaxLife = 0.5f + (float)random.NextDouble() * 0.6f,
                Size = 3 + random.Next(0, 4),
                Brightness = (byte)(160 + random.Next(0, 60))
            };
        }
    }

    private void UpdateAndDrawSmoke(SKCanvas canvas, float deltaTime)
    {
        for (int i = 0; i < _smokeCount; i++)
        {
            ref var p = ref _smoke[i];
            p.Life += deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            p.VelocityY -= 5 * deltaTime; // Steigt auf
            p.Size += deltaTime * 4; // Wird groesser

            if (p.Life >= p.MaxLife)
            {
                _smoke[i] = _smoke[--_smokeCount];
                i--;
                continue;
            }

            float alpha = (1 - (p.Life / p.MaxLife));
            // Einfaden in der ersten 20% der Lebenszeit
            if (p.Life < p.MaxLife * 0.2f)
                alpha *= p.Life / (p.MaxLife * 0.2f);

            _smokePaint.Color = new SKColor(p.Brightness, p.Brightness, p.Brightness, (byte)(alpha * 80));
            canvas.DrawCircle(p.X, p.Y, p.Size, _smokePaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Partikel: Glut/Embers in der Esse
    // ═══════════════════════════════════════════════════════════════════════

    private void SpawnEmber(SKRect bounds, float padding, float areaTop, float areaH)
    {
        if (_emberCount >= MAX_EMBERS) return;

        float fLeft = bounds.Left + padding;
        float fTop = areaTop + 8;
        float fW = 54;
        float fH = areaH * 0.42f;

        var random = Random.Shared;
        _embers[_emberCount++] = new EmberParticle
        {
            X = fLeft + 10 + random.Next(0, (int)(fW - 20)),
            Y = fTop + fH - 16 + random.Next(0, 8),
            Life = 0,
            MaxLife = 0.8f + (float)random.NextDouble() * 1.5f,
            Size = 1.5f + (float)random.NextDouble() * 2,
            PulseOffset = (float)(random.NextDouble() * Math.PI * 2)
        };
    }

    private void UpdateAndDrawEmbers(SKCanvas canvas, float deltaTime)
    {
        for (int i = 0; i < _emberCount; i++)
        {
            ref var e = ref _embers[i];
            e.Life += deltaTime;

            if (e.Life >= e.MaxLife)
            {
                _embers[i] = _embers[--_emberCount];
                i--;
                continue;
            }

            float pulse = (float)(0.5 + 0.5 * Math.Sin(_animTime * 6 + e.PulseOffset));
            float lifeRatio = e.Life / e.MaxLife;
            float alpha = lifeRatio < 0.2f ? lifeRatio / 0.2f : 1 - ((lifeRatio - 0.2f) / 0.8f);

            // Glut-Farbe: rot→orange (pulsierend)
            byte r = (byte)(0xFF * (0.8f + 0.2f * pulse));
            byte g = (byte)(0x40 + 0x40 * pulse);
            _fillPaint.Color = new SKColor(r, g, 0x00, (byte)(alpha * 200));
            canvas.DrawCircle(e.X, e.Y - lifeRatio * 8, e.Size * (0.8f + 0.2f * pulse), _fillPaint);

            // Glut-Glow
            _glowPaint.Color = new SKColor(0xFF, 0x60, 0x00, (byte)(alpha * pulse * 80));
            _glowPaint.MaskFilter = Blur4;
            canvas.DrawCircle(e.X, e.Y - lifeRatio * 8, e.Size * 2, _glowPaint);
            _glowPaint.MaskFilter = null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Hilfsfunktionen
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bestimmt die Treffer-Zone (0=Miss, 1=Ok, 2=Good, 3=Perfect) aus der Temperatur.
    /// </summary>
    private static int DetermineZone(double temp,
        double pStart, double pWidth, double gStart, double gWidth,
        double oStart, double oWidth)
    {
        if (temp >= pStart && temp <= pStart + pWidth) return 3;
        if (temp >= gStart && temp <= gStart + gWidth) return 2;
        if (temp >= oStart && temp <= oStart + oWidth) return 1;
        return 0;
    }

    private static SKColor GetZoneColor(int zone) => zone switch
    {
        3 => PerfectColor,
        2 => GoodColor,
        1 => OkColor,
        _ => new SKColor(0x80, 0x40, 0x20)
    };

    /// <summary>
    /// Interpoliert die Metall-Farbe basierend auf der Temperatur (0.0-1.0).
    /// </summary>
    private static SKColor InterpolateMetalColor(double temp)
    {
        if (temp <= 0.25)
        {
            float t = (float)(temp / 0.25);
            return LerpColor(MetalCold, MetalWarm, t);
        }
        if (temp <= 0.55)
        {
            float t = (float)((temp - 0.25) / 0.3);
            return LerpColor(MetalWarm, MetalHot, t);
        }
        float t2 = Math.Clamp((float)((temp - 0.55) / 0.45), 0, 1);
        return LerpColor(MetalHot, MetalWhiteHot, t2);
    }

    private static SKColor LerpColor(SKColor a, SKColor b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue + (b.Blue - a.Blue) * t));
    }

    private static SKColor Darken(SKColor c, float factor) => new(
        (byte)(c.Red * factor), (byte)(c.Green * factor), (byte)(c.Blue * factor), c.Alpha);

    private static SKColor Brighten(SKColor c, float factor) => new(
        (byte)Math.Min(255, c.Red * factor),
        (byte)Math.Min(255, c.Green * factor),
        (byte)Math.Min(255, c.Blue * factor), c.Alpha);
}
