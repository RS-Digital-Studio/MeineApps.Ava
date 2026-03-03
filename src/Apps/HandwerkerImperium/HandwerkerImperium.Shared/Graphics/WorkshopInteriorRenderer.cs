using SkiaSharp;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert den dezenten Hintergrund einer Werkstatt.
/// Boden-Gradient + Boden-Pattern + Vignette-Beleuchtung + Wand-Details.
/// Die eigentliche Szene wird vom WorkshopSceneRenderer darüber gezeichnet.
/// Gecachte SKPaint-Instanzen fuer 0 Allokationen pro Frame.
/// </summary>
public class WorkshopInteriorRenderer : IDisposable
{
    private bool _disposed;

    // Statisches Farbarray (vermeidet Allokation pro Frame)
    private static readonly SKColor[] PainterSplatColors =
    [
        new SKColor(0xEC, 0x48, 0x99, 20), new SKColor(0x42, 0xA5, 0xF5, 20),
        new SKColor(0x66, 0xBB, 0x6A, 20), new SKColor(0xFF, 0xCA, 0x28, 20)
    ];

    // ═══════════════════════════════════════════════════════════════════════
    // GECACHTE SKPAINT-INSTANZEN (0 Allokationen pro Frame)
    // ═══════════════════════════════════════════════════════════════════════

    // --- Statische Paints (konstante Properties) ---
    private static readonly SKPaint _cablePaint = new()
    {
        IsAntialias = true,
        Color = new SKColor(0x50, 0x45, 0x38, 45),
        StrokeWidth = 1.2f,
        Style = SKPaintStyle.Stroke
    };

