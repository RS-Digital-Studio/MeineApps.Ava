using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// SkiaSharp-Renderer fuer das Saege-Minigame.
/// AAA-Qualitaet: Realistisches Holz mit Bezier-Maserung, 3D-Astloecher mit
/// Jahresringen, Rinden-Textur, Schneide-Animation mit Holzspaltung,
/// Saegemehl-Explosion und Stirnholz-Sicht.
/// </summary>
public class SawingGameRenderer
{
    // Holz-Farben (reicher/waermer)
    private static readonly SKColor WoodDark = new(0x5C, 0x3A, 0x21);
    private static readonly SKColor WoodMedium = new(0x8B, 0x5E, 0x3C);
    private static readonly SKColor WoodLight = new(0xA8, 0x7E, 0x56);
    private static readonly SKColor WoodGrain = new(0xC4, 0x9A, 0x6C);
    private static readonly SKColor WoodHighlight = new(0xD4, 0xAA, 0x7C);
    private static readonly SKColor BarkDark = new(0x3E, 0x27, 0x14);
    private static readonly SKColor BarkMedium = new(0x4A, 0x30, 0x1A);
    private static readonly SKColor EndGrainLight = new(0xC0, 0x90, 0x60);
    private static readonly SKColor EndGrainDark = new(0x6E, 0x48, 0x28);
    private static readonly SKColor EndGrainRing = new(0x8A, 0x60, 0x3A);

    // Saege-Farben (metallischer)
    private static readonly SKColor SawBlade = new(0xC8, 0xC8, 0xC8);
    private static readonly SKColor SawBladeShine = new(0xE8, 0xE8, 0xF0);
    private static readonly SKColor SawHandle = new(0x8B, 0x45, 0x13);
    private static readonly SKColor SawHandleLight = new(0xA5, 0x5B, 0x2A);
    private static readonly SKColor SawHandleDark = new(0x6D, 0x33, 0x0E);
    private static readonly SKColor SawTooth = new(0xA0, 0xA0, 0xA0);

    // Zonen-Farben
    private static readonly SKColor PerfectZone = new(0x4C, 0xAF, 0x50);
    private static readonly SKColor GoodZone = new(0xE8, 0xA0, 0x0E);
    private static readonly SKColor OkZone = new(0xFF, 0xC1, 0x07);
    private static readonly SKColor MissZone = new(0xEF, 0x44, 0x44);
    private static readonly SKColor MarkerColor = SKColors.White;

    // Saegemehl-Partikel
    private readonly List<SawdustParticle> _sawdust = new();
    private float _sawAnimTime;

    // Holzsplitter-Partikel (beim Schneiden)
    private readonly List<WoodChipParticle> _woodChips = new();

    // Schneide-Animation
    private bool _prevIsResultShown;
    private bool _cutStarted;
    private float _cutAnimTime;
    private bool _cutBurstDone;

