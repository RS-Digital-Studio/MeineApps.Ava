using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// SkiaSharp-Renderer fuer das Schmiede-Minigame.
/// Zeichnet Amboss, gluehnendes Werkstueck, Schmiedehammer, Esse und Temperatur-Bar.
/// Pixel-Art Stil: Flache Fuellungen, kein Anti-Aliasing, passend zu CityRenderer/WorkshopInteriorRenderer.
/// </summary>
public class ForgeGameRenderer
{
    // Amboss-Farben
    private static readonly SKColor AnvilDark = new(0x3A, 0x3A, 0x3A);
    private static readonly SKColor AnvilMedium = new(0x4A, 0x4A, 0x4A);
    private static readonly SKColor AnvilLight = new(0x6A, 0x6A, 0x6A);
    private static readonly SKColor AnvilHighlight = new(0x80, 0x80, 0x80);

    // Hammer-Farben
    private static readonly SKColor HammerHead = new(0x60, 0x60, 0x60);
    private static readonly SKColor HammerHandle = new(0x8B, 0x5A, 0x2B);
    private static readonly SKColor HammerHandleLight = new(0xA6, 0x7C, 0x52);

    // Feuer-/Glut-Farben
    private static readonly SKColor FireDark = new(0xFF, 0x6B, 0x00);
    private static readonly SKColor FireMedium = new(0xFF, 0xB3, 0x00);
    private static readonly SKColor FireLight = new(0xFF, 0xF1, 0x76);

    // Werkstueck-Farben (temperaturabhaengig)
    private static readonly SKColor MetalCold = new(0x8B, 0x45, 0x13);
    private static readonly SKColor MetalWarm = new(0xFF, 0x6B, 0x00);
    private static readonly SKColor MetalHot = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor MetalWhiteHot = new(0xFF, 0xFF, 0xFF);

    // Zonen-Farben (Temperatur-Bar)
    private static readonly SKColor PerfectZone = new(0xFF, 0x6B, 0x00);   // Glueh-Orange
    private static readonly SKColor GoodZone = new(0xE8, 0xA0, 0x0E);      // CraftOrange
    private static readonly SKColor OkZone = new(0xFF, 0xC1, 0x07);        // Gelb
    private static readonly SKColor ColdZone = new(0x42, 0x72, 0xB0);      // Blau (zu kalt)
    private static readonly SKColor TooHotZone = new(0xEF, 0x44, 0x44);    // Rot (zu heiss)

    // Marker
    private static readonly SKColor MarkerColor = SKColors.White;

    // Funken-Partikel
    private readonly List<SparkParticle> _sparks = new();
    private float _animTime;
    private float _hammerAnimTime;
    private bool _wasHammering;