    private static readonly SKPaint _shadePaint = new()
    {
        IsAntialias = true,
        Color = new SKColor(0x6D, 0x5C, 0x48, 50),
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint _shadeEdgePaint = new()
    {
        IsAntialias = true,
        Color = new SKColor(0x90, 0x80, 0x68, 35),
        StrokeWidth = 0.8f,
        Style = SKPaintStyle.Stroke
    };

    // --- Instanz-Paints (dynamische Farbe/Shader pro Frame) ---
    private readonly SKPaint _gradientPaint = new() { IsAntialias = true };
    private readonly SKPaint _vignettePaint = new() { IsAntialias = true };
    private readonly SKPaint _spotPaint = new() { IsAntialias = true };
    private readonly SKPaint _glowPaint = new() { IsAntialias = true };
    private readonly SKPaint _bulbPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _corePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _conePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _detailPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private readonly SKPaint _linePaint = new() { IsAntialias = true, StrokeWidth = 0.5f, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _splatPaint = new() { IsAntialias = true };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gradientPaint.Shader?.Dispose();
        _gradientPaint.Dispose();
        _vignettePaint.Shader?.Dispose();
        _vignettePaint.Dispose();
        _spotPaint.Shader?.Dispose();
        _spotPaint.Dispose();
        _glowPaint.Shader?.Dispose();
        _glowPaint.Dispose();
        _bulbPaint.Dispose();
        _corePaint.Dispose();
        _conePaint.Dispose();
        _detailPaint.Dispose();
        _linePaint.Dispose();
        _splatPaint.Dispose();
    }
    // Workshop-Typ-spezifische Farbpaletten
    private static readonly Dictionary<WorkshopType, (SKColor floorTop, SKColor floorBottom, SKColor pattern)> WorkshopColors = new()
    {
        { WorkshopType.Carpenter,          (new SKColor(0xD7, 0xCC, 0xB7), new SKColor(0xBC, 0xAA, 0x84), new SKColor(0xA6, 0x93, 0x72, 35)) },
        { WorkshopType.Plumber,            (new SKColor(0xCF, 0xD8, 0xDC), new SKColor(0xB0, 0xBE, 0xC5), new SKColor(0x90, 0xA4, 0xAE, 35)) },
        { WorkshopType.Electrician,        (new SKColor(0xE0, 0xE0, 0xE0), new SKColor(0xBD, 0xBD, 0xBD), new SKColor(0xA0, 0xA0, 0xA0, 30)) },
        { WorkshopType.Painter,            (new SKColor(0xF5, 0xF5, 0xF5), new SKColor(0xE1, 0xD5, 0xC0), new SKColor(0xD0, 0xC0, 0xA8, 25)) },
        { WorkshopType.Roofer,             (new SKColor(0xD7, 0xCC, 0xA1), new SKColor(0xA1, 0x88, 0x7F), new SKColor(0x8D, 0x6E, 0x63, 30)) },
        { WorkshopType.Contractor,         (new SKColor(0xD2, 0xC4, 0xA0), new SKColor(0xC8, 0xB0, 0x90), new SKColor(0xB0, 0x9A, 0x78, 30)) },
        { WorkshopType.Architect,          (new SKColor(0xEE, 0xEE, 0xEE), new SKColor(0xD0, 0xD0, 0xD0), new SKColor(0xC0, 0xC0, 0xC0, 25)) },
        { WorkshopType.GeneralContractor,  (new SKColor(0xE8, 0xD8, 0xA0), new SKColor(0xC0, 0xA8, 0x70), new SKColor(0xA8, 0x90, 0x60, 30)) },
        { WorkshopType.MasterSmith,         (new SKColor(0xD7, 0xC0, 0xA0), new SKColor(0xA8, 0x88, 0x60), new SKColor(0x90, 0x70, 0x48, 35)) },
        { WorkshopType.InnovationLab,       (new SKColor(0xE0, 0xE0, 0xEA), new SKColor(0xC8, 0xC8, 0xD8), new SKColor(0xB0, 0xB0, 0xC8, 25)) },
    };

    // Workshop-Typ-spezifische Lichtfarben (für dynamische Beleuchtung)
    private static readonly Dictionary<WorkshopType, SKColor> LightColors = new()
    {
        { WorkshopType.Carpenter,          new SKColor(0xFF, 0xD7, 0x00) },  // Warmes Amber
        { WorkshopType.Plumber,            new SKColor(0xB0, 0xD4, 0xF1) },  // Kühles Blau-Weiß
        { WorkshopType.Electrician,        new SKColor(0xFF, 0xF5, 0x90) },  // Helles Gelb
        { WorkshopType.Painter,            new SKColor(0xFF, 0xF0, 0xE0) },  // Neutrales Warmweiß
        { WorkshopType.Roofer,             new SKColor(0xFF, 0xCC, 0x80) },  // Warmes Orange
        { WorkshopType.Contractor,         new SKColor(0xFF, 0xE0, 0x82) },  // Warmes Gelb
        { WorkshopType.Architect,          new SKColor(0xE8, 0xEA, 0xF0) },  // Kühles Weiß
        { WorkshopType.GeneralContractor,  new SKColor(0xFF, 0xD7, 0x00) },  // Reiches Gold
        { WorkshopType.MasterSmith,        new SKColor(0xFF, 0x8F, 0x00) },  // Tiefer Amber (Essen-Glut)
        { WorkshopType.InnovationLab,      new SKColor(0xB3, 0xA0, 0xFF) },  // Kühles Blau-Violett
    };

    /// <summary>
    /// Rendert den dezenten Hintergrund eines Workshops mit dynamischer Beleuchtung.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, Workshop workshop, float time)
    {
        var colors = WorkshopColors.GetValueOrDefault(workshop.Type,
            (new SKColor(0xD7, 0xCC, 0xB7), new SKColor(0xBC, 0xAA, 0x84), new SKColor(0xA6, 0x93, 0x72, 35)));

        // Vertikaler Gradient (oben heller → unten dunkler)
        using var gradientShader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.Left, bounds.Top),
            new SKPoint(bounds.Left, bounds.Bottom),
            new[] { colors.Item1, colors.Item2 },
            null,
            SKShaderTileMode.Clamp);
        _gradientPaint.Shader?.Dispose();
        _gradientPaint.Shader = gradientShader;
        canvas.DrawRect(bounds, _gradientPaint);
        _gradientPaint.Shader = null;

        // Dezente Wand-Details (obere 50%, sehr subtil)
        DrawWallDetails(canvas, bounds, workshop.Type);

        // Dezentes Boden-Pattern (nur untere 25%, sehr subtil)
        DrawSubtleFloorPattern(canvas, bounds, workshop.Type, colors.Item3);