    private struct SawdustParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Size;
    }

    private struct WoodChipParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Width, Height, Rotation, RotSpeed;
        public SKColor Color;
    }

    // Deterministische Astloch-Positionen (relativ zum Brett)
    private static readonly (float relX, float relY, float radius)[] KnotPositions =
    [
        (0.18f, 0.30f, 8f),
        (0.72f, 0.55f, 6f),
        (0.42f, 0.72f, 5f),
        (0.85f, 0.25f, 4f),
        (0.28f, 0.85f, 3.5f),
    ];

    /// <summary>
    /// Rendert das gesamte Saege-Spielfeld.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds,
        double markerPosition,
        double perfectStart, double perfectWidth,
        double goodStart, double goodWidth,
        double okStart, double okWidth,
        bool isPlaying, bool isResultShown,
        float deltaTime)
    {
        _sawAnimTime += deltaTime;

        // Schneide-Animation starten wenn Ergebnis erstmals angezeigt wird
        if (isResultShown && !_prevIsResultShown)
        {
            _cutStarted = true;
            _cutAnimTime = 0;
            _cutBurstDone = false;
        }
        _prevIsResultShown = isResultShown;

        if (_cutStarted)
            _cutAnimTime += deltaTime;

        float padding = 16;
        float barAreaTop = bounds.Top + padding;
        float barAreaBottom = bounds.Bottom - padding;
        float barAreaHeight = barAreaBottom - barAreaTop;

        // Obere Haelfte: Holzbrett mit Saege
        float woodTop = barAreaTop;
        float woodHeight = barAreaHeight * 0.55f;
        float woodBottom = woodTop + woodHeight;
        DrawWoodBoard(canvas, bounds.Left + padding, woodTop, bounds.Width - 2 * padding, woodHeight,
            markerPosition, isPlaying);

        // Untere Haelfte: Timing-Bar
        float barTop = woodBottom + 20;
        float barHeight = Math.Min(50, barAreaHeight * 0.2f);
        float barLeft = bounds.Left + padding + 8;
        float barWidth = bounds.Width - 2 * padding - 16;
        DrawTimingBar(canvas, barLeft, barTop, barWidth, barHeight,
            markerPosition, perfectStart, perfectWidth, goodStart, goodWidth, okStart, okWidth, isPlaying);

        // Partikel zeichnen (immer, auch nach Spielende fuer Schneide-Animation)
        UpdateAndDrawSawdust(canvas, bounds, padding, woodTop, woodHeight, deltaTime, isPlaying);
        UpdateAndDrawWoodChips(canvas, deltaTime);
    }

    /// <summary>
    /// Zeichnet das Holzbrett mit Bezier-Maserung, Jahresring-Astloechern,
    /// Rinden-Textur und optionaler Schneide/Spalt-Animation.
    /// </summary>
    private void DrawWoodBoard(SKCanvas canvas, float x, float y, float width, float height,
        double markerPos, bool isPlaying)
    {
        // Schneide-Fortschritt berechnen
        float cutProgress = 0; // 0-1: Saege faehrt durch Holz
        float splitProgress = 0; // 0-1: Holz spaltet sich
        if (_cutStarted)
        {
            cutProgress = Math.Clamp(_cutAnimTime / 1.2f, 0, 1); // 1.2s Schnitt
            if (cutProgress >= 1.0f)
            {
                float splitTime = _cutAnimTime - 1.2f;
                splitProgress = Math.Clamp(splitTime / 0.6f, 0, 1); // 0.6s Spaltung
                // EaseOutBack fuer federnden Effekt
                splitProgress = EaseOutBack(splitProgress);
            }
        }

        float cutX = x + width * 0.5f;
        float splitGap = splitProgress * width * 0.06f; // Max 6% Gap

        // Saegemehl-Burst beim Schneiden (einmalig)
        if (_cutStarted && cutProgress > 0.1f && !_cutBurstDone)
        {
            SpawnCutBurst(cutX, y + cutProgress * height, height);
            _cutBurstDone = true;
        }

        // Kontinuierliches Saegemehl waehrend des Schnitts
        if (_cutStarted && cutProgress > 0 && cutProgress < 1)
        {
            SpawnCuttingSawdust(cutX, y + cutProgress * height);
        }

        // === Linke Holzhaelfte ===
        float leftW = width * 0.5f - splitGap;
        canvas.Save();
        canvas.ClipRect(new SKRect(x, y, x + leftW, y + height));
        DrawWoodSurface(canvas, x, y, width, height);
        canvas.Restore();

        // === Rechte Holzhaelfte ===
        float rightX = cutX + splitGap;
        float rightW = width * 0.5f - splitGap;
        canvas.Save();
        canvas.ClipRect(new SKRect(rightX, y, rightX + rightW, y + height));
        DrawWoodSurface(canvas, x, y, width, height);
        canvas.Restore();

        // === Stirnholz im Spalt (Jahresringe sichtbar) ===
        if (splitGap > 1.5f)
        {
            DrawEndGrain(canvas, cutX - splitGap, y, splitGap * 2, height);
        }

        // === Schnittlinie ===
        if (_cutStarted && cutProgress < 1.0f)
        {
            float cutDepth = cutProgress * height;
            using var cutPaint = new SKPaint
            {
                Color = new SKColor(0x00, 0x00, 0x00, 120),
                IsAntialias = true,
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke
            };
            canvas.DrawLine(cutX, y, cutX, y + cutDepth, cutPaint);

            // Saegeriss-Splitter an der Schnittkante
            using var rissPath = new SKPath();
            var rng = new Random(42); // Deterministisch
            for (int i = 0; i < 6; i++)
            {
                float ry = y + cutDepth - 4 + rng.Next(0, 8);
                float rx = cutX + (rng.Next(0, 2) == 0 ? -1 : 1) * (1 + rng.Next(0, 3));
                rissPath.MoveTo(cutX, ry);
                rissPath.LineTo(rx, ry + rng.Next(-2, 3));
            }
            using var rissPaint = new SKPaint
            {
                Color = WoodDark.WithAlpha(80),
                IsAntialias = false,
                StrokeWidth = 1,
                Style = SKPaintStyle.Stroke
            };
            canvas.DrawPath(rissPath, rissPaint);
        }
        else if (!_cutStarted)
        {
            // Markierungslinie wo geschnitten werden soll
            using var markPaint = new SKPaint
            {
                Color = new SKColor(0xFF, 0xFF, 0xFF, 70),
                IsAntialias = false,
                StrokeWidth = 1,
                PathEffect = SKPathEffect.CreateDash([6, 4], 0)
            };
            canvas.DrawLine(cutX, y + 2, cutX, y + height - 2, markPaint);
        }

        // === Saege ===
        if (isPlaying)
        {
            float sawY = y + height + 4;
            float sawBounce = MathF.Sin(_sawAnimTime * 8) * 3;
            DrawSaw(canvas, cutX, sawY + sawBounce, width * 0.35f);
        }
        else if (_cutStarted && cutProgress < 1.0f)
        {
            // Saege faehrt durch das Holz
            float sawY = y + cutProgress * height;
            float sawBounce = MathF.Sin(_sawAnimTime * 14) * 1.5f; // Schnellere Vibration
            DrawSaw(canvas, cutX, sawY + sawBounce, width * 0.35f);
        }
    }

    /// <summary>
    /// Zeichnet die komplette Holzoberflaeche mit Bezier-Maserung,
    /// Astloechern mit Jahresringen und Rinden-Textur.
    /// </summary>
    private void DrawWoodSurface(SKCanvas canvas, float x, float y, float width, float height)
    {
        // Hintergrund-Farbe (Brett-Koerper)
        using var woodPaint = new SKPaint { Color = WoodMedium, IsAntialias = false };
        canvas.DrawRect(x, y, width, height, woodPaint);

        // Subtiler Farbverlauf (Mitte heller, Raender dunkler)
        using var gradientPaint = new SKPaint
        {
            IsAntialias = false,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(x, y), new SKPoint(x, y + height),
                [WoodDark.WithAlpha(60), SKColors.Transparent, WoodDark.WithAlpha(40)],
                [0, 0.5f, 1.0f],
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(x, y, width, height, gradientPaint);

        // === Bezier-Maserung (geschwungene Linien) ===
        DrawWoodGrainCurved(canvas, x, y, width, height);

        // === Astloecher mit Jahresringen ===
        foreach (var (relX, relY, radius) in KnotPositions)
        {
            float kx = x + relX * width;
            float ky = y + relY * height;
            float scaledRadius = radius * Math.Min(width, height) / 200f;
            scaledRadius = Math.Max(scaledRadius, 3);
            DrawKnot(canvas, kx, ky, scaledRadius);
        }

        // === Rinden-Textur oben/unten ===
        DrawBark(canvas, x, y, width, 5, true);
        DrawBark(canvas, x, y + height - 5, width, 5, false);

        // === Seitenkanten (dunkler) ===
        using var edgePaint = new SKPaint { Color = WoodDark, IsAntialias = false };
        canvas.DrawRect(x, y, 3, height, edgePaint);
        canvas.DrawRect(x + width - 3, y, 3, height, edgePaint);

        // === Glanzkante oben (3D-Effekt) ===
        using var shinePaint = new SKPaint { Color = WoodHighlight.WithAlpha(60), IsAntialias = false };
        canvas.DrawRect(x + 3, y + 5, width - 6, 2, shinePaint);
    }

    /// <summary>
    /// Zeichnet geschwungene Holzmaserung mit Bezier-Kurven die um Astloecher fliessen.
    /// </summary>
    private static void DrawWoodGrainCurved(SKCanvas canvas, float x, float y, float width, float height)
    {
        using var grainPaint1 = new SKPaint
        {
            Color = WoodGrain.WithAlpha(90),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f
        };
        using var grainPaint2 = new SKPaint
        {
            Color = WoodLight.WithAlpha(50),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        using var grainPaint3 = new SKPaint
        {
            Color = WoodDark.WithAlpha(40),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.8f
        };

        // Hauptmaserung (dickere Linien)
        for (float gy = y + 8; gy < y + height - 5; gy += 9)
        {
            using var path = new SKPath();
            float waveOffset = MathF.Sin(gy * 0.15f) * 6;
            path.MoveTo(x + 2, gy + waveOffset);

            float segW = width / 5f;
            for (int seg = 0; seg < 5; seg++)
            {
                float x1 = x + seg * segW;
                float x2 = x + (seg + 1) * segW;
                float midX = (x1 + x2) * 0.5f;
                float ctrlY = gy + MathF.Sin((x1 + gy) * 0.06f) * 5 + waveOffset;
                float endY = gy + MathF.Sin((x2 + gy * 0.8f) * 0.05f) * 3 + waveOffset;
                path.QuadTo(midX, ctrlY, x2, endY);
            }

            var paint = ((int)(gy * 3) % 3) switch
            {
                0 => grainPaint1,
                1 => grainPaint2,
                _ => grainPaint3
            };
            canvas.DrawPath(path, paint);
        }

        // Feine Zwischen-Maserung (duennere Linien, versetzt)
        using var fineGrainPaint = new SKPaint
        {
            Color = WoodGrain.WithAlpha(35),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.5f
        };

        for (float gy = y + 12; gy < y + height - 5; gy += 9)
        {
            using var path = new SKPath();
            float waveOffset = MathF.Sin(gy * 0.12f + 1.5f) * 4;
            path.MoveTo(x + 4, gy + waveOffset);

            float segW = width / 4f;
            for (int seg = 0; seg < 4; seg++)
            {
                float x1 = x + seg * segW;
                float x2 = x + (seg + 1) * segW;
                float midX = (x1 + x2) * 0.5f;
                float ctrlY = gy + MathF.Sin((x1 + gy) * 0.08f + 2) * 3 + waveOffset;
                path.QuadTo(midX, ctrlY, x2, gy + waveOffset);
            }

            canvas.DrawPath(path, fineGrainPaint);
        }
    }

    /// <summary>
    /// Zeichnet ein Astloch mit konzentrischen Jahresringen und 3D-Tiefe.
    /// </summary>
    private static void DrawKnot(SKCanvas canvas, float cx, float cy, float radius)
    {
        // Aeusserer Schatten (Vertiefung)
        using var shadowPaint = new SKPaint
        {
            Color = WoodDark.WithAlpha(100),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2)
        };
        canvas.DrawCircle(cx + 1, cy + 1, radius + 1, shadowPaint);

        // Hintergrund (dunkleres Holz)
        using var bgPaint = new SKPaint { Color = WoodDark, IsAntialias = true };
        canvas.DrawCircle(cx, cy, radius, bgPaint);

        // Jahresringe (konzentrische Kreise mit abwechselnden Farben)
        int ringCount = Math.Max(2, (int)(radius / 2));
        for (int i = ringCount; i >= 0; i--)
        {
            float r = radius * (i / (float)ringCount);
            byte alpha = (byte)(120 + (ringCount - i) * 15);
            alpha = Math.Min(alpha, (byte)220);

            var color = (i % 2 == 0) ? WoodMedium.WithAlpha(alpha) : WoodDark.WithAlpha(alpha);
            using var ringPaint = new SKPaint
            {
                Color = color,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(1, radius / ringCount * 0.8f)
            };
            canvas.DrawCircle(cx, cy, r, ringPaint);
        }

        // Zentrum (dunkler Kern)
        using var centerPaint = new SKPaint { Color = new SKColor(0x3A, 0x22, 0x10, 200), IsAntialias = true };
        canvas.DrawCircle(cx, cy, radius * 0.2f, centerPaint);

        // Glanz-Punkt (Lichtreflexion oben links)
        using var glanzPaint = new SKPaint { Color = WoodHighlight.WithAlpha(60), IsAntialias = true };
        canvas.DrawCircle(cx - radius * 0.25f, cy - radius * 0.25f, radius * 0.15f, glanzPaint);
    }

    /// <summary>
    /// Zeichnet Rinden-Textur an den Brettkanten.
    /// </summary>
    private static void DrawBark(SKCanvas canvas, float x, float y, float width, float barkHeight, bool isTop)
    {
        // Rinden-Hintergrund
        using var barkPaint = new SKPaint { Color = BarkDark, IsAntialias = false };
        canvas.DrawRect(x, y, width, barkHeight, barkPaint);

        // Raue Rinden-Textur (unregelmaeige Pixel-Bloecke)
        using var texPaint = new SKPaint { Color = BarkMedium, IsAntialias = false };
        var rng = new Random(isTop ? 123 : 456); // Deterministisch
        for (float bx = x + 1; bx < x + width - 2; bx += 4 + rng.Next(0, 3))
        {
            float by = y + rng.Next(0, (int)barkHeight - 1);
            float bw = 2 + rng.Next(0, 3);
            float bh = 1 + rng.Next(0, 2);
            canvas.DrawRect(bx, by, bw, bh, texPaint);
        }

        // Heller Streifen (Rinden-Kante zum Holz)
        if (isTop)
        {
            using var edgePaint = new SKPaint { Color = WoodDark.WithAlpha(180), IsAntialias = false };
            canvas.DrawRect(x, y + barkHeight - 1, width, 1, edgePaint);
        }
        else
        {
            using var edgePaint = new SKPaint { Color = WoodDark.WithAlpha(180), IsAntialias = false };
            canvas.DrawRect(x, y, width, 1, edgePaint);
        }
    }

    /// <summary>
    /// Zeichnet Stirnholz (Querschnitt) im Spalt mit sichtbaren Jahresringen.
    /// </summary>
    private static void DrawEndGrain(SKCanvas canvas, float x, float y, float gapWidth, float height)
    {
        // Hintergrund (helles Stirnholz)
        using var bgPaint = new SKPaint { Color = EndGrainLight, IsAntialias = false };
        canvas.DrawRect(x, y, gapWidth, height, bgPaint);

        // Jahresringe (horizontale gebogene Linien)
        using var ringPaint = new SKPaint
        {
            Color = EndGrainRing.WithAlpha(120),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };

        float centerY = y + height * 0.45f; // Leicht nach oben versetzt
        float centerX = x + gapWidth * 0.5f;
        for (float r = 5; r < height * 0.6f; r += 4)
        {
            using var path = new SKPath();
            path.AddArc(new SKRect(centerX - gapWidth * 0.4f, centerY - r, centerX + gapWidth * 0.4f, centerY + r),
                0, 360);
            canvas.DrawPath(path, ringPaint);
        }

        // Dunklere Raender
        using var edgePaint = new SKPaint { Color = EndGrainDark.WithAlpha(100), IsAntialias = false };
        canvas.DrawRect(x, y, 1, height, edgePaint);
        canvas.DrawRect(x + gapWidth - 1, y, 1, height, edgePaint);
    }

    /// <summary>
    /// Zeichnet die Saege mit detailliertem Blatt, Zaehnen und Griff.
    /// </summary>
    private void DrawSaw(SKCanvas canvas, float x, float y, float sawHalfWidth)
    {
        float bladeH = 5;
        float toothH = 4;

        // Saegeblatt (metallischer Gradient)
        using var bladePaint = new SKPaint
        {
            IsAntialias = false,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(x - sawHalfWidth, y),
                new SKPoint(x - sawHalfWidth, y + bladeH),
                [SawBladeShine, SawBlade, new SKColor(0x90, 0x90, 0x90)],
                [0, 0.4f, 1.0f],
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(x - sawHalfWidth, y, sawHalfWidth * 2, bladeH, bladePaint);

        // Saegezaehne (alternierend gross/klein)
        using var toothPaint = new SKPaint { Color = SawTooth, IsAntialias = false };
        bool bigTooth = true;
        for (float tx = x - sawHalfWidth + 2; tx < x + sawHalfWidth - 2; tx += 5)
        {
            float th = bigTooth ? toothH : toothH * 0.6f;
            canvas.DrawRect(tx, y + bladeH, 2, th, toothPaint);
            // Zahnspitze (dunklerer Punkt)
            using var tipPaint = new SKPaint { Color = new SKColor(0x70, 0x70, 0x70), IsAntialias = false };
            canvas.DrawRect(tx, y + bladeH + th, 2, 1, tipPaint);
            bigTooth = !bigTooth;
        }

        // Griff (ergonomisch mit Holz-Optik)
        float handleW = 20;
        float handleH = 16;
        float handleX = x - handleW / 2;
        float handleY = y - handleH;

        using var handlePaint = new SKPaint
        {
            IsAntialias = false,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(handleX, handleY),
                new SKPoint(handleX, handleY + handleH),
                [SawHandleLight, SawHandle, SawHandleDark],
                [0, 0.5f, 1.0f],
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(handleX, handleY, handleW, handleH, handlePaint);

        // Griff-Akzent (Metallring oben/unten)
        using var ringPaint = new SKPaint { Color = new SKColor(0x90, 0x90, 0x90), IsAntialias = false };
        canvas.DrawRect(handleX, handleY, handleW, 2, ringPaint);
        canvas.DrawRect(handleX, handleY + handleH - 2, handleW, 2, ringPaint);

        // Griff-Niete (zwei kleine Punkte)
        using var nietPaint = new SKPaint { Color = new SKColor(0xC0, 0xC0, 0xC0), IsAntialias = false };
        canvas.DrawRect(x - 3, handleY + handleH * 0.35f, 2, 2, nietPaint);
        canvas.DrawRect(x + 1, handleY + handleH * 0.35f, 2, 2, nietPaint);
    }

    /// <summary>
    /// Zeichnet die Timing-Bar mit Zonen und Marker.
    /// </summary>
    private void DrawTimingBar(SKCanvas canvas, float x, float y, float width, float height,
        double markerPos, double pStart, double pWidth, double gStart, double gWidth,
        double oStart, double oWidth, bool isPlaying)
    {
        // Bar-Hintergrund (dunkle Holz-Optik)
        using var bgPaint = new SKPaint { Color = new SKColor(0x33, 0x2B, 0x20), IsAntialias = false };
        canvas.DrawRect(x, y, width, height, bgPaint);

        // Rahmen (Holzoptik)
        using var framePaint = new SKPaint
        {
            Color = new SKColor(0x5D, 0x40, 0x37),
            IsAntialias = false,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke
        };
        canvas.DrawRect(x, y, width, height, framePaint);

        // Miss-Zone (gesamte Bar)
        using var missPaint = new SKPaint { Color = MissZone.WithAlpha(60), IsAntialias = false };
        canvas.DrawRect(x + 2, y + 2, width - 4, height - 4, missPaint);

        // OK-Zone
        float okLeft = x + (float)(oStart * width);
        float okW = (float)(oWidth * width);
        using var okPaint = new SKPaint { Color = OkZone.WithAlpha(100), IsAntialias = false };
        canvas.DrawRect(okLeft, y + 2, okW, height - 4, okPaint);

        // Good-Zone
        float goodLeft = x + (float)(gStart * width);
        float goodW = (float)(gWidth * width);
        using var goodPaint = new SKPaint { Color = GoodZone.WithAlpha(140), IsAntialias = false };
        canvas.DrawRect(goodLeft, y + 2, goodW, height - 4, goodPaint);

        // Perfect-Zone
        float perfLeft = x + (float)(pStart * width);
        float perfW = (float)(pWidth * width);
        using var perfectPaint = new SKPaint { Color = PerfectZone.WithAlpha(200), IsAntialias = false };
        canvas.DrawRect(perfLeft, y + 2, perfW, height - 4, perfectPaint);

        // Perfect-Zone Glow-Puls
        if (isPlaying)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin(_sawAnimTime * 4);
            using var glowPaint = new SKPaint { Color = PerfectZone.WithAlpha((byte)(40 * pulse)), IsAntialias = false };
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

        // Marker
        float markerX = x + (float)(markerPos * width);

        using var markerShadow = new SKPaint { Color = new SKColor(0, 0, 0, 80), IsAntialias = false };
        canvas.DrawRect(markerX - 2, y - 3, 6, height + 6, markerShadow);

        using var markerPaint = new SKPaint { Color = MarkerColor, IsAntialias = false };
        canvas.DrawRect(markerX - 3, y - 4, 6, height + 8, markerPaint);

        using var markerHighlight = new SKPaint { Color = new SKColor(255, 255, 255, 200), IsAntialias = false };
        canvas.DrawRect(markerX - 1, y - 3, 2, 4, markerHighlight);

        // Marker-Spitze
        using var arrowPaint = new SKPaint { Color = MarkerColor, IsAntialias = false };
        canvas.DrawRect(markerX - 4, y - 7, 8, 3, arrowPaint);
        canvas.DrawRect(markerX - 2, y - 9, 4, 2, arrowPaint);
    }

    /// <summary>
    /// Erzeugt Saegemehl-Explosion beim Schnitt-Start.
    /// </summary>
    private void SpawnCutBurst(float cutX, float cutY, float woodHeight)
    {
        var random = Random.Shared;

        // Grosse Saegemehl-Wolke
        for (int i = 0; i < 30; i++)
        {
            _sawdust.Add(new SawdustParticle
            {
                X = cutX + random.Next(-12, 13),
                Y = cutY + random.Next(-8, 8),
                VelocityX = (float)(random.NextDouble() - 0.5) * 120,
                VelocityY = -40 - random.Next(0, 80),
                Life = 0,
                MaxLife = 0.8f + (float)random.NextDouble() * 0.8f,
                Size = 1 + random.Next(0, 4)
            });
        }

        // Holzsplitter (groesser, drehen sich)
        for (int i = 0; i < 8; i++)
        {
            _woodChips.Add(new WoodChipParticle
            {
                X = cutX + random.Next(-6, 7),
                Y = cutY + random.Next(-4, 4),
                VelocityX = (float)(random.NextDouble() - 0.5) * 80,
                VelocityY = -50 - random.Next(0, 60),
                Life = 0,
                MaxLife = 1.0f + (float)random.NextDouble() * 0.5f,
                Width = 3 + random.Next(0, 4),
                Height = 1 + random.Next(0, 2),
                Rotation = (float)random.NextDouble() * 360,
                RotSpeed = (float)(random.NextDouble() - 0.5) * 400,
                Color = random.Next(3) switch
                {
                    0 => WoodLight,
                    1 => WoodMedium,
                    _ => WoodGrain
                }
            });
        }
    }

    /// <summary>
    /// Erzeugt kontinuierliches Saegemehl waehrend des Schnitts.
    /// </summary>
    private void SpawnCuttingSawdust(float cutX, float cutY)
    {
        if (_sawdust.Count >= 40) return;
        var random = Random.Shared;

        _sawdust.Add(new SawdustParticle
        {
            X = cutX + random.Next(-4, 5),
            Y = cutY + random.Next(-2, 3),
            VelocityX = (float)(random.NextDouble() - 0.5) * 50,
            VelocityY = -15 - random.Next(0, 25),
            Life = 0,
            MaxLife = 0.4f + (float)random.NextDouble() * 0.4f,
            Size = 1 + random.Next(0, 3)
        });
    }

    /// <summary>
    /// Aktualisiert und zeichnet Saegemehl-Partikel.
    /// </summary>
    private void UpdateAndDrawSawdust(SKCanvas canvas, SKRect bounds, float padding,
        float woodTop, float woodHeight, float deltaTime, bool isPlaying)
    {
        var random = Random.Shared;
        float cutX = bounds.Left + padding + (bounds.Width - 2 * padding) * 0.5f;
        float woodBottom = woodTop + woodHeight;

        // Waehrend des Spiels: Standard-Saegemehl an der Schnittstelle
        if (isPlaying && _sawdust.Count < 20)
        {
            _sawdust.Add(new SawdustParticle
            {
                X = cutX + random.Next(-8, 9),
                Y = woodBottom - random.Next(0, 10),
                VelocityX = (float)(random.NextDouble() - 0.5) * 40,
                VelocityY = -20 - random.Next(0, 30),
                Life = 0,
                MaxLife = 0.5f + (float)random.NextDouble() * 0.5f,
                Size = 1 + random.Next(0, 3)
            });
        }

        // Partikel aktualisieren und zeichnen
        using var dustPaint = new SKPaint { IsAntialias = false };
        for (int i = _sawdust.Count - 1; i >= 0; i--)
        {
            var p = _sawdust[i];
            p.Life += deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            p.VelocityY += 60 * deltaTime; // Schwerkraft

            if (p.Life >= p.MaxLife)
            {
                _sawdust.RemoveAt(i);
                continue;
            }

            _sawdust[i] = p;

            float alpha = 1 - (p.Life / p.MaxLife);
            dustPaint.Color = new SKColor(0xD2, 0xB4, 0x8C, (byte)(alpha * 220));
            canvas.DrawRect(p.X, p.Y, p.Size, p.Size, dustPaint);
        }
    }

    /// <summary>
    /// Aktualisiert und zeichnet Holzsplitter-Partikel (rotierende Stuecke).
    /// </summary>
    private void UpdateAndDrawWoodChips(SKCanvas canvas, float deltaTime)
    {
        for (int i = _woodChips.Count - 1; i >= 0; i--)
        {
            var p = _woodChips[i];
            p.Life += deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            p.VelocityY += 80 * deltaTime; // Schwerkraft
            p.Rotation += p.RotSpeed * deltaTime;

            if (p.Life >= p.MaxLife)
            {
                _woodChips.RemoveAt(i);
                continue;
            }

            _woodChips[i] = p;

            float alpha = 1 - (p.Life / p.MaxLife);
            using var chipPaint = new SKPaint
            {
                Color = p.Color.WithAlpha((byte)(alpha * 255)),
                IsAntialias = false
            };

            canvas.Save();
            canvas.Translate(p.X, p.Y);
            canvas.RotateDegrees(p.Rotation);
            canvas.DrawRect(-p.Width / 2, -p.Height / 2, p.Width, p.Height, chipPaint);
            canvas.Restore();
        }
    }

    /// <summary>
    /// EaseOutBack-Kurve fuer federnden Spaltungs-Effekt.
    /// </summary>
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1;
        return 1 + c3 * MathF.Pow(t - 1, 3) + c1 * MathF.Pow(t - 1, 2);
    }
}
