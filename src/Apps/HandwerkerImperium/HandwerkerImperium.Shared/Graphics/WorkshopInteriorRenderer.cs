using SkiaSharp;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert den dezenten Hintergrund einer Werkstatt.
/// Boden-Gradient + Boden-Pattern + Vignette-Beleuchtung + Wand-Details.
/// Die eigentliche Szene wird vom WorkshopSceneRenderer darüber gezeichnet.
/// </summary>
public class WorkshopInteriorRenderer
{
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

    /// <summary>
    /// Rendert den dezenten Hintergrund eines Workshops.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, Workshop workshop)
    {
        var colors = WorkshopColors.GetValueOrDefault(workshop.Type,
            (new SKColor(0xD7, 0xCC, 0xB7), new SKColor(0xBC, 0xAA, 0x84), new SKColor(0xA6, 0x93, 0x72, 35)));

        // Vertikaler Gradient (oben heller → unten dunkler)
        using var gradientPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(bounds.Left, bounds.Top),
                new SKPoint(bounds.Left, bounds.Bottom),
                new[] { colors.Item1, colors.Item2 },
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(bounds, gradientPaint);

        // Dezente Wand-Details (obere 50%, sehr subtil)
        DrawWallDetails(canvas, bounds, workshop.Type);

        // Dezentes Boden-Pattern (nur untere 25%, sehr subtil)
        DrawSubtleFloorPattern(canvas, bounds, workshop.Type, colors.Item3);

        // Vignette-Beleuchtung: Fokus auf Mitte, Ränder dunkler
        DrawVignette(canvas, bounds);
    }

    // =================================================================
    // Vignette-Beleuchtung (Phase 6)
    // =================================================================

    private static void DrawVignette(SKCanvas canvas, SKRect bounds)
    {
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        // Radius = Diagonale * 0.7 → deckt Ecken ab
        float radius = MathF.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height) * 0.55f;