        // Dynamische Beleuchtung (Spotlight + Hängelampe + warme Vignette)
        DrawDynamicLighting(canvas, bounds, workshop.Type, time);
    }

    // =================================================================
    // Dynamische Beleuchtung (ersetzt statische Vignette)
    // =================================================================

    private void DrawDynamicLighting(SKCanvas canvas, SKRect bounds, WorkshopType type, float time)
    {
        var lightColor = LightColors.GetValueOrDefault(type, new SKColor(0xFF, 0xD7, 0x00));

        // 1. Warme Vignette (Ränder dunkel-warm statt kalt-schwarz)
        DrawWarmVignette(canvas, bounds, lightColor);

        // 2. Dynamischer Spotlight (wandernder Lichtkreis über Arbeitsfläche)
        DrawSpotlight(canvas, bounds, lightColor, time);

        // 3. Hängelampe an der Decke mit Glow-Puls
        DrawHangingLamp(canvas, bounds, lightColor, time);
    }

    /// <summary>
    /// Warme Vignette: Ränder mit warmem Dunkel statt kaltem Schwarz.
    /// Subtiler Warm-Tint im Zentrum für Werkstatt-Atmosphäre.
    /// </summary>
    private void DrawWarmVignette(SKCanvas canvas, SKRect bounds, SKColor lightColor)
    {
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float radius = MathF.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height) * 0.55f;

        // Warme Randfarbe: Lichtfarbe stark abgedunkelt
        byte edgeR = (byte)(lightColor.Red / 8);
        byte edgeG = (byte)(lightColor.Green / 10);
        byte edgeB = (byte)(lightColor.Blue / 12);

        using var vignetteShader = SKShader.CreateRadialGradient(
            new SKPoint(cx, cy),
            radius,
            new[]
            {
                new SKColor(lightColor.Red, lightColor.Green, lightColor.Blue, 8),
                SKColors.Transparent,
                new SKColor(edgeR, edgeG, edgeB, 30),
                new SKColor(edgeR, edgeG, edgeB, 55),
            },
            new float[] { 0f, 0.3f, 0.7f, 1.0f },
            SKShaderTileMode.Clamp);
        _vignettePaint.Shader?.Dispose();
        _vignettePaint.Shader = vignetteShader;
        canvas.DrawRect(bounds, _vignettePaint);
        _vignettePaint.Shader = null;
    }

    /// <summary>
    /// Dynamischer Spotlight: Warmer Lichtkreis der sich langsam über die
    /// Arbeitsfläche bewegt (Sinus X/Y, ~0.2Hz = 5s Periode).
    /// </summary>
    private void DrawSpotlight(SKCanvas canvas, SKRect bounds, SKColor lightColor, float time)
    {
        // Spotlight-Position: Langsame Sinus-Bewegung (~0.2Hz)
        float spotX = bounds.MidX + MathF.Sin(time * 0.4f * MathF.PI) * bounds.Width * 0.15f;
        float spotY = bounds.Top + bounds.Height * 0.45f + MathF.Cos(time * 0.3f * MathF.PI) * bounds.Height * 0.08f;

        // Spotlight-Radius: ~45% der kleineren Dimension
        float spotRadius = MathF.Min(bounds.Width, bounds.Height) * 0.45f;

        using var spotShader = SKShader.CreateRadialGradient(
            new SKPoint(spotX, spotY),
            spotRadius,
            new[]
            {
                new SKColor(lightColor.Red, lightColor.Green, lightColor.Blue, 22),
                new SKColor(lightColor.Red, lightColor.Green, lightColor.Blue, 10),
                SKColors.Transparent
            },
            new float[] { 0f, 0.5f, 1.0f },
            SKShaderTileMode.Clamp);
        _spotPaint.Shader?.Dispose();
        _spotPaint.Shader = spotShader;
        canvas.DrawRect(bounds, _spotPaint);
        _spotPaint.Shader = null;
    }

    /// <summary>
    /// Hängelampe: Kabel von der Decke + Lampenschirm (Trapez) + Glühbirne mit pulsierendem Glow.
    /// </summary>
    private void DrawHangingLamp(SKCanvas canvas, SKRect bounds, SKColor lightColor, float time)
    {
        float lampX = bounds.MidX;
        float cableTop = bounds.Top + 2;
        float cableLen = bounds.Height * 0.12f;
        float lampY = cableTop + cableLen;

        // Glow-Puls: ~1.5Hz sanfte Intensitäts-Schwankung
        float glowPulse = 0.6f + 0.4f * MathF.Sin(time * 3f * MathF.PI);
        byte glowAlpha = (byte)(18 * glowPulse);

        // --- Glow hinter der Lampe (zuerst zeichnen, liegt dahinter) ---
        float glowRadius = bounds.Width * 0.25f * (0.85f + 0.15f * glowPulse);
        using var glowShader = SKShader.CreateRadialGradient(
            new SKPoint(lampX, lampY + 4),
            glowRadius,
            new[]
            {
                new SKColor(lightColor.Red, lightColor.Green, lightColor.Blue, glowAlpha),
                SKColors.Transparent
            },
            new float[] { 0f, 1.0f },
            SKShaderTileMode.Clamp);
        _glowPaint.Shader?.Dispose();
        _glowPaint.Shader = glowShader;
        canvas.DrawRect(bounds, _glowPaint);
        _glowPaint.Shader = null;

        // --- Kabel ---
        canvas.DrawLine(lampX, cableTop, lampX, lampY, _cablePaint);

        // --- Lampenschirm (Trapez: oben schmal, unten breit) ---
        float shadeW = 16;
        float shadeH = 6;
        float shadeTopW = 8;
        using var shadePath = new SKPath();
        shadePath.MoveTo(lampX - shadeTopW / 2, lampY);
        shadePath.LineTo(lampX + shadeTopW / 2, lampY);
        shadePath.LineTo(lampX + shadeW / 2, lampY + shadeH);
        shadePath.LineTo(lampX - shadeW / 2, lampY + shadeH);
        shadePath.Close();
        canvas.DrawPath(shadePath, _shadePaint);

        // Lampenschirm-Rand (subtiler Highlight oben)
        canvas.DrawLine(lampX - shadeTopW / 2, lampY, lampX + shadeTopW / 2, lampY, _shadeEdgePaint);

        // --- Glühbirne ---
        float bulbY = lampY + shadeH + 2;
        float bulbR = 2.5f;
        byte bulbAlpha = (byte)(60 + (int)(40 * glowPulse));
        _bulbPaint.Color = new SKColor(lightColor.Red, lightColor.Green, lightColor.Blue, bulbAlpha);
        canvas.DrawCircle(lampX, bulbY, bulbR, _bulbPaint);

        // Heller Kern der Glühbirne
        byte coreAlpha = (byte)(40 * glowPulse);
        _corePaint.Color = new SKColor(0xFF, 0xFF, 0xFF, coreAlpha);
        canvas.DrawCircle(lampX, bulbY, bulbR * 0.5f, _corePaint);

        // --- Lichtkegel unter der Lampe (kegelförmiger Lichtstrahl) ---
        float coneBottom = lampY + shadeH + bounds.Height * 0.25f;
        float coneBottomW = shadeW * 2.5f;
        byte coneAlpha = (byte)(10 * glowPulse);
        _conePaint.Color = new SKColor(lightColor.Red, lightColor.Green, lightColor.Blue, coneAlpha);
        using var conePath = new SKPath();
        conePath.MoveTo(lampX - shadeW / 2, lampY + shadeH);
        conePath.LineTo(lampX + shadeW / 2, lampY + shadeH);
        conePath.LineTo(lampX + coneBottomW / 2, coneBottom);
        conePath.LineTo(lampX - coneBottomW / 2, coneBottom);
        conePath.Close();
        canvas.DrawPath(conePath, _conePaint);
    }

    // =================================================================
    // Wand-Details (Phase 8) - sehr dezent, Alpha 15-25
    // =================================================================

    private void DrawWallDetails(SKCanvas canvas, SKRect bounds, WorkshopType type)
    {
        float wallBottom = bounds.Top + bounds.Height * 0.5f;

        // detailPaint zurücksetzen auf Standard-Werte
        _detailPaint.Style = SKPaintStyle.Stroke;
        _detailPaint.StrokeWidth = 1f;

        switch (type)
        {
            case WorkshopType.Carpenter:
                // Werkzeug-Silhouetten an der Wand (Säge + Hobel)
                _detailPaint.Color = new SKColor(0x8D, 0x6E, 0x63, 20);
                // Säge
                float sawX = bounds.Left + bounds.Width * 0.15f;
                float sawY = bounds.Top + bounds.Height * 0.18f;
                canvas.DrawRect(sawX, sawY, 20, 8, _detailPaint);
                canvas.DrawLine(sawX + 20, sawY + 4, sawX + 32, sawY + 4, _detailPaint);
                // Hobel
                float planeX = bounds.Left + bounds.Width * 0.7f;
                float planeY = bounds.Top + bounds.Height * 0.22f;
                canvas.DrawRoundRect(planeX, planeY, 22, 10, 2, 2, _detailPaint);
                break;

            case WorkshopType.Plumber:
                // Horizontale Rohr-Linien an der Wand
                _detailPaint.Color = new SKColor(0x78, 0x90, 0x9C, 22);
                _detailPaint.StrokeWidth = 2.5f;
                float pipeY1 = bounds.Top + bounds.Height * 0.15f;
                float pipeY2 = bounds.Top + bounds.Height * 0.35f;
                canvas.DrawLine(bounds.Left + 10, pipeY1, bounds.Right - 10, pipeY1, _detailPaint);
                canvas.DrawLine(bounds.Left + 30, pipeY2, bounds.Right - 30, pipeY2, _detailPaint);
                // Abgang
                _detailPaint.StrokeWidth = 2f;
                canvas.DrawLine(bounds.Right - 30, pipeY2, bounds.Right - 30, pipeY2 + 20, _detailPaint);
                break;

            case WorkshopType.Electrician:
                // Kabelkanal-Silhouette
                _detailPaint.Color = new SKColor(0x75, 0x75, 0x75, 20);
                _detailPaint.StrokeWidth = 2f;
                float cableY = bounds.Top + bounds.Height * 0.2f;
                canvas.DrawLine(bounds.Left + 15, cableY, bounds.Right - 15, cableY, _detailPaint);
                // Abgänge nach unten
                float abg1X = bounds.Left + bounds.Width * 0.3f;
                float abg2X = bounds.Left + bounds.Width * 0.7f;
                canvas.DrawLine(abg1X, cableY, abg1X, cableY + 25, _detailPaint);
                canvas.DrawLine(abg2X, cableY, abg2X, cableY + 18, _detailPaint);
                break;

            case WorkshopType.Painter:
                // Farbfelder an der Wand (3 kleine Quadrate)
                _detailPaint.Style = SKPaintStyle.Fill;
                float swatchSize = 10;
                float swatchY = bounds.Top + bounds.Height * 0.18f;
                float swatchStartX = bounds.Left + bounds.Width * 0.6f;
                _detailPaint.Color = new SKColor(0xEC, 0x48, 0x99, 18);
                canvas.DrawRect(swatchStartX, swatchY, swatchSize, swatchSize, _detailPaint);
                _detailPaint.Color = new SKColor(0x42, 0xA5, 0xF5, 18);
                canvas.DrawRect(swatchStartX + swatchSize + 4, swatchY, swatchSize, swatchSize, _detailPaint);
                _detailPaint.Color = new SKColor(0x66, 0xBB, 0x6A, 18);
                canvas.DrawRect(swatchStartX + (swatchSize + 4) * 2, swatchY, swatchSize, swatchSize, _detailPaint);
                break;

            case WorkshopType.Roofer:
                // Leiter-Silhouette an der Wand lehnend
                _detailPaint.Color = new SKColor(0x8D, 0x6E, 0x63, 18);
                _detailPaint.StrokeWidth = 1.5f;
                float ladderX = bounds.Left + bounds.Width * 0.12f;
                float ladderTop = bounds.Top + bounds.Height * 0.08f;
                float ladderBot = wallBottom;
                // Holme
                canvas.DrawLine(ladderX, ladderTop, ladderX + 6, ladderBot, _detailPaint);
                canvas.DrawLine(ladderX + 14, ladderTop, ladderX + 20, ladderBot, _detailPaint);
                // Sprossen
                float ladderH = ladderBot - ladderTop;
                for (int i = 1; i <= 4; i++)
                {
                    float frac = i / 5f;
                    float ly = ladderTop + ladderH * frac;
                    float lxOff = 6 * frac; // Leichte Neigung
                    canvas.DrawLine(ladderX + lxOff, ly, ladderX + 14 + lxOff, ly, _detailPaint);
                }
                break;

            case WorkshopType.Contractor:
                // Bauplan angepinnt (Rechteck mit Linien)
                _detailPaint.Color = new SKColor(0x90, 0x90, 0x90, 20);
                float planX = bounds.Left + bounds.Width * 0.7f;
                float planY = bounds.Top + bounds.Height * 0.12f;
                canvas.DrawRect(planX, planY, 24, 18, _detailPaint);
                // Linien im Plan
                _detailPaint.StrokeWidth = 0.5f;
                canvas.DrawLine(planX + 3, planY + 5, planX + 21, planY + 5, _detailPaint);
                canvas.DrawLine(planX + 3, planY + 9, planX + 18, planY + 9, _detailPaint);
                canvas.DrawLine(planX + 3, planY + 13, planX + 15, planY + 13, _detailPaint);
                break;

            case WorkshopType.Architect:
                // 2 Rahmen-Silhouetten (Diplom/Zertifikat)
                _detailPaint.Color = new SKColor(0xA0, 0xA0, 0xA0, 20);
                float frameY = bounds.Top + bounds.Height * 0.12f;
                float frame1X = bounds.Left + bounds.Width * 0.15f;
                float frame2X = bounds.Left + bounds.Width * 0.65f;
                canvas.DrawRect(frame1X, frameY, 20, 16, _detailPaint);
                canvas.DrawRect(frame2X, frameY, 20, 16, _detailPaint);
                // Innere Linien
                _detailPaint.StrokeWidth = 0.5f;
                canvas.DrawLine(frame1X + 4, frameY + 6, frame1X + 16, frameY + 6, _detailPaint);
                canvas.DrawLine(frame1X + 4, frameY + 10, frame1X + 12, frameY + 10, _detailPaint);
                canvas.DrawLine(frame2X + 4, frameY + 6, frame2X + 16, frameY + 6, _detailPaint);
                canvas.DrawLine(frame2X + 4, frameY + 10, frame2X + 12, frameY + 10, _detailPaint);
                break;

            case WorkshopType.GeneralContractor:
                // Fenster-Silhouette mit Stadt-Skyline
                _detailPaint.Color = new SKColor(0xA0, 0x90, 0x60, 20);
                float winX = bounds.Left + bounds.Width * 0.35f;
                float winY = bounds.Top + bounds.Height * 0.08f;
                float winW = bounds.Width * 0.3f;
                float winH = bounds.Height * 0.28f;
                // Fensterrahmen
                canvas.DrawRect(winX, winY, winW, winH, _detailPaint);
                // Kreuz im Fenster
                canvas.DrawLine(winX + winW / 2, winY, winX + winW / 2, winY + winH, _detailPaint);
                canvas.DrawLine(winX, winY + winH / 2, winX + winW, winY + winH / 2, _detailPaint);
                // Skyline (dezente Gebäude-Silhouetten im unteren Teil)
                _detailPaint.Style = SKPaintStyle.Fill;
                _detailPaint.Color = new SKColor(0x80, 0x70, 0x50, 15);
                float skyBase = winY + winH * 0.65f;
                float skyH1 = winH * 0.30f;
                float skyH2 = winH * 0.22f;
                float skyH3 = winH * 0.35f;
                float bw = winW / 8;
                canvas.DrawRect(winX + bw, skyBase - skyH1, bw * 1.2f, skyH1 + (winY + winH - skyBase), _detailPaint);
                canvas.DrawRect(winX + bw * 3, skyBase - skyH2, bw, skyH2 + (winY + winH - skyBase), _detailPaint);
                canvas.DrawRect(winX + bw * 4.5f, skyBase - skyH3, bw * 1.5f, skyH3 + (winY + winH - skyBase), _detailPaint);
                canvas.DrawRect(winX + bw * 6.5f, skyBase - skyH1 * 0.7f, bw, skyH1 * 0.7f + (winY + winH - skyBase), _detailPaint);
                break;

            case WorkshopType.MasterSmith:
                // Schmiedewerkzeuge an der Wand (Zange + Hammer + Amboss-Umriss)
                _detailPaint.Color = new SKColor(0x8D, 0x6E, 0x63, 22);
                // Hammer-Silhouette
                float hammerX = bounds.Left + bounds.Width * 0.15f;
                float hammerY = bounds.Top + bounds.Height * 0.12f;
                canvas.DrawRect(hammerX, hammerY, 14, 6, _detailPaint); // Kopf
                canvas.DrawRect(hammerX + 5, hammerY + 6, 4, 18, _detailPaint); // Stiel
                // Zange-Silhouette
                float tongX = bounds.Left + bounds.Width * 0.7f;
                float tongY = bounds.Top + bounds.Height * 0.1f;
                _detailPaint.StrokeWidth = 1.5f;
                canvas.DrawLine(tongX, tongY, tongX + 4, tongY + 22, _detailPaint);
                canvas.DrawLine(tongX + 8, tongY, tongX + 4, tongY + 22, _detailPaint);
                // Zangenmaul
                canvas.DrawLine(tongX - 2, tongY, tongX + 10, tongY, _detailPaint);
                // Amboss-Umriss (dezent, Mitte unten)
                float anvilWX = bounds.Left + bounds.Width * 0.4f;
                float anvilWY = bounds.Top + bounds.Height * 0.28f;
                _detailPaint.Color = new SKColor(0x70, 0x60, 0x50, 18);
                canvas.DrawRect(anvilWX, anvilWY + 4, 20, 6, _detailPaint); // Basis
                canvas.DrawRect(anvilWX + 3, anvilWY, 14, 5, _detailPaint); // Körper
                // Funken-Muster (kleine Punkte)
                _detailPaint.Style = SKPaintStyle.Fill;
                _detailPaint.Color = new SKColor(0xFF, 0x8F, 0x00, 15);
                canvas.DrawCircle(bounds.Left + bounds.Width * 0.3f, bounds.Top + bounds.Height * 0.2f, 2, _detailPaint);
                canvas.DrawCircle(bounds.Left + bounds.Width * 0.55f, bounds.Top + bounds.Height * 0.15f, 1.5f, _detailPaint);
                canvas.DrawCircle(bounds.Left + bounds.Width * 0.45f, bounds.Top + bounds.Height * 0.25f, 1.8f, _detailPaint);
                canvas.DrawCircle(bounds.Left + bounds.Width * 0.65f, bounds.Top + bounds.Height * 0.3f, 1.2f, _detailPaint);
                break;

            case WorkshopType.InnovationLab:
                // Reagenzgläser an der Wand (3 Stück)
                _detailPaint.Color = new SKColor(0x6A, 0x5A, 0xCD, 18);
                float tubeStartX = bounds.Left + bounds.Width * 0.12f;
                float tubeY = bounds.Top + bounds.Height * 0.1f;
                for (int i = 0; i < 3; i++)
                {
                    float tx = tubeStartX + i * 14;
                    // Röhrchen
                    canvas.DrawLine(tx, tubeY, tx, tubeY + 20, _detailPaint);
                    canvas.DrawLine(tx + 4, tubeY, tx + 4, tubeY + 20, _detailPaint);
                    // Abgerundeter Boden
                    canvas.DrawArc(new SKRect(tx - 0.5f, tubeY + 16, tx + 4.5f, tubeY + 22), 0, 180, false, _detailPaint);
                    // Flüssigkeitslevel (unterschiedlich hoch)
                    _detailPaint.Style = SKPaintStyle.Fill;
                    _detailPaint.Color = new SKColor(0x6A, 0x5A, 0xCD, (byte)(12 + i * 3));
                    float liquidTop = tubeY + 8 + i * 4;
                    canvas.DrawRect(tx + 0.5f, liquidTop, 3, tubeY + 19 - liquidTop, _detailPaint);
                    _detailPaint.Style = SKPaintStyle.Stroke;
                    _detailPaint.Color = new SKColor(0x6A, 0x5A, 0xCD, 18);
                }
                // Molekül-Struktur (rechts, dezent)
                float molX = bounds.Left + bounds.Width * 0.68f;
                float molY = bounds.Top + bounds.Height * 0.15f;
                _detailPaint.Color = new SKColor(0x80, 0x80, 0xA0, 20);
                _detailPaint.StrokeWidth = 1f;
                // Atome (Kreise)
                canvas.DrawCircle(molX, molY, 4, _detailPaint);
                canvas.DrawCircle(molX + 16, molY + 5, 3, _detailPaint);
                canvas.DrawCircle(molX + 8, molY + 18, 3.5f, _detailPaint);
                canvas.DrawCircle(molX - 8, molY + 14, 2.5f, _detailPaint);
                // Bindungen (Linien)
                canvas.DrawLine(molX + 4, molY + 1, molX + 13, molY + 3, _detailPaint);
                canvas.DrawLine(molX + 2, molY + 4, molX + 5, molY + 15, _detailPaint);
                canvas.DrawLine(molX + 14, molY + 8, molX + 10, molY + 15, _detailPaint);
                canvas.DrawLine(molX - 4, molY + 3, molX - 6, molY + 11, _detailPaint);
                // Zahnrad-Silhouette (klein, rechts unten)
                float gearSilX = bounds.Left + bounds.Width * 0.82f;
                float gearSilY = bounds.Top + bounds.Height * 0.28f;
                float gearSilR = 8;
                _detailPaint.Color = new SKColor(0x80, 0x80, 0x90, 16);
                canvas.DrawCircle(gearSilX, gearSilY, gearSilR, _detailPaint);
                canvas.DrawCircle(gearSilX, gearSilY, gearSilR * 0.4f, _detailPaint);
                for (int g = 0; g < 6; g++)
                {
                    float ga = g * MathF.PI / 3;
                    canvas.DrawLine(
                        gearSilX + MathF.Cos(ga) * gearSilR,
                        gearSilY + MathF.Sin(ga) * gearSilR,
                        gearSilX + MathF.Cos(ga) * (gearSilR + 3),
                        gearSilY + MathF.Sin(ga) * (gearSilR + 3),
                        _detailPaint);
                }
                break;
        }
    }

    // =================================================================
    // Boden-Pattern (Original)
    // =================================================================

    private void DrawSubtleFloorPattern(SKCanvas canvas, SKRect bounds, WorkshopType type, SKColor patternColor)
    {
        _linePaint.Color = patternColor;
        _linePaint.StrokeWidth = 0.5f;

        float patternTop = bounds.Top + bounds.Height * 0.75f;

        switch (type)
        {
            case WorkshopType.Carpenter:
                // Dezente Holzdielen-Linien
                for (float y = patternTop; y < bounds.Bottom; y += 14)
                    canvas.DrawLine(bounds.Left, y, bounds.Right, y, _linePaint);
                break;

            case WorkshopType.Plumber:
                // Dezentes Fliesen-Raster
                for (float y = patternTop; y < bounds.Bottom; y += 18)
                    canvas.DrawLine(bounds.Left, y, bounds.Right, y, _linePaint);
                for (float x = bounds.Left + 18; x < bounds.Right; x += 18)
                    canvas.DrawLine(x, patternTop, x, bounds.Bottom, _linePaint);
                break;

            case WorkshopType.Electrician:
                // Dezente Warnstreifen
                _linePaint.StrokeWidth = 2;
                _linePaint.Color = new SKColor(0xFB, 0xC0, 0x2D, 25);
                for (float x = bounds.Left; x < bounds.Right; x += 14)
                    canvas.DrawLine(x, bounds.Bottom - 4, x + 7, bounds.Bottom, _linePaint);
                break;

            case WorkshopType.Painter:
                // Dezente Farbspritzer (statisch gecacht, vermeidet Array-Allokation pro Frame)
                for (int i = 0; i < 6; i++)
                {
                    _splatPaint.Color = PainterSplatColors[i % PainterSplatColors.Length];
                    float sx = bounds.Left + 30 + (i * 47) % (bounds.Width - 60);
                    float sy = patternTop + 10 + (i * 13) % (bounds.Bottom - patternTop - 20);
                    canvas.DrawCircle(sx, sy, 4 + (i % 3) * 2, _splatPaint);
                }
                break;

            case WorkshopType.Roofer:
            case WorkshopType.Contractor:
                // Dezente horizontale Linien
                for (float y = patternTop; y < bounds.Bottom; y += 20)
                    canvas.DrawLine(bounds.Left, y, bounds.Right, y, _linePaint);
                break;

            case WorkshopType.Architect:
                // Dezente diagonale Linien (Marmor)
                _linePaint.Color = new SKColor(0xC0, 0xC0, 0xC0, 20);
                for (float d = bounds.Left - bounds.Height; d < bounds.Right; d += 35)
                    canvas.DrawLine(d, bounds.Bottom, d + bounds.Height, bounds.Top, _linePaint);
                break;

            case WorkshopType.GeneralContractor:
                // Dezentes Fischgrätmuster
                for (float y = patternTop; y < bounds.Bottom; y += 14)
                {
                    for (float x = bounds.Left; x < bounds.Right; x += 28)
                    {
                        float offset = ((int)(y / 14) % 2 == 0) ? 0 : 14;
                        canvas.DrawLine(x + offset, y, x + offset + 14, y, _linePaint);
                    }
                }
                break;

            case WorkshopType.MasterSmith:
                // Dezente Steinplatten (wie Schmiedeboden)
                _linePaint.Color = new SKColor(0x70, 0x58, 0x40, 30);
                for (float y = patternTop; y < bounds.Bottom; y += 16)
                {
                    canvas.DrawLine(bounds.Left, y, bounds.Right, y, _linePaint);
                    float rowOffset = ((int)(y / 16) % 2 == 0) ? 0 : 20;
                    for (float x = bounds.Left + rowOffset; x < bounds.Right; x += 40)
                        canvas.DrawLine(x, y, x, y + 16, _linePaint);
                }
                break;

            case WorkshopType.InnovationLab:
                // Dezentes Kachel-Raster (Labor-Fliesen, feiner als Plumber)
                _linePaint.Color = new SKColor(0xA0, 0xA0, 0xB8, 22);
                for (float y = patternTop; y < bounds.Bottom; y += 14)
                    canvas.DrawLine(bounds.Left, y, bounds.Right, y, _linePaint);
                for (float x = bounds.Left + 14; x < bounds.Right; x += 14)
                    canvas.DrawLine(x, patternTop, x, bounds.Bottom, _linePaint);
                break;
        }
    }
}
