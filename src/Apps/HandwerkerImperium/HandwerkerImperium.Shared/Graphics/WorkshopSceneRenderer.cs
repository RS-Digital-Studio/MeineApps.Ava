using System;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using MeineApps.UI.SkiaSharp.Shaders;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert die animierten Workshop-Szenen mit großen, erkennbaren Elementen.
/// Jeder Workshop-Typ hat eine einzigartige, ikonische Szene.
/// IsAntialias = true für glatte Kanten auf allen Displays.
/// Features: Schatten, Tool-Glow, Worker-Accessoires, Level-Progression, Ambient-Effekte.
/// </summary>
public class WorkshopSceneRenderer
{
    // Gecachte Paints für GC-Optimierung (werden pro Frame wiederverwendet)
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Gecachte Shadow-Filter (3 Größen, wie in BomberBlast bewährt)
    private readonly SKMaskFilter _shadowSmall = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2);
    private readonly SKMaskFilter _shadowMedium = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4);
    private readonly SKMaskFilter _shadowLarge = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6);
    // Glow-Filter (gecacht statt pro Frame)
    private readonly SKMaskFilter _glowSmall = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3);
    private readonly SKMaskFilter _glowMedium = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5);

    /// <summary>
    /// Zeichnet die Workshop-Szene basierend auf Typ, Worker-Anzahl, Level und Animations-Phase.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, Workshop workshop,
        float phase, int activeWorkers, float speed, int particleRate, int productCount,
        Action<float, float, SKColor> addWorkParticle, Action<float, float> addCoinParticle)
    {
        // Nutzbare Fläche - volle Breite nutzen (kein Platz für Werkzeugwand verschwendet)
        float left = bounds.Left + 8;
        float right = bounds.Right - 8;
        float top = bounds.Top + 6;
        float bottom = bounds.Bottom - 6;
        float w = right - left;
        float h = bottom - top;

        float p = phase * speed;
        int level = workshop.Level;

        // Level-basierte Partikel-Dichte: Ab Level 100 +50%
        int effectiveParticleRate = level >= 100 ? (int)(particleRate * 1.5f) : particleRate;

        switch (workshop.Type)
        {
            case WorkshopType.Carpenter:
                DrawCarpenterScene(canvas, left, top, w, h, p, effectiveParticleRate, productCount, activeWorkers, level, workshop.Type, addWorkParticle, addCoinParticle);
                break;
            case WorkshopType.Plumber:
                DrawPlumberScene(canvas, left, top, w, h, p, effectiveParticleRate, activeWorkers, level, workshop.Type, addWorkParticle, addCoinParticle);
                break;
            case WorkshopType.Electrician:
                DrawElectricianScene(canvas, left, top, w, h, p, effectiveParticleRate, activeWorkers, level, workshop.Type, addWorkParticle, addCoinParticle);
                break;
            case WorkshopType.Painter:
                DrawPainterScene(canvas, left, top, w, h, p, effectiveParticleRate, activeWorkers, level, workshop.Type, addWorkParticle, addCoinParticle);
                break;
            case WorkshopType.Roofer:
                DrawRooferScene(canvas, left, top, w, h, p, effectiveParticleRate, activeWorkers, level, workshop.Type, addWorkParticle, addCoinParticle);
                break;
            case WorkshopType.Contractor:
                DrawContractorScene(canvas, left, top, w, h, p, effectiveParticleRate, activeWorkers, level, workshop.Type, addWorkParticle, addCoinParticle);
                break;
            case WorkshopType.Architect:
                DrawArchitectScene(canvas, left, top, w, h, p, effectiveParticleRate, activeWorkers, level, workshop.Type, addWorkParticle, addCoinParticle);
                break;
            case WorkshopType.GeneralContractor:
                DrawGeneralContractorScene(canvas, left, top, w, h, p, effectiveParticleRate, productCount, activeWorkers, level, workshop.Type, addWorkParticle, addCoinParticle);
                break;
            case WorkshopType.MasterSmith:
                DrawMasterSmithScene(canvas, left, top, w, h, p, effectiveParticleRate, activeWorkers, level, workshop.Type, addWorkParticle, addCoinParticle);
                break;
            case WorkshopType.InnovationLab:
                DrawInnovationLabScene(canvas, left, top, w, h, p, effectiveParticleRate, activeWorkers, level, workshop.Type, addWorkParticle, addCoinParticle);
                break;
        }

        // Level-basierte Overlay-Effekte (nach der Szene gezeichnet)
        DrawLevelEffects(canvas, bounds, level, phase);
    }

    /// <summary>
    /// Zeichnet den gedimmten Leerlauf-Zustand (0 Worker).
    /// Werkzeuge liegen still, dezentes Warnsymbol.
    /// </summary>
    public void RenderIdle(SKCanvas canvas, SKRect bounds, Workshop workshop)
    {
        float left = bounds.Left + 8;
        float right = bounds.Right - 8;
        float top = bounds.Top + 6;
        float bottom = bounds.Bottom - 6;
        float w = right - left;
        float h = bottom - top;
        float cx = left + w / 2;
        float cy = top + h / 2;

        // Gedimmte Szene: Stillliegende Werkzeuge je nach Typ
        canvas.Save();
        _fillPaint.Color = new SKColor(0x00, 0x00, 0x00, 60);
        canvas.DrawRect(bounds, _fillPaint);

        DrawIdleTools(canvas, left, top, w, h, workshop.Type);

        // Warnsymbol (⚠ Dreieck) zentral
        float triSize = 20;
        using var path = new SKPath();
        path.MoveTo(cx, cy - triSize * 0.6f);
        path.LineTo(cx - triSize * 0.5f, cy + triSize * 0.4f);
        path.LineTo(cx + triSize * 0.5f, cy + triSize * 0.4f);
        path.Close();

        // Dreieck-Hintergrund (gelb)
        _fillPaint.Color = new SKColor(0xFF, 0xC1, 0x07, 200);
        canvas.DrawPath(path, _fillPaint);

        // Dreieck-Rand
        _strokePaint.Color = new SKColor(0xF5, 0x7F, 0x17);
        _strokePaint.StrokeWidth = 2;
        canvas.DrawPath(path, _strokePaint);

        // Ausrufezeichen
        _fillPaint.Color = new SKColor(0x42, 0x42, 0x42);
        canvas.DrawRect(cx - 1.5f, cy - triSize * 0.3f, 3, triSize * 0.35f, _fillPaint);
        canvas.DrawCircle(cx, cy + triSize * 0.25f, 2, _fillPaint);

        canvas.Restore();
    }

    // ====================================================================
    // Hilfsmethoden
    // ====================================================================

    /// <summary>
    /// Zeichnet einen Drop-Shadow unter einem Rechteck.
    /// Wird VOR dem eigentlichen Element aufgerufen.
    /// </summary>
    private void DrawShadow(SKCanvas canvas, float x, float y, float w, float h, SKMaskFilter filter)
    {
        _fillPaint.Color = new SKColor(0x00, 0x00, 0x00, 45);
        _fillPaint.MaskFilter = filter;
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(x + 2, y + 3, x + w + 2, y + h + 3), 3), _fillPaint);
        _fillPaint.MaskFilter = null;
    }

    /// <summary>
    /// Zeichnet einen kreisförmigen Shadow (für runde Elemente).
    /// </summary>
    private void DrawCircleShadow(SKCanvas canvas, float cx, float cy, float radius, SKMaskFilter filter)
    {
        _fillPaint.Color = new SKColor(0x00, 0x00, 0x00, 40);
        _fillPaint.MaskFilter = filter;
        canvas.DrawCircle(cx + 1.5f, cy + 2, radius, _fillPaint);
        _fillPaint.MaskFilter = null;
    }

    /// <summary>
    /// Zeichnet einen Glow-Effekt um ein Element.
    /// </summary>
    private void DrawGlow(SKCanvas canvas, float cx, float cy, float radius, SKColor color, SKMaskFilter filter)
    {
        _glowPaint.Color = color;
        _glowPaint.MaskFilter = filter;
        canvas.DrawCircle(cx, cy, radius, _glowPaint);
        _glowPaint.MaskFilter = null;
    }

    /// <summary>
    /// Ping-Pong: Sanftes Hin-und-Her statt hartem Reset bei Modulo.
    /// Gibt Wert 0→1→0→1→0... zurück.
    /// </summary>
    private static float PingPong(float phase, float rate)
    {
        float t = (phase * rate) % 2.0f;
        return t > 1.0f ? 2.0f - t : t;
    }

    /// <summary>
    /// Zeichnet eine Worker-Figur mit Koerper, Kleidung, Gesicht und Workshop-spezifischen Accessoires.
    /// Ersetzt das alte Strichmaennchen durch eine vollwertige Figur mit Charakter.
    /// </summary>
    private void DrawStickFigure(SKCanvas canvas, float x, float y, float scale,
        SKColor headColor, float armAngle = 0, float legPhase = 0,
        WorkshopType type = WorkshopType.Carpenter)
    {
        // Hautfarbe
        var skinColor = new SKColor(0xFF, 0xDA, 0xB9);

        // Masse (30% groesser als altes Strichmaennchen)
        float headR = 6.5f * scale;
        float bodyLen = 14 * scale;
        float bodyW = 10 * scale;
        float armLen = 9 * scale;
        float legLen = 11 * scale;

        float headCY = y - bodyLen - headR;
        float torsoTop = y - bodyLen;
        float torsoBottom = y;

        // Workshop-spezifische Kleidungsfarbe
        SKColor clothColor = type switch
        {
            WorkshopType.Carpenter => new SKColor(0x8B, 0x69, 0x14),       // Braune Schuerze
            WorkshopType.Plumber => new SKColor(0x15, 0x65, 0xC0),         // Blaue Latzhose
            WorkshopType.Electrician => new SKColor(0xFD, 0xD8, 0x35),     // Gelbe Sicherheitsweste
            WorkshopType.Painter => new SKColor(0xE0, 0xE0, 0xE0),        // Weiss bespritzt
            WorkshopType.Roofer => new SKColor(0xE6, 0x51, 0x00),         // Orange Weste
            WorkshopType.Contractor => new SKColor(0x61, 0x61, 0x61),      // Graue Arbeitskleidung
            WorkshopType.Architect => new SKColor(0xF5, 0xF5, 0xF5),       // Weisses Hemd
            WorkshopType.GeneralContractor => new SKColor(0x21, 0x21, 0x21), // Schwarzer Anzug
            WorkshopType.MasterSmith => new SKColor(0x6D, 0x4C, 0x41),      // Braune Lederschürze
            WorkshopType.InnovationLab => new SKColor(0xE0, 0xE0, 0xF0),    // Weißer Laborkittel
            _ => new SKColor(0x78, 0x78, 0x78)
        };

        // --- Beine (hinter Koerper) ---
        float legSpread = MathF.Sin(legPhase) * 0.25f;
        byte bootColor_R = 0x3E, bootColor_G = 0x27, bootColor_B = 0x23;
        _strokePaint.Color = new SKColor(0x4A, 0x4A, 0x4A);
        _strokePaint.StrokeWidth = 4 * scale;
        _strokePaint.StrokeCap = SKStrokeCap.Round;

        // Linkes Bein
        float lLegEndX = x - legLen * MathF.Sin(0.25f + legSpread);
        float lLegEndY = y + legLen * MathF.Cos(0.25f + legSpread);
        canvas.DrawLine(x - 2 * scale, torsoBottom, lLegEndX, lLegEndY, _strokePaint);

        // Rechtes Bein
        float rLegEndX = x + legLen * MathF.Sin(0.25f - legSpread);
        float rLegEndY = y + legLen * MathF.Cos(0.25f - legSpread);
        canvas.DrawLine(x + 2 * scale, torsoBottom, rLegEndX, rLegEndY, _strokePaint);

        // Stiefel (kleine Rechtecke an Beinenden)
        _fillPaint.Color = new SKColor(bootColor_R, bootColor_G, bootColor_B);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(
            lLegEndX - 3 * scale, lLegEndY - 1 * scale,
            lLegEndX + 4 * scale, lLegEndY + 3 * scale), 1), _fillPaint);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(
            rLegEndX - 3 * scale, rLegEndY - 1 * scale,
            rLegEndX + 4 * scale, rLegEndY + 3 * scale), 1), _fillPaint);

        // --- Torso (Arbeitskleidung) ---
        _fillPaint.Color = clothColor;
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(
            x - bodyW / 2, torsoTop, x + bodyW / 2, torsoBottom), 2 * scale), _fillPaint);

        // Schatten am unteren Rand des Torsos
        _fillPaint.Color = clothColor.WithAlpha(200);
        canvas.DrawRect(x - bodyW / 2 + 1, torsoBottom - 3 * scale, bodyW - 2, 3 * scale, _fillPaint);

        // Kleidungs-Details je nach Typ
        switch (type)
        {
            case WorkshopType.Painter:
                // Farbflecken auf weisser Kleidung
                _fillPaint.Color = new SKColor(0xEC, 0x48, 0x99, 140);
                canvas.DrawCircle(x - 2 * scale, torsoTop + bodyLen * 0.3f, 2 * scale, _fillPaint);
                _fillPaint.Color = new SKColor(0x42, 0xA5, 0xF5, 140);
                canvas.DrawCircle(x + 3 * scale, torsoTop + bodyLen * 0.6f, 1.5f * scale, _fillPaint);
                _fillPaint.Color = new SKColor(0x66, 0xBB, 0x6A, 120);
                canvas.DrawCircle(x - 1 * scale, torsoTop + bodyLen * 0.7f, 1 * scale, _fillPaint);
                break;
            case WorkshopType.Electrician:
                // Reflektierende Streifen auf gelber Weste
                _fillPaint.Color = new SKColor(0xE0, 0xE0, 0xE0, 160);
                canvas.DrawRect(x - bodyW / 2 + 1, torsoTop + bodyLen * 0.35f, bodyW - 2, 2 * scale, _fillPaint);
                canvas.DrawRect(x - bodyW / 2 + 1, torsoTop + bodyLen * 0.55f, bodyW - 2, 2 * scale, _fillPaint);
                break;
            case WorkshopType.Plumber:
                // Latzhose Traeger
                _fillPaint.Color = new SKColor(0x0D, 0x47, 0xA1);
                canvas.DrawRect(x - 3 * scale, torsoTop, 2 * scale, bodyLen * 0.4f, _fillPaint);
                canvas.DrawRect(x + 1 * scale, torsoTop, 2 * scale, bodyLen * 0.4f, _fillPaint);
                break;
            case WorkshopType.GeneralContractor:
                // Krawatte
                _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
                float tieTop = torsoTop + 2 * scale;
                using (var tiePath = new SKPath())
                {
                    tiePath.MoveTo(x, tieTop);
                    tiePath.LineTo(x - 2.5f * scale, tieTop + 6 * scale);
                    tiePath.LineTo(x, tieTop + 10 * scale);
                    tiePath.LineTo(x + 2.5f * scale, tieTop + 6 * scale);
                    tiePath.Close();
                    canvas.DrawPath(tiePath, _fillPaint);
                }
                break;
        }

        // --- Arme (Hautfarbe, animiert) ---
        _strokePaint.Color = skinColor;
        _strokePaint.StrokeWidth = 4 * scale;
        _strokePaint.StrokeCap = SKStrokeCap.Round;
        float armY = torsoTop + bodyLen * 0.2f;
        float aL = MathF.Sin(armAngle) * 0.3f;
        canvas.DrawLine(x - bodyW / 2, armY,
            x - bodyW / 2 - armLen * MathF.Cos(0.6f + aL), armY + armLen * MathF.Sin(0.6f + aL), _strokePaint);
        canvas.DrawLine(x + bodyW / 2, armY,
            x + bodyW / 2 + armLen * MathF.Cos(0.6f - aL), armY + armLen * MathF.Sin(0.6f - aL), _strokePaint);

        // --- Kopf (ovaler Kreis, Hautfarbe) ---
        _fillPaint.Color = skinColor;
        canvas.DrawOval(x, headCY, headR, headR * 1.1f, _fillPaint);

        // Gesicht: Augen
        _fillPaint.Color = new SKColor(0x33, 0x33, 0x33);
        canvas.DrawCircle(x - 2.5f * scale, headCY - 0.5f * scale, 1.2f * scale, _fillPaint);
        canvas.DrawCircle(x + 2.5f * scale, headCY - 0.5f * scale, 1.2f * scale, _fillPaint);

        // Gesicht: Mund (kleiner Strich)
        _strokePaint.Color = new SKColor(0x66, 0x33, 0x33);
        _strokePaint.StrokeWidth = 1 * scale;
        canvas.DrawLine(x - 1.5f * scale, headCY + 2.5f * scale,
            x + 1.5f * scale, headCY + 2.5f * scale, _strokePaint);

        // --- Workshop-spezifische Accessoires (30% groesser) ---
        float accScale = scale * 1.3f;
        switch (type)
        {
            case WorkshopType.Carpenter:
                // Gelber Schutzhelm (vergroessert)
                _fillPaint.Color = new SKColor(0xFF, 0xC1, 0x07);
                canvas.DrawArc(new SKRect(x - headR - 2 * accScale, headCY - headR - 3 * accScale,
                    x + headR + 2 * accScale, headCY + 2), 180, 180, true, _fillPaint);
                // Helmrand
                _fillPaint.Color = new SKColor(0xF5, 0x9E, 0x0B);
                canvas.DrawRect(x - headR - 3 * accScale, headCY - 1, (headR + 3 * accScale) * 2, 2.5f * accScale, _fillPaint);
                // Helm-Visier (weisser Streifen)
                _fillPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 60);
                canvas.DrawRect(x - headR, headCY - headR - 1, headR * 2, 2 * accScale, _fillPaint);
                break;
            case WorkshopType.Plumber:
                // Blaue Kappe (vergroessert)
                _fillPaint.Color = new SKColor(0x15, 0x65, 0xC0);
                canvas.DrawArc(new SKRect(x - headR - 1, headCY - headR - 2 * accScale,
                    x + headR + 1, headCY + 1), 180, 180, true, _fillPaint);
                // Schirm
                _fillPaint.Color = new SKColor(0x0D, 0x47, 0xA1);
                canvas.DrawRect(x - headR - 3 * accScale, headCY - 1, headR + 3 * accScale, 2.5f * accScale, _fillPaint);
                break;
            case WorkshopType.Electrician:
                // Weisser Schutzhelm
                _fillPaint.Color = new SKColor(0xF5, 0xF5, 0xF5);
                canvas.DrawArc(new SKRect(x - headR - 2 * accScale, headCY - headR - 3 * accScale,
                    x + headR + 2 * accScale, headCY + 2), 180, 180, true, _fillPaint);
                _fillPaint.Color = new SKColor(0xE0, 0xE0, 0xE0);
                canvas.DrawRect(x - headR - 3 * accScale, headCY - 1, (headR + 3 * accScale) * 2, 2.5f * accScale, _fillPaint);
                break;
            case WorkshopType.Painter:
                // Beret (vergroessert)
                _fillPaint.Color = new SKColor(0xEC, 0x48, 0x99);
                using (var beretPath = new SKPath())
                {
                    beretPath.MoveTo(x - headR * 1.1f, headCY - headR * 0.5f);
                    beretPath.LineTo(x + headR * 0.5f, headCY - headR * 2.2f);
                    beretPath.LineTo(x + headR * 1.1f, headCY - headR * 0.5f);
                    beretPath.Close();
                    canvas.DrawPath(beretPath, _fillPaint);
                }
                // Beret-Pompon
                _fillPaint.Color = new SKColor(0xD6, 0x33, 0x84);
                canvas.DrawCircle(x + headR * 0.5f, headCY - headR * 2.2f, 2 * accScale, _fillPaint);
                break;
            case WorkshopType.Roofer:
                // Roter Helm (vergroessert)
                _fillPaint.Color = new SKColor(0xDC, 0x26, 0x26);
                canvas.DrawArc(new SKRect(x - headR - 2 * accScale, headCY - headR - 3 * accScale,
                    x + headR + 2 * accScale, headCY + 2), 180, 180, true, _fillPaint);
                _fillPaint.Color = new SKColor(0xBF, 0x20, 0x20);
                canvas.DrawRect(x - headR - 3 * accScale, headCY - 1, (headR + 3 * accScale) * 2, 2.5f * accScale, _fillPaint);
                break;
            case WorkshopType.Contractor:
                // Oranger Helm (vergroessert)
                _fillPaint.Color = new SKColor(0xEA, 0x58, 0x0C);
                canvas.DrawArc(new SKRect(x - headR - 2 * accScale, headCY - headR - 3 * accScale,
                    x + headR + 2 * accScale, headCY + 2), 180, 180, true, _fillPaint);
                _fillPaint.Color = new SKColor(0xD2, 0x4B, 0x0A);
                canvas.DrawRect(x - headR - 3 * accScale, headCY - 1, (headR + 3 * accScale) * 2, 2.5f * accScale, _fillPaint);
                // Helm-Visier
                _fillPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 50);
                canvas.DrawRect(x - headR, headCY - headR - 1, headR * 2, 2 * accScale, _fillPaint);
                break;
            case WorkshopType.Architect:
                // Brille (vergroessert, mit Glasglanz)
                _strokePaint.Color = new SKColor(0x33, 0x33, 0x33);
                _strokePaint.StrokeWidth = 1.8f * accScale;
                float glassR = 3.2f * accScale;
                canvas.DrawCircle(x - 3 * scale, headCY, glassR, _strokePaint);
                canvas.DrawCircle(x + 3 * scale, headCY, glassR, _strokePaint);
                canvas.DrawLine(x - 3 * scale + glassR, headCY, x + 3 * scale - glassR, headCY, _strokePaint);
                // Glasglanz
                _fillPaint.Color = new SKColor(0xBB, 0xDE, 0xFB, 60);
                canvas.DrawCircle(x - 3 * scale - 1, headCY - 1, glassR * 0.5f, _fillPaint);
                canvas.DrawCircle(x + 3 * scale - 1, headCY - 1, glassR * 0.5f, _fillPaint);
                break;
            case WorkshopType.GeneralContractor:
                // Kurze gepflegte Frisur (kein Helm)
                _fillPaint.Color = new SKColor(0x33, 0x33, 0x33);
                canvas.DrawArc(new SKRect(x - headR * 0.9f, headCY - headR * 1.3f,
                    x + headR * 0.9f, headCY - headR * 0.2f), 180, 180, true, _fillPaint);
                break;
            case WorkshopType.MasterSmith:
                // Schmied-Kappe (braune Lederkappe + Schweißband)
                _fillPaint.Color = new SKColor(0x6D, 0x4C, 0x41);
                canvas.DrawArc(new SKRect(x - headR - 1, headCY - headR - 2 * accScale,
                    x + headR + 1, headCY + 1), 180, 180, true, _fillPaint);
                // Schweißband (kupferfarben)
                _fillPaint.Color = new SKColor(0xD4, 0xA3, 0x73);
                canvas.DrawRect(x - headR - 2 * accScale, headCY - 1, (headR + 2 * accScale) * 2, 2 * accScale, _fillPaint);
                break;
            case WorkshopType.InnovationLab:
                // Schutzbrille (violett getönt)
                _strokePaint.Color = new SKColor(0x6A, 0x5A, 0xCD);
                _strokePaint.StrokeWidth = 1.8f * accScale;
                float labGlassR = 3.5f * accScale;
                canvas.DrawCircle(x - 3 * scale, headCY, labGlassR, _strokePaint);
                canvas.DrawCircle(x + 3 * scale, headCY, labGlassR, _strokePaint);
                canvas.DrawLine(x - 3 * scale + labGlassR, headCY, x + 3 * scale - labGlassR, headCY, _strokePaint);
                // Violetter Glasglanz
                _fillPaint.Color = new SKColor(0x9C, 0x8A, 0xE7, 50);
                canvas.DrawCircle(x - 3 * scale - 1, headCY - 1, labGlassR * 0.5f, _fillPaint);
                canvas.DrawCircle(x + 3 * scale - 1, headCY - 1, labGlassR * 0.5f, _fillPaint);
                break;
        }
    }

    /// <summary>
    /// Münz-Emission: Level-skaliert. Mehr Münzen bei höheren Leveln.
    /// </summary>
    private static void TryEmitCoin(float phase, int activeWorkers, int level,
        float x, float y, Action<float, float> addCoinParticle)
    {
        float interval = activeWorkers switch
        {
            1 => 5.0f,
            2 or 3 => 3.0f,
            _ => 2.0f
        };
        float cyclePos = (phase * 0.5f) % interval;
        if (cyclePos < 0.08f)
        {
            addCoinParticle(x, y);
            // Ab Level 250: 2 Münzen
            if (level >= 250) addCoinParticle(x - 5, y + 2);
            // Ab Level 500: 3 Münzen
            if (level >= 500) addCoinParticle(x + 5, y - 2);
        }
    }

    /// <summary>
    /// Level-basierte Overlay-Effekte (nach der Szene gezeichnet).
    /// </summary>
    private void DrawLevelEffects(SKCanvas canvas, SKRect bounds, int level, float phase)
    {
        if (level < 50) return;

        // Level 250+: Pulsierende goldene Sterne in den Ecken (groesser, heller)
        if (level >= 250)
        {
            float starPulse = 0.5f + MathF.Sin(phase * 2.5f) * 0.4f;
            float starSize = 5 + starPulse * 3;
            // 4 Sterne in allen Ecken
            DrawMiniStar(canvas, bounds.Left + 14, bounds.Top + 12, starSize, phase);
            DrawMiniStar(canvas, bounds.Right - 14, bounds.Top + 12, starSize, phase * 1.3f);
            DrawMiniStar(canvas, bounds.Left + 14, bounds.Bottom - 12, starSize, phase * 0.7f);
            if (level >= 500)
                DrawMiniStar(canvas, bounds.Right - 14, bounds.Bottom - 12, starSize, phase * 0.8f);
        }

        // Level 500+: Premium-Gold-Aura (GPU-Shader, staerker sichtbar)
        if (level >= 500)
        {
            SkiaGlowEffect.DrawEdgeGlow(canvas, bounds, phase,
                new SKColor(0xFF, 0xD7, 0x00, 80),
                pulseSpeed: 1.5f, pulseMin: 0.02f, pulseMax: 0.07f);
        }

        // Level 1000: Gold-Shimmer über gesamte Szene
        if (level >= 1000)
        {
            SkiaShimmerEffect.DrawGoldShimmer(canvas, bounds, phase);
        }
    }

    /// <summary>
    /// Zeichnet einen kleinen pulsierenden Stern mit 8 Strahlen und hellem Kern.
    /// </summary>
    private void DrawMiniStar(SKCanvas canvas, float cx, float cy, float size, float phase)
    {
        // Pulsierender Kern-Glow
        float kernPulse = 0.6f + MathF.Sin(phase * 3f) * 0.4f;
        _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00, (byte)(kernPulse * 100));
        canvas.DrawCircle(cx, cy, size * 0.5f, _fillPaint);

        // 8 Strahlen statt 4
        _strokePaint.Color = new SKColor(0xFF, 0xD7, 0x00, 180);
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.StrokeCap = SKStrokeCap.Round;
        for (int i = 0; i < 8; i++)
        {
            float a = phase * 0.5f + i * MathF.PI / 4;
            float rayLen = (i % 2 == 0) ? size : size * 0.6f; // Abwechselnd lang/kurz
            canvas.DrawLine(cx, cy,
                cx + MathF.Cos(a) * rayLen, cy + MathF.Sin(a) * rayLen, _strokePaint);
        }
    }

    /// <summary>
    /// Stillliegende Werkzeuge für den Leerlauf-Zustand.
    /// </summary>
    private void DrawIdleTools(SKCanvas canvas, float left, float top, float w, float h, WorkshopType type)
    {
        float cx = left + w / 2;
        float bottom = top + h;

        _fillPaint.Color = new SKColor(0x90, 0x90, 0x90, 100);
        _strokePaint.Color = new SKColor(0x90, 0x90, 0x90, 100);
        _strokePaint.StrokeWidth = 2;

        switch (type)
        {
            case WorkshopType.Carpenter:
                // Säge liegt am Boden
                canvas.DrawRect(cx - 30, bottom - 20, 60, 4, _fillPaint);
                _fillPaint.Color = new SKColor(0x6D, 0x4C, 0x41, 100);
                canvas.DrawRect(cx + 30, bottom - 26, 10, 12, _fillPaint);
                break;
            case WorkshopType.Plumber:
                // Rohrzange liegt am Boden
                canvas.DrawLine(cx - 10, bottom - 20, cx + 10, bottom - 10, _strokePaint);
                canvas.DrawLine(cx - 10, bottom - 10, cx + 10, bottom - 20, _strokePaint);
                break;
            case WorkshopType.Electrician:
                // Schraubendreher liegt am Boden
                _fillPaint.Color = new SKColor(0xF9, 0x73, 0x16, 100);
                canvas.DrawRect(cx - 20, bottom - 16, 14, 6, _fillPaint);
                _fillPaint.Color = new SKColor(0x90, 0x90, 0x90, 100);
                canvas.DrawRect(cx - 6, bottom - 14, 24, 2, _fillPaint);
                break;
            case WorkshopType.Painter:
                // Farbeimer umgekippt
                _fillPaint.Color = new SKColor(0x78, 0x78, 0x78, 100);
                canvas.DrawRect(cx - 10, bottom - 18, 16, 14, _fillPaint);
                _fillPaint.Color = new SKColor(0xEC, 0x48, 0x99, 60);
                canvas.DrawCircle(cx + 12, bottom - 8, 8, _fillPaint);
                break;
            case WorkshopType.Roofer:
                // Hammer liegt am Boden
                _fillPaint.Color = new SKColor(0x78, 0x78, 0x78, 100);
                canvas.DrawRect(cx - 6, bottom - 18, 12, 6, _fillPaint);
                _fillPaint.Color = new SKColor(0x6D, 0x4C, 0x41, 100);
                canvas.DrawRect(cx - 2, bottom - 12, 4, 16, _fillPaint);
                break;
            case WorkshopType.Contractor:
                // Kelle liegt am Boden
                _fillPaint.Color = new SKColor(0x78, 0x78, 0x78, 100);
                canvas.DrawRect(cx - 15, bottom - 16, 20, 6, _fillPaint);
                _fillPaint.Color = new SKColor(0x6D, 0x4C, 0x41, 100);
                canvas.DrawRect(cx + 5, bottom - 20, 4, 10, _fillPaint);
                break;
            case WorkshopType.Architect:
                // Bleistift liegt am Boden
                _fillPaint.Color = new SKColor(0xFF, 0xCA, 0x28, 100);
                canvas.DrawRect(cx - 20, bottom - 14, 40, 4, _fillPaint);
                break;
            case WorkshopType.GeneralContractor:
                // Stempel liegt am Boden
                _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00, 100);
                canvas.DrawRect(cx - 8, bottom - 20, 6, 14, _fillPaint);
                canvas.DrawRect(cx - 12, bottom - 6, 14, 4, _fillPaint);
                break;
            case WorkshopType.MasterSmith:
                // Schmiedehammer liegt am Boden
                _fillPaint.Color = new SKColor(0x90, 0x90, 0x90, 100);
                canvas.DrawRect(cx - 8, bottom - 18, 16, 6, _fillPaint);
                _fillPaint.Color = new SKColor(0x6D, 0x4C, 0x41, 100);
                canvas.DrawRect(cx + 2, bottom - 14, 4, 18, _fillPaint);
                break;
            case WorkshopType.InnovationLab:
                // Reagenzglas liegt am Boden
                _fillPaint.Color = new SKColor(0x6A, 0x5A, 0xCD, 80);
                canvas.DrawRect(cx - 15, bottom - 14, 30, 4, _fillPaint);
                _fillPaint.Color = new SKColor(0x90, 0x90, 0x90, 100);
                canvas.DrawRect(cx + 15, bottom - 18, 6, 8, _fillPaint);
                break;
        }
    }

    // ====================================================================
    // Schreiner: Kreissäge schneidet Brett
    // ====================================================================
    private void DrawCarpenterScene(SKCanvas canvas, float left, float top, float w, float h,
        float phase, int particleRate, int productCount, int activeWorkers, int level, WorkshopType type,
        Action<float, float, SKColor> addWorkParticle, Action<float, float> addCoinParticle)
    {
        float cx = left + w * 0.45f;
        float cy = top + h * 0.48f;

        // --- Arbeitstisch (großer Holztisch) ---
        float tableW = w * 0.65f;
        float tableH = 14;
        float tableX = left + (w - tableW) / 2;
        float tableY = cy - tableH / 2;

        // Schatten unter Tisch
        DrawShadow(canvas, tableX, tableY, tableW, tableH + h * 0.28f, _shadowMedium);

        // Tischbeine
        _fillPaint.Color = new SKColor(0x6D, 0x4C, 0x41);
        canvas.DrawRect(tableX + 8, tableY + tableH, 6, h * 0.28f, _fillPaint);
        canvas.DrawRect(tableX + tableW - 14, tableY + tableH, 6, h * 0.28f, _fillPaint);

        // Tischplatte
        _fillPaint.Color = new SKColor(0x8D, 0x6E, 0x63);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(tableX, tableY, tableX + tableW, tableY + tableH), 3), _fillPaint);
        // Holz-Highlight
        _fillPaint.Color = new SKColor(0xA1, 0x88, 0x7F);
        canvas.DrawRect(tableX + 2, tableY + 1, tableW - 4, 3, _fillPaint);

        // --- Holzbrett wandert von links durch Säge ---
        float boardProgress = PingPong(phase, 0.4f);
        float boardW = w * 0.55f;
        float boardH = 10;
        float boardX = tableX - boardW * 0.3f + boardProgress * tableW * 0.5f;
        float boardY = tableY + (tableH - boardH) / 2;

        _fillPaint.Color = new SKColor(0xBC, 0x8A, 0x5F);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(boardX, boardY, boardX + boardW, boardY + boardH), 2), _fillPaint);
        // Maserung (mehr Linien fuer realistischeren Look)
        _strokePaint.Color = new SKColor(0xA0, 0x72, 0x3C, 100);
        _strokePaint.StrokeWidth = 0.8f;
        for (int m = 0; m < 5; m++)
        {
            float my = boardY + 1.5f + m * 2;
            canvas.DrawLine(boardX + 4, my, boardX + boardW - 4, my, _strokePaint);
        }
        // Astloecher (2 kleine dunkle Ovale)
        _fillPaint.Color = new SKColor(0x7A, 0x52, 0x2A, 140);
        canvas.DrawOval(boardX + boardW * 0.25f, boardY + boardH * 0.5f, 3, 2, _fillPaint);
        canvas.DrawOval(boardX + boardW * 0.7f, boardY + boardH * 0.4f, 2.5f, 1.8f, _fillPaint);

        // --- Kreissäge (rotierendes Sägeblatt) ---
        float sawX = tableX + tableW * 0.5f;
        float sawY = tableY + tableH / 2;
        float sawRadius = 18;
        float sawAngle = phase * 12;

        // Sägeblatt-Glow (Licht-Reflexion beim Rotieren)
        DrawGlow(canvas, sawX, sawY, sawRadius + 6, new SKColor(0xFF, 0xFF, 0xFF, 30), _glowMedium);

        // Sägeblatt-Scheibe
        _fillPaint.Color = new SKColor(0xC0, 0xC0, 0xC0);
        canvas.DrawCircle(sawX, sawY, sawRadius, _fillPaint);
        // Innerer Ring
        _fillPaint.Color = new SKColor(0x90, 0x90, 0x90);
        canvas.DrawCircle(sawX, sawY, sawRadius * 0.6f, _fillPaint);
        // Metallglanz-Streifen (diagonaler Highlight auf dem Blatt)
        _fillPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 50);
        canvas.Save();
        canvas.ClipRect(new SKRect(sawX - sawRadius, sawY - sawRadius, sawX + sawRadius, sawY + sawRadius));
        canvas.DrawRect(sawX - sawRadius * 0.3f, sawY - sawRadius, sawRadius * 0.4f, sawRadius * 2, _fillPaint);
        canvas.Restore();

        // Mittelachse
        _fillPaint.Color = new SKColor(0x60, 0x60, 0x60);
        canvas.DrawCircle(sawX, sawY, 4, _fillPaint);
        // Achsen-Glanz
        _fillPaint.Color = new SKColor(0x90, 0x90, 0x90);
        canvas.DrawCircle(sawX - 1, sawY - 1, 2, _fillPaint);

        // Sägezähne (8 Zacken, rotierend)
        _strokePaint.Color = new SKColor(0x78, 0x78, 0x78);
        _strokePaint.StrokeWidth = 2;
        for (int t = 0; t < 8; t++)
        {
            float a = sawAngle + t * MathF.Tau / 8;
            float outerR = sawRadius + 3;
            canvas.DrawLine(
                sawX + MathF.Cos(a) * sawRadius * 0.8f,
                sawY + MathF.Sin(a) * sawRadius * 0.8f,
                sawX + MathF.Cos(a + 0.15f) * outerR,
                sawY + MathF.Sin(a + 0.15f) * outerR,
                _strokePaint);
        }

        // Schnittlinie im Brett
        _strokePaint.Color = new SKColor(0x4E, 0x34, 0x2E);
        _strokePaint.StrokeWidth = 2;
        float cutX = Math.Min(sawX, boardX + boardW);
        canvas.DrawLine(boardX, boardY + boardH / 2, cutX, boardY + boardH / 2, _strokePaint);

        // --- Sägespäne-Partikel (mehr, goldener) ---
        float emitPhase = (phase * 4) % MathF.Tau;
        if (emitPhase > MathF.PI - 0.5f && emitPhase < MathF.PI + 0.5f)
        {
            for (int p = 0; p < particleRate + 3; p++)
                addWorkParticle(sawX, tableY + tableH + 2, new SKColor(0xDA, 0xC0, 0x8F));
        }

        // --- Fertige Bretter rechts ---
        float stackX = left + w * 0.82f;
        for (int s = 0; s < productCount; s++)
        {
            _fillPaint.Color = (s % 2 == 0) ? new SKColor(0xA0, 0x72, 0x3C, 200) : new SKColor(0xBC, 0x8A, 0x5F, 200);
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(stackX, top + h * 0.7f - s * 8, stackX + 30, top + h * 0.7f - s * 8 + 6), 1), _fillPaint);
        }

        // --- Worker-Figur links ---
        DrawStickFigure(canvas, tableX - 12, tableY + tableH + 20, 0.9f,
            new SKColor(0xA0, 0x52, 0x2D), phase * 2, phase * 3, type);

        // --- 4+ Worker: Zweiter Arbeiter rechts sortiert Bretter ---
        if (activeWorkers >= 4)
        {
            DrawStickFigure(canvas, stackX + 15, top + h * 0.7f + 20, 0.8f,
                new SKColor(0x8D, 0x65, 0x34), phase * 1.5f, phase * 2, type);
        }

        // Münz-Emission
        TryEmitCoin(phase, activeWorkers, level, sawX, tableY - 10, addCoinParticle);
    }

    // ====================================================================
    // Klempner: Waschbecken-Installation
    // ====================================================================
    private void DrawPlumberScene(SKCanvas canvas, float left, float top, float w, float h,
        float phase, int particleRate, int activeWorkers, int level, WorkshopType type,
        Action<float, float, SKColor> addWorkParticle, Action<float, float> addCoinParticle)
    {
        float cx = left + w * 0.42f;

        // --- Großes Waschbecken ---
        float sinkW = 70;
        float sinkH = 30;
        float sinkX = cx - sinkW / 2;
        float sinkY = top + h * 0.2f;

        // Schatten unter Waschbecken
        DrawShadow(canvas, sinkX, sinkY, sinkW, sinkH, _shadowMedium);

        // Becken-Körper (abgerundetes Rechteck)
        _fillPaint.Color = new SKColor(0xF5, 0xF5, 0xF5);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(sinkX, sinkY, sinkX + sinkW, sinkY + sinkH), 6), _fillPaint);
        // Inneres Becken (dunkleres Grau)
        _fillPaint.Color = new SKColor(0xE0, 0xE0, 0xE0);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(sinkX + 4, sinkY + 4, sinkX + sinkW - 4, sinkY + sinkH - 2), 4), _fillPaint);
        // Rand-Glanz
        _strokePaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 120);
        _strokePaint.StrokeWidth = 1;
        canvas.DrawLine(sinkX + 6, sinkY + 2, sinkX + sinkW - 6, sinkY + 2, _strokePaint);

        // Keramik-Glanz (weisser Bogen innen)
        _strokePaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 60);
        _strokePaint.StrokeWidth = 2;
        canvas.DrawArc(new SKRect(sinkX + 8, sinkY + 6, sinkX + sinkW - 8, sinkY + sinkH - 4), 200, 140, false, _strokePaint);

        // --- Wasserhahn ---
        float faucetX = cx;
        float faucetY = sinkY - 2;
        // Rohr nach oben
        _fillPaint.Color = new SKColor(0xB0, 0xB0, 0xB0);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(faucetX - 3, faucetY - 22, faucetX + 3, faucetY), 2), _fillPaint);
        // Bogen nach rechts
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(faucetX - 3, faucetY - 22, faucetX + 16, faucetY - 16), 3), _fillPaint);
        // Auslauf
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(faucetX + 12, faucetY - 20, faucetX + 18, faucetY - 6), 2), _fillPaint);

        // --- Fließendes Wasser ---
        float waterX = faucetX + 15;
        // Wasser-Glow
        DrawGlow(canvas, waterX, sinkY - 6, 10, new SKColor(0x42, 0xA5, 0xF5, 40), _glowSmall);
        _fillPaint.Color = new SKColor(0x42, 0xA5, 0xF5, 160);
        float waterLen = sinkY - (faucetY - 6);
        // Wasser als wellige Linie
        for (float wy = 0; wy < waterLen; wy += 3)
        {
            float wobble = MathF.Sin(wy * 0.3f + phase * 6) * 1.5f;
            float wWidth = 2 + wy / waterLen * 3; // Breiter werdend
            canvas.DrawRect(waterX + wobble - wWidth / 2, faucetY - 6 + wy, wWidth, 3, _fillPaint);
        }

        // --- Abflussrohr nach unten ---
        float pipeX = cx;
        float pipeTopY = sinkY + sinkH;
        float pipeBottomY = top + h * 0.85f;

        _fillPaint.Color = new SKColor(0x78, 0x90, 0x9C);
        canvas.DrawRect(pipeX - 4, pipeTopY, 8, pipeBottomY - pipeTopY, _fillPaint);
        // Chrome-Highlight (heller Streifen links)
        _fillPaint.Color = new SKColor(0xB0, 0xBE, 0xC5);
        canvas.DrawRect(pipeX - 4, pipeTopY, 2, pipeBottomY - pipeTopY, _fillPaint);
        // Chrome-Reflexion (weisser Streifen)
        _fillPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 40);
        canvas.DrawRect(pipeX - 3, pipeTopY, 1, pipeBottomY - pipeTopY, _fillPaint);

        // Flansch-Verbindung
        _fillPaint.Color = new SKColor(0x60, 0x7D, 0x8B);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(pipeX - 6, pipeTopY + 15, pipeX + 6, pipeTopY + 21), 2), _fillPaint);

        // --- Rohrschlüssel animiert am Flansch ---
        float wrenchAngle = MathF.Sin(phase * 3) * 0.4f;
        float wrenchX = pipeX + 6;
        float wrenchY = pipeTopY + 18;
        canvas.Save();
        canvas.Translate(wrenchX, wrenchY);
        canvas.RotateRadians(wrenchAngle);
        _fillPaint.Color = new SKColor(0xE0, 0x35, 0x35);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(0, -3, 20, 3), 1), _fillPaint);
        // Schlüssel-Maul
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(-4, -5, 4, 5), 1), _fillPaint);
        canvas.Restore();

        // --- Wassertropfen am Rohrende ---
        float dropPhase = (phase * 2.5f) % MathF.Tau;
        if (dropPhase > MathF.PI - 0.4f && dropPhase < MathF.PI + 0.4f)
        {
            for (int p = 0; p < particleRate; p++)
                addWorkParticle(pipeX, pipeBottomY, new SKColor(0x42, 0xA5, 0xF5));
        }

        // --- Worker liegt unter dem Waschbecken ---
        float workerX = cx - sinkW / 2 - 8;
        float workerY = sinkY + sinkH + 30;
        // Liegender Worker (vereinfacht)
        _strokePaint.Color = new SKColor(0x0E, 0x74, 0x90);
        _strokePaint.StrokeWidth = 2;
        _strokePaint.StrokeCap = SKStrokeCap.Round;
        // Kopf
        _fillPaint.Color = new SKColor(0x0E, 0x74, 0x90);
        canvas.DrawCircle(workerX - 12, workerY, 5, _fillPaint);
        // Körper (liegend)
        canvas.DrawLine(workerX - 6, workerY, workerX + 20, workerY, _strokePaint);
        // Arm nach oben (arbeitet am Rohr)
        float armWave = MathF.Sin(phase * 4) * 3;
        canvas.DrawLine(workerX + 10, workerY, workerX + 10 + armWave, workerY - 12, _strokePaint);
        // Beine
        canvas.DrawLine(workerX + 20, workerY, workerX + 28, workerY + 6, _strokePaint);
        canvas.DrawLine(workerX + 20, workerY, workerX + 28, workerY - 4, _strokePaint);

        // --- 4+ Worker: Zweiter Arbeiter am Hahn + Dampf ---
        if (activeWorkers >= 4)
        {
            DrawStickFigure(canvas, faucetX - 25, sinkY + 8, 0.7f,
                new SKColor(0x0E, 0x74, 0x90), phase * 2, 0, type);

            // Dampfwolken
            float steamP = (phase * 1.2f) % 1.0f;
            float steamY = sinkY - 25 - steamP * 15;
            byte steamA = (byte)Math.Clamp(120 - steamP * 120, 10, 120);
            _fillPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, steamA);
            canvas.DrawCircle(waterX + 5, steamY, 6 + steamP * 4, _fillPaint);
            canvas.DrawCircle(waterX + 12, steamY - 6, 4 + steamP * 3, _fillPaint);
        }

        // Münz-Emission
        TryEmitCoin(phase, activeWorkers, level, cx, sinkY - 30, addCoinParticle);
    }

    // ====================================================================
    // Elektriker: Großer Sicherungskasten
    // ====================================================================
    private void DrawElectricianScene(SKCanvas canvas, float left, float top, float w, float h,
        float phase, int particleRate, int activeWorkers, int level, WorkshopType type,
        Action<float, float, SKColor> addWorkParticle, Action<float, float> addCoinParticle)
    {
        float cx = left + w * 0.4f;

        // --- Großer offener Sicherungskasten ---
        float boxW = 80;
        float boxH = 60;
        float boxX = cx - boxW / 2;
        float boxY = top + h * 0.08f;

        // Schatten unter Sicherungskasten
        DrawShadow(canvas, boxX, boxY, boxW, boxH, _shadowLarge);

        // Kasten-Hintergrund (dunkelgrau)
        _fillPaint.Color = new SKColor(0x37, 0x37, 0x37);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(boxX, boxY, boxX + boxW, boxY + boxH), 4), _fillPaint);
        // Innenfläche
        _fillPaint.Color = new SKColor(0x21, 0x21, 0x21);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(boxX + 4, boxY + 4, boxX + boxW - 4, boxY + boxH - 4), 2), _fillPaint);

        // Offene Tür (nach links)
        _fillPaint.Color = new SKColor(0x42, 0x42, 0x42);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(boxX - 20, boxY + 2, boxX + 2, boxY + boxH - 2), 3), _fillPaint);
        // Scharnier
        _fillPaint.Color = new SKColor(0x60, 0x60, 0x60);
        canvas.DrawCircle(boxX + 1, boxY + 10, 3, _fillPaint);
        canvas.DrawCircle(boxX + 1, boxY + boxH - 10, 3, _fillPaint);

        // --- Schalter-Reihe (6 Schalter) ---
        for (int s = 0; s < 6; s++)
        {
            float sx = boxX + 10 + s * 11;
            float sy = boxY + 12;
            bool isOn = MathF.Sin(phase * 3 + s * 1.7f) > -0.3f; // Meistens an

            // Schalter-Gehäuse
            _fillPaint.Color = new SKColor(0x50, 0x50, 0x50);
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(sx, sy, sx + 8, sy + 16), 2), _fillPaint);

            // Schalter-Hebel
            _fillPaint.Color = isOn ? new SKColor(0x66, 0xBB, 0x6A) : new SKColor(0xF4, 0x43, 0x36);
            float leverY = isOn ? sy + 2 : sy + 9;
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(sx + 1, leverY, sx + 7, leverY + 5), 1), _fillPaint);
        }

        // --- LED-Reihe darunter ---
        for (int led = 0; led < 6; led++)
        {
            float lx = boxX + 12 + led * 11;
            float ly = boxY + 34;
            bool on = MathF.Sin(phase * 5 + led * 2.1f) > 0;

            _fillPaint.Color = on ? new SKColor(0x00, 0xE6, 0x76, 240) : new SKColor(0x33, 0x55, 0x33, 120);
            canvas.DrawCircle(lx, ly, 3, _fillPaint);

            // LED-Glow-Ring wenn an (gecachter Filter)
            if (on)
            {
                DrawGlow(canvas, lx, ly, 8, new SKColor(0x00, 0xE6, 0x76, 60), _glowSmall);
            }
        }

        // --- 3 Kabel nach unten ---
        var cableColors = new[] {
            new SKColor(0xF4, 0x43, 0x36), // Rot
            new SKColor(0x42, 0xA5, 0xF5), // Blau
            new SKColor(0xFF, 0xC1, 0x07)  // Gelb
        };
        float cableStartY = boxY + boxH;
        float cableEndY = top + h * 0.85f;

        for (int c = 0; c < 3; c++)
        {
            float cableX = boxX + 16 + c * 22;
            _strokePaint.Color = cableColors[c];
            _strokePaint.StrokeWidth = 3;
            _strokePaint.StrokeCap = SKStrokeCap.Round;

            // Kabel als leichte Kurve
            using var path = new SKPath();
            path.MoveTo(cableX, cableStartY);
            float ctrlX = cableX + MathF.Sin(phase * 0.8f + c) * 8;
            path.QuadTo(ctrlX, (cableStartY + cableEndY) / 2, cableX, cableEndY);
            canvas.DrawPath(path, _strokePaint);

            // Strom-Puls wandert auf Kabel
            float pulsePos = ((phase * 2 + c * 1.2f) % 1.0f);
            float pulseY = cableStartY + pulsePos * (cableEndY - cableStartY);
            float pulseXOff = MathF.Sin(phase * 0.8f + c) * 4 * (1 - pulsePos);
            _fillPaint.Color = new SKColor(0xFF, 0xFF, 0x80, 200);
            canvas.DrawCircle(cableX + pulseXOff, pulseY, 4, _fillPaint);
            DrawGlow(canvas, cableX + pulseXOff, pulseY, 10, new SKColor(0xFF, 0xFF, 0x80, 80), _glowMedium);
        }

        // --- Worker mit Schraubendreher am Kasten ---
        float workerX = boxX + boxW + 15;
        float workerY = boxY + boxH * 0.6f;
        DrawStickFigure(canvas, workerX, workerY + 18, 0.85f,
            new SKColor(0xF9, 0x73, 0x16), phase * 3, 0, type);

        // Schraubendreher in der Hand → zum Kasten zeigend
        _fillPaint.Color = new SKColor(0xF9, 0x73, 0x16);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(workerX - 16, workerY - 2, workerX - 6, workerY + 3), 1), _fillPaint);
        _fillPaint.Color = new SKColor(0x90, 0x90, 0x90);
        canvas.DrawRect(workerX - 26, workerY - 1, 12, 2, _fillPaint);

        // Funken am Schraubendreher
        float sparkPhase = (phase * 5) % MathF.Tau;
        if (sparkPhase > MathF.PI - 0.4f && sparkPhase < MathF.PI + 0.4f)
        {
            for (int p = 0; p < particleRate; p++)
            {
                var sparkColor = (p % 2 == 0) ? new SKColor(0xFF, 0xC1, 0x07) : new SKColor(0xFF, 0xFF, 0x80);
                addWorkParticle(workerX - 26, workerY, sparkColor);
            }
        }

        // --- 4+ Worker: Blitz-Flash + Neon-Glow ---
        if (activeWorkers >= 4)
        {
            float flashCycle = (phase * 0.5f) % 2.0f;
            if (flashCycle < 0.06f)
            {
                _strokePaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 220);
                _strokePaint.StrokeWidth = 2;
                float fx = left + w * 0.15f;
                float fy = top + h * 0.2f;
                canvas.DrawLine(fx, fy, fx + 6, fy + 12, _strokePaint);
                canvas.DrawLine(fx + 6, fy + 12, fx + 2, fy + 24, _strokePaint);
                canvas.DrawLine(fx + 2, fy + 24, fx + 8, fy + 36, _strokePaint);
            }
        }

        // Münz-Emission
        TryEmitCoin(phase, activeWorkers, level, cx, boxY - 10, addCoinParticle);
    }

    // ====================================================================
    // Maler: Wand wird gestrichen
    // ====================================================================
    private void DrawPainterScene(SKCanvas canvas, float left, float top, float w, float h,
        float phase, int particleRate, int activeWorkers, int level, WorkshopType type,
        Action<float, float, SKColor> addWorkParticle, Action<float, float> addCoinParticle)
    {
        var wallColors = new[] {
            new SKColor(0xEC, 0x48, 0x99), new SKColor(0x42, 0xA5, 0xF5),
            new SKColor(0x66, 0xBB, 0x6A), new SKColor(0xFF, 0xCA, 0x28)
        };
        int colorIdx = ((int)(phase * 0.12f)) % wallColors.Length;
        var currentColor = wallColors[colorIdx];

        // --- Große Wand (fast volle Szene) ---
        float wallX = left + w * 0.08f;
        float wallW = w * 0.72f;
        float wallY = top + 4;
        float wallH = h * 0.78f;

        // Schatten hinter Wand
        DrawShadow(canvas, wallX, wallY, wallW, wallH, _shadowLarge);

        // Ungestrichene Wand
        _fillPaint.Color = new SKColor(0xEE, 0xEE, 0xEE);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(wallX, wallY, wallX + wallW, wallY + wallH), 3), _fillPaint);

        // Gestrichene Fläche (wächst und schrumpft sanft - PingPong)
        float paintProgress = PingPong(phase, 0.25f);
        float paintedW = paintProgress * wallW;
        _fillPaint.Color = currentColor.WithAlpha(190);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(wallX, wallY, wallX + paintedW, wallY + wallH), 3, 0), _fillPaint);

        // Nasser Übergangsrand (verlaufend, weich)
        _fillPaint.Color = currentColor.WithAlpha(100);
        for (int d = 0; d < (int)(wallH / 8); d++)
        {
            float dripX = wallX + paintedW + MathF.Sin(d * 1.3f + phase) * 5;
            float dripY = wallY + d * 8;
            float dripH = 4 + MathF.Sin(d * 0.7f + phase * 2) * 3;
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(dripX, dripY, dripX + 4, dripY + dripH), 1), _fillPaint);
        }

        // --- Leiter rechts ---
        float ladderX = left + w * 0.83f;
        float ladderTopY = wallY + 4;
        float ladderBotY = top + h - 4;

        _strokePaint.Color = new SKColor(0x90, 0x90, 0x90);
        _strokePaint.StrokeWidth = 2.5f;
        // Holme
        canvas.DrawLine(ladderX, ladderTopY, ladderX + 4, ladderBotY, _strokePaint);
        canvas.DrawLine(ladderX + 16, ladderTopY, ladderX + 12, ladderBotY, _strokePaint);
        // Sprossen
        _strokePaint.StrokeWidth = 2;
        for (int r = 0; r < 5; r++)
        {
            float ry = ladderTopY + 10 + r * (ladderBotY - ladderTopY - 20) / 4;
            float lOff = (ry - ladderTopY) / (ladderBotY - ladderTopY) * 4;
            canvas.DrawLine(ladderX + lOff, ry, ladderX + 16 - lOff + 4, ry, _strokePaint);
        }

        // --- Farbroller am Rand der gestrichenen Zone ---
        float rollerX = wallX + paintedW - 4;
        float rollerOscY = MathF.Sin(phase * 2.5f);
        float rollerY = wallY + wallH * 0.15f + (rollerOscY + 1) / 2 * (wallH * 0.6f);

        // Roller-Glow (Farbe leuchtet)
        DrawGlow(canvas, rollerX, rollerY + 13, 14, currentColor.WithAlpha(50), _glowSmall);

        // Roller-Walze
        _fillPaint.Color = currentColor;
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(rollerX - 4, rollerY, rollerX + 5, rollerY + 26), 3), _fillPaint);
        // Farb-Textur auf Walze
        _fillPaint.Color = currentColor.WithAlpha(220);
        for (int t = 0; t < 3; t++)
            canvas.DrawRect(rollerX - 3, rollerY + 3 + t * 8, 7, 3, _fillPaint);

        // Stiel (Metallstange)
        _strokePaint.Color = new SKColor(0x90, 0x90, 0x90);
        _strokePaint.StrokeWidth = 2;
        canvas.DrawLine(rollerX + 1, rollerY - 2, rollerX + 1, rollerY - 18, _strokePaint);
        canvas.DrawLine(rollerX + 1, rollerY - 18, rollerX + 12, rollerY - 26, _strokePaint);

        // --- Worker auf der Leiter ---
        float workerLadderY = ladderTopY + (ladderBotY - ladderTopY) * 0.35f;
        DrawStickFigure(canvas, ladderX + 8, workerLadderY, 0.8f,
            new SKColor(0xEC, 0x48, 0x99), phase * 2, 0, type);

        // --- Farbeimer unten links ---
        float bucketX = left + 6;
        float bucketY = top + h - 18;
        _fillPaint.Color = new SKColor(0x78, 0x78, 0x78);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(bucketX, bucketY, bucketX + 18, bucketY + 16), 2), _fillPaint);
        _fillPaint.Color = currentColor;
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(bucketX + 2, bucketY + 2, bucketX + 16, bucketY + 10), 1), _fillPaint);
        // Henkel
        _strokePaint.Color = new SKColor(0x60, 0x60, 0x60);
        _strokePaint.StrokeWidth = 1.5f;
        using (var henkelPath = new SKPath())
        {
            henkelPath.MoveTo(bucketX + 3, bucketY);
            henkelPath.QuadTo(bucketX + 9, bucketY - 8, bucketX + 15, bucketY);
            canvas.DrawPath(henkelPath, _strokePaint);
        }

        // Farbspritzer-Partikel
        float splatPhase = (phase * 3) % MathF.Tau;
        if (splatPhase > MathF.PI - 0.3f && splatPhase < MathF.PI + 0.3f)
        {
            for (int p = 0; p < particleRate; p++)
                addWorkParticle(rollerX, rollerY + 12, currentColor);
        }

        // --- 4+ Worker: Zweiter Roller + Regenbogen ---
        if (activeWorkers >= 4)
        {
            var color2 = wallColors[(colorIdx + 2) % wallColors.Length];
            float r2Y = wallY + wallH * 0.7f - MathF.Sin(phase * 2 + 1) * (wallH * 0.4f);
            _fillPaint.Color = color2;
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(rollerX + 14, r2Y, rollerX + 22, r2Y + 22), 3), _fillPaint);
            _strokePaint.Color = new SKColor(0x90, 0x90, 0x90);
            _strokePaint.StrokeWidth = 2;
            canvas.DrawLine(rollerX + 18, r2Y - 2, rollerX + 18, r2Y - 16, _strokePaint);

            // Regenbogen-Streifen oben
            for (int s = 0; s < 4; s++)
            {
                _fillPaint.Color = wallColors[s].WithAlpha(50);
                canvas.DrawRect(wallX, wallY - 8 + s * 2.5f, wallW, 2.5f, _fillPaint);
            }
        }

        // Münz-Emission
        TryEmitCoin(phase, activeWorkers, level, wallX + wallW / 2, wallY - 10, addCoinParticle);
    }

    // ====================================================================
    // Dachdecker: Hausdach wird gedeckt
    // ====================================================================
    private void DrawRooferScene(SKCanvas canvas, float left, float top, float w, float h,
        float phase, int particleRate, int activeWorkers, int level, WorkshopType type,
        Action<float, float, SKColor> addWorkParticle, Action<float, float> addCoinParticle)
    {
        float cx = left + w * 0.42f;

        // --- Haus-Silhouette ---
        float houseW = w * 0.55f;
        float houseX = cx - houseW / 2;
        float roofPeakY = top + h * 0.08f;
        float roofBaseY = top + h * 0.48f;
        float wallBaseY = top + h * 0.92f;

        // Schatten unter Haus
        DrawShadow(canvas, houseX + 10, roofBaseY, houseW - 20, wallBaseY - roofBaseY, _shadowLarge);

        // Hauswand
        _fillPaint.Color = new SKColor(0xE8, 0xE0, 0xD4);
        canvas.DrawRect(houseX + 10, roofBaseY, houseW - 20, wallBaseY - roofBaseY, _fillPaint);
        // Fenster mit Rahmen
        _fillPaint.Color = new SKColor(0x87, 0xCE, 0xEB, 180);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(houseX + 20, roofBaseY + 8, houseX + 38, roofBaseY + 22), 2), _fillPaint);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(houseX + houseW - 48, roofBaseY + 8, houseX + houseW - 30, roofBaseY + 22), 2), _fillPaint);
        // Fensterrahmen (weiss)
        _strokePaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 160);
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(houseX + 20, roofBaseY + 8, houseX + 38, roofBaseY + 22), 2), _strokePaint);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(houseX + houseW - 48, roofBaseY + 8, houseX + houseW - 30, roofBaseY + 22), 2), _strokePaint);
        // Fensterkreuz
        canvas.DrawLine(houseX + 29, roofBaseY + 8, houseX + 29, roofBaseY + 22, _strokePaint);
        canvas.DrawLine(houseX + 20, roofBaseY + 15, houseX + 38, roofBaseY + 15, _strokePaint);
        canvas.DrawLine(houseX + houseW - 39, roofBaseY + 8, houseX + houseW - 39, roofBaseY + 22, _strokePaint);
        canvas.DrawLine(houseX + houseW - 48, roofBaseY + 15, houseX + houseW - 30, roofBaseY + 15, _strokePaint);
        // Tuer
        _fillPaint.Color = new SKColor(0x6D, 0x4C, 0x41);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(cx - 8, roofBaseY + 6, cx + 8, wallBaseY), 2, 2), _fillPaint);
        // Tuerklinke
        _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00, 180);
        canvas.DrawCircle(cx + 5, roofBaseY + (wallBaseY - roofBaseY) * 0.55f, 1.5f, _fillPaint);

        // --- Schornstein ---
        float chimX = cx + houseW * 0.2f;
        float chimTopY = roofPeakY + (roofBaseY - roofPeakY) * 0.15f;
        float chimBotY = roofPeakY + (roofBaseY - roofPeakY) * 0.45f;
        _fillPaint.Color = new SKColor(0x8D, 0x6E, 0x63);
        canvas.DrawRect(chimX - 5, chimTopY - 12, 10, chimBotY - chimTopY + 12, _fillPaint);
        // Schornstein-Abschluss
        _fillPaint.Color = new SKColor(0x6D, 0x4C, 0x41);
        canvas.DrawRect(chimX - 7, chimTopY - 14, 14, 4, _fillPaint);

        // --- Dach-Dreieck (Holzunterlage) ---
        using var roofPath = new SKPath();
        roofPath.MoveTo(cx, roofPeakY);
        roofPath.LineTo(houseX, roofBaseY);
        roofPath.LineTo(houseX + houseW, roofBaseY);
        roofPath.Close();

        _fillPaint.Color = new SKColor(0x8D, 0x6E, 0x63);
        canvas.DrawPath(roofPath, _fillPaint);

        // Dachlatten
        _strokePaint.Color = new SKColor(0x6D, 0x4C, 0x41);
        _strokePaint.StrokeWidth = 1.5f;
        for (int l = 0; l < 5; l++)
        {
            float t = (l + 1) / 6.0f;
            float latteY = roofPeakY + t * (roofBaseY - roofPeakY);
            float latteHalfW = t * houseW / 2;
            canvas.DrawLine(cx - latteHalfW, latteY, cx + latteHalfW, latteY, _strokePaint);
        }

        // --- Dachziegel progressiv gelegt (PingPong) ---
        float tileProgress = PingPong(phase, 0.35f);
        int tileRows = 5;
        int visibleRows = (int)(tileProgress * tileRows) + 1;

        for (int row = tileRows - 1; row >= Math.Max(0, tileRows - visibleRows); row--)
        {
            float rowT = (row + 1) / (float)(tileRows + 1);
            float rowY = roofPeakY + rowT * (roofBaseY - roofPeakY);
            float rowHalfW = rowT * houseW / 2;
            float tileW = 14;
            float tileH = 8;
            int tilesInRow = (int)(rowHalfW * 2 / (tileW + 1));
            float offset = (row % 2 == 1) ? tileW / 2 : 0;

            for (int t = 0; t < tilesInRow; t++)
            {
                float tx = cx - rowHalfW + offset + t * (tileW + 1);
                if (tx + tileW > cx + rowHalfW) continue;
                if (tx < cx - rowHalfW) continue;

                _fillPaint.Color = new SKColor(0xDC, 0x26, 0x26);
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(tx, rowY, tx + tileW, rowY + tileH), 1.5f), _fillPaint);
                // Staerkerer 3D-Effekt: Dunklerer Schatten unten + seitlich
                _fillPaint.Color = new SKColor(0xA0, 0x18, 0x18);
                canvas.DrawRect(tx, rowY + tileH - 2.5f, tileW, 2.5f, _fillPaint);
                _fillPaint.Color = new SKColor(0xBF, 0x20, 0x20, 120);
                canvas.DrawRect(tx + tileW - 2, rowY + 1, 2, tileH - 2, _fillPaint);
                // Heller Glanz oben (breiter)
                _fillPaint.Color = new SKColor(0xEF, 0x53, 0x50, 100);
                canvas.DrawRect(tx + 1, rowY + 1, tileW - 3, 2.5f, _fillPaint);
            }
        }

        // --- Nächster Ziegel schwebt ein ---
        float nextFloat = MathF.Abs(MathF.Sin(phase * 4)) * 14;
        float nextTileX = houseX + houseW + 8;
        float nextTileY = roofBaseY - 20 - nextFloat;
        _fillPaint.Color = new SKColor(0xDC, 0x26, 0x26, 220);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(nextTileX, nextTileY, nextTileX + 14, nextTileY + 8), 1.5f), _fillPaint);

        // --- Hammer klopft ---
        float hammerPhase = phase * 6;
        float hammerX = houseX + houseW * 0.7f;
        float hammerBaseY = roofPeakY + (roofBaseY - roofPeakY) * 0.5f;
        float hammerY = hammerBaseY - 4 + MathF.Sin(hammerPhase) * 6;

        // Hammer-Flash bei Aufschlag
        float hammerHit = hammerPhase % MathF.Tau;
        if (hammerHit > MathF.PI - 0.3f && hammerHit < MathF.PI + 0.3f)
            DrawGlow(canvas, hammerX, hammerBaseY, 10, new SKColor(0xFF, 0xFF, 0xFF, 60), _glowSmall);

        // Hammer-Griff (Holz mit Maserung)
        _fillPaint.Color = new SKColor(0x8D, 0x6E, 0x63);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(hammerX - 2.5f, hammerY, hammerX + 2.5f, hammerY + 16), 1), _fillPaint);
        // Holz-Maserung auf Griff
        _strokePaint.Color = new SKColor(0x6D, 0x4C, 0x41, 100);
        _strokePaint.StrokeWidth = 0.5f;
        canvas.DrawLine(hammerX - 1, hammerY + 2, hammerX - 1, hammerY + 14, _strokePaint);
        canvas.DrawLine(hammerX + 1, hammerY + 3, hammerX + 1, hammerY + 13, _strokePaint);
        // Hammerkopf (Metall mit Highlight)
        _fillPaint.Color = new SKColor(0x78, 0x78, 0x78);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(hammerX - 7, hammerY - 6, hammerX + 7, hammerY + 1), 2), _fillPaint);
        // Metall-Glanz
        _fillPaint.Color = new SKColor(0xA0, 0xA0, 0xA0, 120);
        canvas.DrawRect(hammerX - 6, hammerY - 5, 12, 2, _fillPaint);

        // Staub-Partikel
        float hitPhase = hammerPhase % MathF.Tau;
        if (hitPhase > MathF.PI - 0.3f && hitPhase < MathF.PI + 0.3f)
        {
            for (int p = 0; p < particleRate; p++)
                addWorkParticle(hammerX, hammerBaseY + 2, new SKColor(0x9E, 0x9E, 0x9E, 180));
        }

        // --- Worker auf dem Dach ---
        float workerRoofT = 0.6f;
        float workerRoofY = roofPeakY + workerRoofT * (roofBaseY - roofPeakY);
        float workerRoofX = cx + workerRoofT * houseW * 0.3f;
        DrawStickFigure(canvas, workerRoofX, workerRoofY + 10, 0.75f,
            new SKColor(0xDC, 0x26, 0x26), phase * 2, 0, type);

        // --- 4+ Worker: Ziegelstapel + zweiter Arbeiter ---
        if (activeWorkers >= 4)
        {
            // Ziegelstapel unten rechts
            for (int b = 0; b < 3; b++)
            {
                _fillPaint.Color = new SKColor(0xDC, 0x26, 0x26, 180);
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(
                    left + w * 0.82f, wallBaseY - 6 - b * 6,
                    left + w * 0.82f + 16, wallBaseY - b * 6), 1), _fillPaint);
            }

            // Zweiter Worker reicht Ziegel
            DrawStickFigure(canvas, left + w * 0.85f, wallBaseY - 20, 0.7f,
                new SKColor(0xBF, 0x20, 0x20), phase * 1.5f, phase * 2, type);
        }

        // Münz-Emission
        TryEmitCoin(phase, activeWorkers, level, cx, roofPeakY - 10, addCoinParticle);
    }

    // ====================================================================
    // Bauunternehmer: Backsteinmauer hochziehen
    // ====================================================================
    private void DrawContractorScene(SKCanvas canvas, float left, float top, float w, float h,
        float phase, int particleRate, int activeWorkers, int level, WorkshopType type,
        Action<float, float, SKColor> addWorkParticle, Action<float, float> addCoinParticle)
    {
        // --- Große Backsteinmauer ---
        float wallX = left + w * 0.1f;
        float wallW = w * 0.55f;
        float wallBottom = top + h * 0.92f;
        float brickW = 16;
        float brickH = 8;
        float mortarGap = 2;
        int maxRows = (int)((h * 0.8f) / (brickH + mortarGap));
        int bricksPerRow = (int)(wallW / (brickW + mortarGap));

        float rowProgress = PingPong(phase, 0.2f);
        int visibleRows = Math.Max(1, (int)(rowProgress * maxRows));

        // Schatten hinter Mauer
        DrawShadow(canvas, wallX, wallBottom - visibleRows * (brickH + mortarGap), wallW, visibleRows * (brickH + mortarGap), _shadowLarge);

        // --- Backsteinmauer von unten aufbauen ---
        for (int row = 0; row < visibleRows && row < maxRows; row++)
        {
            float rowY = wallBottom - (row + 1) * (brickH + mortarGap);
            float offset = (row % 2 == 1) ? brickW / 2 + 1 : 0;

            for (int col = 0; col < bricksPerRow; col++)
            {
                float bx = wallX + offset + col * (brickW + mortarGap);
                if (bx + brickW > wallX + wallW + brickW / 2) continue;

                // Stein
                _fillPaint.Color = new SKColor(0xEA, 0x58, 0x0C);
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(bx, rowY, bx + brickW, rowY + brickH), 1), _fillPaint);
                // Highlight oben
                _fillPaint.Color = new SKColor(0xF5, 0x73, 0x30, 80);
                canvas.DrawRect(bx + 1, rowY + 1, brickW - 2, 2, _fillPaint);
            }

            // Fugen horizontal
            if (row > 0)
            {
                _fillPaint.Color = new SKColor(0xBD, 0xBD, 0xBD);
                canvas.DrawRect(wallX, rowY + brickH, wallW, mortarGap, _fillPaint);
            }
        }

        // --- Kelle streicht Mörtel über oberste Reihe ---
        float topRowY = wallBottom - visibleRows * (brickH + mortarGap);
        float kellePhase = MathF.Sin(phase * 3);
        float kelleX = wallX + wallW * 0.2f + kellePhase * (wallW * 0.4f);

        // Mörtel-Spur
        _fillPaint.Color = new SKColor(0xBD, 0xBD, 0xBD, 150);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(wallX, topRowY - mortarGap - 1, kelleX + 6, topRowY), 1), _fillPaint);

        // Kelle (Metallblatt)
        _fillPaint.Color = new SKColor(0xA0, 0xA0, 0xA0);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(kelleX - 7, topRowY - mortarGap - 4, kelleX + 9, topRowY - mortarGap), 1), _fillPaint);
        // Metall-Glanz
        _fillPaint.Color = new SKColor(0xC0, 0xC0, 0xC0, 100);
        canvas.DrawRect(kelleX - 5, topRowY - mortarGap - 3, 12, 1.5f, _fillPaint);
        // Moertel-Klecks auf der Kelle
        _fillPaint.Color = new SKColor(0xBD, 0xBD, 0xBD, 180);
        canvas.DrawOval(kelleX + 2, topRowY - mortarGap - 2, 5, 2.5f, _fillPaint);
        // Holzgriff
        _fillPaint.Color = new SKColor(0x6D, 0x4C, 0x41);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(kelleX, topRowY - mortarGap - 14, kelleX + 4, topRowY - mortarGap - 3), 1), _fillPaint);

        // --- Neuer Stein schwebt ein ---
        float stoneFloat = MathF.Abs(MathF.Sin(phase * 2)) * 16;
        _fillPaint.Color = new SKColor(0xEA, 0x58, 0x0C, 200);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(
            wallX + wallW * 0.4f, topRowY - brickH - 10 - stoneFloat,
            wallX + wallW * 0.4f + brickW, topRowY - 10 - stoneFloat), 1), _fillPaint);

        // --- Kleiner Kran rechts ---
        float craneX = left + w * 0.78f;
        float craneTopY = top + 6;
        float craneBaseY = top + h * 0.92f;

        // Kran-Mast
        _strokePaint.Color = new SKColor(0xFF, 0xC1, 0x07);
        _strokePaint.StrokeWidth = 3;
        canvas.DrawLine(craneX, craneBaseY, craneX, craneTopY, _strokePaint);
        // Kran-Ausleger
        canvas.DrawLine(craneX, craneTopY + 4, craneX - 30, craneTopY + 4, _strokePaint);
        // Seil mit Textur (gestrichelt fuer Seil-Look)
        _strokePaint.Color = new SKColor(0x50, 0x50, 0x50);
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.PathEffect = SKPathEffect.CreateDash([3, 2], 0);
        float seilLen = 15 + MathF.Sin(phase * 1.5f) * 8;
        canvas.DrawLine(craneX - 20, craneTopY + 4, craneX - 20, craneTopY + 4 + seilLen, _strokePaint);
        _strokePaint.PathEffect = null;
        // Haken (detaillierter)
        _fillPaint.Color = new SKColor(0x78, 0x78, 0x78);
        canvas.DrawCircle(craneX - 20, craneTopY + 4 + seilLen + 3, 3.5f, _fillPaint);
        // Haken-Oeffnung
        _strokePaint.Color = new SKColor(0x60, 0x60, 0x60);
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawArc(new SKRect(craneX - 24, craneTopY + 4 + seilLen + 1, craneX - 16, craneTopY + 4 + seilLen + 9), 0, 200, false, _strokePaint);

        // --- Worker mit Kelle ---
        DrawStickFigure(canvas, wallX - 14, wallBottom - 10, 0.9f,
            new SKColor(0xEA, 0x58, 0x0C), phase * 3, phase * 2, type);

        // --- Zementsäcke unten rechts ---
        _fillPaint.Color = new SKColor(0x9E, 0x9E, 0x9E);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(left + w * 0.7f, wallBottom - 10, left + w * 0.7f + 20, wallBottom + 2), 2), _fillPaint);
        _fillPaint.Color = new SKColor(0x85, 0x85, 0x85);
        canvas.DrawRect(left + w * 0.7f + 3, wallBottom - 7, 14, 4, _fillPaint);

        // Staubwolken
        float dustPhase = (phase * 3) % MathF.Tau;
        if (dustPhase > MathF.PI - 0.3f && dustPhase < MathF.PI + 0.3f)
        {
            for (int p = 0; p < particleRate; p++)
                addWorkParticle(kelleX, topRowY - 2, new SKColor(0xBD, 0xBD, 0xBD));
        }

        // --- 4+ Worker: Wasserwaage + zweiter Arbeiter ---
        if (activeWorkers >= 4)
        {
            // Wasserwaage an der Mauer
            _fillPaint.Color = new SKColor(0xFF, 0xC1, 0x07);
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(wallX + wallW + 6, topRowY + 10, wallX + wallW + 12, topRowY + 40), 1), _fillPaint);
            float bubbleY = topRowY + 25 + MathF.Sin(phase * 1.5f) * 5;
            _fillPaint.Color = new SKColor(0x66, 0xBB, 0x6A, 200);
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(wallX + wallW + 7, bubbleY - 3, wallX + wallW + 11, bubbleY + 3), 1), _fillPaint);

            // Zweiter Arbeiter
            DrawStickFigure(canvas, craneX + 12, craneBaseY - 10, 0.75f,
                new SKColor(0xF5, 0x73, 0x30), phase * 1.5f, phase * 2, type);
        }

        // Münz-Emission
        TryEmitCoin(phase, activeWorkers, level, wallX + wallW / 2, topRowY - 20, addCoinParticle);
    }

    // ====================================================================
    // Architekt: Bauplan zeichnen
    // ====================================================================
    private void DrawArchitectScene(SKCanvas canvas, float left, float top, float w, float h,
        float phase, int particleRate, int activeWorkers, int level, WorkshopType type,
        Action<float, float, SKColor> addWorkParticle, Action<float, float> addCoinParticle)
    {
        // --- Großer Bauplan (Blaupause) ---
        float bpX = left + 4;
        float bpY = top + 4;
        float bpW = w * 0.88f;
        float bpH = h * 0.82f;

        // Schatten hinter Blaupause
        DrawShadow(canvas, bpX, bpY, bpW, bpH, _shadowMedium);

        // Papier-Hintergrund (Blaupause-Blau)
        _fillPaint.Color = new SKColor(0x1A, 0x47, 0x7A, 60);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(bpX, bpY, bpX + bpW, bpY + bpH), 4), _fillPaint);

        // Weißer Rand
        _strokePaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 40);
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(bpX, bpY, bpX + bpW, bpY + bpH), 4), _strokePaint);

        // Feines Raster
        _strokePaint.Color = new SKColor(0x42, 0xA5, 0xF5, 20);
        _strokePaint.StrokeWidth = 0.5f;
        float gridSize = 18;
        for (float gx = bpX + gridSize; gx < bpX + bpW; gx += gridSize)
            canvas.DrawLine(gx, bpY + 2, gx, bpY + bpH - 2, _strokePaint);
        for (float gy = bpY + gridSize; gy < bpY + bpH; gy += gridSize)
            canvas.DrawLine(bpX + 2, gy, bpX + bpW - 2, gy, _strokePaint);

        // --- Grundriss wird progressiv gezeichnet (PingPong) ---
        float drawProgress = PingPong(phase, 0.18f);
        _strokePaint.Color = new SKColor(0x42, 0xA5, 0xF5, 200);
        _strokePaint.StrokeWidth = 2.5f;
        _strokePaint.StrokeCap = SKStrokeCap.Round;

        float gpX = bpX + 16;
        float gpY = bpY + 12;
        float gpW = bpW - 32;
        float gpH = bpH - 24;

        // Außenwände (4 Seiten, progressiv)
        float p1 = Math.Clamp(drawProgress / 0.18f, 0, 1);
        if (p1 > 0)
            canvas.DrawLine(gpX, gpY, gpX + gpW * p1, gpY, _strokePaint);

        float p2 = Math.Clamp((drawProgress - 0.18f) / 0.18f, 0, 1);
        if (p2 > 0)
            canvas.DrawLine(gpX + gpW, gpY, gpX + gpW, gpY + gpH * p2, _strokePaint);

        float p3 = Math.Clamp((drawProgress - 0.36f) / 0.18f, 0, 1);
        if (p3 > 0)
            canvas.DrawLine(gpX + gpW, gpY + gpH, gpX + gpW - gpW * p3, gpY + gpH, _strokePaint);

        float p4 = Math.Clamp((drawProgress - 0.54f) / 0.18f, 0, 1);
        if (p4 > 0)
            canvas.DrawLine(gpX, gpY + gpH, gpX, gpY + gpH - gpH * p4, _strokePaint);

        // Innenwände (0.72-1.0)
        float p5 = Math.Clamp((drawProgress - 0.72f) / 0.28f, 0, 1);
        if (p5 > 0)
        {
            _strokePaint.Color = new SKColor(0x42, 0xA5, 0xF5, 160);
            // Vertikale Trennwand
            canvas.DrawLine(gpX + gpW * 0.45f, gpY, gpX + gpW * 0.45f, gpY + gpH * p5, _strokePaint);
            // Horizontale Trennwand mit Türöffnung
            if (p5 > 0.3f)
            {
                float hw = gpW * 0.45f * Math.Clamp((p5 - 0.3f) / 0.7f, 0, 1);
                canvas.DrawLine(gpX, gpY + gpH * 0.55f, gpX + hw * 0.35f, gpY + gpH * 0.55f, _strokePaint);
                // Tür-Bogen
                _strokePaint.Color = new SKColor(0x42, 0xA5, 0xF5, 100);
                using var doorPath = new SKPath();
                doorPath.MoveTo(gpX + hw * 0.35f, gpY + gpH * 0.55f);
                doorPath.QuadTo(gpX + hw * 0.35f + 6, gpY + gpH * 0.55f - 8,
                    gpX + hw * 0.6f, gpY + gpH * 0.55f);
                canvas.DrawPath(doorPath, _strokePaint);
                _strokePaint.Color = new SKColor(0x42, 0xA5, 0xF5, 160);
                canvas.DrawLine(gpX + hw * 0.6f, gpY + gpH * 0.55f, gpX + hw, gpY + gpH * 0.55f, _strokePaint);
            }
            // Zweite Trennwand
            if (p5 > 0.5f)
            {
                float vw = gpH * Math.Clamp((p5 - 0.5f) / 0.5f, 0, 1);
                canvas.DrawLine(gpX + gpW * 0.7f, gpY + gpH, gpX + gpW * 0.7f, gpY + gpH - vw * 0.55f, _strokePaint);
            }
        }

        // --- Bleistift am Endpunkt ---
        float pencilX, pencilY;
        if (drawProgress < 0.18f) { pencilX = gpX + gpW * p1; pencilY = gpY; }
        else if (drawProgress < 0.36f) { pencilX = gpX + gpW; pencilY = gpY + gpH * p2; }
        else if (drawProgress < 0.54f) { pencilX = gpX + gpW - gpW * p3; pencilY = gpY + gpH; }
        else if (drawProgress < 0.72f) { pencilX = gpX; pencilY = gpY + gpH - gpH * p4; }
        else { pencilX = gpX + gpW * 0.45f; pencilY = gpY + gpH * p5; }

        // Bleistift-Glow an der Spitze
        DrawGlow(canvas, pencilX, pencilY, 6, new SKColor(0xFF, 0xCA, 0x28, 50), _glowSmall);

        // Bleistift (groß, diagonal)
        canvas.Save();
        canvas.Translate(pencilX, pencilY);
        canvas.RotateDegrees(-45);
        _fillPaint.Color = new SKColor(0xFF, 0xCA, 0x28);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(-2, -16, 3, 0), 1), _fillPaint);
        _fillPaint.Color = new SKColor(0xF5, 0xCB, 0xA7);
        canvas.DrawRect(-1.5f, 0, 4, 5, _fillPaint);
        // Spitze
        _fillPaint.Color = new SKColor(0x33, 0x33, 0x33);
        canvas.DrawRect(-0.5f, 5, 2, 2, _fillPaint);
        canvas.Restore();

        // --- Lineal ---
        _fillPaint.Color = new SKColor(0xB0, 0xB0, 0xB0, 70);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(pencilX + 8, pencilY - 6, pencilX + 50, pencilY - 2), 1), _fillPaint);
        _strokePaint.Color = new SKColor(0x80, 0x80, 0x80, 50);
        _strokePaint.StrokeWidth = 0.5f;
        for (int m = 0; m < 9; m++)
            canvas.DrawLine(pencilX + 10 + m * 4.5f, pencilY - 5, pencilX + 10 + m * 4.5f, pencilY - 3, _strokePaint);

        // --- Worker sitzt am Zeichentisch (rechts unten) ---
        float deskX = left + w * 0.82f;
        float deskY = top + h * 0.7f;
        // Schatten unter Tisch
        DrawShadow(canvas, deskX - 10, deskY, 30, 20, _shadowSmall);
        // Tisch
        _fillPaint.Color = new SKColor(0x6D, 0x4C, 0x41);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(deskX - 10, deskY, deskX + 20, deskY + 6), 2), _fillPaint);
        // Tischbeine
        canvas.DrawRect(deskX - 8, deskY + 6, 3, 14, _fillPaint);
        canvas.DrawRect(deskX + 16, deskY + 6, 3, 14, _fillPaint);

        DrawStickFigure(canvas, deskX + 5, deskY + 26, 0.8f,
            new SKColor(0x78, 0x71, 0x6C), phase * 1.5f, 0, type);

        // Radiergummi-Krümel
        float crumbPhase = (phase * 2) % MathF.Tau;
        if (crumbPhase > MathF.PI - 0.2f && crumbPhase < MathF.PI + 0.2f)
        {
            for (int p = 0; p < particleRate; p++)
                addWorkParticle(pencilX, pencilY, new SKColor(0xF5, 0xF5, 0xF5));
        }

        // --- 4+ Worker: Zirkel-Bogen ---
        if (activeWorkers >= 4)
        {
            float arcProgress = (phase * 0.3f) % 1.0f;
            float arcCx = gpX + gpW * 0.72f;
            float arcCy = gpY + gpH * 0.3f;
            float arcRadius = 16;
            _strokePaint.Color = new SKColor(0x00, 0xBC, 0xD4, 120);
            _strokePaint.StrokeWidth = 1.5f;
            using var arcPath = new SKPath();
            arcPath.AddArc(new SKRect(arcCx - arcRadius, arcCy - arcRadius,
                arcCx + arcRadius, arcCy + arcRadius), 0, arcProgress * 300);
            canvas.DrawPath(arcPath, _strokePaint);

            // Bemaßungs-Pfeile
            _strokePaint.Color = new SKColor(0xFF, 0x57, 0x22, 100);
            _strokePaint.StrokeWidth = 1;
            canvas.DrawLine(gpX, gpY + gpH + 6, gpX + gpW * 0.45f, gpY + gpH + 6, _strokePaint);
            // Maßzahl
            _fillPaint.Color = new SKColor(0xFF, 0x57, 0x22, 100);
            using var dimFont = new SKFont(SKTypeface.Default, 7);
            canvas.DrawText("4.5m", gpX + gpW * 0.18f, gpY + gpH + 14, SKTextAlign.Center, dimFont, _fillPaint);
        }

        // Münz-Emission
        TryEmitCoin(phase, activeWorkers, level, bpX + bpW / 2, bpY - 8, addCoinParticle);
    }

    // ====================================================================
    // Generalunternehmer: Goldenes Büro mit Vertrag + Stempel
    // ====================================================================
    private void DrawGeneralContractorScene(SKCanvas canvas, float left, float top, float w, float h,
        float phase, int particleRate, int productCount, int activeWorkers, int level, WorkshopType type,
        Action<float, float, SKColor> addWorkParticle, Action<float, float> addCoinParticle)
    {
        float cx = left + w * 0.42f;
        float cy = top + h * 0.42f;

        // --- Goldener Vertrag (groß, zentral) ---
        float docW = 70;
        float docH = 50;
        float docX = cx - docW / 2;
        float docY = cy - docH / 2;

        // Schatten unter Vertrag
        DrawShadow(canvas, docX, docY, docW, docH, _shadowMedium);

        // Papier mit abgerundeten Ecken
        _fillPaint.Color = new SKColor(0xFF, 0xF8, 0xE1);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(docX, docY, docX + docW, docY + docH), 4), _fillPaint);

        // Goldener Rand
        _strokePaint.Color = new SKColor(0xFF, 0xD7, 0x00);
        _strokePaint.StrokeWidth = 2.5f;
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(docX, docY, docX + docW, docY + docH), 4), _strokePaint);

        // Text-Linien
        _fillPaint.Color = new SKColor(0xBD, 0xBD, 0xBD, 140);
        for (int l = 0; l < 5; l++)
        {
            float lw = (l == 4) ? docW * 0.45f : docW * 0.7f;
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(docX + 8, docY + 8 + l * 8, docX + 8 + lw, docY + 10 + l * 8), 1), _fillPaint);
        }

        // Siegel (goldener Kreis + Stern)
        float sealX = docX + docW - 14;
        float sealY = docY + docH - 12;
        _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00, 200);
        canvas.DrawCircle(sealX, sealY, 8, _fillPaint);
        _fillPaint.Color = new SKColor(0xFF, 0xB3, 0x00);
        canvas.DrawCircle(sealX, sealY, 5, _fillPaint);
        // Stern-Zacken
        _strokePaint.Color = new SKColor(0xFF, 0xD7, 0x00);
        _strokePaint.StrokeWidth = 1.5f;
        for (int s = 0; s < 4; s++)
        {
            float angle = s * MathF.PI / 2 + MathF.PI / 4;
            canvas.DrawLine(sealX, sealY,
                sealX + MathF.Cos(angle) * 5, sealY + MathF.Sin(angle) * 5, _strokePaint);
        }

        // --- Stempel drückt auf Dokument ---
        float stampCycle = (phase * 1.2f % MathF.Tau) / MathF.Tau;
        float stampX = docX + docW * 0.35f;
        float stampBaseY = docY + docH * 0.5f;
        float stampY;
        if (stampCycle < 0.35f)
            stampY = stampBaseY - 32 - stampCycle * 20;
        else if (stampCycle < 0.45f)
            stampY = stampBaseY - 32 - 7 + (stampCycle - 0.35f) * 390;
        else
            stampY = stampBaseY;

        // Stempel-Griff
        _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(stampX - 4, stampY - 16, stampX + 4, stampY), 2), _fillPaint);
        // Stempel-Fläche
        _fillPaint.Color = new SKColor(0xFF, 0xB3, 0x00);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(stampX - 8, stampY, stampX + 8, stampY + 5), 2), _fillPaint);

        // Roter Stempel-Abdruck (realistischer)
        if (stampCycle > 0.45f)
        {
            byte abdAlpha = (byte)Math.Clamp((stampCycle - 0.45f) * 300, 0, 200);
            // Roter Abdruck-Kreis
            _fillPaint.Color = new SKColor(0xE5, 0x3E, 0x3E, abdAlpha);
            canvas.DrawCircle(stampX, stampBaseY + 2, 7, _fillPaint);
            // Innerer Ring
            _strokePaint.Color = new SKColor(0xCC, 0x22, 0x22, abdAlpha);
            _strokePaint.StrokeWidth = 1.5f;
            canvas.DrawCircle(stampX, stampBaseY + 2, 5, _strokePaint);

            // Glow um Abdruck (gold)
            DrawGlow(canvas, stampX, stampBaseY + 2, 14, new SKColor(0xFF, 0xD7, 0x00, (byte)(abdAlpha / 3)), _glowMedium);
        }

        // Gold-Partikel bei Stempel-Aufschlag
        if (stampCycle > 0.43f && stampCycle < 0.52f)
        {
            for (int p = 0; p < particleRate + 1; p++)
                addWorkParticle(stampX, stampBaseY, new SKColor(0xFF, 0xD7, 0x00));
        }

        // --- Gold-Shimmer wandert diagonal ---
        float shimmerPhase = (phase * 0.7f) % 1.0f;
        float shimmerX = left + shimmerPhase * w;
        float shimmerY = top + shimmerPhase * h;
        byte shimmerAlpha = (byte)(50 + MathF.Sin(shimmerPhase * MathF.PI) * 50);
        _strokePaint.Color = new SKColor(0xFF, 0xF0, 0x70, shimmerAlpha);
        _strokePaint.StrokeWidth = 3;
        canvas.DrawLine(shimmerX - 12, shimmerY - 12, shimmerX + 12, shimmerY + 12, _strokePaint);
        _strokePaint.Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(shimmerAlpha / 3));
        _strokePaint.StrokeWidth = 1;
        canvas.DrawLine(shimmerX - 10, shimmerY - 10, shimmerX + 10, shimmerY + 10, _strokePaint);

        // --- Eleganter Schreibtisch unten ---
        float deskW = 60;
        float deskX = cx - deskW / 2;
        float deskY = top + h * 0.78f;
        // Schatten unter Schreibtisch
        DrawShadow(canvas, deskX, deskY, deskW, 18, _shadowSmall);
        _fillPaint.Color = new SKColor(0x5D, 0x40, 0x37);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(deskX, deskY, deskX + deskW, deskY + 6), 2), _fillPaint);
        // Tischbeine
        canvas.DrawRect(deskX + 4, deskY + 6, 4, 12, _fillPaint);
        canvas.DrawRect(deskX + deskW - 8, deskY + 6, 4, 12, _fillPaint);

        // Worker am Schreibtisch
        DrawStickFigure(canvas, deskX + deskW / 2, deskY + 22, 0.85f,
            new SKColor(0xFF, 0xB3, 0x00), phase * 1.5f, 0, type);

        // --- Goldmuenzen rechts (mit Praegung + Glanz) ---
        for (int c = 0; c < productCount; c++)
        {
            float coinX = left + w * 0.88f;
            float coinY = top + h * 0.5f - c * 10;
            // Muenz-Schatten
            _fillPaint.Color = new SKColor(0xCC, 0x99, 0x00, 60);
            canvas.DrawCircle(coinX + 1, coinY + 1, 7.5f, _fillPaint);
            // Muenze
            _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
            canvas.DrawCircle(coinX, coinY, 7, _fillPaint);
            // Praege-Ring
            _strokePaint.Color = new SKColor(0xCC, 0x99, 0x00);
            _strokePaint.StrokeWidth = 1;
            canvas.DrawCircle(coinX, coinY, 5.5f, _strokePaint);
            // Innerer Bereich
            _fillPaint.Color = new SKColor(0xFF, 0xC1, 0x07);
            canvas.DrawCircle(coinX, coinY, 4.5f, _fillPaint);
            // Euro-Symbol
            _strokePaint.Color = new SKColor(0xCC, 0x99, 0x00);
            _strokePaint.StrokeWidth = 1.5f;
            canvas.DrawArc(new SKRect(coinX - 3, coinY - 3, coinX + 3, coinY + 3), 40, 280, false, _strokePaint);
            // Glanz-Highlight oben links
            _fillPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 60);
            canvas.DrawCircle(coinX - 2, coinY - 2, 2.5f, _fillPaint);
        }

        // --- 4+ Worker: Gold-Regen + Stern ---
        if (activeWorkers >= 4)
        {
            float rainPhase = (phase * 3) % MathF.Tau;
            if (rainPhase < 0.5f)
            {
                for (int p = 0; p < 2; p++)
                    addWorkParticle(left + 25 + p * 35, top + 4, new SKColor(0xFF, 0xD7, 0x00));
            }

            // Pulsierender Stern oben links
            float starPhase = phase * 2;
            float starX = left + 16;
            float starY = top + 16;
            float starSize = 8 + MathF.Sin(starPhase) * 3;
            _strokePaint.Color = new SKColor(0xFF, 0xD7, 0x00, 180);
            _strokePaint.StrokeWidth = 2;
            for (int s = 0; s < 4; s++)
            {
                float a = starPhase * 0.3f + s * MathF.PI / 2;
                canvas.DrawLine(starX, starY,
                    starX + MathF.Cos(a) * starSize, starY + MathF.Sin(a) * starSize, _strokePaint);
            }
        }

        // Münz-Emission (häufiger beim Generalunternehmer, level-skaliert)
        float coinInterval = activeWorkers switch { 1 => 3.0f, 2 or 3 => 2.0f, _ => 1.5f };
        float coinCycle = (phase * 0.5f) % coinInterval;
        if (coinCycle < 0.08f)
        {
            addCoinParticle(cx, docY - 10);
            if (level >= 250) addCoinParticle(cx - 5, docY - 8);
            if (level >= 500) addCoinParticle(cx + 5, docY - 12);
        }
    }

    // ====================================================================
    // Meisterschmiede: Amboss + Esse + Schmiedehammer + Funken
    // ====================================================================
    private void DrawMasterSmithScene(SKCanvas canvas, float left, float top, float w, float h,
        float phase, int particleRate, int activeWorkers, int level, WorkshopType type,
        Action<float, float, SKColor> addWorkParticle, Action<float, float> addCoinParticle)
    {
        float cx = left + w * 0.45f;
        float cy = top + h * 0.48f;
        var copperColor = new SKColor(0xD4, 0xA3, 0x73);

        // --- Esse (links, Feuerstelle mit Glow) ---
        float esseX = left + w * 0.08f;
        float esseY = cy - h * 0.15f;
        float esseW = w * 0.28f;
        float esseH = h * 0.35f;

        DrawShadow(canvas, esseX, esseY, esseW, esseH, _shadowMedium);
        // Esse-Steinblock
        _fillPaint.Color = new SKColor(0x5C, 0x3A, 0x21);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(esseX, esseY, esseX + esseW, esseY + esseH), 4), _fillPaint);
        // Esse-Öffnung
        _fillPaint.Color = new SKColor(0x2A, 0x1A, 0x0A);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(esseX + 4, esseY + 6, esseX + esseW - 4, esseY + esseH - 6), 3), _fillPaint);

        // Feuer in der Esse (animiert)
        float firePhase = PingPong(phase, 1.5f);
        _fillPaint.Color = new SKColor(0xFF, 0x6B, 0x00, (byte)(180 + firePhase * 50));
        using var firePath = new SKPath();
        float fireCx = esseX + esseW / 2;
        float fireBot = esseY + esseH - 10;
        firePath.MoveTo(fireCx - 12, fireBot);
        firePath.QuadTo(fireCx - 8, fireBot - 18 - firePhase * 8, fireCx, fireBot - 25 - firePhase * 10);
        firePath.QuadTo(fireCx + 8, fireBot - 18 - firePhase * 6, fireCx + 12, fireBot);
        firePath.Close();
        canvas.DrawPath(firePath, _fillPaint);

        // Innerer Feuerkern (gelb-weiß)
        _fillPaint.Color = new SKColor(0xFF, 0xE0, 0x82, (byte)(150 + firePhase * 60));
        using var innerFirePath = new SKPath();
        innerFirePath.MoveTo(fireCx - 6, fireBot - 2);
        innerFirePath.QuadTo(fireCx - 3, fireBot - 12 - firePhase * 5, fireCx, fireBot - 16 - firePhase * 6);
        innerFirePath.QuadTo(fireCx + 3, fireBot - 12 - firePhase * 4, fireCx + 6, fireBot - 2);
        innerFirePath.Close();
        canvas.DrawPath(innerFirePath, _fillPaint);

        // Feuer-Glow
        DrawGlow(canvas, fireCx, fireBot - 12, 20 + firePhase * 8, new SKColor(0xFF, 0x6B, 0x00, 40), _glowMedium);

        // Schornstein über der Esse
        _fillPaint.Color = new SKColor(0x4A, 0x2E, 0x18);
        canvas.DrawRect(fireCx - 5, esseY - 18, 10, 20, _fillPaint);
        // Rauch-Andeutung
        _fillPaint.Color = new SKColor(0x90, 0x90, 0x90, (byte)(20 + firePhase * 15));
        canvas.DrawCircle(fireCx - 2, esseY - 22, 4, _fillPaint);
        canvas.DrawCircle(fireCx + 1, esseY - 28, 3, _fillPaint);

        // --- Amboss (Mitte, massiv) ---
        float anvilX = cx;
        float anvilY = cy + h * 0.05f;

        DrawShadow(canvas, anvilX - 22, anvilY - 5, 44, 20, _shadowMedium);
        // Amboss-Sockel
        _fillPaint.Color = new SKColor(0x4A, 0x4A, 0x4A);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(anvilX - 16, anvilY + 8, anvilX + 16, anvilY + 18), 2), _fillPaint);
        // Amboss-Körper
        _fillPaint.Color = new SKColor(0x78, 0x78, 0x78);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(anvilX - 20, anvilY - 4, anvilX + 20, anvilY + 10), 3), _fillPaint);
        // Amboss-Horn (rechts spitz)
        using var hornPath = new SKPath();
        hornPath.MoveTo(anvilX + 20, anvilY - 2);
        hornPath.LineTo(anvilX + 35, anvilY + 3);
        hornPath.LineTo(anvilX + 20, anvilY + 8);
        hornPath.Close();
        _fillPaint.Color = new SKColor(0x70, 0x70, 0x70);
        canvas.DrawPath(hornPath, _fillPaint);
        // Metallglanz-Streifen
        _fillPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 40);
        canvas.DrawRect(anvilX - 18, anvilY - 3, 36, 2, _fillPaint);

        // --- Glühendes Werkstück auf dem Amboss ---
        float workPiece = PingPong(phase, 0.8f);
        _fillPaint.Color = new SKColor(0xFF, 0x6B, 0x00, (byte)(160 + workPiece * 60));
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(anvilX - 10, anvilY - 6, anvilX + 12, anvilY - 1), 1), _fillPaint);
        // Lv50+ Tool-Glow auf Werkstück
        if (level >= 50)
        {
            DrawGlow(canvas, anvilX, anvilY - 4, 14, new SKColor(0xFF, 0xFF, 0xFF, 30), _glowSmall);
        }

        // --- Schmiedehammer (PingPong-Animation: schlägt auf Amboss) ---
        float hammerAngle = PingPong(phase, 2.0f);
        float hammerSwing = -45 + hammerAngle * 60; // -45° bis +15° Schwung

        canvas.Save();
        canvas.Translate(anvilX + 5, anvilY - 6);
        canvas.RotateDegrees(hammerSwing);
        // Stiel
        _fillPaint.Color = new SKColor(0x8D, 0x6E, 0x63);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(-2.5f, -40, 2.5f, 0), 1), _fillPaint);
        // Hammerkopf
        _fillPaint.Color = new SKColor(0x90, 0x90, 0x90);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(-10, -44, 10, -34), 2), _fillPaint);
        // Glanz
        _fillPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 50);
        canvas.DrawRect(-8, -43, 16, 2, _fillPaint);
        canvas.Restore();

        // --- Funken-Partikel (beim Hammerschlag) ---
        if (hammerAngle < 0.15f)
        {
            for (int p = 0; p < particleRate + 4; p++)
                addWorkParticle(anvilX, anvilY - 8, new SKColor(0xFF, 0x8F, 0x00));
        }

        // --- Worker-Figur rechts vom Amboss ---
        DrawStickFigure(canvas, anvilX + w * 0.25f, anvilY + 22, 0.9f,
            copperColor, phase * 3, phase * 2, type);

        // 4+ Worker: Zweiter Arbeiter an der Esse
        if (activeWorkers >= 4)
        {
            DrawStickFigure(canvas, esseX + esseW + 5, esseY + esseH + 20, 0.8f,
                new SKColor(0xA0, 0x7A, 0x50), phase * 1.8f, phase * 2.5f, type);
        }

        // Münz-Emission
        TryEmitCoin(phase, activeWorkers, level, anvilX, anvilY - 20, addCoinParticle);
    }

    // ====================================================================
    // Innovationslabor: Erfindung am Tisch + Zahnräder + Glühbirne + Dampf
    // ====================================================================
    private void DrawInnovationLabScene(SKCanvas canvas, float left, float top, float w, float h,
        float phase, int particleRate, int activeWorkers, int level, WorkshopType type,
        Action<float, float, SKColor> addWorkParticle, Action<float, float> addCoinParticle)
    {
        float cx = left + w * 0.45f;
        float cy = top + h * 0.48f;
        var violetColor = new SKColor(0x6A, 0x5A, 0xCD);

        // --- Arbeitstisch (großer Labortisch) ---
        float tableW = w * 0.6f;
        float tableH = 12;
        float tableX = left + (w - tableW) / 2;
        float tableY = cy + h * 0.02f;

        DrawShadow(canvas, tableX, tableY, tableW, tableH + h * 0.25f, _shadowMedium);
        // Tischbeine (Metall)
        _fillPaint.Color = new SKColor(0x70, 0x70, 0x70);
        canvas.DrawRect(tableX + 10, tableY + tableH, 5, h * 0.25f, _fillPaint);
        canvas.DrawRect(tableX + tableW - 15, tableY + tableH, 5, h * 0.25f, _fillPaint);
        // Tischplatte (weiß/grau - Labor-Optik)
        _fillPaint.Color = new SKColor(0xD0, 0xD0, 0xDA);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(tableX, tableY, tableX + tableW, tableY + tableH), 2), _fillPaint);
        // Highlight
        _fillPaint.Color = new SKColor(0xE8, 0xE8, 0xF0);
        canvas.DrawRect(tableX + 2, tableY + 1, tableW - 4, 3, _fillPaint);

        // --- Erlenmeyer-Kolben auf Tisch (links) ---
        float flaskX = tableX + tableW * 0.2f;
        float flaskY = tableY - 2;

        // Kolbenhals
        _strokePaint.Color = new SKColor(0xB0, 0xD0, 0xF0, 0xD0);
        _strokePaint.StrokeWidth = 2;
        canvas.DrawLine(flaskX - 4, flaskY - 28, flaskX - 4, flaskY - 12, _strokePaint);
        canvas.DrawLine(flaskX + 4, flaskY - 28, flaskX + 4, flaskY - 12, _strokePaint);
        // Kolbenkörper
        using var flaskPath = new SKPath();
        flaskPath.MoveTo(flaskX - 4, flaskY - 12);
        flaskPath.LineTo(flaskX - 14, flaskY);
        flaskPath.LineTo(flaskX + 14, flaskY);
        flaskPath.LineTo(flaskX + 4, flaskY - 12);
        canvas.DrawPath(flaskPath, _strokePaint);
        // Flüssigkeit
        float bubblePhase = PingPong(phase, 1.2f);
        _fillPaint.Color = violetColor.WithAlpha((byte)(100 + bubblePhase * 40));
        using var liquidPath = new SKPath();
        liquidPath.MoveTo(flaskX - 8, flaskY - 8);
        liquidPath.LineTo(flaskX - 13, flaskY - 1);
        liquidPath.LineTo(flaskX + 13, flaskY - 1);
        liquidPath.LineTo(flaskX + 8, flaskY - 8);
        liquidPath.Close();
        canvas.DrawPath(liquidPath, _fillPaint);
        // Blasen (animiert)
        _fillPaint.Color = violetColor.WithAlpha(60);
        float bub1Y = flaskY - 10 - bubblePhase * 12;
        float bub2Y = flaskY - 6 - PingPong(phase + 0.5f, 1.0f) * 10;
        canvas.DrawCircle(flaskX - 2, bub1Y, 2, _fillPaint);
        canvas.DrawCircle(flaskX + 3, bub2Y, 1.5f, _fillPaint);
        canvas.DrawCircle(flaskX - 1, bub2Y + 4, 1, _fillPaint);

        // --- Rotierende Zahnräder (rechts oben) ---
        float gearCx = left + w * 0.78f;
        float gearCy = cy - h * 0.12f;
        float gearR1 = 16;
        float gearR2 = 10;
        float gearAngle1 = phase * 1.5f;
        float gearAngle2 = -phase * 1.5f * (gearR1 / gearR2); // Gegenläufig, korrekte Übersetzung

        // Großes Zahnrad
        _strokePaint.Color = new SKColor(0x80, 0x80, 0x90);
        _strokePaint.StrokeWidth = 2;
        canvas.DrawCircle(gearCx, gearCy, gearR1, _strokePaint);
        canvas.DrawCircle(gearCx, gearCy, gearR1 * 0.45f, _strokePaint);
        // Zähne
        for (int t = 0; t < 8; t++)
        {
            float a = gearAngle1 + t * MathF.PI / 4;
            canvas.DrawLine(
                gearCx + MathF.Cos(a) * gearR1 * 0.85f,
                gearCy + MathF.Sin(a) * gearR1 * 0.85f,
                gearCx + MathF.Cos(a) * (gearR1 + 4),
                gearCy + MathF.Sin(a) * (gearR1 + 4),
                _strokePaint);
        }

        // Kleines Zahnrad (versetzt, gegenläufig)
        float gear2Cx = gearCx + gearR1 + gearR2 - 4;
        float gear2Cy = gearCy - gearR2 * 0.3f;
        canvas.DrawCircle(gear2Cx, gear2Cy, gearR2, _strokePaint);
        canvas.DrawCircle(gear2Cx, gear2Cy, gearR2 * 0.4f, _strokePaint);
        for (int t = 0; t < 6; t++)
        {
            float a = gearAngle2 + t * MathF.PI / 3;
            canvas.DrawLine(
                gear2Cx + MathF.Cos(a) * gearR2 * 0.8f,
                gear2Cy + MathF.Sin(a) * gearR2 * 0.8f,
                gear2Cx + MathF.Cos(a) * (gearR2 + 3),
                gear2Cy + MathF.Sin(a) * (gearR2 + 3),
                _strokePaint);
        }

        // --- Leuchtende Glühbirne (oben Mitte) ---
        float bulbCx = cx + w * 0.05f;
        float bulbCy = top + h * 0.08f;
        float bulbR = 10;
        float bulbPulse = 0.6f + MathF.Sin(phase * 2.5f) * 0.4f;

        // Glow um Birne
        DrawGlow(canvas, bulbCx, bulbCy, bulbR * 2.5f, new SKColor(0xFF, 0xF1, 0x76, (byte)(20 + bulbPulse * 25)), _glowMedium);
        // Glaskolben
        _fillPaint.Color = new SKColor(0xFF, 0xF1, 0x76, (byte)(120 + bulbPulse * 80));
        canvas.DrawCircle(bulbCx, bulbCy, bulbR, _fillPaint);
        _strokePaint.Color = new SKColor(0xFF, 0xF1, 0x76, 0xC0);
        _strokePaint.StrokeWidth = 1;
        canvas.DrawCircle(bulbCx, bulbCy, bulbR, _strokePaint);
        // Sockel
        _fillPaint.Color = new SKColor(0x90, 0x90, 0x90);
        canvas.DrawRect(bulbCx - 4, bulbCy + bulbR - 1, 8, 5, _fillPaint);
        // Glühdraht
        _strokePaint.Color = new SKColor(0xFF, 0xD5, 0x4F, 0xB0);
        _strokePaint.StrokeWidth = 1;
        canvas.DrawLine(bulbCx - 2, bulbCy + 2, bulbCx, bulbCy - 4, _strokePaint);
        canvas.DrawLine(bulbCx, bulbCy - 4, bulbCx + 2, bulbCy + 2, _strokePaint);

        // --- Erfindung/Prototyp auf dem Tisch (rechts) ---
        float protoX = tableX + tableW * 0.6f;
        float protoY = tableY - 18;

        // Kleines Gerät (Box mit Antennen)
        _fillPaint.Color = new SKColor(0x50, 0x50, 0x60);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(protoX - 10, protoY, protoX + 10, protoY + 16), 2), _fillPaint);
        // Bildschirm
        _fillPaint.Color = violetColor.WithAlpha(80);
        canvas.DrawRect(protoX - 7, protoY + 2, 14, 8, _fillPaint);
        // Antenne
        _strokePaint.Color = new SKColor(0xA0, 0xA0, 0xA0);
        _strokePaint.StrokeWidth = 1;
        canvas.DrawLine(protoX + 6, protoY, protoX + 10, protoY - 10, _strokePaint);
        // Blinkende LED
        _fillPaint.Color = new SKColor(0x00, 0xFF, 0x00, (byte)(80 + bulbPulse * 120));
        canvas.DrawCircle(protoX - 5, protoY + 13, 1.5f, _fillPaint);

        // --- Dampf/Rauch-Partikel aus Kolben ---
        float steamPhase = (phase * 3) % MathF.Tau;
        if (steamPhase > MathF.PI - 0.5f && steamPhase < MathF.PI + 0.5f)
        {
            for (int p = 0; p < particleRate + 2; p++)
                addWorkParticle(flaskX, flaskY - 30, new SKColor(0x90, 0x90, 0xB0));
        }

        // --- Blaupause-Linien auf Tisch (dezent) ---
        _strokePaint.Color = violetColor.WithAlpha(25);
        _strokePaint.StrokeWidth = 0.5f;
        float bpX = tableX + tableW * 0.35f;
        float bpY = tableY - 1;
        canvas.DrawRect(bpX, bpY - 12, 24, 12, _strokePaint);
        canvas.DrawLine(bpX + 6, bpY - 12, bpX + 6, bpY, _strokePaint);
        canvas.DrawLine(bpX, bpY - 6, bpX + 24, bpY - 6, _strokePaint);

        // --- Worker-Figur links am Tisch ---
        DrawStickFigure(canvas, tableX - 10, tableY + tableH + 20, 0.9f,
            violetColor, phase * 1.5f, phase * 2, type);

        // 4+ Worker: Zweiter Arbeiter an den Zahnrädern
        if (activeWorkers >= 4)
        {
            DrawStickFigure(canvas, gearCx - 15, gearCy + 35, 0.8f,
                new SKColor(0x8A, 0x7A, 0xBD), phase * 2, phase * 1.5f, type);
        }

        // Münz-Emission
        TryEmitCoin(phase, activeWorkers, level, cx, tableY - 20, addCoinParticle);
    }

    /// <summary>
    /// Ressourcen freigeben.
    /// </summary>
    public void Dispose()
    {
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _glowPaint.Dispose();
        _shadowSmall.Dispose();
        _shadowMedium.Dispose();
        _shadowLarge.Dispose();
        _glowSmall.Dispose();
        _glowMedium.Dispose();
    }
}