    private struct SparkParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Size;
        public SKColor Color;
    }

    /// <summary>
    /// Rendert das gesamte Schmiedespiel.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds,
        double temperature,
        double targetStart, double targetWidth,
        double goodStart, double goodWidth,
        double okStart, double okWidth,
        int hitsCompleted, int hitsRequired,
        bool isPlaying, bool isResultShown,
        bool isHammering,
        float deltaTime)
    {
        _animTime += deltaTime;

        // Hammer-Animation tracken
        if (isHammering && !_wasHammering)
        {
            _hammerAnimTime = 0;
            SpawnSparks(bounds, temperature);
        }
        _wasHammering = isHammering;
        if (isHammering)
        {
            _hammerAnimTime += deltaTime;
        }

        float padding = 16;
        float barAreaTop = bounds.Top + padding;
        float barAreaBottom = bounds.Bottom - padding;
        float barAreaHeight = barAreaBottom - barAreaTop;

        // Hintergrund: Esse/Schmiedefeuer links
        DrawForge(canvas, bounds, padding, barAreaTop, barAreaHeight, temperature, isPlaying);

        // Obere Haelfte: Amboss mit Werkstueck
        float anvilTop = barAreaTop + barAreaHeight * 0.15f;
        float anvilHeight = barAreaHeight * 0.35f;
        float anvilCenterX = bounds.Left + bounds.Width * 0.5f;
        DrawAnvil(canvas, anvilCenterX, anvilTop, anvilHeight);
        DrawWorkpiece(canvas, anvilCenterX, anvilTop, anvilHeight, temperature);

        // Hammer ueber dem Amboss
        float hammerBaseY = anvilTop - 10;
        DrawHammer(canvas, anvilCenterX, hammerBaseY, isPlaying, isHammering);

        // Schlag-Fortschritt (kleine Punkte)
        DrawHitProgress(canvas, bounds, anvilTop + anvilHeight + 8, hitsCompleted, hitsRequired);

        // Untere Haelfte: Temperatur-Bar
        float barTop = anvilTop + anvilHeight + 30;
        float barHeight = Math.Min(50, barAreaHeight * 0.18f);
        float barLeft = bounds.Left + padding + 8;
        float barWidth = bounds.Width - 2 * padding - 16;
        DrawTemperatureBar(canvas, barLeft, barTop, barWidth, barHeight,
            temperature, targetStart, targetWidth, goodStart, goodWidth, okStart, okWidth, isPlaying);

        // Funken-Partikel
        UpdateAndDrawSparks(canvas, deltaTime);
    }

    /// <summary>
    /// Prueft ob ein Touch den Amboss-Bereich getroffen hat.
    /// </summary>
    public bool HitTest(SKRect bounds, float touchX, float touchY)
    {
        float padding = 16;
        float barAreaTop = bounds.Top + padding;
        float barAreaHeight = (bounds.Bottom - padding) - barAreaTop;
        float anvilTop = barAreaTop + barAreaHeight * 0.15f;
        float anvilHeight = barAreaHeight * 0.35f;
        float anvilCenterX = bounds.Left + bounds.Width * 0.5f;

        // Grosszuegiger Trefferbereich um den Amboss
        float hitLeft = anvilCenterX - 80;
        float hitRight = anvilCenterX + 80;
        float hitTop = anvilTop - 40;
        float hitBottom = anvilTop + anvilHeight + 20;

        return touchX >= hitLeft && touchX <= hitRight && touchY >= hitTop && touchY <= hitBottom;
    }

    /// <summary>
    /// Zeichnet den Amboss (trapezfoermig, dunkelgrau mit Highlights).
    /// </summary>
    private void DrawAnvil(SKCanvas canvas, float centerX, float top, float height)
    {
        float baseWidth = 100;
        float topWidth = 70;
        float hornLength = 30;

        // Amboss-Koerper (Trapez)
        using var bodyPaint = new SKPaint { Color = AnvilMedium, IsAntialias = false };
        using var bodyPath = new SKPath();
        bodyPath.MoveTo(centerX - topWidth / 2, top);
        bodyPath.LineTo(centerX + topWidth / 2, top);
        bodyPath.LineTo(centerX + baseWidth / 2, top + height);
        bodyPath.LineTo(centerX - baseWidth / 2, top + height);
        bodyPath.Close();
        canvas.DrawPath(bodyPath, bodyPaint);

        // Highlight auf der Oberseite
        using var highlightPaint = new SKPaint { Color = AnvilHighlight, IsAntialias = false };
        canvas.DrawRect(centerX - topWidth / 2 + 4, top, topWidth - 8, 4, highlightPaint);

        // Dunkle Kanten
        using var edgePaint = new SKPaint { Color = AnvilDark, IsAntialias = false, StrokeWidth = 2, Style = SKPaintStyle.Stroke };
        canvas.DrawPath(bodyPath, edgePaint);

        // Horn (links, spitz zulaufend)
        using var hornPaint = new SKPaint { Color = AnvilLight, IsAntialias = false };
        using var hornPath = new SKPath();
        float hornY = top + height * 0.3f;
        hornPath.MoveTo(centerX - topWidth / 2 - 2, hornY);
        hornPath.LineTo(centerX - topWidth / 2 - hornLength, hornY + 5);
        hornPath.LineTo(centerX - topWidth / 2 - 2, hornY + 12);
        hornPath.Close();
        canvas.DrawPath(hornPath, hornPaint);

        // Sockel (breiter Block unten)
        float baseTop = top + height;
        using var basePaint = new SKPaint { Color = AnvilDark, IsAntialias = false };
        canvas.DrawRect(centerX - baseWidth / 2 - 4, baseTop, baseWidth + 8, 10, basePaint);
        canvas.DrawRect(centerX - baseWidth / 2 - 8, baseTop + 10, baseWidth + 16, 8, basePaint);
    }

    /// <summary>
    /// Zeichnet das Werkstueck auf dem Amboss. Farbe abhaengig von Temperatur.
    /// </summary>
    private void DrawWorkpiece(SKCanvas canvas, float centerX, float anvilTop, float anvilHeight, double temperature)
    {
        float pieceWidth = 40;
        float pieceHeight = 8;
        float pieceY = anvilTop - pieceHeight + 2; // Direkt auf dem Amboss

        // Farbe interpolieren basierend auf Temperatur
        SKColor metalColor = InterpolateMetalColor(temperature);

        using var metalPaint = new SKPaint { Color = metalColor, IsAntialias = false };
        canvas.DrawRect(centerX - pieceWidth / 2, pieceY, pieceWidth, pieceHeight, metalPaint);

        // Glueh-Effekt bei hoher Temperatur
        if (temperature > 0.3)
        {
            byte glowAlpha = (byte)(Math.Min(1.0, (temperature - 0.3) / 0.7) * 120);
            using var glowPaint = new SKPaint
            {
                Color = new SKColor(0xFF, 0xD7, 0x00, glowAlpha),
                IsAntialias = false,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4)
            };
            canvas.DrawRect(centerX - pieceWidth / 2 - 3, pieceY - 3, pieceWidth + 6, pieceHeight + 6, glowPaint);
        }

        // Weissglut-Glow bei sehr hoher Temperatur
        if (temperature > 0.7)
        {
            byte whiteAlpha = (byte)(Math.Min(1.0, (temperature - 0.7) / 0.3) * 80);
            using var whitePaint = new SKPaint
            {
                Color = new SKColor(0xFF, 0xFF, 0xFF, whiteAlpha),
                IsAntialias = false,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6)
            };
            canvas.DrawRect(centerX - pieceWidth / 2, pieceY, pieceWidth, pieceHeight, whitePaint);
        }
    }

    /// <summary>
    /// Interpoliert die Metall-Farbe basierend auf der Temperatur.
    /// </summary>
    private static SKColor InterpolateMetalColor(double temp)
    {
        if (temp <= 0.25)
        {
            // Kalt: Braun
            float t = (float)(temp / 0.25);
            return LerpColor(MetalCold, MetalWarm, t);
        }
        if (temp <= 0.55)
        {
            // Warm: Orange -> Gelb
            float t = (float)((temp - 0.25) / 0.3);
            return LerpColor(MetalWarm, MetalHot, t);
        }
        // Heiss: Gelb -> Weiss
        float t2 = (float)((temp - 0.55) / 0.45);
        t2 = Math.Clamp(t2, 0, 1);
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

    /// <summary>
    /// Zeichnet den Schmiedehammer ueber dem Amboss.
    /// Vibriert bei isPlaying, schlaegt runter bei isHammering.
    /// </summary>
    private void DrawHammer(SKCanvas canvas, float centerX, float baseY, bool isPlaying, bool isHammering)
    {
        float hammerOffsetY = 0;

        if (isHammering)
        {
            // Hammer schlaegt nach unten (schnelle Animation)
            float t = Math.Min(_hammerAnimTime / 0.12f, 1.0f);
            if (t < 0.5f)
            {
                // Runter
                hammerOffsetY = t * 2 * 25;
            }
            else
            {
                // Hoch (Bounce)
                hammerOffsetY = (1 - (t - 0.5f) * 2) * 25;
            }
        }
        else if (isPlaying)
        {
            // Leichte Vibration waehrend des Spiels
            hammerOffsetY = (float)Math.Sin(_animTime * 6) * 2;
        }

        float hammerY = baseY - 50 + hammerOffsetY;
        float hammerX = centerX + 15; // Leicht rechts versetzt

        // Stiel (vertikal)
        using var handlePaint = new SKPaint { Color = HammerHandle, IsAntialias = false };
        canvas.DrawRect(hammerX - 3, hammerY, 6, 40, handlePaint);

        // Stiel-Highlight
        using var handleLight = new SKPaint { Color = HammerHandleLight, IsAntialias = false };
        canvas.DrawRect(hammerX - 1, hammerY + 2, 2, 36, handleLight);

        // Hammerkopf (horizontal)
        using var headPaint = new SKPaint { Color = HammerHead, IsAntialias = false };
        canvas.DrawRect(hammerX - 16, hammerY - 8, 32, 12, headPaint);

        // Hammerkopf-Highlight (Metallglanz)
        using var headHighlight = new SKPaint { Color = AnvilHighlight, IsAntialias = false };
        canvas.DrawRect(hammerX - 14, hammerY - 7, 28, 3, headHighlight);

        // Hammerkopf-Schatten (unten)
        using var headShadow = new SKPaint { Color = AnvilDark, IsAntialias = false };
        canvas.DrawRect(hammerX - 14, hammerY + 1, 28, 2, headShadow);
    }

    /// <summary>
    /// Zeichnet die Esse/Schmiede-Feuer als Hintergrund-Dekoration.
    /// </summary>
    private void DrawForge(SKCanvas canvas, SKRect bounds, float padding, float areaTop, float areaHeight,
        double temperature, bool isPlaying)
    {
        // Esse links (Steinrahmen)
        float forgeLeft = bounds.Left + padding;
        float forgeTop = areaTop + 10;
        float forgeWidth = 50;
        float forgeHeight = areaHeight * 0.45f;

        // Steinrahmen
        using var stonePaint = new SKPaint { Color = new SKColor(0x5D, 0x4E, 0x44), IsAntialias = false };
        canvas.DrawRect(forgeLeft, forgeTop, forgeWidth, forgeHeight, stonePaint);

        // Innerer dunkler Bereich (Feuerstelle)
        using var innerPaint = new SKPaint { Color = new SKColor(0x2A, 0x1A, 0x0A), IsAntialias = false };
        canvas.DrawRect(forgeLeft + 5, forgeTop + 5, forgeWidth - 10, forgeHeight - 10, innerPaint);

        // Feuer (animiert, pulsierend)
        if (isPlaying || temperature > 0.05)
        {
            float firePulse = 0.7f + 0.3f * (float)Math.Sin(_animTime * 5);
            float fireHeight = (forgeHeight - 20) * firePulse * (float)Math.Max(0.3, temperature);

            float fireX = forgeLeft + 10;
            float fireBaseY = forgeTop + forgeHeight - 10;

            // Aeussere Flamme (dunkelorange)
            using var outerFlamePaint = new SKPaint { Color = FireDark.WithAlpha(200), IsAntialias = false };
            canvas.DrawRect(fireX, fireBaseY - fireHeight, forgeWidth - 20, fireHeight, outerFlamePaint);

            // Mittlere Flamme (orange)
            float midWidth = (forgeWidth - 20) * 0.7f;
            using var midFlamePaint = new SKPaint { Color = FireMedium.WithAlpha(200), IsAntialias = false };
            canvas.DrawRect(fireX + (forgeWidth - 20 - midWidth) / 2, fireBaseY - fireHeight * 0.8f,
                midWidth, fireHeight * 0.8f, midFlamePaint);

            // Innere Flamme (gelb)
            float innerWidth = (forgeWidth - 20) * 0.4f;
            using var innerFlamePaint = new SKPaint { Color = FireLight.WithAlpha(180), IsAntialias = false };
            canvas.DrawRect(fireX + (forgeWidth - 20 - innerWidth) / 2, fireBaseY - fireHeight * 0.5f,
                innerWidth, fireHeight * 0.5f, innerFlamePaint);

            // Glueh-Schein auf Umgebung
            byte glowAlpha = (byte)(firePulse * 40 * Math.Max(0.3, temperature));
            using var glowPaint = new SKPaint
            {
                Color = new SKColor(0xFF, 0x8C, 0x00, glowAlpha),
                IsAntialias = false,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 12)
            };
            canvas.DrawRect(forgeLeft - 10, forgeTop - 5, forgeWidth + 20, forgeHeight + 10, glowPaint);
        }

        // Rauch ueber der Esse (kleine graue Partikel)
        if (isPlaying)
        {
            float smokeBase = forgeTop - 5;
            using var smokePaint = new SKPaint { IsAntialias = false };
            for (int i = 0; i < 3; i++)
            {
                float smokeTime = _animTime + i * 1.3f;
                float smokeProgress = (smokeTime % 2.0f) / 2.0f;
                float smokeX = forgeLeft + forgeWidth / 2 + (float)Math.Sin(smokeTime * 2) * 6;
                float smokeY = smokeBase - smokeProgress * 30;
                byte smokeAlpha = (byte)((1 - smokeProgress) * 80);
                smokePaint.Color = new SKColor(0x90, 0x90, 0x90, smokeAlpha);
                canvas.DrawRect(smokeX - 2, smokeY - 2, 4, 4, smokePaint);
            }
        }

        // Esse rechts (dekorativ, Blasebalghinweis)
        float bellowsX = bounds.Right - padding - 40;
        float bellowsY = areaTop + areaHeight * 0.3f;

        using var bellowsPaint = new SKPaint { Color = new SKColor(0x6D, 0x4C, 0x41), IsAntialias = false };
        // Hauptkoerper
        canvas.DrawRect(bellowsX, bellowsY, 30, 20, bellowsPaint);
        // Griff
        using var bellowsHandle = new SKPaint { Color = new SKColor(0x8B, 0x5A, 0x2B), IsAntialias = false };
        canvas.DrawRect(bellowsX + 10, bellowsY - 8, 10, 10, bellowsHandle);
        // Spitze
        using var bellowsTip = new SKPaint { Color = new SKColor(0x90, 0x90, 0x90), IsAntialias = false };
        canvas.DrawRect(bellowsX - 5, bellowsY + 7, 8, 6, bellowsTip);
    }

    /// <summary>
    /// Zeichnet die Temperatur-Bar mit Zonen und Temperatur-Marker.
    /// </summary>
    private void DrawTemperatureBar(SKCanvas canvas, float x, float y, float width, float height,
        double temperature, double pStart, double pWidth, double gStart, double gWidth,
        double oStart, double oWidth, bool isPlaying)
    {
        // Bar-Hintergrund (dunkel)
        using var bgPaint = new SKPaint { Color = new SKColor(0x2A, 0x1A, 0x0A), IsAntialias = false };
        canvas.DrawRect(x, y, width, height, bgPaint);

        // Rahmen (Metalloptik)
        using var framePaint = new SKPaint { Color = new SKColor(0x5D, 0x5D, 0x5D), IsAntialias = false, StrokeWidth = 2, Style = SKPaintStyle.Stroke };
        canvas.DrawRect(x, y, width, height, framePaint);

        // Zu-kalt-Zone (links, blau)
        float coldEnd = x + (float)(oStart * width);
        using var coldPaint = new SKPaint { Color = ColdZone.WithAlpha(80), IsAntialias = false };
        canvas.DrawRect(x + 2, y + 2, coldEnd - x - 2, height - 4, coldPaint);

        // Zu-heiss-Zone (rechts, rot)
        float hotStart = x + (float)((oStart + oWidth) * width);
        using var hotPaint = new SKPaint { Color = TooHotZone.WithAlpha(80), IsAntialias = false };
        canvas.DrawRect(hotStart, y + 2, x + width - hotStart - 2, height - 4, hotPaint);

        // OK-Zone (gelb)
        float okLeft = x + (float)(oStart * width);
        float okW = (float)(oWidth * width);
        using var okPaint = new SKPaint { Color = OkZone.WithAlpha(100), IsAntialias = false };
        canvas.DrawRect(okLeft, y + 2, okW, height - 4, okPaint);

        // Good-Zone (orange)
        float goodLeft = x + (float)(gStart * width);
        float goodW = (float)(gWidth * width);
        using var goodPaint = new SKPaint { Color = GoodZone.WithAlpha(140), IsAntialias = false };
        canvas.DrawRect(goodLeft, y + 2, goodW, height - 4, goodPaint);

        // Perfect-Zone (glueh-orange, hell)
        float perfLeft = x + (float)(pStart * width);
        float perfW = (float)(pWidth * width);
        using var perfectPaint = new SKPaint { Color = PerfectZone.WithAlpha(200), IsAntialias = false };
        canvas.DrawRect(perfLeft, y + 2, perfW, height - 4, perfectPaint);

        // Perfect-Zone Glow-Puls
        if (isPlaying)
        {
            float pulse = (float)(0.5 + 0.5 * Math.Sin(_animTime * 4));
            using var glowPaint = new SKPaint { Color = PerfectZone.WithAlpha((byte)(50 * pulse)), IsAntialias = false };
            canvas.DrawRect(perfLeft - 2, y - 2, perfW + 4, height + 4, glowPaint);
        }

        // Tick-Markierungen
        using var tickPaint = new SKPaint { Color = new SKColor(255, 255, 255, 40), IsAntialias = false, StrokeWidth = 1 };
        for (float t = 0.1f; t < 1.0f; t += 0.1f)
        {
            float tickX = x + t * width;
            canvas.DrawLine(tickX, y, tickX, y + 4, tickPaint);
            canvas.DrawLine(tickX, y + height - 4, tickX, y + height, tickPaint);
        }

        // Temperatur-Beschriftungen
        using var labelPaint = new SKPaint
        {
            Color = new SKColor(255, 255, 255, 100),
            IsAntialias = true,
            TextSize = 9
        };
        canvas.DrawText("KALT", x + 4, y + height + 12, labelPaint);
        canvas.DrawText("HEISS", x + width - 30, y + height + 12, labelPaint);

        // Temperatur-Marker (dreieckiger Zeiger)
        float markerX = x + (float)(temperature * width);

        // Marker-Schatten
        using var markerShadow = new SKPaint { Color = new SKColor(0, 0, 0, 80), IsAntialias = false };
        canvas.DrawRect(markerX - 2, y - 3, 6, height + 6, markerShadow);

        // Marker-Koerper
        using var markerPaint = new SKPaint { Color = MarkerColor, IsAntialias = false };
        canvas.DrawRect(markerX - 3, y - 4, 6, height + 8, markerPaint);

        // Marker-Glanz
        using var markerHighlight = new SKPaint { Color = new SKColor(255, 255, 255, 200), IsAntialias = false };
        canvas.DrawRect(markerX - 1, y - 3, 2, 4, markerHighlight);

        // Marker-Spitze (Pfeil oben)
        using var arrowPaint = new SKPaint { Color = MarkerColor, IsAntialias = false };
        canvas.DrawRect(markerX - 4, y - 7, 8, 3, arrowPaint);
        canvas.DrawRect(markerX - 2, y - 9, 4, 2, arrowPaint);

        // Temperatur-Farbe auf Marker (visuelles Feedback)
        SKColor tempColor = InterpolateMetalColor(temperature);
        using var tempMarkerPaint = new SKPaint { Color = tempColor.WithAlpha(150), IsAntialias = false };
        canvas.DrawRect(markerX - 2, y + 2, 4, height - 4, tempMarkerPaint);
    }

    /// <summary>
    /// Zeichnet die Schlag-Fortschrittsanzeige (ausgefuellte/leere Punkte).
    /// </summary>
    private void DrawHitProgress(SKCanvas canvas, SKRect bounds, float top, int completed, int required)
    {
        if (required <= 0) return;

        float dotSize = 6;
        float spacing = 10;
        float totalWidth = required * dotSize + (required - 1) * (spacing - dotSize);
        float startX = bounds.Left + (bounds.Width - totalWidth) / 2;

        for (int i = 0; i < required; i++)
        {
            float cx = startX + i * spacing + dotSize / 2;
            float cy = top + dotSize / 2;

            if (i < completed)
            {
                // Abgeschlossener Schlag (orange ausgefuellt)
                using var filledPaint = new SKPaint { Color = FireDark, IsAntialias = false };
                canvas.DrawRect(cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize, filledPaint);
            }
            else
            {
                // Ausstehender Schlag (grauer Rahmen)
                using var emptyPaint = new SKPaint
                {
                    Color = new SKColor(0x80, 0x80, 0x80, 100),
                    IsAntialias = false,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1
                };
                canvas.DrawRect(cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize, emptyPaint);
            }
        }
    }

    /// <summary>
    /// Erzeugt Funken-Partikel bei einem Hammerschlag.
    /// </summary>
    private void SpawnSparks(SKRect bounds, double temperature)
    {
        if (_sparks.Count > 40) return; // Limit

        var random = Random.Shared;
        float centerX = bounds.Left + bounds.Width * 0.5f;
        float centerY = bounds.Top + bounds.Height * 0.3f;

        int sparkCount = 8 + (int)(temperature * 8); // Mehr Funken bei hoeherer Temperatur

        for (int i = 0; i < sparkCount; i++)
        {
            float angle = (float)(random.NextDouble() * Math.PI * 2);
            float speed = 40 + random.Next(0, 80);

            // Farbe temperaturabhaengig
            SKColor sparkColor;
            double colorRoll = random.NextDouble();
            if (temperature > 0.6 && colorRoll < 0.3)
                sparkColor = new SKColor(0xFF, 0xFF, 0xCC); // Helle Funken
            else if (colorRoll < 0.6)
                sparkColor = new SKColor(0xFF, 0xD7, 0x00); // Gold
            else
                sparkColor = new SKColor(0xFF, 0x8C, 0x00); // Orange

            _sparks.Add(new SparkParticle
            {
                X = centerX + random.Next(-10, 11),
                Y = centerY + random.Next(-5, 6),
                VelocityX = (float)Math.Cos(angle) * speed,
                VelocityY = (float)Math.Sin(angle) * speed - 30, // Tendenz nach oben
                Life = 0,
                MaxLife = 0.3f + (float)random.NextDouble() * 0.5f,
                Size = 1 + random.Next(0, 3),
                Color = sparkColor
            });
        }
    }

    /// <summary>
    /// Aktualisiert und zeichnet Funken-Partikel.
    /// </summary>
    private void UpdateAndDrawSparks(SKCanvas canvas, float deltaTime)
    {
        using var sparkPaint = new SKPaint { IsAntialias = false };

        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var p = _sparks[i];
            p.Life += deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            p.VelocityY += 80 * deltaTime; // Schwerkraft
            p.VelocityX *= 0.98f; // Luftwiderstand

            if (p.Life >= p.MaxLife)
            {
                _sparks.RemoveAt(i);
                continue;
            }

            _sparks[i] = p;

            // Alpha basierend auf verbleibender Lebenszeit
            float alpha = 1 - (p.Life / p.MaxLife);
            sparkPaint.Color = p.Color.WithAlpha((byte)(alpha * 255));
            canvas.DrawRect(p.X, p.Y, p.Size, p.Size, sparkPaint);
        }
    }
}