        using var vignettePaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy),
                radius,
                new[]
                {
                    SKColors.Transparent,           // Mitte: hell
                    SKColors.Transparent,           // Innerer Bereich: noch hell
                    new SKColor(0, 0, 0, 25),       // Übergang
                    new SKColor(0, 0, 0, 45),       // Ränder: dezent dunkler
                },
                new float[] { 0f, 0.4f, 0.75f, 1.0f },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(bounds, vignettePaint);
    }

    // =================================================================
    // Wand-Details (Phase 8) - sehr dezent, Alpha 15-25
    // =================================================================

    private static void DrawWallDetails(SKCanvas canvas, SKRect bounds, WorkshopType type)
    {
        float wallBottom = bounds.Top + bounds.Height * 0.5f;

        using var detailPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f
        };

        switch (type)
        {
            case WorkshopType.Carpenter:
                // Werkzeug-Silhouetten an der Wand (Säge + Hobel)
                detailPaint.Color = new SKColor(0x8D, 0x6E, 0x63, 20);
                // Säge
                float sawX = bounds.Left + bounds.Width * 0.15f;
                float sawY = bounds.Top + bounds.Height * 0.18f;
                canvas.DrawRect(sawX, sawY, 20, 8, detailPaint);
                canvas.DrawLine(sawX + 20, sawY + 4, sawX + 32, sawY + 4, detailPaint);
                // Hobel
                float planeX = bounds.Left + bounds.Width * 0.7f;
                float planeY = bounds.Top + bounds.Height * 0.22f;
                canvas.DrawRoundRect(planeX, planeY, 22, 10, 2, 2, detailPaint);
                break;

            case WorkshopType.Plumber:
                // Horizontale Rohr-Linien an der Wand
                detailPaint.Color = new SKColor(0x78, 0x90, 0x9C, 22);
                detailPaint.StrokeWidth = 2.5f;
                float pipeY1 = bounds.Top + bounds.Height * 0.15f;
                float pipeY2 = bounds.Top + bounds.Height * 0.35f;
                canvas.DrawLine(bounds.Left + 10, pipeY1, bounds.Right - 10, pipeY1, detailPaint);
                canvas.DrawLine(bounds.Left + 30, pipeY2, bounds.Right - 30, pipeY2, detailPaint);
                // Abgang
                detailPaint.StrokeWidth = 2f;
                canvas.DrawLine(bounds.Right - 30, pipeY2, bounds.Right - 30, pipeY2 + 20, detailPaint);
                break;

            case WorkshopType.Electrician:
                // Kabelkanal-Silhouette
                detailPaint.Color = new SKColor(0x75, 0x75, 0x75, 20);
                detailPaint.StrokeWidth = 2f;
                float cableY = bounds.Top + bounds.Height * 0.2f;
                canvas.DrawLine(bounds.Left + 15, cableY, bounds.Right - 15, cableY, detailPaint);
                // Abgänge nach unten
                float abg1X = bounds.Left + bounds.Width * 0.3f;
                float abg2X = bounds.Left + bounds.Width * 0.7f;
                canvas.DrawLine(abg1X, cableY, abg1X, cableY + 25, detailPaint);
                canvas.DrawLine(abg2X, cableY, abg2X, cableY + 18, detailPaint);
                break;

            case WorkshopType.Painter:
                // Farbfelder an der Wand (3 kleine Quadrate)
                detailPaint.Style = SKPaintStyle.Fill;
                float swatchSize = 10;
                float swatchY = bounds.Top + bounds.Height * 0.18f;
                float swatchStartX = bounds.Left + bounds.Width * 0.6f;
                detailPaint.Color = new SKColor(0xEC, 0x48, 0x99, 18);
                canvas.DrawRect(swatchStartX, swatchY, swatchSize, swatchSize, detailPaint);
                detailPaint.Color = new SKColor(0x42, 0xA5, 0xF5, 18);
                canvas.DrawRect(swatchStartX + swatchSize + 4, swatchY, swatchSize, swatchSize, detailPaint);
                detailPaint.Color = new SKColor(0x66, 0xBB, 0x6A, 18);
                canvas.DrawRect(swatchStartX + (swatchSize + 4) * 2, swatchY, swatchSize, swatchSize, detailPaint);
                break;

            case WorkshopType.Roofer:
                // Leiter-Silhouette an der Wand lehnend
                detailPaint.Color = new SKColor(0x8D, 0x6E, 0x63, 18);
                detailPaint.StrokeWidth = 1.5f;
                float ladderX = bounds.Left + bounds.Width * 0.12f;
                float ladderTop = bounds.Top + bounds.Height * 0.08f;
                float ladderBot = wallBottom;
                // Holme
                canvas.DrawLine(ladderX, ladderTop, ladderX + 6, ladderBot, detailPaint);
                canvas.DrawLine(ladderX + 14, ladderTop, ladderX + 20, ladderBot, detailPaint);
                // Sprossen
                float ladderH = ladderBot - ladderTop;
                for (int i = 1; i <= 4; i++)
                {
                    float frac = i / 5f;
                    float ly = ladderTop + ladderH * frac;
                    float lxOff = 6 * frac; // Leichte Neigung
                    canvas.DrawLine(ladderX + lxOff, ly, ladderX + 14 + lxOff, ly, detailPaint);
                }
                break;

            case WorkshopType.Contractor:
                // Bauplan angepinnt (Rechteck mit Linien)
                detailPaint.Color = new SKColor(0x90, 0x90, 0x90, 20);
                float planX = bounds.Left + bounds.Width * 0.7f;
                float planY = bounds.Top + bounds.Height * 0.12f;
                canvas.DrawRect(planX, planY, 24, 18, detailPaint);
                // Linien im Plan
                detailPaint.StrokeWidth = 0.5f;
                canvas.DrawLine(planX + 3, planY + 5, planX + 21, planY + 5, detailPaint);
                canvas.DrawLine(planX + 3, planY + 9, planX + 18, planY + 9, detailPaint);
                canvas.DrawLine(planX + 3, planY + 13, planX + 15, planY + 13, detailPaint);
                break;

            case WorkshopType.Architect:
                // 2 Rahmen-Silhouetten (Diplom/Zertifikat)
                detailPaint.Color = new SKColor(0xA0, 0xA0, 0xA0, 20);
                float frameY = bounds.Top + bounds.Height * 0.12f;
                float frame1X = bounds.Left + bounds.Width * 0.15f;
                float frame2X = bounds.Left + bounds.Width * 0.65f;
                canvas.DrawRect(frame1X, frameY, 20, 16, detailPaint);
                canvas.DrawRect(frame2X, frameY, 20, 16, detailPaint);
                // Innere Linien
                detailPaint.StrokeWidth = 0.5f;
                canvas.DrawLine(frame1X + 4, frameY + 6, frame1X + 16, frameY + 6, detailPaint);
                canvas.DrawLine(frame1X + 4, frameY + 10, frame1X + 12, frameY + 10, detailPaint);
                canvas.DrawLine(frame2X + 4, frameY + 6, frame2X + 16, frameY + 6, detailPaint);
                canvas.DrawLine(frame2X + 4, frameY + 10, frame2X + 12, frameY + 10, detailPaint);
                break;

            case WorkshopType.GeneralContractor:
                // Fenster-Silhouette mit Stadt-Skyline
                detailPaint.Color = new SKColor(0xA0, 0x90, 0x60, 20);
                float winX = bounds.Left + bounds.Width * 0.35f;
                float winY = bounds.Top + bounds.Height * 0.08f;
                float winW = bounds.Width * 0.3f;
                float winH = bounds.Height * 0.28f;
                // Fensterrahmen
                canvas.DrawRect(winX, winY, winW, winH, detailPaint);
                // Kreuz im Fenster
                canvas.DrawLine(winX + winW / 2, winY, winX + winW / 2, winY + winH, detailPaint);
                canvas.DrawLine(winX, winY + winH / 2, winX + winW, winY + winH / 2, detailPaint);
                // Skyline (dezente Gebäude-Silhouetten im unteren Teil)
                detailPaint.Style = SKPaintStyle.Fill;
                detailPaint.Color = new SKColor(0x80, 0x70, 0x50, 15);
                float skyBase = winY + winH * 0.65f;
                float skyH1 = winH * 0.30f;
                float skyH2 = winH * 0.22f;
                float skyH3 = winH * 0.35f;
                float bw = winW / 8;
                canvas.DrawRect(winX + bw, skyBase - skyH1, bw * 1.2f, skyH1 + (winY + winH - skyBase), detailPaint);
                canvas.DrawRect(winX + bw * 3, skyBase - skyH2, bw, skyH2 + (winY + winH - skyBase), detailPaint);
                canvas.DrawRect(winX + bw * 4.5f, skyBase - skyH3, bw * 1.5f, skyH3 + (winY + winH - skyBase), detailPaint);
                canvas.DrawRect(winX + bw * 6.5f, skyBase - skyH1 * 0.7f, bw, skyH1 * 0.7f + (winY + winH - skyBase), detailPaint);
                break;

            case WorkshopType.MasterSmith:
                // Schmiedewerkzeuge an der Wand (Zange + Hammer + Amboss-Umriss)
                detailPaint.Color = new SKColor(0x8D, 0x6E, 0x63, 22);
                // Hammer-Silhouette
                float hammerX = bounds.Left + bounds.Width * 0.15f;
                float hammerY = bounds.Top + bounds.Height * 0.12f;
                canvas.DrawRect(hammerX, hammerY, 14, 6, detailPaint); // Kopf
                canvas.DrawRect(hammerX + 5, hammerY + 6, 4, 18, detailPaint); // Stiel
                // Zange-Silhouette
                float tongX = bounds.Left + bounds.Width * 0.7f;
                float tongY = bounds.Top + bounds.Height * 0.1f;
                detailPaint.StrokeWidth = 1.5f;
                canvas.DrawLine(tongX, tongY, tongX + 4, tongY + 22, detailPaint);
                canvas.DrawLine(tongX + 8, tongY, tongX + 4, tongY + 22, detailPaint);
                // Zangenmaul
                canvas.DrawLine(tongX - 2, tongY, tongX + 10, tongY, detailPaint);
                // Amboss-Umriss (dezent, Mitte unten)
                float anvilWX = bounds.Left + bounds.Width * 0.4f;
                float anvilWY = bounds.Top + bounds.Height * 0.28f;
                detailPaint.Color = new SKColor(0x70, 0x60, 0x50, 18);
                canvas.DrawRect(anvilWX, anvilWY + 4, 20, 6, detailPaint); // Basis
                canvas.DrawRect(anvilWX + 3, anvilWY, 14, 5, detailPaint); // Körper
                // Funken-Muster (kleine Punkte)
                detailPaint.Style = SKPaintStyle.Fill;
                detailPaint.Color = new SKColor(0xFF, 0x8F, 0x00, 15);
                canvas.DrawCircle(bounds.Left + bounds.Width * 0.3f, bounds.Top + bounds.Height * 0.2f, 2, detailPaint);
                canvas.DrawCircle(bounds.Left + bounds.Width * 0.55f, bounds.Top + bounds.Height * 0.15f, 1.5f, detailPaint);
                canvas.DrawCircle(bounds.Left + bounds.Width * 0.45f, bounds.Top + bounds.Height * 0.25f, 1.8f, detailPaint);
                canvas.DrawCircle(bounds.Left + bounds.Width * 0.65f, bounds.Top + bounds.Height * 0.3f, 1.2f, detailPaint);
                break;

            case WorkshopType.InnovationLab:
                // Reagenzgläser an der Wand (3 Stück)
                detailPaint.Color = new SKColor(0x6A, 0x5A, 0xCD, 18);
                float tubeStartX = bounds.Left + bounds.Width * 0.12f;
                float tubeY = bounds.Top + bounds.Height * 0.1f;
                for (int i = 0; i < 3; i++)
                {
                    float tx = tubeStartX + i * 14;
                    // Röhrchen
                    canvas.DrawLine(tx, tubeY, tx, tubeY + 20, detailPaint);
                    canvas.DrawLine(tx + 4, tubeY, tx + 4, tubeY + 20, detailPaint);
                    // Abgerundeter Boden
                    canvas.DrawArc(new SKRect(tx - 0.5f, tubeY + 16, tx + 4.5f, tubeY + 22), 0, 180, false, detailPaint);
                    // Flüssigkeitslevel (unterschiedlich hoch)
                    detailPaint.Style = SKPaintStyle.Fill;
                    detailPaint.Color = new SKColor(0x6A, 0x5A, 0xCD, (byte)(12 + i * 3));
                    float liquidTop = tubeY + 8 + i * 4;
                    canvas.DrawRect(tx + 0.5f, liquidTop, 3, tubeY + 19 - liquidTop, detailPaint);
                    detailPaint.Style = SKPaintStyle.Stroke;
                    detailPaint.Color = new SKColor(0x6A, 0x5A, 0xCD, 18);
                }
                // Molekül-Struktur (rechts, dezent)
                float molX = bounds.Left + bounds.Width * 0.68f;
                float molY = bounds.Top + bounds.Height * 0.15f;
                detailPaint.Color = new SKColor(0x80, 0x80, 0xA0, 20);
                detailPaint.StrokeWidth = 1f;
                // Atome (Kreise)
                canvas.DrawCircle(molX, molY, 4, detailPaint);
                canvas.DrawCircle(molX + 16, molY + 5, 3, detailPaint);
                canvas.DrawCircle(molX + 8, molY + 18, 3.5f, detailPaint);
                canvas.DrawCircle(molX - 8, molY + 14, 2.5f, detailPaint);
                // Bindungen (Linien)
                canvas.DrawLine(molX + 4, molY + 1, molX + 13, molY + 3, detailPaint);
                canvas.DrawLine(molX + 2, molY + 4, molX + 5, molY + 15, detailPaint);
                canvas.DrawLine(molX + 14, molY + 8, molX + 10, molY + 15, detailPaint);
                canvas.DrawLine(molX - 4, molY + 3, molX - 6, molY + 11, detailPaint);
                // Zahnrad-Silhouette (klein, rechts unten)
                float gearSilX = bounds.Left + bounds.Width * 0.82f;
                float gearSilY = bounds.Top + bounds.Height * 0.28f;
                float gearSilR = 8;
                detailPaint.Color = new SKColor(0x80, 0x80, 0x90, 16);
                canvas.DrawCircle(gearSilX, gearSilY, gearSilR, detailPaint);
                canvas.DrawCircle(gearSilX, gearSilY, gearSilR * 0.4f, detailPaint);
                for (int g = 0; g < 6; g++)
                {
                    float ga = g * MathF.PI / 3;
                    canvas.DrawLine(
                        gearSilX + MathF.Cos(ga) * gearSilR,
                        gearSilY + MathF.Sin(ga) * gearSilR,
                        gearSilX + MathF.Cos(ga) * (gearSilR + 3),
                        gearSilY + MathF.Sin(ga) * (gearSilR + 3),
                        detailPaint);
                }
                break;
        }
    }

    // =================================================================
    // Boden-Pattern (Original)
    // =================================================================

    private static void DrawSubtleFloorPattern(SKCanvas canvas, SKRect bounds, WorkshopType type, SKColor patternColor)
    {
        using var linePaint = new SKPaint { IsAntialias = true, StrokeWidth = 0.5f, Color = patternColor };

        float patternTop = bounds.Top + bounds.Height * 0.75f;

        switch (type)
        {
            case WorkshopType.Carpenter:
                // Dezente Holzdielen-Linien
                for (float y = patternTop; y < bounds.Bottom; y += 14)
                    canvas.DrawLine(bounds.Left, y, bounds.Right, y, linePaint);
                break;

            case WorkshopType.Plumber:
                // Dezentes Fliesen-Raster
                for (float y = patternTop; y < bounds.Bottom; y += 18)
                    canvas.DrawLine(bounds.Left, y, bounds.Right, y, linePaint);
                for (float x = bounds.Left + 18; x < bounds.Right; x += 18)
                    canvas.DrawLine(x, patternTop, x, bounds.Bottom, linePaint);
                break;

            case WorkshopType.Electrician:
                // Dezente Warnstreifen
                linePaint.StrokeWidth = 2;
                linePaint.Color = new SKColor(0xFB, 0xC0, 0x2D, 25);
                for (float x = bounds.Left; x < bounds.Right; x += 14)
                    canvas.DrawLine(x, bounds.Bottom - 4, x + 7, bounds.Bottom, linePaint);
                break;

            case WorkshopType.Painter:
                // Dezente Farbspritzer
                using (var splatPaint = new SKPaint { IsAntialias = true })
                {
                    var splatColors = new[] {
                        new SKColor(0xEC, 0x48, 0x99, 20), new SKColor(0x42, 0xA5, 0xF5, 20),
                        new SKColor(0x66, 0xBB, 0x6A, 20), new SKColor(0xFF, 0xCA, 0x28, 20)
                    };
                    for (int i = 0; i < 6; i++)
                    {
                        splatPaint.Color = splatColors[i % splatColors.Length];
                        float sx = bounds.Left + 30 + (i * 47) % (bounds.Width - 60);
                        float sy = patternTop + 10 + (i * 13) % (bounds.Bottom - patternTop - 20);
                        canvas.DrawCircle(sx, sy, 4 + (i % 3) * 2, splatPaint);
                    }
                }
                break;

            case WorkshopType.Roofer:
            case WorkshopType.Contractor:
                // Dezente horizontale Linien
                for (float y = patternTop; y < bounds.Bottom; y += 20)
                    canvas.DrawLine(bounds.Left, y, bounds.Right, y, linePaint);
                break;

            case WorkshopType.Architect:
                // Dezente diagonale Linien (Marmor)
                linePaint.Color = new SKColor(0xC0, 0xC0, 0xC0, 20);
                for (float d = bounds.Left - bounds.Height; d < bounds.Right; d += 35)
                    canvas.DrawLine(d, bounds.Bottom, d + bounds.Height, bounds.Top, linePaint);
                break;

            case WorkshopType.GeneralContractor:
                // Dezentes Fischgrätmuster
                for (float y = patternTop; y < bounds.Bottom; y += 14)
                {
                    for (float x = bounds.Left; x < bounds.Right; x += 28)
                    {
                        float offset = ((int)(y / 14) % 2 == 0) ? 0 : 14;
                        canvas.DrawLine(x + offset, y, x + offset + 14, y, linePaint);
                    }
                }
                break;

            case WorkshopType.MasterSmith:
                // Dezente Steinplatten (wie Schmiedeboden)
                linePaint.Color = new SKColor(0x70, 0x58, 0x40, 30);
                for (float y = patternTop; y < bounds.Bottom; y += 16)
                {
                    canvas.DrawLine(bounds.Left, y, bounds.Right, y, linePaint);
                    float rowOffset = ((int)(y / 16) % 2 == 0) ? 0 : 20;
                    for (float x = bounds.Left + rowOffset; x < bounds.Right; x += 40)
                        canvas.DrawLine(x, y, x, y + 16, linePaint);
                }
                break;

            case WorkshopType.InnovationLab:
                // Dezentes Kachel-Raster (Labor-Fliesen, feiner als Plumber)
                linePaint.Color = new SKColor(0xA0, 0xA0, 0xB8, 22);
                for (float y = patternTop; y < bounds.Bottom; y += 14)
                    canvas.DrawLine(bounds.Left, y, bounds.Right, y, linePaint);
                for (float x = bounds.Left + 14; x < bounds.Right; x += 14)
                    canvas.DrawLine(x, patternTop, x, bounds.Bottom, linePaint);
                break;
        }
    }
}
